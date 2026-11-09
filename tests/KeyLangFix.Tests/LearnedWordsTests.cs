using System.Linq;
using KeyLangFix.Services;
using Xunit;

namespace KeyLangFix.Tests;

public class LearnedWordsTests
{
    [Fact]
    public void ParseFormat_RoundTrip()
    {
        var entries = new[] { new LearnedEntry("api", true), new LearnedEntry("שלום", false) };
        var back = LearnedWords.Parse(LearnedWords.Format(entries));
        Assert.Equal(entries, back);
    }

    [Fact]
    public void Parse_SkipsBlankCommentsAndDuplicates_DefaultsToUnapproved()
    {
        var list = LearnedWords.Parse(new[] { "", "# c", "foo", "1|FOO", "0|bar" });
        Assert.Equal(new[] { new LearnedEntry("foo", false), new LearnedEntry("bar", false) }, list);
    }

    [Fact]
    public void OnlyApprovedWordsMatch()
    {
        LearnedWords.Set(new[] { new LearnedEntry("שלום", true) },
            new[] { new LearnedEntry("ctrlx", true), new LearnedEntry("pending", false) }, save: false);
        try
        {
            Assert.True(LearnedWords.IsApproved("ctrlx"));
            Assert.True(LearnedWords.IsApproved("CtrlX"));          // case-insensitive
            Assert.True(LearnedWords.IsApproved("ctrlxyz"));        // starts with an approved word
            Assert.False(LearnedWords.IsApproved("pending"));       // not approved
            Assert.False(LearnedWords.IsApproved("ctr"));
            Assert.True(LearnedWords.IsApproved("שלום"));
            Assert.False(LearnedWords.IsApproved(""));
        }
        finally { LearnedWords.Set(Enumerable.Empty<LearnedEntry>(), Enumerable.Empty<LearnedEntry>(), save: false); }
    }

    [Fact]
    public void HebrewFinalLettersCompareAsPlainForms()
    {
        LearnedWords.Set(new[] { new LearnedEntry("שלומ", true) }, System.Array.Empty<LearnedEntry>(), save: false);
        try { Assert.True(LearnedWords.IsApproved("שלום")); }
        finally { LearnedWords.Set(Enumerable.Empty<LearnedEntry>(), Enumerable.Empty<LearnedEntry>(), save: false); }
    }
}
