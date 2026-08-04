namespace MgaAfterDrive.IO;

/// <summary>
/// ホスト実行ファイル名・プロセス名・タスク名の共通定数。
/// </summary>
public static class AppExecutableNames
{
    public const string HostProductName = "MGA AfterDrive";
    public const string HostExeFileName = "MGA AfterDrive.exe";
    public const string HostProcessName = "MGA AfterDrive";

    /// <summary>
    /// タスク スケジューラ上のタスク名（ホスト製品名と同じ）。
    /// </summary>
    public const string TaskSchedulerTaskName = HostProductName;
}
