using System;

namespace KeyLangFix.Detection;

/// <summary>User-tunable detection parameters.</summary>
internal readonly record struct DetectorOptions(double Threshold, int MinLetters)
{
    /// <summary>Confidence level 1 (switch eagerly) to 5 (very strict).</summary>
    public static double ThresholdFor(int level) => level switch
    {
        <= 1 => 0.6,
        2 => 0.9,
        3 => 1.2,
        4 => 1.6,
        _ => 2.0,
    };
}

internal readonly record struct Verdict(bool Switch, int Start, double Llr)
{
    public static readonly Verdict None = new(false, 0, 0);
}

internal static class Detector
{
    private const double WordListBonus = 1.0;
    private const double RunContinuation = 0.35;
    private const int MaxRunWords = 3;

    /// <summary>
    /// Decides whether the keys typed so far look like text of <paramref name="alt"/> typed on the
    /// layout of <paramref name="cur"/>. <paramref name="curText"/> and <paramref name="altText"/>
    /// have equal length (one char per key); words are separated by ' '.
    /// On success <see cref="Verdict.Start"/> is the index of the first key to convert.
    /// </summary>
    public static Verdict Evaluate(NgramModel cur, NgramModel alt, ReadOnlySpan<char> curText,
        ReadOnlySpan<char> altText, bool lastComplete, DetectorOptions opt)
    {
        int n = curText.Length;
        if (n == 0 || altText.Length != n) return Verdict.None;

        // Walk words back to front.
        double sumAlt = 0, sumCur = 0;
        int cntAlt = 0, cntCur = 0, letters = 0, start = n, words = 0;
        int end = n;
        bool first = true;
        while (end > 0 && words < MaxRunWords)
        {
            // trim separators after the word
            while (end > 0 && curText[end - 1] == ' ') end--;
            if (end == 0) break;
            int s = end;
            while (s > 0 && curText[s - 1] != ' ') s--;

            var cw = curText.Slice(s, end - s);
            var aw = altText.Slice(s, end - s);
            bool complete = !first || lastComplete;

            // A reading that puts a final-form letter mid-word is not a word of that language.
            if (alt.HasMisplacedFinal(aw))
            {
                if (first) return Verdict.None;
                break;
            }

            double ca = alt.Score(aw, complete, out int na, out int la);
            double cc = cur.Score(cw, complete, out int nc, out int lc);

            if (first)
            {
                if (la < 2) return Verdict.None;
                // What is typed is a real word, or still the start of one, in the current layout:
                // keep waiting rather than switch ("נשמ" is "ban" but also the start of "נשמע").
                if (cw.Length >= (complete ? 2 : 3) && (complete ? cur.IsWord(cw) : cur.IsWordOrPrefix(cw))) return Verdict.None;
            }

            double llr;
            if (na == 0) llr = double.NegativeInfinity;
            else if (nc == 0) llr = 0;
            else llr = ca / na - cc / nc;
            if (aw.Length >= 3 && alt.IsWord(aw)) llr += WordListBonus;

            // An earlier word joins the run only if it has real evidence of its own: at least two letters
            // in both layouts. "b." is a letter plus a period, but its '.' key is a Hebrew letter.
            if (!first && (la < 2 || lc < 2 || llr < RunContinuation)) break;

            if (na > 0 && nc > 0)
            {
                sumAlt += ca; cntAlt += na;
                sumCur += cc; cntCur += nc;
            }
            letters += la;
            start = s;
            words++;
            first = false;
            end = s;
        }

        if (cntAlt == 0 || cntCur == 0) return Verdict.None;

        // Earlier words that are also valid words in the target language ("i do" before "not know")
        // join the rewrite. They are ambiguous on their own, so they only count as supporting
        // evidence for the minimum-length rule, never towards the score.
        int extLetters = 0;
        for (int e = start; e > 0;)
        {
            while (e > 0 && curText[e - 1] == ' ') e--;
            if (e == 0) break;
            int s = e;
            while (s > 0 && curText[s - 1] != ' ') s--;
            var aw = altText.Slice(s, e - s);
            int la = alt.CountLetters(aw);
            bool ok = la >= 2 ? alt.IsWord(aw) : la == 1 && aw.Length == 1 && alt.IsSingleLetterWord(aw[0]);
            if (!ok) break;
            extLetters += la;
            start = s;
            e = s;
        }

        var trimmed = curText.TrimEnd(' ');
        int lastWordStart = trimmed.LastIndexOf(' ') + 1;
        var lastAlt = altText.Slice(lastWordStart, trimmed.Length - lastWordStart);

        // A finished 2-letter word is accepted when the words right before it are also valid words in
        // the target language ("נשמע לך" typed as "bang kl": the first word is a real English word,
        // so only the short second one carries the evidence).
        bool shortTail = lastComplete && lastAlt.Length == 2 && extLetters >= 3 && alt.IsWord(lastAlt);

        // Mid-word, only the word being typed counts: a 3-letter prefix like "נשמ" (= "ban") must not be
        // switched on the strength of earlier short words. At a word boundary the leading words help.
        int counted = lastComplete ? letters + extLetters : letters;
        if ((letters < Math.Min(3, opt.MinLetters) && !shortTail) || counted < opt.MinLetters) return Verdict.None;

        double total = sumAlt / cntAlt - sumCur / cntCur;
        // Bonus applies per word above; for the run use the last word's bonus too.
        if ((lastAlt.Length >= 3 || shortTail) && alt.IsWord(lastAlt)) total += WordListBonus;

        return total >= opt.Threshold ? new Verdict(true, start, total) : new Verdict(false, start, total);
    }
}
