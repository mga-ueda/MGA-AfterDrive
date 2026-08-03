using MGA_AfterDrive.Theme;

namespace MGA_AfterDrive.Setting;

public partial class MainForm
{
    /// <summary>
    /// 最低 5 行分の高さを確保し、行数・列幅に合わせてウィンドウを固定サイズ更新する。
    /// 内容が収まるよう寸法を取り、スクロールバーを出さない。
    /// </summary>
    private void FitWindowToContent()
    {
        if (_fittingWindow || !IsHandleCreated || IsDisposed || entryGrid.IsDisposed || entryGrid.ColumnCount == 0)
        {
            return;
        }

        _fittingWindow = true;
        try
        {
            // スクロールバー分の幅を奪われないよう、先に無効化する
            entryGrid.ScrollBars = ScrollBars.None;

            var spacing = LogicalToDeviceUnits(AppLayout.Spacing);
            var rowHeight = MeasureGridRowHeight();
            var visibleRows = Math.Max(MinVisibleRows, _entries.Count);
            var headerHeight = Math.Max(entryGrid.ColumnHeadersHeight, entryGrid.ColumnHeadersDefaultCellStyle.Font?.Height ?? 0);
            var idealGridHeight = headerHeight + (rowHeight * visibleRows) + GetGridChromeHeight();

            var columnsWidth = entryGrid.Columns.GetColumnsWidth(DataGridViewElementStates.Visible);
            if (entryGrid.RowHeadersVisible)
            {
                columnsWidth += entryGrid.RowHeadersWidth;
            }

            // 枠・セル余白・DPI 誤差で 1px 足りず横スクロールが出るのを防ぐ
            var gridChrome = GetGridChromeWidth() + spacing;

            var buttonBarContentWidth =
                startAllButton.Width
                + saveButton.Width
                + cancelButton.Width
                + (spacing * 4);

            var optionsBarHeight = Math.Max(
                optionsBar.PreferredSize.Height,
                (spacing * 2)
                    + maxWaitTextBox.Height
                    + spacing
                    + startMinimizedCheckBox.PreferredSize.Height
                    + spacing
                    + taskSchedulerNoteLabel.PreferredSize.Height
                    + spacing
                    + taskSchedulerButton.Height);

            // AppLayout 定数は 96 DPI 基準。実ボタン高さと DPI 換算の大きい方を使う
            var buttonHeight = Math.Max(
                LogicalToDeviceUnits(AppLayout.ButtonHeight),
                Math.Max(startAllButton.Height, Math.Max(saveButton.Height, cancelButton.Height)));
            var buttonBarHeight = spacing + buttonHeight + spacing;

            // Absolute 行と計算値を一致させ、Percent 行（グリッド）が削られないようにする
            if (rootLayout.RowCount >= 3)
            {
                rootLayout.RowStyles[2].SizeType = SizeType.Absolute;
                rootLayout.RowStyles[2].Height = buttonBarHeight;
            }

            var layoutMarginHeight =
                optionsBar.Margin.Vertical
                + entryGrid.Margin.Vertical
                + buttonBar.Margin.Vertical;

            var area = Screen.FromControl(this).WorkingArea;
            var chromeW = Width - ClientSize.Width;
            var chromeH = Height - ClientSize.Height;
            var maxClientW = Math.Max(200, area.Width - chromeW);
            var maxClientH = Math.Max(150, area.Height - chromeH);

            var maxWaitRowWidth =
                maxWaitLabel.PreferredWidth
                + spacing
                + maxWaitTextBox.Width
                + spacing
                + maxWaitUnitLabel.PreferredWidth;
            var optionsContentWidth =
                Math.Max(
                    Math.Max(
                        Math.Max(maxWaitRowWidth, startMinimizedCheckBox.PreferredSize.Width),
                        taskSchedulerNoteLabel.PreferredSize.Width),
                    taskSchedulerButton.Width)
                + (spacing * 2);

            var clientW = Math.Max(
                Math.Max(columnsWidth + gridChrome, buttonBarContentWidth),
                optionsContentWidth);
            var clientH = optionsBarHeight + idealGridHeight + buttonBarHeight + layoutMarginHeight;

            // 画面に収まらない場合のみ縦スクロールを許可（横は出さない）
            var heightClamped = clientH > maxClientH;
            clientW = Math.Min(clientW, maxClientW);
            clientH = Math.Min(clientH, maxClientH);

            if (heightClamped)
            {
                clientW = Math.Min(
                    Math.Max(clientW, columnsWidth + gridChrome + SystemInformation.VerticalScrollBarWidth),
                    maxClientW);
            }

            if (ClientSize.Width != clientW || ClientSize.Height != clientH)
            {
                ClientSize = new Size(clientW, clientH);
            }

            PerformLayout();

            // レイアウト後にグリッド実高さが不足していれば補正（計算誤差・DPI ずれの保険）
            var gridDeficit = idealGridHeight - entryGrid.ClientSize.Height;
            if (gridDeficit > 0)
            {
                var grown = Math.Min(ClientSize.Height + gridDeficit, maxClientH);
                if (grown != ClientSize.Height)
                {
                    ClientSize = new Size(ClientSize.Width, grown);
                    PerformLayout();
                }
            }

            // 画面クランプ以外でも、グリッドが足りなければ縦スクロールを出す
            var needsVerticalScroll = entryGrid.ClientSize.Height < idealGridHeight;
            entryGrid.ScrollBars = needsVerticalScroll ? ScrollBars.Vertical : ScrollBars.None;
            if (needsVerticalScroll)
            {
                var withScroll = Math.Min(
                    Math.Max(ClientSize.Width, columnsWidth + gridChrome + SystemInformation.VerticalScrollBarWidth),
                    maxClientW);
                if (withScroll != ClientSize.Width)
                {
                    ClientSize = new Size(withScroll, ClientSize.Height);
                }
            }

            EnsureOnScreen();

            // 幅確定後に注意文を折り返す
            taskSchedulerNoteLabel.MaximumSize = new Size(
                Math.Max(LogicalToDeviceUnits(200), ClientSize.Width - (spacing * 4)),
                0);
        }
        finally
        {
            _fittingWindow = false;
        }
    }

    private int MeasureGridRowHeight()
    {
        var rowHeight = entryGrid.RowTemplate.Height;
        foreach (DataGridViewRow row in entryGrid.Rows)
        {
            if (row.Height > rowHeight)
            {
                rowHeight = row.Height;
            }
        }

        return Math.Max(1, rowHeight);
    }

    private int GetGridChromeWidth()
    {
        return entryGrid.BorderStyle switch
        {
            BorderStyle.Fixed3D => SystemInformation.Border3DSize.Width * 2,
            BorderStyle.FixedSingle => SystemInformation.BorderSize.Width * 2,
            _ => 2,
        };
    }

    private int GetGridChromeHeight()
    {
        // 下端の罫線・DPI 丸め誤差用の余裕
        return entryGrid.BorderStyle switch
        {
            BorderStyle.Fixed3D => SystemInformation.Border3DSize.Height * 2,
            BorderStyle.FixedSingle => SystemInformation.BorderSize.Height * 2,
            _ => LogicalToDeviceUnits(2),
        };
    }
}
