using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace KeyLangFix.Core;

/// <summary>
/// Tells whether the focused UI element is a masked (password) field, for controls the classic
/// ES_PASSWORD check cannot see: browsers, WPF, UWP/WinUI, Electron. Uses UI Automation's IsPassword
/// property. The query is cross-process and can be slow, so it runs on a worker thread, started by a
/// focus-change WinEvent; the keyboard hook only reads <see cref="Masked"/>. No answer = not masked.
/// COM is called through raw vtable pointers (no [ComImport]), which also works trimmed and AOT-compiled.
/// </summary>
internal static unsafe partial class PasswordProbe
{
    private const uint EVENT_OBJECT_FOCUS = 0x8005, WINEVENT_OUTOFCONTEXT = 0;
    private const long RecheckMs = 1000;

    private static readonly Guid ClsidAutomation = new(0xff48dba4, 0x60ef, 0x4201, 0xaa, 0x87, 0x54, 0x10, 0x3e, 0xef, 0x59, 0x4e);
    private static readonly Guid IidAutomation = new(0x30cbe57d, 0xd9d0, 0x452a, 0xab, 0x13, 0x7a, 0xc5, 0xac, 0x48, 0x25, 0xee);

    private static readonly AutoResetEvent Signal = new(false);
    private static nint _hook;
    private static Thread? _thread;
    private static volatile bool _run;
    private static volatile bool _masked;
    private static long _stamp;

    /// <summary>True while the focused element reported itself as a password field.</summary>
    public static bool Masked => _masked;

    public static void SetEnabled(bool on)
    {
        if (on) Start(); else Stop();
    }

    /// <summary>Asks for a fresh answer if the current one is old (the focus event may have been missed).</summary>
    public static void Refresh()
    {
        if (_run && Environment.TickCount64 - Interlocked.Read(ref _stamp) > RecheckMs) Signal.Set();
    }

    private static void Start()
    {
        if (_run) return;
        try
        {
            _run = true;
            _hook = SetWinEventHook(EVENT_OBJECT_FOCUS, EVENT_OBJECT_FOCUS, 0,
                (nint)(delegate* unmanaged[Stdcall]<nint, uint, nint, int, int, uint, uint, void>)&OnFocusEvent,
                0, 0, WINEVENT_OUTOFCONTEXT);
            _thread = new Thread(Worker) { IsBackground = true, Name = "KeyLangFix.PasswordProbe" };
            _thread.SetApartmentState(ApartmentState.MTA);
            _thread.Start();
            Signal.Set();
        }
        catch { Stop(); }
    }

    private static void Stop()
    {
        _run = false;
        _masked = false;
        if (_hook != 0) { UnhookWinEvent(_hook); _hook = 0; }
        Signal.Set();
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvStdcall) })]
    private static void OnFocusEvent(nint hook, uint ev, nint hwnd, int idObject, int idChild, uint thread, uint time)
    {
        _masked = false;      // focus moved: the old answer no longer applies
        Interlocked.Exchange(ref _stamp, 0);
        Signal.Set();
    }

    private static void Worker()
    {
        if (CoInitializeEx(0, 0 /* COINIT_MULTITHREADED */) < 0) return;
        nint uia = 0;
        try
        {
            while (true)
            {
                Signal.WaitOne();
                if (!_run) return;
                if (uia == 0) uia = Create();
                if (uia == 0) { _masked = false; continue; }
                bool masked = QueryFocusedIsPassword(uia);
                if (!_run) return;
                _masked = masked;
                Interlocked.Exchange(ref _stamp, Environment.TickCount64);
            }
        }
        catch { _masked = false; }
        finally
        {
            if (uia != 0) Release(uia);
            CoUninitialize();
        }
    }

    private static nint Create()
    {
        nint p;
        Guid clsid = ClsidAutomation, iid = IidAutomation;
        return CoCreateInstance(&clsid, 0, 1 /* CLSCTX_INPROC_SERVER */, &iid, &p) >= 0 ? p : 0;
    }

    /// <summary>One synchronous query of the current focus. Exposed for tests.</summary>
    internal static bool QueryFocusedIsPassword(nint uia)
    {
        nint element = 0;
        // IUIAutomation::GetFocusedElement = vtable slot 8
        if (((delegate* unmanaged[Stdcall]<nint, nint*, int>)Slot(uia, 8))(uia, &element) < 0 || element == 0) return false;
        try
        {
            int isPassword = 0;
            // IUIAutomationElement::get_CurrentIsPassword = vtable slot 35
            int hr = ((delegate* unmanaged[Stdcall]<nint, int*, int>)Slot(element, 35))(element, &isPassword);
            return hr >= 0 && isPassword != 0;
        }
        finally { Release(element); }
    }

    internal static nint CreateAutomation() => Create();

    private static nint Slot(nint com, int index) => (*(nint**)com)[index];

    private static void Release(nint com) => ((delegate* unmanaged[Stdcall]<nint, uint>)Slot(com, 2))(com);

    [LibraryImport("user32.dll")] private static partial nint SetWinEventHook(uint min, uint max, nint mod, nint proc, uint pid, uint tid, uint flags);
    [LibraryImport("user32.dll")] private static partial int UnhookWinEvent(nint hook);
    [LibraryImport("ole32.dll")] private static partial int CoInitializeEx(nint reserved, uint model);
    [LibraryImport("ole32.dll")] private static partial void CoUninitialize();
    [LibraryImport("ole32.dll")] private static partial int CoCreateInstance(Guid* clsid, nint outer, uint ctx, Guid* iid, nint* result);
}
