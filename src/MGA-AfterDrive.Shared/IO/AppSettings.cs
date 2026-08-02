namespace MGA_AfterDrive.IO;

/// <summary>
/// アプリ共通設定。
/// </summary>
public sealed class AppSettings
{
    /// <summary>既定の最大待機（秒）。従来の 3 分相当。</summary>
    public const int DefaultMaxWaitSeconds = 180;

    public const int MinMaxWaitSeconds = 1;
    public const int MaxMaxWaitSeconds = 86_400;

    /// <summary>
    /// Google Drive プロセス待機・アクセス確認の最大待ち時間（秒）。
    /// </summary>
    public int MaxWaitSeconds { get; set; } = DefaultMaxWaitSeconds;

    /// <summary>
    /// true のとき、メインアプリをタスクトレイに格納した状態で起動する。既定はオフ。
    /// </summary>
    public bool StartMinimizedToTray { get; set; }

    public static int ClampMaxWaitSeconds(int seconds)
    {
        return Math.Clamp(seconds, MinMaxWaitSeconds, MaxMaxWaitSeconds);
    }

    public TimeSpan GetMaxWait() => TimeSpan.FromSeconds(ClampMaxWaitSeconds(MaxWaitSeconds));
}
