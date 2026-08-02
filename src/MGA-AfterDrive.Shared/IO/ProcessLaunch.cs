using System.Diagnostics;

namespace MGA_AfterDrive.IO;

/// <summary>
/// 登録アプリの起動（シェル実行）。
/// </summary>
public static class ProcessLaunch
{
    public static bool TryStart(string filePath, string? arguments, out string error)
    {
        error = string.Empty;
        var trimmed = filePath?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            error = "パスが空です。";
            return false;
        }

        if (!File.Exists(trimmed))
        {
            error = $"ファイルが見つかりません: {trimmed}";
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = trimmed,
                Arguments = arguments ?? string.Empty,
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(trimmed) ?? Environment.CurrentDirectory,
            });
            return true;
        }
        catch (Exception ex) when (
            ex is InvalidOperationException
                or System.ComponentModel.Win32Exception
                or IOException
                or UnauthorizedAccessException)
        {
            error = ex.Message;
            return false;
        }
    }
}
