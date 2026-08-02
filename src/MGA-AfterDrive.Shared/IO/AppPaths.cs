namespace MGA_AfterDrive.IO;

/// <summary>
/// アプリ設定の共通パス。
/// </summary>
public static class AppPaths
{
    public const string ProductFolderName = "MGA AfterDrive";
    public const string LegacyProductFolderName = "MGA G Delay Run";

    public const string DelayEntriesFileName = "delay-entries.json";
    public const string SettingsFileName = "settings.json";

    public static string GetStoreDirectory()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MGA");
        var dir = Path.Combine(root, ProductFolderName);
        MigrateLegacyStoreDirectory(root, dir);
        return dir;
    }

    public static string GetDelayEntriesFilePath() =>
        Path.Combine(GetStoreDirectory(), DelayEntriesFileName);

    public static string GetSettingsFilePath() =>
        Path.Combine(GetStoreDirectory(), SettingsFileName);

    /// <summary>
    /// 旧正式名称フォルダがあれば新名称へ移す（設定・エントリの継承）。
    /// </summary>
    private static void MigrateLegacyStoreDirectory(string root, string newDirectory)
    {
        var legacy = Path.Combine(root, LegacyProductFolderName);
        if (Directory.Exists(newDirectory) || !Directory.Exists(legacy))
        {
            return;
        }

        try
        {
            Directory.Move(legacy, newDirectory);
        }
        catch (Exception ex) when (
            ex is IOException
                or UnauthorizedAccessException
                or NotSupportedException)
        {
            // 移行失敗時は新規フォルダを使う（呼び出し側で CreateDirectory）
        }
    }
}
