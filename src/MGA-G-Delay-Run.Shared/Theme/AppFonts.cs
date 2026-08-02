namespace MGA_G_Delay_Run.Theme;

/// <summary>
/// アプリケーション共通フォント。利用可能なモダンフォントを優先して選ぶ。
/// </summary>
public static class AppFonts
{
    public static Font UI { get; } = Create(9F, FontStyle.Regular,
        "Segoe UI Variable Text",
        "Segoe UI Variable",
        "Segoe UI");

    public static Font Log { get; } = Create(8F, FontStyle.Regular,
        "Cascadia Mono",
        "Cascadia Code",
        "Consolas");

    private static Font Create(float size, FontStyle style, params string[] familyNames)
    {
        // ボールド合成を避け、必ず指定ウェイトのみを使う
        style &= ~FontStyle.Bold;

        foreach (var familyName in familyNames)
        {
            try
            {
                using var probe = new FontFamily(familyName);
                if (!probe.IsStyleAvailable(style))
                {
                    continue;
                }

                return new Font(probe, size, style, GraphicsUnit.Point);
            }
            catch (ArgumentException)
            {
                // 未インストールのフォントは次候補へ
            }
        }

        return new Font(SystemFonts.MessageBoxFont!.FontFamily, size, FontStyle.Regular, GraphicsUnit.Point);
    }
}
