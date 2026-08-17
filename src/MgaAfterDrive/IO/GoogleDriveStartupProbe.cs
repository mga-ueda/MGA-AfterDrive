namespace MgaAfterDrive.IO;

/// <summary>
/// Google Drive のマウント解決、プロセス待機、アクセス確認を行う。
/// </summary>
public static class GoogleDriveStartupProbe
{
    private const string ProcessName = "GoogleDriveFS";
    /// <summary>ドライブ確認の試行間隔。短いと logon 直後の負荷をさらに上げる。</summary>
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
                processRunning = await Task.Run(IsProcessRunning, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
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
    /// 条件成立まで待機。<see cref="OperationPause"/> 中は期限を延ばす。
    /// カウントダウンは壁時計で更新し、ドライブ I/O の完了を待たない。
    /// </summary>
    private static async Task<bool> WaitUntilAsync(
        Func<bool> isDone,
        TimeSpan maxWait,
        string label,
        Action<string?> setStatusText,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + maxWait;
        var nextCheckAt = DateTime.UtcNow;
        Task<bool>? inFlight = null;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (inFlight is { IsCompleted: true })
            {
                var done = await inFlight.ConfigureAwait(false);
                inFlight = null;
                if (done)
                {
                    return true;
                }

                nextCheckAt = DateTime.UtcNow + PollInterval;
            }

            if (OperationPause.ShouldPause())
            {
                setStatusText($"{label}を一時停止中（{OperationPause.DescribeReason()}）");
                var pausedAt = DateTime.UtcNow;
                await Task.Delay(PausePollInterval, cancellationToken).ConfigureAwait(false);
                deadline += DateTime.UtcNow - pausedAt;
                continue;
            }

            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                if (inFlight is null)
                {
                    return false;
                }

                try
                {
                    return await inFlight.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    return false;
                }
            }

            setStatusText($"{label}中 {TimeDisplay.FormatCountdown(remaining)}");

            if (inFlight is null && DateTime.UtcNow >= nextCheckAt)
            {
                // キャンセルしても Directory.Exists 等は止まらないため、CT は載せない
                inFlight = Task.Run(isDone, CancellationToken.None);
            }

            if (inFlight is null)
            {
                await Task.Delay(PausePollInterval, cancellationToken).ConfigureAwait(false);
                continue;
            }

            await Task.WhenAny(inFlight, Task.Delay(PausePollInterval, cancellationToken))
                .ConfigureAwait(false);
        }
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
                or ArgumentException
                or OutOfMemoryException)
        {
            // Google Drive プロセスが落ちた／ハングしたあと、フィルタドライバ越しの I/O が
            // Win32 ERROR_NOT_ENOUGH_MEMORY を返し、.NET が OutOfMemoryException にすることがある。
            // ヒープ不足ではなく「マウントに手が出せない」ので、アクセス不可として扱う。
            detail = $"{ex.GetType().Name}: {ex.Message}";
            return false;
        }
    }
}
