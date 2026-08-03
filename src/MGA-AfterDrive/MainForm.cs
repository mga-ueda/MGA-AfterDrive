using System.Runtime.InteropServices;
using MGA_AfterDrive.Forms;
using MGA_AfterDrive.IO;
using MGA_AfterDrive.Native;
using MGA_AfterDrive.Theme;

namespace MGA_AfterDrive;

public partial class MainForm : AppForm
{
    private const int WmNcCalcSize = 0x0083;
    private const int WmNcHitTest = 0x0084;
    private const int HtClient = 1;
    private const int HtCaption = 2;
    private const int HtTop = 12;
    private const int HtTopLeft = 13;
    private const int HtTopRight = 14;
    private const int SmCySizeFrame = 33;
    private const int SmCxPaddedBorder = 92;

    private readonly CancellationTokenSource _lifetimeCts = new();
    private readonly SemaphoreSlim _recoveryGate = new(1, 1);
    private CancellationTokenSource? _launchCts;
    private int _probeStarted;
    private int _healthMonitorStarted;
    /// <summary>初回の遅延起動シーケンスが最後まで完了したか。</summary>
    private int _initialLaunchCompleted;
    /// <summary>初回起動が未完了のまま切断された。復帰時に全エントリを起動する。</summary>
    private int _needsFullRelaunch;
    private bool _allowExit;
    private bool _startMinimizedToTray;
    /// <summary>ライセンス表示前のログ行。</summary>
    private string[]? _logSnapshotBeforeLicense;
    /// <summary>ライセンス表示中に届いたログ（復帰時に追記）。</summary>
    private readonly List<string> _logBufferDuringLicense = [];
    /// <summary>ステータス表示の世代。古い BeginInvoke を打ち消す。</summary>
    private int _statusVersion;

    public MainForm()
    {
        InitializeComponent();
        Text = AppInfo.WindowTitle;
        ShowInTaskbar = false;
        trayIcon.Text = AppInfo.ProductName;
        trayIcon.Icon = AppIcons.Default;
        trayIcon.Visible = true;
        _startMinimizedToTray = AppSettingsStore.Load().StartMinimizedToTray;
        if (_startMinimizedToTray)
        {
            // 可視化前の万一のフラッシュを画面外へ逃がす
            StartPosition = FormStartPosition.Manual;
            Location = new Point(-32000, -32000);
        }
#if DEBUG
        AddDebugTrayMenuItems();
#endif
    }

    protected override bool PersistWindowBounds => false;

    protected override bool ShouldRevealOnShown => !_startMinimizedToTray;

    /// <summary>
    /// DWM のキャプションはガラス（ACCENT）と両立しないため、
    /// 上端だけ既定の非クライアント計算を打ち消してキャプション領域を
    /// クライアント化し、タイトルバーは captionBar が自前描画する。
    /// リサイズ枠・影・角丸・スナップは DWM のまま維持される。
    /// </summary>
    protected override void WndProc(ref Message m)
    {
        switch (m.Msg)
        {
            case WmNcCalcSize when m.WParam != IntPtr.Zero:
                HandleNcCalcSize(ref m);
                return;

            case WmNcHitTest:
                HandleNcHitTest(ref m);
                return;
        }

        base.WndProc(ref m);
    }

    private void HandleNcCalcSize(ref Message m)
    {
        // 既定処理に左右下の枠を計算させ、上端だけウィンドウ外周へ戻す
        var rect = Marshal.PtrToStructure<NativeRect>(m.LParam);
        var top = rect.Top;

        base.WndProc(ref m);

        if (WindowState != FormWindowState.Maximized)
        {
            rect = Marshal.PtrToStructure<NativeRect>(m.LParam);
            rect.Top = top;
            Marshal.StructureToPtr(rect, m.LParam, false);
        }

        m.Result = IntPtr.Zero;
    }

    private void HandleNcHitTest(ref Message m)
    {
        base.WndProc(ref m);

        if (m.Result != HtClient)
        {
            return;
        }

        var client = PointToClient(new Point(
            unchecked((short)(long)m.LParam),
            unchecked((short)((long)m.LParam >> 16))));

        var frame = GetSystemMetrics(SmCySizeFrame) + GetSystemMetrics(SmCxPaddedBorder);
        if (WindowState != FormWindowState.Maximized && client.Y < frame)
        {
            m.Result = client.X < frame
                ? HtTopLeft
                : client.X >= ClientSize.Width - frame
                    ? HtTopRight
                    : HtTop;
            return;
        }

        if (client.Y < captionBar.Height)
        {
            m.Result = HtCaption;
        }
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        captionBar.Text = Text;
    }

    private void CaptionBar_CloseRequested(object? sender, EventArgs e)
    {
        // _allowExit が立っていない限り OnFormClosing がトレイ格納に振り替える
        Close();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        ShowInTaskbar = false;
        trayIcon.Visible = true;
        // ガラスキーは純黒のみ。AppTheme.Apply の灰色を上書きする。
        BackColor = Color.Black;
        logEditor.BackColor = Color.Black;
        logEditor.ForeColor = AppTheme.LogForeground;
        logEditor.Font = AppFonts.Log;
        // ステータスバーもガラスキー（純黒）。上辺だけ境界線を描く。
        statusBar.BackColor = Color.Black;
        statusLayout.BackColor = Color.Black;
        statusLabel.BackColor = Color.Transparent;
        statusLabel.ForeColor = AppTheme.Foreground;
        statusBar.Paint += StatusBar_Paint;
        ApplyTrayMenuTheme();
        SetStatusText(null);

        if (WindowState == FormWindowState.Maximized)
        {
            WindowState = FormWindowState.Normal;
        }

        if (Interlocked.Exchange(ref _probeStarted, 1) != 0)
        {
            return;
        }

        _ = RunStartupSequenceAsync();
    }

    protected override void OnRevealed()
    {
        base.OnRevealed();

        BackColor = Color.Black;
        logEditor.BackColor = Color.Black;

        if (_startMinimizedToTray)
        {
            // Opacity は 0 のまま。可視化せずトレイへ格納する
            HideToTray();
            return;
        }

        // WinBlur SetBlurStyle 相当（レイヤード解除済みの Opacity=1 以降）
        AcrylicBackdrop.Apply(this, AcrylicBackdrop.BlurType.Acrylic);
        logEditor.Invalidate();
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        if (Opacity >= 1.0 && IsHandleCreated)
        {
            AcrylicBackdrop.Apply(this, AcrylicBackdrop.BlurType.Acrylic);
        }
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Color.Black);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_allowExit)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        base.OnFormClosing(e);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            HideToTray();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        try
        {
            trayIcon.Visible = false;
            CancelPendingLaunch();

            if (!_lifetimeCts.IsCancellationRequested)
            {
                _lifetimeCts.Cancel();
            }
        }
        catch (ObjectDisposedException)
        {
            // already disposed
        }
        finally
        {
            _recoveryGate.Dispose();
            _lifetimeCts.Dispose();
            base.OnFormClosed(e);
        }
    }

    private void ApplyTrayMenuTheme()
        => AppTheme.ApplyContextMenu(trayMenu);

#if DEBUG
    /// <summary>
    /// DEBUG 限定: Google Drive 切断／復帰のシミュレーションメニュー。
    /// Release ビルドではコンパイルされない。
    /// </summary>
    private void AddDebugTrayMenuItems()
    {
        var separator = new ToolStripSeparator();
        var disconnectItem = new ToolStripMenuItem("切断をシミュレート (&D)");
        var restoreItem = new ToolStripMenuItem("復帰をシミュレート (&R)");

        disconnectItem.Click += (_, _) =>
        {
            AppendLog("[DEBUG] Google Drive 切断をシミュレートします。");
            // 監視未開始でもすぐ反応するよう起動してからパルスする
            StartHealthMonitor();
            GoogleDriveHealthMonitor.SimulateDisconnect();
        };

        restoreItem.Click += (_, _) =>
        {
            AppendLog("[DEBUG] Google Drive 復帰をシミュレートします。");
            StartHealthMonitor();
            GoogleDriveHealthMonitor.SimulateRestore();
        };

        // Setting の直後へ挿入: Setting / --- / Disconnect / Restore / Exit
        var settingIndex = trayMenu.Items.IndexOf(settingMenuItem);
        var insertAt = settingIndex >= 0 ? settingIndex + 1 : trayMenu.Items.Count;
        trayMenu.Items.Insert(insertAt, separator);
        trayMenu.Items.Insert(insertAt + 1, disconnectItem);
        trayMenu.Items.Insert(insertAt + 2, restoreItem);
    }
#endif

    private void LicenseLink_LinkClicked(object? sender, LinkLabelLinkClickedEventArgs e)
    {
        if (OperationPause.IsLicenseViewActive)
        {
            HideLicensesAndResume();
        }
        else
        {
            ShowLicensesInLog();
        }
    }

    private void ShowLicensesInLog()
    {
        if (OperationPause.IsLicenseViewActive)
        {
            return;
        }

        _logSnapshotBeforeLicense = logEditor.CaptureLines();
        _logBufferDuringLicense.Clear();
        OperationPause.SetLicenseViewActive(true);
        SetLicenseLinkText("Return");
        SetStatusText($"一時停止中（{OperationPause.DescribeReason()}）");

        try
        {
            var lines = EmbeddedLicenses.LoadCombinedLines();
            logEditor.Clear();
            logEditor.AppendLines(lines);
            logEditor.ScrollToStart();
        }
        catch (Exception ex)
        {
            logEditor.Clear();
            logEditor.AppendLine($"[ERROR] 埋め込みライセンスの読み込みに失敗しました: {ex.GetType().Name}: {ex.Message}");
            logEditor.ScrollToStart();
        }
    }

    private void HideLicensesAndResume()
    {
        if (!OperationPause.IsLicenseViewActive)
        {
            return;
        }

        OperationPause.SetLicenseViewActive(false);
        SetLicenseLinkText("Licenses");

        logEditor.Clear();
        if (_logSnapshotBeforeLicense is { Length: > 0 })
        {
            logEditor.AppendLines(_logSnapshotBeforeLicense);
        }

        _logSnapshotBeforeLicense = null;

        if (_logBufferDuringLicense.Count > 0)
        {
            logEditor.AppendLines(_logBufferDuringLicense);
            _logBufferDuringLicense.Clear();
        }

        // 待機ループが動いていれば直後に正しい表示へ上書きする。
        // 動いていなくても「一時停止中（ライセンス表示）」を残さない。
        SetStatusText(null);
    }

    private void SetLicenseLinkText(string text)
    {
        licenseLink.Text = text;
        licenseLink.LinkArea = new LinkArea(0, text.Length);
    }

    private void StatusBar_Paint(object? sender, PaintEventArgs e)
    {
        using var pen = new Pen(AppTheme.Border);
        e.Graphics.DrawLine(pen, 0, 0, statusBar.ClientSize.Width, 0);
    }

    private async Task RunStartupSequenceAsync()
    {
        try
        {
            var driveOk = await GoogleDriveStartupProbe
                .RunAsync(AppendLog, SetStatusText, _lifetimeCts.Token)
                .ConfigureAwait(true);

            if (!driveOk)
            {
                AppendLog("Google Drive の確認に失敗したため、遅延起動をスキップします。");
                return;
            }

            // カウントダウン中の切断を検知するため、起動シーケンス前に監視を始める
            StartHealthMonitor();

            var entries = DelayEntriesReader.Load();
            if (entries.Count == 0)
            {
                AppendLog("起動エントリがありません。起動するものはありません。");
                Interlocked.Exchange(ref _initialLaunchCompleted, 1);
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
            // 切断による中断時の未完了フラグは HandleDriveRecoveryAsync 側で立てる
            // （復帰処理との競合でフラグが再セットされないようにする）
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
            if (!OperationPause.IsLicenseViewActive)
            {
                SetStatusText(null);
            }
        }
    }

    private void HideToTray()
    {
        if (IsDisposed || Disposing)
        {
            return;
        }

        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(HideToTray);
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }

            return;
        }

        trayIcon.Visible = true;
        ShowInTaskbar = false;
        Hide();
        // 次回 Restore で Opacity=0 のまま Acrylic を先にかけられるようにする
        Opacity = 0;
        StartHealthMonitor();
    }

    /// <summary>
    /// トレイ格納を機に Google Drive の死活監視を開始する（初回のみ）。
    /// 以降はアプリ終了（またはライセンス表示による中断）まで動き続ける。
    /// </summary>
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
            // アプリ終了・ライセンス表示による中断
        }
        catch (Exception ex)
        {
            AppendLog($"[ERROR] 死活監視が予期せず停止しました: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void OnDriveHealthChanged(bool healthy, string detail)
    {
        if (IsDisposed || Disposing)
        {
            return;
        }

        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(() => OnDriveHealthChanged(healthy, detail));
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }

            return;
        }

        // NotifyIcon.Text は 63 文字制限があるため短く保つ
        trayIcon.Text = healthy
            ? AppInfo.ProductName
            : $"{AppInfo.ProductName} - 切断中";

        // Windows のトースト／バルーン通知
        trayIcon.BalloonTipTitle = AppInfo.ProductName;
        trayIcon.BalloonTipIcon = healthy ? ToolTipIcon.Info : ToolTipIcon.Warning;
        trayIcon.BalloonTipText = healthy
            ? "Google Drive の接続が復帰しました。管理アプリを再開します。"
            : $"Google Drive の接続が切れました。管理アプリを一時停止します。{detail}";
        trayIcon.ShowBalloonTip(5000);

        _ = HandleDriveRecoveryAsync(healthy);
    }

    /// <summary>
    /// 切断時: 起動中なら全キャンセル＋未完了フラグ、Google Drive 上アプリを強制終了。
    /// 復帰時: 未完了なら全エントリ、通常時は Google Drive 上アプリのみを直ちに起動。
    /// </summary>
    private async Task HandleDriveRecoveryAsync(bool healthy)
    {
        // 進行中の起動シーケンスは新しい遷移で打ち切る
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

            // 復帰
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
        if (IsDisposed || Disposing)
        {
            return;
        }

        ShowInTaskbar = false;
        if (WindowState == FormWindowState.Minimized)
        {
            WindowState = FormWindowState.Normal;
        }

        // 表示位置を決めてから、Acrylic 準備中は画面外に置く（不透明フレームのチラつき防止）
        if (Location.X <= -10000 || Location.Y <= -10000)
        {
            CenterOnPrimaryDisplay();
        }

        var targetLocation = Location;
        Opacity = 0;
        Location = new Point(-32000, -32000);
        BackColor = Color.Black;
        logEditor.BackColor = Color.Black;

        Show();
        Update();

        AcrylicBackdrop.Apply(this, AcrylicBackdrop.BlurType.Acrylic);
        Update();

        Location = targetLocation;
        EnsureOnScreen();
        MarkRevealed();
        Activate();
        BringToFront();
        logEditor.Invalidate();
    }

    private void ToggleTrayWindow()
    {
        if (Visible && WindowState != FormWindowState.Minimized)
        {
            HideToTray();
        }
        else
        {
            RestoreFromTray();
        }
    }

    private void TrayIcon_MouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        ToggleTrayWindow();
    }

    private void SettingLink_LinkClicked(object? sender, LinkLabelLinkClickedEventArgs e)
        => OpenSettingApp();

    private void SettingMenuItem_Click(object? sender, EventArgs e)
        => OpenSettingApp();

    private void OpenSettingApp()
    {
        if (!SettingAppLauncher.TryStart(out var error))
        {
            AppDialogs.Error(null, AppInfo.ProductName, error);
        }
    }

    private void ExitMenuItem_Click(object? sender, EventArgs e)
    {
        _allowExit = true;
        trayIcon.Visible = false;
        Close();
    }

    /// <summary>
    /// ステータスバー右寄せのカウントダウン／状態表示。タイトルバーは変更しない。
    /// BeginInvoke の遅延適用で古い一時停止表示が復活しないよう世代番号で打ち消す。
    /// </summary>
    private void SetStatusText(string? status)
    {
        if (IsDisposed || Disposing)
        {
            return;
        }

        var version = Interlocked.Increment(ref _statusVersion);
        var text = string.IsNullOrWhiteSpace(status) ? string.Empty : status.Trim();

        void Apply()
        {
            if (IsDisposed || Disposing || statusLabel.IsDisposed)
            {
                return;
            }

            if (version != Volatile.Read(ref _statusVersion))
            {
                return;
            }

            try
            {
                statusLabel.Text = text;
                statusLabel.Visible = text.Length > 0;
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(Apply);
            }
            catch (ObjectDisposedException)
            {
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
        if (IsDisposed || Disposing)
        {
            return;
        }

        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(() => AppendLog(message));
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }

            return;
        }

        try
        {
            if (logEditor.IsDisposed)
            {
                return;
            }

            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
            if (OperationPause.IsLicenseViewActive)
            {
                _logBufferDuringLicense.Add(line);
                return;
            }

            logEditor.AppendLine(line);
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }
}
