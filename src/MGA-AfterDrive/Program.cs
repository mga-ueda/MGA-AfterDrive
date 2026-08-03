using MGA_AfterDrive.Forms;
using MGA_AfterDrive.IO;
using MGA_AfterDrive.Theme;

namespace MGA_AfterDrive;

static class Program
{
    private const string SingleInstanceMutexName = @"Local\MGA.AfterDrive.SingleInstance";

    /// <summary>
    /// アプリケーションのメイン エントリ ポイントです。
    /// </summary>
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.SetDefaultFont(AppFonts.UI);

        var acquire = SingleInstanceGuard.TryAcquire(
            SingleInstanceMutexName,
            out var singleInstance,
            out var acquireError);

        if (acquire == SingleInstanceAcquireResult.AlreadyRunning)
        {
            AppDialogs.Info(
                null,
                AppInfo.ProductName,
                "MGA AfterDrive は既に起動しています。\nタスクトレイを確認してください。");
            return;
        }

        if (acquire != SingleInstanceAcquireResult.Acquired || singleInstance is null)
        {
            AppDialogs.Error(
                null,
                AppInfo.ProductName,
                $"起動状態を確認できませんでした。{Environment.NewLine}{acquireError}");
            return;
        }

        using (singleInstance)
        {
            if (!DelayEntriesPresence.HasAny())
            {
                if (!SettingAppLauncher.TryStart(out var error))
                {
                    AppDialogs.Error(
                        null,
                        AppInfo.ProductName,
                        $"設定がありません。設定アプリを起動できませんでした。{Environment.NewLine}{error}");
                }

                return;
            }

            Application.Run(new MainForm());
        }
    }
}
