using MGA_G_Delay_Run.IO;
using MGA_G_Delay_Run.Native;
using MGA_G_Delay_Run.Theme;

namespace MGA_G_Delay_Run.Forms;

/// <summary>
/// 全フォームの基底。ダークテーマ・最前面・描画完了後の表示を共通化する。
/// </summary>
public class AppForm : Form
{
    private bool _revealed;
    private bool _boundsRestored;

    protected AppForm()
    {
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        Font = AppFonts.UI;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        ShowInTaskbar = true;
        Opacity = 0;
        Icon = AppIcons.Default;
    }

    /// <summary>
    /// ウィンドウ配置の記憶キー。既定は型の完全名。
    /// </summary>
    protected virtual string WindowBoundsKey => GetType().FullName ?? GetType().Name;

    /// <summary>
    /// false のとき位置・サイズを記憶せず、常にメインディスプレイ中央へ配置する。
    /// </summary>
    protected virtual bool PersistWindowBounds => true;

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        DarkTitleBar.Apply(this);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        AppTheme.Apply(this);
        DarkTitleBar.Apply(this);
        PerformLayout();

        if (PersistWindowBounds)
        {
            _boundsRestored = WindowBoundsStore.TryRestore(this, WindowBoundsKey);
        }

        if (!_boundsRestored)
        {
            CenterOnPrimaryDisplay();
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        if (!PersistWindowBounds || !_boundsRestored)
        {
            CenterOnPrimaryDisplay();
        }

        BringToFront();
        Activate();
        RevealAfterPaint();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (PersistWindowBounds)
        {
            try
            {
                WindowBoundsStore.Save(this, WindowBoundsKey);
            }
            catch (Exception ex) when (
                ex is IOException
                    or UnauthorizedAccessException
                    or NotSupportedException)
            {
                // 配置の保存失敗で閉じ処理は止めない
            }
        }

        base.OnFormClosing(e);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            Close();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    /// <summary>
    /// メインディスプレイの作業領域中央へ、ウィンドウサイズを考慮して配置する。
    /// </summary>
    protected void CenterOnPrimaryDisplay()
    {
        var screen = Screen.PrimaryScreen;
        if (screen is null)
        {
            return;
        }

        var area = screen.WorkingArea;
        var x = area.Left + Math.Max(0, (area.Width - Width) / 2);
        var y = area.Top + Math.Max(0, (area.Height - Height) / 2);
        Location = new Point(x, y);
    }

    /// <summary>
    /// ウィンドウが作業領域内に収まるよう位置を補正する。
    /// </summary>
    protected void EnsureOnScreen()
    {
        var screen = Screen.FromControl(this);
        var area = screen.WorkingArea;
        var x = Math.Min(Location.X, area.Right - Width);
        var y = Math.Min(Location.Y, area.Bottom - Height);
        x = Math.Max(area.Left, x);
        y = Math.Max(area.Top, y);
        Location = new Point(x, y);
    }

    /// <summary>
    /// レイアウトと初回描画が終わってからフォームを可視化する。
    /// </summary>
    private void RevealAfterPaint()
    {
        if (_revealed || DesignMode)
        {
            Opacity = 1;
            return;
        }

        Update();

        BeginInvoke(() =>
        {
            if (IsDisposed)
            {
                return;
            }

            Opacity = 1;
            // Opacity 変更で付く WS_EX_LAYERED を外さないと DWM Acrylic が黒/グレーになる
            AcrylicBackdrop.ClearLayeredStyle(Handle);
            _revealed = true;
            OnRevealed();
        });
    }

    /// <summary>
    /// 描画完了後、ウィンドウが可視化されたときに呼ばれる。
    /// </summary>
    protected virtual void OnRevealed()
    {
    }
}
