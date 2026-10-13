using System;
using System.IO;
using System.Runtime.InteropServices;
using KeyLangFix.Native;

namespace KeyLangFix.Services;

/// <summary>Plays the embedded WAV from native memory, scaled to the volume setting.</summary>
internal static class SoundPlayer
{
    private const int WavHeaderBytes = 44;   // plain PCM16 mono WAV written by tools/make_assets.py

    private static byte[]? _base;
    private static nint _buffer;
    private static int _bufferVolume = -1;

    public static void Init()
    {
        using var s = typeof(SoundPlayer).Assembly.GetManifestResourceStream("switch.wav");
        if (s == null) return;
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        _base = ms.ToArray();
    }

    /// <summary>Plays at <paramref name="volume"/> (0-100), or at the saved setting when null.</summary>
    public static void Play(int? volume = null)
    {
        if (_base == null) return;
        int v = Math.Clamp(volume ?? SettingsStore.SoundVolume, 0, 100);
        if (v == 0) return;
        Prepare(v);
        Win32.PlaySound(_buffer, 0, Win32.SND_MEMORY | Win32.SND_ASYNC | Win32.SND_NODEFAULT);
    }

    private static void Prepare(int volume)
    {
        if (_buffer != 0 && _bufferVolume == volume) return;
        var b = (byte[])_base!.Clone();
        double gain = volume / 100.0;
        for (int i = WavHeaderBytes; i + 1 < b.Length; i += 2)
        {
            short sample = (short)(BitConverter.ToInt16(b, i) * gain);
            b[i] = (byte)sample;
            b[i + 1] = (byte)(sample >> 8);
        }
        // A previous buffer may still be playing asynchronously, so a new one is allocated and the
        // old one left alone (a few KB, and only when the volume changes).
        nint p = Marshal.AllocHGlobal(b.Length);
        Marshal.Copy(b, 0, p, b.Length);
        _buffer = p;
        _bufferVolume = volume;
    }
}
