using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace ScoreCode.Client.Helpers;

/// <summary>
/// Uses the Windows Desktop Window Manager (DWM) API to customise the native title bar.
/// Supports dark/light mode toggle and custom caption colour (Windows 11+).
/// </summary>
public static class TitleBarHelper
{
    // DWM attributes
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;   // Win10 1809+
    private const int DWMWA_CAPTION_COLOR           = 35;   // Win11 22000+

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd, int attribute, ref int value, int size);

    /// <summary>
    /// Apply dark-mode title bar to a WPF window.
    /// Call this in OnSourceInitialized or after the window has a handle.
    /// </summary>
    public static void ApplyDarkMode(Window window)
    {
        if (window.IsLoaded)
            SetDarkMode(window, true);
        else
            window.SourceInitialized += (_, _) => SetDarkMode(window, true);
    }

    /// <summary>
    /// Apply light-mode title bar to a WPF window.
    /// </summary>
    public static void ApplyLightMode(Window window)
    {
        if (window.IsLoaded)
            SetDarkMode(window, false);
        else
            window.SourceInitialized += (_, _) => SetDarkMode(window, false);
    }

    /// <summary>
    /// Set the title bar caption background colour (Windows 11 only).
    /// On Windows 10 this is a no-op.
    /// </summary>
    public static void SetCaptionColor(Window window, byte r, byte g, byte b)
    {
        if (window.IsLoaded)
            ApplyCaptionColor(window, r, g, b);
        else
            window.SourceInitialized += (_, _) => ApplyCaptionColor(window, r, g, b);
    }

    // ── internals ──────────────────────────────────────────────────────────
    private static void SetDarkMode(Window window, bool dark)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        int value = dark ? 1 : 0;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));
    }

    private static void ApplyCaptionColor(Window window, byte r, byte g, byte b)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        // COLORREF is 0x00BBGGRR
        int colorRef = r | (g << 8) | (b << 16);
        DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref colorRef, sizeof(int));
    }

    /// <summary>
    /// Set window icon from Assets/scorecode.ico next to the exe.
    /// </summary>
    public static void SetIcon(Window window)
    {
        var icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "scorecode.ico");
        if (!File.Exists(icoPath)) return;

        var uri = new Uri(icoPath, UriKind.Absolute);
        window.Icon = BitmapFrame.Create(uri);
    }
}
