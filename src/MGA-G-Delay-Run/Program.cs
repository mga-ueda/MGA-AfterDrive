using MGA_G_Delay_Run.IO;
using MGA_G_Delay_Run.Theme;

namespace MGA_G_Delay_Run;

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
