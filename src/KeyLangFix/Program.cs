using System;
using System.Runtime.InteropServices;
using KeyLangFix.Core;
using KeyLangFix.Hooks;
using KeyLangFix.Native;
using KeyLangFix.Services;
using KeyLangFix.Tray;
using KeyLangFix.Ui;

namespace KeyLangFix;

internal static unsafe class Program
{
    private const string WindowClass = "KeyLangFixHiddenWindow";
    private const nuint WatchdogTimerId = 2;
    private static uint _taskbarCreated;

    [STAThread]
    private static void Main()
    {
        // One instance per user session.
        Win32.CreateMutex(0, false, @"Local\KeyLangFix.SingleInstance");
        if (Marshal.GetLastWin32Error() == 183 /* ERROR_ALREADY_EXISTS */) return;

        SettingsStore.MigrateFromAutoLang();
        SettingsStore.Load();
        StatsStore.Load();
        SoundPlayer.Init();
        Theme.EnableThemedMenus();
        _ = Detection.Models.All;   // load models once, up front

        nint hInstance = Win32.GetModuleHandle(null);
        var wc = new WNDCLASSEXW
        {
            cbSize = (uint)sizeof(WNDCLASSEXW),
            lpfnWndProc = (nint)(delegate* unmanaged<nint, uint, nuint, nint, nint>)&WndProc,
            hInstance = hInstance,
            lpszClassName = Marshal.StringToHGlobalUni(WindowClass),
        };
        Win32.RegisterClassEx(ref wc);

        // A hidden top-level window (not message-only) so it receives the TaskbarCreated broadcast.
        nint hwnd = Win32.CreateWindowEx(0, WindowClass, "KeyLangFix", Win32.WS_POPUP, 0, 0, 0, 0, 0, 0, hInstance, 0);
        if (hwnd == 0) return;
        _taskbarCreated = Win32.RegisterWindowMessage("TaskbarCreated");

        Engine.MainWindow = hwnd;
        TrayIcon.Create(hwnd);
        HookManager.Install();
        if (Environment.GetCommandLineArgs().Length > 1 && Environment.GetCommandLineArgs()[1] == "--settings") SettingsWindow.Show();
        HookManager.LastCallTick = (uint)Environment.TickCount;
        Win32.SetTimer(hwnd, WatchdogTimerId, 4000, 0);

        while (Win32.GetMessage(out var msg, 0, 0, 0) > 0)
        {
            if (SettingsWindow.Handle != 0 && Win32.IsDialogMessage(SettingsWindow.Handle, ref msg)) continue;
            Win32.TranslateMessage(ref msg);
            Win32.DispatchMessage(ref msg);
        }

        HookManager.Uninstall();
        TrayIcon.Remove();
    }

    [UnmanagedCallersOnly]
    private static nint WndProc(nint hwnd, uint msg, nuint wParam, nint lParam)
    {
        try
        {
            if (msg == TrayIcon.CallbackMessage)
            {
                switch ((uint)lParam)
                {
                    case Win32.WM_RBUTTONUP: TrayIcon.ShowMenu(); break;
                    case Win32.WM_LBUTTONDBLCLK: SettingsWindow.Show(); break;
                    case Win32.WM_MOUSEMOVE: TrayIcon.RefreshTipIfChanged(); break;
                }
                return 0;
            }
            if (msg == _taskbarCreated && msg != 0)
            {
                TrayIcon.Add();
                return 0;
            }
            if (msg == Engine.SwitchedMessage)
            {
                StatsStore.RecordSwitch();
                if (SettingsStore.PlaySound) SoundPlayer.Play();
                TrayIcon.Update();
                return 0;
            }
            switch (msg)
            {
                case Win32.WM_TIMER when wParam == WatchdogTimerId:
                    HookManager.CheckAlive();
                    return 0;
                case Win32.WM_TIMER when wParam == Engine.ConvertTimerId:
                    Engine.OnConvertTimer();
                    return 0;
                case Win32.WM_COMMAND:
                    switch ((int)(wParam & 0xFFFF))
                    {
                        case TrayIcon.CmdToggle:
                            Engine.SetEnabled(!SettingsStore.Enabled);
                            TrayIcon.Update();
                            break;
                        case TrayIcon.CmdResetStats:
                            // MB_YESNO | MB_ICONQUESTION | MB_DEFBUTTON2 | MB_TOPMOST; IDYES = 6
                            if (Win32.MessageBox(hwnd, "Reset all switch statistics (total, this week, today)?" + Environment.NewLine + "This cannot be undone.",
                                    "KeyLangFix", 0x4 | 0x20 | 0x100 | 0x40000) == 6)
                            {
                                StatsStore.Reset();
                                TrayIcon.Update();
                            }
                            break;
                        case TrayIcon.CmdSettings:
                            SettingsWindow.Show();
                            break;
                        case TrayIcon.CmdQuit:
                            Win32.DestroyWindow(hwnd);
                            break;
                    }
                    return 0;
                case Win32.WM_DESTROY:
                    Win32.PostQuitMessage(0);
                    return 0;
            }
        }
        catch { }
        return Win32.DefWindowProc(hwnd, msg, wParam, lParam);
    }
}
