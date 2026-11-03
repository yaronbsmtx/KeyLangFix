using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using KeyLangFix.Native;
using KeyLangFix.Services;

namespace KeyLangFix.Ui;

/// <summary>Modal editor for the abbreviation list (one entry per line), opened from the settings window.</summary>
internal static unsafe class AbbreviationsWindow
{
    private const string ClassName = "KeyLangFixAbbreviations";
    private const int IdEdit = 201, IdReset = 202;

    private static nint _hwnd, _font, _hInstance;
    private static bool _classRegistered, _dark;
    private static uint _dpi = 96;

    private static int S(int v) => (int)(v * _dpi / 96);

    public static void ShowModal(nint owner, nint font, uint dpi, bool dark)
    {
        if (_hwnd != 0) { Win32.SetForegroundWindow(_hwnd); return; }
        _hInstance = Win32.GetModuleHandle(null);
        _font = font;
        _dpi = dpi;
        _dark = dark;
        if (!_classRegistered)
        {
            var wc = new WNDCLASSEXW
            {
                cbSize = (uint)sizeof(WNDCLASSEXW),
                style = Win32.CS_HREDRAW | Win32.CS_VREDRAW,
                lpfnWndProc = (nint)(delegate* unmanaged<nint, uint, nuint, nint, nint>)&WndProc,
                hInstance = _hInstance,
                hCursor = Win32.LoadCursor(0, Win32.IDC_ARROW),
                hbrBackground = Win32.COLOR_WINDOW + 1,
                lpszClassName = Marshal.StringToHGlobalUni(ClassName),
            };
            Win32.RegisterClassEx(ref wc);
            _classRegistered = true;
        }

        const uint style = Win32.WS_CAPTION | Win32.WS_SYSMENU;
        var rect = new RECT { Right = S(360), Bottom = S(404) };
        Win32.AdjustWindowRectEx(ref rect, style, false, Win32.WS_EX_CONTROLPARENT);
        int w = rect.Right - rect.Left, h = rect.Bottom - rect.Top;
        int x = (Win32.GetSystemMetrics(Win32.SM_CXSCREEN) - w) / 2;
        int y = (Win32.GetSystemMetrics(Win32.SM_CYSCREEN) - h) / 3;
        _hwnd = Win32.CreateWindowEx(Win32.WS_EX_CONTROLPARENT, ClassName, "Abbreviations", style, x, y, w, h, owner, 0, _hInstance, 0);
        if (_hwnd == 0) return;
        Theme.Apply(_hwnd, _dark);

        Child("STATIC", "Typed as they are, never switched (any case). One entry per line; lines starting with # are ignored.",
            Win32.SS_LEFT, 16, 12, 328, 34, 0);
        nint edit = Child("EDIT", Abbreviations.Text,
            Win32.WS_TABSTOP | Win32.WS_VSCROLL | Win32.WS_BORDER | Win32.ES_MULTILINE | Win32.ES_AUTOVSCROLL | Win32.ES_WANTRETURN,
            16, 52, 328, 288, IdEdit);
        Child("BUTTON", "Reset to defaults", Win32.BS_PUSHBUTTON | Win32.WS_TABSTOP, 16, 358, 122, 28, IdReset);
        Child("BUTTON", "OK", Win32.BS_DEFPUSHBUTTON | Win32.WS_TABSTOP, 174, 358, 80, 28, Win32.IDOK);
        Child("BUTTON", "Cancel", Win32.BS_PUSHBUTTON | Win32.WS_TABSTOP, 264, 358, 80, 28, Win32.IDCANCEL);
        foreach (int id in new[] { IdEdit, IdReset, Win32.IDOK, Win32.IDCANCEL })
            Theme.ApplyToControl(Win32.GetDlgItem(_hwnd, id), _dark);

        Win32.EnableWindow(owner, false);
        Win32.ShowWindow(_hwnd, Win32.SW_SHOW);
        Win32.SetFocus(edit);

        // Own message loop until this window is destroyed (the settings window stays disabled meanwhile).
        while (_hwnd != 0 && Win32.GetMessage(out var msg, 0, 0, 0) > 0)
        {
            if (Win32.IsDialogMessage(_hwnd, ref msg)) continue;
            Win32.TranslateMessage(ref msg);
            Win32.DispatchMessage(ref msg);
        }
        Win32.EnableWindow(owner, true);
        Win32.SetForegroundWindow(owner);
    }

    private static nint Child(string cls, string text, uint style, int x, int y, int w, int h, int id)
    {
        nint c = Win32.CreateWindowEx(0, cls, text, Win32.WS_CHILD | Win32.WS_VISIBLE | style,
            S(x), S(y), S(w), S(h), _hwnd, id, _hInstance, 0);
        Win32.SendMessage(c, Win32.WM_SETFONT, (nuint)_font, 1);
        return c;
    }

    private static string EditText()
    {
        nint edit = Win32.GetDlgItem(_hwnd, IdEdit);
        int len = Win32.GetWindowTextLength(edit);
        char[] buf = new char[len + 1];
        fixed (char* p = buf) Win32.GetWindowText(edit, p, buf.Length);
        return new string(buf, 0, len);
    }

    [UnmanagedCallersOnly]
    private static nint WndProc(nint hwnd, uint msg, nuint wParam, nint lParam)
    {
        try
        {
            switch (msg)
            {
                case Win32.WM_ERASEBKGND when _dark:
                    Win32.GetClientRect(hwnd, out var rc);
                    Win32.FillRect((nint)wParam, ref rc, Theme.DarkBrush);
                    return 1;
                case Win32.WM_CTLCOLORSTATIC when _dark:
                case Win32.WM_CTLCOLORBTN when _dark:
                case Win32.WM_CTLCOLOREDIT when _dark:
                    Win32.SetTextColor((nint)wParam, Theme.DarkText);
                    Win32.SetBkColor((nint)wParam, Theme.DarkBack);
                    return Theme.DarkBrush;
                case Win32.WM_COMMAND:
                    int id = (int)(wParam & 0xFFFF);
                    if (id == Win32.IDOK) { Abbreviations.SaveText(EditText()); Win32.DestroyWindow(hwnd); return 0; }
                    if (id == Win32.IDCANCEL) { Win32.DestroyWindow(hwnd); return 0; }
                    if (id == IdReset) { Win32.SetWindowText(Win32.GetDlgItem(hwnd, IdEdit), Abbreviations.DefaultText); return 0; }
                    break;
                case Win32.WM_CLOSE:
                    Win32.DestroyWindow(hwnd);
                    return 0;
                case Win32.WM_DESTROY:
                    _hwnd = 0;
                    return 0;
            }
        }
        catch { }
        return Win32.DefWindowProc(hwnd, msg, wParam, lParam);
    }
}
