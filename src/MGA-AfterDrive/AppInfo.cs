using System.Reflection;
using MGA_AfterDrive.IO;

namespace MGA_AfterDrive;

/// <summary>
/// アプリケーション表示名とバージョン。
/// </summary>
public static class AppInfo
{
    public const string ProductName = "MGA AfterDrive";

    public static string Version { get; } = AppVersion.From(Assembly.GetExecutingAssembly());

    public static string WindowTitle { get; } = $"{ProductName} - Version {Version}";
}
