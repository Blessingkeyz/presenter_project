using System.Globalization;
using System.Xml.Linq;

namespace BiblePresenter.App.Services.Parsers;

/// <summary>
/// Best-effort fallback for Bible XML shapes that are neither Zefania nor OSIS.
/// Looks for elements that look like verses (by tag name) and reads book/chapter/verse
/// either from attributes on the verse element itself, or from the nearest ancestor
/// elements that look like chapter/book containers.
/// </summary>
public sealed class GenericXmlParser : IBibleXmlParser
{
    private static readonly string[] VerseTagNames = { "verse", "vers", "v" };
    private static readonly string[] VerseNumberAttrs = { "number", "num", "n", "vnumber", "verse", "id" };
    // Only unambiguous names are checked directly on the verse element itself - the generic
    // "number"/"n"/"id" fallbacks are reserved for a distinct chapter-shaped ancestor element,
    // since on the verse element those same names already mean the verse number.
    private static readonly string[] ChapterSelfAttrs = { "chapter", "cnumber" };
    private static readonly string[] ChapterAncestorAttrs = { "number", "num", "n", "cnumber", "id" };
    // Same collision rule as chapter: "n"/"id" are only safe to read from a book-shaped
    // ancestor, never from the verse element itself (where they mean the verse number).
    private static readonly string[] BookSelfAttrs = { "book", "bookname", "bname", "name" };
    private static readonly string[] BookAncestorAttrs = { "name", "bname", "book", "bookname", "n", "id" };

    public bool CanParse(XDocument document)
        => document.Descendants().Any(e => VerseTagNames.Contains(e.Name.LocalName.ToLowerInvariant()));

    public string? ExtractTranslationName(XDocument document) => null;

    public IReadOnlyList<ParsedVerse> Parse(XDocument document)
    {
        var result = new List<ParsedVerse>();

        foreach (var verseEl in document.Descendants().Where(e => VerseTagNames.Contains(e.Name.LocalName.ToLowerInvariant())))
        {
            var verseNum = ReadInt(verseEl, VerseNumberAttrs);
            var (chapterNum, bookToken) = ResolveChapterAndBook(verseEl);

            if (verseNum is null || chapterNum is null || string.IsNullOrWhiteSpace(bookToken))
                continue;

            var text = StripNotes(verseEl).Trim();
            if (text.Length == 0)
                continue;

            result.Add(new ParsedVerse
            {
                RawBookToken = bookToken!,
                Chapter = chapterNum.Value,
                Number = verseNum.Value,
                Text = text
            });
        }

        return result;
    }

    private static (int? chapter, string? book) ResolveChapterAndBook(XElement verseEl)
    {
        var chapter = ReadInt(verseEl, ChapterSelfAttrs);
        var book = ReadFirstAttr(verseEl, BookSelfAttrs);

        var ancestor = verseEl.Parent;
        var depth = 0;
        while (ancestor is not null && depth < 6 && (chapter is null || book is null))
        {
            var local = ancestor.Name.LocalName.ToLowerInvariant();
            if (chapter is null && (local.Contains("chap") || local == "c"))
                chapter ??= ReadInt(ancestor, ChapterAncestorAttrs);
            if (book is null && (local.Contains("book") || local == "b"))
                book ??= ReadFirstAttr(ancestor, BookAncestorAttrs);

            ancestor = ancestor.Parent;
            depth++;
        }

        return (chapter, book);
    }

    private static int? ReadInt(XElement el, string[] attrNames)
    {
        foreach (var name in attrNames)
        {
            var raw = el.Attribute(name)?.Value;
            if (raw is not null && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                return value;
        }
        return null;
    }

    private static string? ReadFirstAttr(XElement el, string[] attrNames)
    {
        foreach (var name in attrNames)
        {
            var raw = el.Attribute(name)?.Value;
            if (!string.IsNullOrWhiteSpace(raw))
                return raw;
        }
        return null;
    }

    private static string StripNotes(XElement verseEl)
    {
        var clone = new XElement(verseEl);
        clone.Descendants().Where(d => d.Name.LocalName.Equals("note", StringComparison.OrdinalIgnoreCase))
            .ToList()
            .ForEach(n => n.Remove());
        return string.Join(' ', clone.Value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
