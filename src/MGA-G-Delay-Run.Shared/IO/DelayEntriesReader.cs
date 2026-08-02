using System.Text.Json;

namespace MGA_G_Delay_Run.IO;

/// <summary>
/// delay-entries.json の読み取り。
/// </summary>
public static class DelayEntriesReader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

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

            var entries = JsonSerializer.Deserialize<List<DelayEntryRecord>>(json, JsonOptions);
            return entries ?? [];
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
