using MgaAfterDrive.Forms;
using MgaAfterDrive.IO;

namespace MgaAfterDrive;

static class Program
{
    private const string SingleInstanceMutexName = @"Local\MgaAfterDrive.SingleInstance";
    private const string SettingsFlag = "--settings";

    [STAThread]
    static void Main(string[] args)
    {
        var settingsOnly = args.Any(arg =>
            string.Equals(arg, SettingsFlag, StringComparison.OrdinalIgnoreCase));

        // WinForms NotifyIcon 用
        System.Windows.Forms.Application.EnableVisualStyles();
        System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
        System.Windows.Forms.Application.SetHighDpiMode(System.Windows.Forms.HighDpiMode.PerMonitorV2);

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
            var app = new App();
            app.InitializeComponent();
            app.ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;

            app.DispatcherUnhandledException += (_, e) =>
            {
                e.Handled = true;
                AppDialogs.Error(
                    null,
                    AppInfo.ProductName,
                    $"予期しないエラーが発生しました。{Environment.NewLine}{e.Exception.Message}");
            };

            if (settingsOnly)
            {
                var settingWindow = CreateSettingWindow();
                settingWindow.Closed += (_, _) => app.Shutdown();
                app.Run(settingWindow);
                return;
            }

            if (!DelayEntriesPresence.HasAny())
            {
                var settingWindow = CreateSettingWindow();
                // Run 前の ShowDialog は環境によって描画されないことがあるため、
                // 一度 Run して閉じたあとエントリ有無で続行する。
                settingWindow.Closed += (_, _) =>
                {
                    OperationPause.SetSettingOpen(false);
                    if (!DelayEntriesPresence.HasAny())
                    {
                        app.Shutdown();
                        return;
                    }

                    var mainWindow = new MainWindow();
                    app.MainWindow = mainWindow;
                    mainWindow.Show();
                };
                OperationPause.SetSettingOpen(true);
                app.Run(settingWindow);
                return;
            }

            app.Run(new MainWindow());
        }
    }

    private static SettingWindow CreateSettingWindow()
        => new()
        {
            Opacity = 1,
            ShowInTaskbar = true,
            WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
            Topmost = true,
        };
}
