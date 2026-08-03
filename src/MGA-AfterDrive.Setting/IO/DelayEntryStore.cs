using System.Text.Json;
using MGA_AfterDrive.IO;
using MGA_AfterDrive.Setting.Models;

namespace MGA_AfterDrive.Setting.IO;

/// <summary>
/// 遅延実行リストの保存・読込。
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
        var path = AppPaths.GetDelayEntriesFilePath();
        if (!File.Exists(path))
        {
            return Array.Empty<DelayEntry>();
        }

        var json = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<DelayEntry>();
        }

        var entries = JsonSerializer.Deserialize<List<DelayEntry>>(json, AppJson.Indented) ?? [];
        if (DelayEntriesJson.HasRestartProperty(json))
        {
            return entries;
        }

        missingRestartProperty = true;
        migratedDriveRestart = DelayEntriesJson.TryMigrateDriveRestart(entries);
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
