using System;
using System.Drawing;
using System.Windows.Forms;

namespace FishPeek;

public class ControlPanelForm : Form
{
    private readonly FishController _controller;

    // 状态控件
    private Label _lblTargetTitle = null!;
    private Label _lblVisibility = null!;
    private Button _btnPick = null!;
    private Button _btnToggle = null!;
    private Button _btnUnbind = null!;

    // 快捷键输入控件
    private HotkeyInputBox _boxPickKey = null!;
    private HotkeyInputBox _boxToggleKey = null!;
    private HotkeyInputBox _boxUnbindKey = null!;
    private HotkeyInputBox _boxKillKey = null!;

    // 功能设置控件
    private CheckBox _chkHoverEnabled = null!;
    private CheckBox _chkNoDelay = null!;
    private NumericUpDown _numDelay = null!;
    private Label _lblDelayPrompt = null!;
    private Label _lblDelayUnit = null!;

    private CheckBox _chkMuteOnHide = null!;
    private CheckBox _chkEnableKillKey = null!;

    private Button _btnSave = null!;
    private Button _btnReset = null!;
    private Button _btnClose = null!;

    public ControlPanelForm(FishController controller)
    {
        _controller = controller;
        InitializeComponent();
        LoadConfigToUi();

        _controller.TargetChanged += OnTargetChanged;
        _controller.VisibilityChanged += OnVisibilityChanged;
        _controller.PickingStateChanged += OnPickingStateChanged;
    }

    private void InitializeComponent()
    {
        Text = "摸鱼助手 - 控制面板";
        ClientSize = new Size(580, 680);
        MinimumSize = new Size(560, 650);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        AutoScaleMode = AutoScaleMode.Font;

        // ================= 底部固定操作栏 (自适应按钮高度，留足留白) =================
        var pnlBottom = new Panel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(16, 12, 16, 16),
            BackColor = Color.FromArgb(246, 248, 250)
        };

        var flowActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };

        _btnSave = new Button
        {
            Text = "💾 保存并应用设置",
            AutoSize = true,
            Padding = new Padding(16, 8, 16, 8),
            Margin = new Padding(0, 0, 14, 0),
            Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold),
            BackColor = Color.FromArgb(220, 240, 255),
            Cursor = Cursors.Hand
        };
        _btnSave.Click += OnSaveClicked;

        _btnReset = new Button
        {
            Text = "🔄 恢复默认设置",
            AutoSize = true,
            Padding = new Padding(14, 8, 14, 8),
            Margin = new Padding(0, 0, 14, 0),
            Cursor = Cursors.Hand
        };
        _btnReset.Click += OnResetClicked;

        _btnClose = new Button
        {
            Text = "隐藏到托盘",
            AutoSize = true,
            Padding = new Padding(14, 8, 14, 8),
            Margin = new Padding(0, 0, 0, 0),
            Cursor = Cursors.Hand
        };
        _btnClose.Click += (s, e) => Hide();

        flowActions.Controls.AddRange(new Control[] { _btnSave, _btnReset, _btnClose });
        pnlBottom.Controls.Add(flowActions);
        Controls.Add(pnlBottom);
        pnlBottom.BringToFront();

        // ================= 中间可滚动内容区域 =================
        var pnlContent = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(14, 8, 14, 8)
        };

        var layoutContent = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 3,
            AutoSize = true
        };
        layoutContent.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layoutContent.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layoutContent.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        // ----------------- 1. 当前状态区 -----------------
        var grpStatus = new GroupBox
        {
            Text = "📌 当前选择状态",
            Dock = DockStyle.Top,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 10),
            Padding = new Padding(12, 10, 12, 10)
        };

        var pnlStatusContent = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true
        };

        _lblTargetTitle = new Label
        {
            Text = "目标窗口：未选择",
            AutoSize = true,
            Margin = new Padding(0, 2, 0, 4),
            Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold)
        };

        _lblVisibility = new Label
        {
            Text = "显隐状态：无",
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 8),
            ForeColor = Color.DarkSlateGray
        };

        var flowBtns = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 2)
        };

        _btnPick = new Button
        {
            Text = "🎯 选择鼠标下窗口",
            AutoSize = true,
            Padding = new Padding(10, 4, 10, 4),
            Margin = new Padding(0, 0, 10, 0),
            Cursor = Cursors.Hand
        };
        _btnPick.Click += (s, e) => _controller.StartInteractivePick(Handle);

        _btnToggle = new Button
        {
            Text = "👁️ 显隐一键切换",
            AutoSize = true,
            Padding = new Padding(10, 4, 10, 4),
            Margin = new Padding(0, 0, 10, 0),
            Cursor = Cursors.Hand
        };
        _btnToggle.Click += (s, e) => _controller.ToggleVisibility();

        _btnUnbind = new Button
        {
            Text = "🔓 解除选择窗口",
            AutoSize = true,
            Padding = new Padding(10, 4, 10, 4),
            Margin = new Padding(0, 0, 0, 0),
            Cursor = Cursors.Hand
        };
        _btnUnbind.Click += (s, e) => _controller.UnbindTarget();

        flowBtns.Controls.AddRange(new Control[] { _btnPick, _btnToggle, _btnUnbind });
        pnlStatusContent.Controls.AddRange(new Control[] { _lblTargetTitle, _lblVisibility, flowBtns });
        grpStatus.Controls.Add(pnlStatusContent);
        layoutContent.Controls.Add(grpStatus);

        // ----------------- 2. 快捷键设置区 -----------------
        var grpHotkeys = new GroupBox
        {
            Text = "⌨️ 全局快捷键设置",
            Dock = DockStyle.Top,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 10),
            Padding = new Padding(12, 10, 12, 10)
        };

        var tblKeys = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 5,
            AutoSize = true,
            Padding = new Padding(2)
        };
        tblKeys.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        tblKeys.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        // 选择目标
        var lblPick = new Label { Text = "🎯 选择目标窗口：", Anchor = AnchorStyles.Left, AutoSize = true, Margin = new Padding(0, 3, 8, 3) };
        _boxPickKey = new HotkeyInputBox { Width = 230, Anchor = AnchorStyles.Left, Margin = new Padding(0, 3, 0, 3) };
        tblKeys.Controls.Add(lblPick, 0, 0);
        tblKeys.Controls.Add(_boxPickKey, 1, 0);

        // 显隐老板键
        var lblToggle = new Label { Text = "👁️ 显隐老板键：", Anchor = AnchorStyles.Left, AutoSize = true, Margin = new Padding(0, 3, 8, 3) };
        _boxToggleKey = new HotkeyInputBox { Width = 230, Anchor = AnchorStyles.Left, Margin = new Padding(0, 3, 0, 3) };
        tblKeys.Controls.Add(lblToggle, 0, 1);
        tblKeys.Controls.Add(_boxToggleKey, 1, 1);

        // 解除选择
        var lblUnbind = new Label { Text = "🔓 解除选择窗口：", Anchor = AnchorStyles.Left, AutoSize = true, Margin = new Padding(0, 3, 8, 3) };
        _boxUnbindKey = new HotkeyInputBox { Width = 230, Anchor = AnchorStyles.Left, Margin = new Padding(0, 3, 0, 3) };
        tblKeys.Controls.Add(lblUnbind, 0, 2);
        tblKeys.Controls.Add(_boxUnbindKey, 1, 2);

        // 关闭所选窗口并退出
        var lblKill = new Label { Text = "✖  关闭所选窗口并退出：", Anchor = AnchorStyles.Left, AutoSize = true, Margin = new Padding(0, 3, 8, 3) };
        _boxKillKey = new HotkeyInputBox { Width = 230, Anchor = AnchorStyles.Left, Margin = new Padding(0, 3, 0, 3) };
        tblKeys.Controls.Add(lblKill, 0, 3);
        tblKeys.Controls.Add(_boxKillKey, 1, 3);

        // 提示说明
        var lblKeyHint = new Label
        {
            Text = "💡 说明：点击上方输入框激活后，直接在键盘上按下新按键组合即可录入",
            ForeColor = Color.DimGray,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 6, 0, 2)
        };
        tblKeys.SetColumnSpan(lblKeyHint, 2);
        tblKeys.Controls.Add(lblKeyHint, 0, 4);

        grpHotkeys.Controls.Add(tblKeys);
        layoutContent.Controls.Add(grpHotkeys);

        // ----------------- 3. 功能与悬停设置区 -----------------
        var grpOptions = new GroupBox
        {
            Text = "⚡ 功能机制与悬停窥视设置",
            Dock = DockStyle.Top,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 6),
            Padding = new Padding(12, 10, 12, 10)
        };

        var pnlOptionsContent = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true
        };

        _chkHoverEnabled = new CheckBox
        {
            Text = "启用鼠标移入/移出自动显隐（移出窗口立即隐藏）",
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
            Margin = new Padding(0, 2, 0, 4)
        };

        _chkNoDelay = new CheckBox
        {
            Text = "取消延迟（光标一旦划入原区域即刻显形，0 毫秒）",
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 4)
        };
        _chkNoDelay.CheckedChanged += (s, e) =>
        {
            bool custom = !_chkNoDelay.Checked;
            _lblDelayPrompt.Enabled = custom;
            _numDelay.Enabled = custom;
            _lblDelayUnit.Enabled = custom;
        };

        var flowDelay = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            Margin = new Padding(16, 0, 0, 6)
        };

        _lblDelayPrompt = new Label
        {
            Text = "自定义悬停延迟时间：",
            Anchor = AnchorStyles.Left,
            AutoSize = true,
            Margin = new Padding(0, 4, 4, 4)
        };

        _numDelay = new NumericUpDown
        {
            Width = 85,
            Minimum = 0,
            Maximum = 5000,
            Increment = 50,
            TextAlign = HorizontalAlignment.Center,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(2, 2, 4, 2)
        };

        _lblDelayUnit = new Label
        {
            Text = "毫秒 (ms)",
            Anchor = AnchorStyles.Left,
            AutoSize = true,
            Margin = new Padding(2, 4, 0, 4)
        };

        flowDelay.Controls.AddRange(new Control[] { _lblDelayPrompt, _numDelay, _lblDelayUnit });

        _chkMuteOnHide = new CheckBox
        {
            Text = "隐藏时自动静音该应用，显示时恢复声音",
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
            Margin = new Padding(0, 4, 0, 4)
        };

        _chkEnableKillKey = new CheckBox
        {
            Text = "启用快捷键一键关闭所选窗口并退出本程序",
            AutoSize = true,
            Margin = new Padding(0, 2, 0, 2)
        };
        _chkEnableKillKey.CheckedChanged += (s, e) =>
        {
            _boxKillKey.Enabled = _chkEnableKillKey.Checked;
        };

        pnlOptionsContent.Controls.AddRange(new Control[]
        {
            _chkHoverEnabled,
            _chkNoDelay,
            flowDelay,
            _chkMuteOnHide,
            _chkEnableKillKey
        });

        grpOptions.Controls.Add(pnlOptionsContent);
        layoutContent.Controls.Add(grpOptions);

        pnlContent.Controls.Add(layoutContent);
        Controls.Add(pnlContent);
    }

    private void LoadConfigToUi()
    {
        var cfg = _controller.Config;

        _boxPickKey.Hotkey = cfg.PickKey;
        _boxToggleKey.Hotkey = cfg.ToggleKey;
        _boxUnbindKey.Hotkey = cfg.UnbindKey;
        _boxKillKey.Hotkey = cfg.KillAppKey;

        _chkHoverEnabled.Checked = cfg.HoverPeekEnabled;
        _chkNoDelay.Checked = cfg.NoDelay;
        _numDelay.Value = Math.Clamp(cfg.HoverDelayMs, 0, 5000);

        bool custom = !cfg.NoDelay;
        _lblDelayPrompt.Enabled = custom;
        _numDelay.Enabled = custom;
        _lblDelayUnit.Enabled = custom;

        _chkMuteOnHide.Checked = cfg.MuteOnHide;
        _chkEnableKillKey.Checked = cfg.EnableKillAppKey;
        _boxKillKey.Enabled = cfg.EnableKillAppKey;

        UpdateStatusText();
    }

    private void OnSaveClicked(object? sender, EventArgs e)
    {
        var newConfig = new AppConfig
        {
            PickKey = _boxPickKey.Hotkey,
            ToggleKey = _boxToggleKey.Hotkey,
            UnbindKey = _boxUnbindKey.Hotkey,
            KillAppKey = _boxKillKey.Hotkey,
            HoverPeekEnabled = _chkHoverEnabled.Checked,
            NoDelay = _chkNoDelay.Checked,
            HoverDelayMs = (int)_numDelay.Value,
            MuteOnHide = _chkMuteOnHide.Checked,
            EnableKillAppKey = _chkEnableKillKey.Checked
        };

        _controller.ApplyConfig(newConfig);

        MessageBox.Show(
            "设置已成功保存并立即生效！\n\n" +
            $"• 选择目标快捷键：{newConfig.PickKey}\n" +
            $"• 显隐老板键：{newConfig.ToggleKey}\n" +
            $"• 解除选择快捷键：{newConfig.UnbindKey}\n" +
            $"• 关闭窗口并退出快捷键：{(newConfig.EnableKillAppKey ? newConfig.KillAppKey.ToString() : "已停用")}\n" +
            $"• 隐藏时自动静音：{(newConfig.MuteOnHide ? "已启用" : "已停用")}\n" +
            $"• 悬停延迟：{(newConfig.NoDelay ? "0ms (无延迟即显)" : $"{newConfig.HoverDelayMs}ms")}",
            "摸鱼助手",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information
        );
    }

    private void OnResetClicked(object? sender, EventArgs e)
    {
        if (MessageBox.Show("确定要将所有设置恢复为默认配置吗？", "提示", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            var defConfig = new AppConfig();
            _controller.ApplyConfig(defConfig);
            LoadConfigToUi();
        }
    }

    private void OnTargetChanged(string title)
    {
        if (InvokeRequired)
        {
            Invoke(() => UpdateStatusText());
        }
        else
        {
            UpdateStatusText();
        }
    }

    private void OnVisibilityChanged(bool isVisible)
    {
        if (InvokeRequired)
        {
            Invoke(() => UpdateStatusText());
        }
        else
        {
            UpdateStatusText();
        }
    }

    private void OnPickingStateChanged(bool isPicking)
    {
        if (InvokeRequired)
        {
            Invoke(() => OnPickingStateChanged(isPicking));
            return;
        }

        if (isPicking)
        {
            _btnPick.Text = "⏳ 请点击目标窗口 (按Esc取消)";
            _btnPick.BackColor = Color.FromArgb(255, 230, 180);
            _lblVisibility.Text = "显隐状态：请在屏幕或任务栏上点击想要选择的目标应用窗口...";
            _lblVisibility.ForeColor = Color.DarkOrange;
        }
        else
        {
            _btnPick.Text = "🎯 选择鼠标下窗口";
            _btnPick.BackColor = SystemColors.Control;
            UpdateStatusText();
        }
    }

    private void UpdateStatusText()
    {
        if (string.IsNullOrEmpty(_controller.TargetTitle))
        {
            _lblTargetTitle.Text = "目标窗口：未选择";
            _lblTargetTitle.ForeColor = Color.Black;
            _lblVisibility.Text = "显隐状态：无";
            _lblVisibility.ForeColor = Color.Gray;
        }
        else
        {
            _lblTargetTitle.Text = $"目标窗口：{_controller.TargetTitle}";
            _lblTargetTitle.ForeColor = Color.DarkBlue;
            _lblVisibility.Text = _controller.IsHidden ? "显隐状态：已隐形 (移入悬停或按快捷键可显现)" : "显隐状态：正常可见";
            _lblVisibility.ForeColor = _controller.IsHidden ? Color.OrangeRed : Color.ForestGreen;
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        base.OnFormClosing(e);
    }
}
