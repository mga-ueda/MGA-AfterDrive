using MGA_AfterDrive.IO;
using MGA_AfterDrive.Theme;

namespace MGA_AfterDrive;

static class Program
{
    /// <summary>
    /// アプリケーションのメイン エントリ ポイントです。
    /// </summary>
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.SetDefaultFont(AppFonts.UI);

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
