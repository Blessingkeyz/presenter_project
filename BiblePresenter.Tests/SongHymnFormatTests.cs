using System.Security;
using BiblePresenter.App.Services;

namespace BiblePresenter.Tests;

public class SongHymnFormatTests
{
    private const string HeLeadethMe = """
        .      D             D/F# G                   D                           A
        1He    leadeth me!   O    blessed thought! O  words with heavenly comfort fraught!
        2Lord, I would clasp Thy  hand in mine, Nor   ever murmur nor re__________pine,
        3And   when my task on    earth is done, When by Thy grace the victory's  won,

        .     D                 D/F#  G                    D           Bm        D  A    D
        1What_e'er I do,        where'er I be, Still       'tis God's  hand that leadeth me.
        2Con__tent, what________ever  lot I see, Since     God through Jordan    leadeth me.
        3E'en death's cold wave I     will not flee, Since God through Jordan    leadeth me.
        """;

    private static List<(string Label, string Text)> Import(string lyrics)
    {
        var path = Path.Combine(Path.GetTempPath(), $"bp-hymn-{Guid.NewGuid():N}.xml");
        try
        {
            File.WriteAllText(path, $"<?xml version=\"1.0\" encoding=\"utf-8\"?><song><title>Test</title><lyrics>{SecurityElement.Escape(lyrics)}</lyrics></song>");
            var item = new SongImportService().Import(path);
            return item.Slides.Select((text, i) => (item.LabelAt(i), text)).ToList();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LinesWithTheSameVerseNumber_BecomeOneVerse_AcrossStanzas()
    {
        var slides = Import(HeLeadethMe);

        Assert.Equal(new[] { "V1", "V2", "V3" }, slides.Select(s => s.Label));
        Assert.Contains("He    leadeth me!", slides[0].Text);
        Assert.Contains("Whate'er I do,", slides[0].Text);
        Assert.Contains("leadeth me.", slides[0].Text);
    }

    [Fact]
    public void EachVerse_HoldsOnlyItsOwnLines()
    {
        var slides = Import(HeLeadethMe);

        Assert.DoesNotContain("Lord, I would", slides[0].Text);
        Assert.Contains("Lord, I would clasp", slides[1].Text);
        Assert.Contains("lot I see", slides[1].Text);
        Assert.Contains("And   when my task", slides[2].Text);
        Assert.Contains("E'en death's cold wave", slides[2].Text);
    }

    [Fact]
    public void VerseNumbers_AreRemovedFromTheLyrics()
    {
        var slides = Import(HeLeadethMe);

        Assert.All(slides, s => Assert.DoesNotMatch(@"(^|\n)\d", s.Text));
    }

    [Fact]
    public void Underscores_JoinTheHeldSyllables_AndAreNotShown()
    {
        var slides = Import(HeLeadethMe);

        Assert.Contains("nor repine,", slides[1].Text);
        Assert.Contains("Whate'er I do,", slides[0].Text);
        Assert.DoesNotContain("_", string.Concat(slides.Select(s => s.Text)));
    }

    [Fact]
    public void ChordRows_AreNotShown()
    {
        var slides = Import(HeLeadethMe);

        Assert.DoesNotContain("D/F#", slides[0].Text);
        Assert.DoesNotContain("Bm", string.Concat(slides.Select(s => s.Text)));
        Assert.StartsWith("He    leadeth me!", slides[0].Text);
    }

    [Fact]
    public void ChordRowsAbove_UntaggedLyricsStillSplitOnBlankLines_WithoutTheChords()
    {
        var slides = Import(".     D\n1He leadeth me\n\n.   G\n2Lord, hear\n");

        Assert.Equal(new[] { "V1", "V2" }, slides.Select(s => s.Label));
        Assert.Equal("He leadeth me", slides[0].Text);
        Assert.Equal("Lord, hear", slides[1].Text);
    }

    [Fact]
    public void TaggedSong_GroupsVersesInsideItsTags_AndKeepsTheChorus()
    {
        var slides = Import(
            "[V]\n.  D\n1He leadeth me!\n2Lord, I would clasp\n\n.   D\n1What_e'er I do\n2Con__tent\n\n" +
            "[C]\n.   D       A\n He leadeth me, He leadeth me");

        Assert.Equal(new[] { "V1", "V2", "C" }, slides.Select(s => s.Label));
        Assert.Contains("He leadeth me!", slides[0].Text);
        Assert.Contains("Whate'er I do", slides[0].Text);
        Assert.Contains("Lord, I would clasp", slides[1].Text);
        Assert.Contains("He leadeth me, He leadeth me", slides[2].Text);
        Assert.DoesNotMatch(@"(^|\n)\d", string.Concat(slides.Select(s => s.Text)));
    }

    [Fact]
    public void PlainLyricsWithoutChordsOrNumbers_StillSplitOnBlankLines()
    {
        var slides = Import("First line\nsecond line\n\nAnother verse");

        Assert.Equal(2, slides.Count);
        Assert.Equal("First line\nsecond line", slides[0].Text);
        Assert.Equal("", slides[0].Label);
    }
}
