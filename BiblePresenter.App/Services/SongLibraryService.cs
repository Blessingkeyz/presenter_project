using System.IO;
using BiblePresenter.App.Models;

namespace BiblePresenter.App.Services;

/// <summary>
/// Manages the user-visible song library folder (%USERPROFILE%\Songs, matching OpenSong's own
/// convention) so users can drop OpenSong song files straight in, and so a set's bare "type=song"
/// name references can be resolved to real lyrics at load/present time.
///
/// The folder listing is cached after the first scan (real libraries can hold thousands of
/// files, and a set can reference many songs - re-scanning per lookup would multiply that cost).
/// Cache is invalidated whenever this service adds or changes a file.
/// </summary>
public sealed class SongLibraryService
{
    private readonly string _folder;
    private readonly SongImportService _importService;
    private List<(string Title, string FilePath)>? _cache;
    private List<(string Title, string FilePath, string Lyrics)>? _lyricsCache;

    public SongLibraryService(SongImportService importService, string? folder = null)
    {
        _importService = importService;
        _folder = folder ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Songs");
        Directory.CreateDirectory(_folder);
    }

    public string Folder => _folder;

    /// <summary>
    /// Forces both the title and lyrics caches to build right now, instead of lazily on the first
    /// search. Meant to be called from a background thread during app startup (behind a splash
    /// screen) so the first "By Lyrics" search a user runs doesn't pay the full-library scan cost
    /// on the UI thread and freeze the app.
    /// </summary>
    public void WarmUp() => EnsureScanned();

    /// <summary>Lists every song in the library (title + file path), skipping files that don't parse as songs.</summary>
    public List<(string Title, string FilePath)> ListSongs()
    {
        EnsureScanned();
        return _cache!;
    }

    /// <summary>
    /// Scans the folder once, reading each file's title and lyrics together - both come from the same XML
    /// parse, so reading them separately (as this used to, in two full passes) parsed every file twice for
    /// no reason. This alone roughly halves the scan's work.
    ///
    /// Tried and reverted: running this scan in parallel (PLINQ's AsParallel). It measured ~4x slower on a
    /// ~2,800-file library, not faster - these files are small (parsing each takes well under a millisecond
    /// of real work), so PLINQ's own per-item scheduling and result-aggregation overhead outweighs anything
    /// splitting the work across cores could save. Parallelism pays off for a handful of expensive items,
    /// not thousands of cheap ones. Don't re-add it without measuring on a real large library first.
    /// </summary>
    private void EnsureScanned()
    {
        if (_lyricsCache is not null)
            return;

        var scanned = new List<(string Title, string FilePath, string Lyrics)>();
        foreach (var file in Directory.EnumerateFiles(_folder))
        {
            if (_importService.PeekTitleAndLyrics(file) is { } peek)
                scanned.Add((peek.Title, file, peek.Lyrics));
        }

        _lyricsCache = scanned;
        _cache = scanned
            .Select(s => (s.Title, s.FilePath))
            .OrderBy(s => s.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Finds a song in the library by title (case-insensitive) and imports it, or null if not found.</summary>
    public SetItem? ResolveByTitle(string title)
    {
        foreach (var (songTitle, path) in ListSongs())
        {
            if (string.Equals(songTitle, title, StringComparison.OrdinalIgnoreCase))
                return _importService.Import(path);
        }
        return null;
    }

    /// <summary>Reads one song file from the library, e.g. for editing it.</summary>
    public SetItem Load(string path) => _importService.Import(path);

    /// <summary>
    /// Searches the library by title (substring) or by lyrics content. Lyrics search reads the
    /// full text of every song once and caches it (same rationale as the title cache: real
    /// libraries can hold thousands of files, so this must not re-scan per keystroke).
    /// </summary>
    public List<SongSearchResult> Search(string query, bool byLyrics)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new List<SongSearchResult>();

        EnsureScanned();

        if (!byLyrics)
        {
            return _cache!
                .Where(s => s.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Select(s => new SongSearchResult { Title = s.Title, FilePath = s.FilePath })
                .ToList();
        }

        return _lyricsCache!
            .Where(s => s.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                        || s.Lyrics.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Select(s => new SongSearchResult { Title = s.Title, FilePath = s.FilePath })
            .ToList();
    }

    /// <summary>
    /// Fills in a song-type SetItem's Slides/Subtitle from the library if it hasn't been resolved
    /// yet (see <see cref="SetXmlStore"/> - song references are left unresolved at set-load time
    /// to avoid scanning the whole library for every reference). Safe to call repeatedly; a no-op
    /// once resolved. Mutates the item in place, so bound UI (Slide Editor) picks it up automatically.
    /// </summary>
    public void EnsureResolved(SetItem item)
    {
        if (item.Type != SetItemType.Song || item.IsSetOnly || item.Slides.Count > 0)
            return;

        var resolved = ResolveByTitle(item.Title);
        if (resolved is not null)
        {
            item.Subtitle = resolved.Subtitle;
            item.Slides = resolved.Slides;
            item.SlideLabels = resolved.SlideLabels;
            item.LibraryPath = resolved.LibraryPath;
        }
        else
        {
            item.Slides = new List<string> { "(song not found in library)" };
        }
    }

    /// <summary>Copies an external song file into the library so it becomes part of it going forward.</summary>
    public string AddExternalFile(string sourceFilePath)
    {
        var destFileName = Path.GetFileName(sourceFilePath);
        var destPath = Path.Combine(_folder, destFileName);
        destPath = MakeUnique(destPath);
        File.Copy(sourceFilePath, destPath);
        InvalidateCache();
        return destPath;
    }

    /// <summary>
    /// Writes a song to the library as an OpenSong file and returns its path. With
    /// <paramref name="existingPath"/> the file is overwritten in place (so OpenSong and any other
    /// app that reads that file see the change); without it a new file is created, named after the title.
    /// </summary>
    public string SaveSong(SetItem song, string? existingPath)
    {
        if (existingPath is not null)
        {
            _importService.SaveInPlace(song, existingPath);
            InvalidateCache();
            return existingPath;
        }

        var path = MakeUnique(Path.Combine(_folder, SanitizeFileName(song.Title)));
        _importService.Export(song, path);
        InvalidateCache();
        return path;
    }

    /// <summary>Both caches are always (re)built together in <see cref="EnsureScanned"/>, so they're always invalidated together too.</summary>
    private void InvalidateCache()
    {
        _cache = null;
        _lyricsCache = null;
    }

    /// <summary>True when saving this song's lyrics into <paramref name="existingPath"/> would drop chord rows or verse numbers from that file.</summary>
    public bool WouldRewriteChordedLyrics(SetItem song, string? existingPath)
        => existingPath is not null
           && File.Exists(existingPath)
           && _importService.HasChordLayout(existingPath)
           && !_importService.SameLyrics(_importService.Import(existingPath), song);

    private static string MakeUnique(string path)
    {
        if (!File.Exists(path))
            return path;

        var dir = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        var i = 1;
        string candidate;
        do
        {
            candidate = Path.Combine(dir, $"{name} ({i}){ext}");
            i++;
        } while (File.Exists(candidate));

        return candidate;
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        return sanitized.Length == 0 ? "Untitled Song" : sanitized;
    }
}
