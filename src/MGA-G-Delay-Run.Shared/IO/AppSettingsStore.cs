using System.Text.Json;

namespace MGA_G_Delay_Run.IO;

/// <summary>
/// settings.json の読み書き。
/// </summary>
public static class AppSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public static AppSettings Load()
    {
        var path = AppPaths.GetSettingsFilePath();
        if (!File.Exists(path))
        {
            return new AppSettings();
        }

        try
        {
            var json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new AppSettings();
            }

            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            settings.MaxWaitSeconds = AppSettings.ClampMaxWaitSeconds(settings.MaxWaitSeconds);
            return settings;
        }
        catch (Exception ex) when (
            ex is IOException
                or UnauthorizedAccessException
                or JsonException
                or NotSupportedException)
        {
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        settings.MaxWaitSeconds = AppSettings.ClampMaxWaitSeconds(settings.MaxWaitSeconds);

        var directory = AppPaths.GetStoreDirectory();
        Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(AppPaths.GetSettingsFilePath(), json);
    }
}
