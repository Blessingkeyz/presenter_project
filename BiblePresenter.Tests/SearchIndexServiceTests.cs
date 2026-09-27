using BiblePresenter.App.Models;
using BiblePresenter.App.Services;
using Xunit;

namespace BiblePresenter.Tests;

public class SearchIndexServiceTests
{
    private static Translation BuildSampleTranslation()
    {
        var verses = new List<Verse>
        {
            new() { TranslationId = "t1", BookIndex = 1, BookName = "Genesis", Chapter = 1, Number = 1, Text = "In the beginning God created the heaven and the earth." },
            new() { TranslationId = "t1", BookIndex = 1, BookName = "Genesis", Chapter = 1, Number = 2, Text = "And the earth was without form, and void." },
            new() { TranslationId = "t1", BookIndex = 43, BookName = "John", Chapter = 3, Number = 16, Text = "For God so loved the world, that he gave his only begotten Son." },
            new() { TranslationId = "t1", BookIndex = 43, BookName = "John", Chapter = 3, Number = 17, Text = "For God sent not his Son into the world to condemn the world." },
            new() { TranslationId = "t1", BookIndex = 52, BookName = "1 Thessalonians", Chapter = 2, Number = 1, Text = "For yourselves, brethren, know our entrance in unto you, that it was not in vain." },
            new() { TranslationId = "t1", BookIndex = 53, BookName = "2 Thessalonians", Chapter = 2, Number = 1, Text = "Now we beseech you, brethren, by the coming of our Lord Jesus Christ." },
        };

        return new Translation { Id = "t1", Name = "Test", Abbreviation = "TST", Verses = verses };
    }

    private static SearchIndexService BuildLoadedIndex()
    {
        var index = new SearchIndexService();
        index.Load(BuildSampleTranslation());
        return index;
    }

    [Fact]
    public void Search_ReferenceQuery_JumpsDirectlyToVerse()
    {
        var index = BuildLoadedIndex();
        var result = index.Search("jn 3:16");

        Assert.True(result.WasReferenceJump);
        var verse = Assert.Single(result.Verses);
        Assert.Equal("John", verse.BookName);
        Assert.Equal(16, verse.Number);
    }

    [Fact]
    public void Search_ChapterOnlyReference_ReturnsWholeChapter()
    {
        var index = BuildLoadedIndex();
        var result = index.Search("john 3");

        Assert.True(result.WasReferenceJump);
        Assert.Equal(2, result.Verses.Count);
    }

    [Fact]
    public void Search_KeywordQuery_MatchesAllWordsAsAnd()
    {
        var index = BuildLoadedIndex();
        var result = index.Search("god world");

        Assert.False(result.WasReferenceJump);
        Assert.Equal(2, result.Verses.Count);
        Assert.All(result.Verses, v => Assert.Contains("God", v.Text));
    }

    [Fact]
    public void Search_KeywordQuery_SupportsPrefixMatching()
    {
        var index = BuildLoadedIndex();
        // "beg" alone would also match "begotten" (verse 3) - use a longer prefix unique to "beginning".
        var result = index.Search("beginn");

        var verse = Assert.Single(result.Verses);
        Assert.Equal(1, verse.Number);
    }

    [Fact]
    public void Search_KeywordQuery_NoMatchesReturnsEmpty()
    {
        var index = BuildLoadedIndex();
        var result = index.Search("xyzzy");

        Assert.Empty(result.Verses);
    }

    [Fact]
    public void Search_MisspelledWord_FallsBackToClosestMatch()
    {
        var index = BuildLoadedIndex();
        // "beginning" misspelled by one letter - no exact/prefix hit, should still find it via
        // the approximate ("closest to") fallback.
        var result = index.Search("begining");

        Assert.False(result.WasReferenceJump);
        var verse = Assert.Single(result.Verses);
        Assert.Equal(1, verse.Number);
        Assert.Equal("Genesis", verse.BookName);
    }

    [Fact]
    public void Search_AmbiguousBookStem_ReturnsBothBooksAsFlatListNotASinglePassage()
    {
        var index = BuildLoadedIndex();
        var result = index.Search("thess 2 1");

        // Two distinct books (1 & 2 Thessalonians) can't form one continuous passage, so this
        // must NOT be flagged as a reference jump - GoLive falls back to single-verse behavior
        // instead of trying to build one (invalid) range label spanning both books.
        Assert.False(result.WasReferenceJump);
        Assert.Equal(2, result.Verses.Count);
        Assert.Equal("1 Thessalonians", result.Verses[0].BookName);
        Assert.Equal("2 Thessalonians", result.Verses[1].BookName);
    }
}
