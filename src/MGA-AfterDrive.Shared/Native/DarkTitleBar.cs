using System.Runtime.InteropServices;

namespace MGA_AfterDrive.Native;

/// <summary>
/// Windows のタイトルバーをダークモードにする。
/// </summary>
internal static class DarkTitleBar
{
    private const int DwmwaUseImmersiveDarkModeBefore20H1 = 19;
    private const int DwmwaUseImmersiveDarkMode = 20;

    public static void Apply(Form form)
    {
        ArgumentNullException.ThrowIfNull(form);

        if (!form.IsHandleCreated)
        {
            return;
        }

        var enabled = 1;
        if (DwmSetWindowAttribute(form.Handle, DwmwaUseImmersiveDarkMode, ref enabled, sizeof(int)) != 0)
        {
            DwmSetWindowAttribute(form.Handle, DwmwaUseImmersiveDarkModeBefore20H1, ref enabled, sizeof(int));
        }
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int attribute,
        ref int attributeValue,
        int attributeSize);
}
