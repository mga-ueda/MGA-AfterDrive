using System.Text.Json;
using Microsoft.Win32;

namespace MGA_G_Delay_Run.IO;

/// <summary>
/// Resolves the Google Drive for desktop mount path (drive letter / path).
/// </summary>
public static class GoogleDriveLocator
{
    private const string RegistryPath = @"Software\Google\DriveFS";
    private const string PreferencesValueName = "PerAccountPreferences";
    private const string CurrentAccountValueName = "CurrentAccountToken";

    public static bool TryGetMountPath(out string mountPath, out string detail)
    {
        mountPath = string.Empty;
        detail = string.Empty;

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath);
            if (key is null)
            {
                detail = @"Registry key HKCU\Software\Google\DriveFS was not found.";
                return false;
            }

            if (key.GetValue(PreferencesValueName) is not string json || string.IsNullOrWhiteSpace(json))
            {
                detail = "PerAccountPreferences is empty.";
                return false;
            }

            var currentAccount = key.GetValue(CurrentAccountValueName) as string;

            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("per_account_preferences", out var preferences)
                || preferences.ValueKind != JsonValueKind.Array)
            {
                detail = "Unable to parse per_account_preferences.";
                return false;
            }

            string? fallbackRaw = null;

            foreach (var entry in preferences.EnumerateArray())
            {
                if (!TryReadMountPoint(entry, out var accountKey, out var raw))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(currentAccount)
                    && string.Equals(accountKey, currentAccount, StringComparison.Ordinal))
                {
                    return FinalizeMountPath(
                        raw,
                        $"CurrentAccountToken={currentAccount}, mount_point_path={raw}",
                        out mountPath,
                        out detail);
                }

                fallbackRaw ??= raw;
            }

            if (fallbackRaw is not null)
            {
                var reason = string.IsNullOrWhiteSpace(currentAccount)
                    ? $"mount_point_path={fallbackRaw}"
                    : $"No match for CurrentAccountToken={currentAccount}. Falling back to mount_point_path={fallbackRaw}";
                return FinalizeMountPath(fallbackRaw, reason, out mountPath, out detail);
            }

            detail = "mount_point_path is not configured.";
            return false;
        }
        catch (JsonException ex)
        {
            detail = $"JSON parse error: {ex.Message}";
            return false;
        }
        catch (ArgumentException ex)
        {
            detail = $"Invalid path: {ex.Message}";
            return false;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
        {
            detail = $"Registry access denied: {ex.Message}";
            return false;
        }
        catch (Exception ex)
        {
            detail = $"Lookup error ({ex.GetType().Name}): {ex.Message}";
            return false;
        }
    }

    private static bool TryReadMountPoint(JsonElement entry, out string? accountKey, out string raw)
    {
        accountKey = null;
        raw = string.Empty;

        if (entry.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (entry.TryGetProperty("key", out var keyElement) && keyElement.ValueKind == JsonValueKind.String)
        {
            accountKey = keyElement.GetString();
        }

        if (!entry.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!value.TryGetProperty("mount_point_path", out var mountPoint)
            || mountPoint.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var mount = mountPoint.GetString();
        if (string.IsNullOrWhiteSpace(mount))
        {
            return false;
        }

        raw = mount;
        return true;
    }

    private static bool FinalizeMountPath(string raw, string reason, out string mountPath, out string detail)
    {
        mountPath = NormalizeMountPath(raw);
        detail = reason;

        if (!IsPlausibleMountPath(mountPath, out var validationError))
        {
            detail = $"{reason}; validation failed: {validationError}";
            mountPath = string.Empty;
            return false;
        }

        return true;
    }

    private static string NormalizeMountPath(string raw)
    {
        var trimmed = raw.Trim().TrimEnd('\\', '/');

        if (trimmed.Length == 1 && char.IsAsciiLetter(trimmed[0]))
        {
            return $"{char.ToUpperInvariant(trimmed[0])}:\\";
        }

        if (trimmed.Length == 2 && char.IsAsciiLetter(trimmed[0]) && trimmed[1] == ':')
        {
            return $"{char.ToUpperInvariant(trimmed[0])}:\\";
        }

        return Path.GetFullPath(trimmed);
    }

    private static bool IsPlausibleMountPath(string mountPath, out string error)
    {
        if (string.IsNullOrWhiteSpace(mountPath))
        {
            error = "Path is empty.";
            return false;
        }

        try
        {
            var root = Path.GetPathRoot(mountPath);
            if (string.IsNullOrWhiteSpace(root))
            {
                error = "Unable to get root path.";
                return false;
            }

            if (root.Length >= 2 && root[1] == ':')
            {
                _ = new DriveInfo(root);
            }

            error = string.Empty;
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            error = ex.Message;
            return false;
        }
    }
}
