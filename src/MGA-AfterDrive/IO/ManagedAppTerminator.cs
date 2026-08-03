using System.Diagnostics;

namespace MGA_AfterDrive.IO;

/// <summary>
/// Restart 対象のアプリを強制終了する。
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
            .Where(entry => PathUtil.TryNormalize(entry.Path, out _))
            .GroupBy(entry => PathUtil.Normalize(entry.Path), StringComparer.OrdinalIgnoreCase)
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
        if (!PathUtil.TryNormalize(filePath, out var targetPath))
        {
            log($"[ERROR] 無効なパスです ({label}): {filePath}");
            return;
        }

        var processName = Path.GetFileNameWithoutExtension(targetPath);
        if (string.IsNullOrWhiteSpace(processName))
        {
            log($"[ERROR] プロセス名を取得できません: {label}");
            return;
        }

        if (!ProcessExecutable.TryGetByName(processName, out var processes, out var enumerateError))
        {
            log($"[ERROR] プロセスの列挙に失敗しました ({label}): {enumerateError?.Message}");
            return;
        }

        if (processes.Length == 0)
        {
            ProcessExecutable.DisposeAll(processes);
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

                    if (!ProcessExecutable.MatchesPath(
                            process,
                            targetPath,
                            allowNameFallback: true,
                            out var matchDetail))
                    {
                        log($"スキップ PID {process.Id}（{label}）: {matchDetail}");
                        continue;
                    }

                    KillProcess(process, label, log);
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
                        or AggregateException
                        or System.ComponentModel.Win32Exception
                        or NotSupportedException)
                {
                    log($"[ERROR] 終了に失敗しました ({label}, PID {process.Id}): {ex.Message}");
                }
            }
        }
        finally
        {
            ProcessExecutable.DisposeAll(processes);
        }

        if (killed == 0)
        {
            log($"一致するプロセスを終了できませんでした: {label}");
        }
    }

    /// <summary>
    /// プロセスツリーごと終了を試し、呼び出し元がツリーに含まれる場合などは対象のみ終了する。
    /// （ファイルマネージャ等から本アプリを起動していると tree kill は InvalidOperationException になる）
    /// </summary>
    private static void KillProcess(Process process, string label, Action<string> log)
    {
        try
        {
            process.Kill(entireProcessTree: true);
            return;
        }
        catch (Exception ex) when (ex is InvalidOperationException or AggregateException)
        {
            try
            {
                if (process.HasExited)
                {
                    return;
                }
            }
            catch (InvalidOperationException)
            {
                return;
            }

            log($"プロセスツリー終了をスキップし、対象のみ終了します: {label}（PID {process.Id}）: {ex.Message}");
        }

        process.Kill(entireProcessTree: false);
    }
}
