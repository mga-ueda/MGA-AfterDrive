using System.Diagnostics;

namespace MGA_G_Delay_Run.IO;

/// <summary>
/// Setting に登録されたアプリを Delay 順に順次起動する。
/// </summary>
public static class DelayedLaunchRunner
{
    public static async Task RunAsync(
        IReadOnlyList<DelayEntryRecord> entries,
        Action<string> log,
        Action<string?> setTitleStatus,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(setTitleStatus);

        var ordered = entries
            .OrderBy(entry => entry.Delay)
            .ThenBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        log($"Starting delayed launch for {ordered.Length} entr{(ordered.Length == 1 ? "y" : "ies")}.");

        var phaseStartedAt = DateTime.UtcNow;

        for (var index = 0; index < ordered.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var entry = ordered[index];
            var label = string.IsNullOrWhiteSpace(entry.FileName)
                ? Path.GetFileName(entry.Path)
                : entry.FileName;
            var step = $"{index + 1}/{ordered.Length}";

            var delaySeconds = Math.Max(0, entry.Delay);
            var targetAt = phaseStartedAt + TimeSpan.FromSeconds(delaySeconds);
            var wait = targetAt - DateTime.UtcNow;

            if (wait > TimeSpan.Zero)
            {
                log($"[{step}] Waiting {FormatDuration(wait)} before launching: {label}");
                await WaitWithCountdownAsync(wait, label, setTitleStatus, cancellationToken);
            }
            else
            {
                log($"[{step}] Launching now: {label}");
            }

            LaunchOne(entry, label, step, log);
        }

        setTitleStatus(null);
        log("All launch entries processed.");
    }

    private static void LaunchOne(DelayEntryRecord entry, string label, string step, Action<string> log)
    {
        var filePath = entry.Path?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(filePath))
        {
            log($"[{step}] [ERROR] Path is empty: {label}");
            return;
        }

        if (!File.Exists(filePath))
        {
            log($"[{step}] [ERROR] File not found: {filePath}");
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
            log($"[{step}] Started: {filePath}");
        }
        catch (Exception ex) when (
            ex is InvalidOperationException
                or System.ComponentModel.Win32Exception
                or IOException
                or UnauthorizedAccessException)
        {
            log($"[{step}] [ERROR] Failed to start {label}: {ex.Message}");
        }
    }

    private static async Task WaitWithCountdownAsync(
        TimeSpan wait,
        string label,
        Action<string?> setTitleStatus,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + wait;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                break;
            }

            setTitleStatus($"Launch {label} in {FormatCountdown(remaining)}");
            var slice = remaining < TimeSpan.FromSeconds(1) ? remaining : TimeSpan.FromSeconds(1);
            await Task.Delay(slice, cancellationToken);
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
            return $"{(int)duration.TotalMinutes} min";
        }

        return $"{Math.Max(0, (int)Math.Ceiling(duration.TotalSeconds))} sec";
    }
}
