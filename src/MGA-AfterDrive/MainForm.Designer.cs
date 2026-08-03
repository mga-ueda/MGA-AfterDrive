using MGA_AfterDrive.Controls;
using MGA_AfterDrive.Theme;

namespace MGA_AfterDrive;

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
        captionBar = new GlassCaptionBar();
        statusBar = new Panel();
        statusLayout = new TableLayoutPanel();
        licenseLink = new LinkLabel();
        settingLink = new LinkLabel();
        statusLabel = new Label();
        trayIcon = new NotifyIcon(components);
        trayMenu = new ContextMenuStrip(components);
        settingMenuItem = new ToolStripMenuItem();
        exitMenuItem = new ToolStripMenuItem();
        statusBar.SuspendLayout();
        statusLayout.SuspendLayout();
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
        // captionBar
        //
        captionBar.Dock = DockStyle.Top;
        captionBar.Height = AppLayout.CaptionBarHeight;
        captionBar.Name = "captionBar";
        captionBar.TabIndex = 2;
        captionBar.CloseRequested += CaptionBar_CloseRequested;
        //
        // licenseLink
        //
        licenseLink.ActiveLinkColor = AppTheme.Foreground;
        licenseLink.AutoSize = true;
        licenseLink.BackColor = Color.Transparent;
        licenseLink.DisabledLinkColor = AppTheme.ForegroundMuted;
        licenseLink.Dock = DockStyle.Fill;
        licenseLink.Font = AppFonts.UI;
        licenseLink.LinkBehavior = LinkBehavior.HoverUnderline;
        licenseLink.LinkColor = AppTheme.Accent;
        licenseLink.Margin = new Padding(0);
        licenseLink.Name = "licenseLink";
        licenseLink.TabIndex = 0;
        licenseLink.TabStop = true;
        licenseLink.Text = "Licenses";
        licenseLink.TextAlign = ContentAlignment.MiddleLeft;
        licenseLink.VisitedLinkColor = AppTheme.Accent;
        licenseLink.LinkClicked += LicenseLink_LinkClicked;
        //
        // settingLink（Licenses の右）
        //
        settingLink.ActiveLinkColor = AppTheme.Foreground;
        settingLink.AutoSize = true;
        settingLink.BackColor = Color.Transparent;
        settingLink.DisabledLinkColor = AppTheme.ForegroundMuted;
        settingLink.Dock = DockStyle.Fill;
        settingLink.Font = AppFonts.UI;
        settingLink.LinkBehavior = LinkBehavior.HoverUnderline;
        settingLink.LinkColor = AppTheme.Accent;
        settingLink.Margin = new Padding(AppLayout.Spacing, 0, 0, 0);
        settingLink.Name = "settingLink";
        settingLink.TabIndex = 1;
        settingLink.TabStop = true;
        settingLink.Text = "Setting";
        settingLink.TextAlign = ContentAlignment.MiddleLeft;
        settingLink.VisitedLinkColor = AppTheme.Accent;
        settingLink.LinkClicked += SettingLink_LinkClicked;
        //
        // statusLabel（カウントダウン等・右寄せ）
        //
        statusLabel.AutoSize = true;
        statusLabel.BackColor = Color.Transparent;
        statusLabel.Dock = DockStyle.Fill;
        statusLabel.Font = AppFonts.UI;
        statusLabel.ForeColor = AppTheme.Foreground;
        statusLabel.Margin = new Padding(AppLayout.Spacing, 0, 0, 0);
        statusLabel.Name = "statusLabel";
        statusLabel.TabIndex = 2;
        statusLabel.TextAlign = ContentAlignment.MiddleRight;
        //
        // statusLayout
        //
        statusLayout.BackColor = Color.Black;
        statusLayout.ColumnCount = 3;
        statusLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        statusLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        statusLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        statusLayout.Controls.Add(licenseLink, 0, 0);
        statusLayout.Controls.Add(settingLink, 1, 0);
        statusLayout.Controls.Add(statusLabel, 2, 0);
        statusLayout.Dock = DockStyle.Fill;
        statusLayout.Name = "statusLayout";
        statusLayout.Padding = new Padding(AppLayout.Spacing, 0, AppLayout.Spacing, 0);
        statusLayout.RowCount = 1;
        statusLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        statusLayout.TabIndex = 0;
        //
        // statusBar
        //
        // 純黒 = DWM ガラスキー。不透明色にすると Acrylic が透けない。
        statusBar.BackColor = Color.Black;
        statusBar.Controls.Add(statusLayout);
        statusBar.Dock = DockStyle.Bottom;
        statusBar.Height = AppLayout.StatusBarHeight;
        statusBar.Name = "statusBar";
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
        trayIcon.Text = "MGA AfterDrive";
        trayIcon.MouseClick += TrayIcon_MouseClick;
        //
        // MainForm
        //
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(720, 267);
        Controls.Add(logEditor);
        Controls.Add(statusBar);
        Controls.Add(captionBar);
        MaximizeBox = false;
        MinimizeBox = false;
        MinimumSize = new Size(560, 214);
        Name = "MainForm";
        StartPosition = FormStartPosition.Manual;
        Text = "MGA AfterDrive - Version 1.0.1";
        statusBar.ResumeLayout(false);
        statusLayout.ResumeLayout(false);
        statusLayout.PerformLayout();
        trayMenu.ResumeLayout(false);
        ResumeLayout(false);
    }

    #endregion

    private FrostedLogView logEditor;
    private GlassCaptionBar captionBar;
    private Panel statusBar;
    private TableLayoutPanel statusLayout;
    private LinkLabel licenseLink;
    private LinkLabel settingLink;
    private Label statusLabel;
    private NotifyIcon trayIcon;
    private ContextMenuStrip trayMenu;
    private ToolStripMenuItem settingMenuItem;
    private ToolStripMenuItem exitMenuItem;
}
