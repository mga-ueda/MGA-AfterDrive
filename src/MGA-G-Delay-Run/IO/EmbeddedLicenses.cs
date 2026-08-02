using System.Reflection;
using System.Text;

namespace MGA_G_Delay_Run.IO;

/// <summary>
/// アプリに内包したサードパーティライセンス文言。
/// </summary>
public static class EmbeddedLicenses
{
    private const string NoticesResource = "MGA_G_Delay_Run.Licenses.THIRD_PARTY_NOTICES.md";
    private const string WinBlurResource = "MGA_G_Delay_Run.Licenses.WinBlur-LICENSE.txt";

    public static string LoadCombinedText()
    {
        var notices = ReadResource(NoticesResource);
        var winBlur = ReadResource(WinBlurResource);
        return notices + Environment.NewLine + Environment.NewLine + winBlur;
    }

    public static IReadOnlyList<string> LoadCombinedLines()
    {
        var text = LoadCombinedText();
        return text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
    }

    private static string ReadResource(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"License resource not found: {resourceName}");
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd().TrimEnd();
    }
}
