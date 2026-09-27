using BiblePresenter.App.Models;
using BiblePresenter.App.Services;
using Xunit;

namespace BiblePresenter.Tests;

public class SongImportServiceTests
{
    [Fact]
    public void Import_RealOpenSongFile_SplitsIntoSectionSlides()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "All must be well");
        Assert.True(File.Exists(path), $"Fixture not found at {path}");

        var item = new SongImportService().Import(path);

        Assert.Equal(SetItemType.Song, item.Type);
        Assert.Equal("All must be well", item.Title);
        Assert.Equal(4, item.Slides.Count);
        Assert.Contains("Through the love of God our Savior", item.Slides[0]);
        Assert.Contains("All must be well", item.Slides[0]);
        Assert.Contains("Though we pass through tribulation", item.Slides[1]);
        Assert.Contains("We expect a bright tomorrow", item.Slides[2]);
        // Section tags themselves should not leak into slide text.
        Assert.DoesNotContain("[v1]", item.Slides[0]);
    }

    [Fact]
    public void Import_NoSectionTags_FallsBackToBlankLineSplitting()
    {
        const string xml = """
            <song>
              <title>Simple Song</title>
              <lyrics>First stanza line one
            First stanza line two

            Second stanza line one</lyrics>
              <author>A. Writer</author>
            </song>
            """;

        var path = Path.Combine(Path.GetTempPath(), $"song-{Guid.NewGuid():N}.xml");
        File.WriteAllText(path, xml);
        try
        {
            var item = new SongImportService().Import(path);

            Assert.Equal("Simple Song", item.Title);
            Assert.Equal("A. Writer", item.Subtitle);
            Assert.Equal(2, item.Slides.Count);
            Assert.Equal("First stanza line one\nFirst stanza line two", item.Slides[0]);
            Assert.Equal("Second stanza line one", item.Slides[1]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Import_ThrowsWhenNotASongFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"notasong-{Guid.NewGuid():N}.xml");
        File.WriteAllText(path, "<root><item>hello</item></root>");
        try
        {
            Assert.Throws<BibleImportException>(() => new SongImportService().Import(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
