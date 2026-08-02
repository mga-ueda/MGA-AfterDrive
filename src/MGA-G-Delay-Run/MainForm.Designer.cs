using MGA_G_Delay_Run.Controls;
using MGA_G_Delay_Run.Theme;

namespace MGA_G_Delay_Run;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        logEditor = new FrostedLogView();
        statusBar = new Panel();
        licenseLink = new LinkLabel();
        trayIcon = new NotifyIcon(components);
        trayMenu = new ContextMenuStrip(components);
        settingMenuItem = new ToolStripMenuItem();
        exitMenuItem = new ToolStripMenuItem();
        statusBar.SuspendLayout();
        trayMenu.SuspendLayout();
        SuspendLayout();
        //
        // logEditor
        //
        logEditor.Dock = DockStyle.Fill;
        logEditor.Font = AppFonts.Log;
        logEditor.Name = "logEditor";
        logEditor.TabIndex = 0;
        //
        // licenseLink
        //
        licenseLink.ActiveLinkColor = AppTheme.Foreground;
        licenseLink.AutoSize = true;
        licenseLink.BackColor = Color.Transparent;
        licenseLink.DisabledLinkColor = AppTheme.ForegroundMuted;
        licenseLink.Font = AppFonts.UI;
        licenseLink.LinkBehavior = LinkBehavior.HoverUnderline;
        licenseLink.LinkColor = AppTheme.Accent;
        licenseLink.Location = new Point(AppLayout.Spacing, 6);
        licenseLink.Margin = new Padding(AppLayout.Spacing);
        licenseLink.Name = "licenseLink";
        licenseLink.TabIndex = 0;
        licenseLink.TabStop = true;
        licenseLink.Text = "Licenses";
        licenseLink.VisitedLinkColor = AppTheme.Accent;
        licenseLink.LinkClicked += LicenseLink_LinkClicked;
        //
        // statusBar
        //
        statusBar.BackColor = AppTheme.Surface;
        statusBar.Controls.Add(licenseLink);
        statusBar.Dock = DockStyle.Bottom;
        statusBar.Height = AppLayout.StatusBarHeight;
        statusBar.Name = "statusBar";
        statusBar.Padding = new Padding(AppLayout.Spacing, 0, AppLayout.Spacing, 0);
        statusBar.TabIndex = 1;
        //
        // settingMenuItem
        //
        settingMenuItem.Name = "settingMenuItem";
        settingMenuItem.Text = "Setting (&S)";
        settingMenuItem.Click += SettingMenuItem_Click;
        //
        // exitMenuItem
        //
        exitMenuItem.Name = "exitMenuItem";
        exitMenuItem.Text = "Exit (&X)";
        exitMenuItem.Click += ExitMenuItem_Click;
        //
        // trayMenu
        //
        trayMenu.Items.AddRange(new ToolStripItem[] { settingMenuItem, exitMenuItem });
        trayMenu.Name = "trayMenu";
        //
        // trayIcon
        //
        trayIcon.ContextMenuStrip = trayMenu;
        trayIcon.Text = "MGA G Delay Run";
        trayIcon.MouseClick += TrayIcon_MouseClick;
        //
        // MainForm
        //
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(960, 540);
        Controls.Add(logEditor);
        Controls.Add(statusBar);
        MaximizeBox = false;
        MinimizeBox = false;
        MinimumSize = new Size(640, 400);
        Name = "MainForm";
        StartPosition = FormStartPosition.Manual;
        Text = "MGA G Delay Run - Version 0.0.1";
        statusBar.ResumeLayout(false);
        statusBar.PerformLayout();
        trayMenu.ResumeLayout(false);
        ResumeLayout(false);
    }

    #endregion

    private FrostedLogView logEditor;
    private Panel statusBar;
    private LinkLabel licenseLink;
    private NotifyIcon trayIcon;
    private ContextMenuStrip trayMenu;
    private ToolStripMenuItem settingMenuItem;
    private ToolStripMenuItem exitMenuItem;
}
