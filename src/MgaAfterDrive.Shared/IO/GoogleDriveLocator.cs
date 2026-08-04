using System.Text.Json;
using Microsoft.Win32;

namespace MgaAfterDrive.IO;

/// <summary>
/// Resolves the Google Drive for desktop mount path (drive letter / path).
/// </summary>
public static class GoogleDriveLocator
{
    private const string RegistryPath = @"Software\Google\DriveFS";
    private const string PreferencesValueName = "PerAccountPreferences";
    private const string CurrentAccountValueName = "CurrentAccountToken";

    /// <summary>
    /// 指定パスが Google Drive マウント配下かどうかを判定する。
    /// マウント解決に失敗した場合は false。
    /// </summary>
    public static bool IsPathUnderGoogleDrive(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (!TryGetMountPath(out var mountPath, out _))
        {
            return false;
        }

        return IsPathUnderMount(path, mountPath);
    }

    /// <summary>
    /// 指定パスがマウントルート配下（またはマウントルートそのもの）かを判定する。
    /// </summary>
    public static bool IsPathUnderMount(string path, string mountPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(mountPath);

        if (!PathUtil.TryNormalize(path, out var fullPath)
            || !PathUtil.TryNormalize(mountPath, out var fullMount))
        {
            return false;
        }

        var mountRoot = fullMount.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.Equals(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), mountRoot, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var prefix = mountRoot + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryGetMountPath(out string mountPath, out string detail)
    {
        mountPath = string.Empty;
        detail = string.Empty;

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath);
            if (key is null)
            {
                detail = @"レジストリキー HKCU\Software\Google\DriveFS が見つかりません。";
                return false;
            }

            if (key.GetValue(PreferencesValueName) is not string json || string.IsNullOrWhiteSpace(json))
            {
                detail = "PerAccountPreferences が空です。";
                return false;
            }

            var currentAccount = key.GetValue(CurrentAccountValueName) as string;

            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("per_account_preferences", out var preferences)
                || preferences.ValueKind != JsonValueKind.Array)
            {
                detail = "per_account_preferences を解析できません。";
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
                    : $"CurrentAccountToken={currentAccount} に一致なし。フォールバック mount_point_path={fallbackRaw}";
                return FinalizeMountPath(fallbackRaw, reason, out mountPath, out detail);
            }

            detail = "mount_point_path が設定されていません。";
            return false;
        }
        catch (JsonException ex)
        {
            detail = $"JSON 解析エラー: {ex.Message}";
            return false;
        }
        catch (ArgumentException ex)
        {
            detail = $"無効なパス: {ex.Message}";
            return false;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
        {
            detail = $"レジストリアクセスが拒否されました: {ex.Message}";
            return false;
        }
        catch (Exception ex)
        {
            detail = $"参照エラー（{ex.GetType().Name}）: {ex.Message}";
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
            detail = $"{reason}; 検証失敗: {validationError}";
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
            error = "パスが空です。";
            return false;
        }

        try
        {
            var root = Path.GetPathRoot(mountPath);
            if (string.IsNullOrWhiteSpace(root))
            {
                error = "ルートパスを取得できません。";
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
