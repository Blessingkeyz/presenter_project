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
        var slides = SplitLyricsIntoSlides(lyrics);

        return new SetItem
        {
            Type = SetItemType.Song,
            Title = title,
            Subtitle = author,
            Slides = slides
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

    /// <summary>Writes a song item back out as an OpenSong-format file, regenerating generic [v1]/[v2]/... section tags.</summary>
    public void Export(SetItem item, string filePath)
    {
        var lyrics = string.Join("\n\n", item.Slides.Select((slide, i) => $"[v{i + 1}]\n\n{slide}"));

        var document = new XDocument(
            new XElement("song",
                new XElement("title", item.Title),
                new XElement("lyrics", lyrics),
                new XElement("author", item.Subtitle)));

        document.Save(filePath);
    }

    private static List<string> SplitLyricsIntoSlides(string lyrics)
    {
        var normalized = lyrics.Replace("\r\n", "\n");
        var lines = normalized.Split('\n');

        if (lines.Any(l => SectionTag.IsMatch(l.Trim())))
            return SplitByTags(lines);

        // No [tag] markers - fall back to blank-line-separated stanzas.
        return normalized
            .Split("\n\n")
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();
    }

    private static List<string> SplitByTags(string[] lines)
    {
        var slides = new List<string>();
        var current = new List<string>();

        void Flush()
        {
            var contentLines = current.Select(l => l.Trim()).ToList();
            while (contentLines.Count > 0 && contentLines[0].Length == 0)
                contentLines.RemoveAt(0);
            while (contentLines.Count > 0 && contentLines[^1].Length == 0)
                contentLines.RemoveAt(contentLines.Count - 1);

            current.Clear();
            if (contentLines.Count > 0)
                slides.Add(string.Join("\n", contentLines));
        }

        foreach (var line in lines)
        {
            if (SectionTag.IsMatch(line.Trim()))
            {
                Flush();
                continue;
            }
            current.Add(line);
        }
        Flush();

        return slides;
    }
}
