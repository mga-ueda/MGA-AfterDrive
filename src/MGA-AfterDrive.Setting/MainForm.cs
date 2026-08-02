using System.ComponentModel;
using MGA_AfterDrive.Forms;
using MGA_AfterDrive.IO;
using MGA_AfterDrive.Setting.IO;
using MGA_AfterDrive.Setting.Models;
using MGA_AfterDrive.Theme;

namespace MGA_AfterDrive.Setting;

public partial class MainForm : AppForm
{
    private readonly BindingList<DelayEntry> _entries = [];
    private string? _userSortProperty;
    private ListSortDirection _userSortDirection = ListSortDirection.Ascending;
    private int _testRunWaitCount;
    private const int MinVisibleRows = 5;

    private bool _isDirty;
    private bool _isLoading;
    private bool _allowCloseWithoutPrompt;
    private bool _fittingWindow;
    private ToolTip? _optionsToolTip;

    public MainForm()
    {
        InitializeComponent();
        Text = AppInfo.WindowTitle;
        entryGrid.DataSource = _entries;
        entryGrid.ColumnHeaderMouseClick += EntryGrid_ColumnHeaderMouseClick;
        entryGrid.CellPainting += EntryGrid_CellPainting;
        entryGrid.EditingControlShowing += EntryGrid_EditingControlShowing;
        _entries.ListChanged += Entries_ListChanged;
    }

    protected override bool PersistWindowBounds => false;

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        LayoutMaxWaitOptions();
        FitWindowToContent();
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        ApplyContextMenuTheme();
        restartColumn.HeaderCell.ToolTipText = restartColumn.ToolTipText;
        var maxWaitTip = "Google Drive プロセス待機とアクセス確認の上限時間（秒）";
        _optionsToolTip ??= new ToolTip(components);
        _optionsToolTip.SetToolTip(maxWaitLabel, maxWaitTip);
        _optionsToolTip.SetToolTip(maxWaitTextBox, maxWaitTip);
        _optionsToolTip.SetToolTip(maxWaitUnitLabel, maxWaitTip);
        _optionsToolTip.SetToolTip(startMinimizedCheckBox, "起動時にウィンドウを出さず、タスクトレイへ格納した状態で開始します。");
        LayoutMaxWaitOptions();
        LoadSettingsAndEntries();
        UpdateActionButtonAppearances();
        FitWindowToContent();
        CenterOnPrimaryDisplay();
    }

    /// <summary>
    /// 上段: [ラベル] Spacing [5文字エディタ] Spacing [秒]
    /// 下段: タスクトレイに最小化して起動（行間 Spacing）
    /// </summary>
    private void LayoutMaxWaitOptions()
    {
        maxWaitLabel.Text = "最大待機時間";
        maxWaitUnitLabel.Text = "秒";

        maxWaitTextBox.Font = AppFonts.UI;
        var textWidth = TextRenderer.MeasureText(
            "00000",
            maxWaitTextBox.Font,
            Size.Empty,
            TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width;
        maxWaitTextBox.Width = textWidth + 8;
        // 単一行 TextBox はフォント由来の PreferredHeight を使う（無理に ButtonHeight にしない）
        maxWaitTextBox.Height = maxWaitTextBox.PreferredHeight;
        maxWaitTextBox.TextAlign = HorizontalAlignment.Center;

        startMinimizedCheckBox.ForeColor = AppTheme.Foreground;
        startMinimizedCheckBox.BackColor = Color.Transparent;

        // 上段ラベルをエディタの垂直中央に。下段チェックは行間 Spacing
        var editorHeight = maxWaitTextBox.Height;
        var labelOffset = Math.Max(0, (editorHeight - maxWaitLabel.PreferredHeight) / 2);
        var unitOffset = Math.Max(0, (editorHeight - maxWaitUnitLabel.PreferredHeight) / 2);
        maxWaitLabel.Margin = new Padding(0, labelOffset, AppLayout.Spacing, labelOffset);
        maxWaitTextBox.Margin = new Padding(0, 0, AppLayout.Spacing, 0);
        maxWaitUnitLabel.Margin = new Padding(0, unitOffset, 0, unitOffset);
        startMinimizedCheckBox.Margin = new Padding(0, AppLayout.Spacing, 0, 0);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_allowCloseWithoutPrompt && _isDirty && !ConfirmDiscardChanges())
        {
            e.Cancel = true;
            return;
        }

        base.OnFormClosing(e);
    }

    private void ApplyContextMenuTheme()
    {
        gridContextMenu.BackColor = AppTheme.Surface;
        gridContextMenu.ForeColor = AppTheme.Foreground;
        gridContextMenu.RenderMode = ToolStripRenderMode.System;

        foreach (ToolStripItem item in gridContextMenu.Items)
        {
            item.BackColor = AppTheme.Surface;
            item.ForeColor = AppTheme.Foreground;
        }
    }

    private void LoadSettingsAndEntries()
    {
        _isLoading = true;
        try
        {
            var settings = AppSettingsStore.Load();
            maxWaitTextBox.Text = AppSettings.ClampMaxWaitSeconds(settings.MaxWaitSeconds).ToString();
            startMinimizedCheckBox.Checked = settings.StartMinimizedToTray;

            _entries.Clear();
            foreach (var entry in DelayEntryStore.Load())
            {
                ApplyRestartFromPath(entry);
                _entries.Add(entry);
            }

            ApplyDefaultSort();
            SetDirty(false);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"設定の読み込みに失敗しました。{Environment.NewLine}{ex.Message}",
                AppInfo.ProductName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void MaxWaitTextBox_TextChanged(object? sender, EventArgs e)
    {
        if (!_isLoading)
        {
            SetDirty(true);
        }
    }

    private void StartMinimizedCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        if (!_isLoading)
        {
            SetDirty(true);
        }
    }

    private void MaxWaitTextBox_KeyPress(object? sender, KeyPressEventArgs e)
    {
        if (char.IsControl(e.KeyChar) || char.IsDigit(e.KeyChar))
        {
            return;
        }

        e.Handled = true;
    }

    private bool TryReadMaxWaitSeconds(out int seconds, out string error)
    {
        seconds = AppSettings.DefaultMaxWaitSeconds;
        error = string.Empty;

        if (!int.TryParse(maxWaitTextBox.Text.Trim(), out var value))
        {
            error = "最大待機時間は整数で入力してください。";
            return false;
        }

        if (value < AppSettings.MinMaxWaitSeconds || value > AppSettings.MaxMaxWaitSeconds)
        {
            error = $"最大待機時間は {AppSettings.MinMaxWaitSeconds}〜{AppSettings.MaxMaxWaitSeconds} の範囲で入力してください。";
            return false;
        }

        seconds = value;
        return true;
    }

    private void Entries_ListChanged(object? sender, ListChangedEventArgs e)
    {
        if (!_isLoading
            && e.ListChangedType is ListChangedType.ItemAdded
                or ListChangedType.ItemDeleted
                or ListChangedType.ItemChanged
                or ListChangedType.Reset)
        {
            SetDirty(true);
        }

        UpdateActionButtonAppearances();
    }

    private void SetDirty(bool isDirty)
    {
        _isDirty = isDirty;
        UpdateActionButtonAppearances();
    }

    private void UpdateActionButtonAppearances()
    {
        var hasEntries = _entries.Count > 0;
        startAllButton.Enabled = hasEntries;
        if (hasEntries)
        {
            AppTheme.ApplyWarningButton(startAllButton);
        }
        else
        {
            AppTheme.ApplyDisabledButton(startAllButton);
        }

        saveButton.Enabled = _isDirty;
        if (_isDirty)
        {
            AppTheme.ApplyDangerButton(saveButton);
        }
        else
        {
            AppTheme.ApplyDisabledButton(saveButton);
        }

        AppTheme.ApplyAccentButton(cancelButton);
    }

    private bool ConfirmDiscardChanges()
    {
        var result = MessageBox.Show(
            this,
            "保存していない変更を破棄しますか？",
            AppInfo.ProductName,
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);

        return result == DialogResult.Yes;
    }

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
            nameof(DelayEntry.RestartMark) => _userSortDirection == ListSortDirection.Ascending
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
                nameof(DelayEntry.RestartMark) => "Restart",
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

    private void EntryGrid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex != -1 || e.ColumnIndex < 0 || e.Graphics is null || e.CellStyle is null)
        {
            return;
        }

        var direction = GetSortDirectionForColumn(e.ColumnIndex);

        e.Paint(
            e.CellBounds,
            DataGridViewPaintParts.Background
            | DataGridViewPaintParts.Border
            | DataGridViewPaintParts.ContentBackground);

        var text = entryGrid.Columns[e.ColumnIndex].HeaderText;
        var textBounds = Rectangle.Inflate(e.CellBounds, -6, 0);
        if (direction != SortOrder.None)
        {
            textBounds.Width -= 14;
            DrawSortGlyph(e.Graphics, e.CellBounds, direction);
        }

        TextRenderer.DrawText(
            e.Graphics,
            text,
            e.CellStyle.Font ?? entryGrid.Font,
            textBounds,
            e.CellStyle.ForeColor,
            TextFormatFlags.HorizontalCenter
            | TextFormatFlags.VerticalCenter
            | TextFormatFlags.EndEllipsis);

        e.Handled = true;
    }

    private static void DrawSortGlyph(Graphics graphics, Rectangle bounds, SortOrder direction)
    {
        const int width = 10;
        const int height = 6;
        var x = bounds.Right - width - AppLayout.Spacing;
        var y = bounds.Top + ((bounds.Height - height) / 2);

        Point[] points = direction == SortOrder.Ascending
            ? [new Point(x, y + height), new Point(x + width, y + height), new Point(x + (width / 2), y)]
            : [new Point(x, y), new Point(x + width, y), new Point(x + (width / 2), y + height)];

        using var brush = new SolidBrush(AppTheme.ForegroundMuted);
        graphics.FillPolygon(brush, points);
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
        FitWindowToContent();
    }

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

            var rowHeight = entryGrid.RowTemplate.Height;
            if (entryGrid.Rows.Count > 0)
            {
                rowHeight = Math.Max(rowHeight, entryGrid.Rows[0].Height);
            }

            var visibleRows = Math.Max(MinVisibleRows, _entries.Count);
            var headerHeight = entryGrid.ColumnHeadersHeight;

            var columnsWidth = entryGrid.Columns.GetColumnsWidth(DataGridViewElementStates.Visible);
            if (entryGrid.RowHeadersVisible)
            {
                columnsWidth += entryGrid.RowHeadersWidth;
            }

            // 枠・セル余白・DPI 誤差で 1px 足りず横スクロールが出るのを防ぐ
            var gridChrome = GetGridChromeWidth() + AppLayout.Spacing;

            var buttonBarContentWidth =
                startAllButton.Width
                + saveButton.Width
                + cancelButton.Width
                + (AppLayout.Spacing * 4);

            var optionsBarHeight = Math.Max(
                optionsBar.PreferredSize.Height,
                (AppLayout.Spacing * 2)
                    + maxWaitTextBox.Height
                    + AppLayout.Spacing
                    + startMinimizedCheckBox.PreferredSize.Height);
            var buttonBarHeight = AppLayout.Spacing + AppLayout.ButtonHeight + AppLayout.Spacing;
            var idealGridHeight = headerHeight + (rowHeight * visibleRows) + GetGridChromeHeight();

            var area = Screen.FromControl(this).WorkingArea;
            var chromeW = Width - ClientSize.Width;
            var chromeH = Height - ClientSize.Height;
            var maxClientW = Math.Max(200, area.Width - chromeW);
            var maxClientH = Math.Max(150, area.Height - chromeH);

            var maxWaitRowWidth =
                maxWaitLabel.PreferredWidth
                + AppLayout.Spacing
                + maxWaitTextBox.Width
                + AppLayout.Spacing
                + maxWaitUnitLabel.PreferredWidth;
            var optionsContentWidth =
                Math.Max(maxWaitRowWidth, startMinimizedCheckBox.PreferredSize.Width)
                + (AppLayout.Spacing * 2);

            var clientW = Math.Max(
                Math.Max(columnsWidth + gridChrome, buttonBarContentWidth),
                optionsContentWidth);
            var clientH = optionsBarHeight + idealGridHeight + buttonBarHeight;

            // 画面に収まらない場合のみ縦スクロールを許可（横は出さない）
            var heightClamped = clientH > maxClientH;
            clientW = Math.Min(clientW, maxClientW);
            clientH = Math.Min(clientH, maxClientH);
            entryGrid.ScrollBars = heightClamped ? ScrollBars.Vertical : ScrollBars.None;

            if (heightClamped)
            {
                clientW = Math.Min(
                    Math.Max(clientW, columnsWidth + gridChrome + SystemInformation.VerticalScrollBarWidth),
                    maxClientW);
            }

            if (ClientSize.Width == clientW && ClientSize.Height == clientH)
            {
                return;
            }

            ClientSize = new Size(clientW, clientH);
            EnsureOnScreen();
        }
        finally
        {
            _fittingWindow = false;
        }
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
        return entryGrid.BorderStyle switch
        {
            BorderStyle.Fixed3D => SystemInformation.Border3DSize.Height * 2,
            BorderStyle.FixedSingle => SystemInformation.BorderSize.Height * 2,
            _ => 2,
        };
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

            var fullPath = Path.GetFullPath(path);
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
            MessageBox.Show(
                this,
                $"実行ファイルではないため、{rejected} 件をスキップしました。",
                AppInfo.ProductName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
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

    private void EntryGrid_CellEndEdit(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _entries.Count)
        {
            return;
        }

        var entry = _entries[e.RowIndex];
        if (e.ColumnIndex == pathColumn.Index && !string.IsNullOrWhiteSpace(entry.Path))
        {
            try
            {
                var fullPath = Path.GetFullPath(entry.Path.Trim());
                entry.Path = fullPath;
                entry.FileName = Path.GetFileName(fullPath);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                // 不正パスはそのまま残し、保存時や Test Run で検出する
            }

            ApplyRestartFromPath(entry);
        }

        OptimizeColumnWidths();
    }

    /// <summary>
    /// Path が Google Drive マウント配下なら Restart を自動 ON（列に ✓ を表示）。
    /// </summary>
    private static void ApplyRestartFromPath(DelayEntry entry)
    {
        entry.Restart = GoogleDriveLocator.IsPathUnderGoogleDrive(entry.Path);
    }

    private void EntryGrid_DataError(object? sender, DataGridViewDataErrorEventArgs e)
    {
        e.ThrowException = false;
        MessageBox.Show(
            this,
            $"値が不正です。{Environment.NewLine}{e.Exception?.Message}",
            AppInfo.ProductName,
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
    }

    private void GridContextMenu_Opening(object? sender, CancelEventArgs e)
    {
        var hasSelection = entryGrid.SelectedRows.Count > 0 || entryGrid.CurrentRow is not null;
        testRunMenuItem.Enabled = hasSelection;
        deleteMenuItem.Enabled = hasSelection;
    }

    private void TestRunMenuItem_Click(object? sender, EventArgs e)
    {
        foreach (var entry in GetSelectedEntries())
        {
            _ = TryTestRunAsync(entry);
        }
    }

    private void StartAllButton_Click(object? sender, EventArgs e)
    {
        entryGrid.EndEdit();

        if (_entries.Count == 0)
        {
            return;
        }

        if (!TryValidateEntries(out var error))
        {
            MessageBox.Show(
                this,
                error,
                AppInfo.ProductName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        foreach (var entry in _entries.ToList())
        {
            _ = TryTestRunAsync(entry);
        }
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

    private void SaveButton_Click(object? sender, EventArgs e)
    {
        entryGrid.EndEdit();

        if (!TryValidateEntries(out var error))
        {
            MessageBox.Show(
                this,
                error,
                AppInfo.ProductName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        if (!TryReadMaxWaitSeconds(out var maxWaitSeconds, out var maxWaitError))
        {
            MessageBox.Show(
                this,
                maxWaitError,
                AppInfo.ProductName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            maxWaitTextBox.Focus();
            maxWaitTextBox.SelectAll();
            return;
        }

        try
        {
            AppSettingsStore.Save(new AppSettings
            {
                MaxWaitSeconds = maxWaitSeconds,
                StartMinimizedToTray = startMinimizedCheckBox.Checked,
            });
            DelayEntryStore.Save(_entries);
            SetDirty(false);
            MessageBox.Show(
                this,
                "保存しました。",
                AppInfo.ProductName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"保存に失敗しました。{Environment.NewLine}{ex.Message}",
                AppInfo.ProductName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void CancelButton_Click(object? sender, EventArgs e)
    {
        if (_isDirty && !ConfirmDiscardChanges())
        {
            return;
        }

        _allowCloseWithoutPrompt = true;
        Close();
    }

    private async Task WaitWithCountdownAsync(int delaySeconds, string fileName)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(delaySeconds);

        while (!IsDisposed && !Disposing)
        {
            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                break;
            }

            SetTitleStatus($"Test Run {TimeDisplay.FormatCountdown(remaining)} - {fileName}");
            var delay = remaining < TimeSpan.FromMilliseconds(250)
                ? remaining
                : TimeSpan.FromMilliseconds(250);
            await Task.Delay(delay);
        }
    }

    private void SetTitleStatus(string? status)
    {
        if (IsDisposed || Disposing)
        {
            return;
        }

        void Apply()
        {
            Text = string.IsNullOrWhiteSpace(status)
                ? AppInfo.WindowTitle
                : $"{AppInfo.WindowTitle} - {status}";
        }

        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(Apply);
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }

            return;
        }

        Apply();
    }

    private IReadOnlyList<DelayEntry> GetSelectedEntries()
    {
        var rows = entryGrid.SelectedRows.Cast<DataGridViewRow>()
            .Where(row => row.DataBoundItem is DelayEntry)
            .Select(row => (DelayEntry)row.DataBoundItem!)
            .ToList();

        if (rows.Count == 0 && entryGrid.CurrentRow?.DataBoundItem is DelayEntry current)
        {
            rows.Add(current);
        }

        return rows;
    }

    private async Task TryTestRunAsync(DelayEntry entry)
    {
        // 待機中の編集影響を避けるため、開始時点の値を使う
        var delaySeconds = Math.Max(0, entry.Delay);
        var filePath = entry.Path?.Trim() ?? string.Empty;
        var option = entry.Option ?? string.Empty;
        var fileName = string.IsNullOrWhiteSpace(entry.FileName)
            ? Path.GetFileName(filePath)
            : entry.FileName;

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            MessageBox.Show(
                this,
                $"ファイルが見つかりません。{Environment.NewLine}{filePath}",
                AppInfo.ProductName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        if (!ExecutableFileFilter.IsExecutable(filePath))
        {
            MessageBox.Show(
                this,
                $"実行ファイルではありません。{Environment.NewLine}{filePath}",
                AppInfo.ProductName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        if (delaySeconds > 0)
        {
            Interlocked.Increment(ref _testRunWaitCount);
            try
            {
                await WaitWithCountdownAsync(delaySeconds, fileName);
            }
            finally
            {
                if (Interlocked.Decrement(ref _testRunWaitCount) <= 0)
                {
                    Interlocked.Exchange(ref _testRunWaitCount, 0);
                    SetTitleStatus(null);
                }
            }
        }

        if (IsDisposed || Disposing)
        {
            return;
        }

        if (!ProcessLaunch.TryStart(filePath, option, out var launchError))
        {
            MessageBox.Show(
                this,
                $"テスト実行に失敗しました。{Environment.NewLine}{launchError}",
                AppInfo.ProductName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private bool TryValidateEntries(out string error)
    {
        for (var i = 0; i < _entries.Count; i++)
        {
            var entry = _entries[i];
            if (entry.Delay < 0)
            {
                error = $"{i + 1} 行目: Delay は 0 以上（秒）で指定してください。";
                return false;
            }

            if (string.IsNullOrWhiteSpace(entry.Path))
            {
                error = $"{i + 1} 行目: Path は必須です。";
                return false;
            }

            if (!ExecutableFileFilter.IsExecutable(entry.Path))
            {
                error = $"{i + 1} 行目: Path が実行ファイルではありません。{Environment.NewLine}{entry.Path}";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }
}
