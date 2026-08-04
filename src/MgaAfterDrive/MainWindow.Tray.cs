using System.Windows;
using MediaBrushes = System.Windows.Media.Brushes;
using MgaAfterDrive.IO;
using MgaAfterDrive.Theme;
using MgaAfterDrive.Windows;
using Drawing = System.Drawing;
using WinForms = System.Windows.Forms;

namespace MgaAfterDrive;

public partial class MainWindow
{
    private void InitializeTrayIcon()
    {
        _trayMenu = new WinForms.ContextMenuStrip();
        ApplyTrayMenuTheme(_trayMenu);

        _settingMenuItem = new WinForms.ToolStripMenuItem("Setting (&S)");
        // WinForms メニューのモーダルループ中に直接 Show すると不可視のまま残ることがある
        _settingMenuItem.Click += (_, _) => Dispatcher.BeginInvoke(OpenSetting);
        var exitMenuItem = new WinForms.ToolStripMenuItem("Exit (&X)");
        exitMenuItem.Click += (_, _) => Dispatcher.BeginInvoke(ExitApp);
        _trayMenu.Items.Add(_settingMenuItem);
        _trayMenu.Items.Add(exitMenuItem);

        // Icon(Stream) はストリーム生存に依存するため、Clone してから Stream を閉じる
        using (var iconStream = AppIcons.OpenIconStream())
        using (var loaded = new Drawing.Icon(iconStream))
        {
            _trayIconHandle = (Drawing.Icon)loaded.Clone();
        }

        _trayIcon = new WinForms.NotifyIcon
        {
            Text = AppInfo.ProductName,
            Icon = _trayIconHandle,
            Visible = true,
            ContextMenuStrip = _trayMenu,
        };
        _trayIcon.MouseClick += TrayIcon_MouseClick;
    }

    private static void ApplyTrayMenuTheme(WinForms.ContextMenuStrip menu)
    {
        menu.RenderMode = WinForms.ToolStripRenderMode.Professional;
        menu.BackColor = ToDrawing(AppTheme.Surface);
        menu.ForeColor = ToDrawing(AppTheme.Foreground);
        menu.Renderer = new DarkTrayRenderer();
    }

    private static Drawing.Color ToDrawing(System.Windows.Media.Color c)
        => Drawing.Color.FromArgb(c.A, c.R, c.G, c.B);

#if DEBUG
    private void AddDebugTrayMenuItems()
    {
        if (_trayMenu is null || _settingMenuItem is null)
        {
            return;
        }

        var separator = new WinForms.ToolStripSeparator();
        var disconnectItem = new WinForms.ToolStripMenuItem("切断をシミュレート (&D)");
        var restoreItem = new WinForms.ToolStripMenuItem("復帰をシミュレート (&R)");

        disconnectItem.Click += (_, _) =>
        {
            AppendLog("[DEBUG] Google Drive 切断をシミュレートします。");
            StartHealthMonitor();
            GoogleDriveHealthMonitor.SimulateDisconnect();
        };

        restoreItem.Click += (_, _) =>
        {
            AppendLog("[DEBUG] Google Drive 復帰をシミュレートします。");
            StartHealthMonitor();
            GoogleDriveHealthMonitor.SimulateRestore();
        };

        var settingIndex = _trayMenu.Items.IndexOf(_settingMenuItem);
        var insertAt = settingIndex >= 0 ? settingIndex + 1 : _trayMenu.Items.Count;
        _trayMenu.Items.Insert(insertAt, separator);
        _trayMenu.Items.Insert(insertAt + 1, disconnectItem);
        _trayMenu.Items.Insert(insertAt + 2, restoreItem);
    }
#endif

    private void HideToTray()
    {
        if (!Dispatcher.CheckAccess())
        {
            UiDispatch.BeginInvoke(Dispatcher, HideToTray);
            return;
        }

        if (_trayIcon is not null)
        {
            _trayIcon.Visible = true;
            if (_trayIcon.Icon is null && _trayIconHandle is not null)
            {
                _trayIcon.Icon = _trayIconHandle;
            }
        }

        ShowInTaskbar = false;
        Hide();
        Opacity = 0;
        StartHealthMonitor();
    }

    private void RestoreFromTray()
    {
        ShowInTaskbar = false;
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        if (Left <= OffScreenParkThreshold || Top <= OffScreenParkThreshold)
        {
            CenterOnPrimaryDisplay();
        }

        var targetLeft = Left;
        var targetTop = Top;
        Opacity = 0;
        Left = OffScreenCoordinate;
        Top = OffScreenCoordinate;
        Background = MediaBrushes.Transparent;
        LogList.Background = MediaBrushes.Transparent;

        Show();
        UpdateLayout();

        ApplyAcrylicEffect();
        UpdateLayout();

        Left = targetLeft;
        Top = targetTop;
        EnsureOnScreen();
        MarkRevealed();
        Activate();
        Opacity = 1;
        ApplyAcrylicEffect();
    }

    private void ToggleTrayWindow()
    {
        if (IsVisible && WindowState != WindowState.Minimized)
        {
            HideToTray();
        }
        else
        {
            RestoreFromTray();
        }
    }

    private void TrayIcon_MouseClick(object? sender, WinForms.MouseEventArgs e)
    {
        if (e.Button != WinForms.MouseButtons.Left)
        {
            return;
        }

        Dispatcher.BeginInvoke(ToggleTrayWindow);
    }

    private void ExitApp()
    {
        _allowExit = true;
        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
        }

        Close();
        System.Windows.Application.Current?.Shutdown();
    }

    private sealed class DarkTrayRenderer : WinForms.ToolStripProfessionalRenderer
    {
        public DarkTrayRenderer()
            : base(new DarkTrayColorTable())
        {
        }

        protected override void OnRenderToolStripBorder(WinForms.ToolStripRenderEventArgs e)
        {
        }
    }

    private sealed class DarkTrayColorTable : WinForms.ProfessionalColorTable
    {
        public override Drawing.Color MenuItemSelected => ToDrawing(AppTheme.Selection);
        public override Drawing.Color MenuItemSelectedGradientBegin => ToDrawing(AppTheme.Selection);
        public override Drawing.Color MenuItemSelectedGradientEnd => ToDrawing(AppTheme.Selection);
        public override Drawing.Color MenuItemBorder => ToDrawing(AppTheme.Border);
        public override Drawing.Color ToolStripDropDownBackground => ToDrawing(AppTheme.Surface);
        public override Drawing.Color ImageMarginGradientBegin => ToDrawing(AppTheme.Surface);
        public override Drawing.Color ImageMarginGradientMiddle => ToDrawing(AppTheme.Surface);
        public override Drawing.Color ImageMarginGradientEnd => ToDrawing(AppTheme.Surface);
        public override Drawing.Color SeparatorDark => ToDrawing(AppTheme.Border);
        public override Drawing.Color SeparatorLight => ToDrawing(AppTheme.Border);
    }
}
