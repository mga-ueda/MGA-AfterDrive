namespace MgaAfterDrive.IO;

/// <summary>
/// 保存された遅延実行エントリ（読み取り用）。
/// </summary>
public sealed class DelayEntryRecord : IRestartableDelayEntry
{
    public int Delay { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public string Option { get; set; } = string.Empty;

    public bool Restart { get; set; }
}
