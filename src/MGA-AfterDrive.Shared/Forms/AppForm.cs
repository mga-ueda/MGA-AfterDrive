using MGA_AfterDrive.IO;
using MGA_AfterDrive.Native;
using MGA_AfterDrive.Theme;

namespace MGA_AfterDrive.Forms;

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

    /// <summary>
    /// false のとき初回表示で Opacity を上げず、サブクラスがトレイ格納などへ進める。
    /// </summary>
    protected virtual bool ShouldRevealOnShown => true;

    /// <summary>
    /// 初回描画完了後に可視化済みか。
    /// </summary>
    protected bool IsRevealed => _revealed;

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

        // トレイ起動など非表示のまま進める場合は中央寄せしない（画面内に出すとフラッシュの原因になる）
        if (!_boundsRestored && ShouldRevealOnShown)
        {
            CenterOnPrimaryDisplay();
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        if (ShouldRevealOnShown)
        {
            if (!PersistWindowBounds || !_boundsRestored)
            {
                CenterOnPrimaryDisplay();
            }

            BringToFront();
            Activate();
            RevealAfterPaint();
            return;
        }

        // トレイ起動など: Opacity=0 のまま即座に隠す。
        // ClearLayeredStyle すると Opacity=0 が効かなくなり一瞬全面表示されるため呼ばない。
        Hide();
        BeginInvoke(() =>
        {
            if (IsDisposed || _revealed)
            {
                return;
            }

            _revealed = true;
            OnRevealed();
        });
    }

    /// <summary>
    /// トレイなどから初めてウィンドウを見せるときに呼ぶ。
    /// Acrylic 適用前に呼ぶと不透明が一瞬見えるため、適用後に <see cref="MarkRevealed"/> する方が安全。
    /// </summary>
    protected void RevealNow()
    {
        if (IsDisposed)
        {
            return;
        }

        Opacity = 1;
        if (IsHandleCreated)
        {
            AcrylicBackdrop.ClearLayeredStyle(Handle);
        }

        _revealed = true;
    }

    /// <summary>
    /// 可視化シーケンス完了を記録する（Opacity / Acrylic は呼び出し側で済ませている前提）。
    /// </summary>
    protected void MarkRevealed()
    {
        _revealed = true;
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
