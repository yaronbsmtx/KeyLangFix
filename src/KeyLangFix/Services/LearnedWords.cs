using System;
using System.Collections.Generic;
using System.IO;

namespace KeyLangFix.Services;

internal readonly record struct LearnedEntry(string Word, bool Approved);

/// <summary>
/// Words the app switched away from and the user switched straight back (so the switch was wrong). They are
/// collected unapproved in two lists, by the language of the layout the word was typed on, and shown in the
/// settings. Only approved entries are left alone by the detector. Files: learned-he.txt and learned-en.txt in
/// %APPDATA%\KeyLangFix, one "1|word" (approved) or "0|word" per line.
/// </summary>
internal static class LearnedWords
{
    private const int MinPrefix = 3;

    private static List<LearnedEntry> _he = new(), _en = new();
    private static HashSet<string> _approved = new();

    public static IReadOnlyList<LearnedEntry> Hebrew => _he;
    public static IReadOnlyList<LearnedEntry> English => _en;

    private static string HePath => Path.Combine(SettingsStore.Directory, "learned-he.txt");
    private static string EnPath => Path.Combine(SettingsStore.Directory, "learned-en.txt");

    public static void Load()
    {
        try
        {
            if (File.Exists(HePath)) _he = Parse(File.ReadAllLines(HePath));
            if (File.Exists(EnPath)) _en = Parse(File.ReadAllLines(EnPath));
        }
        catch { /* unreadable: start empty */ }
        Rebuild();
    }

    /// <summary>Adds the word, unapproved, unless it is already listed in that language.</summary>
    public static void Record(string word, bool hebrew)
    {
        word = word.Trim();
        if (word.Length < 2) return;
        var list = hebrew ? _he : _en;
        string key = Abbreviations.Normalize(word);
        foreach (var e in list)
            if (Abbreviations.Normalize(e.Word) == key) return;
        list.Add(new LearnedEntry(word, false));
        Save();
    }

    /// <summary>Replaces both lists (the settings dialog's result) and saves.</summary>
    public static void Set(IEnumerable<LearnedEntry> hebrew, IEnumerable<LearnedEntry> english, bool save = true)
    {
        _he = new List<LearnedEntry>(hebrew);
        _en = new List<LearnedEntry>(english);
        Rebuild();
        if (save) Save();
    }

    /// <summary>
    /// True if <paramref name="word"/> is an approved word, or starts with one (a word cut off by a switch in the
    /// middle is learned as the part typed so far).
    /// </summary>
    public static bool IsApproved(ReadOnlySpan<char> word)
    {
        if (_approved.Count == 0 || word.Length == 0) return false;
        string key = Abbreviations.Normalize(word);
        if (_approved.Contains(key)) return true;
        for (int k = MinPrefix; k < key.Length; k++)
            if (_approved.Contains(key[..k])) return true;
        return false;
    }

    private static void Rebuild()
    {
        var set = new HashSet<string>();
        foreach (var e in _he) if (e.Approved) set.Add(Abbreviations.Normalize(e.Word));
        foreach (var e in _en) if (e.Approved) set.Add(Abbreviations.Normalize(e.Word));
        _approved = set;
    }

    private static void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsStore.Directory);
            WriteAtomic(HePath, Format(_he));
            WriteAtomic(EnPath, Format(_en));
        }
        catch { /* read-only profile: the lists stay in memory */ }
    }

    private static void WriteAtomic(string path, IEnumerable<string> lines)
    {
        var tmp = path + ".tmp";
        File.WriteAllLines(tmp, lines);
        File.Move(tmp, path, overwrite: true);
    }

    internal static List<LearnedEntry> Parse(IEnumerable<string> lines)
    {
        var list = new List<LearnedEntry>();
        var seen = new HashSet<string>();
        foreach (var line in lines)
        {
            var t = line.Trim();
            if (t.Length == 0 || t[0] == '#') continue;
            bool approved = false;
            if (t.Length >= 2 && t[1] == '|' && (t[0] == '0' || t[0] == '1'))
            {
                approved = t[0] == '1';
                t = t[2..].Trim();
            }
            if (t.Length == 0 || !seen.Add(Abbreviations.Normalize(t))) continue;
            list.Add(new LearnedEntry(t, approved));
        }
        return list;
    }

    internal static IEnumerable<string> Format(IEnumerable<LearnedEntry> entries)
    {
        foreach (var e in entries) yield return (e.Approved ? "1|" : "0|") + e.Word;
    }
}
