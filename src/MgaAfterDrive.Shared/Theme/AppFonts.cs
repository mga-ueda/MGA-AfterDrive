using System.Windows.Media;

namespace MgaAfterDrive.Theme;

/// <summary>
/// アプリケーション共通フォント。利用可能なモダンフォントを優先して選ぶ。
/// サイズは WPF の DIP（約 9pt ≈ 12px、ログは約 11）。
/// </summary>
public static class AppFonts
{
    public static FontFamily UIFamily { get; } = Resolve(
        "Segoe UI Variable Text",
        "Segoe UI Variable",
        "Segoe UI");

    public static FontFamily LogFamily { get; } = Resolve(
        "Cascadia Mono",
        "Cascadia Code",
        "Consolas");

    public const double UISize = 12;
    public const double LogSize = 11;

    private static FontFamily Resolve(params string[] familyNames)
    {
        foreach (var familyName in familyNames)
        {
            if (IsInstalled(familyName))
            {
                return new FontFamily(familyName);
            }
        }

        return new FontFamily(string.Join(", ", familyNames));
    }

    private static bool IsInstalled(string familyName)
    {
        foreach (var family in Fonts.SystemFontFamilies)
        {
            if (string.Equals(family.Source, familyName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            foreach (var name in family.FamilyNames.Values)
            {
                if (string.Equals(name, familyName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
