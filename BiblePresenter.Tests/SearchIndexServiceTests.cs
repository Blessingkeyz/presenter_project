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
}
