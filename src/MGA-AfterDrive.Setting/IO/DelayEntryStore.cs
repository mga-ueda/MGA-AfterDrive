using System.Text.Json;
using MGA_AfterDrive.IO;
using MGA_AfterDrive.Setting.Models;

namespace MGA_AfterDrive.Setting.IO;

/// <summary>
/// 遅延実行リストの保存・読込。
/// </summary>
public static class DelayEntryStore
{
    public static string GetStoreDirectory() => AppPaths.GetStoreDirectory();

    public static string GetStoreFilePath() => AppPaths.GetDelayEntriesFilePath();

    public static IReadOnlyList<DelayEntry> Load() => Load(out _, out _);

    /// <summary>
    /// エントリを読み込む。
    /// </summary>
    /// <param name="missingRestartProperty">JSON に Restart プロパティが無い旧形式のとき true。</param>
    /// <param name="migratedDriveRestart">Drive 配下の Restart を補完できたとき true。</param>
    public static IReadOnlyList<DelayEntry> Load(out bool missingRestartProperty, out bool migratedDriveRestart)
    {
        missingRestartProperty = false;
        migratedDriveRestart = false;
        var path = GetStoreFilePath();
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

        // Restart 導入前の設定: Drive 配下だけ自動 ON（明示 false は存在しない）
        foreach (var entry in entries)
        {
            if (!entry.Restart && GoogleDriveLocator.IsPathUnderGoogleDrive(entry.Path))
            {
                entry.Restart = true;
                migratedDriveRestart = true;
            }
        }

        return entries;
    }

    public static void Save(IEnumerable<DelayEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var directory = GetStoreDirectory();
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
        File.WriteAllText(GetStoreFilePath(), json);
    }
}
