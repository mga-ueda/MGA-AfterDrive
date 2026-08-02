namespace MGA_AfterDrive.IO;

/// <summary>
/// Restart 管理対象（切断時の強制終了・復帰時の再起動）の判定。
/// </summary>
public static class DelayEntryRestartPolicy
{
    /// <summary>
    /// 切断時の強制終了・復帰時の再起動の対象か。
    /// Setting で保存された Restart フラグに従う（Drive 上のアプリは追加／Path 変更時に自動 ON）。
    /// </summary>
    public static bool ShouldManage(DelayEntryRecord entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (string.IsNullOrWhiteSpace(entry.Path))
        {
            return false;
        }

        return entry.Restart;
    }
}
