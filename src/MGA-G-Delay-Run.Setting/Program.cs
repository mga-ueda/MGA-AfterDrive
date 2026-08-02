using MGA_G_Delay_Run.Theme;

namespace MGA_G_Delay_Run.Setting;

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
