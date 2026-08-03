using System.Diagnostics;
using System.Security.Cryptography;

namespace MGA_AfterDrive.IO;

/// <summary>
/// 設定アプリ（MGA AfterDrive Setting）を起動する。
/// 開発時は隣の多ファイル構成、公開時は単一 EXE 内の埋め込みを展開して起動する。
/// </summary>
public static class SettingAppLauncher
{
    private const string BundledResourceName = "MGA_AfterDrive.Bundled.Setting.exe";

    /// <summary>
    /// 設定アプリが起動中かどうか。
    /// </summary>
    public static bool IsRunning()
        => ProcessExecutable.AnyByName(AppExecutableNames.SettingProcessName);

    public static bool TryStart(out string error)
    {
        error = string.Empty;

        var exePath = ResolveExecutablePath(out error);
        if (exePath is null)
        {
            if (string.IsNullOrWhiteSpace(error))
            {
                error = $"{AppExecutableNames.SettingExeFileName} が見つかりません。";
            }

            return false;
        }

        try
        {
            var hostExe = Environment.ProcessPath;
            var arguments = string.IsNullOrWhiteSpace(hostExe)
                ? string.Empty
                : AppExecutableNames.FormatHostExeArgument(hostExe);

            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = arguments,
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(exePath) ?? Environment.CurrentDirectory,
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                error = $"{AppExecutableNames.SettingExeFileName} を起動できませんでした。";
                return false;
            }

            if (process.WaitForExit(300) && process.ExitCode != 0)
            {
                error = $"{AppExecutableNames.SettingExeFileName} の起動に失敗しました。(exit {process.ExitCode})";
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
        yield return Path.Combine(baseDirectory, AppExecutableNames.SettingExeFileName);
        yield return Path.Combine(AppPaths.GetBundledAppDirectory(), AppExecutableNames.SettingExeFileName);

        foreach (var configuration in new[] { "Debug", "Release" })
        {
            yield return Path.GetFullPath(Path.Combine(
                baseDirectory,
                "..", "..", "..", "..",
                "MGA-AfterDrive.Setting",
                "bin",
                configuration,
                "net8.0-windows",
                AppExecutableNames.SettingExeFileName));
        }
    }

    private static bool TryExtractBundled(out string? exePath, out string error)
    {
        exePath = null;
        error = string.Empty;

        var assembly = typeof(SettingAppLauncher).Assembly;
        using var stream = assembly.GetManifestResourceStream(BundledResourceName);
        if (stream is null)
        {
            error =
                $"{AppExecutableNames.SettingExeFileName} の埋め込みリソース（{BundledResourceName}）が見つかりません。"
                + " 単一 EXE 公開のビルドが不完全な可能性があります。";
            return false;
        }

        try
        {
            var directory = AppPaths.GetBundledAppDirectory();
            Directory.CreateDirectory(directory);
            var destination = Path.Combine(directory, AppExecutableNames.SettingExeFileName);

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
