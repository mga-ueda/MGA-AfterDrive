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
    private bool _legacyMissingRestartProperty;
    private string? _pathBeforeEdit;
    private ToolTip? _optionsToolTip;
    private bool _taskSchedulerRegistered;

    public MainForm()
    {
        InitializeComponent();
        Text = AppInfo.WindowTitle;
        entryGrid.DataSource = _entries;
        entryGrid.ColumnHeaderMouseClick += EntryGrid_ColumnHeaderMouseClick;
        entryGrid.CellPainting += EntryGrid_CellPainting;
        entryGrid.CellBeginEdit += EntryGrid_CellBeginEdit;
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
        RefreshTaskSchedulerButtonState();
        UpdateActionButtonAppearances();
        FitWindowToContent();
        CenterOnPrimaryDisplay();
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
        AppTheme.ApplyContextMenu(gridContextMenu);
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
            var loaded = DelayEntryStore.Load(out var missingRestartProperty, out var migratedDriveRestart);
            _legacyMissingRestartProperty = missingRestartProperty;
            foreach (var entry in loaded)
            {
                // 通常は保存値を尊重。Restart 未定義の旧 JSON のみ Drive 配下を ON に補完する。
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
        UpdateTaskSchedulerButtonAppearance();
    }

    private bool ConfirmDiscardChanges()
        => AppDialogs.Confirm(this, AppInfo.ProductName, "保存していない変更を破棄しますか？");

    private void SaveButton_Click(object? sender, EventArgs e)
    {
        entryGrid.EndEdit();

        if (!TryValidateEntries(out var error))
        {
            AppDialogs.Warn(this, AppInfo.ProductName, error);
            return;
        }

        if (!TryReadMaxWaitSeconds(out var maxWaitSeconds, out var maxWaitError))
        {
            AppDialogs.Warn(this, AppInfo.ProductName, maxWaitError);
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

            // 旧 JSON（Restart 未定義）を保存する前に、マウントが使える今もう一度 Drive を ON にする
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

    private void CancelButton_Click(object? sender, EventArgs e)
    {
        if (_isDirty && !ConfirmDiscardChanges())
        {
            return;
        }

        _allowCloseWithoutPrompt = true;
        Close();
    }
}
