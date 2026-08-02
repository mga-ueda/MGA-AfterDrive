using System.Drawing.Drawing2D;
using System.Drawing.Text;
using MGA_AfterDrive.Theme;

namespace MGA_AfterDrive.Controls;

/// <summary>
/// 枠付き・角丸で、文字を上下左右の中央に描画するフラットボタン。
/// </summary>
public class AppButton : Button
{
    private bool _hover;
    private bool _pressed;

    public AppButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 1;
        UseVisualStyleBackColor = false;
        TextAlign = ContentAlignment.MiddleCenter;
        AutoSize = false;
        Cursor = Cursors.Hand;
        SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw
            | ControlStyles.SupportsTransparentBackColor,
            true);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hover = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = false;
        _pressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _pressed = true;
            Invalidate();
        }

        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        _pressed = false;
        Invalidate();
        base.OnMouseUp(e);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        Invalidate();
        base.OnEnabledChanged(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.Clear(Parent?.BackColor ?? AppTheme.Background);

        var backColor = ResolveBackColor();
        var borderColor = Enabled ? FlatAppearance.BorderColor : AppTheme.Border;
        var textColor = Enabled ? ForeColor : AppTheme.ForegroundMuted;
        var borderWidth = Math.Max(1f, FlatAppearance.BorderSize);

        // 枠のアンチエイリアスが見切れないよう 1px 内側に描く
        var bounds = new RectangleF(
            borderWidth / 2f,
            borderWidth / 2f,
            Math.Max(0f, Width - borderWidth - 1f),
            Math.Max(0f, Height - borderWidth - 1f));

        using (var path = CreateRoundRectangle(bounds, AppLayout.ButtonCornerRadius))
        {
            using (var backBrush = new SolidBrush(backColor))
            {
                g.FillPath(backBrush, path);
            }

            using var borderPen = new Pen(borderColor, borderWidth);
            borderPen.Alignment = PenAlignment.Center;
            g.DrawPath(borderPen, path);
        }

        if (string.IsNullOrEmpty(Text))
        {
            return;
        }

        DrawCenteredText(g, Text, textColor, bounds);
    }

    /// <summary>
    /// ボタン面（枠内）の高さセンターに文字を置く。光学オフセットは加えない。
    /// </summary>
    private void DrawCenteredText(Graphics g, string text, Color textColor, RectangleF faceBounds)
    {
        var rect = Rectangle.Round(faceBounds);
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        TextRenderer.DrawText(
            g,
            text,
            Font,
            rect,
            textColor,
            TextFormatFlags.HorizontalCenter
            | TextFormatFlags.VerticalCenter
            | TextFormatFlags.EndEllipsis
            | TextFormatFlags.NoPrefix
            | TextFormatFlags.NoPadding
            | TextFormatFlags.SingleLine);
    }

    private Color ResolveBackColor()
    {
        if (!Enabled)
        {
            return BackColor;
        }

        if (_pressed)
        {
            return FlatAppearance.MouseDownBackColor == Color.Empty
                ? AppTheme.SurfaceHover
                : FlatAppearance.MouseDownBackColor;
        }

        if (_hover)
        {
            return FlatAppearance.MouseOverBackColor == Color.Empty
                ? AppTheme.SurfaceHover
                : FlatAppearance.MouseOverBackColor;
        }

        return BackColor;
    }

    private static GraphicsPath CreateRoundRectangle(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Max(0f, Math.Min(radius * 2f, Math.Min(bounds.Width, bounds.Height)));
        if (diameter <= 0f)
        {
            path.AddRectangle(bounds);
            return path;
        }

        var arc = new RectangleF(bounds.Location, new SizeF(diameter, diameter));

        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }
}
