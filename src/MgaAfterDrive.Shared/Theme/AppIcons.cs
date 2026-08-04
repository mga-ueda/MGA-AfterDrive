using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MgaAfterDrive.Theme;

/// <summary>
/// アプリケーション共通アイコン。
/// </summary>
public static class AppIcons
{
    private const string ResourceName = "MgaAfterDrive.Resources.app.ico";

    private static readonly Lazy<ImageSource> DefaultImageLazy = new(LoadDefaultImage);

    /// <summary>
    /// WPF ウィンドウ等で使う ImageSource。
    /// </summary>
    public static ImageSource DefaultImage => DefaultImageLazy.Value;

    /// <summary>
    /// トレイアイコン作成用に埋め込み ICO のコピーを開く。呼び出し側が Dispose する。
    /// </summary>
    public static Stream OpenIconStream()
    {
        var assembly = typeof(AppIcons).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Icon resource not found: {ResourceName}");

        var copy = new MemoryStream();
        stream.CopyTo(copy);
        copy.Position = 0;
        return copy;
    }

    private static ImageSource LoadDefaultImage()
    {
        using var stream = OpenIconStream();
        var decoder = BitmapDecoder.Create(
            stream,
            BitmapCreateOptions.None,
            BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        frame.Freeze();
        return frame;
    }
}
