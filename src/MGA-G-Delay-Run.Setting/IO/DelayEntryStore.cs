using System.Text.Json;
using MGA_G_Delay_Run.IO;
using MGA_G_Delay_Run.Setting.Models;

namespace MGA_G_Delay_Run.Setting.IO;

/// <summary>
/// 遅延実行リストの保存・読込。
/// </summary>
public static class DelayEntryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string GetStoreDirectory() => AppPaths.GetStoreDirectory();

    public static string GetStoreFilePath() => AppPaths.GetDelayEntriesFilePath();

    public static IReadOnlyList<DelayEntry> Load()
    {
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

        var entries = JsonSerializer.Deserialize<List<DelayEntry>>(json, JsonOptions);
        return entries ?? [];
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

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        File.WriteAllText(GetStoreFilePath(), json);
    }
}
