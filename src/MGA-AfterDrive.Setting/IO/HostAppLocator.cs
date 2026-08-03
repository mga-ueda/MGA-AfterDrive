using System.Diagnostics;
using MGA_AfterDrive.IO;

namespace MGA_AfterDrive.Setting.IO;

/// <summary>
/// タスク登録対象となるメインアプリ（MGA-AfterDrive.exe）のパスを解決する。
/// </summary>
public static class HostAppLocator
{
    private static string? _fromArguments;

    public static void Initialize(string[] args)
    {
        foreach (var arg in args)
        {
            if (!arg.StartsWith(AppExecutableNames.HostExeArgumentPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = arg[AppExecutableNames.HostExeArgumentPrefix.Length..].Trim().Trim('"');
            if (!string.IsNullOrWhiteSpace(value))
            {
                _fromArguments = value;
            }

            return;
        }
    }

    public static bool TryResolve(out string exePath, out string error)
    {
        exePath = string.Empty;
        error = string.Empty;

        if (TryUseExistingPath(_fromArguments, out exePath))
        {
            return true;
        }

        var sibling = Path.Combine(AppContext.BaseDirectory, AppExecutableNames.HostExeFileName);
        if (TryUseExistingPath(sibling, out exePath))
        {
            return true;
        }

        if (TryResolveFromRunningProcess(out exePath))
        {
            return true;
        }

        error = $"{AppExecutableNames.HostExeFileName} の場所を特定できませんでした。メインアプリから Setting を開いてください。";
        return false;
    }

    private static bool TryUseExistingPath(string? path, out string exePath)
    {
        exePath = string.Empty;
        if (!PathUtil.TryNormalize(path, out var fullPath) || !File.Exists(fullPath))
        {
            return false;
        }

        exePath = fullPath;
        return true;
    }

    private static bool TryResolveFromRunningProcess(out string exePath)
    {
        exePath = string.Empty;
        string? found = null;

        if (!ProcessExecutable.TryForEachByName(
                AppExecutableNames.HostProcessName,
                process =>
                {
                    if (found is not null)
                    {
                        return;
                    }

                    try
                    {
                        var modulePath = process.MainModule?.FileName;
                        if (TryUseExistingPath(modulePath, out var resolved))
                        {
                            found = resolved;
                        }
                    }
                    catch (Exception ex) when (
                        ex is InvalidOperationException
                            or System.ComponentModel.Win32Exception
                            or NotSupportedException)
                    {
                    }
                },
                out _))
        {
            return false;
        }

        if (found is null)
        {
            return false;
        }

        exePath = found;
        return true;
    }
}
