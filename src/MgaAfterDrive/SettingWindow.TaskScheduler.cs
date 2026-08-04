using System.Windows;
using MgaAfterDrive.Dialogs;
using MgaAfterDrive.IO;

namespace MgaAfterDrive;

public partial class SettingWindow
{
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
}
