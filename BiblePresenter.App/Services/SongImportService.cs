using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using BiblePresenter.App.Models;

namespace BiblePresenter.App.Services;

/// <summary>
/// Imports OpenSong-format song files: &lt;song&gt;&lt;title&gt;/&lt;lyrics&gt;/&lt;author&gt;, where
/// &lt;lyrics&gt; is one blob of text with [v1]/[c]/[b]-style section tags marking stanza
/// boundaries. These files conventionally have no file extension.
/// </summary>
public sealed class SongImportService
{
    private static readonly Regex SectionTag = new(@"^\[[^\]]+\]$", RegexOptions.Compiled);

    public SetItem Import(string filePath)
    {
        XDocument document;
        try
        {
            document = XDocument.Load(filePath);
        }
        catch (Exception ex)
        {
            throw new BibleImportException($"'{Path.GetFileName(filePath)}' is not valid XML: {ex.Message}", ex);
        }

        var root = document.Root;
        if (root is null || !root.Name.LocalName.Equals("song", StringComparison.OrdinalIgnoreCase))
            throw new BibleImportException($"'{Path.GetFileName(filePath)}' doesn't look like an OpenSong song file.");

        var title = root.Element("title")?.Value.Trim();
        if (string.IsNullOrWhiteSpace(title))
            title = Path.GetFileNameWithoutExtension(filePath);

        var author = root.Element("author")?.Value.Trim() ?? "";
        var lyrics = root.Element("lyrics")?.Value ?? "";
        var sections = SplitLyricsIntoSections(lyrics);

        return new SetItem
        {
            Type = SetItemType.Song,
            Title = title,
            Subtitle = author,
            Slides = sections.Select(s => s.Text).ToList(),
            SlideLabels = sections.Select(s => s.Label).ToList(),
            LibraryPath = filePath
        };
    }

    /// <summary>
    /// Reads just the title from a song file, without parsing/splitting the full lyrics - much
    /// cheaper than <see cref="Import"/> when listing a large library (thousands of files).
    /// Returns null if the file isn't a recognizable song file.
    /// </summary>
    public string? PeekTitle(string filePath)
    {
        if (!LooksLikeXml(filePath))
            return null;

        XDocument document;
        try
        {
            document = XDocument.Load(filePath);
        }
        catch (Exception)
        {
            return null;
        }

        var root = document.Root;
        if (root is null || !root.Name.LocalName.Equals("song", StringComparison.OrdinalIgnoreCase))
            return null;

        var title = root.Element("title")?.Value.Trim();
        return string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(filePath) : title;
    }

    /// <summary>
    /// Reads the title and raw (unsplit) lyrics text - cheaper than <see cref="Import"/> since it
    /// skips slide-splitting, used to build a searchable-by-lyrics index over a large library.
    /// Returns null if the file isn't a recognizable song file.
    /// </summary>
    public (string Title, string Lyrics)? PeekTitleAndLyrics(string filePath)
    {
        if (!LooksLikeXml(filePath))
            return null;

        XDocument document;
        try
        {
            document = XDocument.Load(filePath);
        }
        catch (Exception)
        {
            return null;
        }

        var root = document.Root;
        if (root is null || !root.Name.LocalName.Equals("song", StringComparison.OrdinalIgnoreCase))
            return null;

        var title = root.Element("title")?.Value.Trim();
        if (string.IsNullOrWhiteSpace(title))
            title = Path.GetFileNameWithoutExtension(filePath);

        return (title, root.Element("lyrics")?.Value ?? "");
    }

    /// <summary>
    /// Cheap check so bulk-scanning a large folder (real song libraries can hold thousands of
    /// files, many not song XML at all - notes, programs, stray documents) doesn't pay for a
    /// full XML parse + exception per non-XML file.
    /// </summary>
    private static bool LooksLikeXml(string filePath)
    {
        try
        {
            using var reader = new StreamReader(filePath);
            var buffer = new char[256];
            var read = reader.Read(buffer, 0, buffer.Length);
            var head = new string(buffer, 0, read).TrimStart();
            return head.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase)
                   || head.StartsWith("<song", StringComparison.OrdinalIgnoreCase);
        }
        catch (IOException)
        {
            return false;
        }
    }

    /// <summary>Writes a song item back out as an OpenSong-format file. Sections keep their own tags (V1, C, B...); untagged ones get [v1], [v2]...</summary>
    public void Export(SetItem item, string filePath)
    {
        var document = new XDocument(
            new XElement("song",
                new XElement("title", item.Title),
                new XElement("lyrics", LyricsText(item)),
                new XElement("author", item.Subtitle)));

        document.Save(filePath);
    }

    /// <summary>
    /// Saves a song into an existing file by changing only what differs: the title, the author, and the
    /// lyrics. Everything else in the file (copyright, presentation order, chord rows the app doesn't
    /// show...) is kept as it is. Writes nothing when the song is unchanged. Returns true if it wrote.
    /// </summary>
    public bool SaveInPlace(SetItem song, string filePath)
    {
        if (!File.Exists(filePath))
        {
            Export(song, filePath);
            return true;
        }

        var document = XDocument.Load(filePath);
        var root = document.Root!;
        var current = Import(filePath);

        var titleChanged = current.Title != song.Title;
        var authorChanged = current.Subtitle != song.Subtitle;
        var lyricsChanged = !SameLyrics(current, song);
        if (!titleChanged && !authorChanged && !lyricsChanged)
            return false;

        if (titleChanged)
            SetChildText(root, "title", song.Title);
        if (authorChanged)
            SetChildText(root, "author", song.Subtitle);
        if (lyricsChanged)
            SetChildText(root, "lyrics", LyricsText(song));

        document.Save(filePath);
        return true;
    }

    /// <summary>True when the song's sections (text and tags) are the same as the file's.</summary>
    public bool SameLyrics(SetItem a, SetItem b)
        => a.Slides.SequenceEqual(b.Slides)
           && Enumerable.Range(0, a.Slides.Count).All(i => a.LabelAt(i) == b.LabelAt(i));

    /// <summary>True when the file's lyrics have chord rows or verse numbers, which the app reads but doesn't keep when it rewrites lyrics.</summary>
    public bool HasChordLayout(string filePath)
    {
        XDocument document;
        try
        {
            document = XDocument.Load(filePath);
        }
        catch (Exception)
        {
            return false;
        }

        var lyrics = document.Root?.Element("lyrics")?.Value ?? "";
        return lyrics.Replace("\r\n", "\n").Split('\n')
            .Any(l => l.StartsWith('.') || VerseNumberPrefix.IsMatch(l));
    }

    /// <summary>Compares two XML elements ignoring indentation and line-ending differences in the text.</summary>
    public static bool SameXml(XElement a, XElement b)
    {
        if (a.Name != b.Name)
            return false;

        var attributesA = a.Attributes().ToDictionary(x => x.Name, x => x.Value);
        var attributesB = b.Attributes().ToDictionary(x => x.Name, x => x.Value);
        if (attributesA.Count != attributesB.Count || attributesA.Any(kv => !attributesB.TryGetValue(kv.Key, out var v) || v != kv.Value))
            return false;

        var childrenA = a.Elements().ToList();
        var childrenB = b.Elements().ToList();
        if (childrenA.Count != childrenB.Count)
            return false;
        if (childrenA.Count == 0)
            return a.Value.Replace("\r\n", "\n").Trim() == b.Value.Replace("\r\n", "\n").Trim();

        return childrenA.Zip(childrenB).All(pair => SameXml(pair.First, pair.Second));
    }

    private static string LyricsText(SetItem item) => string.Join("\n\n", item.Slides.Select((slide, i) =>
    {
        var label = item.LabelAt(i).Trim();
        return $"[{(label.Length > 0 ? label : $"v{i + 1}")}]\n\n{slide}";
    }));

    private static void SetChildText(XElement root, string name, string value)
    {
        var element = root.Element(name);
        if (element is null)
            root.Add(new XElement(name, value));
        else
            element.Value = value;
    }

    private readonly record struct Section(string Label, string Text);

    private static List<Section> SplitLyricsIntoSections(string lyrics)
    {
        var normalized = lyrics.Replace("\r\n", "\n");
        var lines = normalized.Split('\n');

        var hasTags = lines.Any(l => SectionTag.IsMatch(l.Trim()));
        if (lines.Any(l => l.StartsWith('.') || VerseNumberPrefix.IsMatch(l)))
            return SplitChordedHymn(lines, hasTags);

        if (hasTags)
            return SplitByTags(lines);

        // No [tag] markers - fall back to blank-line-separated stanzas, which carry no labels.
        return normalized
            .Split("\n\n")
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .Select(s => new Section("", s))
            .ToList();
    }

    /// <summary>A verse number glued to the start of a lyric line ("1He leadeth me") - the digits are the verse.</summary>
    private static readonly Regex VerseNumberPrefix = new(@"^(\d+)(?=[A-Za-z'""(‘’“”])", RegexOptions.Compiled);

    /// <summary>
    /// Reads chorded hymn layout. Lines starting with "." are chord rows and are not shown. A leading
    /// number groups lines into verses: all lines numbered 1 - even from different stanzas - become
    /// verse 1. Underscores mark held syllables and are dropped. Lines that aren't numbered belong to the
    /// last [tag] above them (e.g. [C]) when the song has tags; otherwise blank lines separate them.
    /// </summary>
    private static List<Section> SplitChordedHymn(string[] lines, bool hasTags)
    {
        var blocks = new List<(string Label, List<string> Lines)>();
        var verseBlocks = new Dictionary<int, List<string>>();
        List<string>? plainBlock = null;
        var tag = "";

        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();
            if (SectionTag.IsMatch(line.Trim()))
            {
                tag = line.Trim()[1..^1].Trim();
                plainBlock = null;
                continue;
            }

            if (line.Length == 0)
            {
                // Untagged songs split stanzas on blank lines; tagged ones run to the next tag.
                if (!hasTags)
                    plainBlock = null;
                continue;
            }

            if (line.StartsWith('.'))
                continue;

            var match = VerseNumberPrefix.Match(line);
            if (match.Success)
            {
                var verse = int.TryParse(match.Groups[1].Value, out var number) ? number : 0;
                if (!verseBlocks.TryGetValue(verse, out var verseLines))
                {
                    verseLines = new List<string>();
                    verseBlocks[verse] = verseLines;
                    blocks.Add(($"V{verse}", verseLines));
                }

                verseLines.Add(CleanLyric(line[match.Groups[1].Length..]));
                plainBlock = null;
            }
            else
            {
                if (plainBlock is null)
                {
                    plainBlock = new List<string>();
                    blocks.Add((tag, plainBlock));
                }

                plainBlock.Add(CleanLyric(line));
            }
        }

        return blocks
            .Select(b => new Section(b.Label, string.Join("\n", b.Lines)))
            .Where(s => s.Text.Length > 0)
            .ToList();
    }

    private static string CleanLyric(string lyric) => lyric.Replace("_", "").Trim();


    private static List<Section> SplitByTags(string[] lines)
    {
        var sections = new List<Section>();
        var current = new List<string>();
        var label = "";

        void Flush()
        {
            var contentLines = current.Select(l => l.Trim()).ToList();
            while (contentLines.Count > 0 && contentLines[0].Length == 0)
                contentLines.RemoveAt(0);
            while (contentLines.Count > 0 && contentLines[^1].Length == 0)
                contentLines.RemoveAt(contentLines.Count - 1);

            current.Clear();
            if (contentLines.Count > 0)
                sections.Add(new Section(label, string.Join("\n", contentLines)));
        }

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (SectionTag.IsMatch(trimmed))
            {
                Flush();
                label = trimmed[1..^1].Trim();
                continue;
            }
            current.Add(line);
        }
        Flush();

        return sections;
    }
}
