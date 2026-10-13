using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace KeyLangFix.Ui;

/// <summary>Follows the Windows app light/dark setting for the native windows and menus we create.</summary>
internal static partial class Theme
{
    public const uint DarkBack = 0x00202020;   // COLORREF 0x00BBGGRR
    public const uint DarkText = 0x00FFFFFF;

    private static nint _darkBrush;

    public static nint DarkBrush => _darkBrush != 0 ? _darkBrush : (_darkBrush = CreateSolidBrush(DarkBack));

    /// <summary>True when "Choose your default app mode" is Dark.</summary>
    public static bool IsDark()
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return k?.GetValue("AppsUseLightTheme") is int v && v == 0;
        }
        catch { return false; }
    }

    /// <summary>Lets popup menus (the tray menu) follow the system theme. Undocumented uxtheme ordinals, best effort.</summary>
    public static void EnableThemedMenus()
    {
        try
        {
            nint ux = LoadLibraryW("uxtheme.dll");
            if (ux == 0) return;
            nint setMode = GetProcAddress(ux, 135);      // SetPreferredAppMode
            nint flush = GetProcAddress(ux, 136);        // FlushMenuThemes
            unsafe
            {
                if (setMode != 0) ((delegate* unmanaged<int, int>)setMode)(1 /* AllowDark: follow system */);
                if (flush != 0) ((delegate* unmanaged<void>)flush)();
            }
        }
        catch { /* older Windows: menus stay light */ }
    }

    /// <summary>Dark title bar plus dark-aware common controls for a top-level window and its children.</summary>
    public static unsafe void Apply(nint window, bool dark)
    {
        int on = dark ? 1 : 0;
        if (DwmSetWindowAttribute(window, 20, &on, sizeof(int)) != 0)
            DwmSetWindowAttribute(window, 19, &on, sizeof(int));   // Windows 10 before 20H1
    }

    public static void ApplyToControl(nint control, bool dark)
    {
        if (dark) SetWindowTheme(control, "DarkMode_Explorer", null);
    }

    [LibraryImport("gdi32.dll")] private static partial nint CreateSolidBrush(uint color);
    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16)] private static partial nint LoadLibraryW(string name);
    [LibraryImport("kernel32.dll")] private static partial nint GetProcAddress(nint module, nint ordinal);
    [LibraryImport("dwmapi.dll")] private static unsafe partial int DwmSetWindowAttribute(nint hwnd, int attr, int* value, int size);
    [LibraryImport("uxtheme.dll", StringMarshalling = StringMarshalling.Utf16)] private static partial int SetWindowTheme(nint hwnd, string? subApp, string? subIdList);
}
