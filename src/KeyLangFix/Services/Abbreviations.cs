using System;
using System.Collections.Generic;
using System.IO;
using KeyLangFix.Detection;

namespace KeyLangFix.Services;

/// <summary>
/// Words the user types as they are (ctrl, dll, msg, ...). They look like Hebrew words on the other layout
/// but are not words in either language's list, so the detector leaves them alone. One entry per line in
/// %APPDATA%\KeyLangFix\abbreviations.txt; a missing file means the built-in list.
/// </summary>
internal static class Abbreviations
{
    private const int MinPrefix = 3;

    public static readonly string[] Defaults =
    {
        "ctrl", "alt", "esc", "del", "ins", "pgup", "pgdn", "bksp", "prtsc", "msg", "cfg", "conf", "config", "dll", "exe",
        "msi", "bat", "cmd", "ps", "api", "sdk", "cli", "gui", "ide", "sql", "url", "uri", "http", "https", "html", "css",
        "xml", "json", "yaml", "toml", "ini", "csv", "tsv", "pdf", "png", "jpg", "jpeg", "gif", "svg", "tcp", "udp", "dns",
        "dhcp", "ssh", "ftp", "sftp", "smtp", "imap", "pop", "vpn", "lan", "wan", "wifi", "nfc", "rdp", "vnc", "sso", "mfa",
        "otp", "ldap", "jwt", "oauth", "rest", "soap", "grpc", "wcf", "wpf", "uwp", "msmq", "iis", "git", "npm", "npx",
        "php", "cpu", "gpu", "ram", "rom", "ssd", "hdd", "usb", "hdmi", "bios", "uefi", "ios", "src", "bin", "lib", "pkg",
        "env", "dev", "prod", "tmp", "ver", "faq", "asap", "fyi", "etc", "vs", "eg", "ie", "pc", "ui", "ux", "os", "id",
        "ok", "qa", "ci", "cd",
    };

    private static HashSet<string> _words = Build(Defaults);
    private static HashSet<string> _prefixes = Prefixes(_words);

    private static string FilePath => Path.Combine(SettingsStore.Directory, "abbreviations.txt");

    public static void Load()
    {
        try
        {
            if (File.Exists(FilePath)) Set(File.ReadAllLines(FilePath));
        }
        catch { /* unreadable: keep the built-in list */ }
    }

    /// <summary>The list as editable text, one entry per line.</summary>
    public static string Text => string.Join("\r\n", Entries(_words));

    public static string DefaultText => string.Join("\r\n", Entries(Build(Defaults)));

    public static void SaveText(string text)
    {
        var lines = text.Split('\n');
        Set(lines);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllLines(FilePath, Entries(_words));
        }
        catch { /* read-only profile: the list stays in memory */ }
    }

    /// <summary>
    /// True if <paramref name="word"/> is a listed abbreviation, or (while still being typed) the beginning of one.
    /// </summary>
    public static bool Matches(ReadOnlySpan<char> word, bool complete)
    {
        if (word.Length == 0) return false;
        string key = Normalize(word);
        return _words.Contains(key) || (!complete && key.Length >= MinPrefix && _prefixes.Contains(key));
    }

    private static void Set(IEnumerable<string> lines)
    {
        var words = Build(lines);
        _prefixes = Prefixes(words);
        _words = words;
    }

    private static HashSet<string> Build(IEnumerable<string> lines)
    {
        var set = new HashSet<string>();
        foreach (var line in lines)
        {
            var t = line.Trim();
            if (t.Length == 0 || t[0] == '#') continue;
            set.Add(Normalize(t));
        }
        return set;
    }

    private static HashSet<string> Prefixes(HashSet<string> words)
    {
        var set = new HashSet<string>();
        foreach (var w in words)
            for (int k = MinPrefix; k < w.Length; k++) set.Add(w[..k]);
        return set;
    }

    private static List<string> Entries(HashSet<string> words)
    {
        var list = new List<string>(words);
        list.Sort(StringComparer.Ordinal);
        return list;
    }

    /// <summary>Case-insensitive, and Hebrew final-form letters compare as their plain forms.</summary>
    internal static string Normalize(ReadOnlySpan<char> s)
    {
        Span<char> buf = s.Length <= 64 ? stackalloc char[s.Length] : new char[s.Length];
        for (int i = 0; i < s.Length; i++) buf[i] = NgramModel.Normalize(s[i]);
        return new string(buf);
    }
}
