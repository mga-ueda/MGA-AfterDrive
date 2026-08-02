using System.Text.Json;

namespace MGA_AfterDrive.IO;

/// <summary>
/// delay-entries.json のプロパティ有無判定。
/// </summary>
public static class DelayEntriesJson
{
    /// <summary>
    /// 配列要素のいずれかに Restart プロパティがあるか（値の部分一致は見ない）。
    /// </summary>
    public static bool HasRestartProperty(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                foreach (var property in element.EnumerateObject())
                {
                    if (string.Equals(property.Name, "Restart", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    /// <summary>
    /// Restart 未定義の旧データ向けに、Google Drive 配下エントリの Restart を ON にする。
    /// </summary>
    public static bool TryMigrateDriveRestart(IEnumerable<DelayEntryRecord> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var migrated = false;
        foreach (var entry in entries)
        {
            if (entry.Restart || string.IsNullOrWhiteSpace(entry.Path))
            {
                continue;
            }

            if (GoogleDriveLocator.IsPathUnderGoogleDrive(entry.Path))
            {
                entry.Restart = true;
                migrated = true;
            }
        }

        return migrated;
    }
}
