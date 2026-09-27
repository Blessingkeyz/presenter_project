using BiblePresenter.App.Services;
using Xunit;

namespace BiblePresenter.Tests;

public class ReferenceParserTests
{
    private readonly ReferenceParser _parser = new();

    [Theory]
    [InlineData("jn 3:16", "John", 3, 16, null)]
    [InlineData("John 3:16", "John", 3, 16, null)]
    [InlineData("1 cor 13", "1 Corinthians", 13, null, null)]
    [InlineData("1cor 13:4", "1 Corinthians", 13, 4, null)]
    [InlineData("genesis 1:1-3", "Genesis", 1, 1, 3)]
    [InlineData("song of solomon 2:1", "Song of Solomon", 2, 1, null)]
    [InlineData("gen 2 2-10", "Genesis", 2, 2, 10)]
    [InlineData("genesis 1 5", "Genesis", 1, 5, null)]
    [InlineData("jn 3  16", "John", 3, 16, null)]
    [InlineData("gen 1 3-4", "Genesis", 1, 3, 4)]
    [InlineData("gen 1 3 4", "Genesis", 1, 3, 4)]
    public void TryParse_ResolvesKnownReferenceFormats(string query, string expectedBook, int expectedChapter, int? expectedV1, int? expectedV2)
    {
        var result = _parser.TryParse(query);

        Assert.NotNull(result);
        Assert.Equal(expectedBook, result!.Book.Name);
        Assert.Equal(expectedChapter, result.Chapter);
        Assert.Equal(expectedV1, result.VerseStart);
        Assert.Equal(expectedV2, result.VerseEnd);
    }

    [Theory]
    [InlineData("love")]
    [InlineData("for god so loved the world")]
    [InlineData("")]
    [InlineData("notabook 3:16")]
    public void TryParse_ReturnsNullForNonReferenceQueries(string query)
    {
        Assert.Null(_parser.TryParse(query));
    }

    [Fact]
    public void ParseAll_AmbiguousBareStem_ReturnsBothNumberedBooks()
    {
        var results = _parser.ParseAll("thess 2 1-5");

        Assert.Equal(2, results.Count);
        Assert.Equal("1 Thessalonians", results[0].Book.Name);
        Assert.Equal("2 Thessalonians", results[1].Book.Name);
        Assert.All(results, r =>
        {
            Assert.Equal(2, r.Chapter);
            Assert.Equal(1, r.VerseStart);
            Assert.Equal(5, r.VerseEnd);
        });
    }

    [Fact]
    public void ParseAll_UnambiguousReference_ReturnsSingleResult()
    {
        var results = _parser.ParseAll("jn 3:16");

        var single = Assert.Single(results);
        Assert.Equal("John", single.Book.Name);
    }

    [Fact]
    public void ParseAll_NonReferenceQuery_ReturnsEmpty()
    {
        Assert.Empty(_parser.ParseAll("for god so loved the world"));
    }
}
