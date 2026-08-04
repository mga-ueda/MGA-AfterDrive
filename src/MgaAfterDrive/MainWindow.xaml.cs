using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using MediaBrushes = System.Windows.Media.Brushes;
using MgaAfterDrive.Dialogs;
using MgaAfterDrive.IO;
using MgaAfterDrive.Native;
using MgaAfterDrive.Theme;
using MgaAfterDrive.Windows;
using Drawing = System.Drawing;
using WinForms = System.Windows.Forms;

namespace MgaAfterDrive;

public partial class MainWindow : AppWindow
{
    private const double OffScreenCoordinate = -32000;
    private const double OffScreenParkThreshold = -10000;

    private readonly CancellationTokenSource _lifetimeCts = new();
    private readonly SemaphoreSlim _recoveryGate = new(1, 1);
    private readonly ObservableCollection<string> _logLines = [];
    private readonly List<string> _logBufferDuringLicense = [];
    private CancellationTokenSource? _launchCts;
    private SettingWindow? _settingWindow;
    private WinForms.NotifyIcon? _trayIcon;
    private WinForms.ContextMenuStrip? _trayMenu;
    private WinForms.ToolStripMenuItem? _settingMenuItem;
    private Drawing.Icon? _trayIconHandle;
    private string[]? _logSnapshotBeforeLicense;
    private int _probeStarted;
    private int _healthMonitorStarted;
    private int _initialLaunchCompleted;
    private int _needsFullRelaunch;
    private int _statusVersion;
    private bool _allowExit;
    private bool _startMinimizedToTray;

    public MainWindow()
    {
        InitializeComponent();
        Title = AppInfo.WindowTitle;
        Caption.Title = Title;
        LogList.ItemsSource = _logLines;
        LogList.FontFamily = AppFonts.LogFamily;
        LogList.FontSize = AppFonts.LogSize;
        LogList.FontWeight = FontWeights.Light;
        LicenseLink.FontFamily = AppFonts.UIFamily;
        LicenseLink.FontSize = AppFonts.UISize;
        SettingLink.FontFamily = AppFonts.UIFamily;
        SettingLink.FontSize = AppFonts.UISize;
        StatusLabel.FontFamily = AppFonts.UIFamily;
        StatusLabel.FontSize = AppFonts.UISize;

        ShowInTaskbar = false;
        _startMinimizedToTray = AppSettingsStore.Load().StartMinimizedToTray;
        if (_startMinimizedToTray)
        {
            Left = OffScreenCoordinate;
            Top = OffScreenCoordinate;
        }

        InitializeTrayIcon();
#if DEBUG
        AddDebugTrayMenuItems();
#endif
        Loaded += MainWindow_Loaded;
        Closed += MainWindow_Closed;
    }

    protected override bool PersistWindowBounds => false;

    protected override bool ShouldRevealOnShown => !_startMinimizedToTray;

    protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            HideToTray();
            e.Handled = true;
            return;
        }

        base.OnPreviewKeyDown(e);
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_allowExit)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        base.OnClosing(e);
    }

    protected override void OnRevealed()
    {
        base.OnRevealed();

        Background = MediaBrushes.Transparent;
        LogList.Background = MediaBrushes.Transparent;

        if (_startMinimizedToTray)
        {
            HideToTray();
            return;
        }

        ApplyAcrylicEffect();
    }

    private void MainWindow_Activated(object? sender, EventArgs e)
    {
        if (Opacity >= 1.0 && IsLoaded && IsVisible)
        {
            ApplyAcrylicEffect();
        }
    }

    private void ApplyAcrylicEffect()
    {
        Background = MediaBrushes.Transparent;
        LogList.Background = MediaBrushes.Transparent;
        AcrylicBackdrop.Apply(this, AcrylicBackdrop.BlurType.Acrylic);
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        ShowInTaskbar = false;
        if (_trayIcon is not null)
        {
            _trayIcon.Visible = true;
        }

        Background = MediaBrushes.Transparent;
        LogList.Background = MediaBrushes.Transparent;
        LogList.Foreground = new SolidColorBrush(AppTheme.LogForeground);
        SetStatusText(null);

        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
        }

        if (Interlocked.Exchange(ref _probeStarted, 1) != 0)
        {
            return;
        }

        _ = RunStartupSequenceAsync();
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        try
        {
            if (_trayIcon is not null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }

            _trayMenu?.Dispose();
            _trayMenu = null;
            if (_trayMenuOwner is not null)
            {
                _trayMenuOwner.DestroyHandle();
                _trayMenuOwner = null;
            }

            _trayIconHandle?.Dispose();
            _trayIconHandle = null;

            CancelPendingLaunch();

            if (!_lifetimeCts.IsCancellationRequested)
            {
                _lifetimeCts.Cancel();
            }
        }
        catch (ObjectDisposedException)
        {
        }
        finally
        {
            _recoveryGate.Dispose();
            _lifetimeCts.Dispose();
        }
    }

    private void Caption_HideRequested(object? sender, EventArgs e)
        => HideToTray();

    private void LicenseLink_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (OperationPause.IsLicenseViewActive)
        {
            HideLicensesAndResume();
        }
        else
        {
            ShowLicensesInLog();
        }
    }

    private void SettingLink_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        OpenSetting();
    }

    private void OpenSetting()
    {
        if (!Dispatcher.CheckAccess())
        {
            UiDispatch.BeginInvoke(Dispatcher, OpenSetting);
            return;
        }

        try
        {
            if (_settingWindow is not null)
            {
                BringSettingToFront(_settingWindow);
                return;
            }

            // OperationPause は SettingWindow の ctor / Closed が管理する
            var setting = new SettingWindow
            {
                Owner = null,
                ShowInTaskbar = true,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Topmost = true,
            };
            setting.Closed += (_, _) =>
            {
                if (ReferenceEquals(_settingWindow, setting))
                {
                    _settingWindow = null;
                }
            };

            _settingWindow = setting;
            // Reveal は SettingWindow 側がリスト準備後に行う（ここで Activate するとチラつく）
            setting.Show();
        }
        catch (Exception ex)
        {
            _settingWindow = null;
            OperationPause.SetSettingOpen(false);
            AppDialogs.Error(
                IsVisible && Opacity >= 1.0 ? this : null,
                AppInfo.ProductName,
                $"Setting を開けませんでした。{Environment.NewLine}{ex}");
        }
    }

    private static void BringSettingToFront(Window window)
    {
        if (!window.IsVisible)
        {
            window.Show();
        }

        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.ShowInTaskbar = true;
        window.Activate();
        window.Topmost = true;
        _ = window.Focus();
    }

    private void SetStatusText(string? status)
    {
        var version = Interlocked.Increment(ref _statusVersion);
        var text = string.IsNullOrWhiteSpace(status) ? string.Empty : status.Trim();

        void Apply()
        {
            if (version != Volatile.Read(ref _statusVersion))
            {
                return;
            }

            StatusLabel.Text = text;
            StatusLabel.Visibility = text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        if (!Dispatcher.CheckAccess())
        {
            UiDispatch.BeginInvoke(Dispatcher, Apply);
            return;
        }

        Apply();
    }

    public void AppendLog(string message)
    {
        if (!Dispatcher.CheckAccess())
        {
            UiDispatch.BeginInvoke(Dispatcher, () => AppendLog(message));
            return;
        }

        try
        {
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
            if (OperationPause.IsLicenseViewActive)
            {
                _logBufferDuringLicense.Add(line);
                return;
            }

            _logLines.Add(line);
            ScrollLogToEnd();
        }
        catch (InvalidOperationException)
        {
        }
    }
}
