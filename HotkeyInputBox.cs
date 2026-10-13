using System;
using System.Drawing;
using System.Windows.Forms;

namespace FishPeek;

public class HotkeyInputBox : TextBox
{
    private HotkeyConfig _hotkey;

    public HotkeyConfig Hotkey
    {
        get => _hotkey;
        set
        {
            _hotkey = value ?? new HotkeyConfig();
            UpdateDisplayText();
        }
    }

    public event Action<HotkeyConfig>? HotkeyChanged;

    public HotkeyInputBox()
    {
        _hotkey = new HotkeyConfig();
        ReadOnly = true;
        BackColor = Color.White;
        Cursor = Cursors.Hand;
        TextAlign = HorizontalAlignment.Center;
        Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
        UpdateDisplayText();
    }

    private void UpdateDisplayText()
    {
        Text = _hotkey.ToString();
    }

    protected override void OnEnter(EventArgs e)
    {
        base.OnEnter(e);
        BackColor = Color.FromArgb(235, 245, 255);
        Text = "【请在键盘上按下快捷键】";
    }

    protected override void OnLeave(EventArgs e)
    {
        base.OnLeave(e);
        BackColor = Color.White;
        UpdateDisplayText();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        e.Handled = true;
        e.SuppressKeyPress = true;

        Keys key = e.KeyCode;

        // 如果用户单独按了 Backspace 或 Delete，可清空修饰并重置
        if (key is Keys.Back or Keys.Delete)
        {
            return;
        }

        // 单独按下修饰键时，只显示正在输入的状态
        if (key is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin)
        {
            var tempParts = new System.Collections.Generic.List<string>();
            if (e.Control) tempParts.Add("Ctrl");
            if (e.Alt) tempParts.Add("Alt");
            if (e.Shift) tempParts.Add("Shift");
            tempParts.Add("...");
            Text = string.Join(" + ", tempParts);
            return;
        }

        // 按下了实体按键
        bool hasCtrl = e.Control;
        bool hasAlt = e.Alt;
        bool hasShift = e.Shift;

        // 为了防止用户单按一个字母键导致日常打字冲突，如果没有修饰键且不是 F1~F12 等功能键，默认至少建议配合修饰键
        _hotkey = new HotkeyConfig
        {
            Ctrl = hasCtrl,
            Alt = hasAlt,
            Shift = hasShift,
            Key = key
        };

        UpdateDisplayText();
        HotkeyChanged?.Invoke(_hotkey);

        // 录制完成后让焦点移开，避免重复录入
        Parent?.SelectNextControl(this, true, true, true, true);
    }
}
