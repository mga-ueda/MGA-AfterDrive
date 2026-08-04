using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;

namespace MgaAfterDrive.IO;

/// <summary>
/// ウィンドウの位置・サイズ・最大化状態を記憶する。
/// </summary>
public static class WindowBoundsStore
{
    private sealed class WindowBoundsData
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public bool Maximized { get; set; }
    }

    private sealed class StoreFile
    {
        [JsonPropertyName("windows")]
        public Dictionary<string, WindowBoundsData> Windows { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public static string GetStoreFilePath() => Path.Combine(AppPaths.GetStoreDirectory(), "window-bounds.json");

    public static bool TryRestore(Window window, string key)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (!TryRead(out var store) || !store.Windows.TryGetValue(key, out var data))
        {
            return false;
        }

        if (data.Width < window.MinWidth || data.Height < window.MinHeight)
        {
            return false;
        }

        if (!IsBoundsVisible(data.X, data.Y, data.Width, data.Height))
        {
            return false;
        }

        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.WindowState = WindowState.Normal;
        window.Left = data.X;
        window.Top = data.Y;
        window.Width = data.Width;
        window.Height = data.Height;

        if (data.Maximized)
        {
            window.WindowState = WindowState.Maximized;
        }

        return true;
    }

    public static void Save(Window window, string key)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (window.WindowState == WindowState.Minimized)
        {
            return;
        }

        var bounds = window.WindowState == WindowState.Maximized
            ? window.RestoreBounds
            : new Rect(window.Left, window.Top, window.Width, window.Height);

        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var store = TryRead(out var existing) ? existing : new StoreFile();
        store.Windows[key] = new WindowBoundsData
        {
            X = (int)Math.Round(bounds.X),
            Y = (int)Math.Round(bounds.Y),
            Width = (int)Math.Round(bounds.Width),
            Height = (int)Math.Round(bounds.Height),
            Maximized = window.WindowState == WindowState.Maximized,
        };

        var directory = AppPaths.GetStoreDirectory();
        Directory.CreateDirectory(directory);
        File.WriteAllText(GetStoreFilePath(), JsonSerializer.Serialize(store, AppJson.Indented));
    }

    private static bool TryRead(out StoreFile store)
    {
        store = new StoreFile();
        var path = GetStoreFilePath();
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            var json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            var loaded = JsonSerializer.Deserialize<StoreFile>(json, AppJson.Indented);
            if (loaded?.Windows is null)
            {
                return false;
            }

            store = new StoreFile
            {
                Windows = new Dictionary<string, WindowBoundsData>(
                    loaded.Windows,
                    StringComparer.OrdinalIgnoreCase),
            };
            return true;
        }
        catch (Exception ex) when (
            ex is IOException
                or UnauthorizedAccessException
                or JsonException
                or NotSupportedException)
        {
            return false;
        }
    }

    private static bool IsBoundsVisible(double x, double y, double width, double height)
    {
        const double margin = 40;
        var virtualLeft = SystemParameters.VirtualScreenLeft;
        var virtualTop = SystemParameters.VirtualScreenTop;
        var virtualRight = virtualLeft + SystemParameters.VirtualScreenWidth;
        var virtualBottom = virtualTop + SystemParameters.VirtualScreenHeight;

        var probeLeft = x + margin;
        var probeTop = y + margin;
        var probeRight = probeLeft + Math.Max(1, width - (margin * 2));
        var probeBottom = probeTop + Math.Max(1, height - (margin * 2));

        return probeRight > virtualLeft
            && probeLeft < virtualRight
            && probeBottom > virtualTop
            && probeTop < virtualBottom;
    }
}
