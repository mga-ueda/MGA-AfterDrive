using System.Diagnostics;

namespace MGA_G_Delay_Run.IO;

/// <summary>
/// Resolves the Google Drive mount, waits for the process, and verifies access.
/// </summary>
public static class GoogleDriveStartupProbe
{
    private const string ProcessName = "GoogleDriveFS";
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan MaxWait = TimeSpan.FromMinutes(3);

    /// <param name="log">Log output (timestamp is added by the caller).</param>
    /// <param name="setTitleStatus">Window title status. Pass null to clear.</param>
    /// <returns>true when Google Drive is resolved and accessible.</returns>
    public static async Task<bool> RunAsync(
        Action<string> log,
        Action<string?> setTitleStatus,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(setTitleStatus);

        var succeeded = false;
        try
        {
            log("Starting Google Drive probe.");

            if (!GoogleDriveLocator.TryGetMountPath(out var mountPath, out var detail))
            {
                log($"[ERROR] Failed to resolve drive letter: {detail}");
                return false;
            }

            log($"Resolved drive letter: {mountPath} ({detail})");

            bool processRunning;
            try
            {
                processRunning = IsProcessRunning();
            }
            catch (Exception ex)
            {
                log($"[ERROR] Failed to query process state: {ex.GetType().Name}: {ex.Message}");
                return false;
            }

            if (processRunning)
            {
                log($"Process {ProcessName} is already running.");
            }
            else
            {
                log($"Waiting for process {ProcessName} (up to {FormatDuration(MaxWait)}).");
                try
                {
                    var started = await WaitForProcessAsync(setTitleStatus, cancellationToken);
                    if (!started)
                    {
                        log($"[ERROR] Timed out waiting for process {ProcessName} ({FormatDuration(MaxWait)}).");
                        return false;
                    }

                    log($"Process {ProcessName} is running.");
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    log($"[ERROR] Failed while waiting for process: {ex.GetType().Name}: {ex.Message}");
                    return false;
                }
            }

            log($"Checking access: {mountPath} (up to {FormatDuration(MaxWait)})");

            bool accessible;
            try
            {
                accessible = await WaitUntilAccessibleAsync(mountPath, setTitleStatus, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                log($"[ERROR] Failed during access check: {ex.GetType().Name}: {ex.Message}");
                return false;
            }

            if (accessible)
            {
                log($"Accessible: {mountPath}");
                succeeded = true;
            }
            else
            {
                log($"[ERROR] Not accessible (timeout {FormatDuration(MaxWait)}): {mountPath}");
            }

            return succeeded;
        }
        finally
        {
            setTitleStatus(null);
            log(succeeded
                ? "Google Drive probe finished successfully."
                : "Google Drive probe finished with errors.");
        }
    }

    private static async Task<bool> WaitForProcessAsync(
        Action<string?> setTitleStatus,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + MaxWait;

        while (!IsProcessRunning())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                return false;
            }

            setTitleStatus($"Waiting for {ProcessName} {FormatCountdown(remaining)}");
            await Task.Delay(PollInterval, cancellationToken);
        }

        return true;
    }

    private static async Task<bool> WaitUntilAccessibleAsync(
        string mountPath,
        Action<string?> setTitleStatus,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + MaxWait;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (TryAccess(mountPath, out _))
            {
                return true;
            }

            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                return false;
            }

            setTitleStatus($"Checking access {FormatCountdown(remaining)}");
            await Task.Delay(PollInterval, cancellationToken);
        }
    }

    private static string FormatCountdown(TimeSpan remaining)
    {
        if (remaining < TimeSpan.Zero)
        {
            remaining = TimeSpan.Zero;
        }

        var totalSeconds = (int)Math.Ceiling(remaining.TotalSeconds);
        var minutes = totalSeconds / 60;
        var seconds = totalSeconds % 60;
        return $"{minutes:00}:{seconds:00}";
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalMinutes >= 1 && duration.Seconds == 0)
        {
            return $"{(int)duration.TotalMinutes} min";
        }

        return $"{duration.TotalSeconds:0} sec";
    }

    private static bool IsProcessRunning()
    {
        Process[] processes;
        try
        {
            processes = Process.GetProcessesByName(ProcessName);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            throw new InvalidOperationException($"Failed to enumerate process {ProcessName}.", ex);
        }

        try
        {
            return processes.Length > 0;
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }

    private static bool TryAccess(string mountPath, out string detail)
    {
        try
        {
            var root = Path.GetPathRoot(mountPath);
            if (!string.IsNullOrWhiteSpace(root) && root.Length >= 2 && root[1] == ':')
            {
                var drive = new DriveInfo(root);
                if (!drive.IsReady)
                {
                    detail = $"Drive {root} is not ready (Type={drive.DriveType}).";
                    return false;
                }
            }

            if (!Directory.Exists(mountPath))
            {
                detail = "Directory does not exist.";
                return false;
            }

            using var enumerator = Directory.EnumerateFileSystemEntries(mountPath).GetEnumerator();
            _ = enumerator.MoveNext();

            detail = "Root enumeration succeeded.";
            return true;
        }
        catch (Exception ex) when (
            ex is IOException
                or UnauthorizedAccessException
                or System.Security.SecurityException
                or DriveNotFoundException
                or DirectoryNotFoundException
                or NotSupportedException
                or ArgumentException)
        {
            detail = $"{ex.GetType().Name}: {ex.Message}";
            return false;
        }
    }
}
