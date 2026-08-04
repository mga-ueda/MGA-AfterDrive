namespace MgaAfterDrive.Theme;

/// <summary>
/// アプリケーション共通のレイアウト寸法（device-independent pixels / logical）。
/// </summary>
public static class AppLayout
{
    /// <summary>
    /// 規定のパーツ間隔。
    /// </summary>
    public const int Spacing = 10;

    /// <summary>
    /// 標準ボタンの高さ。
    /// </summary>
    public const int ButtonHeight = 32;

    /// <summary>
    /// ボタン枠の角丸半径。
    /// </summary>
    public const int ButtonCornerRadius = 6;

    /// <summary>
    /// ステータスバーの高さ。
    /// XAML の Height 等は double 必須（int の x:Static は XamlParseException になる）。
    /// </summary>
    public const double StatusBarHeight = 28;

    /// <summary>
    /// 自前描画タイトルバー（キャプション）の高さ。
    /// </summary>
    public const int CaptionBarHeight = 32;

    /// <summary>
    /// 自前描画スクロールバーの幅。
    /// </summary>
    public const int ScrollbarWidth = 6;

    /// <summary>
    /// 自前描画スクロールバーつまみの最小高さ。
    /// </summary>
    public const int ScrollbarMinThumbHeight = 24;

    /// <summary>
    /// Setting のオプション欄おおよそ高さ（フィット計算用）。
    /// </summary>
    public const int SettingOptionsPanelEstimateHeight = 160;

    /// <summary>
    /// Setting グリッドの既定行の高さ。
    /// </summary>
    public const int SettingGridRowHeight = 28;

    /// <summary>
    /// Setting グリッド列ヘッダーの高さ。
    /// </summary>
    public const int SettingGridHeaderHeight = 32;

    /// <summary>
    /// ウィンドウを画面内に収めるときの余白。
    /// </summary>
    public const int WindowFitScreenMargin = 40;
}
