using System.IO;
using System.Xml.Linq;
using BiblePresenter.App.Models;

namespace BiblePresenter.App.Services;

/// <summary>
/// Reads/writes Sets in OpenSong's own native XML format (&lt;set&gt;&lt;slide_groups&gt;&lt;slide_group&gt;),
/// stored in the user-visible %USERPROFILE%\Sets folder, so set files are interchangeable with
/// real OpenSong. Song items are persisted as bare name references (matching OpenSong) and
/// resolved against the song library at load time; scripture/custom items are baked in directly.
///
/// Known gap: per-item &lt;style&gt; overrides and "image" type items aren't understood yet - image
/// items round-trip as a placeholder custom slide so the set's item count/order still survives.
/// </summary>
public sealed class SetXmlStore
{
    private readonly string _folder;
    private readonly SongLibraryService _songLibrary;

    /// <summary>Tracks which on-disk file each in-memory set (by its session-local Id) came from, for rename/delete.</summary>
    private readonly Dictionary<string, string> _fileNameBySetId = new();

    public SetXmlStore(SongLibraryService songLibrary, string? folder = null)
    {
        _songLibrary = songLibrary;
        _folder = folder ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Sets");
        Directory.CreateDirectory(_folder);
    }

    public string Folder => _folder;

    public List<PresentationSet> LoadAll()
    {
        var result = new List<PresentationSet>();
        foreach (var file in Directory.EnumerateFiles(_folder))
        {
            var set = TryLoad(file);
            if (set is not null)
            {
                result.Add(set);
                _fileNameBySetId[set.Id] = Path.GetFileName(file);
            }
        }

        result.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return result;
    }

    private PresentationSet? TryLoad(string filePath)
    {
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
        if (root is null || !root.Name.LocalName.Equals("set", StringComparison.OrdinalIgnoreCase))
            return null;

        var name = root.Attribute("name")?.Value;
        if (string.IsNullOrWhiteSpace(name))
            name = Path.GetFileNameWithoutExtension(filePath);

        var groups = root.Element("slide_groups")?.Elements("slide_group") ?? Enumerable.Empty<XElement>();
        var items = groups.Select(ParseSlideGroup).ToList();

        return new PresentationSet { Name = name, Items = items };
    }

    private SetItem ParseSlideGroup(XElement group)
    {
        var typeAttr = group.Attribute("type")?.Value ?? "custom";
        var name = group.Attribute("name")?.Value ?? "Untitled";

        if (typeAttr.Equals("song", StringComparison.OrdinalIgnoreCase))
        {
            // Deliberately not resolved here: a set can reference many songs, and eagerly
            // resolving all of them against a large library would multiply the scan cost at
            // set-load time. Resolved lazily via SongLibraryService.EnsureResolved when the
            // item is actually selected or presented.
            return new SetItem { Type = SetItemType.Song, Title = name };
        }

        var isScripture = typeAttr.Equals("scripture", StringComparison.OrdinalIgnoreCase);
        var isCustom = typeAttr.Equals("custom", StringComparison.OrdinalIgnoreCase);

        var title = group.Element("title")?.Value.Trim();
        if (string.IsNullOrWhiteSpace(title))
            title = name;

        var subtitle = group.Element("subtitle")?.Value.Trim() ?? "";

        var slides = group.Element("slides")?.Elements("slide")
            .Select(s => s.Element("body")?.Value.Trim() ?? "")
            .Where(s => s.Length > 0)
            .ToList() ?? new List<string>();

        if (slides.Count == 0 && !isScripture && !isCustom)
        {
            // An item type we don't understand yet (e.g. "image") - keep a placeholder so the
            // set's item count/order still survives a round trip through this app.
            slides.Add($"[Unsupported item type '{typeAttr}' - open in OpenSong to edit]");
        }

        var type = isScripture ? SetItemType.Scripture : SetItemType.Custom;
        return new SetItem { Type = type, Title = title, Subtitle = subtitle, Slides = slides };
    }

    public void Save(PresentationSet set)
    {
        var fileName = SanitizeFileName(set.Name);

        if (_fileNameBySetId.TryGetValue(set.Id, out var previousFileName) && previousFileName != fileName)
        {
            var previousPath = Path.Combine(_folder, previousFileName);
            if (File.Exists(previousPath))
                File.Delete(previousPath);
        }

        var document = new XDocument(
            new XElement("set",
                new XAttribute("name", set.Name),
                new XElement("slide_groups", set.Items.Select(BuildSlideGroup))));

        document.Save(Path.Combine(_folder, fileName));
        _fileNameBySetId[set.Id] = fileName;
    }

    private static XElement BuildSlideGroup(SetItem item)
    {
        if (item.Type == SetItemType.Song)
        {
            // Match OpenSong's own convention: songs are referenced by name and resolved from
            // the library rather than baked into the set file, so they always present with the
            // song's current lyrics.
            return new XElement("slide_group",
                new XAttribute("name", item.Title),
                new XAttribute("type", "song"),
                new XAttribute("presentation", ""),
                new XAttribute("path", ""));
        }

        var typeAttr = item.Type == SetItemType.Scripture ? "scripture" : "custom";
        return new XElement("slide_group",
            new XAttribute("name", item.Title),
            new XAttribute("type", typeAttr),
            new XElement("title", item.Title),
            new XElement("subtitle", item.Subtitle),
            new XElement("notes"),
            new XElement("slides",
                item.Slides.Select(s => new XElement("slide", new XElement("body", s)))));
    }

    public void Delete(PresentationSet set)
    {
        if (!_fileNameBySetId.TryGetValue(set.Id, out var fileName))
            return;

        var path = Path.Combine(_folder, fileName);
        if (File.Exists(path))
            File.Delete(path);
        _fileNameBySetId.Remove(set.Id);
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        return sanitized.Length == 0 ? "New Set" : sanitized;
    }
}
