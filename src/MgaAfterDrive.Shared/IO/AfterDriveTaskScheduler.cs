using System.Diagnostics;
using System.Text;
using System.Xml.Linq;

namespace MgaAfterDrive.IO;

/// <summary>
/// メインアプリを Windows タスク スケジューラへ登録／削除する。
/// </summary>
public static class AfterDriveTaskScheduler
{
    public const string TaskName = AppExecutableNames.TaskSchedulerTaskName;

    private static readonly XNamespace TaskNs =
        "http://schemas.microsoft.com/windows/2004/02/mit/task";

    public static bool IsRegistered()
    {
        return TryRunSchtasks($"/Query /TN \"{TaskName}\"", out _, out _) == 0;
    }

    public static bool TryRegister(string exePath, out string error)
    {
        error = string.Empty;
        if (!PathUtil.TryNormalize(exePath, out var fullPath) || !File.Exists(fullPath))
        {
            error = "登録する実行ファイルが見つかりません。";
            return false;
        }

        var xmlPath = Path.Combine(
            Path.GetTempPath(),
            $"mga-afterdrive-task-{Guid.NewGuid():N}.xml");

        try
        {
            var document = BuildTaskDocument(fullPath);
            var settings = new System.Xml.XmlWriterSettings
            {
                Encoding = Encoding.Unicode,
                Indent = true,
                OmitXmlDeclaration = false,
            };
            using (var writer = System.Xml.XmlWriter.Create(xmlPath, settings))
            {
                document.Save(writer);
            }

            var exitCode = TryRunSchtasks(
                $"/Create /TN \"{TaskName}\" /XML \"{xmlPath}\" /F",
                out var stdout,
                out var stderr);
            if (exitCode != 0)
            {
                error = FormatSchtasksError("登録", exitCode, stdout, stderr);
                return false;
            }

            return true;
        }
        catch (Exception ex) when (
            ex is IOException
                or UnauthorizedAccessException
                or NotSupportedException
                or InvalidOperationException)
        {
            error = ex.Message;
            return false;
        }
        finally
        {
            try
            {
                if (File.Exists(xmlPath))
                {
                    File.Delete(xmlPath);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    public static bool TryUnregister(out string error)
    {
        error = string.Empty;
        if (!IsRegistered())
        {
            return true;
        }

        var exitCode = TryRunSchtasks(
            $"/Delete /TN \"{TaskName}\" /F",
            out var stdout,
            out var stderr);
        if (exitCode != 0)
        {
            error = FormatSchtasksError("削除", exitCode, stdout, stderr);
            return false;
        }

        return true;
    }

    private static XDocument BuildTaskDocument(string exePath)
    {
        var userId = $"{Environment.UserDomainName}\\{Environment.UserName}";
        var workingDirectory = Path.GetDirectoryName(exePath) ?? string.Empty;

        return new XDocument(
            new XDeclaration("1.0", "UTF-16", null),
            new XElement(
                TaskNs + "Task",
                new XAttribute("version", "1.2"),
                new XElement(
                    TaskNs + "RegistrationInfo",
                    new XElement(TaskNs + "Author", userId),
                    new XElement(TaskNs + "URI", $"\\{TaskName}")),
                new XElement(
                    TaskNs + "Triggers",
                    new XElement(
                        TaskNs + "LogonTrigger",
                        new XElement(TaskNs + "Enabled", "true"),
                        new XElement(TaskNs + "UserId", userId))),
                new XElement(
                    TaskNs + "Principals",
                    new XElement(
                        TaskNs + "Principal",
                        new XAttribute("id", "Author"),
                        new XElement(TaskNs + "UserId", userId),
                        new XElement(TaskNs + "LogonType", "InteractiveToken"),
                        // UI の「最上位の特権で実行する」に対応（XML 値は Highest ではなく HighestAvailable）
                        new XElement(TaskNs + "RunLevel", "HighestAvailable"))),
                new XElement(
                    TaskNs + "Settings",
                    new XElement(TaskNs + "MultipleInstancesPolicy", "IgnoreNew"),
                    new XElement(TaskNs + "DisallowStartIfOnBatteries", "true"),
                    new XElement(TaskNs + "StopIfGoingOnBatteries", "true"),
                    new XElement(TaskNs + "AllowHardTerminate", "true"),
                    new XElement(TaskNs + "StartWhenAvailable", "false"),
                    new XElement(TaskNs + "RunOnlyIfNetworkAvailable", "false"),
                    new XElement(
                        TaskNs + "IdleSettings",
                        new XElement(TaskNs + "StopOnIdleEnd", "true"),
                        new XElement(TaskNs + "RestartOnIdle", "false")),
                    new XElement(TaskNs + "AllowStartOnDemand", "true"),
                    new XElement(TaskNs + "Enabled", "true"),
                    new XElement(TaskNs + "Hidden", "false"),
                    new XElement(TaskNs + "RunOnlyIfIdle", "false"),
                    new XElement(TaskNs + "WakeToRun", "false"),
                    new XElement(TaskNs + "ExecutionTimeLimit", "P3D"),
                    new XElement(TaskNs + "Priority", "7")),
                new XElement(
                    TaskNs + "Actions",
                    new XAttribute("Context", "Author"),
                    new XElement(
                        TaskNs + "Exec",
                        new XElement(TaskNs + "Command", exePath),
                        string.IsNullOrEmpty(workingDirectory)
                            ? null
                            : new XElement(TaskNs + "WorkingDirectory", workingDirectory)))));
    }

    private static int TryRunSchtasks(string arguments, out string stdout, out string stderr)
    {
        stdout = string.Empty;
        stderr = string.Empty;

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    // OEM（CP932 等）は CodePages 未登録だと例外になるため既定エンコーディングを使う
                },
            };

            process.Start();
            stdout = process.StandardOutput.ReadToEnd();
            stderr = process.StandardError.ReadToEnd();
            process.WaitForExit(15_000);
            return process.HasExited ? process.ExitCode : -1;
        }
        catch (Exception ex) when (
            ex is InvalidOperationException
                or System.ComponentModel.Win32Exception
                or IOException)
        {
            stderr = ex.Message;
            return -1;
        }
    }

    private static string FormatSchtasksError(
        string action,
        int exitCode,
        string stdout,
        string stderr)
    {
        var detail = string.Join(
            Environment.NewLine,
            new[] { stderr.Trim(), stdout.Trim() }.Where(static s => s.Length > 0));

        return string.IsNullOrEmpty(detail)
            ? $"タスク スケジューラへの{action}に失敗しました。(exit {exitCode})"
            : $"タスク スケジューラへの{action}に失敗しました。{Environment.NewLine}{detail}";
    }
}
