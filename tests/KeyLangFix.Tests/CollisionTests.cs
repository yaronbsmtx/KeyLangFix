using System;
using System.IO;
using System.Linq;
using KeyLangFix.Detection;
using KeyLangFix.Input;
using Xunit;
using Xunit.Abstractions;

namespace KeyLangFix.Tests;

public class CollisionTests
{
    private readonly ITestOutputHelper _out;
    private static readonly NgramModel En = Models.Get(0x09)!;
    private static readonly NgramModel He = Models.Get(0x0D)!;
    public CollisionTests(ITestOutputHelper o) => _out = o;

    private static (bool fired, int keys, double llr) TypeHebrewCorrectly(string hebrew, int level = 3)
    {
        var opt = new DetectorOptions(DetectorOptions.ThresholdFor(level), 4);
        var buf = new WordBuffer();
        Span<char> alts = stackalloc char[1];
        double last = 0; int n = 0;
        foreach (char ch in hebrew + " ")
        {
            alts[0] = ch == ' ' ? ' ' : Kb.ToEn(ch);
            buf.Add(ch, He, alts, new[] { En });
            n++;
            var v = Detector.Evaluate(He, En, buf.Cur, buf.Alt(0), ch == ' ', opt);
            last = v.Llr;
            if (v.Switch) return (true, n, v.Llr);
        }
        return (false, n, last);
    }

    [Theory]
    [InlineData("נשמע")]
    [InlineData("גם נשמע")]
    [InlineData("עם נשמע")]
    [InlineData("זה נשמע טוב")]
    public void Correct_hebrew_phrase_with_english_lookalikes_does_not_switch(string phrase)
    {
        Assert.False(TypeHebrewCorrectly(phrase).fired);
    }

    [Fact]
    public void Three_letter_prefix_of_a_hebrew_word_does_not_switch_with_min_letters_3()
    {
        var opt = new DetectorOptions(DetectorOptions.ThresholdFor(3), 3);
        var buf = new WordBuffer();
        Span<char> alts = stackalloc char[1];
        foreach (char ch in "נשמ")
        {
            alts[0] = Kb.ToEn(ch);
            buf.Add(ch, He, alts, new[] { En });
            Assert.False(Detector.Evaluate(He, En, buf.Cur, buf.Alt(0), false, opt).Switch);
        }
    }

    [Fact]
    public void Probe_nishma()
    {
        _out.WriteLine("IsWord(נשמע) in Hebrew list: " + He.IsWord("נשמע"));
        _out.WriteLine("IsWord(bang) in English list: " + En.IsWord("bang"));
        var r = TypeHebrewCorrectly("נשמע");
        _out.WriteLine($"typing נשמע correctly: fired={r.fired} keys={r.keys} llr={r.llr:F2}");
    }

    [Fact]
    public void Correct_hebrew_words_that_spell_english_words()
    {
        var hebrew = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "data", "train.he.txt"))
            .Concat(File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "data", "heldout.he.txt")))
            .Where(w => w.Length >= 4 && w.All(c => c is >= 'א' and <= 'ת')).Distinct().ToList();
        int collisions = 0, fired = 0, firedInDict = 0;
        var examples = new System.Collections.Generic.List<string>();
        foreach (var w in hebrew)
        {
            var en = new string(w.Select(Kb.ToEn).ToArray());
            if (!En.IsWord(en)) continue;
            collisions++;
            var r = TypeHebrewCorrectly(w);
            if (r.fired) { fired++; if (He.IsWord(w)) firedInDict++; if (examples.Count < 15) examples.Add($"{w}->{en}"); }
        }
        _out.WriteLine($"Hebrew words {hebrew.Count}; spell an English word: {collisions}; wrongly switched: {fired} (of which in Hebrew list: {firedInDict})");
        _out.WriteLine("examples: " + string.Join(", ", examples));
    }
}

public class WrongLayoutProbe
{
    private readonly ITestOutputHelper _out;
    public WrongLayoutProbe(ITestOutputHelper o) => _out = o;

    [Fact]
    public void Probe_nishma_lach_on_english_layout()
    {
        var en = Models.Get(0x09)!; var he = Models.Get(0x0D)!;
        var opt = new DetectorOptions(DetectorOptions.ThresholdFor(3), 3);
        var buf = new WordBuffer();
        System.Span<char> alts = stackalloc char[1];
        foreach (char ch in "נשמע לך שלום ")
        {
            char typed = ch == ' ' ? ' ' : Kb.ToEn(ch);
            alts[0] = ch;
            buf.Add(typed, en, alts, new[] { he });
            var v = Detector.Evaluate(en, he, buf.Cur, buf.Alt(0), ch == ' ', opt);
            _out.WriteLine($"'{buf.Cur.ToString()}' -> switch={v.Switch} start={v.Start} llr={v.Llr:F2}");
            if (v.Switch) break;
        }
    }
}

public class CorrectEnglishDoesNotSwitch
{
    private static readonly NgramModel En = Models.Get(0x09)!;
    private static readonly NgramModel He = Models.Get(0x0D)!;

    [Theory]
    [InlineData("b. add", 1)]
    [InlineData("b. add", 2)]
    [InlineData("b. add ", 1)]
    [InlineData("a. add", 1)]
    [InlineData("e.g. add", 1)]
    [InlineData("c. add the", 1)]
    public void Correctly_typed_english_with_short_lists_does_not_switch(string text, int level)
    {
        var opt = new DetectorOptions(DetectorOptions.ThresholdFor(level), 3);
        var buf = new WordBuffer();
        System.Span<char> alts = stackalloc char[1];
        foreach (char ch in text)
        {
            alts[0] = ch == ' ' ? ' ' : Kb.ToHe(ch);
            buf.Add(ch, En, alts, new[] { He });
            Assert.False(Detector.Evaluate(En, He, buf.Cur, buf.Alt(0), ch == ' ', opt).Switch,
                $"switched at '{buf.Cur.ToString()}'");
        }
    }
}
