using BiblePresenter.App.Models;
using BiblePresenter.App.Services;
using Xunit;

namespace BiblePresenter.Tests;

public class SetXmlStoreTests
{
    private static string TempDir() => Path.Combine(Path.GetTempPath(), $"bp-sets-{Guid.NewGuid():N}");

    private static (SetXmlStore Store, SongLibraryService Library, string SetsDir, string SongsDir) BuildStore()
    {
        var setsDir = TempDir();
        var songsDir = TempDir();
        var library = new SongLibraryService(new SongImportService(), songsDir);
        var store = new SetXmlStore(library, setsDir);
        return (store, library, setsDir, songsDir);
    }

    [Fact]
    public void Parse_RealOpenSongExampleFile_ReadsCustomAndScriptureItemsCorrectly()
    {
        var (store, library, setsDir, songsDir) = BuildStore();
        try
        {
            var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "OpenSongSetExample");
            Assert.True(File.Exists(fixturePath), $"Fixture not found at {fixturePath}");
            File.Copy(fixturePath, Path.Combine(setsDir, "Example"));

            var sets = store.LoadAll();
            var set = Assert.Single(sets);

            // 1 custom (Announcements, 3 slides) + 1 scripture (Colossians, 2 slides) + 4 song
            // references (left unresolved at load time - see SetXmlStore) + 1 scripture
            // (Philippians, 1 slide) = 7 items total, in original order.
            Assert.Equal(7, set.Items.Count);

            var announcements = set.Items[0];
            Assert.Equal(SetItemType.Custom, announcements.Type);
            Assert.Equal("Announcements", announcements.Title);
            Assert.Equal("For April 26th, 2004", announcements.Subtitle);
            Assert.Equal(3, announcements.Slides.Count);
            Assert.Contains("Prayer Meeting", announcements.Slides[0]);

            var colossians = set.Items[1];
            Assert.Equal(SetItemType.Scripture, colossians.Type);
            Assert.Equal("Colossians 1:11-14", colossians.Title);
            Assert.Equal("NKJV", colossians.Subtitle);
            Assert.Equal(2, colossians.Slides.Count);

            var song = set.Items[2];
            Assert.Equal(SetItemType.Song, song.Type);
            Assert.Equal("And Can It Be", song.Title);
            Assert.Empty(song.Slides); // left unresolved until selected/presented

            library.EnsureResolved(song);
            Assert.Contains("not found", song.Slides[0]); // empty test library, so no match

            var philippians = set.Items[6];
            Assert.Equal(SetItemType.Scripture, philippians.Type);
            Assert.Equal("Philippians 4:6-7", philippians.Title);
        }
        finally
        {
            Directory.Delete(setsDir, recursive: true);
            Directory.Delete(songsDir, recursive: true);
        }
    }

    [Fact]
    public void SaveThenLoad_RoundTripsScriptureAndCustomItems()
    {
        var (store, _, setsDir, songsDir) = BuildStore();
        try
        {
            var set = new PresentationSet
            {
                Name = "Sunday Service",
                Items =
                {
                    new SetItem { Type = SetItemType.Scripture, Title = "Psalms 126:1-6", Subtitle = "KJV", Slides = { "1 When the LORD...", "2 Then was..." } },
                    new SetItem { Type = SetItemType.Custom, Title = "OPENING PRAYER", Slides = { "OPENING PRAYER" } }
                }
            };

            store.Save(set);
            var reloaded = Assert.Single(store.LoadAll());

            Assert.Equal("Sunday Service", reloaded.Name);
            Assert.Equal(2, reloaded.Items.Count);
            Assert.Equal("Psalms 126:1-6", reloaded.Items[0].Title);
            Assert.Equal("KJV", reloaded.Items[0].Subtitle);
            Assert.Equal(2, reloaded.Items[0].Slides.Count);
            Assert.Equal(SetItemType.Custom, reloaded.Items[1].Type);
        }
        finally
        {
            Directory.Delete(setsDir, recursive: true);
            Directory.Delete(songsDir, recursive: true);
        }
    }

    [Fact]
    public void SaveThenLoad_SongItem_PersistsAsNameReferenceAndResolvesFromLibrary()
    {
        var (store, library, setsDir, songsDir) = BuildStore();
        try
        {
            library.SaveToLibrary(new SetItem
            {
                Type = SetItemType.Song,
                Title = "Amazing Grace",
                Subtitle = "John Newton",
                Slides = { "Amazing grace, how sweet the sound" }
            });

            var set = new PresentationSet
            {
                Name = "Worship Set",
                Items = { new SetItem { Type = SetItemType.Song, Title = "Amazing Grace" } }
            };
            store.Save(set);

            // The set file itself should only contain a bare reference, not baked-in lyrics.
            var savedText = File.ReadAllText(Path.Combine(setsDir, "Worship Set"));
            Assert.Contains("type=\"song\"", savedText);
            Assert.DoesNotContain("sweet the sound", savedText);

            var reloaded = Assert.Single(store.LoadAll());
            var songItem = Assert.Single(reloaded.Items);
            Assert.Equal(SetItemType.Song, songItem.Type);
            Assert.Equal("Amazing Grace", songItem.Title);
            Assert.Empty(songItem.Slides); // left unresolved until selected/presented

            library.EnsureResolved(songItem);
            Assert.Equal("John Newton", songItem.Subtitle);
            Assert.Contains("sweet the sound", songItem.Slides[0]);
        }
        finally
        {
            Directory.Delete(setsDir, recursive: true);
            Directory.Delete(songsDir, recursive: true);
        }
    }

    [Fact]
    public void Save_UnderNewName_DeletesThePreviousFile()
    {
        var (store, _, setsDir, songsDir) = BuildStore();
        try
        {
            var set = new PresentationSet { Name = "First Name" };
            store.Save(set);
            Assert.True(File.Exists(Path.Combine(setsDir, "First Name")));

            set.Name = "Renamed";
            store.Save(set);

            Assert.False(File.Exists(Path.Combine(setsDir, "First Name")));
            Assert.True(File.Exists(Path.Combine(setsDir, "Renamed")));
            Assert.Single(store.LoadAll());
        }
        finally
        {
            Directory.Delete(setsDir, recursive: true);
            Directory.Delete(songsDir, recursive: true);
        }
    }

    [Fact]
    public void Delete_RemovesTheSetFile()
    {
        var (store, _, setsDir, songsDir) = BuildStore();
        try
        {
            var set = new PresentationSet { Name = "Temp Set" };
            store.Save(set);
            Assert.Single(store.LoadAll());

            // Delete() needs the store's own tracking, populated by Save/LoadAll for this set's Id.
            store.Delete(set);

            Assert.Empty(store.LoadAll());
        }
        finally
        {
            Directory.Delete(setsDir, recursive: true);
            Directory.Delete(songsDir, recursive: true);
        }
    }
}
