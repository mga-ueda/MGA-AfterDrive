namespace MGA_AfterDrive.IO;

/// <summary>
/// ホスト／Setting の実行ファイル名・プロセス名・CLI 引数の共通定数。
/// </summary>
public static class AppExecutableNames
{
    public const string HostProductName = "MGA AfterDrive";
    public const string HostExeFileName = "MGA-AfterDrive.exe";
    public const string HostProcessName = "MGA-AfterDrive";

    public const string SettingExeFileName = "MGA-AfterDrive.Setting.exe";
    public const string SettingProcessName = "MGA-AfterDrive.Setting";

    public const string HostExeArgumentPrefix = "--host-exe=";

    /// <summary>
    /// タスク スケジューラ上のタスク名（ホスト製品名と同じ）。
    /// </summary>
    public const string TaskSchedulerTaskName = HostProductName;

    public static string FormatHostExeArgument(string hostExePath)
        => $"{HostExeArgumentPrefix}\"{hostExePath}\"";
}
