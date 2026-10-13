using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using KeyLangFix.Core;
using KeyLangFix.Native;

namespace KeyLangFix.Hooks;

/// <summary>
/// Global low-level keyboard and mouse hooks. Both run on the UI thread, so the work done in them
/// is kept tiny: mouse moves return immediately, key presses go to <see cref="Engine"/>.
/// </summary>
internal static unsafe class HookManager
{
    private static nint _keyboard, _mouse;

    /// <summary>Tick of the last call into either hook; used by the watchdog.</summary>
    public static volatile uint LastCallTick;

    public static bool Install()
    {
        nint module = Win32.GetModuleHandle(null);
        _keyboard = Win32.SetWindowsHookExW(Win32.WH_KEYBOARD_LL, (nint)(delegate* unmanaged<int, nuint, nint, nint>)&KeyboardProc, module, 0);
        _mouse = Win32.SetWindowsHookExW(Win32.WH_MOUSE_LL, (nint)(delegate* unmanaged<int, nuint, nint, nint>)&MouseProc, module, 0);
        return _keyboard != 0;
    }

    /// <summary>
    /// Windows drops a low-level hook silently if it ever times out. If the user is active but
    /// neither hook has been called since, put both back.
    /// </summary>
    public static void CheckAlive()
    {
        var lii = new LASTINPUTINFO { cbSize = (uint)sizeof(LASTINPUTINFO) };
        if (!Win32.GetLastInputInfo(ref lii)) return;
        uint now = (uint)Environment.TickCount;
        if (now - lii.dwTime > 3000) return;                // user idle: nothing to conclude
        if ((int)(lii.dwTime - LastCallTick) > 1500)        // input newer than any hook call
        {
            Uninstall();
            Install();
            LastCallTick = (uint)Environment.TickCount;
        }
    }

    public static void Uninstall()
    {
        if (_keyboard != 0) Win32.UnhookWindowsHookEx(_keyboard);
        if (_mouse != 0) Win32.UnhookWindowsHookEx(_mouse);
        _keyboard = _mouse = 0;
    }

    [UnmanagedCallersOnly]
    private static nint KeyboardProc(int code, nuint wParam, nint lParam)
    {
        LastCallTick = (uint)Environment.TickCount;
        if (code == Win32.HC_ACTION)
        {
            try
            {
                var k = (KBDLLHOOKSTRUCT*)lParam;
                bool down = wParam == Win32.WM_KEYDOWN || wParam == Win32.WM_SYSKEYDOWN;
                if (k->dwExtraInfo != Engine.Magic && Engine.OnKey(k->vkCode, k->scanCode, k->flags, down))
                    return 1;
            }
            catch { /* never let an exception escape into the OS hook chain */ }
        }
        return Win32.CallNextHookEx(0, code, wParam, lParam);
    }

    [UnmanagedCallersOnly]
    private static nint MouseProc(int code, nuint wParam, nint lParam)
    {
        LastCallTick = (uint)Environment.TickCount;
        if (code == Win32.HC_ACTION && (uint)wParam is Win32.WM_LBUTTONDOWN or Win32.WM_RBUTTONDOWN or Win32.WM_MBUTTONDOWN or Win32.WM_XBUTTONDOWN)
        {
            try { Engine.OnMouseDown(); } catch { }
        }
        return Win32.CallNextHookEx(0, code, wParam, lParam);
    }
}
