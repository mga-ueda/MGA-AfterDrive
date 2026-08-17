namespace MgaAfterDrive.IO;

/// <summary>
/// Setting に登録されたアプリを Delay 順に順次起動する。
/// </summary>
public static class DelayedLaunchRunner
{
    /// <param name="respectEntryDelay">
    /// true: 各エントリの Delay（秒）をシーケンス開始からの待機に使う。
    /// false: Delay を無視して登録順（Delay 昇順）に連続起動する（復帰時向け）。
    /// </param>
    public static async Task RunAsync(
        IReadOnlyList<DelayEntryRecord> entries,
        Action<string> log,
        Action<string?> setStatusText,
        CancellationToken cancellationToken,
        bool respectEntryDelay = true)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(setStatusText);

        var actionVerb = respectEntryDelay ? "起動" : "再開";
        log(respectEntryDelay
            ? $"遅延起動を開始します（{entries.Count} 件）。"
            : $"再開を開始します（{entries.Count} 件、Delay 待ちなし）。");

        var currentEntries = entries.ToList();
        var launchedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var skippedLabels = new List<string>();
        var failedLabels = new List<string>();
        var elapsedActive = TimeSpan.Zero;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var pending = currentEntries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Path))
                .Select(entry => (
                    Entry: entry,
                    Path: PathUtil.TryNormalize(entry.Path, out var normalized)
                        ? normalized
                        : entry.Path.Trim()))
                .Where(item => item.Path.Length > 0 && !launchedPaths.Contains(item.Path))
                .OrderBy(item => item.Entry.Delay)
                .ThenBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (pending.Length == 0)
            {
                break;
            }

            var (entry, path) = pending[0];
            var label = string.IsNullOrWhiteSpace(entry.FileName)
                ? Path.GetFileName(entry.Path)
                : entry.FileName;
            var step = $"{launchedPaths.Count + 1}/{launchedPaths.Count + pending.Length}";

            if (respectEntryDelay)
            {
                var targetDelay = TimeSpan.FromSeconds(Math.Max(0, entry.Delay));
                var wait = targetDelay - elapsedActive;

                if (wait > TimeSpan.Zero)
                {
                    log($"[{step}] {TimeDisplay.FormatDuration(wait)} 待機してから{actionVerb}: {label}");
                    var waitResult = await WaitWithCountdownAsync(
                            wait,
                            label,
                            setStatusText,
                            cancellationToken)
                        .ConfigureAwait(false);

                    elapsedActive += waitResult.Elapsed;

                    if (waitResult.InterruptedForReload)
                    {
                        currentEntries = DelayEntriesReader.Load().ToList();
                        log("設定の変更を検知したため、起動タイマーを再設定します。");
                        continue;
                    }
                }
                else
                {
                    log($"[{step}] {actionVerb}: {label}");
                }
            }
            else
            {
                log($"[{step}] {actionVerb}: {label}");
            }

            var outcome = LaunchOne(entry, label, step, log, actionVerb);
            if (outcome == LaunchOutcome.SkippedAlreadyRunning)
            {
                skippedLabels.Add(label);
            }
            else if (outcome == LaunchOutcome.Failed)
            {
                failedLabels.Add(label);
            }

            launchedPaths.Add(path);
        }

        setStatusText(null);
        log("すべての起動エントリを処理しました。");
        if (skippedLabels.Count > 0)
        {
            log($"[WARN] 起動済みのためスキップしたアプリがあります（{skippedLabels.Count} 件）。");
        }

        if (failedLabels.Count > 0)
        {
            log($"[ERROR] {actionVerb}できなかったアプリがあります（{failedLabels.Count} 件）。");
        }
    }

    private enum LaunchOutcome
    {
        Started,
        SkippedAlreadyRunning,
        Failed,
    }

    private static LaunchOutcome LaunchOne(
        DelayEntryRecord entry,
        string label,
        string step,
        Action<string> log,
        string actionVerb)
    {
        var filePath = entry.Path?.Trim() ?? string.Empty;
        if (ProcessExecutable.IsRunning(filePath))
        {
            log($"[{step}] [WARN] 起動済みのためスキップしました: {label}");
            return LaunchOutcome.SkippedAlreadyRunning;
        }

        if (!ProcessLaunch.TryStart(filePath, entry.Option, out var error))
        {
            log($"[{step}] [ERROR] {actionVerb}に失敗しました ({label}): {error}");
            return LaunchOutcome.Failed;
        }

        log($"[{step}] {actionVerb}しました: {filePath}");
        return LaunchOutcome.Started;
    }

    private static Task<PauseAwareCountdown.WaitResult> WaitWithCountdownAsync(
        TimeSpan wait,
        string label,
        Action<string?> setStatusText,
        CancellationToken cancellationToken)
    {
        return PauseAwareCountdown.WaitAsync(
            wait,
            TimeSpan.FromSeconds(1),
            remaining => $"{label} 起動まで {TimeDisplay.FormatCountdown(remaining)}",
            () => $"{label} 起動待機を一時停止中（{OperationPause.DescribeReason()}）",
            setStatusText,
            detectSettingEntryChanges: true,
            cancellationToken);
    }
}
