using System;
using System.Collections.Generic;
using System.IO;

namespace KeyLangFix.Services;

/// <summary>Automatic-switch counters in %APPDATA%\KeyLangFix\stats.ini.</summary>
internal static class StatsStore
{
    private const int KeepDays = 14;

    private static readonly Dictionary<DateOnly, int> Days = new();
    private static DateOnly _first = DateOnly.FromDateTime(DateTime.Now);
    private static long _total;

    private static string FilePath => Path.Combine(SettingsStore.Directory, "stats.ini");

    public static long Total => _total;
    public static DateOnly FirstLaunch => _first;

    public static int Today => Days.TryGetValue(Now(), out var n) ? n : 0;

    public static int ThisWeek
    {
        get
        {
            var today = Now();
            var monday = MondayOf(today);
            int sum = 0;
            foreach (var (d, n) in Days) if (d >= monday && d <= today) sum += n;
            return sum;
        }
    }

    public static void Load()
    {
        try
        {
            if (!File.Exists(FilePath)) { Save(); return; }
            foreach (var (k, v) in IniFile.Read(FilePath))
            {
                if (k == "first" && DateOnly.TryParseExact(v, "yyyy-MM-dd", out var f)) _first = f;
                else if (k == "total" && long.TryParse(v, out var t)) _total = t;
                else if (k.StartsWith("d.") && DateOnly.TryParseExact(k[2..], "yyyy-MM-dd", out var d) && int.TryParse(v, out var n))
                    Days[d] = n;
            }
        }
        catch { /* start from zero if unreadable */ }
    }

    /// <summary>Clears all counters; "first launch" restarts from today.</summary>
    public static void Reset()
    {
        _total = 0;
        Days.Clear();
        _first = Now();
        Save();
    }

    public static void RecordSwitch()
    {
        var today = Now();
        _total++;
        Days[today] = Today + 1;
        Prune(today);
        Save();
    }

    private static void Prune(DateOnly today)
    {
        var cutoff = today.AddDays(-KeepDays);
        var old = new List<DateOnly>();
        foreach (var d in Days.Keys) if (d < cutoff) old.Add(d);
        foreach (var d in old) Days.Remove(d);
    }

    private static void Save()
    {
        try
        {
            var items = new List<(string, string)>
            {
                ("first", _first.ToString("yyyy-MM-dd")),
                ("total", _total.ToString()),
            };
            foreach (var (d, n) in Days) items.Add(("d." + d.ToString("yyyy-MM-dd"), n.ToString()));
            IniFile.Write(FilePath, items);
        }
        catch { /* ignore */ }
    }

    private static DateOnly Now() => DateOnly.FromDateTime(DateTime.Now);

    internal static DateOnly MondayOf(DateOnly d)
    {
        int back = ((int)d.DayOfWeek + 6) % 7;
        return d.AddDays(-back);
    }
}
