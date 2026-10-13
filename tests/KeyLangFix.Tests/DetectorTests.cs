using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KeyLangFix.Detection;
using KeyLangFix.Input;
using Xunit;
using Xunit.Abstractions;

namespace KeyLangFix.Tests;

/// <summary>Standard US-English / Hebrew (SI-1452) key mapping, test-side only.</summary>
internal static class Kb
{
    private const string En = "qwertyuiopasdfghjkl;'zxcvbnm,./";
    private const string He = "/'קראטוןםפשדגכעיחלךף,זסבהנמצתץ.";

    public static char ToHe(char en) => En.IndexOf(en) is var i and >= 0 ? He[i] : en;
    public static char ToEn(char he) => He.IndexOf(he) is var i and >= 0 ? En[i] : he;
}

public class DetectorTests
{
    private readonly ITestOutputHelper _out;
    private static readonly NgramModel En = Models.Get(0x09)!;
    private static readonly NgramModel He = Models.Get(0x0D)!;

    public DetectorTests(ITestOutputHelper output) => _out = output;

    private static string Dir => Path.Combine(AppContext.BaseDirectory, "data");
    private static string[] Words(string file) => File.ReadAllLines(Path.Combine(Dir, file));

    /// <summary>Types text key by key; returns the verdict at the first key where it fires (or null).</summary>
    private static (int Keys, Verdict V)? Type(string intended, bool intendedHebrew, DetectorOptions opt)
    {
        // Wrong layout active: English layout while intending Hebrew, or Hebrew layout while intending English.
        var curModel = intendedHebrew ? En : He;
        var altModel = intendedHebrew ? He : En;
        var buf = new WordBuffer();
        Span<char> alts = stackalloc char[1];
        int typed = 0;
        foreach (char ch in intended)
        {
            char c = intendedHebrew ? Kb.ToEn(ch) : Kb.ToHe(ch);
            alts[0] = ch;
            typed++;
            if (ch == ' ')
            {
                buf.Add(' ', curModel, new[] { ' ' }, new[] { altModel });
            }
            else buf.Add(c, curModel, alts, new[] { altModel });
            var v = Detector.Evaluate(curModel, altModel, buf.Cur, buf.Alt(0), ch == ' ', opt);
            if (v.Switch) return (typed, v);
        }
        return null;
    }

    /// <summary>Normal typing on the correct layout; returns true if a false switch fires.</summary>
    private static bool FalseSwitch(string text, bool hebrew, DetectorOptions opt)
    {
        var curModel = hebrew ? He : En;
        var altModel = hebrew ? En : He;
        var buf = new WordBuffer();
        Span<char> alts = stackalloc char[1];
        foreach (char ch in text)
        {
            char other = hebrew ? Kb.ToEn(ch) : Kb.ToHe(ch);
            alts[0] = other;
            buf.Add(ch, curModel, alts, new[] { altModel });
            var v = Detector.Evaluate(curModel, altModel, buf.Cur, buf.Alt(0), ch == ' ', opt);
            if (v.Switch) return true;
        }
        return false;
    }

    private static DetectorOptions Opt(int level = 3, int min = 4) => new(DetectorOptions.ThresholdFor(level), min);

    [Theory]
    [InlineData("שלום", true)]
    [InlineData("תודה רבה", true)]
    [InlineData("hello", false)]
    [InlineData("good morning", false)]
    [InlineData("what do you want", false)]
    [InlineData("how are you ", false)]
    [InlineData("I am fine", false)]
    [InlineData("thank you", false)]
    [InlineData("see you tomorrow", false)]
    [InlineData("מה אתה", true)]
    [InlineData("למה אני", true)]
    [InlineData("מה שלומך", true)]
    public void Detects_wrong_layout(string intended, bool hebrew)
    {
        Assert.NotNull(Type(intended, hebrew, Opt()));
    }

    [Fact]
    public void Short_leading_words_join_the_rewrite()
    {
        // "i do not know" typed on the Hebrew layout: the whole phrase must be converted.
        var r = Type("i do not know", false, Opt());
        Assert.NotNull(r);
        Assert.Equal(0, r!.Value.V.Start);
        Assert.True(r.Value.Keys <= 9, "should fire by the end of 'not', not wait for 'know'");
    }

    [Fact]
    public void Does_not_fire_before_min_letters()
    {
        var r = Type("שלום", true, Opt(min: 4));
        Assert.NotNull(r);
        Assert.True(r!.Value.Keys >= 4);
    }

    [Theory]
    [InlineData("hello world this is a normal english sentence", false)]
    [InlineData("שלום עולם זהו משפט רגיל בעברית", true)]
    [InlineData("github.com/user/repo", false)]
    [InlineData("Yaron Ben-Sasson", false)]
    public void No_false_switch_on_normal_text(string text, bool hebrew)
    {
        Assert.False(FalseSwitch(text, hebrew, Opt()));
    }

    [Fact]
    public void Stats_heldout_words()
    {
        foreach (var level in new[] { 1, 2, 3, 4, 5 })
        {
            var opt = Opt(level);
            var line = new List<string>();
            foreach (var (lang, hebrew) in new[] { ("en", false), ("he", true) })
            {
                var words = Words($"heldout.{lang}.txt")
                    .Where(w => w.Length >= 4 && w.All(c => hebrew ? c is >= 'א' and <= 'ת' : c is >= 'a' and <= 'z')).Take(3000).ToList();
                int caught = words.Count(w => Type(w, hebrew, opt) != null);
                int fp = words.Count(w => FalseSwitch(w + " ", hebrew, opt));
                line.Add($"{lang}: wrong-layout caught {100.0 * caught / words.Count:F1}%  false switch {100.0 * fp / words.Count:F2}% (n={words.Count})");
            }
            _out.WriteLine($"level {level} thr {opt.Threshold}: " + string.Join(" | ", line));
        }
    }
}
