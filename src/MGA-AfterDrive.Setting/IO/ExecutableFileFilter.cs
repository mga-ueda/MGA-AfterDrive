namespace MGA_AfterDrive.Setting.IO;

/// <summary>
/// 登録可能な実行ファイルかどうかを判定する。
/// </summary>
public static class ExecutableFileFilter
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe",
        ".bat",
        ".cmd",
        ".com",
    };

    public static bool IsExecutable(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        try
        {
            if (!File.Exists(filePath))
            {
                return false;
            }

            var extension = Path.GetExtension(filePath);
            return AllowedExtensions.Contains(extension);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }
}
