using MgaAfterDrive.IO;
using MgaAfterDrive.Windows;
using WinForms = System.Windows.Forms;

namespace MgaAfterDrive;

public partial class MainWindow
{
    private const int BalloonTipMilliseconds = 5000;
    private static readonly TimeSpan HealthMonitorRestartDelay = TimeSpan.FromSeconds(5);

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
        while (!_lifetimeCts.IsCancellationRequested)
        {
            try
            {
                await GoogleDriveHealthMonitor
                    .RunAsync(AppendLog, OnDriveHealthChanged, _lifetimeCts.Token)
                    .ConfigureAwait(true);

                // RunAsync はキャンセル以外では通常戻らない。戻った場合も監視を継続する。
                if (_lifetimeCts.IsCancellationRequested)
                {
                    return;
                }

                AppendLog("[WARN] 死活監視が終了したため再開始します。");
            }
            catch (OperationCanceledException) when (_lifetimeCts.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                AppendLog(
                    $"[ERROR] 死活監視が予期せず停止しました: {ex.GetType().Name}: {ex.Message}。再開始します。");
            }

            try
            {
                await Task.Delay(HealthMonitorRestartDelay, _lifetimeCts.Token)
                    .ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private void OnDriveHealthChanged(bool healthy, string detail)
    {
        if (!Dispatcher.CheckAccess())
        {
            UiDispatch.BeginInvoke(Dispatcher, () => OnDriveHealthChanged(healthy, detail));
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
            _trayIcon.ShowBalloonTip(BalloonTipMilliseconds);
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

                AppendLog($"接続が復帰しました。Google Drive 上のアプリ（{launchEntries.Count} 件）を再開します。");
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
}
