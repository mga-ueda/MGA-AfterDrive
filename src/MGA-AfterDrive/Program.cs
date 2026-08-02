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
            MessageBox.Show(
                "MGA AfterDrive は既に起動しています。\nタスクトレイを確認してください。",
                AppInfo.ProductName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        if (acquire != SingleInstanceAcquireResult.Acquired || singleInstance is null)
        {
            MessageBox.Show(
                $"起動状態を確認できませんでした。{Environment.NewLine}{acquireError}",
                AppInfo.ProductName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        using (singleInstance)
        {
            if (!DelayEntriesPresence.HasAny())
            {
                if (!SettingAppLauncher.TryStart(out var error))
                {
                    MessageBox.Show(
                        $"設定がありません。設定アプリを起動できませんでした。{Environment.NewLine}{error}",
                        AppInfo.ProductName,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }

                return;
            }

            Application.Run(new MainForm());
        }
    }
}
