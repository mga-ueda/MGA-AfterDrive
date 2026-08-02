using System.Drawing.Text;
using MGA_G_Delay_Run.Theme;

namespace MGA_G_Delay_Run.Controls;

/// <summary>
/// 親フォームの Acrylic（純黒キー）の上にログ文字だけを描く。
/// </summary>
public sealed class FrostedLogView : Control
{
    private readonly List<string> _lines = [];
    private Font _drawFont = AppFonts.Log;
    private bool _ownsDrawFont;
    private int _scrollOffset;
    private int _lineHeight = 16;

    public FrostedLogView()
    {
        // OptimizedDoubleBuffer は不透明ビットマップを載せてガラスを潰すため使わない
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.UserPaint
            | ControlStyles.ResizeRedraw,
            true);

        BackColor = Color.Black;
        ForeColor = AppTheme.Foreground;
        Font = AppFonts.Log;
        TabStop = true;
        RefreshDrawFont();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _ownsDrawFont)
        {
            _drawFont.Dispose();
            _ownsDrawFont = false;
        }

        base.Dispose(disposing);
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        // 純黒 = DWM ガラスキー（灰色を塗ると透けない）
        pevent.Graphics.Clear(Color.Black);
    }

    public void Clear()
    {
        _lines.Clear();
        _scrollOffset = 0;
        Invalidate();
    }

    public void AppendLine(string line)
    {
        _lines.Add(line);
        EnsureVisibleEnd();
        Invalidate();
    }

    public void AppendLines(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        foreach (var line in lines)
        {
            _lines.Add(line);
        }

        EnsureVisibleEnd();
        Invalidate();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        RefreshDrawFont();
        _lineHeight = Math.Max(12, _drawFont.Height + 2);
        EnsureVisibleEnd();
        Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        ClampScroll();
        Invalidate();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        var delta = e.Delta > 0 ? -3 : 3;
        _scrollOffset = Math.Clamp(_scrollOffset + delta, 0, MaxScroll());
        Invalidate();
        base.OnMouseWheel(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        // 背景は OnPaintBackground の純黒のまま。文字だけ描く。
        if (_lines.Count == 0)
        {
            return;
        }

        var g = e.Graphics;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        var y = 4f;
        var textWidth = Math.Max(1, ClientSize.Width - 8);
        var bottom = ClientSize.Height;

        using var brush = new SolidBrush(ForeColor);
        using var format = new StringFormat(StringFormat.GenericTypographic)
        {
            Alignment = StringAlignment.Near,
            LineAlignment = StringAlignment.Near,
            FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.NoClip,
            Trimming = StringTrimming.EllipsisCharacter,
        };

        for (var i = _scrollOffset; i < _lines.Count && y < bottom; i++)
        {
            g.DrawString(
                _lines[i],
                _drawFont,
                brush,
                new RectangleF(4, y, textWidth, _lineHeight),
                format);
            y += _lineHeight;
        }
    }

    private void RefreshDrawFont()
    {
        if (_ownsDrawFont)
        {
            _drawFont.Dispose();
            _ownsDrawFont = false;
        }

        if (Font.Style == FontStyle.Regular)
        {
            _drawFont = Font;
            return;
        }

        _drawFont = new Font(Font, FontStyle.Regular);
        _ownsDrawFont = true;
    }

    private int VisibleLineCount()
    {
        return Math.Max(1, (ClientSize.Height - 8) / Math.Max(1, _lineHeight));
    }

    private int MaxScroll()
    {
        return Math.Max(0, _lines.Count - VisibleLineCount());
    }

    private void ClampScroll()
    {
        _scrollOffset = Math.Clamp(_scrollOffset, 0, MaxScroll());
    }

    private void EnsureVisibleEnd()
    {
        _scrollOffset = MaxScroll();
    }
}
