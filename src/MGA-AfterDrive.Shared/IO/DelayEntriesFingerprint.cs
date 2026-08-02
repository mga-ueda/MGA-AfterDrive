namespace MGA_AfterDrive.IO;

/// <summary>
/// delay-entries.json の変更検知用フィンガープリント。
/// </summary>
public static class DelayEntriesFingerprint
{
    public static string Capture()
    {
        var path = AppPaths.GetDelayEntriesFilePath();
        try
        {
            if (!File.Exists(path))
            {
                return "missing";
            }

            var info = new FileInfo(path);
            return $"{info.LastWriteTimeUtc.Ticks}:{info.Length}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return "unavailable";
        }
    }

    public static bool Equals(string? left, string? right) =>
        string.Equals(left, right, StringComparison.Ordinal);
}
