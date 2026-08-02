namespace MGA_AfterDrive.IO;

/// <summary>
/// 名前付き Mutex による単一インスタンス制御の結果。
/// </summary>
public enum SingleInstanceAcquireResult
{
    Acquired,
    AlreadyRunning,
    Failed,
}

/// <summary>
/// 名前付き Mutex による単一インスタンス制御。
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    private readonly Mutex _mutex;
    private bool _hasHandle;
    private bool _disposed;

    private SingleInstanceGuard(Mutex mutex, bool hasHandle)
    {
        _mutex = mutex;
        _hasHandle = hasHandle;
    }

    /// <summary>
    /// 他インスタンスがいなければ所有権を取得する。
    /// </summary>
    public static SingleInstanceAcquireResult TryAcquire(
        string mutexName,
        out SingleInstanceGuard? guard,
        out string? error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mutexName);

        guard = null;
        error = null;

        Mutex mutex;
        try
        {
            mutex = new Mutex(false, mutexName);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or WaitHandleCannotBeOpenedException or IOException)
        {
            error = ex.Message;
            return SingleInstanceAcquireResult.Failed;
        }

        var hasHandle = false;
        try
        {
            try
            {
                hasHandle = mutex.WaitOne(0, exitContext: false);
            }
            catch (AbandonedMutexException)
            {
                // 前回プロセスが異常終了した場合は所有権を引き継ぐ
                hasHandle = true;
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or WaitHandleCannotBeOpenedException)
        {
            mutex.Dispose();
            error = ex.Message;
            return SingleInstanceAcquireResult.Failed;
        }

        if (!hasHandle)
        {
            mutex.Dispose();
            return SingleInstanceAcquireResult.AlreadyRunning;
        }

        guard = new SingleInstanceGuard(mutex, hasHandle: true);
        return SingleInstanceAcquireResult.Acquired;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_hasHandle)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // 所有していない場合は無視
            }

            _hasHandle = false;
        }

        _mutex.Dispose();
    }
}
