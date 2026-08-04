using System.Reflection;
using MgaAfterDrive.IO;

namespace MgaAfterDrive;

/// <summary>
/// アプリケーション表示名とバージョン。
/// </summary>
public static class AppInfo
{
    public const string ProductName = AppExecutableNames.HostProductName;

    public static string Version { get; } = AppVersion.From(Assembly.GetExecutingAssembly());

    public static string WindowTitle { get; } = $"{ProductName} - Version {Version}";
}
