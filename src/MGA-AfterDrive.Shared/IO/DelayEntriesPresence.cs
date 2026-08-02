using System.Text.Json;

namespace MGA_AfterDrive.IO;

/// <summary>
/// 遅延実行リストが保存されているか判定する。
/// </summary>
public static class DelayEntriesPresence
{
    public static bool HasAny()
    {
        var path = AppPaths.GetDelayEntriesFilePath();
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            var json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Array
                && document.RootElement.GetArrayLength() > 0;
        }
        catch (Exception ex) when (
            ex is IOException
                or UnauthorizedAccessException
                or JsonException
                or NotSupportedException)
        {
            return false;
        }
    }
}
