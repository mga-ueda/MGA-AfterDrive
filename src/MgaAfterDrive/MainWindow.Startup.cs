using System.Windows;
using MgaAfterDrive.Dialogs;
using MgaAfterDrive.IO;

namespace MgaAfterDrive;

public partial class MainWindow
{
    private static readonly TimeSpan HideToTrayAfterLaunchDelay = TimeSpan.FromSeconds(2);

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
                    HideToTrayAfterLaunchDelay,
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
}
