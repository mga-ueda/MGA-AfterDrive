using System.Drawing.Text;
using MGA_G_Delay_Run.Theme;

namespace MGA_G_Delay_Run.Controls;

/// <summary>
/// クライアント領域化したタイトルバー。
/// ガラスキー（純黒）の上にアイコン・タイトル・閉じるボタンだけを描き、
/// 閉じるボタン以外はヒットテストを透過して親フォームの
/// HTCAPTION（ドラッグ移動）に委ねる。
/// </summary>
public sealed class GlassCaptionBar : Control
{
    private const int WmNcHitTest = 0x0084;
    private const int HtTransparent = -1;

    /// <summary>閉じるボタンの幅（96 DPI 基準）。Win11 標準に合わせる。</summary>
    private const int CloseButtonLogicalWidth = 46;

    private Bitmap? _iconBitmap;
    private bool _closeHover;
    private bool _closePressed;

    public event EventHandler? CloseRequested;

    public GlassCaptionBar()
    {
        // OptimizedDoubleBuffer は不透明ビットマップを載せてガラスを潰すため使わない
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.UserPaint
            | ControlStyles.ResizeRedraw,
            true);

        BackColor = Color.Black;
        ForeColor = AppTheme.Foreground;
        Font = AppFonts.UI;
        TabStop = false;
    }

    private Rectangle CloseButtonBounds
    {
        get
        {
            var width = LogicalToDeviceUnits(CloseButtonLogicalWidth);
            return new Rectangle(Width - width, 0, width, Height);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _iconBitmap?.Dispose();
            _iconBitmap = null;
        }

        base.Dispose(disposing);
    }

    protected override void WndProc(ref Message m)
    {
        // 閉じるボタン以外は親フォームへヒットテストを透過し、
        // フォーム側で HTCAPTION / HTTOP を返してもらう
        if (m.Msg == WmNcHitTest)
        {
            var screenPoint = new Point(
                unchecked((short)(long)m.LParam),
                unchecked((short)((long)m.LParam >> 16)));

            if (!CloseButtonBounds.Contains(PointToClient(screenPoint)))
            {
                m.Result = new IntPtr(HtTransparent);
                return;
            }
        }

        base.WndProc(ref m);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        RebuildIconBitmap();
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        RebuildIconBitmap();
        Invalidate();
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        Invalidate();
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        // 純黒 = DWM ガラスキー（灰色を塗ると透けない）
        pevent.Graphics.Clear(Color.Black);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        var x = LogicalToDeviceUnits(AppLayout.Spacing);

        if (_iconBitmap is not null)
        {
            var iconY = Math.Max(0, (Height - _iconBitmap.Height) / 2);
            g.DrawImage(_iconBitmap, x, iconY, _iconBitmap.Width, _iconBitmap.Height);
            x += _iconBitmap.Width + LogicalToDeviceUnits(AppLayout.Spacing);
        }

        DrawTitle(g, x);
        DrawCloseButton(g);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        SetCloseHover(CloseButtonBounds.Contains(e.Location));
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        SetCloseHover(false);
        _closePressed = false;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left && CloseButtonBounds.Contains(e.Location))
        {
            _closePressed = true;
            Invalidate(CloseButtonBounds);
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        var wasPressed = _closePressed;
        _closePressed = false;
        Invalidate(CloseButtonBounds);

        if (wasPressed && e.Button == MouseButtons.Left && CloseButtonBounds.Contains(e.Location))
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void DrawTitle(Graphics g, int left)
    {
        var bounds = new RectangleF(
            left,
            0,
            Math.Max(1, CloseButtonBounds.Left - left - LogicalToDeviceUnits(AppLayout.Spacing)),
            Height);

        using var brush = new SolidBrush(ForeColor);
        using var format = new StringFormat(StringFormat.GenericTypographic)
        {
            Alignment = StringAlignment.Near,
            LineAlignment = StringAlignment.Center,
            FormatFlags = StringFormatFlags.NoWrap,
            Trimming = StringTrimming.EllipsisCharacter,
        };

        g.DrawString(Text, Font, brush, bounds, format);
    }

    private void DrawCloseButton(Graphics g)
    {
        var bounds = CloseButtonBounds;

        if (_closePressed)
        {
            using var pressed = new SolidBrush(AppTheme.DangerHover);
            g.FillRectangle(pressed, bounds);
        }
        else if (_closeHover)
        {
            using var hover = new SolidBrush(AppTheme.Danger);
            g.FillRectangle(hover, bounds);
        }

        using var brush = new SolidBrush(ForeColor);
        using var format = new StringFormat(StringFormat.GenericTypographic)
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
        };

        g.DrawString("\u2715", Font, brush, bounds, format);
    }

    private void SetCloseHover(bool value)
    {
        if (_closeHover == value)
        {
            return;
        }

        _closeHover = value;
        Invalidate(CloseButtonBounds);
    }

    private void RebuildIconBitmap()
    {
        _iconBitmap?.Dispose();
        var size = LogicalToDeviceUnits(16);
        using var sized = new Icon(AppIcons.Default, size, size);
        _iconBitmap = sized.ToBitmap();
    }
}
