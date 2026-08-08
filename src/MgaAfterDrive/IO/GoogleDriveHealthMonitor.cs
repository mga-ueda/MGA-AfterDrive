namespace MgaAfterDrive.IO;

/// <summary>
/// Google Drive の死活を定期監視する。
/// 状態が変化（接続→切断／切断→復活）したときだけログと通知を行う。
/// </summary>
public static class GoogleDriveHealthMonitor
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(15);

#if DEBUG
    private static readonly object SimulationSync = new();
    private static bool _simulatedOffline;
    private static TaskCompletionSource _pulse =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// DEBUG 限定: Google Drive 切断をシミュレートする。
    /// </summary>
    public static void SimulateDisconnect()
    {
        lock (SimulationSync)
        {
            _simulatedOffline = true;
        }

        Pulse();
    }

    /// <summary>
    /// DEBUG 限定: Google Drive 復帰をシミュレートする。
    /// </summary>
    public static void SimulateRestore()
    {
        lock (SimulationSync)
        {
            _simulatedOffline = false;
        }

        Pulse();
    }

    private static void Pulse()
    {
        var previous = Interlocked.Exchange(
            ref _pulse,
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
        previous.TrySetResult();
    }
#endif

    // 予期せぬ再開始後も直前の死活状態を引き継ぎ、復帰検知を取りこぼさない
    private static bool? _lastHealthy;

    /// <summary>
    /// 起動プローブ失敗時など、未接続として監視を開始する。
    /// 初回ポーリングが正常でも切断→復帰として通知されるようにする。
    /// </summary>
    public static void SeedAsDisconnected()
    {
        _lastHealthy = false;
    }

    /// <param name="log">ログ出力（タイムスタンプは呼び出し側が付与）。</param>
    /// <param name="onStateChanged">状態遷移時の通知。引数は (接続中か, 詳細)。</param>
    public static async Task RunAsync(
        Action<string> log,
        Action<bool, string> onStateChanged,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(onStateChanged);

        log($"Google Drive の死活監視を開始しました（間隔 {PollInterval.TotalSeconds:0} 秒）。");

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var healthy = Check(out var detail);
                var lastHealthy = _lastHealthy;

                if (lastHealthy is null)
                {
                    // 初回は切断状態のときだけ通知する
                    log(healthy
                        ? $"Google Drive は正常です: {detail}"
                        : $"[WARN] Google Drive を利用できません: {detail}");
                    if (!healthy)
                    {
                        onStateChanged(false, detail);
                    }
                }
                else if (healthy != lastHealthy)
                {
                    log(healthy
                        ? $"Google Drive の接続が復帰しました: {detail}"
                        : $"[WARN] Google Drive の接続が切れました: {detail}");
                    onStateChanged(healthy, detail);
                }

                _lastHealthy = healthy;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // 1 回のポーリング失敗で監視全体を止めない
                log($"[ERROR] 死活監視のポーリングでエラー: {ex.GetType().Name}: {ex.Message}");
            }

#if DEBUG
            await DelayOrPulseAsync(cancellationToken);
#else
            await Task.Delay(PollInterval, cancellationToken);
#endif
        }
    }

#if DEBUG
    private static async Task DelayOrPulseAsync(CancellationToken cancellationToken)
    {
        var pulseTask = _pulse.Task;
        var delayTask = Task.Delay(PollInterval, cancellationToken);
        // UI 同期コンテキストを維持し、通知・復旧処理を UI スレッドで行う
        var completed = await Task.WhenAny(pulseTask, delayTask).ConfigureAwait(true);
        if (completed == delayTask)
        {
            await delayTask.ConfigureAwait(true);
        }
    }
#endif

    private static bool Check(out string detail)
    {
#if DEBUG
        lock (SimulationSync)
        {
            if (_simulatedOffline)
            {
                detail = "切断のシミュレーション（DEBUG）";
                return false;
            }
        }
#endif

        if (!GoogleDriveLocator.TryGetMountPath(out var mountPath, out detail))
        {
            return false;
        }

        try
        {
            if (!GoogleDriveStartupProbe.IsProcessRunning())
            {
                detail = "プロセス GoogleDriveFS が起動していません。";
                return false;
            }
        }
        catch (InvalidOperationException ex)
        {
            detail = ex.Message;
            return false;
        }

        if (!GoogleDriveStartupProbe.TryAccess(mountPath, out var accessDetail))
        {
            detail = $"{mountPath}: {accessDetail}";
            return false;
        }

        detail = mountPath;
        return true;
    }
}
