using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MgaAfterDrive.Dialogs;
using MgaAfterDrive.IO;

namespace MgaAfterDrive;

public partial class SettingWindow
{
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
            DelayEntryRestartPolicy.ApplyFromPathChange(entry);
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

            DelayEntryRestartPolicy.ApplyFromPathChange(entry, wasUnderDrive);
            if (entry.Restart != restartBefore)
            {
                SetDirty(true);
            }
        }

        _pathBeforeEdit = null;
        Dispatcher.BeginInvoke(FitWindowToContent);
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
}
