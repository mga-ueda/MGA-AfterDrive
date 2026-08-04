using System.Text.Json;

namespace MgaAfterDrive.IO;

/// <summary>
/// アプリ共通の JSON シリアライズ設定。
/// </summary>
public static class AppJson
{
    public static JsonSerializerOptions Compact { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public static JsonSerializerOptions Indented { get; } = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };
}
