using BiblePresenter.App.Services;

namespace BiblePresenter.Tests;

public class ScriptureSlideSplitterTests
{
    [Fact]
    public void SplitsPackedSlidesIntoOneVersePerSlide()
    {
        var slides = new List<string>
        {
            "11 strengthened with all might, according to His glorious power, for all patience and longsuffering with joy; 12 giving thanks to the Father who has qualified us to be partakers of the inheritance of the saints in the light.",
            "13 He has delivered us from the power of darkness and conveyed us into the kingdom of the Son of His love, 14 in whom we have redemption through His blood, the forgiveness of sins."
        };

        var result = ScriptureSlideSplitter.SplitIntoVerses(slides);

        Assert.Equal(4, result.Count);
        Assert.StartsWith("11  strengthened", result[0]);
        Assert.EndsWith("with joy;", result[0]);
        Assert.StartsWith("12  giving thanks", result[1]);
        Assert.EndsWith("in the light.", result[1]);
        Assert.StartsWith("13  He has delivered", result[2]);
        Assert.StartsWith("14  in whom", result[3]);
        Assert.EndsWith("forgiveness of sins.", result[3]);
    }

    [Fact]
    public void SplitsTwoVersesInOneSlide()
    {
        var slides = new List<string> { "6 Be anxious for nothing; 7 and the peace of God will guard your hearts." };

        var result = ScriptureSlideSplitter.SplitIntoVerses(slides);

        Assert.Equal(2, result.Count);
        Assert.Equal("6  Be anxious for nothing;", result[0]);
        Assert.Equal("7  and the peace of God will guard your hearts.", result[1]);
    }

    [Fact]
    public void LeavesASingleVerseUntouched()
    {
        var slides = new List<string> { "16 For God so loved the world, that he gave his only begotten Son." };

        var result = ScriptureSlideSplitter.SplitIntoVerses(slides);

        Assert.Equal(slides, result);
    }

    [Fact]
    public void IgnoresNumbersInsideVerseTextThatAreNotTheNextVerse()
    {
        var slides = new List<string> { "3 It rained for 40 days and 40 nights." };

        var result = ScriptureSlideSplitter.SplitIntoVerses(slides);

        Assert.Equal(slides, result);
    }

    [Fact]
    public void SplitsALongVerseAtASentenceEnd()
    {
        var first = "Behold, I stand at the door and knock; if any man hear my voice, and open the door, I will come in to him, and will sup with him.";
        var second = "To him that overcometh will I grant to sit with me in my throne, even as I also overcame, and am set down with my Father in his throne.";

        var result = ScriptureSlideSplitter.SplitLongText($"20  {first} {second}");

        Assert.Equal(2, result.Count);
        Assert.Equal($"20  {first}", result[0]);
        Assert.Equal(second, result[1]);
    }

    [Fact]
    public void LongVerseParts_AreEachWithinTheLimitAndKeepEveryWord()
    {
        var verse = "9  Then were the king's scribes called at that time in the third month, that is, the month Sivan, on the three and twentieth day thereof; " +
                    "and it was written according to all that Mordecai commanded unto the Jews, and to the lieutenants, and the deputies and rulers of the provinces " +
                    "which are from India unto Ethiopia, an hundred twenty and seven provinces, unto every province according to the writing thereof, " +
                    "and unto every people after their language, and to the Jews according to their writing, and according to their language.";

        var parts = ScriptureSlideSplitter.SplitLongText(verse);

        Assert.True(parts.Count >= 4);
        Assert.All(parts, p => Assert.True(p.Length <= ScriptureSlideSplitter.MaxSlideChars, $"too long ({p.Length}): {p}"));
        Assert.StartsWith("9  ", parts[0]);
        Assert.DoesNotContain(parts.Skip(1), p => p.StartsWith("9  "));
        Assert.Equal(verse.Split(' ', StringSplitOptions.RemoveEmptyEntries), string.Join(" ", parts).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact]
    public void LongVerseParts_AreRoughlyEvenInSize()
    {
        var verse = string.Join(" ", Enumerable.Repeat("and the word was with them", 14));

        var parts = ScriptureSlideSplitter.SplitLongText(verse);

        Assert.True(parts.Count >= 3);
        Assert.True(parts.Max(p => p.Length) - parts.Min(p => p.Length) < 45);
    }

    [Fact]
    public void TextWithoutSpacesStillSplitsWithinTheLimit()
    {
        var parts = ScriptureSlideSplitter.SplitLongText(new string('x', 400));

        Assert.True(parts.Count >= 3);
        Assert.All(parts, p => Assert.True(p.Length <= ScriptureSlideSplitter.MaxSlideChars));
    }

    [Fact]
    public void LongSongStanza_CutsAtALineBreak_NotInsideALine()
    {
        var stanza = string.Join("\n", Enumerable.Repeat("Amazing grace how sweet the sound", 6));

        var parts = ScriptureSlideSplitter.SplitLongText(stanza, 120);

        Assert.True(parts.Count >= 2);
        Assert.All(parts, p => Assert.True(p.Length <= 120));
        Assert.All(parts, p => Assert.All(p.Split('\n'), line => Assert.Equal("Amazing grace how sweet the sound", line)));
    }

    [Fact]
    public void SplittingIsStableWhenRunAgainOnItsOwnOutput()
    {
        var packed = new List<string>
        {
            "9 " + string.Join(" ", Enumerable.Repeat("and the Lord spake unto Moses saying,", 8)) + " 10 " + string.Join(" ", Enumerable.Repeat("and it came to pass in those days,", 9))
        };

        var once = ScriptureSlideSplitter.SplitIntoVerses(packed);
        var twice = ScriptureSlideSplitter.SplitIntoVerses(once);

        Assert.True(once.Count > 2);
        Assert.Equal(once, twice);
    }

    [Fact]
    public void Resplit_FoldsContinuationsBackTogetherWhenTheLimitGrows()
    {
        var verse = "5  " + string.Join(" ", Enumerable.Repeat("they that were full have hired out themselves for bread;", 5));
        var parts = ScriptureSlideSplitter.SplitIntoVerses(new List<string> { verse }, 100);
        Assert.True(parts.Count > 1);

        var merged = ScriptureSlideSplitter.Resplit(parts, 1000);

        Assert.Equal(new List<string> { verse }, merged);
    }

    [Fact]
    public void Resplit_CutsFurtherWhenTheLimitShrinks()
    {
        var slides = new List<string>
        {
            "3  " + string.Join(" ", Enumerable.Repeat("talk no more so exceeding proudly;", 6)),
            "4  the bows of the mighty men are broken."
        };

        var result = ScriptureSlideSplitter.Resplit(slides, 90);

        Assert.True(result.Count > 2);
        Assert.All(result, s => Assert.True(s.Length <= 90, $"too long ({s.Length}): {s}"));
        Assert.StartsWith("4  ", result[^1]);
    }

    [Fact]
    public void ExplicitLimitOverridesTheGlobalOne()
    {
        var text = string.Join(" ", Enumerable.Repeat("and it came to pass,", 12));

        Assert.True(ScriptureSlideSplitter.SplitLongText(text, 60).Count > ScriptureSlideSplitter.SplitLongText(text, 200).Count);
    }

    [Fact]
    public void LeavesSlidesWithoutALeadingVerseNumberUntouched()
    {
        var slides = new List<string> { "In the beginning God created the heaven and the earth.", "And the earth was without form." };

        var result = ScriptureSlideSplitter.SplitIntoVerses(slides);

        Assert.Equal(slides, result);
    }
}
