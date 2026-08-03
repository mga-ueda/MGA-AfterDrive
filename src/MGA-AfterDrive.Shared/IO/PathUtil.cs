namespace MGA_AfterDrive.IO;

/// <summary>
/// パス正規化の共通処理。
/// </summary>
public static class PathUtil
{
    public static string Normalize(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Path.GetFullPath(path.Trim());
    }

    public static bool TryNormalize(string? path, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            normalized = Normalize(path);
            return true;
        }
        catch (Exception ex) when (
            ex is ArgumentException
                or NotSupportedException
                or PathTooLongException
                or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
