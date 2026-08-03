using MGA_AfterDrive.Setting.IO;
using MGA_AfterDrive.Theme;

namespace MGA_AfterDrive.Setting;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        HostAppLocator.Initialize(args);
        ApplicationConfiguration.Initialize();
        Application.SetDefaultFont(AppFonts.UI);
        Application.Run(new MainForm());
    }
}
