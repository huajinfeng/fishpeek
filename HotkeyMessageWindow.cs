using System;
using System.Windows.Forms;

namespace FishPeek;

public class HotkeyMessageWindow : NativeWindow, IDisposable
{
    public event Action<int>? HotkeyPressed;

    public HotkeyMessageWindow()
    {
        CreateHandle(new CreateParams());
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WM_HOTKEY)
        {
            int id = m.WParam.ToInt32();
            HotkeyPressed?.Invoke(id);
        }
        base.WndProc(ref m);
    }

    public bool RegisterKey(int id, uint modifiers, Keys key)
    {
        if (Handle == IntPtr.Zero) return false;
        return NativeMethods.RegisterHotKey(Handle, id, modifiers | NativeMethods.MOD_NOREPEAT, (uint)key);
    }

    public void UnregisterKey(int id)
    {
        if (Handle != IntPtr.Zero)
        {
            NativeMethods.UnregisterHotKey(Handle, id);
        }
    }

    public void Dispose()
    {
        DestroyHandle();
    }
}
