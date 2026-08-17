using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using MgaAfterDrive.Theme;

namespace MgaAfterDrive.Native;

/// <summary>
/// ウィンドウへすりガラスを適用する。
/// ACCENT_POLICY（AcrylicBlurBehind）のみを使い、透け具合は
/// <see cref="AppTheme.AcrylicTintAlpha"/> で調整する。
/// 注意: DwmExtendFrameIntoClientArea と併用すると、Win11 では
/// フレーム拡張領域が材質なし＝真っ黒になるため呼ばないこと。
/// WPF では <see cref="Apply(Window)"/> を使い、CompositionTarget を透明にする。
/// </summary>
public static class AcrylicBackdrop
{
    private const int GwlExStyle = -20;
    private const int WsExLayered = 0x00080000;
    private const int WcaAccentPolicy = 19;
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaSystemBackdropType = 38;
    private const int DwmsbtNone = 1;
    /// <summary>Win11 標準枠と同程度の角丸。</summary>
    private const int DwmwcpRound = 2;

    private enum AccentState
    {
        AccentEnableBlurBehind = 3,
        AccentEnableAcrylicBlurBehind = 4,
    }

    public enum BlurType
    {
        Acrylic = 4,
    }

    /// <summary>
    /// WPF Window へ Acrylic を適用する。背景ブラシは呼び出し側で Transparent にしておくこと。
    /// </summary>
    public static void Apply(Window window, BlurType blurType = BlurType.Acrylic)
    {
        ArgumentNullException.ThrowIfNull(window);

        var hwnd = new WindowInteropHelper(window).EnsureHandle();
        var source = HwndSource.FromHwnd(hwnd);
        if (source?.CompositionTarget is not null)
        {
            // WPF 既定の不透明クリアをやめ、DWM Acrylic が透けるようにする
            source.CompositionTarget.BackgroundColor = Colors.Transparent;
        }

        // Opacity を先に 1 にすると、Accent 適用までの 1 フレームが真っ黒になる
        Apply(hwnd, blurType);
    }

    public static void Apply(IntPtr hwnd, BlurType blurType = BlurType.Acrylic)
    {
        _ = blurType;

        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        try
        {
            var darkMode = 1;
            _ = DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref darkMode, sizeof(int));

            // SYSTEMBACKDROP は ACCENT と競合するため明示的に無効化
            var backdrop = DwmsbtNone;
            _ = DwmSetWindowAttribute(hwnd, DwmwaSystemBackdropType, ref backdrop, sizeof(int));

            // WindowStyle=None では OS 標準の角丸が付かないため明示する
            var corner = DwmwcpRound;
            _ = DwmSetWindowAttribute(hwnd, DwmwaWindowCornerPreference, ref corner, sizeof(int));

            // Opacity 由来の WS_EX_LAYERED が残ると黒/グレー固定になる
            ClearLayeredStyle(hwnd);

            // 透けの本体。アルファが低いほど透ける。
            ApplyAccent(hwnd, AccentState.AccentEnableAcrylicBlurBehind, AppTheme.AcrylicTintAlpha);
        }
        catch (Exception ex) when (
            ex is ExternalException
                or InvalidOperationException
                or NotSupportedException)
        {
            // 非対応環境では何もしない
        }
    }

    private static void ApplyAccent(IntPtr hwnd, AccentState state, byte alpha)
    {
        // GradientColor は ABGR。A=不透明度、B=G=R=0（黒ティント）。
        // Acrylic はアルファ 0 だと描画が壊れるため最低 1 を保証する。
        var policy = new AccentPolicy
        {
            AccentState = state,
            AccentFlags = 0,
            GradientColor = Math.Max((byte)1, alpha) << 24,
            AnimationId = 0,
        };

        var size = Marshal.SizeOf<AccentPolicy>();
        var policyPtr = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(policy, policyPtr, false);
            var data = new WindowCompositionAttributeData
            {
                Attribute = WcaAccentPolicy,
                Data = policyPtr,
                SizeOfData = size,
            };
            _ = SetWindowCompositionAttribute(hwnd, ref data);
        }
        finally
        {
            Marshal.FreeHGlobal(policyPtr);
        }
    }

    /// <summary>
    /// Opacity 利用後に残るレイヤードスタイルを外す。
    /// </summary>
    public static void ClearLayeredStyle(IntPtr hwnd)
    {
        var exStyle = GetWindowLongPtr(hwnd, GwlExStyle).ToInt64();
        if ((exStyle & WsExLayered) == 0)
        {
            return;
        }

        _ = SetWindowLongPtr(hwnd, GwlExStyle, new IntPtr(exStyle & ~WsExLayered));
    }

    private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
    {
        return IntPtr.Size == 8
            ? GetWindowLongPtr64(hWnd, nIndex)
            : new IntPtr(GetWindowLong32(hWnd, nIndex));
    }

    private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
    {
        return IntPtr.Size == 8
            ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong)
            : new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public AccentState AccentState;
        public int AccentFlags;
        public int GradientColor;
        public int AnimationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public int Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int attribute,
        ref int attributeValue,
        int attributeSize);

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(
        IntPtr hwnd,
        ref WindowCompositionAttributeData data);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
}
