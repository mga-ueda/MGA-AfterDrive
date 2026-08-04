using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MgaAfterDrive.Forms;
using MgaAfterDrive.IO;
using MgaAfterDrive.Theme;
using MgaAfterDrive.Windows;

namespace MgaAfterDrive;

public partial class SettingWindow : AppWindow
{
    private const int MinVisibleRows = 5;
    private readonly ObservableCollection<DelayEntry> _entries = [];
    private string? _userSortProperty;
    private ListSortDirection _userSortDirection = ListSortDirection.Ascending;
    private int _testRunWaitCount;
    private bool _isDirty;
    private bool _isLoading;
    private bool _allowCloseWithoutPrompt;
    private bool _fittingWindow;
    private bool _legacyMissingRestartProperty;
    private string? _pathBeforeEdit;
    private bool _taskSchedulerRegistered;

    public SettingWindow()
    {
        InitializeComponent();
        // AppWindow の遅延表示を使わない（Acrylic 用）。確実に可視化する。
        Opacity = 1;
        ShowInTaskbar = true;
        Title = $"{AppInfo.ProductName} Setting - Version {AppInfo.Version}";
        EntryGrid.ItemsSource = _entries;
        _entries.CollectionChanged += Entries_CollectionChanged;
        Loaded += SettingWindow_Loaded;
        ContentRendered += (_, _) =>
        {
            Opacity = 1;
            Activate();
        };
    }

    protected override bool PersistWindowBounds => false;

    /// <summary>
    /// Setting は Acrylic ではないため、Opacity=0 の遅延表示を使わない。
    /// </summary>
    protected override bool UseDeferredReveal => false;

    private void SettingWindow_Loaded(object sender, RoutedEventArgs e)
    {
        LoadSettingsAndEntries();
        RefreshTaskSchedulerButtonState();
        UpdateActionButtonAppearances();
        FitWindowToContent();
        CenterOnPrimaryDisplay();
    }

    private void SettingWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (!_allowCloseWithoutPrompt && _isDirty && !ConfirmDiscardChanges())
        {
            e.Cancel = true;
        }
    }

    private void SettingWindow_Closed(object? sender, EventArgs e)
        => OperationPause.SetSettingOpen(false);

    private void LoadSettingsAndEntries()
    {
        _isLoading = true;
        try
        {
            var settings = AppSettingsStore.Load();
            MaxWaitTextBox.Text = AppSettings.ClampMaxWaitSeconds(settings.MaxWaitSeconds).ToString();
            StartMinimizedCheckBox.IsChecked = settings.StartMinimizedToTray;

            _entries.Clear();
            var loaded = DelayEntryStore.Load(out var missingRestartProperty, out var migratedDriveRestart);
            _legacyMissingRestartProperty = missingRestartProperty;
            foreach (var entry in loaded)
            {
                AttachEntry(entry);
                _entries.Add(entry);
            }

            ApplyDefaultSort();
            SetDirty(migratedDriveRestart);
        }
        catch (Exception ex)
        {
            AppDialogs.Error(this, AppInfo.ProductName, $"設定の読み込みに失敗しました。{Environment.NewLine}{ex.Message}");
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void AttachEntry(DelayEntry entry)
    {
        entry.PropertyChanged -= Entry_PropertyChanged;
        entry.PropertyChanged += Entry_PropertyChanged;
    }

    private void DetachEntry(DelayEntry entry)
        => entry.PropertyChanged -= Entry_PropertyChanged;

    private void Entries_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (DelayEntry entry in e.OldItems)
            {
                DetachEntry(entry);
            }
        }

        if (e.NewItems is not null)
        {
            foreach (DelayEntry entry in e.NewItems)
            {
                AttachEntry(entry);
            }
        }

        if (!_isLoading
            && e.Action is NotifyCollectionChangedAction.Add
                or NotifyCollectionChangedAction.Remove
                or NotifyCollectionChangedAction.Replace
                or NotifyCollectionChangedAction.Reset)
        {
            SetDirty(true);
        }

        UpdateActionButtonAppearances();
        FitWindowToContent();
    }

    private void Entry_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_isLoading)
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
        if (StartAllButton is null || SaveButton is null || CancelButton is null || TaskSchedulerButton is null)
        {
            return;
        }

        var hasEntries = _entries.Count > 0;
        StartAllButton.IsEnabled = hasEntries;
        StartAllButton.Style = FindStyle(hasEntries ? "WarningButton" : "DisabledButton");

        SaveButton.IsEnabled = _isDirty;
        SaveButton.Style = FindStyle(_isDirty ? "DangerButton" : "DisabledButton");

        CancelButton.Style = FindStyle("AccentButton");
        UpdateTaskSchedulerButtonAppearance();
    }

    private Style? FindStyle(string key)
    {
        if (TryFindResource(key) is Style style)
        {
            return style;
        }

        return System.Windows.Application.Current?.TryFindResource(key) as Style;
    }

    private bool ConfirmDiscardChanges()
        => AppDialogs.Confirm(this, AppInfo.ProductName, "保存していない変更を破棄しますか？");

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        EntryGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        EntryGrid.CommitEdit(DataGridEditingUnit.Row, true);

        if (!TryValidateEntries(out var error))
        {
            AppDialogs.Warn(this, AppInfo.ProductName, error);
            return;
        }

        if (!TryReadMaxWaitSeconds(out var maxWaitSeconds, out var maxWaitError))
        {
            AppDialogs.Warn(this, AppInfo.ProductName, maxWaitError);
            MaxWaitTextBox.Focus();
            MaxWaitTextBox.SelectAll();
            return;
        }

        try
        {
            AppSettingsStore.Save(new AppSettings
            {
                MaxWaitSeconds = maxWaitSeconds,
                StartMinimizedToTray = StartMinimizedCheckBox.IsChecked == true,
            });

            if (_legacyMissingRestartProperty)
            {
                foreach (var entry in _entries)
                {
                    ApplyRestartFromPath(entry);
                }
            }

            DelayEntryStore.Save(_entries);
            _legacyMissingRestartProperty = false;
            SetDirty(false);
            AppDialogs.Info(this, AppInfo.ProductName, "保存しました。");
        }
        catch (Exception ex)
        {
            AppDialogs.Error(
                this,
                AppInfo.ProductName,
                $"保存に失敗しました。{Environment.NewLine}{ex.Message}");
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isDirty && !ConfirmDiscardChanges())
        {
            return;
        }

        _allowCloseWithoutPrompt = true;
        Close();
    }

    private void MaxWaitTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isLoading)
        {
            SetDirty(true);
        }
    }

    private void StartMinimizedCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isLoading)
        {
            SetDirty(true);
        }
    }

    private void MaxWaitTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        => e.Handled = !Regex.IsMatch(e.Text, "^[0-9]+$");

    private void MaxWaitTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        if (!e.DataObject.GetDataPresent(System.Windows.DataFormats.Text))
        {
            e.CancelCommand();
            return;
        }

        var text = e.DataObject.GetData(System.Windows.DataFormats.Text) as string ?? string.Empty;
        if (!Regex.IsMatch(text, "^[0-9]+$"))
        {
            e.CancelCommand();
        }
    }

    private bool TryReadMaxWaitSeconds(out int seconds, out string error)
    {
        seconds = AppSettings.DefaultMaxWaitSeconds;
        error = string.Empty;

        if (!int.TryParse(MaxWaitTextBox.Text.Trim(), out var value))
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

    private void RefreshTaskSchedulerButtonState()
    {
        _taskSchedulerRegistered = AfterDriveTaskScheduler.IsRegistered();
        UpdateTaskSchedulerButtonAppearance();
    }

    private void UpdateTaskSchedulerButtonAppearance()
    {
        if (TaskSchedulerButton is null)
        {
            return;
        }

        if (_taskSchedulerRegistered)
        {
            TaskSchedulerButton.Content = "タスク スケジューラから削除";
            TaskSchedulerButton.Style = FindStyle("DangerButton");
        }
        else
        {
            TaskSchedulerButton.Content = "タスク スケジューラに登録";
            TaskSchedulerButton.Style = FindStyle("AccentButton");
        }
    }

    private void TaskSchedulerButton_Click(object sender, RoutedEventArgs e)
    {
        if (_taskSchedulerRegistered)
        {
            if (!AfterDriveTaskScheduler.TryUnregister(out var unregisterError))
            {
                AppDialogs.Error(this, AppInfo.ProductName, unregisterError);
                return;
            }

            _taskSchedulerRegistered = false;
            UpdateTaskSchedulerButtonAppearance();
            AppDialogs.Info(this, AppInfo.ProductName, "タスク スケジューラから削除しました。");
            return;
        }

        var exePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
        {
            AppDialogs.Warn(this, AppInfo.ProductName, "現在の実行ファイルの場所を特定できませんでした。");
            return;
        }

        if (!AfterDriveTaskScheduler.TryRegister(exePath, out var registerError))
        {
            AppDialogs.Error(this, AppInfo.ProductName, registerError);
            return;
        }

        _taskSchedulerRegistered = true;
        UpdateTaskSchedulerButtonAppearance();
        AppDialogs.Info(
            this,
            AppInfo.ProductName,
            $"タスク スケジューラに登録しました。{Environment.NewLine}{Environment.NewLine}"
            + $"名前: {AfterDriveTaskScheduler.TaskName}{Environment.NewLine}"
            + $"起動: {exePath}{Environment.NewLine}{Environment.NewLine}"
            + "登録後は EXE を移動しないでください。");
    }

    private void ApplyDefaultSort()
    {
        _userSortProperty = null;
        _userSortDirection = ListSortDirection.Ascending;

        var sorted = _entries
            .OrderBy(entry => entry.Delay)
            .ThenBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        ReplaceEntries(sorted);
    }

    private void EntryGrid_Sorting(object sender, DataGridSortingEventArgs e)
    {
        e.Handled = true;
        var propertyName = e.Column.SortMemberPath;
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            propertyName = e.Column.Header?.ToString() switch
            {
                "Delay" => nameof(DelayEntry.Delay),
                "File Name" => nameof(DelayEntry.FileName),
                "Path" => nameof(DelayEntry.Path),
                "Option" => nameof(DelayEntry.Option),
                "Restart" => nameof(DelayEntry.Restart),
                _ => null,
            };
        }

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

        e.Column.SortDirection = _userSortDirection;
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

        ReplaceEntries(query.ToList());
    }

    private void ReplaceEntries(IReadOnlyList<DelayEntry> sorted)
    {
        var wasLoading = _isLoading;
        _isLoading = true;
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
            _isLoading = wasLoading;
        }
    }

    private void Window_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)
            ? System.Windows.DragDropEffects.Copy
            : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetData(System.Windows.DataFormats.FileDrop) is not string[] paths || paths.Length == 0)
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
                FileName = System.IO.Path.GetFileName(fullPath),
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
            FitWindowToContent();
        }

        if (rejected > 0)
        {
            AppDialogs.Info(
                this,
                AppInfo.ProductName,
                $"実行ファイルではないため、{rejected} 件をスキップしました。");
        }
    }

    private void EntryGrid_PreparingCellForEdit(object? sender, DataGridPreparingCellForEditEventArgs e)
    {
        _pathBeforeEdit = null;
        if (e.Row.Item is DelayEntry entry && e.Column.Header?.ToString() == "Path")
        {
            _pathBeforeEdit = entry.Path;
        }
    }

    private void EntryGrid_CellEditEnding(object? sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.EditAction != DataGridEditAction.Commit || e.Row.Item is not DelayEntry entry)
        {
            return;
        }

        if (e.Column.Header?.ToString() == "Path" && !string.IsNullOrWhiteSpace(entry.Path))
        {
            var restartBefore = entry.Restart;
            var wasUnderDrive = GoogleDriveLocator.IsPathUnderGoogleDrive(_pathBeforeEdit);
            if (PathUtil.TryNormalize(entry.Path, out var fullPath))
            {
                entry.Path = fullPath;
                entry.FileName = System.IO.Path.GetFileName(fullPath);
            }

            ApplyRestartFromPath(entry, wasUnderDrive);
            if (entry.Restart != restartBefore)
            {
                SetDirty(true);
            }
        }

        _pathBeforeEdit = null;
        Dispatcher.BeginInvoke(FitWindowToContent);
    }

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

    private void EntryGrid_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Delete)
        {
            DeleteSelectedEntries();
            e.Handled = true;
        }
    }

    private void GridContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        var hasSelection = GetSelectedEntries().Count > 0;
        TestRunMenuItem.IsEnabled = hasSelection;
        DeleteMenuItem.IsEnabled = hasSelection;
    }

    private void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
        => DeleteSelectedEntries();

    private void DeleteSelectedEntries()
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

        FitWindowToContent();
    }

    private async void TestRunMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var skipped = 0;
        foreach (var entry in GetSelectedEntries())
        {
            if (await TryTestRunAsync(entry) == TestRunOutcome.SkippedAlreadyRunning)
            {
                skipped++;
            }
        }

        NotifySkippedAlreadyRunning(skipped);
    }

    private async void StartAllButton_Click(object sender, RoutedEventArgs e)
    {
        EntryGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        EntryGrid.CommitEdit(DataGridEditingUnit.Row, true);

        if (_entries.Count == 0)
        {
            return;
        }

        if (!TryValidateEntries(out var error))
        {
            AppDialogs.Warn(this, AppInfo.ProductName, error);
            return;
        }

        var skipped = 0;
        var tasks = _entries.ToList().Select(TryTestRunAsync).ToArray();
        var outcomes = await Task.WhenAll(tasks);
        foreach (var outcome in outcomes)
        {
            if (outcome == TestRunOutcome.SkippedAlreadyRunning)
            {
                skipped++;
            }
        }

        NotifySkippedAlreadyRunning(skipped);
    }

    private void NotifySkippedAlreadyRunning(int skippedCount)
    {
        if (skippedCount <= 0)
        {
            return;
        }

        AppDialogs.Info(
            this,
            AppInfo.ProductName,
            skippedCount == 1
                ? "起動済みのためスキップしました。"
                : $"{skippedCount} 件は起動済みのためスキップしました。");
    }

    private async Task WaitWithCountdownAsync(int delaySeconds, string fileName)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(delaySeconds);

        while (IsLoaded)
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
        void Apply()
        {
            Title = string.IsNullOrWhiteSpace(status)
                ? $"{AppInfo.ProductName} Setting - Version {AppInfo.Version}"
                : $"{AppInfo.ProductName} Setting - Version {AppInfo.Version} - {status}";
        }

        if (!Dispatcher.CheckAccess())
        {
            try
            {
                Dispatcher.BeginInvoke(Apply);
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
        var rows = EntryGrid.SelectedItems.OfType<DelayEntry>().ToList();
        if (rows.Count == 0 && EntryGrid.CurrentItem is DelayEntry current)
        {
            rows.Add(current);
        }

        return rows;
    }

    private enum TestRunOutcome
    {
        Started,
        SkippedAlreadyRunning,
        Failed,
        Cancelled,
    }

    private async Task<TestRunOutcome> TryTestRunAsync(DelayEntry entry)
    {
        var delaySeconds = Math.Max(0, entry.Delay);
        var filePath = entry.Path?.Trim() ?? string.Empty;
        var option = entry.Option ?? string.Empty;
        var fileName = string.IsNullOrWhiteSpace(entry.FileName)
            ? System.IO.Path.GetFileName(filePath)
            : entry.FileName;

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            AppDialogs.Warn(
                this,
                AppInfo.ProductName,
                $"ファイルが見つかりません。{Environment.NewLine}{filePath}");
            return TestRunOutcome.Failed;
        }

        if (!ExecutableFileFilter.IsExecutable(filePath))
        {
            AppDialogs.Warn(
                this,
                AppInfo.ProductName,
                $"実行ファイルではありません。{Environment.NewLine}{filePath}");
            return TestRunOutcome.Failed;
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

        if (!IsLoaded)
        {
            return TestRunOutcome.Cancelled;
        }

        if (ProcessExecutable.IsRunning(filePath))
        {
            return TestRunOutcome.SkippedAlreadyRunning;
        }

        if (!ProcessLaunch.TryStart(filePath, option, out var launchError))
        {
            AppDialogs.Error(
                this,
                AppInfo.ProductName,
                $"テスト実行に失敗しました。{Environment.NewLine}{launchError}");
            return TestRunOutcome.Failed;
        }

        return TestRunOutcome.Started;
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
            var visibleRows = Math.Max(MinVisibleRows, _entries.Count);
            var rowHeight = 28;
            var headerHeight = 32;
            var gridHeight = headerHeight + (rowHeight * visibleRows) + 4;

            var optionsHeight = 160;
            var buttonBarHeight = spacing + AppLayout.ButtonHeight + spacing;
            var margins = spacing * 2;

            var area = SystemParameters.WorkArea;
            var desiredHeight = Math.Min(
                optionsHeight + gridHeight + buttonBarHeight + margins + 40,
                area.Height - 40);
            var desiredWidth = Math.Min(Math.Max(Width, 720), area.Width - 40);

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
