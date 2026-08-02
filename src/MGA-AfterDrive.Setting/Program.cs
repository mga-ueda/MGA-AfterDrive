using MGA_AfterDrive.Theme;

namespace MGA_AfterDrive.Setting;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.SetDefaultFont(AppFonts.UI);
        Application.Run(new MainForm());
    }
}
