using System.Text.Json;
using System.Text.Json.Serialization;

namespace MGA_AfterDrive.IO;

/// <summary>
/// ウィンドウの位置・サイズ・最大化状態を記憶する。
/// </summary>
public static class WindowBoundsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

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

    public static bool TryRestore(Form form, string key)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (!TryRead(out var store) || !store.Windows.TryGetValue(key, out var data))
        {
            return false;
        }

        if (data.Width < form.MinimumSize.Width || data.Height < form.MinimumSize.Height)
        {
            return false;
        }

        var bounds = new Rectangle(data.X, data.Y, data.Width, data.Height);
        if (!IsBoundsVisible(bounds))
        {
            return false;
        }

        form.StartPosition = FormStartPosition.Manual;
        form.WindowState = FormWindowState.Normal;
        form.Bounds = bounds;

        if (data.Maximized)
        {
            form.WindowState = FormWindowState.Maximized;
        }

        return true;
    }

    public static void Save(Form form, string key)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (form.WindowState == FormWindowState.Minimized)
        {
            return;
        }

        var bounds = form.WindowState == FormWindowState.Maximized
            ? form.RestoreBounds
            : form.Bounds;

        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var store = TryRead(out var existing) ? existing : new StoreFile();
        store.Windows[key] = new WindowBoundsData
        {
            X = bounds.X,
            Y = bounds.Y,
            Width = bounds.Width,
            Height = bounds.Height,
            Maximized = form.WindowState == FormWindowState.Maximized,
        };

        var directory = AppPaths.GetStoreDirectory();
        Directory.CreateDirectory(directory);
        File.WriteAllText(GetStoreFilePath(), JsonSerializer.Serialize(store, JsonOptions));
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

            var loaded = JsonSerializer.Deserialize<StoreFile>(json, JsonOptions);
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

    private static bool IsBoundsVisible(Rectangle bounds)
    {
        const int margin = 40;
        foreach (var screen in Screen.AllScreens)
        {
            var area = screen.WorkingArea;
            var probe = new Rectangle(
                bounds.X + margin,
                bounds.Y + margin,
                Math.Max(1, bounds.Width - (margin * 2)),
                Math.Max(1, bounds.Height - (margin * 2)));
            if (area.IntersectsWith(probe))
            {
                return true;
            }
        }

        return false;
    }
}
