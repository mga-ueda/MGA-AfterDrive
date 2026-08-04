using System.Windows.Media;

namespace MgaAfterDrive.Theme;

/// <summary>
/// アプリケーション共通のダークテーマ色定義。
/// UI の色指定は原則このクラスの定数のみを使用する。
/// XAML 側は Themes/AppStyles.xaml と値を揃えること。
/// </summary>
public static class AppTheme
{
    public static readonly Color Background = Color.FromRgb(30, 30, 30);

    /// <summary>
    /// すりガラスの不透明度（0x00〜0xFF）。低いほど透ける。
    /// <see cref="Native.AcrylicBackdrop"/> の ACCENT_POLICY に渡す。
    /// OS の SYSTEMBACKDROP Acrylic は薄くできないため、こちらで制御する。
    /// </summary>
    public const byte AcrylicTintAlpha = 0x20;

    public static readonly Color Surface = Color.FromRgb(45, 45, 48);
    public static readonly Color SurfaceHover = Color.FromRgb(62, 62, 66);
    public static readonly Color Border = Color.FromRgb(63, 63, 70);
    public static readonly Color Foreground = Color.FromRgb(241, 241, 241);
    public static readonly Color ForegroundMuted = Color.FromRgb(180, 180, 180);

    /// <summary>
    /// ログ文字色。通常の前景よりわずかに暗くして眩しさを抑える。
    /// </summary>
    public static readonly Color LogForeground = Color.FromRgb(212, 212, 212);
    public static readonly Color Accent = Color.FromRgb(0, 122, 204);
    /// <summary>アクセント枠用の明るい青。</summary>
    public static readonly Color AccentBorder = Color.FromRgb(40, 140, 220);
    /// <summary>アクセントボタンの塗り（暗い青）。</summary>
    public static readonly Color AccentFill = Color.FromRgb(14, 28, 42);
    /// <summary>アクセントボタンのホバー塗り。</summary>
    public static readonly Color AccentFillHover = Color.FromRgb(20, 38, 56);

    /// <summary>
    /// リスト選択など。背景に馴染む暗めのハイライト。
    /// </summary>
    public static readonly Color Selection = Color.FromRgb(40, 48, 58);

    /// <summary>警告枠（オレンジ）。</summary>
    public static readonly Color WarningBorder = Color.FromRgb(220, 130, 40);
    /// <summary>警告ボタンの塗り（暗いオレンジ）。</summary>
    public static readonly Color WarningFill = Color.FromRgb(42, 24, 10);
    /// <summary>警告ボタンのホバー塗り。</summary>
    public static readonly Color WarningFillHover = Color.FromRgb(56, 32, 14);

    /// <summary>危険枠・クローズ等の赤。</summary>
    public static readonly Color Danger = Color.FromRgb(200, 55, 55);
    /// <summary>危険ボタンの塗り（暗い赤）。</summary>
    public static readonly Color DangerFill = Color.FromRgb(42, 16, 16);
    /// <summary>危険ボタンのホバー塗り。</summary>
    public static readonly Color DangerFillHover = Color.FromRgb(56, 22, 22);
}
