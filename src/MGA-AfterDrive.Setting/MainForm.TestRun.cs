using MGA_AfterDrive.Forms;
using MGA_AfterDrive.IO;
using MGA_AfterDrive.Setting.IO;
using MGA_AfterDrive.Setting.Models;

namespace MGA_AfterDrive.Setting;

public partial class MainForm
{
    private async void TestRunMenuItem_Click(object? sender, EventArgs e)
    {
        var skipped = 0;
        foreach (var entry in GetSelectedEntries())
        {
            if (await TryTestRunAsync(entry) == TestRunOutcome.SkippedAlreadyRunning)
            {
                skipped++;
            }
        }

        NotifySkippedAlreadyRunning(skipped);
    }

    private async void StartAllButton_Click(object? sender, EventArgs e)
    {
        entryGrid.EndEdit();

        if (_entries.Count == 0)
        {
            return;
        }

        if (!TryValidateEntries(out var error))
        {
            AppDialogs.Warn(this, AppInfo.ProductName, error);
            return;
        }

        var skipped = 0;
        var tasks = _entries.ToList().Select(TryTestRunAsync).ToArray();
        var outcomes = await Task.WhenAll(tasks);
        foreach (var outcome in outcomes)
        {
            if (outcome == TestRunOutcome.SkippedAlreadyRunning)
            {
                skipped++;
            }
        }

        NotifySkippedAlreadyRunning(skipped);
    }

    private void NotifySkippedAlreadyRunning(int skippedCount)
    {
        if (skippedCount <= 0 || IsDisposed || Disposing)
        {
            return;
        }

        AppDialogs.Info(
            this,
            AppInfo.ProductName,
            skippedCount == 1
                ? "起動済みのためスキップしました。"
                : $"{skippedCount} 件は起動済みのためスキップしました。");
    }

    private async Task WaitWithCountdownAsync(int delaySeconds, string fileName)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(delaySeconds);

        while (!IsDisposed && !Disposing)
        {
            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                break;
            }

            SetTitleStatus($"Test Run {TimeDisplay.FormatCountdown(remaining)} - {fileName}");
            var delay = remaining < TimeSpan.FromMilliseconds(250)
                ? remaining
                : TimeSpan.FromMilliseconds(250);
            await Task.Delay(delay);
        }
    }

    private void SetTitleStatus(string? status)
    {
        if (IsDisposed || Disposing)
        {
            return;
        }

        void Apply()
        {
            Text = string.IsNullOrWhiteSpace(status)
                ? AppInfo.WindowTitle
                : $"{AppInfo.WindowTitle} - {status}";
        }

        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(Apply);
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }

            return;
        }

        Apply();
    }

    private IReadOnlyList<DelayEntry> GetSelectedEntries()
    {
        var rows = entryGrid.SelectedRows.Cast<DataGridViewRow>()
            .Where(row => row.DataBoundItem is DelayEntry)
            .Select(row => (DelayEntry)row.DataBoundItem!)
            .ToList();

        if (rows.Count == 0 && entryGrid.CurrentRow?.DataBoundItem is DelayEntry current)
        {
            rows.Add(current);
        }

        return rows;
    }

    private enum TestRunOutcome
    {
        Started,
        SkippedAlreadyRunning,
        Failed,
        Cancelled,
    }

    private async Task<TestRunOutcome> TryTestRunAsync(DelayEntry entry)
    {
        // 待機中の編集影響を避けるため、開始時点の値を使う
        var delaySeconds = Math.Max(0, entry.Delay);
        var filePath = entry.Path?.Trim() ?? string.Empty;
        var option = entry.Option ?? string.Empty;
        var fileName = string.IsNullOrWhiteSpace(entry.FileName)
            ? Path.GetFileName(filePath)
            : entry.FileName;

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            AppDialogs.Warn(
                this,
                AppInfo.ProductName,
                $"ファイルが見つかりません。{Environment.NewLine}{filePath}");
            return TestRunOutcome.Failed;
        }

        if (!ExecutableFileFilter.IsExecutable(filePath))
        {
            AppDialogs.Warn(
                this,
                AppInfo.ProductName,
                $"実行ファイルではありません。{Environment.NewLine}{filePath}");
            return TestRunOutcome.Failed;
        }

        if (delaySeconds > 0)
        {
            Interlocked.Increment(ref _testRunWaitCount);
            try
            {
                await WaitWithCountdownAsync(delaySeconds, fileName);
            }
            finally
            {
                if (Interlocked.Decrement(ref _testRunWaitCount) <= 0)
                {
                    Interlocked.Exchange(ref _testRunWaitCount, 0);
                    SetTitleStatus(null);
                }
            }
        }

        if (IsDisposed || Disposing)
        {
            return TestRunOutcome.Cancelled;
        }

        if (ProcessExecutable.IsRunning(filePath))
        {
            return TestRunOutcome.SkippedAlreadyRunning;
        }

        if (!ProcessLaunch.TryStart(filePath, option, out var launchError))
        {
            AppDialogs.Error(
                this,
                AppInfo.ProductName,
                $"テスト実行に失敗しました。{Environment.NewLine}{launchError}");
            return TestRunOutcome.Failed;
        }

        return TestRunOutcome.Started;
    }

    private bool TryValidateEntries(out string error)
    {
        for (var i = 0; i < _entries.Count; i++)
        {
            var entry = _entries[i];
            if (entry.Delay < 0)
            {
                error = $"{i + 1} 行目: Delay は 0 以上（秒）で指定してください。";
                return false;
            }

            if (string.IsNullOrWhiteSpace(entry.Path))
            {
                error = $"{i + 1} 行目: Path は必須です。";
                return false;
            }

            if (!ExecutableFileFilter.IsExecutable(entry.Path))
            {
                error = $"{i + 1} 行目: Path が実行ファイルではありません。{Environment.NewLine}{entry.Path}";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }
}
