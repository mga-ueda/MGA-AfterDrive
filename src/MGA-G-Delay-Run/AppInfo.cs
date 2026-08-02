using System.Reflection;

namespace MGA_G_Delay_Run;

/// <summary>
/// アプリケーション表示名とバージョン。
/// </summary>
public static class AppInfo
{
    public const string ProductName = "MGA G Delay Run";

    public static string Version { get; } = ReadVersion();

    public static string WindowTitle { get; } = $"{ProductName} - Version {Version}";

    private static string ReadVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
        {
            return informational.Split('+')[0];
        }

        var version = assembly.GetName().Version;
        return version is null ? "0.0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
    }
}
