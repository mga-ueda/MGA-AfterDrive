using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;

namespace MGA_AfterDrive.IO;

/// <summary>
/// 設定アプリ（MGA AfterDrive Setting）を起動する。
/// 開発時は隣の多ファイル構成、公開時は単一 EXE 内の埋め込みを展開して起動する。
/// </summary>
public static class SettingAppLauncher
{
    private const string SettingExeName = "MGA-AfterDrive.Setting.exe";
    private const string SettingProcessName = "MGA-AfterDrive.Setting";
    private const string BundledResourceName = "MGA_AfterDrive.Bundled.Setting.exe";

    /// <summary>
    /// 設定アプリが起動中かどうか。
    /// </summary>
    public static bool IsRunning()
    {
        Process[] processes;
        try
        {
            processes = Process.GetProcessesByName(SettingProcessName);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }

        try
        {
            return processes.Length > 0;
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }

    public static bool TryStart(out string error)
    {
        error = string.Empty;

        var exePath = ResolveExecutablePath(out error);
        if (exePath is null)
        {
            if (string.IsNullOrWhiteSpace(error))
            {
                error = $"{SettingExeName} が見つかりません。";
            }

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

    private static string? ResolveExecutablePath(out string error)
    {
        error = string.Empty;
        var baseDirectory = AppContext.BaseDirectory;

        foreach (var candidate in EnumerateSidecarCandidates(baseDirectory))
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        if (TryExtractBundled(out var extracted, out error))
        {
            return extracted;
        }

        return null;
    }

    private static IEnumerable<string> EnumerateSidecarCandidates(string baseDirectory)
    {
        yield return Path.Combine(baseDirectory, SettingExeName);

        foreach (var configuration in new[] { "Debug", "Release" })
        {
            yield return Path.GetFullPath(Path.Combine(
                baseDirectory,
                "..", "..", "..", "..",
                "MGA-AfterDrive.Setting",
                "bin",
                configuration,
                "net8.0-windows",
                SettingExeName));
        }
    }

    private static bool TryExtractBundled(out string? exePath, out string error)
    {
        exePath = null;
        error = string.Empty;

        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(BundledResourceName);
        if (stream is null)
        {
            return false;
        }

        try
        {
            var directory = AppPaths.GetBundledAppDirectory();
            Directory.CreateDirectory(directory);
            var destination = Path.Combine(directory, SettingExeName);

            if (!NeedsRewrite(destination, stream))
            {
                exePath = destination;
                return true;
            }

            stream.Position = 0;
            var tempPath = destination + ".tmp";
            using (var output = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.CopyTo(output);
            }

            if (File.Exists(destination))
            {
                File.Delete(destination);
            }

            File.Move(tempPath, destination);
            exePath = destination;
            return true;
        }
        catch (Exception ex) when (
            ex is IOException
                or UnauthorizedAccessException
                or NotSupportedException)
        {
            error = $"埋め込み設定アプリの展開に失敗しました: {ex.Message}";
            return false;
        }
    }

    private static bool NeedsRewrite(string destination, Stream bundled)
    {
        if (!File.Exists(destination))
        {
            return true;
        }

        try
        {
            var existingInfo = new FileInfo(destination);
            if (existingInfo.Length != bundled.Length)
            {
                return true;
            }

            bundled.Position = 0;
            var bundledHash = SHA256.HashData(bundled);
            bundled.Position = 0;
            var existingHash = SHA256.HashData(File.ReadAllBytes(destination));
            return !bundledHash.AsSpan().SequenceEqual(existingHash);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return true;
        }
    }
}
