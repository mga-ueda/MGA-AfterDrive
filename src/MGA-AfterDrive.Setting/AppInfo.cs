using System.Reflection;
using MGA_AfterDrive.IO;

namespace MGA_AfterDrive.Setting;

/// <summary>
/// 設定アプリの表示名とバージョン。
/// </summary>
public static class AppInfo
{
    public const string ProductName = "MGA AfterDrive Setting";

    public static string Version { get; } = AppVersion.From(Assembly.GetExecutingAssembly());

    public static string WindowTitle { get; } = $"{ProductName} - Version {Version}";
}
