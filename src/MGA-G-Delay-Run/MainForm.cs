using MGA_G_Delay_Run.Forms;
using MGA_G_Delay_Run.IO;
using MGA_G_Delay_Run.Native;
using MGA_G_Delay_Run.Theme;

namespace MGA_G_Delay_Run;

public partial class MainForm : AppForm
{
    private readonly CancellationTokenSource _lifetimeCts = new();
    private int _probeStarted;
    private int _licenseViewRequested;
    private bool _allowExit;

    public MainForm()
    {
        InitializeComponent();
        Text = AppInfo.WindowTitle;
        ShowInTaskbar = false;
        trayIcon.Text = AppInfo.ProductName;
        trayIcon.Icon = AppIcons.Default;
        trayIcon.Visible = true;
    }

    protected override bool PersistWindowBounds => false;

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        ShowInTaskbar = false;
        trayIcon.Visible = true;
        // ガラスキーは純黒のみ。AppTheme.Apply の灰色を上書きする。
        BackColor = Color.Black;
        logEditor.BackColor = Color.Black;
        logEditor.ForeColor = AppTheme.Foreground;
        logEditor.Font = AppFonts.Log;
        statusBar.BackColor = AppTheme.Surface;
        ApplyTrayMenuTheme();
        statusBar.Resize += (_, _) => CenterLicenseLink();
        CenterLicenseLink();

        if (WindowState == FormWindowState.Maximized)
        {
            WindowState = FormWindowState.Normal;
        }

        if (Interlocked.Exchange(ref _probeStarted, 1) != 0)
        {
            return;
        }

        _ = RunStartupSequenceAsync();
    }

    protected override void OnRevealed()
    {
        base.OnRevealed();

        // WinBlur SetBlurStyle 相当（レイヤード解除済みの Opacity=1 以降）
        BackColor = Color.Black;
        logEditor.BackColor = Color.Black;
        AcrylicBackdrop.Apply(this, AcrylicBackdrop.BlurType.Acrylic);
        logEditor.Invalidate();
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        if (Opacity >= 1.0 && IsHandleCreated)
        {
            AcrylicBackdrop.Apply(this, AcrylicBackdrop.BlurType.Acrylic);
        }
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Color.Black);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_allowExit)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        base.OnFormClosing(e);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            HideToTray();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        try
        {
            trayIcon.Visible = false;

            if (!_lifetimeCts.IsCancellationRequested)
            {
                _lifetimeCts.Cancel();
            }
        }
        catch (ObjectDisposedException)
        {
            // already disposed
        }
        finally
        {
            _lifetimeCts.Dispose();
            base.OnFormClosed(e);
        }
    }

    private void ApplyTrayMenuTheme()
    {
        trayMenu.BackColor = AppTheme.Surface;
        trayMenu.ForeColor = AppTheme.Foreground;
        trayMenu.RenderMode = ToolStripRenderMode.System;

        foreach (ToolStripItem item in trayMenu.Items)
        {
            item.BackColor = AppTheme.Surface;
            item.ForeColor = AppTheme.Foreground;
        }
    }

    private void LicenseLink_LinkClicked(object? sender, LinkLabelLinkClickedEventArgs e)
    {
        ShowLicensesInLog();
    }

    private void ShowLicensesInLog()
    {
        Interlocked.Exchange(ref _licenseViewRequested, 1);

        try
        {
            if (!_lifetimeCts.IsCancellationRequested)
            {
                _lifetimeCts.Cancel();
            }
        }
        catch (ObjectDisposedException)
        {
        }

        SetTitleStatus(null);

        try
        {
            var lines = EmbeddedLicenses.LoadCombinedLines();
            logEditor.Clear();
            logEditor.AppendLines(lines);
        }
        catch (Exception ex)
        {
            logEditor.Clear();
            logEditor.AppendLine($"[ERROR] Failed to load embedded licenses: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void CenterLicenseLink()
    {
        licenseLink.Location = new Point(
            AppLayout.Spacing,
            Math.Max(0, (statusBar.ClientSize.Height - licenseLink.PreferredHeight) / 2));
    }

    private async Task RunStartupSequenceAsync()
    {
        try
        {
            var driveOk = await GoogleDriveStartupProbe
                .RunAsync(AppendLog, SetTitleStatus, _lifetimeCts.Token)
                .ConfigureAwait(true);

            if (IsLicenseViewRequested())
            {
                return;
            }

            if (!driveOk)
            {
                AppendLog("Skipping delayed launch because Google Drive check did not succeed.");
                return;
            }

            var entries = DelayEntriesReader.Load();
            if (entries.Count == 0)
            {
                AppendLog("No launch entries found. Nothing to start.");
                return;
            }

            await DelayedLaunchRunner
                .RunAsync(entries, AppendLog, SetTitleStatus, _lifetimeCts.Token)
                .ConfigureAwait(true);

            if (IsLicenseViewRequested())
            {
                return;
            }

            AppendLog("Startup sequence complete. Minimizing to tray in 2 sec...");
            await Task.Delay(TimeSpan.FromSeconds(2), _lifetimeCts.Token).ConfigureAwait(true);

            if (IsLicenseViewRequested())
            {
                return;
            }

            HideToTray();
        }
        catch (OperationCanceledException)
        {
            if (!IsLicenseViewRequested())
            {
                AppendLog("Startup sequence canceled.");
            }
        }
        catch (Exception ex)
        {
            if (!IsLicenseViewRequested())
            {
                AppendLog($"[ERROR] Unhandled exception in startup sequence: {ex.GetType().Name}: {ex.Message}");
            }
        }
        finally
        {
            if (!IsLicenseViewRequested())
            {
                SetTitleStatus(null);
            }
        }
    }

    private bool IsLicenseViewRequested() => Volatile.Read(ref _licenseViewRequested) != 0;

    private void HideToTray()
    {
        if (IsDisposed || Disposing)
        {
            return;
        }

        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(HideToTray);
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }

            return;
        }

        trayIcon.Visible = true;
        ShowInTaskbar = false;
        Hide();
    }

    private void RestoreFromTray()
    {
        if (IsDisposed || Disposing)
        {
            return;
        }

        ShowInTaskbar = false;
        if (WindowState == FormWindowState.Minimized)
        {
            WindowState = FormWindowState.Normal;
        }

        Show();
        Activate();
        BringToFront();
        AcrylicBackdrop.Apply(this, AcrylicBackdrop.BlurType.Acrylic);
        logEditor.Invalidate();
    }

    private void ToggleTrayWindow()
    {
        if (Visible && WindowState != FormWindowState.Minimized)
        {
            HideToTray();
        }
        else
        {
            RestoreFromTray();
        }
    }

    private void TrayIcon_MouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        ToggleTrayWindow();
    }

    private void SettingMenuItem_Click(object? sender, EventArgs e)
    {
        if (!SettingAppLauncher.TryStart(out var error))
        {
            MessageBox.Show(
                error,
                AppInfo.ProductName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void ExitMenuItem_Click(object? sender, EventArgs e)
    {
        _allowExit = true;
        trayIcon.Visible = false;
        Close();
    }

    private void SetTitleStatus(string? status)
    {
        if (IsDisposed || Disposing)
        {
            return;
        }

        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(() => SetTitleStatus(status));
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }

            return;
        }

        try
        {
            Text = string.IsNullOrWhiteSpace(status)
                ? AppInfo.WindowTitle
                : $"{AppInfo.WindowTitle} - {status}";
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    public void AppendLog(string message)
    {
        if (IsDisposed || Disposing || IsLicenseViewRequested())
        {
            return;
        }

        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(() => AppendLog(message));
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }

            return;
        }

        try
        {
            if (logEditor.IsDisposed || IsLicenseViewRequested())
            {
                return;
            }

            logEditor.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}");
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }
}
