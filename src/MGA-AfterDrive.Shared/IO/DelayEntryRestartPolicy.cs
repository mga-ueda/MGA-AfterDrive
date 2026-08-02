namespace MGA_AfterDrive.IO;

/// <summary>
/// Google Drive 上のアプリを Restart 管理対象とする判定。
/// </summary>
public static class DelayEntryRestartPolicy
{
    /// <summary>
    /// 切断時の強制終了・復帰時の再起動の対象か。
    /// マウントが解決できるときはパス判定を優先し、できないときは保存済み Restart を使う。
    /// </summary>
    public static bool ShouldManage(DelayEntryRecord entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (string.IsNullOrWhiteSpace(entry.Path))
        {
            return false;
        }

        if (GoogleDriveLocator.TryGetMountPath(out var mountPath, out _))
        {
            return GoogleDriveLocator.IsPathUnderMount(entry.Path, mountPath);
        }

        return entry.Restart;
    }
}
