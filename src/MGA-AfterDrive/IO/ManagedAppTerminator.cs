using System.Diagnostics;

namespace MGA_AfterDrive.IO;

/// <summary>
/// Google Drive 上（Restart 対象）のアプリを強制終了する。
/// </summary>
public static class ManagedAppTerminator
{
    private static readonly TimeSpan ExitWait = TimeSpan.FromSeconds(5);

    public static void KillRestartEntries(
        IReadOnlyList<DelayEntryRecord> entries,
        Action<string> log)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(log);

        var targets = entries
            .Where(DelayEntryRestartPolicy.ShouldManage)
            .GroupBy(entry => NormalizePath(entry.Path), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();

        if (targets.Length == 0)
        {
            log("強制終了対象の Google Drive 上アプリはありません。");
            return;
        }

        log($"Google Drive 上のアプリを強制終了します（{targets.Length} 件）。");

        foreach (var entry in targets)
        {
            var label = string.IsNullOrWhiteSpace(entry.FileName)
                ? Path.GetFileName(entry.Path)
                : entry.FileName;
            KillProcessesForPath(entry.Path, label, log);
        }
    }

    private static void KillProcessesForPath(string filePath, string label, Action<string> log)
    {
        var trimmed = filePath.Trim();
        string targetPath;
        try
        {
            targetPath = NormalizePath(trimmed);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            log($"[ERROR] 無効なパスです ({label}): {ex.Message}");
            return;
        }

        var processName = Path.GetFileNameWithoutExtension(targetPath);
        if (string.IsNullOrWhiteSpace(processName))
        {
            log($"[ERROR] プロセス名を取得できません: {label}");
            return;
        }

        Process[] processes;
        try
        {
            processes = Process.GetProcessesByName(processName);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            log($"[ERROR] プロセスの列挙に失敗しました ({label}): {ex.Message}");
            return;
        }

        if (processes.Length == 0)
        {
            log($"起動していません: {label}");
            return;
        }

        var killed = 0;
        try
        {
            foreach (var process in processes)
            {
                try
                {
                    if (process.Id == Environment.ProcessId)
                    {
                        continue;
                    }

                    if (!IsSameExecutable(process, targetPath, out var matchDetail))
                    {
                        log($"スキップ PID {process.Id}（{label}）: {matchDetail}");
                        continue;
                    }

                    process.Kill(entireProcessTree: true);
                    if (!process.WaitForExit((int)ExitWait.TotalMilliseconds))
                    {
                        log($"[WARN] 終了待機がタイムアウトしました: {label}（PID {process.Id}）");
                    }
                    else
                    {
                        log($"終了しました: {label}（PID {process.Id}）");
                    }

                    killed++;
                }
                catch (Exception ex) when (
                    ex is InvalidOperationException
                        or System.ComponentModel.Win32Exception
                        or NotSupportedException)
                {
                    log($"[ERROR] 終了に失敗しました ({label}, PID {process.Id}): {ex.Message}");
                }
            }
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }

        if (killed == 0)
        {
            log($"一致するプロセスを終了できませんでした: {label}");
        }
    }

    private static bool IsSameExecutable(Process process, string targetPath, out string detail)
    {
        try
        {
            var modulePath = process.MainModule?.FileName;
            if (string.IsNullOrWhiteSpace(modulePath))
            {
                detail = "MainModule のパスが空です。";
                return false;
            }

            if (!string.Equals(NormalizePath(modulePath), targetPath, StringComparison.OrdinalIgnoreCase))
            {
                detail = $"パス不一致（{modulePath}）";
                return false;
            }

            detail = "パス一致";
            return true;
        }
        catch (Exception ex) when (
            ex is InvalidOperationException
                or System.ComponentModel.Win32Exception
                or NotSupportedException)
        {
            // 昇格プロセス等で MainModule が読めない場合はプロセス名一致で終了対象とする
            detail = $"MainModule を取得できません（{ex.GetType().Name}）。プロセス名で照合します";
            return true;
        }
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path.Trim());
    }
}
