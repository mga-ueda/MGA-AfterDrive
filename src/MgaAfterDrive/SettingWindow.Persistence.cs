using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MgaAfterDrive.Dialogs;
using MgaAfterDrive.IO;

namespace MgaAfterDrive;

public partial class SettingWindow
{
    private static readonly Regex DigitsOnly = new("^[0-9]+$", RegexOptions.Compiled);

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
                    DelayEntryRestartPolicy.ApplyFromPathChange(entry);
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
        => e.Handled = !DigitsOnly.IsMatch(e.Text);

    private void MaxWaitTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        if (!e.DataObject.GetDataPresent(System.Windows.DataFormats.Text))
        {
            e.CancelCommand();
            return;
        }

        var text = e.DataObject.GetData(System.Windows.DataFormats.Text) as string ?? string.Empty;
        if (!DigitsOnly.IsMatch(text))
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
}
