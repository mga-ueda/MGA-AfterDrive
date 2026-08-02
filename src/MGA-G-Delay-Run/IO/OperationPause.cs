namespace MGA_G_Delay_Run.IO;

/// <summary>
/// 起動シーケンスの一時停止要因（設定アプリ / ライセンス表示）。
/// </summary>
internal static class OperationPause
{
    private static int _licenseViewActive;

    public static bool IsLicenseViewActive => Volatile.Read(ref _licenseViewActive) != 0;

    public static void SetLicenseViewActive(bool active) =>
        Interlocked.Exchange(ref _licenseViewActive, active ? 1 : 0);

    /// <summary>
    /// カウントダウンや待機を進めてはいけない状態か。
    /// </summary>
    public static bool ShouldPause() =>
        IsLicenseViewActive || SettingAppLauncher.IsRunning();

    public static string DescribeReason()
    {
        if (IsLicenseViewActive)
        {
            return "ライセンス表示";
        }

        if (SettingAppLauncher.IsRunning())
        {
            return "設定ウィンドウ";
        }

        return "一時停止";
    }
}
