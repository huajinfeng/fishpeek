using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace FishPeek;

public class FishTrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly FishController _controller;
    private readonly ControlPanelForm _controlPanel;

    private readonly ToolStripMenuItem _targetStatusItem;
    private readonly ToolStripMenuItem _pickMenuItem;
    private readonly ToolStripMenuItem _toggleMenuItem;
    private readonly ToolStripMenuItem _unbindMenuItem;
    private readonly ToolStripMenuItem _killMenuItem;
    private readonly ToolStripMenuItem _hoverToggleItem;

    public FishTrayApplicationContext()
    {
        _controller = new FishController();
        _controlPanel = new ControlPanelForm(_controller);

        _controller.TargetChanged += OnTargetChanged;
        _controller.ConfigApplied += RefreshMenuLabels;
        _controller.NotifyMessageRequested += ShowBalloonTip;
        _controller.ApplicationExitRequested += ExitApplication;

        // 构建托盘右键菜单
        var contextMenu = new ContextMenuStrip();

        // 1. 打开控制面板（置顶）
        var openPanelItem = new ToolStripMenuItem("⚙️ 打开控制面板...", null, (s, e) => ShowControlPanel())
        {
            Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold)
        };
        contextMenu.Items.Add(openPanelItem);
        contextMenu.Items.Add(new ToolStripSeparator());

        _targetStatusItem = new ToolStripMenuItem("📌 当前目标：未选择")
        {
            Enabled = false
        };
        contextMenu.Items.Add(_targetStatusItem);
        contextMenu.Items.Add(new ToolStripSeparator());

        _pickMenuItem = new ToolStripMenuItem("🎯 选择光标所在窗口", null, (s, e) => _controller.StartInteractivePick());
        contextMenu.Items.Add(_pickMenuItem);

        _toggleMenuItem = new ToolStripMenuItem("👁️ 显隐一键切换", null, (s, e) => _controller.ToggleVisibility());
        contextMenu.Items.Add(_toggleMenuItem);

        _unbindMenuItem = new ToolStripMenuItem("🔓 解除选择窗口", null, (s, e) => _controller.UnbindTarget());
        contextMenu.Items.Add(_unbindMenuItem);

        _killMenuItem = new ToolStripMenuItem("🛑 关闭所选窗口并退出助手", null, (s, e) => _controller.KillTargetApplication());
        contextMenu.Items.Add(_killMenuItem);

        contextMenu.Items.Add(new ToolStripSeparator());

        _hoverToggleItem = new ToolStripMenuItem("⚡ 鼠标移入/移出自动显隐", null, OnToggleHover)
        {
            Checked = _controller.Config.HoverPeekEnabled,
            CheckOnClick = true
        };
        contextMenu.Items.Add(_hoverToggleItem);

        contextMenu.Items.Add(new ToolStripSeparator());

        var exitItem = new ToolStripMenuItem("🚪 退出摸鱼助手", null, (s, e) => ExitApplication());
        contextMenu.Items.Add(exitItem);

        RefreshMenuLabels();

        // 创建托盘图标
        Icon appIcon = CreateFishTrayIcon();

        _trayIcon = new NotifyIcon
        {
            Icon = appIcon,
            ContextMenuStrip = contextMenu,
            Text = "摸鱼助手 - FishPeek\n双击打开控制面板",
            Visible = true
        };

        _trayIcon.DoubleClick += (s, e) => ShowControlPanel();

        // 启动提示
        _trayIcon.ShowBalloonTip(
            3000,
            "摸鱼助手已就绪",
            $"按【{_controller.Config.PickKey}】选择目标窗口\n按【{_controller.Config.ToggleKey}】一键显隐\n按【{_controller.Config.UnbindKey}】解除选择窗口\n双击托盘图标可打开控制面板进行设置",
            ToolTipIcon.Info
        );
    }

    private void ShowControlPanel()
    {
        if (_controlPanel.Visible)
        {
            _controlPanel.Activate();
        }
        else
        {
            _controlPanel.Show();
            _controlPanel.BringToFront();
        }
    }

    private void RefreshMenuLabels()
    {
        var cfg = _controller.Config;
        _pickMenuItem.Text = $"🎯 选择光标所在窗口 ({cfg.PickKey})";
        _toggleMenuItem.Text = $"👁️ 显隐一键切换 ({cfg.ToggleKey})";
        _unbindMenuItem.Text = $"🔓 解除选择窗口 ({cfg.UnbindKey})";
        _killMenuItem.Visible = cfg.EnableKillAppKey;
        _killMenuItem.Text = $"🛑 关闭所选窗口并退出助手 ({cfg.KillAppKey})";
        _hoverToggleItem.Checked = cfg.HoverPeekEnabled;
    }

    private void OnToggleHover(object? sender, EventArgs e)
    {
        var cfg = _controller.Config;
        cfg.HoverPeekEnabled = _hoverToggleItem.Checked;
        _controller.ApplyConfig(cfg);
        string state = cfg.HoverPeekEnabled ? "已开启" : "已暂停";
        ShowBalloonTip($"鼠标悬停自动显隐功能 {state}");
    }

    private void OnTargetChanged(string title)
    {
        if (string.IsNullOrEmpty(title))
        {
            _targetStatusItem.Text = "📌 当前目标：未选择";
            _trayIcon.Text = "摸鱼助手 - FishPeek\n未选择窗口 (双击打开控制面板)";
        }
        else
        {
            string shortTitle = title.Length > 18 ? title.Substring(0, 15) + "..." : title;
            _targetStatusItem.Text = $"📌 选择目标: {shortTitle}";
            _trayIcon.Text = $"摸鱼助手 - 已选择:\n{shortTitle}";
        }
    }

    private void ShowBalloonTip(string message)
    {
        _trayIcon.ShowBalloonTip(2000, "摸鱼助手", message, ToolTipIcon.Info);
    }

    private Icon CreateFishTrayIcon()
    {
        using Bitmap bmp = new Bitmap(32, 32);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            // 绘制深青色渐变底圆
            using (var brush = new LinearGradientBrush(
                new Point(0, 0),
                new Point(32, 32),
                Color.FromArgb(20, 160, 240),
                Color.FromArgb(10, 80, 180)))
            {
                g.FillEllipse(brush, 1, 1, 30, 30);
            }

            // 绘制白色边框
            using (var pen = new Pen(Color.White, 1.5f))
            {
                g.DrawEllipse(pen, 2, 2, 28, 28);
            }

            // 绘制一条白色小鱼简笔画
            using (var penFish = new Pen(Color.White, 2.2f))
            {
                penFish.StartCap = LineCap.Round;
                penFish.EndCap = LineCap.Round;

                g.DrawArc(penFish, 6, 8, 16, 16, 30, 220);
                g.DrawArc(penFish, 6, 8, 16, 16, 150, 220);

                g.DrawLine(penFish, 20, 11, 26, 21);
                g.DrawLine(penFish, 20, 21, 26, 11);

                using (var eyeBrush = new SolidBrush(Color.White))
                {
                    g.FillEllipse(eyeBrush, 10, 14, 2.5f, 2.5f);
                }
            }
        }
        IntPtr hIcon = bmp.GetHicon();
        return Icon.FromHandle(hIcon);
    }

    private void ExitApplication()
    {
        _trayIcon.Visible = false;
        _controller.Dispose();
        _controlPanel.Dispose();
        _trayIcon.Dispose();
        Application.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _trayIcon.Visible = false;
            _controller.Dispose();
            _controlPanel.Dispose();
            _trayIcon.Dispose();
        }
        base.Dispose(disposing);
    }
}
