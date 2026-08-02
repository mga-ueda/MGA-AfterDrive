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

        var ordered = entries
            .OrderBy(entry => entry.Delay)
            .ThenBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var actionVerb = respectEntryDelay ? "起動" : "再開";
        log(respectEntryDelay
            ? $"遅延起動を開始します（{ordered.Length} 件）。"
            : $"再開を開始します（{ordered.Length} 件、Delay 待ちなし）。");

        // エントリ間の差分待機にすると、設定ウィンドウによる一時停止を残り時間に正しく反映できる
        var previousDelaySeconds = 0;

        for (var index = 0; index < ordered.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var entry = ordered[index];
            var label = string.IsNullOrWhiteSpace(entry.FileName)
                ? Path.GetFileName(entry.Path)
                : entry.FileName;
            var step = $"{index + 1}/{ordered.Length}";

            if (respectEntryDelay)
            {
                var delaySeconds = Math.Max(0, entry.Delay);
                var gapSeconds = Math.Max(0, delaySeconds - previousDelaySeconds);
                previousDelaySeconds = Math.Max(previousDelaySeconds, delaySeconds);

                if (gapSeconds > 0)
                {
                    var wait = TimeSpan.FromSeconds(gapSeconds);
                    log($"[{step}] {FormatDuration(wait)} 待機してから{actionVerb}: {label}");
                    await WaitWithCountdownAsync(wait, label, setTitleStatus, cancellationToken);
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

    private static Task WaitWithCountdownAsync(
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
            cancellationToken);
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
