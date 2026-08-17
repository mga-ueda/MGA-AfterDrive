using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
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
    private int _logScrollQueued;
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
        ParkOffScreen();

        InitializeTrayIcon();
#if DEBUG
        AddDebugTrayMenuItems();
#endif
        Loaded += MainWindow_Loaded;
        Closed += MainWindow_Closed;
    }

    protected override bool PersistWindowBounds => false;

    protected override bool ShouldRevealOnShown => !_startMinimizedToTray;

    /// <summary>
    /// 中央寄せと可視化は Acrylic 適用後に自分で行う。
    /// </summary>
    protected override bool RevealOnContentRendered => false;

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        if (!ShouldRevealOnShown)
        {
            return;
        }

        Dispatcher.BeginInvoke(RevealAfterAcrylicReady, DispatcherPriority.Loaded);
    }

    private void RevealAfterAcrylicReady()
    {
        if (IsRevealed)
        {
            return;
        }

        ParkOffScreen();
        Background = MediaBrushes.Transparent;
        LogList.Background = MediaBrushes.Transparent;
        UpdateLayout();
        ApplyAcrylicEffect();
        UpdateLayout();

        Dispatcher.BeginInvoke(() =>
        {
            if (IsRevealed)
            {
                return;
            }

            CenterOnPrimaryDisplay();
            EnsureOnScreen();
            MarkRevealed();
            Activate();
            Opacity = 1;
            ApplyAcrylicEffect();
            StartStartupSequenceWhenReady();
        }, DispatcherPriority.Loaded);
    }

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
            StartStartupSequenceWhenReady();
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
    }

    private void StartStartupSequenceWhenReady()
    {
        if (Interlocked.Exchange(ref _probeStarted, 1) != 0)
        {
            return;
        }

        // ウィンドウを描画してから監視・起動を始める（ログが一気に溜まらないようにする）
        Dispatcher.BeginInvoke(
            () => _ = RunStartupSequenceAsync(),
            DispatcherPriority.ContextIdle);
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

    private void LogContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        var hasLines = _logLines.Count > 0;
        LogCopyMenuItem.IsEnabled = hasLines;
        LogClearMenuItem.IsEnabled = hasLines && !OperationPause.IsLicenseViewActive;
    }

    private void LogCopyMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (_logLines.Count == 0)
        {
            return;
        }

        try
        {
            System.Windows.Clipboard.SetText(string.Join(Environment.NewLine, _logLines));
        }
        catch (Exception ex) when (ex is ExternalException or InvalidOperationException)
        {
            AppendLog($"[ERROR] ログのコピーに失敗しました: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void LogClearMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (OperationPause.IsLicenseViewActive)
        {
            return;
        }

        _logLines.Clear();
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
            try
            {
                Dispatcher.BeginInvoke(Apply, DispatcherPriority.Render);
            }
            catch (InvalidOperationException)
            {
            }

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
            QueueScrollLogToEnd();
        }
        catch (InvalidOperationException)
        {
        }
    }
}
