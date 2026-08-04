using System.Windows;
using MediaBrushes = System.Windows.Media.Brushes;
using MgaAfterDrive.IO;
using MgaAfterDrive.Native;
using MgaAfterDrive.Theme;
using MgaAfterDrive.Windows;
using Drawing = System.Drawing;
using WinForms = System.Windows.Forms;

namespace MgaAfterDrive;

public partial class MainWindow
{
    private WinForms.NativeWindow? _trayMenuOwner;

    private void InitializeTrayIcon()
    {
        var trayMenu = new TrayContextMenuStrip();
        ApplyTrayMenuTheme(trayMenu);
        trayMenu.OnSettingShortcut = () => Dispatcher.BeginInvoke(OpenSetting);
        trayMenu.OnExitShortcut = () => Dispatcher.BeginInvoke(ExitApp);
        _trayMenu = trayMenu;

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
            // ContextMenuStrip を直接付けない（自動表示だとキーボードが届かない）
        };
        _trayIcon.MouseClick += TrayIcon_MouseClick;
        _trayIcon.MouseUp += TrayIcon_MouseUp;
    }

    private void TrayIcon_MouseUp(object? sender, WinForms.MouseEventArgs e)
    {
        if (e.Button != WinForms.MouseButtons.Right)
        {
            return;
        }

        // NotifyIcon のコールバックからでも確実に UI スレッドで表示する
        Dispatcher.BeginInvoke(ShowTrayContextMenu);
    }

    private void ShowTrayContextMenu()
    {
        if (_trayMenu is null)
        {
            return;
        }

        EnsureTrayMenuOwner();
        if (_trayMenuOwner is not null)
        {
            // explorer 由来のクリックでもメニューがキー入力を受け取れるようにする
            ForegroundWindow.Activate(_trayMenuOwner.Handle);
        }

        _trayMenu.Show(WinForms.Control.MousePosition);
        _ = _trayMenu.Handle;
        ForegroundWindow.Activate(_trayMenu.Handle);

        if (_trayMenu.Items.Count > 0)
        {
            _trayMenu.Items[0].Select();
        }
    }

    private void EnsureTrayMenuOwner()
    {
        if (_trayMenuOwner is not null)
        {
            return;
        }

        var owner = new WinForms.NativeWindow();
        owner.CreateHandle(new WinForms.CreateParams
        {
            Caption = "MgaAfterDrive.TrayMenuOwner",
            // WS_POPUP — 非表示のオーナーとしてだけ使う
            Style = unchecked((int)0x80000000),
            X = -2000,
            Y = -2000,
            Width = 1,
            Height = 1,
        });
        _trayMenuOwner = owner;
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

        if (_trayMenu is TrayContextMenuStrip trayMenu)
        {
            trayMenu.OnDisconnectShortcut = () => disconnectItem.PerformClick();
            trayMenu.OnRestoreShortcut = () => restoreItem.PerformClick();
        }
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

    /// <summary>
    /// NotifyIcon 向け。ニーモニックが届かない環境でも S/X を ProcessCmdKey で拾う。
    /// </summary>
    private sealed class TrayContextMenuStrip : WinForms.ContextMenuStrip
    {
        public Action? OnSettingShortcut { get; set; }
        public Action? OnExitShortcut { get; set; }
#if DEBUG
        public Action? OnDisconnectShortcut { get; set; }
        public Action? OnRestoreShortcut { get; set; }
#endif

        protected override bool ProcessCmdKey(ref WinForms.Message msg, WinForms.Keys keyData)
        {
            var key = keyData & WinForms.Keys.KeyCode;
            var mods = keyData & WinForms.Keys.Modifiers;
            if (mods == WinForms.Keys.None)
            {
                switch (key)
                {
                    case WinForms.Keys.S:
                        Close();
                        OnSettingShortcut?.Invoke();
                        return true;
                    case WinForms.Keys.X:
                        Close();
                        OnExitShortcut?.Invoke();
                        return true;
#if DEBUG
                    case WinForms.Keys.D:
                        Close();
                        OnDisconnectShortcut?.Invoke();
                        return true;
                    case WinForms.Keys.R:
                        Close();
                        OnRestoreShortcut?.Invoke();
                        return true;
#endif
                }
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }
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
