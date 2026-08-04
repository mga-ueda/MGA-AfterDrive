using System.Windows;
using System.Windows.Controls;
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
            var screenMargin = AppLayout.WindowFitScreenMargin;
            var visibleRows = Math.Max(MinVisibleRows, _entries.Count);
            var rowHeight = AppLayout.SettingGridRowHeight;

            EntryGrid.RowHeight = rowHeight;
            EntryGrid.ColumnHeaderHeight = AppLayout.SettingGridHeaderHeight;

            var measureWidth = Math.Max(0, Width - (spacing * 2));
            OptionsPanel.Measure(new System.Windows.Size(measureWidth, double.PositiveInfinity));
            ButtonBar.Measure(new System.Windows.Size(measureWidth, double.PositiveInfinity));

            var optionsHeight = OptionsPanel.DesiredSize.Height;
            if (optionsHeight <= 0)
            {
                optionsHeight = AppLayout.SettingOptionsPanelEstimateHeight;
            }

            var buttonBarHeight = ButtonBar.DesiredSize.Height;
            if (buttonBarHeight <= 0)
            {
                buttonBarHeight = AppLayout.ButtonHeight;
            }

            // ヘッダー + 行 + DataGrid 上下ボーダー
            var gridHeight = AppLayout.SettingGridHeaderHeight
                + (rowHeight * visibleRows)
                + 2;

            var contentHeight = spacing
                + optionsHeight
                + OptionsPanel.Margin.Bottom
                + gridHeight
                + ButtonBar.Margin.Top
                + buttonBarHeight
                + spacing;

            var frame = SystemParameters.WindowNonClientFrameThickness;
            var chrome = frame.Top + frame.Bottom;

            var area = SystemParameters.WorkArea;
            var desiredHeight = Math.Min(contentHeight + chrome, area.Height - screenMargin);
            var desiredWidth = Math.Min(Math.Max(Width, 720), area.Width - screenMargin);

            Width = desiredWidth;
            Height = Math.Max(MinHeight, desiredHeight);

            // 未表示中に EnsureOnScreen すると画面外退避が取り消され左上に出る
            if (IsRevealed)
            {
                EnsureOnScreen();
            }
        }
        finally
        {
            _fittingWindow = false;
        }
    }

    /// <summary>
    /// File Name 列をセル内容に合わせて広げる（見切れ防止）。
    /// </summary>
    private void AdjustFileNameColumnWidth()
    {
        if (FileNameColumn is null)
        {
            return;
        }

        // いったん Auto に戻してから SizeToCells を当て直す（追加行の再計測用）
        FileNameColumn.Width = new DataGridLength(1, DataGridLengthUnitType.SizeToCells);
    }
}
