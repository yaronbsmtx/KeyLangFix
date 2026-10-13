using System;
using System.IO;
using KeyLangFix.Native;
using KeyLangFix.Services;

namespace KeyLangFix.Tray;

/// <summary>Notification-area icon: tooltip with statistics, and the right-click menu.</summary>
internal static unsafe class TrayIcon
{
    public const uint CallbackMessage = Win32.WM_APP + 1;
    public const int CmdToggle = 1, CmdSettings = 2, CmdQuit = 3, CmdResetStats = 4;

    private static nint _hwnd;
    private static nint _iconOn, _iconOff;
    private static string _lastTip = "";
    private static bool _added;

    public static string Version
    {
        get
        {
            var v = typeof(TrayIcon).Assembly.GetName().Version;
            return v == null ? "1.0.0" : $"{v.Major}.{v.Minor}.{v.Build}";
        }
    }

    public static void Create(nint hwnd)
    {
        _hwnd = hwnd;
        _iconOn = LoadIcon("icon_on.bin");
        _iconOff = LoadIcon("icon_off.bin");
        Add();
    }

    /// <summary>(Re)adds the icon; also used after Explorer restarts.</summary>
    public static void Add()
    {
        var data = Build();
        _added = Win32.Shell_NotifyIcon(Win32.NIM_ADD, ref data);
    }

    public static void Remove()
    {
        if (!_added) return;
        var data = Build();
        Win32.Shell_NotifyIcon(Win32.NIM_DELETE, ref data);
        _added = false;
    }

    /// <summary>Refreshes tooltip and on/off icon.</summary>
    public static void Update()
    {
        var data = Build();
        Win32.Shell_NotifyIcon(Win32.NIM_MODIFY, ref data);
    }

    /// <summary>Called on hover: refresh only if the numbers moved (e.g. midnight rollover).</summary>
    public static void RefreshTipIfChanged()
    {
        if (TipText() != _lastTip) Update();
    }

    public static string TipText() =>
        $"KeyLangFix v{Version}{(SettingsStore.Enabled ? "" : " (disabled)")}\n" +
        $"Switches: {StatsStore.Total} total\n" +
        $"This week: {StatsStore.ThisWeek}\n" +
        $"Today: {StatsStore.Today}";

    private static NOTIFYICONDATAW Build()
    {
        var d = new NOTIFYICONDATAW
        {
            cbSize = (uint)sizeof(NOTIFYICONDATAW),
            hWnd = _hwnd,
            uID = 1,
            uFlags = Win32.NIF_MESSAGE | Win32.NIF_ICON | Win32.NIF_TIP,
            uCallbackMessage = CallbackMessage,
            hIcon = SettingsStore.Enabled ? _iconOn : _iconOff,
        };
        _lastTip = TipText();
        int n = Math.Min(_lastTip.Length, 127);
        for (int i = 0; i < n; i++) d.szTip[i] = _lastTip[i];
        d.szTip[n] = '\0';
        return d;
    }

    public static void ShowMenu()
    {
        nint menu = Win32.CreatePopupMenu();
        Win32.AppendMenu(menu, Win32.MF_STRING, CmdToggle, SettingsStore.Enabled ? "Disable" : "Enable");
        Win32.AppendMenu(menu, Win32.MF_STRING, CmdSettings, "Settings...");
        Win32.AppendMenu(menu, Win32.MF_STRING, CmdResetStats, "Reset statistics...");
        Win32.AppendMenu(menu, Win32.MF_SEPARATOR, 0, null);
        Win32.AppendMenu(menu, Win32.MF_STRING, CmdQuit, "Quit");

        Win32.GetCursorPos(out var pt);
        Win32.SetForegroundWindow(_hwnd);   // required so the menu closes when clicking elsewhere
        int cmd = Win32.TrackPopupMenu(menu, Win32.TPM_RETURNCMD | Win32.TPM_RIGHTBUTTON | Win32.TPM_BOTTOMALIGN,
            pt.X, pt.Y, 0, _hwnd, 0);
        Win32.PostMessage(_hwnd, Win32.WM_NULL, 0, 0);
        Win32.DestroyMenu(menu);
        if (cmd != 0) Win32.PostMessage(_hwnd, Win32.WM_COMMAND, (nuint)cmd, 0);
    }

    private static nint LoadIcon(string resource)
    {
        using var s = typeof(TrayIcon).Assembly.GetManifestResourceStream(resource);
        if (s == null) return 0;
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        var bytes = ms.ToArray();
        fixed (byte* p = bytes)
        {
            int size = Win32.GetSystemMetrics(49 /* SM_CXSMICON */);
            return Win32.CreateIconFromResourceEx(p, (uint)bytes.Length, true, 0x00030000, size, size, 0);
        }
    }
}
