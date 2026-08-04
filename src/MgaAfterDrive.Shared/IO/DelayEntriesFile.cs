using System.Text.Json;

namespace MgaAfterDrive.IO;

/// <summary>
/// delay-entries.json の生テキスト読み取り（読込経路の共通化）。
/// </summary>
internal static class DelayEntriesFile
{
    /// <summary>
    /// ファイルが無い・空・読込失敗時は null。例外は握りつぶす。
    /// </summary>
    public static string? TryReadText()
    {
        var path = AppPaths.GetDelayEntriesFilePath();
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(path);
            return string.IsNullOrWhiteSpace(json) ? null : json;
        }
        catch (Exception ex) when (
            ex is IOException
                or UnauthorizedAccessException
                or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>
    /// Setting 向け。無い・空は null。I/O 失敗は例外をそのまま投げる。
    /// </summary>
    public static string? ReadTextOrEmpty()
    {
        var path = AppPaths.GetDelayEntriesFilePath();
        if (!File.Exists(path))
        {
            return null;
        }

        var json = File.ReadAllText(path);
        return string.IsNullOrWhiteSpace(json) ? null : json;
    }

    public static void ApplyRestartMigrationIfNeeded<T>(
        string json,
        IList<T> entries,
        out bool missingRestartProperty,
        out bool migratedDriveRestart)
        where T : IRestartableDelayEntry
    {
        missingRestartProperty = false;
        migratedDriveRestart = false;
        if (DelayEntriesJson.HasRestartProperty(json))
        {
            return;
        }

        missingRestartProperty = true;
        migratedDriveRestart = DelayEntriesJson.TryMigrateDriveRestart(entries);
    }
}
