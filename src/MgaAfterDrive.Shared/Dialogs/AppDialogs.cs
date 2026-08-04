using System.Windows;

namespace MgaAfterDrive.Dialogs;

/// <summary>
/// アプリ共通の MessageBox ヘルパー。caption は呼び出し側の製品名を渡す。
/// </summary>
public static class AppDialogs
{
    public static void Info(Window? owner, string caption, string text)
        => Show(owner, text, caption, MessageBoxButton.OK, MessageBoxImage.Information);

    public static void Warn(Window? owner, string caption, string text)
        => Show(owner, text, caption, MessageBoxButton.OK, MessageBoxImage.Warning);

    public static void Error(Window? owner, string caption, string text)
        => Show(owner, text, caption, MessageBoxButton.OK, MessageBoxImage.Error);

    public static bool Confirm(
        Window? owner,
        string caption,
        string text,
        MessageBoxResult defaultResult = MessageBoxResult.No)
    {
        var result = Show(
            owner,
            text,
            caption,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            defaultResult);
        return result == MessageBoxResult.Yes;
    }

    /// <summary>
    /// Yes/No の確認。既定は No。情報・質問など Warning 以外の用途向け。
    /// </summary>
    public static bool AskYesNo(
        Window? owner,
        string caption,
        string text,
        MessageBoxImage icon = MessageBoxImage.Question,
        MessageBoxResult defaultResult = MessageBoxResult.No)
    {
        var result = Show(
            owner,
            text,
            caption,
            MessageBoxButton.YesNo,
            icon,
            defaultResult);
        return result == MessageBoxResult.Yes;
    }

    private static MessageBoxResult Show(
        Window? owner,
        string text,
        string caption,
        MessageBoxButton button,
        MessageBoxImage icon,
        MessageBoxResult defaultResult = MessageBoxResult.None)
    {
        if (owner is null)
        {
            return defaultResult == MessageBoxResult.None
                ? MessageBox.Show(text, caption, button, icon)
                : MessageBox.Show(text, caption, button, icon, defaultResult);
        }

        return defaultResult == MessageBoxResult.None
            ? MessageBox.Show(owner, text, caption, button, icon)
            : MessageBox.Show(owner, text, caption, button, icon, defaultResult);
    }
}
