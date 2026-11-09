using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using KeyLangFix.Native;
using KeyLangFix.Services;

namespace KeyLangFix.Ui;

/// <summary>
/// Modal dialog (opened from the settings window) with the two learned-word lists, Hebrew and English.
/// A checked word is approved: the detector never switches away from it. OK saves, Cancel discards.
/// </summary>
internal static unsafe class LearnedWordsWindow
{
    private const string ClassName = "KeyLangFixLearnedWords";
    private const int IdHebrew = 301, IdEnglish = 302, IdDeleteHe = 303, IdDeleteEn = 304;

    private static nint _hwnd, _font, _hInstance;
    private static bool _classRegistered, _dark;
    private static uint _dpi = 96;
    private static readonly List<string> HeWords = new(), EnWords = new();

    private static int S(int v) => (int)(v * _dpi / 96);

    public static void ShowModal(nint owner, nint font, uint dpi, bool dark)
    {
        if (_hwnd != 0) { Win32.SetForegroundWindow(_hwnd); return; }
        _hInstance = Win32.GetModuleHandle(null);
        _font = font;
        _dpi = dpi;
        _dark = dark;

        var init = new INITCOMMONCONTROLSEX { dwSize = (uint)sizeof(INITCOMMONCONTROLSEX), dwICC = Win32.ICC_LISTVIEW_CLASSES };
        Win32.InitCommonControlsEx(ref init);
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
        var rect = new RECT { Right = S(480), Bottom = S(402) };
        Win32.AdjustWindowRectEx(ref rect, style, false, Win32.WS_EX_CONTROLPARENT);
        int w = rect.Right - rect.Left, h = rect.Bottom - rect.Top;
        int x = (Win32.GetSystemMetrics(Win32.SM_CXSCREEN) - w) / 2;
        int y = (Win32.GetSystemMetrics(Win32.SM_CYSCREEN) - h) / 3;
        _hwnd = Win32.CreateWindowEx(Win32.WS_EX_CONTROLPARENT, ClassName, "Learned words", style, x, y, w, h, owner, 0, _hInstance, 0);
        if (_hwnd == 0) return;
        Theme.Apply(_hwnd, _dark);

        Child("STATIC", "Words you switched back right after an automatic switch. Check a word to approve it: it will then be left as typed.",
            0, Win32.SS_LEFT, 16, 12, 448, 34, 0);
        Child("STATIC", "Hebrew", 0, Win32.SS_LEFT, 16, 54, 218, 18, 0);
        Child("STATIC", "English", 0, Win32.SS_LEFT, 246, 54, 218, 18, 0);
        nint heList = MakeList(IdHebrew, 16, 74);
        nint enList = MakeList(IdEnglish, 246, 74);
        Child("BUTTON", "Delete selected", 0, Win32.BS_PUSHBUTTON | Win32.WS_TABSTOP, 16, 318, 130, 26, IdDeleteHe);
        Child("BUTTON", "Delete selected", 0, Win32.BS_PUSHBUTTON | Win32.WS_TABSTOP, 246, 318, 130, 26, IdDeleteEn);
        Child("BUTTON", "OK", 0, Win32.BS_DEFPUSHBUTTON | Win32.WS_TABSTOP, 294, 360, 80, 28, Win32.IDOK);
        Child("BUTTON", "Cancel", 0, Win32.BS_PUSHBUTTON | Win32.WS_TABSTOP, 384, 360, 80, 28, Win32.IDCANCEL);
        foreach (int id in new[] { IdHebrew, IdEnglish, IdDeleteHe, IdDeleteEn, Win32.IDOK, Win32.IDCANCEL })
            Theme.ApplyToControl(Win32.GetDlgItem(_hwnd, id), _dark);

        Fill(heList, LearnedWords.Hebrew, HeWords);
        Fill(enList, LearnedWords.English, EnWords);

        Win32.EnableWindow(owner, false);
        Win32.ShowWindow(_hwnd, Win32.SW_SHOW);
        Win32.SetFocus(heList);

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

    private static nint Child(string cls, string text, uint exStyle, uint style, int x, int y, int w, int h, int id)
    {
        nint c = Win32.CreateWindowEx(exStyle, cls, text, Win32.WS_CHILD | Win32.WS_VISIBLE | style,
            S(x), S(y), S(w), S(h), _hwnd, id, _hInstance, 0);
        Win32.SendMessage(c, Win32.WM_SETFONT, (nuint)_font, 1);
        return c;
    }

    private static nint MakeList(int id, int x, int y)
    {
        nint lv = Child("SysListView32", "", Win32.WS_EX_CLIENTEDGE,
            Win32.LVS_REPORT | Win32.LVS_NOCOLUMNHEADER | Win32.WS_TABSTOP | Win32.WS_VSCROLL, x, y, 218, 236, id);
        Win32.SendMessage(lv, Win32.LVM_SETEXTENDEDLISTVIEWSTYLE, 0, (nint)(Win32.LVS_EX_CHECKBOXES | Win32.LVS_EX_FULLROWSELECT));
        var col = new LVCOLUMNW { mask = Win32.LVCF_WIDTH, cx = S(196) };
        Win32.SendMessage(lv, Win32.LVM_INSERTCOLUMNW, 0, (nint)(&col));
        if (_dark)
        {
            Win32.SendMessage(lv, Win32.LVM_SETBKCOLOR, 0, (nint)Theme.DarkBack);
            Win32.SendMessage(lv, Win32.LVM_SETTEXTBKCOLOR, 0, (nint)Theme.DarkBack);
            Win32.SendMessage(lv, Win32.LVM_SETTEXTCOLOR, 0, (nint)Theme.DarkText);
        }
        return lv;
    }

    private static void Fill(nint lv, IReadOnlyList<LearnedEntry> entries, List<string> words)
    {
        words.Clear();
        foreach (var e in entries)
        {
            fixed (char* p = e.Word)
            {
                var item = new LVITEMW { mask = Win32.LVIF_TEXT, iItem = words.Count, pszText = (nint)p };
                Win32.SendMessage(lv, Win32.LVM_INSERTITEMW, 0, (nint)(&item));
            }
            SetChecked(lv, words.Count, e.Approved);
            words.Add(e.Word);
        }
    }

    // State image 1 = unchecked, 2 = checked (bits 12-15 of the item state).
    private static void SetChecked(nint lv, int index, bool on)
    {
        var item = new LVITEMW { state = (on ? 2u : 1u) << 12, stateMask = Win32.LVIS_STATEIMAGEMASK };
        Win32.SendMessage(lv, Win32.LVM_SETITEMSTATE, (nuint)index, (nint)(&item));
    }

    private static bool IsChecked(nint lv, int index) =>
        ((uint)Win32.SendMessage(lv, Win32.LVM_GETITEMSTATE, (nuint)index, (nint)Win32.LVIS_STATEIMAGEMASK) >> 12) == 2;

    private static bool IsSelected(nint lv, int index) =>
        ((uint)Win32.SendMessage(lv, Win32.LVM_GETITEMSTATE, (nuint)index, (nint)Win32.LVIS_SELECTED) & Win32.LVIS_SELECTED) != 0;

    private static void DeleteSelected(int listId, List<string> words)
    {
        nint lv = Win32.GetDlgItem(_hwnd, listId);
        for (int i = words.Count - 1; i >= 0; i--)
        {
            if (!IsSelected(lv, i)) continue;
            Win32.SendMessage(lv, Win32.LVM_DELETEITEM, (nuint)i, 0);
            words.RemoveAt(i);
        }
    }

    private static List<LearnedEntry> Collect(int listId, List<string> words)
    {
        nint lv = Win32.GetDlgItem(_hwnd, listId);
        var list = new List<LearnedEntry>(words.Count);
        for (int i = 0; i < words.Count; i++) list.Add(new LearnedEntry(words[i], IsChecked(lv, i)));
        return list;
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
                    Win32.SetTextColor((nint)wParam, Theme.DarkText);
                    Win32.SetBkColor((nint)wParam, Theme.DarkBack);
                    return Theme.DarkBrush;
                case Win32.WM_COMMAND:
                    int id = (int)(wParam & 0xFFFF);
                    if (id == Win32.IDOK)
                    {
                        LearnedWords.Set(Collect(IdHebrew, HeWords), Collect(IdEnglish, EnWords));
                        Win32.DestroyWindow(hwnd);
                        return 0;
                    }
                    if (id == Win32.IDCANCEL) { Win32.DestroyWindow(hwnd); return 0; }
                    if (id == IdDeleteHe) { DeleteSelected(IdHebrew, HeWords); return 0; }
                    if (id == IdDeleteEn) { DeleteSelected(IdEnglish, EnWords); return 0; }
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
