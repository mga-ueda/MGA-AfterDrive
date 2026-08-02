using System.Diagnostics;

namespace MGA_AfterDrive.IO;

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
        Action<string?> setTitleStatus,
        CancellationToken cancellationToken,
        bool respectEntryDelay = true)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(setTitleStatus);

        var actionVerb = respectEntryDelay ? "起動" : "再開";
        log(respectEntryDelay
            ? $"遅延起動を開始します（{entries.Count} 件）。"
            : $"再開を開始します（{entries.Count} 件、Delay 待ちなし）。");

        var currentEntries = entries.ToList();
        var launchedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var elapsedActive = TimeSpan.Zero;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var pending = currentEntries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Path))
                .Select(entry => (Entry: entry, Path: NormalizePath(entry.Path)))
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
                    log($"[{step}] {FormatDuration(wait)} 待機してから{actionVerb}: {label}");
                    var waitResult = await WaitWithCountdownAsync(
                            wait,
                            label,
                            setTitleStatus,
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
                    log($"[{step}] 直ちに{actionVerb}: {label}");
                }
            }
            else
            {
                log($"[{step}] 直ちに{actionVerb}: {label}");
            }

            LaunchOne(entry, label, step, log, actionVerb);
            launchedPaths.Add(path);
        }

        setTitleStatus(null);
        log("すべての起動エントリを処理しました。");
    }

    private static void LaunchOne(
        DelayEntryRecord entry,
        string label,
        string step,
        Action<string> log,
        string actionVerb)
    {
        var filePath = entry.Path?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(filePath))
        {
            log($"[{step}] [ERROR] パスが空です: {label}");
            return;
        }

        if (!File.Exists(filePath))
        {
            log($"[{step}] [ERROR] ファイルが見つかりません: {filePath}");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = filePath,
                Arguments = entry.Option ?? string.Empty,
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(filePath) ?? Environment.CurrentDirectory,
            });
            log($"[{step}] {actionVerb}しました: {filePath}");
        }
        catch (Exception ex) when (
            ex is InvalidOperationException
                or System.ComponentModel.Win32Exception
                or IOException
                or UnauthorizedAccessException)
        {
            log($"[{step}] [ERROR] {actionVerb}に失敗しました ({label}): {ex.Message}");
        }
    }

    private static Task<PauseAwareCountdown.WaitResult> WaitWithCountdownAsync(
        TimeSpan wait,
        string label,
        Action<string?> setTitleStatus,
        CancellationToken cancellationToken)
    {
        return PauseAwareCountdown.WaitAsync(
            wait,
            TimeSpan.FromSeconds(1),
            remaining => $"{label} 起動まで {FormatCountdown(remaining)}",
            () => $"{label} 起動待機を一時停止中（{OperationPause.DescribeReason()}）",
            setTitleStatus,
            detectSettingEntryChanges: true,
            cancellationToken);
    }

    private static string NormalizePath(string path)
    {
        try
        {
            return Path.GetFullPath(path.Trim());
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return path.Trim();
        }
    }

    private static string FormatCountdown(TimeSpan remaining)
    {
        if (remaining < TimeSpan.Zero)
        {
            remaining = TimeSpan.Zero;
        }

        var totalSeconds = (int)Math.Ceiling(remaining.TotalSeconds);
        return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalMinutes >= 1 && duration.Seconds == 0)
        {
            return $"{(int)duration.TotalMinutes} 分";
        }

        return $"{Math.Max(0, (int)Math.Ceiling(duration.TotalSeconds))} 秒";
    }
}
