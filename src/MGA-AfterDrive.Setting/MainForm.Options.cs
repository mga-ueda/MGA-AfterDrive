using MGA_AfterDrive.Forms;
using MGA_AfterDrive.IO;
using MGA_AfterDrive.Setting.IO;
using MGA_AfterDrive.Theme;

namespace MGA_AfterDrive.Setting;

public partial class MainForm
{
    /// <summary>
    /// 上段: [ラベル] Spacing [5文字エディタ] Spacing [秒]
    /// 中段: タスクトレイに最小化して起動（行間 Spacing）
    /// 下段: タスク スケジューラ注意文と登録／削除ボタン
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

        // 上段ラベルをエディタの垂直中央に。下段チェックは行間 Spacing（DPI 換算）
        var spacing = LogicalToDeviceUnits(AppLayout.Spacing);
        var editorHeight = maxWaitTextBox.Height;
        var labelOffset = Math.Max(0, (editorHeight - maxWaitLabel.PreferredHeight) / 2);
        var unitOffset = Math.Max(0, (editorHeight - maxWaitUnitLabel.PreferredHeight) / 2);
        maxWaitLabel.Margin = new Padding(0, labelOffset, spacing, labelOffset);
        maxWaitTextBox.Margin = new Padding(0, 0, spacing, 0);
        maxWaitUnitLabel.Margin = new Padding(0, unitOffset, 0, unitOffset);
        startMinimizedCheckBox.Margin = new Padding(0, spacing, 0, 0);

        taskSchedulerNoteLabel.ForeColor = AppTheme.ForegroundMuted;
        taskSchedulerNoteLabel.BackColor = Color.Transparent;
        taskSchedulerNoteLabel.Margin = new Padding(0, spacing, 0, 0);
        // ウィンドウ幅に合わせて折り返す（FitWindowToContent 前は仮幅）
        var noteWidth = Math.Max(
            LogicalToDeviceUnits(420),
            ClientSize.Width - (spacing * 4));
        taskSchedulerNoteLabel.MaximumSize = new Size(noteWidth, 0);

        taskSchedulerButton.Margin = new Padding(0, spacing, 0, 0);
        var registerTextWidth = TextRenderer.MeasureText(
            "タスク スケジューラに登録",
            taskSchedulerButton.Font,
            Size.Empty,
            TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width;
        var unregisterTextWidth = TextRenderer.MeasureText(
            "タスク スケジューラから削除",
            taskSchedulerButton.Font,
            Size.Empty,
            TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width;
        taskSchedulerButton.Width = Math.Max(registerTextWidth, unregisterTextWidth) + LogicalToDeviceUnits(24);
        taskSchedulerButton.Height = Math.Max(
            LogicalToDeviceUnits(AppLayout.ButtonHeight),
            taskSchedulerButton.Height);
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

    private void RefreshTaskSchedulerButtonState()
    {
        _taskSchedulerRegistered = AfterDriveTaskScheduler.IsRegistered();
        UpdateTaskSchedulerButtonAppearance();
    }

    private void UpdateTaskSchedulerButtonAppearance()
    {
        if (_taskSchedulerRegistered)
        {
            taskSchedulerButton.Text = "タスク スケジューラから削除";
            AppTheme.ApplyDangerButton(taskSchedulerButton);
        }
        else
        {
            taskSchedulerButton.Text = "タスク スケジューラに登録";
            AppTheme.ApplyAccentButton(taskSchedulerButton);
        }
    }

    private void TaskSchedulerButton_Click(object? sender, EventArgs e)
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

        if (!HostAppLocator.TryResolve(out var exePath, out var resolveError))
        {
            AppDialogs.Warn(this, AppInfo.ProductName, resolveError);
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
}
