using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using MediaBrushes = System.Windows.Media.Brushes;
using MgaAfterDrive.Forms;
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
            Left = -32000;
            Top = -32000;
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

    private void InitializeTrayIcon()
    {
        _trayMenu = new WinForms.ContextMenuStrip();
        ApplyTrayMenuTheme(_trayMenu);

        _settingMenuItem = new WinForms.ToolStripMenuItem("Setting (&S)");
        // WinForms メニューのモーダルループ中に直接 Show すると不可視のまま残ることがある
        _settingMenuItem.Click += (_, _) => Dispatcher.BeginInvoke(OpenSetting);
        var exitMenuItem = new WinForms.ToolStripMenuItem("Exit (&X)");
        exitMenuItem.Click += (_, _) => Dispatcher.BeginInvoke(ExitApp);
        _trayMenu.Items.Add(_settingMenuItem);
        _trayMenu.Items.Add(exitMenuItem);

        // Icon(Stream) はストリーム生存に依存するため、Clone してから Stream を閉じる
        using (var iconStream = AppIcons.OpenIconStream())
        using (var loaded = new Drawing.Icon(iconStream))
        {
            _trayIconHandle = (Drawing.Icon)loaded.Clone();
        }

        _trayIcon = new WinForms.NotifyIcon
        {
            Text = AppInfo.ProductName,
            Icon = _trayIconHandle,
            Visible = true,
            ContextMenuStrip = _trayMenu,
        };
        _trayIcon.MouseClick += TrayIcon_MouseClick;
    }

    private static void ApplyTrayMenuTheme(WinForms.ContextMenuStrip menu)
    {
        menu.RenderMode = WinForms.ToolStripRenderMode.Professional;
        menu.BackColor = ToDrawing(AppTheme.Surface);
        menu.ForeColor = ToDrawing(AppTheme.Foreground);
        menu.Renderer = new DarkTrayRenderer();
    }

    private static Drawing.Color ToDrawing(System.Windows.Media.Color c)
        => Drawing.Color.FromArgb(c.A, c.R, c.G, c.B);

#if DEBUG
    private void AddDebugTrayMenuItems()
    {
        if (_trayMenu is null || _settingMenuItem is null)
        {
            return;
        }

        var separator = new WinForms.ToolStripSeparator();
        var disconnectItem = new WinForms.ToolStripMenuItem("切断をシミュレート (&D)");
        var restoreItem = new WinForms.ToolStripMenuItem("復帰をシミュレート (&R)");

        disconnectItem.Click += (_, _) =>
        {
            AppendLog("[DEBUG] Google Drive 切断をシミュレートします。");
            StartHealthMonitor();
            GoogleDriveHealthMonitor.SimulateDisconnect();
        };

        restoreItem.Click += (_, _) =>
        {
            AppendLog("[DEBUG] Google Drive 復帰をシミュレートします。");
            StartHealthMonitor();
            GoogleDriveHealthMonitor.SimulateRestore();
        };

        var settingIndex = _trayMenu.Items.IndexOf(_settingMenuItem);
        var insertAt = settingIndex >= 0 ? settingIndex + 1 : _trayMenu.Items.Count;
        _trayMenu.Items.Insert(insertAt, separator);
        _trayMenu.Items.Insert(insertAt + 1, disconnectItem);
        _trayMenu.Items.Insert(insertAt + 2, restoreItem);
    }
#endif

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

    private void ShowLicensesInLog()
    {
        if (OperationPause.IsLicenseViewActive)
        {
            return;
        }

        _logSnapshotBeforeLicense = _logLines.ToArray();
        _logBufferDuringLicense.Clear();
        OperationPause.SetLicenseViewActive(true);
        LicenseLink.Text = "Return";
        SetStatusText($"一時停止中（{OperationPause.DescribeReason()}）");

        try
        {
            var lines = EmbeddedLicenses.LoadCombinedLines();
            _logLines.Clear();
            foreach (var line in lines)
            {
                _logLines.Add(line);
            }

            ScrollLogToStart();
        }
        catch (Exception ex)
        {
            _logLines.Clear();
            _logLines.Add($"[ERROR] 埋め込みライセンスの読み込みに失敗しました: {ex.GetType().Name}: {ex.Message}");
            ScrollLogToStart();
        }
    }

    private void HideLicensesAndResume()
    {
        if (!OperationPause.IsLicenseViewActive)
        {
            return;
        }

        OperationPause.SetLicenseViewActive(false);
        LicenseLink.Text = "Licenses";

        _logLines.Clear();
        if (_logSnapshotBeforeLicense is { Length: > 0 })
        {
            foreach (var line in _logSnapshotBeforeLicense)
            {
                _logLines.Add(line);
            }
        }

        _logSnapshotBeforeLicense = null;

        if (_logBufferDuringLicense.Count > 0)
        {
            foreach (var line in _logBufferDuringLicense)
            {
                _logLines.Add(line);
            }

            _logBufferDuringLicense.Clear();
        }

        SetStatusText(null);
        ScrollLogToEnd();
    }

    private void ScrollLogToStart()
    {
        if (_logLines.Count > 0)
        {
            LogList.ScrollIntoView(_logLines[0]);
        }
    }

    private void ScrollLogToEnd()
    {
        if (_logLines.Count > 0)
        {
            LogList.ScrollIntoView(_logLines[^1]);
        }
    }

    private async Task ApplyUpdateCheckResultAsync(Task<AppUpdateCheckResult> updateCheckTask)
    {
        try
        {
            var result = await updateCheckTask.ConfigureAwait(true);
            ReportUpdateCheckResult(result);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            AppendLog($"[ERROR] バージョン確認で未処理の例外: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void ReportUpdateCheckResult(AppUpdateCheckResult result)
    {
        if (!result.Succeeded)
        {
            AppendLog($"バージョン確認に失敗しました（現行 {result.CurrentVersion}）: {result.ErrorDetail}");
            return;
        }

        if (!result.UpdateAvailable)
        {
            AppendLog($"バージョンは最新です（{result.CurrentVersion}）。");
            return;
        }

        AppendLog($"新しいバージョンが公開されています: {result.LatestVersion}（現行 {result.CurrentVersion}）。");

        var message =
            $"新しいバージョン {result.LatestVersion} が公開されています。{Environment.NewLine}"
            + $"現在のバージョン: {result.CurrentVersion}{Environment.NewLine}{Environment.NewLine}"
            + "GitHub のリリースページを開きますか？"
            + $"{Environment.NewLine}（自動更新はありません。ダウンロードと差し替えはご自身で行ってください。）";

        var openPage = AppDialogs.AskYesNo(
            GetDialogOwner(),
            AppInfo.ProductName,
            message,
            MessageBoxImage.Information);

        if (!openPage || string.IsNullOrWhiteSpace(result.ReleaseUrl))
        {
            return;
        }

        if (AppUpdateChecker.TryOpenUrl(result.ReleaseUrl, out var openError))
        {
            AppendLog($"リリースページを開きました: {result.ReleaseUrl}");
        }
        else
        {
            AppendLog($"[ERROR] リリースページを開けませんでした: {openError}");
        }
    }

    private Window? GetDialogOwner()
    {
        if (!IsLoaded || !IsVisible || Opacity < 1.0)
        {
            return null;
        }

        if (WindowState == WindowState.Minimized)
        {
            return null;
        }

        return this;
    }

    private async Task RunStartupSequenceAsync()
    {
        Task<AppUpdateCheckResult>? updateCheckTask = null;
        try
        {
            AppendLog("バージョンを確認しています…");
            updateCheckTask = AppUpdateChecker.CheckAsync(AppInfo.Version, _lifetimeCts.Token);

            var driveOk = await GoogleDriveStartupProbe
                .RunAsync(AppendLog, SetStatusText, _lifetimeCts.Token)
                .ConfigureAwait(true);

            if (!driveOk)
            {
                AppendLog("Google Drive の確認に失敗したため、遅延起動をスキップします。");
                await ApplyUpdateCheckResultAsync(updateCheckTask).ConfigureAwait(true);
                updateCheckTask = null;
                return;
            }

            StartHealthMonitor();

            var entries = DelayEntriesReader.Load();
            if (entries.Count == 0)
            {
                AppendLog("起動エントリがありません。起動するものはありません。");
                Interlocked.Exchange(ref _initialLaunchCompleted, 1);
                await ApplyUpdateCheckResultAsync(updateCheckTask).ConfigureAwait(true);
                updateCheckTask = null;
                HideToTray();
                return;
            }

            CancelPendingLaunch();
            _launchCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token);
            var launchToken = _launchCts.Token;

            await DelayedLaunchRunner
                .RunAsync(entries, AppendLog, SetStatusText, launchToken)
                .ConfigureAwait(true);

            Interlocked.Exchange(ref _initialLaunchCompleted, 1);
            Interlocked.Exchange(ref _needsFullRelaunch, 0);

            await ApplyUpdateCheckResultAsync(updateCheckTask).ConfigureAwait(true);
            updateCheckTask = null;

            AppendLog("起動シーケンスが完了しました。2 秒後にトレイへ格納します…");
            await PauseAwareCountdown.WaitAsync(
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromMilliseconds(200),
                    remaining => $"トレイ格納まで {Math.Ceiling(remaining.TotalSeconds):0} 秒",
                    () => $"トレイ格納を一時停止中（{OperationPause.DescribeReason()}）",
                    SetStatusText,
                    _lifetimeCts.Token)
                .ConfigureAwait(true);

            HideToTray();
        }
        catch (OperationCanceledException)
        {
            if (Volatile.Read(ref _initialLaunchCompleted) == 0)
            {
                AppendLog("起動シーケンスを中断しました。");
            }
        }
        catch (Exception ex)
        {
            AppendLog($"[ERROR] 起動シーケンスで未処理の例外: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            if (updateCheckTask is not null)
            {
                try
                {
                    await ApplyUpdateCheckResultAsync(updateCheckTask).ConfigureAwait(true);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    AppendLog($"[ERROR] バージョン確認で未処理の例外: {ex.GetType().Name}: {ex.Message}");
                }
            }

            if (!OperationPause.IsLicenseViewActive)
            {
                SetStatusText(null);
            }
        }
    }

    private void HideToTray()
    {
        if (!Dispatcher.CheckAccess())
        {
            try
            {
                Dispatcher.BeginInvoke(HideToTray);
            }
            catch (InvalidOperationException)
            {
            }

            return;
        }

        if (_trayIcon is not null)
        {
            _trayIcon.Visible = true;
            if (_trayIcon.Icon is null && _trayIconHandle is not null)
            {
                _trayIcon.Icon = _trayIconHandle;
            }
        }

        ShowInTaskbar = false;
        Hide();
        Opacity = 0;
        StartHealthMonitor();
    }

    private void StartHealthMonitor()
    {
        if (Interlocked.Exchange(ref _healthMonitorStarted, 1) != 0)
        {
            return;
        }

        _ = RunHealthMonitorAsync();
    }

    private async Task RunHealthMonitorAsync()
    {
        try
        {
            await GoogleDriveHealthMonitor
                .RunAsync(AppendLog, OnDriveHealthChanged, _lifetimeCts.Token)
                .ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            AppendLog($"[ERROR] 死活監視が予期せず停止しました: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void OnDriveHealthChanged(bool healthy, string detail)
    {
        if (!Dispatcher.CheckAccess())
        {
            try
            {
                Dispatcher.BeginInvoke(() => OnDriveHealthChanged(healthy, detail));
            }
            catch (InvalidOperationException)
            {
            }

            return;
        }

        if (_trayIcon is not null)
        {
            _trayIcon.Text = healthy
                ? AppInfo.ProductName
                : $"{AppInfo.ProductName} - 切断中";

            _trayIcon.BalloonTipTitle = AppInfo.ProductName;
            _trayIcon.BalloonTipIcon = healthy
                ? WinForms.ToolTipIcon.Info
                : WinForms.ToolTipIcon.Warning;
            _trayIcon.BalloonTipText = healthy
                ? "Google Drive の接続が復帰しました。管理アプリを再開します。"
                : $"Google Drive の接続が切れました。管理アプリを一時停止します。{detail}";
            _trayIcon.ShowBalloonTip(5000);
        }

        _ = HandleDriveRecoveryAsync(healthy);
    }

    private async Task HandleDriveRecoveryAsync(bool healthy)
    {
        CancelPendingLaunch();

        try
        {
            await _recoveryGate.WaitAsync(_lifetimeCts.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        try
        {
            var allEntries = DelayEntriesReader.Load();
            var restartEntries = allEntries
                .Where(DelayEntryRestartPolicy.ShouldManage)
                .ToArray();

            if (!healthy)
            {
                if (Volatile.Read(ref _initialLaunchCompleted) == 0)
                {
                    Interlocked.Exchange(ref _needsFullRelaunch, 1);
                    AppendLog("初回起動が完了する前に切断されたため、残りの起動をすべてキャンセルしました。復帰後に全エントリを再試行します。");
                }

                await Task.Run(
                        () => ManagedAppTerminator.KillRestartEntries(restartEntries, AppendLog),
                        _lifetimeCts.Token)
                    .ConfigureAwait(true);
                return;
            }

            IReadOnlyList<DelayEntryRecord> launchEntries;
            bool respectEntryDelay;
            if (Interlocked.Exchange(ref _needsFullRelaunch, 0) != 0)
            {
                launchEntries = allEntries;
                respectEntryDelay = true;
                AppendLog($"接続が復帰しました。初回起動が未完了のため、全エントリ（{launchEntries.Count} 件）を Delay 付きで再開します。");
            }
            else
            {
                launchEntries = restartEntries;
                respectEntryDelay = false;
                if (launchEntries.Count == 0)
                {
                    AppendLog("接続が復帰しました。再開対象の Google Drive 上アプリはありません。");
                    return;
                }

                AppendLog($"接続が復帰しました。Google Drive 上のアプリ（{launchEntries.Count} 件）を直ちに再開します。");
            }

            if (launchEntries.Count == 0)
            {
                AppendLog("接続が復帰しましたが、再開するエントリがありません。");
                Interlocked.Exchange(ref _initialLaunchCompleted, 1);
                return;
            }

            _launchCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token);
            var token = _launchCts.Token;

            await DelayedLaunchRunner
                .RunAsync(launchEntries, AppendLog, SetStatusText, token, respectEntryDelay)
                .ConfigureAwait(true);

            Interlocked.Exchange(ref _initialLaunchCompleted, 1);
        }
        catch (OperationCanceledException)
        {
            AppendLog("管理アプリの復旧処理をキャンセルしました。");
        }
        catch (Exception ex)
        {
            AppendLog($"[ERROR] 管理アプリの復旧に失敗しました: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            _recoveryGate.Release();
            if (!OperationPause.IsLicenseViewActive)
            {
                SetStatusText(null);
            }
        }
    }

    private void CancelPendingLaunch()
    {
        var cts = Interlocked.Exchange(ref _launchCts, null);
        if (cts is null)
        {
            return;
        }

        try
        {
            cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
        finally
        {
            cts.Dispose();
        }
    }

    private void RestoreFromTray()
    {
        ShowInTaskbar = false;
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        if (Left <= -10000 || Top <= -10000)
        {
            CenterOnPrimaryDisplay();
        }

        var targetLeft = Left;
        var targetTop = Top;
        Opacity = 0;
        Left = -32000;
        Top = -32000;
        Background = MediaBrushes.Transparent;
        LogList.Background = MediaBrushes.Transparent;

        Show();
        UpdateLayout();

        ApplyAcrylicEffect();
        UpdateLayout();

        Left = targetLeft;
        Top = targetTop;
        EnsureOnScreen();
        MarkRevealed();
        Activate();
        Opacity = 1;
        ApplyAcrylicEffect();
    }

    private void ToggleTrayWindow()
    {
        if (IsVisible && WindowState != WindowState.Minimized)
        {
            HideToTray();
        }
        else
        {
            RestoreFromTray();
        }
    }

    private void TrayIcon_MouseClick(object? sender, WinForms.MouseEventArgs e)
    {
        if (e.Button != WinForms.MouseButtons.Left)
        {
            return;
        }

        Dispatcher.BeginInvoke(ToggleTrayWindow);
    }

    private void OpenSetting()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(OpenSetting);
            return;
        }

        try
        {
            if (_settingWindow is not null)
            {
                BringSettingToFront(_settingWindow);
                return;
            }

            OperationPause.SetSettingOpen(true);

            // カスタム枠のメインを Owner にすると、子窓が前面に出ない／表示されないことがある
            var setting = new SettingWindow
            {
                Owner = null,
                ShowInTaskbar = true,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Opacity = 1,
                Topmost = true,
            };
            setting.Closed += (_, _) =>
            {
                if (ReferenceEquals(_settingWindow, setting))
                {
                    _settingWindow = null;
                }

                OperationPause.SetSettingOpen(false);
            };

            _settingWindow = setting;
            setting.Show();
            BringSettingToFront(setting);
        }
        catch (Exception ex)
        {
            OperationPause.SetSettingOpen(false);
            _settingWindow = null;
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

        window.Opacity = 1;
        window.ShowInTaskbar = true;
        window.Activate();
        window.Topmost = true;
        _ = window.Focus();
    }

    private void ExitApp()
    {
        _allowExit = true;
        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
        }

        Close();
        System.Windows.Application.Current?.Shutdown();
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
                Dispatcher.BeginInvoke(Apply);
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
            try
            {
                Dispatcher.BeginInvoke(() => AppendLog(message));
            }
            catch (InvalidOperationException)
            {
            }

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

    private sealed class DarkTrayRenderer : WinForms.ToolStripProfessionalRenderer
    {
        public DarkTrayRenderer()
            : base(new DarkTrayColorTable())
        {
        }

        protected override void OnRenderToolStripBorder(WinForms.ToolStripRenderEventArgs e)
        {
        }
    }

    private sealed class DarkTrayColorTable : WinForms.ProfessionalColorTable
    {
        public override Drawing.Color MenuItemSelected => ToDrawing(AppTheme.Selection);
        public override Drawing.Color MenuItemSelectedGradientBegin => ToDrawing(AppTheme.Selection);
        public override Drawing.Color MenuItemSelectedGradientEnd => ToDrawing(AppTheme.Selection);
        public override Drawing.Color MenuItemBorder => ToDrawing(AppTheme.Border);
        public override Drawing.Color ToolStripDropDownBackground => ToDrawing(AppTheme.Surface);
        public override Drawing.Color ImageMarginGradientBegin => ToDrawing(AppTheme.Surface);
        public override Drawing.Color ImageMarginGradientMiddle => ToDrawing(AppTheme.Surface);
        public override Drawing.Color ImageMarginGradientEnd => ToDrawing(AppTheme.Surface);
        public override Drawing.Color SeparatorDark => ToDrawing(AppTheme.Border);
        public override Drawing.Color SeparatorLight => ToDrawing(AppTheme.Border);
    }
}
