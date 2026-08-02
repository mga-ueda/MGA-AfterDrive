namespace MGA_G_Delay_Run.IO;

/// <summary>
/// アプリ設定の共通パス。
/// </summary>
public static class AppPaths
{
    public const string DelayEntriesFileName = "delay-entries.json";
    public const string SettingsFileName = "settings.json";

    public static string GetStoreDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MGA",
            "MGA G Delay Run");
    }

    public static string GetDelayEntriesFilePath() =>
        Path.Combine(GetStoreDirectory(), DelayEntriesFileName);

    public static string GetSettingsFilePath() =>
        Path.Combine(GetStoreDirectory(), SettingsFileName);
}
