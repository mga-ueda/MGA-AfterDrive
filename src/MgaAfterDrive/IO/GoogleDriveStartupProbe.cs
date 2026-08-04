namespace MgaAfterDrive.IO;

/// <summary>
/// Google Drive のマウント解決、プロセス待機、アクセス確認を行う。
/// </summary>
public static class GoogleDriveStartupProbe
{
    private const string ProcessName = "GoogleDriveFS";
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan PausePollInterval = TimeSpan.FromMilliseconds(200);

    /// <param name="log">ログ出力（タイムスタンプは呼び出し側が付与）。</param>
    /// <param name="setStatusText">ステータスバー表示。null で解除。</param>
    /// <returns>解決できてアクセス可能なとき true。</returns>
    public static async Task<bool> RunAsync(
        Action<string> log,
        Action<string?> setStatusText,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(setStatusText);

        var maxWait = AppSettingsStore.Load().GetMaxWait();
        var succeeded = false;
        try
        {
            log("Google Drive の確認を開始します。");
            log($"最大待機時間: {TimeDisplay.FormatDuration(maxWait)}（設定値）。");

            if (!GoogleDriveLocator.TryGetMountPath(out var mountPath, out var detail))
            {
                log($"[ERROR] ドライブ文字の解決に失敗しました: {detail}");
                return false;
            }

            log($"ドライブ文字を解決しました: {mountPath}");

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
                log($"プロセス {ProcessName} の起動を待機します（最大 {TimeDisplay.FormatDuration(maxWait)}）。");
                try
                {
                    var started = await WaitForProcessAsync(maxWait, setStatusText, cancellationToken);
                    if (!started)
                    {
                        log($"[ERROR] プロセス {ProcessName} の待機がタイムアウトしました（{TimeDisplay.FormatDuration(maxWait)}）。");
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

            log($"アクセスを確認しています: {mountPath}（最大 {TimeDisplay.FormatDuration(maxWait)}）");

            bool accessible;
            try
            {
                accessible = await WaitUntilAccessibleAsync(mountPath, maxWait, setStatusText, cancellationToken);
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
                log($"[ERROR] アクセスできません（タイムアウト {TimeDisplay.FormatDuration(maxWait)}）: {mountPath}");
            }

            return succeeded;
        }
        finally
        {
            setStatusText(null);
            log(succeeded
                ? "Google Drive の確認が正常に完了しました。"
                : "Google Drive の確認がエラーで終了しました。");
        }
    }

    private static Task<bool> WaitForProcessAsync(
        TimeSpan maxWait,
        Action<string?> setStatusText,
        CancellationToken cancellationToken) =>
        WaitUntilAsync(
            IsProcessRunning,
            maxWait,
            $"{ProcessName} 待機",
            setStatusText,
            cancellationToken);

    private static Task<bool> WaitUntilAccessibleAsync(
        string mountPath,
        TimeSpan maxWait,
        Action<string?> setStatusText,
        CancellationToken cancellationToken) =>
        WaitUntilAsync(
            () => TryAccess(mountPath, out _),
            maxWait,
            "アクセス確認",
            setStatusText,
            cancellationToken);

    /// <summary>
    /// 条件成立まで待機。<see cref="OperationPause"/> 中は残り時間を減らさない。
    /// </summary>
    private static async Task<bool> WaitUntilAsync(
        Func<bool> isDone,
        TimeSpan maxWait,
        string label,
        Action<string?> setStatusText,
        CancellationToken cancellationToken)
    {
        var remaining = maxWait;

        while (!isDone())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (remaining <= TimeSpan.Zero)
            {
                return false;
            }

            if (OperationPause.ShouldPause())
            {
                setStatusText($"{label}を一時停止中（{OperationPause.DescribeReason()}）");
                await Task.Delay(PausePollInterval, cancellationToken).ConfigureAwait(false);
                continue;
            }

            setStatusText($"{label}中 {TimeDisplay.FormatCountdown(remaining)}");
            var slice = remaining < PollInterval ? remaining : PollInterval;
            if (slice > PausePollInterval)
            {
                slice = PausePollInterval;
            }

            await Task.Delay(slice, cancellationToken).ConfigureAwait(false);
            if (OperationPause.ShouldPause())
            {
                continue;
            }

            remaining -= slice;
        }

        return true;
    }

    internal static bool IsProcessRunning()
    {
        if (!ProcessExecutable.TryAnyByName(ProcessName, out var any, out var error))
        {
            throw new InvalidOperationException(
                $"プロセス {ProcessName} の列挙に失敗しました。",
                error);
        }

        return any;
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
