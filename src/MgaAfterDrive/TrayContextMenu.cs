using MgaAfterDrive.Theme;
using Drawing = System.Drawing;
using WinForms = System.Windows.Forms;

namespace MgaAfterDrive;

/// <summary>
/// NotifyIcon 向けダーク ContextMenuStrip。ニーモニックが届かない環境でも
/// ProcessCmdKey で項目の Click を発火する。
/// </summary>
internal sealed class TrayContextMenuStrip : WinForms.ContextMenuStrip
{
    public WinForms.ToolStripMenuItem? SettingItem { get; set; }
    public WinForms.ToolStripMenuItem? ExitItem { get; set; }
#if DEBUG
    public WinForms.ToolStripMenuItem? DisconnectItem { get; set; }
    public WinForms.ToolStripMenuItem? RestoreItem { get; set; }
#endif

    protected override bool ProcessCmdKey(ref WinForms.Message msg, WinForms.Keys keyData)
    {
        var key = keyData & WinForms.Keys.KeyCode;
        var mods = keyData & WinForms.Keys.Modifiers;
        if (mods == WinForms.Keys.None)
        {
            var item = key switch
            {
                WinForms.Keys.S => SettingItem,
                WinForms.Keys.X => ExitItem,
#if DEBUG
                WinForms.Keys.D => DisconnectItem,
                WinForms.Keys.R => RestoreItem,
#endif
                _ => null,
            };

            if (item is not null)
            {
                Close();
                item.PerformClick();
                return true;
            }
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }
}

internal sealed class DarkTrayRenderer : WinForms.ToolStripProfessionalRenderer
{
    public DarkTrayRenderer()
        : base(new DarkTrayColorTable())
    {
    }

    protected override void OnRenderToolStripBorder(WinForms.ToolStripRenderEventArgs e)
    {
    }
}

internal sealed class DarkTrayColorTable : WinForms.ProfessionalColorTable
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

    private static Drawing.Color ToDrawing(System.Windows.Media.Color c)
        => Drawing.Color.FromArgb(c.A, c.R, c.G, c.B);
}
