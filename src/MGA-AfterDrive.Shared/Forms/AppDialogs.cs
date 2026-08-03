namespace MGA_AfterDrive.Forms;

/// <summary>
/// アプリ共通の MessageBox ヘルパー。caption は呼び出し側の製品名を渡す。
/// </summary>
public static class AppDialogs
{
    public static void Info(IWin32Window? owner, string caption, string text)
        => MessageBox.Show(owner, text, caption, MessageBoxButtons.OK, MessageBoxIcon.Information);

    public static void Warn(IWin32Window? owner, string caption, string text)
        => MessageBox.Show(owner, text, caption, MessageBoxButtons.OK, MessageBoxIcon.Warning);

    public static void Error(IWin32Window? owner, string caption, string text)
        => MessageBox.Show(owner, text, caption, MessageBoxButtons.OK, MessageBoxIcon.Error);

    public static bool Confirm(
        IWin32Window? owner,
        string caption,
        string text,
        MessageBoxDefaultButton defaultButton = MessageBoxDefaultButton.Button2)
    {
        var result = MessageBox.Show(
            owner,
            text,
            caption,
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            defaultButton);
        return result == DialogResult.Yes;
    }
}
