namespace MGA_AfterDrive.IO;

/// <summary>
/// アプリ設定の共通パス。
/// </summary>
public static class AppPaths
{
    public const string ProductFolderName = "MGA AfterDrive";

    public const string DelayEntriesFileName = "delay-entries.json";
    public const string SettingsFileName = "settings.json";

    /// <summary>
    /// 埋め込み Setting を展開するディレクトリ名。
    /// </summary>
    public const string BundledAppFolderName = "app";

    public static string GetStoreDirectory()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MGA",
            ProductFolderName);
        return dir;
    }

    public static string GetDelayEntriesFilePath() =>
        Path.Combine(GetStoreDirectory(), DelayEntriesFileName);

    public static string GetSettingsFilePath() =>
        Path.Combine(GetStoreDirectory(), SettingsFileName);

    public static string GetBundledAppDirectory() =>
        Path.Combine(GetStoreDirectory(), BundledAppFolderName);
}
