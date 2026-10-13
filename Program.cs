using System;
using System.Threading;
using System.Windows.Forms;

namespace FishPeek;

internal static class Program
{
    private const string AppMutexName = "Global\\FishPeek_App_Mutex_2026";

    [STAThread]
    static void Main()
    {
        using Mutex mutex = new Mutex(true, AppMutexName, out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show(
                "摸鱼助手已在后台运行中！\n请检查系统托盘区域图标。",
                "提示",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new FishTrayApplicationContext());
    }
}