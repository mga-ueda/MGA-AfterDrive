namespace MGA_AfterDrive.IO;

/// <summary>
/// 設定ウィンドウやライセンス表示中はカウントダウンを進めない待機ユーティリティ。
/// </summary>
internal static class PauseAwareCountdown
{
    private static readonly TimeSpan PausePollInterval = TimeSpan.FromMilliseconds(200);

    internal readonly record struct WaitResult(TimeSpan Elapsed, bool InterruptedForReload);

    /// <summary>
    /// <paramref name="duration"/> だけ待つ。<see cref="OperationPause"/> 中は残り時間を減らさない。
    /// </summary>
    /// <param name="maxSlice">1 回の待機上限（カウントダウン更新間隔）。</param>
    public static async Task WaitAsync(
        TimeSpan duration,
        TimeSpan maxSlice,
        Func<TimeSpan, string> statusWhileRunning,
        Func<string> statusWhilePaused,
        Action<string?> setStatusText,
        CancellationToken cancellationToken)
    {
        _ = await WaitCoreAsync(
                duration,
                maxSlice,
                statusWhileRunning,
                statusWhilePaused,
                setStatusText,
                detectSettingEntryChanges: false,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Setting 終了後に delay-entries が変わっていたら待機を中断し、呼び出し側でタイマー再設定できるようにする。
    /// </summary>
    public static Task<WaitResult> WaitAsync(
        TimeSpan duration,
        TimeSpan maxSlice,
        Func<TimeSpan, string> statusWhileRunning,
        Func<string> statusWhilePaused,
        Action<string?> setStatusText,
        bool detectSettingEntryChanges,
        CancellationToken cancellationToken)
    {
        return WaitCoreAsync(
            duration,
            maxSlice,
            statusWhileRunning,
            statusWhilePaused,
            setStatusText,
            detectSettingEntryChanges,
            cancellationToken);
    }

    private static async Task<WaitResult> WaitCoreAsync(
        TimeSpan duration,
        TimeSpan maxSlice,
        Func<TimeSpan, string> statusWhileRunning,
        Func<string> statusWhilePaused,
        Action<string?> setStatusText,
        bool detectSettingEntryChanges,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(statusWhileRunning);
        ArgumentNullException.ThrowIfNull(statusWhilePaused);
        ArgumentNullException.ThrowIfNull(setStatusText);

        if (duration <= TimeSpan.Zero)
        {
            return new WaitResult(TimeSpan.Zero, InterruptedForReload: false);
        }

        if (maxSlice <= TimeSpan.Zero)
        {
            maxSlice = TimeSpan.FromSeconds(1);
        }

        var remaining = duration;
        var elapsed = TimeSpan.Zero;
        var wasPaused = false;
        string? entriesFingerprintAtSettingPause = null;

        while (remaining > TimeSpan.Zero)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (OperationPause.ShouldPause())
            {
                if (!wasPaused)
                {
                    wasPaused = true;
                    if (detectSettingEntryChanges && SettingAppLauncher.IsRunning())
                    {
                        entriesFingerprintAtSettingPause = DelayEntriesFingerprint.Capture();
                    }
                }

                setStatusText(statusWhilePaused());
                await Task.Delay(PausePollInterval, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (wasPaused)
            {
                wasPaused = false;
                if (detectSettingEntryChanges
                    && entriesFingerprintAtSettingPause is not null
                    && !DelayEntriesFingerprint.Equals(
                        entriesFingerprintAtSettingPause,
                        DelayEntriesFingerprint.Capture()))
                {
                    return new WaitResult(elapsed, InterruptedForReload: true);
                }

                entriesFingerprintAtSettingPause = null;
            }

            setStatusText(statusWhileRunning(remaining));

            // 長い Delay のあいだにライセンス／Setting が開閉されてもすぐ検知できるよう短く刻む
            var slice = remaining < maxSlice ? remaining : maxSlice;
            if (slice > PausePollInterval)
            {
                slice = PausePollInterval;
            }

            await Task.Delay(slice, cancellationToken).ConfigureAwait(false);

            // Delay 中に一時停止へ入った場合は経過させない（再開後に再表示する）
            if (OperationPause.ShouldPause())
            {
                wasPaused = true;
                if (detectSettingEntryChanges && SettingAppLauncher.IsRunning())
                {
                    entriesFingerprintAtSettingPause ??= DelayEntriesFingerprint.Capture();
                }

                continue;
            }

            remaining -= slice;
            elapsed += slice;
        }

        return new WaitResult(elapsed, InterruptedForReload: false);
    }
}
