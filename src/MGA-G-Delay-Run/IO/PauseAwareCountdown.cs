namespace MGA_G_Delay_Run.IO;

/// <summary>
/// 設定ウィンドウ表示中はカウントダウンを進めない待機ユーティリティ。
/// </summary>
internal static class PauseAwareCountdown
{
    private static readonly TimeSpan PausePollInterval = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// <paramref name="duration"/> だけ待つ。設定アプリ起動中は残り時間を減らさない。
    /// </summary>
    /// <param name="maxSlice">1 回の待機上限（カウントダウン更新間隔）。</param>
    public static async Task WaitAsync(
        TimeSpan duration,
        TimeSpan maxSlice,
        Func<TimeSpan, string> statusWhileRunning,
        Func<string> statusWhilePaused,
        Action<string?> setTitleStatus,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(statusWhileRunning);
        ArgumentNullException.ThrowIfNull(statusWhilePaused);
        ArgumentNullException.ThrowIfNull(setTitleStatus);

        if (duration <= TimeSpan.Zero)
        {
            return;
        }

        if (maxSlice <= TimeSpan.Zero)
        {
            maxSlice = TimeSpan.FromSeconds(1);
        }

        var remaining = duration;
        var wasPaused = false;

        while (remaining > TimeSpan.Zero)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (SettingAppLauncher.IsRunning())
            {
                if (!wasPaused)
                {
                    wasPaused = true;
                }

                setTitleStatus(statusWhilePaused());
                await Task.Delay(PausePollInterval, cancellationToken);
                continue;
            }

            if (wasPaused)
            {
                wasPaused = false;
            }

            setTitleStatus(statusWhileRunning(remaining));
            var slice = remaining < maxSlice ? remaining : maxSlice;
            await Task.Delay(slice, cancellationToken);
            remaining -= slice;
        }
    }
}
