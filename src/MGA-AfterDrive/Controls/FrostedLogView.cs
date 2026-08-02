using System.Drawing.Text;
using MGA_AfterDrive.Theme;

namespace MGA_AfterDrive.Controls;

/// <summary>
/// 親フォームの Acrylic（純黒キー）の上にログ文字だけを描く。
/// 行が溢れるときは右側に自前描画の縦スクロールバーを出す。
/// </summary>
public sealed class FrostedLogView : Control
{
    private const int ContentPadding = 4;

    private readonly List<string> _lines = [];
    private Font _drawFont = AppFonts.Log;
    private bool _ownsDrawFont;
    private int _scrollOffset;
    private int _lineHeight = 16;
    private bool _draggingThumb;
    private bool _thumbHot;
    private int _dragGrabOffsetY;

    public FrostedLogView()
    {
        // OptimizedDoubleBuffer / Opaque はガラス（純黒キー）を潰すため使わない
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.UserPaint
            | ControlStyles.ResizeRedraw
            | ControlStyles.Selectable,
            true);

        BackColor = Color.Black;
        ForeColor = AppTheme.LogForeground;
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

    /// <summary>現在の行をスナップショットする（復元用）。</summary>
    public string[] CaptureLines() => _lines.ToArray();

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

    public void ScrollToStart()
    {
        SetScrollOffset(0);
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        RefreshDrawFont();
        _lineHeight = Math.Max(12, _drawFont.Height + 2);
        ClampScroll();
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
        SetScrollOffset(_scrollOffset + delta);
        base.OnMouseWheel(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.Button != MouseButtons.Left || !ScrollbarVisible)
        {
            return;
        }

        var thumb = ThumbBounds;
        var track = ScrollbarBounds;

        if (thumb.Contains(e.Location))
        {
            _draggingThumb = true;
            _dragGrabOffsetY = e.Y - thumb.Y;
            Capture = true;
            return;
        }

        if (!track.Contains(e.Location))
        {
            return;
        }

        var page = Math.Max(1, VisibleLineCount() - 1);
        SetScrollOffset(e.Y < thumb.Y ? _scrollOffset - page : _scrollOffset + page);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_draggingThumb && ScrollbarVisible)
        {
            var track = ScrollbarBounds;
            var thumbHeight = ThumbBounds.Height;
            var travel = Math.Max(0, track.Height - thumbHeight);
            if (travel <= 0)
            {
                SetScrollOffset(0);
                return;
            }

            var thumbTop = Math.Clamp(e.Y - _dragGrabOffsetY, track.Y, track.Y + travel);
            var max = MaxScroll();
            var offset = (int)Math.Round((thumbTop - track.Y) * (double)max / travel);
            SetScrollOffset(offset);
            return;
        }

        var hot = ScrollbarVisible && ThumbBounds.Contains(e.Location);
        if (hot != _thumbHot)
        {
            _thumbHot = hot;
            Invalidate(ScrollbarBounds);
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);

        if (_thumbHot && !_draggingThumb)
        {
            _thumbHot = false;
            Invalidate(ScrollbarBounds);
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (e.Button != MouseButtons.Left || !_draggingThumb)
        {
            return;
        }

        _draggingThumb = false;
        Capture = false;
        _thumbHot = ScrollbarVisible && ThumbBounds.Contains(PointToClient(Cursor.Position));
        Invalidate(ScrollbarBounds);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        if (_lines.Count > 0)
        {
            var y = (float)ContentPadding;
            var textWidth = Math.Max(1, ContentWidth - ContentPadding);
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
                    new RectangleF(ContentPadding, y, textWidth, _lineHeight),
                    format);
                y += _lineHeight;
            }
        }

        DrawScrollbar(g);
    }

    private void DrawScrollbar(Graphics g)
    {
        if (!ScrollbarVisible)
        {
            return;
        }

        var track = ScrollbarBounds;
        using (var edgePen = new Pen(AppTheme.Border))
        {
            g.DrawLine(edgePen, track.Left, track.Top, track.Left, track.Bottom);
        }

        var thumb = ThumbBounds;
        if (thumb.Height <= 0)
        {
            return;
        }

        var thumbColor = _draggingThumb || _thumbHot
            ? AppTheme.SurfaceHover
            : AppTheme.Surface;
        using var thumbBrush = new SolidBrush(thumbColor);
        using var thumbBorder = new Pen(AppTheme.Border);
        g.FillRectangle(thumbBrush, thumb);
        g.DrawRectangle(thumbBorder, thumb.X, thumb.Y, thumb.Width - 1, thumb.Height - 1);
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

    private bool ScrollbarVisible => MaxScroll() > 0;

    private int ScrollbarWidth =>
        ScrollbarVisible ? LogicalToDeviceUnits(AppLayout.ScrollbarWidth) : 0;

    private int ContentWidth => Math.Max(1, ClientSize.Width - ScrollbarWidth);

    private Rectangle ScrollbarBounds
    {
        get
        {
            var width = ScrollbarWidth;
            if (width <= 0)
            {
                return Rectangle.Empty;
            }

            return new Rectangle(ClientSize.Width - width, 0, width, ClientSize.Height);
        }
    }

    private Rectangle ThumbBounds
    {
        get
        {
            var track = ScrollbarBounds;
            if (track.IsEmpty || _lines.Count == 0)
            {
                return Rectangle.Empty;
            }

            var visible = VisibleLineCount();
            var total = _lines.Count;
            var minThumb = LogicalToDeviceUnits(AppLayout.ScrollbarMinThumbHeight);
            var thumbHeight = (int)Math.Ceiling(track.Height * (visible / (double)total));
            thumbHeight = Math.Clamp(thumbHeight, Math.Min(minThumb, track.Height), track.Height);

            var max = MaxScroll();
            var travel = track.Height - thumbHeight;
            var thumbTop = max <= 0 || travel <= 0
                ? track.Y
                : track.Y + (int)Math.Round(travel * (_scrollOffset / (double)max));

            return new Rectangle(track.X, thumbTop, track.Width, thumbHeight);
        }
    }

    private int VisibleLineCount()
    {
        return Math.Max(1, (ClientSize.Height - (ContentPadding * 2)) / Math.Max(1, _lineHeight));
    }

    private int MaxScroll()
    {
        return Math.Max(0, _lines.Count - VisibleLineCount());
    }

    private void ClampScroll()
    {
        _scrollOffset = Math.Clamp(_scrollOffset, 0, MaxScroll());
    }

    private void SetScrollOffset(int offset)
    {
        var clamped = Math.Clamp(offset, 0, MaxScroll());
        if (clamped == _scrollOffset)
        {
            return;
        }

        _scrollOffset = clamped;
        Invalidate();
    }

    private void EnsureVisibleEnd()
    {
        _scrollOffset = MaxScroll();
    }
}
