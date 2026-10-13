using System;
using System.Collections.Generic;
using System.IO;

namespace KeyLangFix.Services;

/// <summary>User settings in %APPDATA%\KeyLangFix\settings.ini (key=value lines).</summary>
internal static class SettingsStore
{
    public const int DefaultConfidenceLevel = 3, DefaultMinLetters = 4, DefaultSoundVolume = 40;
    public const bool DefaultPlaySound = true, DefaultConvertText = true, DefaultAutoStart = false;

    public static bool Enabled = true;
    /// <summary>Confidence required before switching: 1 (eager) to 5 (very strict).</summary>
    public static int ConfidenceLevel = DefaultConfidenceLevel;
    public static int MinLetters = DefaultMinLetters;
    public static bool PlaySound = DefaultPlaySound;
    /// <summary>Switch-sound volume, 0-100 (100 = the full level of the embedded WAV).</summary>
    public static int SoundVolume = DefaultSoundVolume;
    public static bool ConvertText = DefaultConvertText;

    public static string Directory => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KeyLangFix");

    private static string FilePath => System.IO.Path.Combine(Directory, "settings.ini");

    /// <summary>One-time copy of settings and statistics from the earlier "AutoLang" data folder.</summary>
    public static void MigrateFromAutoLang()
    {
        try
        {
            var old = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AutoLang");
            if (!System.IO.Directory.Exists(old) || System.IO.Directory.Exists(Directory)) return;
            System.IO.Directory.CreateDirectory(Directory);
            foreach (var name in new[] { "settings.ini", "stats.ini" })
            {
                var src = System.IO.Path.Combine(old, name);
                if (File.Exists(src)) File.Copy(src, System.IO.Path.Combine(Directory, name));
            }
        }
        catch { /* start fresh */ }
    }

    public static void Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return;
            foreach (var (k, v) in IniFile.Read(FilePath))
            {
                switch (k)
                {
                    case "enabled": Enabled = v != "0"; break;
                    case "level": ConfidenceLevel = Math.Clamp(Parse(v, 3), 1, 5); break;
                    case "minLetters": MinLetters = Math.Clamp(Parse(v, 4), 3, 8); break;
                    case "sound": PlaySound = v != "0"; break;
                    case "volume": SoundVolume = Math.Clamp(Parse(v, 40), 0, 100); break;
                    case "convert": ConvertText = v != "0"; break;
                }
            }
        }
        catch { /* corrupt or unreadable file: keep defaults */ }
    }

    public static void Save()
    {
        try
        {
            IniFile.Write(FilePath, new List<(string, string)>
            {
                ("enabled", Enabled ? "1" : "0"),
                ("level", ConfidenceLevel.ToString()),
                ("minLetters", MinLetters.ToString()),
                ("sound", PlaySound ? "1" : "0"),
                ("volume", SoundVolume.ToString()),
                ("convert", ConvertText ? "1" : "0"),
            });
        }
        catch { /* read-only profile: settings stay in memory */ }
    }

    private static int Parse(string s, int fallback) => int.TryParse(s, out var n) ? n : fallback;
}

internal static class IniFile
{
    public static IEnumerable<(string Key, string Value)> Read(string path)
    {
        foreach (var line in File.ReadLines(path))
        {
            int eq = line.IndexOf('=');
            if (eq > 0) yield return (line[..eq].Trim(), line[(eq + 1)..].Trim());
        }
    }

    public static void Write(string path, IEnumerable<(string Key, string Value)> items)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        using (var w = new StreamWriter(tmp))
            foreach (var (k, v) in items) w.WriteLine($"{k}={v}");
        File.Move(tmp, path, overwrite: true);
    }
}
