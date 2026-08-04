using System.Windows;
using MgaAfterDrive.Theme;

namespace MgaAfterDrive;

public partial class SettingWindow
{
    private const int MinVisibleRows = 5;

    private void FitWindowToContent()
    {
        if (_fittingWindow || !IsLoaded)
        {
            return;
        }

        _fittingWindow = true;
        try
        {
            var spacing = AppLayout.Spacing;
            var margin = AppLayout.WindowFitScreenMargin;
            var visibleRows = Math.Max(MinVisibleRows, _entries.Count);
            var gridHeight = AppLayout.SettingGridHeaderHeight
                + (AppLayout.SettingGridRowHeight * visibleRows)
                + 4;

            var buttonBarHeight = spacing + AppLayout.ButtonHeight + spacing;
            var margins = spacing * 2;

            var area = SystemParameters.WorkArea;
            var desiredHeight = Math.Min(
                AppLayout.SettingOptionsPanelEstimateHeight + gridHeight + buttonBarHeight + margins + margin,
                area.Height - margin);
            var desiredWidth = Math.Min(Math.Max(Width, 720), area.Width - margin);

            Width = desiredWidth;
            Height = Math.Max(MinHeight, desiredHeight);
            EnsureOnScreen();
        }
        finally
        {
            _fittingWindow = false;
        }
    }
}
