using System.Diagnostics;

namespace MGA_G_Delay_Run.IO;

/// <summary>
/// 設定アプリ（MGA G Delay Run Setting）を起動する。
/// </summary>
public static class SettingAppLauncher
{
    private const string SettingExeName = "MGA-G-Delay-Run.Setting.exe";
    private const string SettingDllName = "MGA-G-Delay-Run.Setting.dll";

    public static bool TryStart(out string error)
    {
        error = string.Empty;

        var exePath = ResolveExecutablePath();
        if (exePath is null)
        {
            error = $"{SettingExeName} が見つかりません。";
            return false;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(exePath) ?? Environment.CurrentDirectory,
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                error = $"{SettingExeName} を起動できませんでした。";
                return false;
            }

            // 依存 DLL 欠落などで即終了した場合を検出
            if (process.WaitForExit(300) && process.ExitCode != 0)
            {
                error = $"{SettingExeName} の起動に失敗しました。(exit {process.ExitCode})";
                return false;
            }

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

    private static string? ResolveExecutablePath()
    {
        var baseDirectory = AppContext.BaseDirectory;

        foreach (var candidate in EnumerateCandidates(baseDirectory))
        {
            if (IsRunnableSetting(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// apphost EXE だけでなく、同梱の managed DLL があることまで確認する。
    /// </summary>
    private static bool IsRunnableSetting(string exePath)
    {
        if (!File.Exists(exePath))
        {
            return false;
        }

        var directory = Path.GetDirectoryName(exePath);
        if (string.IsNullOrEmpty(directory))
        {
            return false;
        }

        return File.Exists(Path.Combine(directory, SettingDllName));
    }

    private static IEnumerable<string> EnumerateCandidates(string baseDirectory)
    {
        // 配布時 / ビルド出力コピー先: メイン EXE と同じフォルダ
        yield return Path.Combine(baseDirectory, SettingExeName);

        // 開発時: src/MGA-G-Delay-Run/bin/{Config}/net8.0-windows → src/ へ 4 階層上がる
        foreach (var configuration in new[] { "Debug", "Release" })
        {
            yield return Path.GetFullPath(Path.Combine(
                baseDirectory,
                "..", "..", "..", "..",
                "MGA-G-Delay-Run.Setting",
                "bin",
                configuration,
                "net8.0-windows",
                SettingExeName));
        }
    }
}
