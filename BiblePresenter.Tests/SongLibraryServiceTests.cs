using BiblePresenter.App.Models;
using BiblePresenter.App.Services;
using Xunit;

namespace BiblePresenter.Tests;

public class SongLibraryServiceTests
{
    private static string TempDir() => Path.Combine(Path.GetTempPath(), $"bp-songs-{Guid.NewGuid():N}");

    [Fact]
    public void ListSongs_EmptyFolder_ReturnsEmpty()
    {
        var dir = TempDir();
        try
        {
            var library = new SongLibraryService(new SongImportService(), dir);
            Assert.Empty(library.ListSongs());
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void SaveToLibrary_ThenListSongs_FindsItByTitle()
    {
        var dir = TempDir();
        try
        {
            var library = new SongLibraryService(new SongImportService(), dir);
            var song = new SetItem
            {
                Type = SetItemType.Song,
                Title = "Amazing Grace",
                Subtitle = "John Newton",
                Slides = { "Amazing grace, how sweet the sound", "That saved a wretch like me" }
            };

            library.SaveToLibrary(song);
            var listed = library.ListSongs();

            var entry = Assert.Single(listed);
            Assert.Equal("Amazing Grace", entry.Title);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ResolveByTitle_IsCaseInsensitive_AndRoundTripsSlides()
    {
        var dir = TempDir();
        try
        {
            var library = new SongLibraryService(new SongImportService(), dir);
            var song = new SetItem
            {
                Type = SetItemType.Song,
                Title = "Amazing Grace",
                Subtitle = "John Newton",
                Slides = { "Amazing grace, how sweet the sound", "That saved a wretch like me" }
            };
            library.SaveToLibrary(song);

            var resolved = library.ResolveByTitle("amazing grace");

            Assert.NotNull(resolved);
            Assert.Equal("Amazing Grace", resolved!.Title);
            Assert.Equal("John Newton", resolved.Subtitle);
            Assert.Equal(2, resolved.Slides.Count);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ResolveByTitle_NotFound_ReturnsNull()
    {
        var dir = TempDir();
        try
        {
            var library = new SongLibraryService(new SongImportService(), dir);
            Assert.Null(library.ResolveByTitle("Nonexistent Song"));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void AddExternalFile_CopiesIntoLibraryFolder()
    {
        var dir = TempDir();
        var sourceDir = TempDir();
        Directory.CreateDirectory(sourceDir);
        try
        {
            var sourcePath = Path.Combine(sourceDir, "My Song");
            File.WriteAllText(sourcePath, "<song><title>My Song</title><lyrics>Line one</lyrics></song>");

            var library = new SongLibraryService(new SongImportService(), dir);
            var destPath = library.AddExternalFile(sourcePath);

            Assert.True(File.Exists(destPath));
            Assert.StartsWith(dir, destPath);
            Assert.Single(library.ListSongs());
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
            Directory.Delete(sourceDir, recursive: true);
        }
    }

    [Fact]
    public void ListSongs_SkipsNonXmlFilesWithoutThrowing()
    {
        var dir = TempDir();
        try
        {
            var library = new SongLibraryService(new SongImportService(), dir); // creates dir

            // Real song folders can contain many unrelated files (notes, program orders, etc.) -
            // these must be silently skipped, not crash the listing.
            File.WriteAllText(Path.Combine(dir, "04-05-2014"), "Sunday service order:\n1. Welcome\n2. Worship");
            File.WriteAllText(Path.Combine(dir, "1. WELCOME ADDRESS"), "Plain text notes, not XML at all.");
            File.WriteAllText(Path.Combine(dir, "Real Song"), "<song><title>Real Song</title><lyrics>La la la</lyrics></song>");

            var songs = library.ListSongs();

            var entry = Assert.Single(songs);
            Assert.Equal("Real Song", entry.Title);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void EnsureResolved_FillsInSlidesOnlyOnce_AndIgnoresNonSongItems()
    {
        var dir = TempDir();
        try
        {
            var library = new SongLibraryService(new SongImportService(), dir);
            library.SaveToLibrary(new SetItem
            {
                Type = SetItemType.Song,
                Title = "Amazing Grace",
                Subtitle = "John Newton",
                Slides = { "Amazing grace, how sweet the sound" }
            });

            var stub = new SetItem { Type = SetItemType.Song, Title = "Amazing Grace" };
            library.EnsureResolved(stub);
            Assert.Equal("John Newton", stub.Subtitle);
            Assert.Single(stub.Slides);

            // Already resolved (non-empty Slides) - a second call must not touch it again, even
            // if the caller has since edited it locally.
            stub.Slides = new List<string> { "Edited locally" };
            library.EnsureResolved(stub);
            Assert.Equal(new List<string> { "Edited locally" }, stub.Slides);

            // Non-song items are untouched.
            var custom = new SetItem { Type = SetItemType.Custom, Title = "Whatever" };
            library.EnsureResolved(custom);
            Assert.Empty(custom.Slides);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Search_ByTitle_MatchesSubstringOnlyInTitle()
    {
        var dir = TempDir();
        try
        {
            var library = new SongLibraryService(new SongImportService(), dir);
            library.SaveToLibrary(new SetItem { Type = SetItemType.Song, Title = "Amazing Grace", Slides = { "How sweet the sound" } });
            library.SaveToLibrary(new SetItem { Type = SetItemType.Song, Title = "How Great Thou Art", Slides = { "O Lord my God" } });

            var byTitle = library.Search("grace", byLyrics: false);
            var titleMatch = Assert.Single(byTitle);
            Assert.Equal("Amazing Grace", titleMatch.Title);

            // "sound" only appears in the lyrics, not the title, so a title-only search misses it.
            Assert.Empty(library.Search("sound", byLyrics: false));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Search_ByLyrics_MatchesContentEvenWhenTitleDoesNotMatch()
    {
        var dir = TempDir();
        try
        {
            var library = new SongLibraryService(new SongImportService(), dir);
            library.SaveToLibrary(new SetItem { Type = SetItemType.Song, Title = "Amazing Grace", Slides = { "How sweet the sound" } });
            library.SaveToLibrary(new SetItem { Type = SetItemType.Song, Title = "How Great Thou Art", Slides = { "O Lord my God" } });

            var byLyrics = library.Search("sweet the sound", byLyrics: true);

            var match = Assert.Single(byLyrics);
            Assert.Equal("Amazing Grace", match.Title);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Search_EmptyQuery_ReturnsEmpty()
    {
        var dir = TempDir();
        try
        {
            var library = new SongLibraryService(new SongImportService(), dir);
            library.SaveToLibrary(new SetItem { Type = SetItemType.Song, Title = "Amazing Grace", Slides = { "How sweet" } });

            Assert.Empty(library.Search("", byLyrics: false));
            Assert.Empty(library.Search("   ", byLyrics: true));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
