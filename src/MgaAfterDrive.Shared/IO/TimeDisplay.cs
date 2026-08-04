namespace MgaAfterDrive.IO;

/// <summary>
/// UI / ログ向けの時間表示フォーマット。
/// </summary>
public static class TimeDisplay
{
    public static string FormatCountdown(TimeSpan remaining)
    {
        if (remaining < TimeSpan.Zero)
        {
            remaining = TimeSpan.Zero;
        }

        var totalSeconds = (int)Math.Ceiling(remaining.TotalSeconds);
        return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
    }

    public static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalMinutes >= 1 && duration.Seconds == 0)
        {
            return $"{(int)duration.TotalMinutes} 分";
        }

        return $"{Math.Max(0, (int)Math.Ceiling(duration.TotalSeconds))} 秒";
    }
}
