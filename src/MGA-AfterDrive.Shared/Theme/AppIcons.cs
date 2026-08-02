namespace MGA_AfterDrive.Theme;

/// <summary>
/// アプリケーション共通アイコン。
/// </summary>
public static class AppIcons
{
    private const string ResourceName = "MGA_AfterDrive.Resources.app.ico";

    private static readonly Lazy<Icon> DefaultLazy = new(LoadDefault);

    public static Icon Default => DefaultLazy.Value;

    private static Icon LoadDefault()
    {
        var assembly = typeof(AppIcons).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Icon resource not found: {ResourceName}");
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        copy.Position = 0;
        using var icon = new Icon(copy);
        return (Icon)icon.Clone();
    }
}
