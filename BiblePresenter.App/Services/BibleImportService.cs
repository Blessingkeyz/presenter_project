using System.IO;
using System.Xml.Linq;
using BiblePresenter.App.Models;
using BiblePresenter.App.Services.Parsers;

namespace BiblePresenter.App.Services;

/// <summary>Imports a Bible XML file (auto-detecting Zefania/OSIS/generic shape) into a Translation.</summary>
public sealed class BibleImportService
{
    private readonly BookTable _bookTable;

    public BibleImportService(BookTable? bookTable = null)
    {
        _bookTable = bookTable ?? BookTable.Instance;
    }

    public Translation Import(string filePath)
    {
        XDocument document;
        try
        {
            document = XDocument.Load(filePath, LoadOptions.None);
        }
        catch (Exception ex)
        {
            throw new BibleImportException($"'{Path.GetFileName(filePath)}' is not valid XML: {ex.Message}", ex);
        }

        var parser = BibleFormatDetector.Detect(document);
        if (parser is null)
            throw new BibleImportException($"'{Path.GetFileName(filePath)}' doesn't look like a Bible XML file I recognize.");

        var parsedVerses = parser.Parse(document);
        if (parsedVerses.Count == 0)
            throw new BibleImportException($"No verses could be read from '{Path.GetFileName(filePath)}'.");

        var translationId = Guid.NewGuid().ToString("N");
        var verses = new List<Verse>(parsedVerses.Count);
        var unresolvedBooks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pv in parsedVerses)
        {
            var book = _bookTable.Resolve(pv.RawBookToken);
            if (book is null)
            {
                unresolvedBooks.Add(pv.RawBookToken);
                continue;
            }

            verses.Add(new Verse
            {
                TranslationId = translationId,
                BookIndex = book.Index,
                BookName = book.Name,
                Chapter = pv.Chapter,
                Number = pv.Number,
                Text = pv.Text
            });
        }

        if (verses.Count == 0)
        {
            throw new BibleImportException(
                $"None of the book names in '{Path.GetFileName(filePath)}' could be recognized (e.g. {string.Join(", ", unresolvedBooks.Take(3))}).");
        }

        verses.Sort((a, b) =>
        {
            var cmp = a.BookIndex.CompareTo(b.BookIndex);
            if (cmp != 0) return cmp;
            cmp = a.Chapter.CompareTo(b.Chapter);
            return cmp != 0 ? cmp : a.Number.CompareTo(b.Number);
        });

        var name = parser.ExtractTranslationName(document) ?? Path.GetFileNameWithoutExtension(filePath);
        var abbreviation = BuildAbbreviation(name);

        return new Translation
        {
            Id = translationId,
            Name = name,
            Abbreviation = abbreviation,
            Verses = verses
        };
    }

    private static string BuildAbbreviation(string name)
    {
        var initials = new string(name.Where(char.IsUpper).ToArray());
        if (initials.Length is >= 2 and <= 6)
            return initials;
        return name.Length <= 8 ? name : name[..8];
    }
}
