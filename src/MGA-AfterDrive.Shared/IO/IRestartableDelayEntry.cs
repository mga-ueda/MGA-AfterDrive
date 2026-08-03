namespace MGA_AfterDrive.IO;

/// <summary>
/// Restart マイグレーション対象のエントリ。
/// </summary>
public interface IRestartableDelayEntry
{
    string Path { get; }

    bool Restart { get; set; }
}
