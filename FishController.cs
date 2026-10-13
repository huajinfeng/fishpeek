using System;
using System.Diagnostics;
using System.Text;
using System.Windows.Forms;

namespace FishPeek;

public class FishController : IDisposable
{
    public const int HOTKEY_PICK = 1;      // 捕获光标下窗口
    public const int HOTKEY_TOGGLE = 2;    // 显隐老板键
    public const int HOTKEY_UNBIND = 3;    // 解除选择
    public const int HOTKEY_KILL = 4;      // 一键关闭应用

    private readonly HotkeyMessageWindow _hotkeyWindow;
    private readonly System.Windows.Forms.Timer _pollTimer;

    public AppConfig Config { get; private set; }

    public IntPtr TargetHwnd { get; private set; } = IntPtr.Zero;
    public uint TargetProcessId { get; private set; } = 0;
    public string TargetTitle { get; private set; } = string.Empty;
    public NativeMethods.RECT TargetRect { get; private set; }
    public bool IsHidden { get; private set; } = false;

    private DateTime? _hoverStartTime = null;

    public bool IsPicking { get; private set; } = false;
    private CancellationTokenSource? _pickCts = null;

    public event Action<string>? TargetChanged;
    public event Action<bool>? VisibilityChanged;
    public event Action<string>? NotifyMessageRequested;
    public event Action? ConfigApplied;
    public event Action<bool>? PickingStateChanged;
    public event Action? ApplicationExitRequested;

    public FishController(AppConfig? initialConfig = null)
    {
        Config = initialConfig ?? AppConfig.Load();

        _hotkeyWindow = new HotkeyMessageWindow();
        _hotkeyWindow.HotkeyPressed += OnHotkeyPressed;

        RegisterHotkeys();

        _pollTimer = new System.Windows.Forms.Timer
        {
            Interval = 40
        };
        _pollTimer.Tick += OnPollTick;
        _pollTimer.Start();
    }

    public void ApplyConfig(AppConfig newConfig)
    {
        Config = newConfig;
        Config.Save();

        // 重新注册热键
        RegisterHotkeys();

        // 若当前窗口处于隐藏状态，根据最新静音配置立即更新声音状态
        if (TargetProcessId > 0 && IsHidden)
        {
            AudioSessionHelper.SetProcessMute((int)TargetProcessId, Config.MuteOnHide);
        }

        ConfigApplied?.Invoke();
    }

    public void RegisterHotkeys()
    {
        // 先注销旧热键
        _hotkeyWindow.UnregisterKey(HOTKEY_PICK);
        _hotkeyWindow.UnregisterKey(HOTKEY_TOGGLE);
        _hotkeyWindow.UnregisterKey(HOTKEY_UNBIND);
        _hotkeyWindow.UnregisterKey(HOTKEY_KILL);

        // 注册新热键
        _hotkeyWindow.RegisterKey(HOTKEY_PICK, Config.PickKey.GetModifiers(), Config.PickKey.Key);
        _hotkeyWindow.RegisterKey(HOTKEY_TOGGLE, Config.ToggleKey.GetModifiers(), Config.ToggleKey.Key);
        _hotkeyWindow.RegisterKey(HOTKEY_UNBIND, Config.UnbindKey.GetModifiers(), Config.UnbindKey.Key);

        if (Config.EnableKillAppKey)
        {
            _hotkeyWindow.RegisterKey(HOTKEY_KILL, Config.KillAppKey.GetModifiers(), Config.KillAppKey.Key);
        }
    }

    private void OnHotkeyPressed(int id)
    {
        switch (id)
        {
            case HOTKEY_PICK:
                PickWindowUnderCursor();
                break;
            case HOTKEY_TOGGLE:
                ToggleVisibility();
                break;
            case HOTKEY_UNBIND:
                UnbindTarget();
                break;
            case HOTKEY_KILL:
                KillTargetApplication();
                break;
        }
    }

    public void PickWindowUnderCursor()
    {
        if (!NativeMethods.GetCursorPos(out var pt)) return;

        IntPtr hwnd = NativeMethods.WindowFromPoint(pt);
        if (hwnd == IntPtr.Zero) return;

        IntPtr rootHwnd = NativeMethods.GetAncestor(hwnd, NativeMethods.GA_ROOT);
        if (rootHwnd != IntPtr.Zero)
        {
            hwnd = rootHwnd;
        }

        BindTargetWindow(hwnd);
    }

    public void BindTargetWindow(IntPtr hwnd)
    {
        if (!IsValidTargetWindow(hwnd))
        {
            NotifyMessageRequested?.Invoke("当前选中的是桌面或系统保留窗口，无法选择。");
            return;
        }

        if (TargetHwnd != IntPtr.Zero && IsHidden && NativeMethods.IsWindow(TargetHwnd))
        {
            NativeMethods.ShowWindow(TargetHwnd, NativeMethods.SW_SHOW);
        }

        TargetHwnd = hwnd;
        NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
        TargetProcessId = pid;
        TargetTitle = GetWindowCaption(hwnd);

        if (NativeMethods.GetWindowRect(hwnd, out var rect))
        {
            TargetRect = rect;
        }

        IsHidden = false;
        _hoverStartTime = null;

        TargetChanged?.Invoke(TargetTitle);
        VisibilityChanged?.Invoke(true);
        NotifyMessageRequested?.Invoke($"已选择窗口：\n[{TargetTitle}]");
    }

    public void StartInteractivePick(IntPtr? excludeFormHandle = null)
    {
        if (IsPicking)
        {
            CancelInteractivePick();
            return;
        }

        IsPicking = true;
        PickingStateChanged?.Invoke(true);
        _pickCts?.Cancel();
        _pickCts = new CancellationTokenSource();
        var token = _pickCts.Token;

        Task.Run(async () =>
        {
            try
            {
                // 1. 等待当前鼠标左键抬起释放（避免点击按钮的动作被误认）
                while ((NativeMethods.GetAsyncKeyState(NativeMethods.VK_LBUTTON) & 0x8000) != 0)
                {
                    if (token.IsCancellationRequested) return;
                    await Task.Delay(20, token);
                }

                // 2. 循环等待下一次全局点击或取消
                while (!token.IsCancellationRequested)
                {
                    // 检测 ESC 或右键取消
                    if ((NativeMethods.GetAsyncKeyState(NativeMethods.VK_ESCAPE) & 0x8000) != 0 ||
                        (NativeMethods.GetAsyncKeyState(NativeMethods.VK_RBUTTON) & 0x8000) != 0)
                    {
                        NotifyMessageRequested?.Invoke("已取消窗口选择。");
                        return;
                    }

                    // 检测左键按下
                    if ((NativeMethods.GetAsyncKeyState(NativeMethods.VK_LBUTTON) & 0x8000) != 0)
                    {
                        IntPtr clickedRoot = IntPtr.Zero;
                        if (NativeMethods.GetCursorPos(out var pt))
                        {
                            IntPtr clickedHwnd = NativeMethods.WindowFromPoint(pt);
                            if (clickedHwnd != IntPtr.Zero)
                            {
                                clickedRoot = NativeMethods.GetAncestor(clickedHwnd, NativeMethods.GA_ROOT);
                                if (clickedRoot == IntPtr.Zero) clickedRoot = clickedHwnd;
                            }
                        }

                        // 如果点击的是控制面板自身，忽略并等待释放
                        if (excludeFormHandle.HasValue && clickedRoot == excludeFormHandle.Value)
                        {
                            while ((NativeMethods.GetAsyncKeyState(NativeMethods.VK_LBUTTON) & 0x8000) != 0)
                            {
                                if (token.IsCancellationRequested) return;
                                await Task.Delay(20, token);
                            }
                            continue;
                        }

                        // 等待左键释放
                        while ((NativeMethods.GetAsyncKeyState(NativeMethods.VK_LBUTTON) & 0x8000) != 0)
                        {
                            if (token.IsCancellationRequested) return;
                            await Task.Delay(20, token);
                        }

                        // 关键：当用户在任务栏某个缩略图卡片上点击后，Windows 会立即将该特定窗口激活置于前台！
                        // 轮询检测前台激活窗口（最多等待 600ms）
                        IntPtr candidateHwnd = IntPtr.Zero;
                        for (int i = 0; i < 20; i++)
                        {
                            if (token.IsCancellationRequested) return;
                            await Task.Delay(30, token);

                            IntPtr fg = NativeMethods.GetForegroundWindow();
                            if (fg != IntPtr.Zero && IsValidTargetWindow(fg, excludeFormHandle))
                            {
                                candidateHwnd = fg;
                                break;
                            }
                        }

                        // 若前台窗口激活成功，优先绑定被激活的特定窗口
                        if (candidateHwnd != IntPtr.Zero && IsValidTargetWindow(candidateHwnd, excludeFormHandle))
                        {
                            BindTargetWindow(candidateHwnd);
                            return;
                        }

                        // 兜底：如果前台未切换但点击的不是任务栏桌面等系统窗口，绑定点击的窗口
                        if (clickedRoot != IntPtr.Zero && IsValidTargetWindow(clickedRoot, excludeFormHandle))
                        {
                            BindTargetWindow(clickedRoot);
                            return;
                        }
                    }

                    await Task.Delay(20, token);
                }
            }
            catch (OperationCanceledException)
            {
                // 正常取消
            }
            finally
            {
                IsPicking = false;
                PickingStateChanged?.Invoke(false);
            }
        }, token);
    }

    public void CancelInteractivePick()
    {
        if (IsPicking && _pickCts != null)
        {
            _pickCts.Cancel();
            IsPicking = false;
            PickingStateChanged?.Invoke(false);
        }
    }

    public void SetWindowVisibleState(bool show)
    {
        if (TargetHwnd == IntPtr.Zero || !NativeMethods.IsWindow(TargetHwnd)) return;

        if (show)
        {
            NativeMethods.ShowWindow(TargetHwnd, NativeMethods.SW_SHOW);
            IsHidden = false;
            _hoverStartTime = null;

            if (Config.MuteOnHide && TargetProcessId > 0)
            {
                AudioSessionHelper.SetProcessMute((int)TargetProcessId, false);
            }

            VisibilityChanged?.Invoke(true);
        }
        else
        {
            if (NativeMethods.GetWindowRect(TargetHwnd, out var rect))
            {
                TargetRect = rect;
            }

            NativeMethods.ShowWindow(TargetHwnd, NativeMethods.SW_HIDE);
            IsHidden = true;
            _hoverStartTime = null;

            if (Config.MuteOnHide && TargetProcessId > 0)
            {
                AudioSessionHelper.SetProcessMute((int)TargetProcessId, true);
            }

            VisibilityChanged?.Invoke(false);
        }
    }

    public void ToggleVisibility()
    {
        if (TargetHwnd == IntPtr.Zero || !NativeMethods.IsWindow(TargetHwnd))
        {
            NotifyMessageRequested?.Invoke("尚未选择任何窗口，请将光标移至目标窗口并按快捷键选择。");
            return;
        }

        if (IsHidden)
        {
            SetWindowVisibleState(true);
            NativeMethods.SetForegroundWindow(TargetHwnd);
        }
        else
        {
            SetWindowVisibleState(false);
        }
    }

    public void KillTargetApplication()
    {
        if (TargetHwnd != IntPtr.Zero)
        {
            string title = TargetTitle;
            uint pid = TargetProcessId;
            IntPtr hwnd = TargetHwnd;

            // 恢复声音
            if (pid > 0)
            {
                AudioSessionHelper.SetProcessMute((int)pid, false);
            }

            // 优雅关闭窗口
            NativeMethods.PostMessage(hwnd, NativeMethods.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);

            // 若仍未退出，则安全结束进程
            if (pid > 0)
            {
                try
                {
                    using var proc = Process.GetProcessById((int)pid);
                    if (!proc.WaitForExit(300))
                    {
                        proc.Kill();
                    }
                }
                catch
                {
                    // 忽略进程已退出异常
                }
            }

            // 解除选择状态
            TargetHwnd = IntPtr.Zero;
            TargetTitle = string.Empty;
            TargetProcessId = 0;
            IsHidden = false;
            _hoverStartTime = null;

            TargetChanged?.Invoke(string.Empty);
            VisibilityChanged?.Invoke(true);
        }

        // 联动退出 FishPeek 自身
        ApplicationExitRequested?.Invoke();
    }

    public void UnbindTarget()
    {
        if (TargetHwnd != IntPtr.Zero)
        {
            if (IsHidden && NativeMethods.IsWindow(TargetHwnd))
            {
                NativeMethods.ShowWindow(TargetHwnd, NativeMethods.SW_SHOW);
            }

            if (TargetProcessId > 0)
            {
                AudioSessionHelper.SetProcessMute((int)TargetProcessId, false);
            }

            TargetHwnd = IntPtr.Zero;
            TargetTitle = string.Empty;
            TargetProcessId = 0;
            IsHidden = false;
            _hoverStartTime = null;
            TargetChanged?.Invoke(string.Empty);
            VisibilityChanged?.Invoke(true);
            NotifyMessageRequested?.Invoke("已解除选择窗口。");
        }
    }

    private void OnPollTick(object? sender, EventArgs e)
    {
        if (TargetHwnd == IntPtr.Zero) return;

        if (!NativeMethods.IsWindow(TargetHwnd))
        {
            UnbindTarget();
            return;
        }

        if (!Config.HoverPeekEnabled) return;

        if (!NativeMethods.GetCursorPos(out var pt)) return;

        int effectiveDelay = Config.NoDelay ? 0 : Math.Max(0, Config.HoverDelayMs);

        if (IsHidden)
        {
            if (TargetRect.Contains(pt))
            {
                if (effectiveDelay == 0)
                {
                    SetWindowVisibleState(true);
                }
                else
                {
                    if (_hoverStartTime == null)
                    {
                        _hoverStartTime = DateTime.UtcNow;
                    }
                    else
                    {
                        double elapsed = (DateTime.UtcNow - _hoverStartTime.Value).TotalMilliseconds;
                        if (elapsed >= effectiveDelay)
                        {
                            SetWindowVisibleState(true);
                        }
                    }
                }
            }
            else
            {
                _hoverStartTime = null;
            }
        }
        else
        {
            if (NativeMethods.GetWindowRect(TargetHwnd, out var currentRect))
            {
                TargetRect = currentRect;
            }

            // 防抖容差 8px
            if (!TargetRect.Contains(pt, tolerance: 8))
            {
                SetWindowVisibleState(false);
            }
        }
    }

    private bool IsValidTargetWindow(IntPtr hwnd, IntPtr? excludeFormHandle = null)
    {
        if (hwnd == IntPtr.Zero) return false;
        if (hwnd == _hotkeyWindow.Handle) return false;
        if (excludeFormHandle.HasValue && hwnd == excludeFormHandle.Value) return false;

        StringBuilder sbClass = new StringBuilder(256);
        NativeMethods.GetClassName(hwnd, sbClass, sbClass.Capacity);
        string className = sbClass.ToString();

        if (className is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd"
            or "TaskListThumbnailWnd" or "Windows.UI.Core.CoreWindow" or "DV2ControlHost")
        {
            return false;
        }

        return true;
    }

    private string GetWindowCaption(IntPtr hwnd)
    {
        StringBuilder sb = new StringBuilder(512);
        NativeMethods.GetWindowText(hwnd, sb, sb.Capacity);
        string text = sb.ToString().Trim();
        if (string.IsNullOrEmpty(text))
        {
            StringBuilder sbClass = new StringBuilder(256);
            NativeMethods.GetClassName(hwnd, sbClass, sbClass.Capacity);
            return $"[类名: {sbClass}]";
        }
        return text;
    }

    public void Dispose()
    {
        CancelInteractivePick();
        _pollTimer.Stop();
        _pollTimer.Dispose();

        if (TargetHwnd != IntPtr.Zero && IsHidden && NativeMethods.IsWindow(TargetHwnd))
        {
            NativeMethods.ShowWindow(TargetHwnd, NativeMethods.SW_SHOW);
        }

        if (TargetProcessId > 0)
        {
            AudioSessionHelper.SetProcessMute((int)TargetProcessId, false);
        }

        _hotkeyWindow.UnregisterKey(HOTKEY_PICK);
        _hotkeyWindow.UnregisterKey(HOTKEY_TOGGLE);
        _hotkeyWindow.UnregisterKey(HOTKEY_UNBIND);
        _hotkeyWindow.UnregisterKey(HOTKEY_KILL);
        _hotkeyWindow.Dispose();
    }
}
