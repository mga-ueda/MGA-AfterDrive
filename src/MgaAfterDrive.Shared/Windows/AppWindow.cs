using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using MgaAfterDrive.IO;
using MgaAfterDrive.Native;
using MgaAfterDrive.Theme;

namespace MgaAfterDrive.Windows;

/// <summary>
/// 全ウィンドウの基底。ダークタイトル・最前面・描画完了後の表示を共通化する。
/// </summary>
public class AppWindow : Window
{
    /// <summary>
    /// 未レイアウト表示のフラッシュを避けるための画面外座標。
    /// </summary>
    protected const double OffScreenCoordinate = -32000;

    /// <summary>
    /// 画面外に退避しているとみなす閾値（Main のトレイ復帰判定用）。
    /// </summary>
    protected const double OffScreenParkThreshold = -10000;

    private bool _revealed;
    private bool _boundsRestored;

    protected AppWindow()
    {
        WindowStartupLocation = WindowStartupLocation.Manual;
        FontFamily = AppFonts.UIFamily;
        FontSize = AppFonts.UISize;
        Topmost = true;
        ShowInTaskbar = true;
        // 現状オーバーライドなし（常に true）。描画前フラッシュ防止のため 0 から Reveal。
        Opacity = UseDeferredReveal ? 0 : 1;
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;
        Icon = AppIcons.DefaultImage;
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
    /// true のとき Opacity=0 で開始し、描画後に可視化する（未レイアウト表示のチラつき対策）。
    /// 現状サブクラスはオーバーライドしていない。
    /// </summary>
    protected virtual bool UseDeferredReveal => true;

    /// <summary>
    /// false のとき ContentRendered では可視化せず、サブクラスが <see cref="RevealNow"/> を呼ぶ。
    /// あわせて Loaded / ContentRendered の自動中央寄せもスキップする。
    /// </summary>
    protected virtual bool RevealOnContentRendered => true;

    /// <summary>
    /// false のとき初回表示で Opacity を上げず、サブクラスがトレイ格納などへ進める。
    /// </summary>
    protected virtual bool ShouldRevealOnShown => true;

    /// <summary>
    /// 初回描画完了後に可視化済みか。
    /// </summary>
    protected bool IsRevealed => _revealed;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        DarkTitleBar.Apply(new WindowInteropHelper(this).Handle);
    }

    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);
        Loaded += OnWindowLoaded;
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnWindowLoaded;

        if (PersistWindowBounds)
        {
            _boundsRestored = WindowBoundsStore.TryRestore(this, WindowBoundsKey);
        }

        // トレイ起動・手動 Reveal 窓は中央寄せしない（画面内に出すとフラッシュの原因になる）
        if (!_boundsRestored && ShouldRevealOnShown && RevealOnContentRendered)
        {
            CenterOnPrimaryDisplay();
        }
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);

        if (ShouldRevealOnShown)
        {
            // 遅延 Reveal の窓は、準備完了前に Activate / 中央寄せすると一瞬チラつくことがある
            if (!RevealOnContentRendered)
            {
                return;
            }

            if (!PersistWindowBounds || !_boundsRestored)
            {
                CenterOnPrimaryDisplay();
            }

            Activate();
            if (UseDeferredReveal)
            {
                RevealAfterPaint();
            }
            else
            {
                Opacity = 1;
                _revealed = true;
                OnRevealed();
            }

            return;
        }

        // トレイ起動など: Opacity=0 のまま即座に隠す。
        // ClearLayeredStyle すると Opacity=0 が効かなくなり一瞬全面表示されるため呼ばない。
        Hide();
        Dispatcher.BeginInvoke(() =>
        {
            if (_revealed)
            {
                return;
            }

            _revealed = true;
            OnRevealed();
        });
    }

    /// <summary>
    /// 可視化シーケンス完了を記録する（Opacity / Acrylic は呼び出し側で済ませている前提）。
    /// </summary>
    protected void MarkRevealed()
    {
        _revealed = true;
    }

    /// <summary>
    /// ウィンドウを画面外へ退避する（トレイ復帰時など）。
    /// </summary>
    protected void ParkOffScreen()
    {
        Left = OffScreenCoordinate;
        Top = OffScreenCoordinate;
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
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

        base.OnClosing(e);
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
            return;
        }

        base.OnPreviewKeyDown(e);
    }

    /// <summary>
    /// メインディスプレイの作業領域中央へ、ウィンドウサイズを考慮して配置する。
    /// </summary>
    protected void CenterOnPrimaryDisplay()
    {
        var area = SystemParameters.WorkArea;
        Left = area.Left + Math.Max(0, (area.Width - Width) / 2);
        Top = area.Top + Math.Max(0, (area.Height - Height) / 2);
    }

    /// <summary>
    /// ウィンドウが作業領域内に収まるよう位置を補正する。
    /// </summary>
    protected void EnsureOnScreen()
    {
        var area = SystemParameters.WorkArea;
        var x = Math.Min(Left, area.Right - Width);
        var y = Math.Min(Top, area.Bottom - Height);
        Left = Math.Max(area.Left, x);
        Top = Math.Max(area.Top, y);
    }

    /// <summary>
    /// レイアウトと初回描画が終わってからウィンドウを可視化する。
    /// </summary>
    private void RevealAfterPaint()
    {
        if (_revealed || System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))
        {
            Opacity = 1;
            return;
        }

        Dispatcher.BeginInvoke(RevealNowCore);
    }

    /// <summary>
    /// サブクラスから、準備完了後に可視化する。
    /// </summary>
    protected void RevealNow()
    {
        if (_revealed)
        {
            return;
        }

        RevealNowCore();
    }

    private void RevealNowCore()
    {
        if (_revealed)
        {
            return;
        }

        Opacity = 1;
        _revealed = true;
        // レイヤード解除は Acrylic.Apply 内で Accent と連続して行う。
        // 先に外すと、透明背景が一瞬真っ黒になる。
        OnRevealed();
    }

    /// <summary>
    /// 描画完了後、ウィンドウが可視化されたときに呼ばれる。
    /// </summary>
    protected virtual void OnRevealed()
    {
    }
}
