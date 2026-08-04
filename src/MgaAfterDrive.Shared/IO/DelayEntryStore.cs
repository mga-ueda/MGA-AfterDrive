using System.Text.Json;

namespace MgaAfterDrive.IO;

/// <summary>
/// 遅延実行リストの保存・読込（Setting 向け・失敗は例外）。
/// </summary>
public static class DelayEntryStore
{
    /// <summary>
    /// エントリを読み込む。
    /// </summary>
    /// <param name="missingRestartProperty">JSON に Restart プロパティが無い旧形式のとき true。</param>
    /// <param name="migratedDriveRestart">Drive 配下の Restart を補完できたとき true。</param>
    public static IReadOnlyList<DelayEntry> Load(out bool missingRestartProperty, out bool migratedDriveRestart)
    {
        missingRestartProperty = false;
        migratedDriveRestart = false;

        var json = DelayEntriesFile.ReadTextOrEmpty();
        if (json is null)
        {
            return Array.Empty<DelayEntry>();
        }

        var entries = JsonSerializer.Deserialize<List<DelayEntry>>(json, AppJson.Indented) ?? [];
        DelayEntriesFile.ApplyRestartMigrationIfNeeded(
            json,
            entries,
            out missingRestartProperty,
            out migratedDriveRestart);
        return entries;
    }

    public static void Save(IEnumerable<DelayEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var directory = AppPaths.GetStoreDirectory();
        Directory.CreateDirectory(directory);

        var payload = entries
            .Select(entry => new DelayEntry
            {
                Delay = entry.Delay,
                FileName = entry.FileName,
                Path = entry.Path,
                Option = entry.Option,
                Restart = entry.Restart,
            })
            .ToList();

        var json = JsonSerializer.Serialize(payload, AppJson.Indented);
        File.WriteAllText(AppPaths.GetDelayEntriesFilePath(), json);
    }
}
