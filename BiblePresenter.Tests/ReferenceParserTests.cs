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
}
