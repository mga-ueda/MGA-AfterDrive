namespace MGA_G_Delay_Run.Theme;

/// <summary>
/// アプリケーション共通のダークテーマ色定義。
/// UI の色指定は原則このクラスの定数のみを使用する。
/// </summary>
public static class AppTheme
{
    public static readonly Color Background = Color.FromArgb(30, 30, 30);

    /// <summary>
    /// すりガラスの不透明度（0x00〜0xFF）。低いほど透ける。
    /// <see cref="Native.AcrylicBackdrop"/> の ACCENT_POLICY に渡す。
    /// OS の SYSTEMBACKDROP Acrylic は薄くできないため、こちらで制御する。
    /// </summary>
    public const byte AcrylicTintAlpha = 0x20;

    public static readonly Color Surface = Color.FromArgb(45, 45, 48);
    public static readonly Color SurfaceHover = Color.FromArgb(62, 62, 66);
    public static readonly Color Border = Color.FromArgb(63, 63, 70);
    public static readonly Color Foreground = Color.FromArgb(241, 241, 241);
    public static readonly Color ForegroundMuted = Color.FromArgb(180, 180, 180);
    public static readonly Color Accent = Color.FromArgb(0, 122, 204);
    /// <summary>
    /// リスト選択など、アクセントより薄いハイライト。
    /// </summary>
    public static readonly Color Selection = Color.FromArgb(56, 88, 120);
    public static readonly Color Danger = Color.FromArgb(180, 40, 40);
    public static readonly Color DangerHover = Color.FromArgb(200, 55, 55);

    /// <summary>
    /// フォームと配下コントロールへダークテーマを適用する。
    /// </summary>
    public static void Apply(Control root)
    {
        ArgumentNullException.ThrowIfNull(root);

        root.BackColor = Background;
        root.ForeColor = Foreground;

        foreach (Control child in root.Controls)
        {
            ApplyToControl(child);
        }
    }

    private static void ApplyToControl(Control control)
    {
        control.ForeColor = Foreground;

        switch (control)
        {
            case Button button:
                ApplyButton(button);
                break;

            case RichTextBox richTextBox:
                richTextBox.BackColor = Background;
                richTextBox.ForeColor = Foreground;
                richTextBox.BorderStyle = BorderStyle.None;
                break;

            case DataGridView grid:
                ApplyDataGridView(grid);
                break;

            case TextBox or ListBox or ComboBox:
                control.BackColor = Surface;
                break;

            case Panel or GroupBox or UserControl or TableLayoutPanel or FlowLayoutPanel:
                control.BackColor = Background;
                break;

            case Label:
                control.BackColor = Color.Transparent;
                break;

            default:
                // FrostedLogView 等は呼び出し側でガラス用色を設定する
                break;
        }

        foreach (Control child in control.Controls)
        {
            ApplyToControl(child);
        }
    }

    /// <summary>
    /// ボタン共通スタイル（文字は上下左右中央）。
    /// </summary>
    public static void ApplyButton(Button button)
    {
        ArgumentNullException.ThrowIfNull(button);

        button.BackColor = Surface;
        button.ForeColor = Foreground;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = Border;
        button.FlatAppearance.MouseOverBackColor = SurfaceHover;
        button.TextAlign = ContentAlignment.MiddleCenter;
        button.ImageAlign = ContentAlignment.MiddleCenter;
        button.UseCompatibleTextRendering = false;
        button.Padding = Padding.Empty;
        button.AutoSize = false;
        button.Cursor = Cursors.Hand;
        button.FlatAppearance.MouseDownBackColor = SurfaceHover;
    }

    private static void ApplyDataGridView(DataGridView grid)
    {
        grid.BackgroundColor = Background;
        grid.GridColor = Border;
        grid.BorderStyle = BorderStyle.None;
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        grid.RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.Single;

        grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Surface,
            ForeColor = Foreground,
            SelectionBackColor = Selection,
            SelectionForeColor = Foreground,
            Font = grid.Font,
        };

        grid.RowHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Surface,
            ForeColor = Foreground,
            SelectionBackColor = Selection,
            SelectionForeColor = Foreground,
        };

        grid.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Background,
            ForeColor = Foreground,
            SelectionBackColor = Selection,
            SelectionForeColor = Foreground,
            Font = grid.Font,
        };

        grid.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Surface,
            ForeColor = Foreground,
            SelectionBackColor = Selection,
            SelectionForeColor = Foreground,
        };
    }
}
