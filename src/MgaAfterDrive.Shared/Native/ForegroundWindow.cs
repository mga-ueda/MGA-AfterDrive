using System.Runtime.InteropServices;

namespace MgaAfterDrive.Native;

/// <summary>
/// ウィンドウをフォアグラウンドにする。NotifyIcon の ContextMenuStrip で
/// キーボード（ニーモニック）を効かせるために使う。
/// </summary>
public static class ForegroundWindow
{
    public static void Activate(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var foreground = GetForegroundWindow();
        if (foreground == hwnd)
        {
            return;
        }

        var foreThread = GetWindowThreadProcessId(foreground, IntPtr.Zero);
        var appThread = GetCurrentThreadId();
        if (foreThread != appThread)
        {
            // トレイクリックは explorer 側がフォアグラウンドのため、そのままでは拒否される
            AttachThreadInput(foreThread, appThread, true);
            try
            {
                SetForegroundWindow(hwnd);
            }
            finally
            {
                AttachThreadInput(foreThread, appThread, false);
            }
        }
        else
        {
            SetForegroundWindow(hwnd);
        }
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr processId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
}
