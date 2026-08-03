using System.Drawing.Text;
using MGA_AfterDrive.Theme;

namespace MGA_AfterDrive.Controls;

/// <summary>
/// クライアント領域化したタイトルバー。
/// ガラスキー（純黒）の上にアイコン・タイトル・トレイ格納ボタンだけを描き、
/// 格納ボタン以外はヒットテストを透過して親フォームの
/// HTCAPTION（ドラッグ移動）に委ねる。
/// 終了はシステムトレイメニューのみ（このボタンは終了しない）。
/// </summary>
public sealed class GlassCaptionBar : Control
{
    private const int WmNcHitTest = 0x0084;
    private const int HtTransparent = -1;

    /// <summary>キャプションボタンの幅（96 DPI 基準）。Win11 標準に合わせる。</summary>
    private const int CaptionButtonLogicalWidth = 46;

    private Bitmap? _iconBitmap;
    private bool _hideHover;
    private bool _hidePressed;

    /// <summary>トレイへ格納する要求（アプリ終了ではない）。</summary>
    public event EventHandler? HideRequested;

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

    private Rectangle HideButtonBounds
    {
        get
        {
            var width = LogicalToDeviceUnits(CaptionButtonLogicalWidth);
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
        // 格納ボタン以外は親フォームへヒットテストを透過し、
        // フォーム側で HTCAPTION / HTTOP を返してもらう
        if (m.Msg == WmNcHitTest)
        {
            var screenPoint = new Point(
                unchecked((short)(long)m.LParam),
                unchecked((short)((long)m.LParam >> 16)));

            if (!HideButtonBounds.Contains(PointToClient(screenPoint)))
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
        DrawHideButton(g);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        SetHideHover(HideButtonBounds.Contains(e.Location));
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        SetHideHover(false);
        _hidePressed = false;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left && HideButtonBounds.Contains(e.Location))
        {
            _hidePressed = true;
            Invalidate(HideButtonBounds);
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        var wasPressed = _hidePressed;
        _hidePressed = false;
        Invalidate(HideButtonBounds);

        if (wasPressed && e.Button == MouseButtons.Left && HideButtonBounds.Contains(e.Location))
        {
            HideRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void DrawTitle(Graphics g, int left)
    {
        var bounds = new RectangleF(
            left,
            0,
            Math.Max(1, HideButtonBounds.Left - left - LogicalToDeviceUnits(AppLayout.Spacing)),
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

    private void DrawHideButton(Graphics g)
    {
        var bounds = HideButtonBounds;

        // 終了ではないため赤ホバーは使わず、Win11 最小化相当の薄いハイライトにする
        if (_hidePressed)
        {
            using var pressed = new SolidBrush(AppTheme.Border);
            g.FillRectangle(pressed, bounds);
        }
        else if (_hideHover)
        {
            using var hover = new SolidBrush(AppTheme.SurfaceHover);
            g.FillRectangle(hover, bounds);
        }

        // Win11 最小化グリフ: 中央の短い横棒
        var barWidth = LogicalToDeviceUnits(10);
        var barHeight = Math.Max(1, LogicalToDeviceUnits(1));
        var barX = bounds.X + ((bounds.Width - barWidth) / 2);
        var barY = bounds.Y + ((bounds.Height - barHeight) / 2);
        using var brush = new SolidBrush(ForeColor);
        g.FillRectangle(brush, barX, barY, barWidth, barHeight);
    }

    private void SetHideHover(bool value)
    {
        if (_hideHover == value)
        {
            return;
        }

        _hideHover = value;
        Invalidate(HideButtonBounds);
    }

    private void RebuildIconBitmap()
    {
        _iconBitmap?.Dispose();
        var size = LogicalToDeviceUnits(16);
        using var sized = new Icon(AppIcons.Default, size, size);
        _iconBitmap = sized.ToBitmap();
    }
}
