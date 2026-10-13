using System;
using KeyLangFix.Input;
using KeyLangFix.Detection;
using KeyLangFix.Services;
using Xunit;

namespace KeyLangFix.Tests;

public class MiscTests
{
    [Theory]
    [InlineData("2026-10-12", "2026-10-12")] // Monday
    [InlineData("2026-10-14", "2026-10-12")]
    [InlineData("2026-10-18", "2026-10-12")] // Sunday
    [InlineData("2026-10-19", "2026-10-19")]
    public void Week_starts_on_monday(string day, string monday)
    {
        Assert.Equal(DateOnly.Parse(monday), StatsStore.MondayOf(DateOnly.Parse(day)));
    }

    [Fact]
    public void Buffer_counts_one_entry_per_key_and_tracks_backspace()
    {
        var en = Models.Get(0x09)!;
        var he = Models.Get(0x0D)!;
        var b = new WordBuffer();
        b.Add(' ', en, new[] { ' ' }, new[] { he });            // leading space ignored
        Assert.Equal(0, b.Length);
        b.Add('a', en, new[] { 'ש' }, new[] { he });
        b.Add('1', en, new[] { '1' }, new[] { he });            // digit = separator in scoring, kept for retype
        Assert.Equal(2, b.Length);
        Assert.Equal("a ", b.Cur.ToString());
        Assert.Equal("1", b.AltRaw(0)[1..].ToString());
        b.Backspace();
        Assert.Equal(1, b.Length);
    }

    [Fact]
    public void Buffer_never_overflows()
    {
        var en = Models.Get(0x09)!;
        var he = Models.Get(0x0D)!;
        var b = new WordBuffer();
        for (int i = 0; i < 500; i++) b.Add('a', en, new[] { 'ש' }, new[] { he });
        Assert.InRange(b.Length, 1, WordBuffer.Capacity);
    }
}
