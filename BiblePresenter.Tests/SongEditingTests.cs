using BiblePresenter.App.Models;
using BiblePresenter.App.Services;

namespace BiblePresenter.Tests;

public class SongEditingTests
{
    private static string TempDir() => Path.Combine(Path.GetTempPath(), $"bp-songs-{Guid.NewGuid():N}");

    private static SetItem Song(string title, params (string Label, string Text)[] sections) => new()
    {
        Type = SetItemType.Song,
        Title = title,
        Subtitle = "Test Author",
        Slides = sections.Select(s => s.Text).ToList(),
        SlideLabels = sections.Select(s => s.Label).ToList()
    };

    [Fact]
    public void Labels_SurviveSaveAndReload()
    {
        var dir = TempDir();
        try
        {
            var library = new SongLibraryService(new SongImportService(), dir);
            var path = library.SaveSong(Song("Grace", ("V1", "Amazing grace"), ("C", "Sing it loud"), ("B", "The bridge")), null);

            var loaded = library.Load(path);

            Assert.Equal(new[] { "V1", "C", "B" }, loaded.SlideLabels);
            Assert.Equal(new[] { "Amazing grace", "Sing it loud", "The bridge" }, loaded.Slides);
            Assert.Equal(path, loaded.LibraryPath);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void UntaggedSections_ExportWithNumberedTags()
    {
        var dir = TempDir();
        try
        {
            var library = new SongLibraryService(new SongImportService(), dir);
            var path = library.SaveSong(Song("Plain", ("", "first"), ("", "second")), null);

            var text = File.ReadAllText(path);

            Assert.Contains("[v1]", text);
            Assert.Contains("[v2]", text);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void SaveWithExistingPath_OverwritesThatFileAndKeepsItsName()
    {
        var dir = TempDir();
        try
        {
            var library = new SongLibraryService(new SongImportService(), dir);
            var path = library.SaveSong(Song("Grace", ("V1", "old words")), null);

            var returned = library.SaveSong(Song("Grace", ("V1", "new words")), path);

            Assert.Equal(path, returned);
            Assert.Single(Directory.GetFiles(dir));
            Assert.Equal("new words", library.Load(path).Slides[0]);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void SaveAsNew_WhenTheNameIsTaken_CreatesASecondFile()
    {
        var dir = TempDir();
        try
        {
            var library = new SongLibraryService(new SongImportService(), dir);
            var original = library.SaveSong(Song("Grace", ("V1", "one")), null);

            var copy = library.SaveSong(Song("Grace", ("V1", "two")), null);

            Assert.NotEqual(original, copy);
            Assert.Equal(2, Directory.GetFiles(dir).Length);
            Assert.Equal("one", library.Load(original).Slides[0]);
            Assert.Equal("two", library.Load(copy).Slides[0]);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void SetOnlySong_RoundTripsItsOwnWordsAndLabels_WithoutTheLibrary()
    {
        var songsDir = TempDir();
        var setsDir = TempDir();
        try
        {
            var library = new SongLibraryService(new SongImportService(), songsDir);
            var store = new SetXmlStore(library, setsDir, Path.Combine(setsDir, "images"));
            var localCopy = Song("Grace (acoustic)", ("V1", "only in this set"), ("C", "chorus here"));
            localCopy.IsSetOnly = true;

            store.Save(new PresentationSet { Name = "Set", Items = { localCopy } });
            var reloaded = Assert.Single(Assert.Single(store.LoadAll()).Items);

            Assert.True(reloaded.IsSetOnly);
            Assert.Equal(SetItemType.Song, reloaded.Type);
            Assert.Equal("Grace (acoustic)", reloaded.Title);
            Assert.Equal(new[] { "only in this set", "chorus here" }, reloaded.Slides);
            Assert.Equal(new[] { "V1", "C" }, reloaded.SlideLabels);

            // A set-only song has its words already, so it must never be looked up in the library.
            library.EnsureResolved(reloaded);
            Assert.Equal("only in this set", reloaded.Slides[0]);
            Assert.Null(reloaded.LibraryPath);
        }
        finally
        {
            Directory.Delete(songsDir, recursive: true);
            Directory.Delete(setsDir, recursive: true);
        }
    }

    [Fact]
    public void LibraryReference_StillStoresOnlyTheName()
    {
        var songsDir = TempDir();
        var setsDir = TempDir();
        try
        {
            var library = new SongLibraryService(new SongImportService(), songsDir);
            var store = new SetXmlStore(library, setsDir, Path.Combine(setsDir, "images"));
            library.SaveSong(Song("Grace", ("V1", "library words")), null);

            store.Save(new PresentationSet { Name = "Set", Items = { new SetItem { Type = SetItemType.Song, Title = "Grace" } } });
            var text = File.ReadAllText(Path.Combine(setsDir, "Set"));

            Assert.DoesNotContain("library words", text);
            Assert.DoesNotContain("source=\"song\"", text);
        }
        finally
        {
            Directory.Delete(songsDir, recursive: true);
            Directory.Delete(setsDir, recursive: true);
        }
    }

    [Fact]
    public void Clone_CopiesEverythingButSharesNoLists()
    {
        var original = Song("Grace", ("V1", "one"));
        original.IsSetOnly = true;
        original.LibraryPath = @"C:\Songs\Grace";

        var copy = original.Clone();
        copy.Slides.Add("two");
        copy.SlideLabels!.Add("C");

        Assert.Equal(new[] { "one" }, original.Slides);
        Assert.Equal(new[] { "V1" }, original.SlideLabels);
        Assert.True(copy.IsSetOnly);
        Assert.Equal(@"C:\Songs\Grace", copy.LibraryPath);
    }
}
