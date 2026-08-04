using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using MgaAfterDrive.Dialogs;
using MgaAfterDrive.IO;
using MgaAfterDrive.Windows;

namespace MgaAfterDrive;

public partial class SettingWindow : AppWindow
{
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
        OperationPause.SetSettingOpen(true);
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
}
