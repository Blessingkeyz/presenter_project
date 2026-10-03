using System.IO;
using System.Security.Cryptography;
using System.Xml.Linq;
using BiblePresenter.App.Models;

namespace BiblePresenter.App.Services;

/// <summary>
/// Reads/writes Sets in OpenSong's own native XML format (&lt;set&gt;&lt;slide_groups&gt;&lt;slide_group&gt;),
/// stored in the user-visible %USERPROFILE%\Sets folder, so set files are interchangeable with
/// real OpenSong. Song items are persisted as bare name references (matching OpenSong) and
/// resolved against the song library at load time; scripture/custom items are baked in directly.
/// Image items embed the picture itself as base64 (exactly how OpenSong does it), so a set with a
/// flyer in it still works after the original file is moved or deleted.
///
/// Known gap: per-item &lt;style&gt; overrides aren't understood yet.
/// </summary>
public sealed class SetXmlStore
{
    private readonly string _folder;
    private readonly string _imageFolder;
    private readonly SongLibraryService _songLibrary;

    /// <summary>Tracks which on-disk file each in-memory set (by its session-local Id) came from, for rename/delete.</summary>
    private readonly Dictionary<string, string> _fileNameBySetId = new();

    public SetXmlStore(SongLibraryService songLibrary, string? folder = null, string? imageFolder = null)
    {
        _songLibrary = songLibrary;
        _folder = folder ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Sets");
        Directory.CreateDirectory(_folder);

        _imageFolder = imageFolder ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BiblePresenter", "SetImages");
        Directory.CreateDirectory(_imageFolder);
    }

    public string Folder => _folder;

    /// <summary>
    /// Copies a picture into the app's own image store and returns the stored path. Files are named by
    /// content hash, so adding the same flyer twice reuses one copy, and the item keeps working if the
    /// original is moved. Throws <see cref="NotSupportedException"/> if it isn't a png/jpg/gif/bmp.
    /// </summary>
    public string StoreImage(string sourcePath) => StoreImageBytes(File.ReadAllBytes(sourcePath));

    private string StoreImageBytes(byte[] bytes)
    {
        var extension = DetectImageExtension(bytes)
            ?? throw new NotSupportedException("That file isn't a supported image (png, jpg, gif or bmp).");

        var path = Path.Combine(_imageFolder, Convert.ToHexString(SHA256.HashData(bytes))[..20] + extension);
        if (!File.Exists(path))
            File.WriteAllBytes(path, bytes);
        return path;
    }

    private static string? DetectImageExtension(byte[] b)
    {
        if (b.Length > 3 && b[0] == 0xFF && b[1] == 0xD8)
            return ".jpg";
        if (b.Length > 4 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47)
            return ".png";
        if (b.Length > 3 && b[0] == 0x47 && b[1] == 0x49 && b[2] == 0x46)
            return ".gif";
        if (b.Length > 2 && b[0] == 0x42 && b[1] == 0x4D)
            return ".bmp";
        return null;
    }

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

        if (group.Attribute(SetOnlySongAttribute)?.Value.Equals("song", StringComparison.OrdinalIgnoreCase) == true)
            return ParseSetOnlySong(group, name);

        if (typeAttr.Equals("song", StringComparison.OrdinalIgnoreCase))
        {
            // Deliberately not resolved here: a set can reference many songs, and eagerly
            // resolving all of them against a large library would multiply the scan cost at
            // set-load time. Resolved lazily via SongLibraryService.EnsureResolved when the
            // item is actually selected or presented.
            return new SetItem { Type = SetItemType.Song, Title = name };
        }

        if (typeAttr.Equals("image", StringComparison.OrdinalIgnoreCase))
            return ParseImageGroup(group, name);

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

        if (isScripture)
            slides = ScriptureSlideSplitter.SplitIntoVerses(slides);

        var type = isScripture ? SetItemType.Scripture : SetItemType.Custom;
        return new SetItem { Type = type, Title = title, Subtitle = subtitle, Slides = slides };
    }

    private SetItem ParseImageGroup(XElement group, string name)
    {
        var paths = new List<string>();
        string? firstDescription = null;

        foreach (var slide in group.Element("slides")?.Elements("slide") ?? Enumerable.Empty<XElement>())
        {
            firstDescription ??= slide.Element("description")?.Value.Trim();
            var path = ResolveImage(slide);
            if (path is not null)
                paths.Add(path);
        }

        var title = group.Element("title")?.Value.Trim();
        if (string.IsNullOrWhiteSpace(title))
            title = name;

        // OpenSong names a fresh image item "Untitled" and keeps the picture's original path as its description.
        if (string.IsNullOrWhiteSpace(title) || title.Equals("Untitled", StringComparison.OrdinalIgnoreCase))
            title = string.IsNullOrWhiteSpace(firstDescription) ? "Image" : Path.GetFileNameWithoutExtension(firstDescription);

        return new SetItem { Type = SetItemType.Image, Title = title, Slides = paths };
    }

    /// <summary>An image slide holds either the picture itself (base64) or, less often, a filename to read.</summary>
    private string? ResolveImage(XElement slide)
    {
        var encoded = slide.Element("image")?.Value;
        if (!string.IsNullOrWhiteSpace(encoded))
        {
            try
            {
                return StoreImageBytes(Convert.FromBase64String(encoded));
            }
            catch (Exception ex) when (ex is FormatException or NotSupportedException)
            {
                // Not a usable embedded picture; fall through to the filename, if any.
            }
        }

        var filename = slide.Element("filename")?.Value.Trim();
        if (string.IsNullOrEmpty(filename))
            return null;

        var candidate = Path.IsPathRooted(filename) ? filename : Path.Combine(_folder, filename);
        return File.Exists(candidate) ? candidate : null;
    }

    /// <summary>A song edited "for this set only": its own words live in the set file, tagged so it isn't mistaken for a library reference.</summary>
    private static SetItem ParseSetOnlySong(XElement group, string name)
    {
        var title = group.Element("title")?.Value.Trim();
        if (string.IsNullOrWhiteSpace(title))
            title = name;

        var sections = group.Element("slides")?.Elements("slide").ToList() ?? new List<XElement>();
        return new SetItem
        {
            Type = SetItemType.Song,
            IsSetOnly = true,
            Title = title,
            Subtitle = group.Element("subtitle")?.Value.Trim() ?? "",
            Slides = sections.Select(s => s.Element("body")?.Value.Trim() ?? "").ToList(),
            SlideLabels = sections.Select(s => s.Element("label")?.Value.Trim() ?? "").ToList()
        };
    }

    private static XElement BuildSetOnlySong(SetItem item)
        => new("slide_group",
            new XAttribute("name", item.Title),
            new XAttribute("type", "custom"),
            new XAttribute(SetOnlySongAttribute, "song"),
            new XElement("title", item.Title),
            new XElement("subtitle", item.Subtitle),
            new XElement("notes"),
            new XElement("slides",
                item.Slides.Select((slide, i) => new XElement("slide",
                    new XElement("body", slide),
                    new XElement("label", item.LabelAt(i))))));

    public void Save(PresentationSet set)
    {
        var fileName = SanitizeFileName(set.Name);

        if (_fileNameBySetId.TryGetValue(set.Id, out var previousFileName) && previousFileName != fileName)
        {
            var previousPath = Path.Combine(_folder, previousFileName);
            if (File.Exists(previousPath))
                File.Delete(previousPath);
        }

        // Start from the file as it is on disk, so anything this app doesn't model (OpenSong's extra
        // fields, the original text of verses and songs that weren't edited) is written back untouched.
        var path = Path.Combine(_folder, fileName);
        var existing = File.Exists(path) ? XDocument.Load(path) : null;
        var before = existing?.Root is { } oldRoot ? new XElement(oldRoot) : null;
        var existingGroups = existing?.Root?.Element("slide_groups")?.Elements("slide_group").ToList() ?? new List<XElement>();

        var groups = set.Items.Select((item, i) =>
            i < existingGroups.Count && GroupUnchanged(existingGroups[i], item)
                ? new XElement(existingGroups[i])
                : BuildSlideGroup(item)).ToList();

        XDocument document;
        if (existing?.Root is { } root)
        {
            root.SetAttributeValue("name", set.Name);
            var slideGroups = root.Element("slide_groups");
            if (slideGroups is null)
                root.Add(new XElement("slide_groups", groups));
            else
                slideGroups.ReplaceNodes(groups);
            document = existing;
        }
        else
        {
            document = new XDocument(
                new XElement("set",
                    new XAttribute("name", set.Name),
                    new XElement("slide_groups", groups)));
        }

        _fileNameBySetId[set.Id] = fileName;
        if (before is not null && SongImportService.SameXml(before, document.Root!))
            return;

        document.Save(path);
    }

    /// <summary>True when the item still matches what this slide group holds on disk, so the group can be kept as it is.</summary>
    private bool GroupUnchanged(XElement group, SetItem item)
    {
        var parsed = ParseSlideGroup(group);
        if (parsed.Type != item.Type || parsed.IsSetOnly != item.IsSetOnly || parsed.Title != item.Title)
            return false;

        // A library reference stores only its name; its words are looked up, not saved.
        if (item.Type == SetItemType.Song && !item.IsSetOnly)
            return true;

        return parsed.Subtitle == item.Subtitle
            && parsed.Slides.SequenceEqual(item.Slides)
            && Enumerable.Range(0, item.Slides.Count).All(i => parsed.LabelAt(i) == item.LabelAt(i));
    }

    private const string SetOnlySongAttribute = "source";

    private static XElement BuildSlideGroup(SetItem item)
    {
        if (item.Type == SetItemType.Song && item.IsSetOnly)
            return BuildSetOnlySong(item);

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

        if (item.Type == SetItemType.Image)
        {
            return new XElement("slide_group",
                new XAttribute("name", item.Title),
                new XAttribute("type", "image"),
                new XAttribute("print", "true"),
                new XAttribute("descriptions_in_subtitle", "false"),
                new XAttribute("seconds", "0"),
                new XAttribute("loop", "false"),
                new XAttribute("transition", "0"),
                new XAttribute("resize", "screen"),
                new XAttribute("keep_aspect", "true"),
                new XAttribute("link", "false"),
                new XElement("title", item.Title),
                new XElement("subtitle", item.Subtitle),
                new XElement("notes"),
                new XElement("slides",
                    item.Slides.Where(File.Exists).Select(path => new XElement("slide",
                        new XElement("image", Convert.ToBase64String(File.ReadAllBytes(path))),
                        new XElement("description", item.Title),
                        new XElement("filename")))));
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
