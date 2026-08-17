using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using MgaAfterDrive.IO;

namespace MgaAfterDrive;

public partial class MainWindow
{
    private void ShowLicensesInLog()
    {
        if (OperationPause.IsLicenseViewActive)
        {
            return;
        }

        _logSnapshotBeforeLicense = _logLines.ToArray();
        _logBufferDuringLicense.Clear();
        OperationPause.SetLicenseViewActive(true);
        LicenseLink.Text = "Return";
        SetStatusText($"一時停止中（{OperationPause.DescribeReason()}）");

        try
        {
            var lines = EmbeddedLicenses.LoadCombinedLines();
            _logLines.Clear();
            foreach (var line in lines)
            {
                _logLines.Add(line);
            }

            ScrollLogToStart();
        }
        catch (Exception ex)
        {
            _logLines.Clear();
            _logLines.Add($"[ERROR] 埋め込みライセンスの読み込みに失敗しました: {ex.GetType().Name}: {ex.Message}");
            ScrollLogToStart();
        }
    }

    private void HideLicensesAndResume()
    {
        if (!OperationPause.IsLicenseViewActive)
        {
            return;
        }

        OperationPause.SetLicenseViewActive(false);
        LicenseLink.Text = "Licenses";

        _logLines.Clear();
        if (_logSnapshotBeforeLicense is { Length: > 0 })
        {
            foreach (var line in _logSnapshotBeforeLicense)
            {
                _logLines.Add(line);
            }
        }

        _logSnapshotBeforeLicense = null;

        if (_logBufferDuringLicense.Count > 0)
        {
            foreach (var line in _logBufferDuringLicense)
            {
                _logLines.Add(line);
            }

            _logBufferDuringLicense.Clear();
        }

        SetStatusText(null);
        QueueScrollLogToEnd();
    }

    private void QueueScrollLogToEnd()
    {
        if (Interlocked.Exchange(ref _logScrollQueued, 1) != 0)
        {
            return;
        }

        Dispatcher.BeginInvoke(() =>
        {
            Interlocked.Exchange(ref _logScrollQueued, 0);
            ScrollLogToEnd();
        }, DispatcherPriority.Loaded);
    }

    private void ScrollLogToStart()
    {
        if (_logLines.Count == 0)
        {
            return;
        }

        LogList.UpdateLayout();
        var viewer = FindLogScrollViewer();
        if (viewer is not null)
        {
            viewer.ScrollToHome();
            return;
        }

        LogList.ScrollIntoView(_logLines[0]);
    }

    private void ScrollLogToEnd()
    {
        if (_logLines.Count == 0)
        {
            return;
        }

        LogList.UpdateLayout();
        var viewer = FindLogScrollViewer();
        if (viewer is not null)
        {
            viewer.ScrollToEnd();
            return;
        }

        LogList.ScrollIntoView(_logLines[^1]);
    }

    private ScrollViewer? FindLogScrollViewer()
    {
        return FindDescendant<ScrollViewer>(LogList);
    }

    private static T? FindDescendant<T>(DependencyObject root)
        where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
            {
                return match;
            }

            var nested = FindDescendant<T>(child);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }
}
