namespace MgaAfterDrive.IO;

/// <summary>
/// 起動シーケンスの一時停止要因（設定ウィンドウ / ライセンス表示）。
/// </summary>
internal static class OperationPause
{
    private static int _licenseViewActive;
    private static int _settingOpen;

    public static bool IsLicenseViewActive => Volatile.Read(ref _licenseViewActive) != 0;

    public static bool IsSettingOpen => Volatile.Read(ref _settingOpen) != 0;

    public static void SetLicenseViewActive(bool active) =>
        Interlocked.Exchange(ref _licenseViewActive, active ? 1 : 0);

    public static void SetSettingOpen(bool open) =>
        Interlocked.Exchange(ref _settingOpen, open ? 1 : 0);

    /// <summary>
    /// カウントダウンや待機を進めてはいけない状態か。
    /// </summary>
    public static bool ShouldPause() =>
        IsLicenseViewActive || IsSettingOpen;

    public static string DescribeReason()
    {
        if (IsLicenseViewActive)
        {
            return "ライセンス表示";
        }

        if (IsSettingOpen)
        {
            return "設定ウィンドウ";
        }

        return "一時停止";
    }
}
