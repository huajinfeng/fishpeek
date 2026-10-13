using System;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;

namespace FishPeek;

public class HotkeyConfig
{
    public bool Ctrl { get; set; } = true;
    public bool Alt { get; set; } = false;
    public bool Shift { get; set; } = false;
    public Keys Key { get; set; } = Keys.F8;

    public uint GetModifiers()
    {
        uint mod = 0;
        if (Alt) mod |= NativeMethods.MOD_ALT;
        if (Ctrl) mod |= NativeMethods.MOD_CONTROL;
        if (Shift) mod |= NativeMethods.MOD_SHIFT;
        return mod;
    }

    public override string ToString()
    {
        var parts = new System.Collections.Generic.List<string>();
        if (Ctrl) parts.Add("Ctrl");
        if (Alt) parts.Add("Alt");
        if (Shift) parts.Add("Shift");
        parts.Add(Key.ToString());
        return string.Join(" + ", parts);
    }
}

public class AppConfig
{
    public HotkeyConfig PickKey { get; set; } = new() { Ctrl = true, Key = Keys.F8 };
    public HotkeyConfig ToggleKey { get; set; } = new() { Ctrl = true, Key = Keys.F9 };
    public HotkeyConfig UnbindKey { get; set; } = new() { Ctrl = true, Key = Keys.F10 };
    public HotkeyConfig KillAppKey { get; set; } = new() { Ctrl = true, Key = Keys.F11 };

    public bool HoverPeekEnabled { get; set; } = true;
    public bool NoDelay { get; set; } = false;
    public int HoverDelayMs { get; set; } = 300;

    public bool MuteOnHide { get; set; } = true;
    public bool EnableKillAppKey { get; set; } = true;

    private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                string json = File.ReadAllText(ConfigPath);
                var cfg = JsonSerializer.Deserialize<AppConfig>(json);
                if (cfg != null) return cfg;
            }
        }
        catch
        {
            // 加载异常时回退到默认配置
        }
        return new AppConfig();
    }

    public void Save()
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(this, options);
            File.WriteAllText(ConfigPath, json);
        }
        catch
        {
            // 忽略写入异常
        }
    }
}
