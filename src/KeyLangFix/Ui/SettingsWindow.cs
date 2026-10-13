using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using KeyLangFix.Native;
using KeyLangFix.Services;
using KeyLangFix.Tray;

namespace KeyLangFix.Ui;

/// <summary>Small native settings window (no WinForms/WPF): sliders and checkboxes.</summary>
internal static unsafe class SettingsWindow
{
    private const string ClassName = "KeyLangFixSettings";
    private const int IdLevel = 101, IdLevelText = 102, IdMin = 103, IdMinText = 104,
        IdSound = 105, IdConvert = 106, IdAutoStart = 107, IdVolume = 108, IdVolumeText = 109, IdDefaults = 110;

    private static nint _hwnd, _font, _hInstance;
    private static bool _classRegistered;
    private static bool _dark;
    private static uint _dpi = 96;

    public static nint Handle => _hwnd;

    private static int S(int v) => (int)(v * _dpi / 96);

    public static void Show()
    {
        if (_hwnd != 0)
        {
            Win32.ShowWindow(_hwnd, Win32.SW_RESTORE);
            Win32.SetForegroundWindow(_hwnd);
            return;
        }

        _hInstance = Win32.GetModuleHandle(null);
        _dpi = Win32.GetDpiForSystem();
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

        var init = new INITCOMMONCONTROLSEX { dwSize = (uint)sizeof(INITCOMMONCONTROLSEX), dwICC = Win32.ICC_BAR_CLASSES };
        Win32.InitCommonControlsEx(ref init);
        CreateFont();

        const uint style = Win32.WS_CAPTION | Win32.WS_SYSMENU;
        var rect = new RECT { Right = S(400), Bottom = S(378) };
        Win32.AdjustWindowRectEx(ref rect, style, false, Win32.WS_EX_CONTROLPARENT);
        int w = rect.Right - rect.Left, h = rect.Bottom - rect.Top;
        int x = (Win32.GetSystemMetrics(Win32.SM_CXSCREEN) - w) / 2;
        int y = (Win32.GetSystemMetrics(Win32.SM_CYSCREEN) - h) / 3;

        _hwnd = Win32.CreateWindowEx(Win32.WS_EX_CONTROLPARENT, ClassName, "KeyLangFix Settings", style,
            x, y, w, h, 0, 0, _hInstance, 0);
        if (_hwnd == 0) return;
        _dark = Theme.IsDark();
        Theme.Apply(_hwnd, _dark);

        Label("Confidence required before switching:", 16, 14, 368, 18, 0);
        Trackbar(IdLevel, 12, 34, 280, 32, 1, 5, SettingsStore.ConfidenceLevel);
        Label("", 300, 40, 90, 18, IdLevelText);
        Label("Lower: switches sooner (more mistakes). Higher: waits for stronger evidence.", 16, 68, 368, 34, 0);

        Label("Minimum letters typed before switching:", 16, 108, 368, 18, 0);
        Trackbar(IdMin, 12, 128, 280, 32, 3, 8, SettingsStore.MinLetters);
        Label("", 300, 134, 90, 18, IdMinText);

        Check("Rewrite the text already typed in the correct language", IdConvert, 16, 176, SettingsStore.ConvertText);
        Check("Play a sound when the language is switched", IdSound, 16, 204, SettingsStore.PlaySound);
        Label("Sound volume:", 36, 240, 90, 18, 0);
        Trackbar(IdVolume, 124, 232, 170, 32, 0, 100, SettingsStore.SoundVolume);
        Label("", 300, 240, 90, 18, IdVolumeText);
        Check("Start with Windows", IdAutoStart, 16, 284, AutoStart.IsEnabled);

        Button("Defaults", IdDefaults, 16, 332, false);
        Button("OK", Win32.IDOK, 214, 332, true);
        Button("Cancel", Win32.IDCANCEL, 304, 332, false);

        foreach (int id in new[] { IdLevel, IdMin, IdVolume, IdSound, IdConvert, IdAutoStart, IdDefaults, Win32.IDOK, Win32.IDCANCEL })
            Theme.ApplyToControl(Win32.GetDlgItem(_hwnd, id), _dark);

        UpdateLabels();
        Win32.ShowWindow(_hwnd, Win32.SW_SHOW);
        Win32.SetForegroundWindow(_hwnd);
    }

    private static void CreateFont()
    {
        if (_font != 0) return;
        var lf = new LOGFONTW { lfHeight = -(int)(9 * _dpi / 72), lfWeight = 400, lfQuality = 5 };
        const string face = "Segoe UI";
        for (int i = 0; i < face.Length; i++) lf.lfFaceName[i] = face[i];
        _font = Win32.CreateFontIndirect(&lf);
    }

    private static nint Child(string cls, string text, uint style, int x, int y, int w, int h, int id)
    {
        nint c = Win32.CreateWindowEx(0, cls, text, Win32.WS_CHILD | Win32.WS_VISIBLE | style,
            S(x), S(y), S(w), S(h), _hwnd, id, _hInstance, 0);
        Win32.SendMessage(c, Win32.WM_SETFONT, (nuint)_font, 1);
        return c;
    }

    private static void Label(string text, int x, int y, int w, int h, int id) => Child("STATIC", text, Win32.SS_LEFT, x, y, w, h, id);

    private static void Check(string text, int id, int x, int y, bool on)
    {
        nint c = Child("BUTTON", text, Win32.BS_AUTOCHECKBOX | Win32.WS_TABSTOP, x, y, 368, 22, id);
        Win32.SendMessage(c, Win32.BM_SETCHECK, on ? 1u : 0u, 0);
    }

    private static void Button(string text, int id, int x, int y, bool isDefault) =>
        Child("BUTTON", text, (isDefault ? Win32.BS_DEFPUSHBUTTON : Win32.BS_PUSHBUTTON) | Win32.WS_TABSTOP, x, y, 80, 28, id);

    private static void Trackbar(int id, int x, int y, int w, int h, int min, int max, int value)
    {
        nint t = Child("msctls_trackbar32", "", Win32.TBS_AUTOTICKS | Win32.WS_TABSTOP, x, y, w, h, id);
        Win32.SendMessage(t, Win32.TBM_SETRANGE, 1, Win32.MakeLParam(min, max));
        Win32.SendMessage(t, Win32.TBM_SETPAGESIZE, 0, 1);
        Win32.SendMessage(t, Win32.TBM_SETPOS, 1, value);
    }

    private static int Pos(int id) => (int)Win32.SendMessage(Win32.GetDlgItem(_hwnd, id), Win32.TBM_GETPOS, 0, 0);
    private static bool Checked(int id) => Win32.SendMessage(Win32.GetDlgItem(_hwnd, id), Win32.BM_GETCHECK, 0, 0) != 0;

    private static void UpdateLabels()
    {
        string[] names = { "", "Very low", "Low", "Medium", "High", "Very high" };
        int level = Math.Clamp(Pos(IdLevel), 1, 5);
        Win32.SetWindowText(Win32.GetDlgItem(_hwnd, IdLevelText), names[level]);
        Win32.SetWindowText(Win32.GetDlgItem(_hwnd, IdMinText), Pos(IdMin) + " letters");
        Win32.SetWindowText(Win32.GetDlgItem(_hwnd, IdVolumeText), Pos(IdVolume) + "%");
    }

    /// <summary>Puts the factory defaults into the controls. Nothing is saved until OK.</summary>
    private static void ResetControlsToDefaults()
    {
        Win32.SendMessage(Win32.GetDlgItem(_hwnd, IdLevel), Win32.TBM_SETPOS, 1, SettingsStore.DefaultConfidenceLevel);
        Win32.SendMessage(Win32.GetDlgItem(_hwnd, IdMin), Win32.TBM_SETPOS, 1, SettingsStore.DefaultMinLetters);
        Win32.SendMessage(Win32.GetDlgItem(_hwnd, IdVolume), Win32.TBM_SETPOS, 1, SettingsStore.DefaultSoundVolume);
        Win32.SendMessage(Win32.GetDlgItem(_hwnd, IdConvert), Win32.BM_SETCHECK, SettingsStore.DefaultConvertText ? 1u : 0u, 0);
        Win32.SendMessage(Win32.GetDlgItem(_hwnd, IdSound), Win32.BM_SETCHECK, SettingsStore.DefaultPlaySound ? 1u : 0u, 0);
        Win32.SendMessage(Win32.GetDlgItem(_hwnd, IdAutoStart), Win32.BM_SETCHECK, SettingsStore.DefaultAutoStart ? 1u : 0u, 0);
        UpdateLabels();
    }

    private static void Apply()
    {
        SettingsStore.ConfidenceLevel = Math.Clamp(Pos(IdLevel), 1, 5);
        SettingsStore.MinLetters = Math.Clamp(Pos(IdMin), 3, 8);
        SettingsStore.ConvertText = Checked(IdConvert);
        SettingsStore.PlaySound = Checked(IdSound);
        SettingsStore.SoundVolume = Math.Clamp(Pos(IdVolume), 0, 100);
        SettingsStore.Save();
        AutoStart.Set(Checked(IdAutoStart));
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
                case Win32.WM_HSCROLL:
                    UpdateLabels();
                    // Preview the sound when the volume slider is released or keyed (SB_ENDSCROLL = 8).
                    if (lParam == Win32.GetDlgItem(hwnd, IdVolume) && (wParam & 0xFFFF) == 8)
                        SoundPlayer.Play(Pos(IdVolume));
                    return 0;
                case Win32.WM_COMMAND:
                    int id = (int)(wParam & 0xFFFF);
                    if (id == Win32.IDOK) { Apply(); Win32.DestroyWindow(hwnd); return 0; }
                    if (id == Win32.IDCANCEL) { Win32.DestroyWindow(hwnd); return 0; }
                    if (id == IdDefaults) { ResetControlsToDefaults(); return 0; }
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
