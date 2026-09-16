using System.Text.RegularExpressions;
using BiblePresenter.App.Models;

namespace BiblePresenter.App.Services;

public sealed record ParsedReference(Book Book, int Chapter, int? VerseStart, int? VerseEnd);

/// <summary>
/// Parses ProPresenter-style reference queries like "jn 3:16", "1 cor 13", "Genesis 1:1-4"
/// into a direct (book, chapter, verse) lookup, so common navigation never needs the keyword index.
/// </summary>
public sealed class ReferenceParser
{
    private static readonly Regex Pattern = new(
        @"^\s*(?<book>[1-3]?\s*[A-Za-z][A-Za-z. ]*?)\s+(?<chapter>\d+)(\s*[:.]\s*(?<v1>\d+)(\s*-\s*(?<v2>\d+))?)?\s*$",
        RegexOptions.Compiled);

    private readonly BookTable _bookTable;

    public ReferenceParser(BookTable? bookTable = null)
    {
        _bookTable = bookTable ?? BookTable.Instance;
    }

    public ParsedReference? TryParse(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return null;

        var match = Pattern.Match(query);
        if (!match.Success)
            return null;

        var book = _bookTable.Resolve(match.Groups["book"].Value.Trim());
        if (book is null)
            return null;

        var chapter = int.Parse(match.Groups["chapter"].Value);
        int? v1 = match.Groups["v1"].Success ? int.Parse(match.Groups["v1"].Value) : null;
        int? v2 = match.Groups["v2"].Success ? int.Parse(match.Groups["v2"].Value) : null;

        return new ParsedReference(book, chapter, v1, v2);
    }
}
