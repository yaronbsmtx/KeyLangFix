using System;
using System.Text;
using KeyLangFix.Detection;
using KeyLangFix.Input;
using KeyLangFix.Native;
using KeyLangFix.Services;

namespace KeyLangFix.Core;

/// <summary>
/// Heart of the app. Receives every key from the low-level hook (on the UI thread), tracks the
/// word(s) being typed, asks the detector whether they look like another installed layout, and then
/// switches the layout and rewrites the already-typed text.
/// </summary>
internal static unsafe class Engine
{
    /// <summary>Tag on our own injected input so we never react to it.</summary>
    public const nuint Magic = 0x4155_4C47;
    public const nuint ConvertTimerId = 1;
    private const uint ConvertPollMs = 10;
    private const long LayoutWaitMs = 500;
    private const long CooldownMs = 1500;
    private const long RevertWindowMs = 3000;
    private const double PenaltyStep = 0.15, PenaltyMax = 0.45;
    private const double PenaltyFadeMs = 180_000.0;

    /// <summary>Test hook: lets automated tests drive the engine with synthetic keys (KEYLANGFIX_TEST=1).</summary>
    private static readonly bool AcceptInjected = Environment.GetEnvironmentVariable("KEYLANGFIX_TEST") == "1";

#if DEBUG
    /// <summary>Trace switch: env KEYLANGFIX_TEST=1 or a file %APPDATA%\KeyLangFix\debug.on (checked at startup). Debug builds only.</summary>
    private static readonly bool Trace = AcceptInjected
        || System.IO.File.Exists(System.IO.Path.Combine(SettingsStore.Directory, "debug.on"));
#else
    private const bool Trace = false;
#endif

    // Calls (and their argument expressions) are removed by the compiler outside Debug builds.
    [System.Diagnostics.Conditional("DEBUG")]
    private static void Dbg(string m)
    {
#if DEBUG
        if (Trace) System.IO.File.AppendAllText(System.IO.Path.Combine(SettingsStore.Directory, "debug.log"), Environment.TickCount64 % 100000 + " " + m + Environment.NewLine, Encoding.UTF8);
#endif
    }

    public static nint MainWindow;
    public const uint SwitchedMessage = Win32.WM_APP + 2;

    private static readonly WordBuffer Buf = new();

    // Current typing context
    private static nint _focus;
    private static bool _focusIsPassword;
    private static nint _bufHkl;
    private static NgramModel? _curModel;
    private static readonly NgramModel[] AltModels = new NgramModel[WordBuffer.MaxAlts];
    private static readonly nint[] AltHkls = new nint[WordBuffer.MaxAlts];
    private static int _altCount;
    private static long _candidatesStamp;

    // After a switch
    private static long _cooldownUntil;
    private static long _switchTick;
    private static nint _switchedFrom;
    private static double _penalty;
    private static long _penaltyTick;

    // Conversion waiting for its timer
    private static bool _pending;
    private static int _pendingBack;
    private static readonly StringBuilder PendingText = new();   // trace only
    private static readonly System.Collections.Generic.List<(ushort Scan, bool Shift)> PendingKeys = new();
    private static nint _pendingHkl;
    private static nint _pendingFocus;
    private static long _pendingSince;
    private static readonly bool[] SwallowedDown = new bool[256];

    private static readonly INPUT[] InputBuf = new INPUT[2 * (WordBuffer.Capacity + 64) + 2 * 64];

    public static void Reset()
    {
        Buf.Clear();
        FlushPending();
    }

    public static void SetEnabled(bool enabled)
    {
        SettingsStore.Enabled = enabled;
        SettingsStore.Save();
        Reset();
    }

    public static void OnMouseDown() => Reset();

    /// <summary>Returns true if the key must be swallowed.</summary>
    public static bool OnKey(uint vk, uint scan, uint flags, bool down)
    {
        if (!SettingsStore.Enabled) return false;
        if ((flags & Win32.LLKHF_INJECTED) != 0 && !AcceptInjected) return false;

        if (!down)
        {
            if (vk < 256 && SwallowedDown[vk]) { SwallowedDown[vk] = false; return true; }
            return false;
        }

        if (IsModifier(vk)) return false;
        if (vk == Win32.VK_LWIN || vk == Win32.VK_RWIN) { Reset(); return false; }

        if (_pending) return OnKeyWhilePending(vk, scan, flags);

        if (IsDown(Win32.VK_CONTROL) || IsDown(Win32.VK_MENU))
        {
            if (vk == Win32.VK_Z && Environment.TickCount64 - _switchTick < RevertWindowMs) AddPenalty();
            Buf.Clear();
            return false;
        }

        // Where is the user typing?
        var gti = new GUITHREADINFO { cbSize = (uint)sizeof(GUITHREADINFO) };
        if (!Win32.GetGUIThreadInfo(0, ref gti)) return false;
        nint target = gti.hwndFocus != 0 ? gti.hwndFocus : gti.hwndActive;
        if (target == 0) return false;
        if (target != _focus)
        {
            Buf.Clear();
            _focus = target;
            _focusIsPassword = IsPasswordField(target);
        }
        if (_focusIsPassword) { Buf.Clear(); return false; }

        uint tid = Win32.GetWindowThreadProcessId(target, out _);
        nint hkl = Win32.GetKeyboardLayout(tid);
        long now = Environment.TickCount64;
        if (hkl != _bufHkl)
        {
            if (now - _switchTick < RevertWindowMs && hkl == _switchedFrom) AddPenalty();
            Buf.Clear();
            _bufHkl = hkl;
            RefreshCandidates(hkl);
        }
        else if (Buf.Length == 0 && now - _candidatesStamp > 5000)
        {
            RefreshCandidates(hkl);
        }
        if (_curModel == null || _altCount == 0) return false;

        if (vk == Win32.VK_BACK) { Buf.Backspace(); return false; }
        if (!IsPrintable(vk)) { Buf.Clear(); return false; }

        byte* state = stackalloc byte[256];
        FillKeyState(state);
        char cur = Translate(vk, scan, hkl, state);
        if (cur == '\0') { Buf.Clear(); return false; }   // dead key, ligature or no char

        char* alts = stackalloc char[WordBuffer.MaxAlts];
        for (int i = 0; i < _altCount; i++)
        {
            char a = Translate(vk, scan, AltHkls[i], state, remap: true);
            alts[i] = a == '\0' ? cur : a;
        }
        Dbg($"key vk={vk:X} scan={scan:X} hkl={hkl:X} tid={tid} focus={target:X} fg={Win32.GetForegroundWindow():X} cur='{cur}' alt='{alts[0]}' buf='{Buf.Cur.ToString()}'");
        Buf.Add(cur, _curModel, new ReadOnlySpan<char>(alts, _altCount), AltModels.AsSpan(0, _altCount),
            ScanOf(scan, flags), IsDown(Win32.VK_SHIFT));

        if (now < _cooldownUntil || Buf.Length == 0) return false;
        if (Decide(hkl, now, target))
        {
            // The triggering key is rewritten along with the rest, so it must not reach the app
            // (it would race with the layout change).
            SwallowedDown[vk] = true;
            return true;
        }
        return false;
    }

    /// <summary>True if a conversion was scheduled and the current key must be swallowed.</summary>
    private static bool Decide(nint curHkl, long now, nint target)
    {
        double threshold = DetectorOptions.ThresholdFor(SettingsStore.ConfidenceLevel) + CurrentPenalty(now);
        var opt = new DetectorOptions(threshold, SettingsStore.MinLetters);
        bool complete = Buf.Cur[^1] == ' ';

        int best = -1, bestStart = 0;
        double bestLlr = double.NegativeInfinity;
        for (int i = 0; i < _altCount; i++)
        {
            var v = Detector.Evaluate(_curModel!, AltModels[i], Buf.Cur, Buf.Alt(i), complete, opt);
            if (v.Switch && v.Llr > bestLlr) { best = i; bestStart = v.Start; bestLlr = v.Llr; }
            if (Trace && Buf.Length >= 3) Dbg($"  eval alt={i} thr={threshold:F2} llr={v.Llr:F2} switch={v.Switch}");
        }
        return best >= 0 && Trigger(best, bestStart, curHkl, now, target);
    }

    private static bool Trigger(int alt, int start, nint curHkl, long now, nint target)
    {
        nint targetHkl = AltHkls[alt];
        Dbg($"TRIGGER cur='{Buf.Cur.ToString()}' alt='{Buf.AltRaw(alt).ToString()}' start={start} back={Buf.Length - start}");
        nint fg = Win32.GetForegroundWindow();
        if (fg != 0) Win32.PostMessage(fg, Win32.WM_INPUTLANGCHANGEREQUEST, 0, targetHkl);

        _switchedFrom = curHkl;
        _switchTick = now;
        _cooldownUntil = now + CooldownMs;

        bool convert = SettingsStore.ConvertText && start < Buf.Length;
        if (convert)
        {
            _pendingBack = Buf.Length - start - 1;   // the last key never reached the app
            _pendingFocus = target;
            _pendingSince = now;
            PendingText.Clear();
            PendingText.Append(Buf.AltRaw(alt)[start..]);
            PendingKeys.Clear();
            for (int i = start; i < Buf.Length; i++) PendingKeys.Add((Buf.Scan(i), Buf.Shift(i)));
            _pendingHkl = targetHkl;
            _pending = true;
            Win32.SetTimer(MainWindow, ConvertTimerId, ConvertPollMs, 0);
        }

        Buf.Clear();
        // Disk, audio and tray work must not run inside the hook: Windows silently removes a
        // low-level hook whose callback is too slow.
        Win32.PostMessage(MainWindow, SwitchedMessage, 0, 0);
        return convert;
    }

    /// <summary>Keys typed in the few ms before we rewrite the text join the rewrite, in the new layout.</summary>
    private static bool OnKeyWhilePending(uint vk, uint scan, uint flags)
    {
        if (vk == Win32.VK_BACK)
        {
            if (PendingKeys.Count > 0)
            {
                PendingKeys.RemoveAt(PendingKeys.Count - 1);
                if (PendingText.Length > 0) PendingText.Length--;
            }
            else _pendingBack++;     // erase one more char of the old text
            SwallowedDown[vk] = true;
            return true;
        }
        if (IsPrintable(vk) && !IsDown(Win32.VK_CONTROL) && !IsDown(Win32.VK_MENU))
        {
            byte* state = stackalloc byte[256];
            FillKeyState(state);
            char c = Translate(vk, scan, _pendingHkl, state, remap: true);
            if (c != '\0')
            {
                PendingText.Append(c);
                PendingKeys.Add((ScanOf(scan, flags), IsDown(Win32.VK_SHIFT)));
                SwallowedDown[vk] = true;
                return true;
            }
        }
        FlushPending();   // other key: finish the rewrite first so it lands before this key
        return false;
    }

    /// <summary>Called from the window procedure when the conversion timer fires.</summary>
    public static void OnConvertTimer()
    {
        if (!_pending) { Win32.KillTimer(MainWindow, ConvertTimerId); return; }
        // Wait until the app has really changed layout, so the rewrite cannot race with it.
        uint tid = Win32.GetWindowThreadProcessId(_pendingFocus, out _);
        bool ready = tid != 0 && Win32.GetKeyboardLayout(tid) == _pendingHkl;
        if (!ready && Environment.TickCount64 - _pendingSince < LayoutWaitMs) return;
        FinishConversion(ready);
    }

    private static void FlushPending()
    {
        if (_pending) FinishConversion(false);
    }

    private static void FinishConversion(bool layoutReady)
    {
        Win32.KillTimer(MainWindow, ConvertTimerId);
        _pending = false;
        Dbg($"CONVERT back={_pendingBack} text='{PendingText}' layoutReady={layoutReady} waited={Environment.TickCount64 - _pendingSince}ms");

        // Replay the physical keys (scan codes) now that the app is on the target layout. To the
        // app this is indistinguishable from the user typing, unlike injected Unicode characters,
        // which many text engines drop or repeat when they arrive in a burst.
        int back = Math.Min(_pendingBack, WordBuffer.Capacity + 64);
        int n = 0;
        for (int i = 0; i < back; i++)
        {
            InputBuf[n++] = ScanKey(0x0E, false);
            InputBuf[n++] = ScanKey(0x0E, true);
        }
        bool shiftDown = false;
        foreach (var (scan, shift) in PendingKeys)
        {
            if (n + 6 > InputBuf.Length) break;
            if (shift != shiftDown)
            {
                InputBuf[n++] = ScanKey(0x2A, !shift);
                shiftDown = shift;
            }
            InputBuf[n++] = ScanKey(scan, false);
            InputBuf[n++] = ScanKey(scan, true);
        }
        if (shiftDown) InputBuf[n++] = ScanKey(0x2A, true);
        PendingKeys.Clear();
        PendingText.Clear();
        if (n == 0) return;
        fixed (INPUT* p = InputBuf) Win32.SendInput((uint)n, p, sizeof(INPUT));
    }

    private static ushort ScanOf(uint scan, uint flags) => (ushort)((scan & 0xFF) | ((flags & 1) << 8));

    private static INPUT ScanKey(ushort scanWithExt, bool up) => new()
    {
        type = Win32.INPUT_KEYBOARD,
        wScan = (ushort)(scanWithExt & 0xFF),
        dwFlags = Win32.KEYEVENTF_SCANCODE | (up ? Win32.KEYEVENTF_KEYUP : 0) | ((scanWithExt & 0x100) != 0 ? Win32.KEYEVENTF_EXTENDEDKEY : 0),
        dwExtraInfo = Magic,
    };

    private static INPUT Key(ushort vk, ushort scan, uint flags) => new()
    {
        type = Win32.INPUT_KEYBOARD,
        wVk = vk,
        wScan = scan,
        dwFlags = flags,
        dwExtraInfo = Magic,
    };

    private static void CancelPending()
    {
        if (!_pending) return;
        _pending = false;
        PendingText.Clear();
        PendingKeys.Clear();
        Win32.KillTimer(MainWindow, ConvertTimerId);
    }

    // ---- layouts -----------------------------------------------------------------------------

    private static ushort Primary(nint hkl) => (ushort)((ulong)hkl & 0x3FF);

    private static void RefreshCandidates(nint curHkl)
    {
        _candidatesStamp = Environment.TickCount64;
        _curModel = Models.Get(Primary(curHkl));
        _altCount = 0;
        if (_curModel == null) return;

        var list = new nint[16];
        int n = Win32.GetKeyboardLayoutList(list.Length, list);
        for (int i = 0; i < n && _altCount < WordBuffer.MaxAlts; i++)
        {
            ushort lang = Primary(list[i]);
            if (lang == Primary(curHkl)) continue;
            var m = Models.Get(lang);
            if (m == null) continue;
            bool dup = false;
            for (int j = 0; j < _altCount; j++) if (AltModels[j] == m) dup = true;
            if (dup) continue;
            AltModels[_altCount] = m;
            AltHkls[_altCount] = list[i];
            _altCount++;
        }
    }

    // ---- key translation ---------------------------------------------------------------------

    private static bool IsDown(int vk) => (Win32.GetAsyncKeyState(vk) & 0x8000) != 0;

    private static void FillKeyState(byte* state)
    {
        new Span<byte>(state, 256).Clear();
        if (IsDown(Win32.VK_SHIFT)) state[Win32.VK_SHIFT] = 0x80;
        if ((Win32.GetKeyState(Win32.VK_CAPITAL) & 1) != 0) state[Win32.VK_CAPITAL] = 1;
        if ((Win32.GetKeyState(Win32.VK_NUMLOCK) & 1) != 0) state[Win32.VK_NUMLOCK] = 1;
    }

    /// <summary>The char this physical key gives on <paramref name="hkl"/>; '\0' if none or a dead key.</summary>
    private static char Translate(uint vk, uint scan, nint hkl, byte* state, bool remap = false)
    {
        if (remap && scan != 0)
        {
            uint mapped = Win32.MapVirtualKeyExW(scan, 3 /* MAPVK_VSC_TO_VK_EX */, hkl);
            if (mapped != 0) vk = mapped;
        }
        char* buf = stackalloc char[4];
        // flag 0x4: do not change the keyboard state (dead keys)
        int n = Win32.ToUnicodeEx(vk, scan, state, buf, 4, 4, hkl);
        return n == 1 && buf[0] >= ' ' ? buf[0] : '\0';
    }

    private static bool IsModifier(uint vk) => vk is Win32.VK_SHIFT or Win32.VK_CONTROL or Win32.VK_MENU
        or Win32.VK_LSHIFT or Win32.VK_RSHIFT or Win32.VK_LCONTROL or Win32.VK_RCONTROL or Win32.VK_LMENU
        or Win32.VK_RMENU or Win32.VK_CAPITAL or Win32.VK_NUMLOCK or Win32.VK_SCROLL;

    private static bool IsPrintable(uint vk) =>
        vk == Win32.VK_SPACE
        || (vk >= 0x30 && vk <= 0x39) || (vk >= 0x41 && vk <= 0x5A)
        || (vk >= 0x60 && vk <= 0x6F)
        || (vk >= 0xBA && vk <= 0xC0) || (vk >= 0xDB && vk <= 0xDF) || vk == 0xE2;

    private static bool IsPasswordField(nint hwnd)
    {
        char* cls = stackalloc char[64];
        int len = Win32.GetClassName(hwnd, cls, 64);
        if (len <= 0) return false;
        var name = new ReadOnlySpan<char>(cls, len);
        if (!name.Contains("Edit", StringComparison.OrdinalIgnoreCase)) return false;
        return ((ulong)Win32.GetWindowLongPtr(hwnd, Win32.GWL_STYLE) & Win32.ES_PASSWORD) != 0;
    }

    // ---- false-positive feedback -------------------------------------------------------------

    private static void AddPenalty()
    {
        _penalty = Math.Min(PenaltyMax, CurrentPenalty(Environment.TickCount64) + PenaltyStep);
        _penaltyTick = Environment.TickCount64;
        Dbg($"PENALTY now={_penalty:F2}");
        _switchTick = 0;   // count each revert once
    }

    /// <summary>Extra confidence demanded after the user undid a switch; fades over three minutes.</summary>
    private static double CurrentPenalty(long now)
    {
        if (_penalty <= 0) return 0;
        double fade = 1.0 - (now - _penaltyTick) / PenaltyFadeMs;
        return fade > 0 ? _penalty * fade : 0;
    }
}
