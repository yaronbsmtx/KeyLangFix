using System;
using System.Runtime.InteropServices;

namespace KeyLangFix.Native;

[StructLayout(LayoutKind.Sequential)]
internal struct POINT { public int X, Y; }

[StructLayout(LayoutKind.Sequential)]
internal struct RECT { public int Left, Top, Right, Bottom; }

[StructLayout(LayoutKind.Sequential)]
internal struct MSG
{
    public nint hwnd;
    public uint message;
    public nuint wParam;
    public nint lParam;
    public uint time;
    public POINT pt;
}

[StructLayout(LayoutKind.Sequential)]
internal struct WNDCLASSEXW
{
    public uint cbSize;
    public uint style;
    public nint lpfnWndProc;
    public int cbClsExtra;
    public int cbWndExtra;
    public nint hInstance;
    public nint hIcon;
    public nint hCursor;
    public nint hbrBackground;
    public nint lpszMenuName;
    public nint lpszClassName;
    public nint hIconSm;
}

[StructLayout(LayoutKind.Sequential)]
internal struct KBDLLHOOKSTRUCT
{
    public uint vkCode;
    public uint scanCode;
    public uint flags;
    public uint time;
    public nuint dwExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
internal struct GUITHREADINFO
{
    public uint cbSize;
    public uint flags;
    public nint hwndActive;
    public nint hwndFocus;
    public nint hwndCapture;
    public nint hwndMenuOwner;
    public nint hwndMoveSize;
    public nint hwndCaret;
    public RECT rcCaret;
}

/// <summary>INPUT with the keyboard member of the union (x64 layout, 40 bytes).</summary>
[StructLayout(LayoutKind.Explicit, Size = 40)]
internal struct INPUT
{
    [FieldOffset(0)] public uint type;
    [FieldOffset(8)] public ushort wVk;
    [FieldOffset(10)] public ushort wScan;
    [FieldOffset(12)] public uint dwFlags;
    [FieldOffset(16)] public uint time;
    [FieldOffset(24)] public nuint dwExtraInfo;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal unsafe struct NOTIFYICONDATAW
{
    public uint cbSize;
    public nint hWnd;
    public uint uID;
    public uint uFlags;
    public uint uCallbackMessage;
    public nint hIcon;
    public fixed char szTip[128];
    public uint dwState;
    public uint dwStateMask;
    public fixed char szInfo[256];
    public uint uTimeoutOrVersion;
    public fixed char szInfoTitle[64];
    public uint dwInfoFlags;
    public Guid guidItem;
    public nint hBalloonIcon;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal unsafe struct LOGFONTW
{
    public int lfHeight, lfWidth, lfEscapement, lfOrientation, lfWeight;
    public byte lfItalic, lfUnderline, lfStrikeOut, lfCharSet, lfOutPrecision, lfClipPrecision, lfQuality, lfPitchAndFamily;
    public fixed char lfFaceName[32];
}

[StructLayout(LayoutKind.Sequential)]
internal struct LASTINPUTINFO
{
    public uint cbSize;
    public uint dwTime;
}

[StructLayout(LayoutKind.Sequential)]
internal struct INITCOMMONCONTROLSEX
{
    public uint dwSize;
    public uint dwICC;
}

internal static partial class Win32
{
    // Messages
    public const uint WM_NULL = 0x0000, WM_DESTROY = 0x0002, WM_CLOSE = 0x0010, WM_QUIT = 0x0012, WM_SETFONT = 0x0030,
        WM_INPUTLANGCHANGEREQUEST = 0x0050, WM_COMMAND = 0x0111, WM_TIMER = 0x0113, WM_HSCROLL = 0x0114,
        WM_MOUSEMOVE = 0x0200, WM_LBUTTONDOWN = 0x0201, WM_LBUTTONUP = 0x0202, WM_LBUTTONDBLCLK = 0x0203,
        WM_RBUTTONDOWN = 0x0204, WM_RBUTTONUP = 0x0205, WM_MBUTTONDOWN = 0x0207, WM_XBUTTONDOWN = 0x020B,
        WM_KEYDOWN = 0x0100, WM_KEYUP = 0x0101, WM_SYSKEYDOWN = 0x0104, WM_SYSKEYUP = 0x0105, WM_APP = 0x8000,
        WM_ERASEBKGND = 0x0014, WM_CTLCOLORBTN = 0x0135, WM_CTLCOLORSTATIC = 0x0138, WM_ENDSESSION = 0x0016, WM_QUERYENDSESSION = 0x0011, WM_DPICHANGED = 0x02E0;

    // Hooks
    public const int WH_KEYBOARD_LL = 13, WH_MOUSE_LL = 14, HC_ACTION = 0;
    public const uint LLKHF_INJECTED = 0x10, LLKHF_ALTDOWN = 0x20, LLKHF_UP = 0x80;

    // Virtual keys
    public const int VK_BACK = 0x08, VK_TAB = 0x09, VK_RETURN = 0x0D, VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12,
        VK_CAPITAL = 0x14, VK_ESCAPE = 0x1B, VK_SPACE = 0x20, VK_LWIN = 0x5B, VK_RWIN = 0x5C, VK_PROCESSKEY = 0xE5,
        VK_LSHIFT = 0xA0, VK_RSHIFT = 0xA1, VK_LCONTROL = 0xA2, VK_RCONTROL = 0xA3, VK_LMENU = 0xA4, VK_RMENU = 0xA5,
        VK_NUMLOCK = 0x90, VK_SCROLL = 0x91, VK_PACKET = 0xE7, VK_Z = 0x5A;

    // SendInput
    public const uint INPUT_KEYBOARD = 1, KEYEVENTF_EXTENDEDKEY = 0x1, KEYEVENTF_KEYUP = 0x2, KEYEVENTF_UNICODE = 0x4, KEYEVENTF_SCANCODE = 0x8;

    // Window styles
    public const uint WS_OVERLAPPED = 0x00000000, WS_POPUP = 0x80000000, WS_CHILD = 0x40000000, WS_VISIBLE = 0x10000000,
        WS_CAPTION = 0x00C00000, WS_SYSMENU = 0x00080000, WS_TABSTOP = 0x00010000, WS_GROUP = 0x00020000,
        WS_EX_CONTROLPARENT = 0x10000;
    public const uint BS_PUSHBUTTON = 0, BS_DEFPUSHBUTTON = 1, BS_AUTOCHECKBOX = 3;
    public const uint BM_GETCHECK = 0x00F0, BM_SETCHECK = 0x00F1;
    public const uint TBM_GETPOS = 0x0400, TBM_SETPOS = 0x0405, TBM_SETRANGE = 0x0406, TBM_SETPAGESIZE = 0x0415,
        TBS_AUTOTICKS = 1;
    public const uint ICC_BAR_CLASSES = 0x4;
    public const uint SS_LEFT = 0;

    public const int IDOK = 1, IDCANCEL = 2;
    public const int SW_HIDE = 0, SW_SHOW = 5, SW_RESTORE = 9;
    public const int SM_CXSCREEN = 0, SM_CYSCREEN = 1;

    // Tray
    public const uint NIM_ADD = 0, NIM_MODIFY = 1, NIM_DELETE = 2;
    public const uint NIF_MESSAGE = 1, NIF_ICON = 2, NIF_TIP = 4;

    // Menu
    public const uint MF_STRING = 0, MF_SEPARATOR = 0x800, MF_CHECKED = 8, MF_GRAYED = 1;
    public const uint TPM_RIGHTBUTTON = 2, TPM_BOTTOMALIGN = 0x20, TPM_RETURNCMD = 0x100, TPM_NONOTIFY = 0x80;

    // PlaySound
    public const uint SND_ASYNC = 1, SND_NODEFAULT = 2, SND_MEMORY = 4;

    public const int GWL_STYLE = -16;
    public const uint ES_PASSWORD = 0x20;
    public const uint CS_HREDRAW = 2, CS_VREDRAW = 1;
    public const int COLOR_WINDOW = 5, IDC_ARROW = 32512;

    public delegate nint HookProc(int code, nuint wParam, nint lParam);

    private const string User32 = "user32.dll";

    [LibraryImport(User32, SetLastError = true)]
    public static partial nint SetWindowsHookExW(int idHook, nint lpfn, nint hMod, uint dwThreadId);
    [LibraryImport(User32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UnhookWindowsHookEx(nint hhk);
    [LibraryImport(User32)]
    public static partial nint CallNextHookEx(nint hhk, int nCode, nuint wParam, nint lParam);

    [LibraryImport(User32)] public static partial nint GetForegroundWindow();
    [LibraryImport(User32)] public static partial uint GetWindowThreadProcessId(nint hWnd, out uint pid);
    [LibraryImport(User32)] public static partial nint GetKeyboardLayout(uint idThread);
    [LibraryImport(User32)] public static partial int GetKeyboardLayoutList(int n, nint[]? list);
    [LibraryImport(User32)] public static partial short GetAsyncKeyState(int vk);
    [LibraryImport(User32)] public static partial short GetKeyState(int vk);
    [LibraryImport(User32)] public static partial uint MapVirtualKeyExW(uint code, uint mapType, nint hkl);
    [LibraryImport(User32)] public static unsafe partial int ToUnicodeEx(uint vk, uint scan, byte* state, char* buf, int cch, uint flags, nint hkl);
    [LibraryImport(User32)] [return: MarshalAs(UnmanagedType.Bool)] public static partial bool GetGUIThreadInfo(uint tid, ref GUITHREADINFO info);
    [LibraryImport(User32, EntryPoint = "GetWindowLongPtrW")] public static partial nint GetWindowLongPtr(nint hWnd, int index);
    [LibraryImport(User32, EntryPoint = "GetClassNameW")] public static unsafe partial int GetClassName(nint hWnd, char* buf, int max);
    [LibraryImport(User32, EntryPoint = "PostMessageW")] [return: MarshalAs(UnmanagedType.Bool)] public static partial bool PostMessage(nint hWnd, uint msg, nuint wParam, nint lParam);
    [LibraryImport(User32, EntryPoint = "SendMessageW")] public static partial nint SendMessage(nint hWnd, uint msg, nuint wParam, nint lParam);
    [LibraryImport(User32)] public static unsafe partial uint SendInput(uint n, INPUT* inputs, int size);

    [LibraryImport(User32, EntryPoint = "RegisterClassExW", SetLastError = true)] public static partial ushort RegisterClassEx(ref WNDCLASSEXW wc);
    [LibraryImport(User32, EntryPoint = "CreateWindowExW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    public static partial nint CreateWindowEx(uint exStyle, string cls, string? title, uint style, int x, int y, int w, int h, nint parent, nint menu, nint inst, nint param);
    [LibraryImport(User32, EntryPoint = "DefWindowProcW")] public static partial nint DefWindowProc(nint hWnd, uint msg, nuint wParam, nint lParam);
    [LibraryImport(User32, EntryPoint = "GetMessageW")] public static partial int GetMessage(out MSG msg, nint hWnd, uint min, uint max);
    [LibraryImport(User32)] [return: MarshalAs(UnmanagedType.Bool)] public static partial bool TranslateMessage(ref MSG msg);
    [LibraryImport(User32, EntryPoint = "DispatchMessageW")] public static partial nint DispatchMessage(ref MSG msg);
    [LibraryImport(User32, EntryPoint = "IsDialogMessageW")] [return: MarshalAs(UnmanagedType.Bool)] public static partial bool IsDialogMessage(nint hDlg, ref MSG msg);
    [LibraryImport(User32)] public static partial void PostQuitMessage(int code);
    [LibraryImport(User32)] [return: MarshalAs(UnmanagedType.Bool)] public static partial bool DestroyWindow(nint hWnd);
    [LibraryImport(User32)] [return: MarshalAs(UnmanagedType.Bool)] public static partial bool ShowWindow(nint hWnd, int cmd);
    [LibraryImport(User32)] [return: MarshalAs(UnmanagedType.Bool)] public static partial bool SetForegroundWindow(nint hWnd);
    [LibraryImport(User32)] public static partial nint SetFocus(nint hWnd);
    [LibraryImport(User32)] public static partial nint GetDlgItem(nint hDlg, int id);
    [LibraryImport(User32, EntryPoint = "SetWindowTextW", StringMarshalling = StringMarshalling.Utf16)] [return: MarshalAs(UnmanagedType.Bool)] public static partial bool SetWindowText(nint hWnd, string text);
    [LibraryImport(User32, EntryPoint = "RegisterWindowMessageW", StringMarshalling = StringMarshalling.Utf16)] public static partial uint RegisterWindowMessage(string name);
    [LibraryImport(User32)] public static partial nuint SetTimer(nint hWnd, nuint id, uint ms, nint proc);
    [LibraryImport(User32)] [return: MarshalAs(UnmanagedType.Bool)] public static partial bool KillTimer(nint hWnd, nuint id);
    [LibraryImport(User32)] public static partial int GetSystemMetrics(int index);
    [LibraryImport(User32)] [return: MarshalAs(UnmanagedType.Bool)] public static partial bool GetClientRect(nint hWnd, out RECT rect);
    [LibraryImport(User32)] public static partial int FillRect(nint hdc, ref RECT rect, nint brush);
    [LibraryImport(User32, EntryPoint = "MessageBoxW", StringMarshalling = StringMarshalling.Utf16)] public static partial int MessageBox(nint hWnd, string text, string caption, uint type);
    [LibraryImport("gdi32.dll")] public static partial uint SetTextColor(nint hdc, uint color);
    [LibraryImport("gdi32.dll")] public static partial uint SetBkColor(nint hdc, uint color);
    [LibraryImport(User32)] public static partial uint GetDpiForSystem();
    [LibraryImport(User32)] [return: MarshalAs(UnmanagedType.Bool)] public static partial bool GetLastInputInfo(ref LASTINPUTINFO info);
    [LibraryImport(User32)] [return: MarshalAs(UnmanagedType.Bool)] public static partial bool GetCursorPos(out POINT pt);
    [LibraryImport(User32)] [return: MarshalAs(UnmanagedType.Bool)] public static partial bool AdjustWindowRectEx(ref RECT rect, uint style, [MarshalAs(UnmanagedType.Bool)] bool menu, uint exStyle);
    [LibraryImport(User32, EntryPoint = "LoadCursorW")] public static partial nint LoadCursor(nint inst, nint name);
    [LibraryImport(User32)] public static unsafe partial nint CreateIconFromResourceEx(byte* bits, uint size, [MarshalAs(UnmanagedType.Bool)] bool icon, uint ver, int cx, int cy, uint flags);
    [LibraryImport(User32)] [return: MarshalAs(UnmanagedType.Bool)] public static partial bool DestroyIcon(nint icon);

    [LibraryImport(User32)] public static partial nint CreatePopupMenu();
    [LibraryImport(User32, EntryPoint = "AppendMenuW", StringMarshalling = StringMarshalling.Utf16)] [return: MarshalAs(UnmanagedType.Bool)] public static partial bool AppendMenu(nint menu, uint flags, nuint id, string? text);
    [LibraryImport(User32)] public static partial int TrackPopupMenu(nint menu, uint flags, int x, int y, int reserved, nint hWnd, nint rect);
    [LibraryImport(User32)] [return: MarshalAs(UnmanagedType.Bool)] public static partial bool DestroyMenu(nint menu);

    [LibraryImport("shell32.dll", EntryPoint = "Shell_NotifyIconW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool Shell_NotifyIcon(uint message, ref NOTIFYICONDATAW data);

    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint GetModuleHandle(string? name);
    [LibraryImport("kernel32.dll", EntryPoint = "CreateMutexW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    public static partial nint CreateMutex(nint attrs, [MarshalAs(UnmanagedType.Bool)] bool owner, string name);

    [LibraryImport("winmm.dll", EntryPoint = "PlaySoundW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool PlaySound(nint sound, nint module, uint flags);

    [LibraryImport("gdi32.dll", EntryPoint = "CreateFontIndirectW")]
    public static unsafe partial nint CreateFontIndirect(LOGFONTW* lf);
    [LibraryImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] public static partial bool DeleteObject(nint obj);

    [LibraryImport("comctl32.dll")] [return: MarshalAs(UnmanagedType.Bool)] public static partial bool InitCommonControlsEx(ref INITCOMMONCONTROLSEX icc);

    public static nint MakeLParam(int lo, int hi) => (nint)(((hi & 0xFFFF) << 16) | (lo & 0xFFFF));
}
