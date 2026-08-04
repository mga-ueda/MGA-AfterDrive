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
        ScrollLogToEnd();
    }

    private void ScrollLogToStart()
    {
        if (_logLines.Count > 0)
        {
            LogList.ScrollIntoView(_logLines[0]);
        }
    }

    private void ScrollLogToEnd()
    {
        if (_logLines.Count > 0)
        {
            LogList.ScrollIntoView(_logLines[^1]);
        }
    }
}
