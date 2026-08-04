using System.Diagnostics;

namespace MgaAfterDrive.IO;

/// <summary>
/// 実行ファイルパスと実行中プロセスの照合・列挙。
/// </summary>
public static class ProcessExecutable
{
    /// <summary>
    /// 指定パスの実行ファイルが既に起動しているか。
    /// このアプリから起動したかどうかに依存しない。
    /// MainModule が取れないプロセスは「起動済み」とみなさない（誤スキップ防止）。
    /// </summary>
    public static bool IsRunning(string? filePath)
    {
        if (!PathUtil.TryNormalize(filePath, out var targetPath))
        {
            return false;
        }

        var processName = Path.GetFileNameWithoutExtension(targetPath);
        if (string.IsNullOrWhiteSpace(processName))
        {
            return false;
        }

        if (!TryGetByName(processName, out var processes, out _))
        {
            return false;
        }

        try
        {
            foreach (var process in processes)
            {
                if (process.Id == Environment.ProcessId)
                {
                    continue;
                }

                if (MatchesPath(process, targetPath, allowNameFallback: false, out _))
                {
                    return true;
                }
            }

            return false;
        }
        finally
        {
            DisposeAll(processes);
        }
    }

    /// <summary>
    /// プロセス名で列挙し、1 つ以上あるかを返す。
    /// 列挙自体に失敗したときは false を返し <paramref name="error"/> に例外を入れる。
    /// </summary>
    public static bool TryAnyByName(string processName, out bool any, out Exception? error)
    {
        any = false;
        if (!TryGetByName(processName, out var processes, out error))
        {
            return false;
        }

        try
        {
            any = processes.Length > 0;
            return true;
        }
        finally
        {
            DisposeAll(processes);
        }
    }

    /// <summary>
    /// プロセス名で列挙する。成功時は呼び出し側が <see cref="DisposeAll"/> すること。
    /// </summary>
    public static bool TryGetByName(string processName, out Process[] processes, out Exception? error)
    {
        processes = [];
        error = null;

        if (string.IsNullOrWhiteSpace(processName))
        {
            return true;
        }

        try
        {
            processes = Process.GetProcessesByName(processName);
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            error = ex;
            processes = [];
            return false;
        }
    }

    public static void DisposeAll(Process[] processes)
    {
        foreach (var process in processes)
        {
            process.Dispose();
        }
    }

    /// <summary>
    /// プロセスが対象実行ファイルと一致するか。
    /// </summary>
    /// <param name="allowNameFallback">
    /// true のとき、MainModule が読めない場合はプロセス名一致として扱う（強制終了向け）。
    /// false のときはパス確認できたときだけ一致とする（起動スキップ向け）。
    /// </param>
    public static bool MatchesPath(
        Process process,
        string targetPath,
        bool allowNameFallback,
        out string detail)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);

        try
        {
            var modulePath = process.MainModule?.FileName;
            if (string.IsNullOrWhiteSpace(modulePath))
            {
                detail = "MainModule のパスが空です。";
                return false;
            }

            if (!PathUtil.TryNormalize(modulePath, out var normalizedModule)
                || !string.Equals(normalizedModule, targetPath, StringComparison.OrdinalIgnoreCase))
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
            detail = $"MainModule を取得できません（{ex.GetType().Name}）";
            if (allowNameFallback)
            {
                detail += "。プロセス名で照合します";
                return true;
            }

            return false;
        }
    }
}
