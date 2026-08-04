using System.Text.Json;

namespace MgaAfterDrive.IO;

/// <summary>
/// delay-entries.json の読み取り。
/// </summary>
public static class DelayEntriesReader
{
    public static IReadOnlyList<DelayEntryRecord> Load()
    {
        var path = AppPaths.GetDelayEntriesFilePath();
        if (!File.Exists(path))
        {
            return Array.Empty<DelayEntryRecord>();
        }

        try
        {
            var json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
            {
                return Array.Empty<DelayEntryRecord>();
            }

            var entries = JsonSerializer.Deserialize<List<DelayEntryRecord>>(json, AppJson.Compact) ?? [];
            if (!DelayEntriesJson.HasRestartProperty(json))
            {
                DelayEntriesJson.TryMigrateDriveRestart(entries);
            }

            return entries;
        }
        catch (Exception ex) when (
            ex is IOException
                or UnauthorizedAccessException
                or JsonException
                or NotSupportedException)
        {
            return Array.Empty<DelayEntryRecord>();
        }
    }
}
