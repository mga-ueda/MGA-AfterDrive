using System.Text.Json;

namespace MgaAfterDrive.IO;

/// <summary>
/// delay-entries.json の読み取り（ホスト起動用・失敗時は空）。
/// </summary>
public static class DelayEntriesReader
{
    public static IReadOnlyList<DelayEntryRecord> Load()
    {
        var json = DelayEntriesFile.TryReadText();
        if (json is null)
        {
            return Array.Empty<DelayEntryRecord>();
        }

        try
        {
            var entries = JsonSerializer.Deserialize<List<DelayEntryRecord>>(json, AppJson.Compact) ?? [];
            DelayEntriesFile.ApplyRestartMigrationIfNeeded(json, entries, out _, out _);
            return entries;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return Array.Empty<DelayEntryRecord>();
        }
    }
}
