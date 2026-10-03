using BiblePresenter.App.Models;
using BiblePresenter.App.Services;

namespace BiblePresenter.Tests;

/// <summary>Saving must change only what was edited: files the user didn't change stay byte-for-byte the same.</summary>
public class SourceFidelityTests
{
    private static string TempDir() => Path.Combine(Path.GetTempPath(), $"bp-fidelity-{Guid.NewGuid():N}");

    private const string SampleSet = """
        <?xml version="1.0" encoding="utf-8"?>
        <set name="Sample" version="2" extra="kept">
          <slide_groups>
            <slide_group name="Psalm" type="scripture" print="true" resize="body">
              <title>Psalm 1</title>
              <subtitle>KJV</subtitle>
              <notes />
              <slides>
                <slide><body>1 Blessed is the man. 2 And he shall be like a tree.</body></slide>
              </slides>
            </slide_group>
            <slide_group name="Note" type="custom">
              <title>Note</title>
              <subtitle />
              <notes />
              <slides><slide><body>Hello</body></slide></slides>
            </slide_group>
          </slide_groups>
        </set>
        """;

    private const string ChordedSong = """
        <?xml version="1.0" encoding="UTF-8"?>
        <song>
          <title>Grace</title>
          <author>Newton</author>
          <copyright>Public Domain</copyright>
          <presentation>V1 C</presentation>
          <lyrics>[V]
        .  D      G
        1Amazing grace how sweet

        [C]
         How sweet the sound
        </lyrics>
        </song>
        """;

    [Fact]
    public void Set_SavedWithoutChanges_LeavesTheFileByteForByteTheSame()
    {
        var setsDir = TempDir();
        var songsDir = TempDir();
        try
        {
            Directory.CreateDirectory(setsDir);
            var path = Path.Combine(setsDir, "Sample");
            File.WriteAllText(path, SampleSet);
            var original = File.ReadAllBytes(path);
            var store = new SetXmlStore(new SongLibraryService(new SongImportService(), songsDir), setsDir, Path.Combine(setsDir, "images"));

            var set = Assert.Single(store.LoadAll());
            store.Save(set);

            Assert.Equal(original, File.ReadAllBytes(path));
        }
        finally
        {
            Directory.Delete(setsDir, recursive: true);
            Directory.Delete(songsDir, recursive: true);
        }
    }

    [Fact]
    public void Set_ChangingOneItem_RewritesOnlyThatItem_AndKeepsTheRestOfTheFile()
    {
        var setsDir = TempDir();
        var songsDir = TempDir();
        try
        {
            Directory.CreateDirectory(setsDir);
            var path = Path.Combine(setsDir, "Sample");
            File.WriteAllText(path, SampleSet);
            var store = new SetXmlStore(new SongLibraryService(new SongImportService(), songsDir), setsDir, Path.Combine(setsDir, "images"));
            var set = Assert.Single(store.LoadAll());

            set.Items[1].Title = "Changed note";
            store.Save(set);
            var text = File.ReadAllText(path);

            Assert.Contains("Changed note", text);
            Assert.DoesNotContain(">Note<", text);
            Assert.Contains("extra=\"kept\"", text);
            Assert.Contains("resize=\"body\"", text);
            Assert.Contains("1 Blessed is the man. 2 And he shall be like a tree.", text); // packed verses kept as written
        }
        finally
        {
            Directory.Delete(setsDir, recursive: true);
            Directory.Delete(songsDir, recursive: true);
        }
    }

    [Fact]
    public void Song_SavedWithoutChanges_LeavesTheFileByteForByteTheSame()
    {
        var songsDir = TempDir();
        try
        {
            Directory.CreateDirectory(songsDir);
            var path = Path.Combine(songsDir, "Grace");
            File.WriteAllText(path, ChordedSong);
            var original = File.ReadAllBytes(path);
            var library = new SongLibraryService(new SongImportService(), songsDir);

            library.SaveSong(library.Load(path), path);

            Assert.Equal(original, File.ReadAllBytes(path));
        }
        finally
        {
            Directory.Delete(songsDir, recursive: true);
        }
    }

    [Fact]
    public void Song_TitleChange_KeepsTheLyricsAndEveryOtherField()
    {
        var songsDir = TempDir();
        try
        {
            Directory.CreateDirectory(songsDir);
            var path = Path.Combine(songsDir, "Grace");
            File.WriteAllText(path, ChordedSong);
            var library = new SongLibraryService(new SongImportService(), songsDir);
            var song = library.Load(path);

            song.Title = "Amazing Grace";
            library.SaveSong(song, path);
            var text = File.ReadAllText(path);

            Assert.Contains("<title>Amazing Grace</title>", text);
            Assert.Contains("<copyright>Public Domain</copyright>", text);
            Assert.Contains("<presentation>V1 C</presentation>", text);
            Assert.Contains("1Amazing grace how sweet", text); // lyrics untouched, chords and all
        }
        finally
        {
            Directory.Delete(songsDir, recursive: true);
        }
    }

    [Fact]
    public void Song_ChangingLyricsInAChordedFile_IsFlaggedBeforeChordsAreDropped()
    {
        var songsDir = TempDir();
        try
        {
            Directory.CreateDirectory(songsDir);
            var path = Path.Combine(songsDir, "Grace");
            File.WriteAllText(path, ChordedSong);
            var library = new SongLibraryService(new SongImportService(), songsDir);
            var song = library.Load(path);
            Assert.True(new SongImportService().HasChordLayout(path));

            song.Slides[0] = "Amazing grace, edited";
            Assert.True(library.WouldRewriteChordedLyrics(song, path));

            library.SaveSong(song, path);
            var text = File.ReadAllText(path);
            Assert.Contains("<copyright>Public Domain</copyright>", text);
            Assert.DoesNotContain("1Amazing grace how sweet", text);
            Assert.Contains("Amazing grace, edited", text);
        }
        finally
        {
            Directory.Delete(songsDir, recursive: true);
        }
    }

    [Fact]
    public void Song_ChangingLyricsInAPlainFile_IsNotFlagged()
    {
        var songsDir = TempDir();
        try
        {
            Directory.CreateDirectory(songsDir);
            var path = Path.Combine(songsDir, "Plain");
            File.WriteAllText(path, "<song><title>Plain</title><lyrics>[v1]\n\nfirst words</lyrics></song>");
            var library = new SongLibraryService(new SongImportService(), songsDir);
            var song = library.Load(path);

            song.Slides[0] = "new words";

            Assert.False(library.WouldRewriteChordedLyrics(song, path));
        }
        finally
        {
            Directory.Delete(songsDir, recursive: true);
        }
    }
}
