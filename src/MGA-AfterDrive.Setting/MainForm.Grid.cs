using System.ComponentModel;
using MGA_AfterDrive.Forms;
using MGA_AfterDrive.IO;
using MGA_AfterDrive.Setting.IO;
using MGA_AfterDrive.Setting.Models;
using MGA_AfterDrive.Theme;

namespace MGA_AfterDrive.Setting;

public partial class MainForm
{
    private void ApplyDefaultSort()
    {
        _userSortProperty = null;
        _userSortDirection = ListSortDirection.Ascending;

        var sorted = _entries
            .OrderBy(entry => entry.Delay)
            .ThenBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var wasLoading = _isLoading;
        _isLoading = true;
        try
        {
            ReplaceEntries(sorted);
        }
        finally
        {
            _isLoading = wasLoading;
        }
    }

    private void EntryGrid_ColumnHeaderMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.ColumnIndex < 0)
        {
            return;
        }

        var propertyName = entryGrid.Columns[e.ColumnIndex].DataPropertyName;
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return;
        }

        if (string.Equals(_userSortProperty, propertyName, StringComparison.Ordinal))
        {
            _userSortDirection = _userSortDirection == ListSortDirection.Ascending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;
        }
        else
        {
            _userSortProperty = propertyName;
            _userSortDirection = ListSortDirection.Ascending;
        }

        ApplyUserSort();
    }

    private void ApplyUserSort()
    {
        if (string.IsNullOrWhiteSpace(_userSortProperty))
        {
            ApplyDefaultSort();
            return;
        }

        IEnumerable<DelayEntry> query = _userSortProperty switch
        {
            nameof(DelayEntry.Delay) => _userSortDirection == ListSortDirection.Ascending
                ? _entries.OrderBy(entry => entry.Delay).ThenBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase)
                : _entries.OrderByDescending(entry => entry.Delay).ThenByDescending(entry => entry.Path, StringComparer.OrdinalIgnoreCase),
            nameof(DelayEntry.FileName) => _userSortDirection == ListSortDirection.Ascending
                ? _entries.OrderBy(entry => entry.FileName, StringComparer.OrdinalIgnoreCase)
                : _entries.OrderByDescending(entry => entry.FileName, StringComparer.OrdinalIgnoreCase),
            nameof(DelayEntry.Path) => _userSortDirection == ListSortDirection.Ascending
                ? _entries.OrderBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase)
                : _entries.OrderByDescending(entry => entry.Path, StringComparer.OrdinalIgnoreCase),
            nameof(DelayEntry.Option) => _userSortDirection == ListSortDirection.Ascending
                ? _entries.OrderBy(entry => entry.Option, StringComparer.OrdinalIgnoreCase)
                : _entries.OrderByDescending(entry => entry.Option, StringComparer.OrdinalIgnoreCase),
            nameof(DelayEntry.Restart) => _userSortDirection == ListSortDirection.Ascending
                ? _entries.OrderBy(entry => entry.Restart).ThenBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase)
                : _entries.OrderByDescending(entry => entry.Restart).ThenByDescending(entry => entry.Path, StringComparer.OrdinalIgnoreCase),
            _ => _entries,
        };

        var wasLoading = _isLoading;
        _isLoading = true;
        try
        {
            ReplaceEntries(query.ToList());
        }
        finally
        {
            _isLoading = wasLoading;
        }
    }

    private void ReplaceEntries(IReadOnlyList<DelayEntry> sorted)
    {
        _entries.RaiseListChangedEvents = false;
        try
        {
            _entries.Clear();
            foreach (var entry in sorted)
            {
                _entries.Add(entry);
            }
        }
        finally
        {
            _entries.RaiseListChangedEvents = true;
            _entries.ResetBindings();
        }

        entryGrid.ClearSelection();
        UpdateSortGlyphs();
        OptimizeColumnWidths();
    }

    private void UpdateSortGlyphs()
    {
        // 既定ソートは Delay → Path。矢印は主キー列に付ける。
        // SortGlyphDirection は DGV 本体の描画と二重になるため使わず、自前描画のみにする。
        foreach (DataGridViewColumn column in entryGrid.Columns)
        {
            column.HeaderText = column.DataPropertyName switch
            {
                nameof(DelayEntry.Delay) => "Delay",
                nameof(DelayEntry.FileName) => "File Name",
                nameof(DelayEntry.Path) => "Path",
                nameof(DelayEntry.Option) => "Option",
                nameof(DelayEntry.Restart) => "Restart",
                _ => column.DataPropertyName,
            };

            column.HeaderCell.SortGlyphDirection = SortOrder.None;
        }

        entryGrid.Invalidate();
    }

    private SortOrder GetSortDirectionForColumn(int columnIndex)
    {
        if (columnIndex < 0 || columnIndex >= entryGrid.Columns.Count)
        {
            return SortOrder.None;
        }

        var activeProperty = _userSortProperty ?? nameof(DelayEntry.Delay);
        var propertyName = entryGrid.Columns[columnIndex].DataPropertyName;
        if (!string.Equals(propertyName, activeProperty, StringComparison.Ordinal))
        {
            return SortOrder.None;
        }

        return _userSortDirection == ListSortDirection.Ascending
            ? SortOrder.Ascending
            : SortOrder.Descending;
    }

    private static readonly TextFormatFlags HeaderTextFormat =
        TextFormatFlags.HorizontalCenter
        | TextFormatFlags.VerticalCenter
        | TextFormatFlags.NoPadding
        | TextFormatFlags.NoPrefix
        | TextFormatFlags.SingleLine;

    private void EntryGrid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex != -1 || e.ColumnIndex < 0 || e.Graphics is null || e.CellStyle is null)
        {
            return;
        }

        var direction = GetSortDirectionForColumn(e.ColumnIndex);
        var sidePadding = LogicalToDeviceUnits(6);

        e.Paint(
            e.CellBounds,
            DataGridViewPaintParts.Background
            | DataGridViewPaintParts.Border
            | DataGridViewPaintParts.ContentBackground);

        var text = entryGrid.Columns[e.ColumnIndex].HeaderText;
        // テキストはセル全体に描画し、矢印は右端に重ねる（幅不足で "Del..." にしない）
        var textBounds = new Rectangle(
            e.CellBounds.X + sidePadding,
            e.CellBounds.Y,
            Math.Max(0, e.CellBounds.Width - (sidePadding * 2)),
            e.CellBounds.Height);

        TextRenderer.DrawText(
            e.Graphics,
            text,
            e.CellStyle.Font ?? entryGrid.Font,
            textBounds,
            e.CellStyle.ForeColor,
            HeaderTextFormat);

        if (direction != SortOrder.None)
        {
            DrawSortGlyph(e.Graphics, e.CellBounds, direction);
        }

        e.Handled = true;
    }

    private void DrawSortGlyph(Graphics graphics, Rectangle bounds, SortOrder direction)
    {
        var width = LogicalToDeviceUnits(10);
        var height = LogicalToDeviceUnits(6);
        var margin = LogicalToDeviceUnits(4);
        var x = bounds.Right - width - margin;
        var y = bounds.Top + ((bounds.Height - height) / 2);

        Point[] points = direction == SortOrder.Ascending
            ? [new Point(x, y + height), new Point(x + width, y + height), new Point(x + (width / 2), y)]
            : [new Point(x, y), new Point(x + width, y), new Point(x + (width / 2), y + height)];

        using var brush = new SolidBrush(AppTheme.ForegroundMuted);
        graphics.FillPolygon(brush, points);
    }

    /// <summary>
    /// ヘッダー右端のソート矢印＋余白。列幅計算と描画で共有する。
    /// </summary>
    private int GetSortGlyphReserveWidth()
        => LogicalToDeviceUnits(10) + LogicalToDeviceUnits(4);

    private int MeasureHeaderMinWidth(string headerText, Font font, bool includeSortGlyph)
    {
        var textWidth = TextRenderer.MeasureText(
            headerText,
            font,
            Size.Empty,
            HeaderTextFormat).Width;
        var sidePadding = LogicalToDeviceUnits(6) * 2;
        var glyphReserve = includeSortGlyph ? GetSortGlyphReserveWidth() : 0;
        // 罫線・丸めの余裕
        return textWidth + sidePadding + glyphReserve + LogicalToDeviceUnits(4);
    }

    private void OptimizeColumnWidths()
    {
        if (entryGrid.ColumnCount == 0)
        {
            return;
        }

        var mode = entryGrid.Rows.Count == 0
            ? DataGridViewAutoSizeColumnsMode.ColumnHeader
            : DataGridViewAutoSizeColumnsMode.AllCells;

        entryGrid.AutoResizeColumns(mode);

        // AutoResize は自前ソート矢印分を見ない。既定ソートの Delay などでヘッダーが欠けるのを防ぐ。
        // どの列でもソートしうるので、矢印分を常に最低幅へ含める。
        var headerFont = entryGrid.ColumnHeadersDefaultCellStyle.Font ?? entryGrid.Font;
        foreach (DataGridViewColumn column in entryGrid.Columns)
        {
            var minHeaderWidth = MeasureHeaderMinWidth(column.HeaderText, headerFont, includeSortGlyph: true);
            column.MinimumWidth = Math.Max(column.MinimumWidth, minHeaderWidth);
            if (column.Width < minHeaderWidth)
            {
                column.Width = minHeaderWidth;
            }
        }

        FitWindowToContent();
    }

    private void EntryGrid_DragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
        {
            e.Effect = DragDropEffects.Copy;
            return;
        }

        e.Effect = DragDropEffects.None;
    }

    private void EntryGrid_DragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is not string[] paths || paths.Length == 0)
        {
            return;
        }

        var rejected = 0;
        var added = 0;

        foreach (var path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!ExecutableFileFilter.IsExecutable(path))
            {
                rejected++;
                continue;
            }

            if (!PathUtil.TryNormalize(path, out var fullPath))
            {
                rejected++;
                continue;
            }

            if (_entries.Any(entry => string.Equals(entry.Path, fullPath, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var entry = new DelayEntry
            {
                Delay = 0,
                FileName = Path.GetFileName(fullPath),
                Path = fullPath,
                Option = string.Empty,
            };
            ApplyRestartFromPath(entry);
            _entries.Add(entry);
            added++;
        }

        if (added > 0)
        {
            if (_userSortProperty is null)
            {
                ApplyDefaultSort();
            }
            else
            {
                ApplyUserSort();
            }
        }
        else
        {
            OptimizeColumnWidths();
        }

        if (rejected > 0)
        {
            AppDialogs.Info(
                this,
                AppInfo.ProductName,
                $"実行ファイルではないため、{rejected} 件をスキップしました。");
        }
    }

    private void EntryGrid_EditingControlShowing(object? sender, DataGridViewEditingControlShowingEventArgs e)
    {
        if (entryGrid.CurrentCell?.OwningColumn != delayColumn)
        {
            return;
        }

        if (e.Control is not TextBox textBox)
        {
            return;
        }

        textBox.TextAlign = HorizontalAlignment.Center;

        // 表示直後にキャレットが末尾へ動くため、次フレームで全選択する
        BeginInvoke(() =>
        {
            if (!textBox.IsDisposed && textBox.Focused)
            {
                textBox.SelectAll();
            }
        });
    }

    private void EntryGrid_CellBeginEdit(object? sender, DataGridViewCellCancelEventArgs e)
    {
        _pathBeforeEdit = null;
        if (e.RowIndex < 0 || e.RowIndex >= _entries.Count || e.ColumnIndex != pathColumn.Index)
        {
            return;
        }

        _pathBeforeEdit = _entries[e.RowIndex].Path;
    }

    private void EntryGrid_CellEndEdit(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _entries.Count)
        {
            return;
        }

        var entry = _entries[e.RowIndex];
        if (e.ColumnIndex == pathColumn.Index && !string.IsNullOrWhiteSpace(entry.Path))
        {
            var restartBefore = entry.Restart;
            var wasUnderDrive = GoogleDriveLocator.IsPathUnderGoogleDrive(_pathBeforeEdit);
            if (PathUtil.TryNormalize(entry.Path, out var fullPath))
            {
                entry.Path = fullPath;
                entry.FileName = Path.GetFileName(fullPath);
            }
            // 不正パスはそのまま残し、保存時や Test Run で検出する

            ApplyRestartFromPath(entry, wasUnderDrive);
            if (entry.Restart != restartBefore)
            {
                SetDirty(true);
            }
        }

        _pathBeforeEdit = null;
        OptimizeColumnWidths();
    }

    /// <summary>
    /// Path が Google Drive マウント配下なら Restart を自動 ON。
    /// Drive 配下から外へ変わったときは OFF に戻す（ローカル手動 ON は維持）。
    /// </summary>
    private static void ApplyRestartFromPath(DelayEntry entry, bool wasUnderDrive = false)
    {
        var underDrive = GoogleDriveLocator.IsPathUnderGoogleDrive(entry.Path);
        if (underDrive)
        {
            entry.Restart = true;
        }
        else if (wasUnderDrive)
        {
            entry.Restart = false;
        }
    }

    private void EntryGrid_CurrentCellDirtyStateChanged(object? sender, EventArgs e)
    {
        if (entryGrid.IsCurrentCellDirty && entryGrid.CurrentCell is DataGridViewCheckBoxCell)
        {
            entryGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }
    }

    private void EntryGrid_DataError(object? sender, DataGridViewDataErrorEventArgs e)
    {
        e.ThrowException = false;
        AppDialogs.Warn(
            this,
            AppInfo.ProductName,
            $"値が不正です。{Environment.NewLine}{e.Exception?.Message}");
    }

    private void GridContextMenu_Opening(object? sender, CancelEventArgs e)
    {
        var hasSelection = entryGrid.SelectedRows.Count > 0 || entryGrid.CurrentRow is not null;
        testRunMenuItem.Enabled = hasSelection;
        deleteMenuItem.Enabled = hasSelection;
    }

    private void DeleteMenuItem_Click(object? sender, EventArgs e)
    {
        var selected = GetSelectedEntries();
        if (selected.Count == 0)
        {
            return;
        }

        foreach (var entry in selected)
        {
            _entries.Remove(entry);
        }

        OptimizeColumnWidths();
    }
}
