using System.Diagnostics;

namespace MGA_AfterDrive.IO;

/// <summary>
/// Google Drive のマウント解決、プロセス待機、アクセス確認を行う。
/// </summary>
public static class GoogleDriveStartupProbe
{
    private const string ProcessName = "GoogleDriveFS";
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(3);

    /// <param name="log">ログ出力（タイムスタンプは呼び出し側が付与）。</param>
    /// <param name="setTitleStatus">ウィンドウタイトル状態。null で解除。</param>
    /// <returns>解決できてアクセス可能なとき true。</returns>
    public static async Task<bool> RunAsync(
        Action<string> log,
        Action<string?> setTitleStatus,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(setTitleStatus);

        var maxWait = AppSettingsStore.Load().GetMaxWait();
        var succeeded = false;
        try
        {
            log("Google Drive の確認を開始します。");
            log($"最大待機時間: {FormatDuration(maxWait)}（設定値）。");

            if (!GoogleDriveLocator.TryGetMountPath(out var mountPath, out var detail))
            {
                log($"[ERROR] ドライブ文字の解決に失敗しました: {detail}");
                return false;
            }

            log($"ドライブ文字を解決しました: {mountPath}（{detail}）");

            bool processRunning;
            try
            {
                processRunning = IsProcessRunning();
            }
            catch (Exception ex)
            {
                log($"[ERROR] プロセス状態の取得に失敗しました: {ex.GetType().Name}: {ex.Message}");
                return false;
            }

            if (processRunning)
            {
                log($"プロセス {ProcessName} は既に起動しています。");
            }
            else
            {
                log($"プロセス {ProcessName} の起動を待機します（最大 {FormatDuration(maxWait)}）。");
                try
                {
                    var started = await WaitForProcessAsync(maxWait, setTitleStatus, cancellationToken);
                    if (!started)
                    {
                        log($"[ERROR] プロセス {ProcessName} の待機がタイムアウトしました（{FormatDuration(maxWait)}）。");
                        return false;
                    }

                    log($"プロセス {ProcessName} が起動しました。");
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    log($"[ERROR] プロセス待機中に失敗しました: {ex.GetType().Name}: {ex.Message}");
                    return false;
                }
            }

            log($"アクセスを確認しています: {mountPath}（最大 {FormatDuration(maxWait)}）");

            bool accessible;
            try
            {
                accessible = await WaitUntilAccessibleAsync(mountPath, maxWait, setTitleStatus, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                log($"[ERROR] アクセス確認中に失敗しました: {ex.GetType().Name}: {ex.Message}");
                return false;
            }

            if (accessible)
            {
                log($"アクセス可能です: {mountPath}");
                succeeded = true;
            }
            else
            {
                log($"[ERROR] アクセスできません（タイムアウト {FormatDuration(maxWait)}）: {mountPath}");
            }

            return succeeded;
        }
        finally
        {
            setTitleStatus(null);
            log(succeeded
                ? "Google Drive の確認が正常に完了しました。"
                : "Google Drive の確認がエラーで終了しました。");
        }
    }

    private static async Task<bool> WaitForProcessAsync(
        TimeSpan maxWait,
        Action<string?> setTitleStatus,
        CancellationToken cancellationToken)
    {
        var remaining = maxWait;

        while (!IsProcessRunning())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (remaining <= TimeSpan.Zero)
            {
                return false;
            }

            if (OperationPause.ShouldPause())
            {
                setTitleStatus($"{ProcessName} 待機を一時停止中（{OperationPause.DescribeReason()}）");
                await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
                continue;
            }

            setTitleStatus($"{ProcessName} 待機中 {FormatCountdown(remaining)}");
            var slice = remaining < PollInterval ? remaining : PollInterval;
            await Task.Delay(slice, cancellationToken);
            remaining -= slice;
        }

        return true;
    }

    private static async Task<bool> WaitUntilAccessibleAsync(
        string mountPath,
        TimeSpan maxWait,
        Action<string?> setTitleStatus,
        CancellationToken cancellationToken)
    {
        var remaining = maxWait;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (TryAccess(mountPath, out _))
            {
                return true;
            }

            if (remaining <= TimeSpan.Zero)
            {
                return false;
            }

            if (OperationPause.ShouldPause())
            {
                setTitleStatus($"アクセス確認を一時停止中（{OperationPause.DescribeReason()}）");
                await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
                continue;
            }

            setTitleStatus($"アクセス確認中 {FormatCountdown(remaining)}");
            var slice = remaining < PollInterval ? remaining : PollInterval;
            await Task.Delay(slice, cancellationToken);
            remaining -= slice;
        }
    }

    private static string FormatCountdown(TimeSpan remaining)
    {
        if (remaining < TimeSpan.Zero)
        {
            remaining = TimeSpan.Zero;
        }

        var totalSeconds = (int)Math.Ceiling(remaining.TotalSeconds);
        var minutes = totalSeconds / 60;
        var seconds = totalSeconds % 60;
        return $"{minutes:00}:{seconds:00}";
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalMinutes >= 1 && duration.Seconds == 0)
        {
            return $"{(int)duration.TotalMinutes} 分";
        }

        return $"{duration.TotalSeconds:0} 秒";
    }

    internal static bool IsProcessRunning()
    {
        Process[] processes;
        try
        {
            processes = Process.GetProcessesByName(ProcessName);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            throw new InvalidOperationException($"プロセス {ProcessName} の列挙に失敗しました。", ex);
        }

        try
        {
            return processes.Length > 0;
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }

    internal static bool TryAccess(string mountPath, out string detail)
    {
        try
        {
            var root = Path.GetPathRoot(mountPath);
            if (!string.IsNullOrWhiteSpace(root) && root.Length >= 2 && root[1] == ':')
            {
                var drive = new DriveInfo(root);
                if (!drive.IsReady)
                {
                    detail = $"ドライブ {root} の準備ができていません（種類={drive.DriveType}）。";
                    return false;
                }
            }

            if (!Directory.Exists(mountPath))
            {
                detail = "ディレクトリが存在しません。";
                return false;
            }

            using var enumerator = Directory.EnumerateFileSystemEntries(mountPath).GetEnumerator();
            _ = enumerator.MoveNext();

            detail = "ルートの列挙に成功しました。";
            return true;
        }
        catch (Exception ex) when (
            ex is IOException
                or UnauthorizedAccessException
                or System.Security.SecurityException
                or DriveNotFoundException
                or DirectoryNotFoundException
                or NotSupportedException
                or ArgumentException)
        {
            detail = $"{ex.GetType().Name}: {ex.Message}";
            return false;
        }
    }
}
