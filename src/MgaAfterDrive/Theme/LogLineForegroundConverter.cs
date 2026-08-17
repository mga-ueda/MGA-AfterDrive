using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using MgaAfterDrive.Theme;
using MediaBrush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;

namespace MgaAfterDrive;

/// <summary>
/// ログ行の [WARN] / [ERROR] に応じて文字色を返す。
/// </summary>
public sealed class LogLineForegroundConverter : IValueConverter
{
    private static readonly MediaBrush DefaultBrush = Freeze(AppTheme.Foreground);
    private static readonly MediaBrush WarnBrush = Freeze(AppTheme.LogWarning);
    private static readonly MediaBrush ErrorBrush = Freeze(AppTheme.LogError);

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string text)
        {
            return DefaultBrush;
        }

        if (text.Contains("[ERROR]", StringComparison.Ordinal))
        {
            return ErrorBrush;
        }

        if (text.Contains("[WARN]", StringComparison.Ordinal))
        {
            return WarnBrush;
        }

        return DefaultBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        System.Windows.Data.Binding.DoNothing;

    private static SolidColorBrush Freeze(MediaColor color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
