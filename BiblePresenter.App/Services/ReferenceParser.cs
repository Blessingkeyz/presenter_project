using System.Text.RegularExpressions;
using BiblePresenter.App.Models;

namespace BiblePresenter.App.Services;

public sealed record ParsedReference(Book Book, int Chapter, int? VerseStart, int? VerseEnd);

/// <summary>
/// Parses ProPresenter-style reference queries like "jn 3:16", "1 cor 13", "Genesis 1:1-4", or
/// "gen 2 2-10" (a plain space works as well as ":"/"." between chapter and verse - no need to
/// type the colon) into a direct (book, chapter, verse) lookup, so common navigation never needs
/// the keyword index. A verse range's end can be written with a dash or just another space - "gen
/// 1 3-4" and "gen 1 3 4" mean the same thing, since typing the dash is one more thing to get
/// right. When the book token is an ambiguous bare stem shared by numbered books (e.g. "thess" for
/// both 1 and 2 Thessalonians), <see cref="ParseAll"/> returns every candidate.
/// </summary>
public sealed class ReferenceParser
{
    private static readonly Regex Pattern = new(
        @"^\s*(?<book>[1-3]?\s*[A-Za-z][A-Za-z. ]*?)\s+(?<chapter>\d+)(?:[\s:.]+(?<v1>\d+)(?:(?:\s*-\s*|\s+)(?<v2>\d+))?)?\s*$",
        RegexOptions.Compiled);

    private readonly BookTable _bookTable;

    public ReferenceParser(BookTable? bookTable = null)
    {
        _bookTable = bookTable ?? BookTable.Instance;
    }

    /// <summary>Convenience for callers that only want the first/only candidate.</summary>
    public ParsedReference? TryParse(string query) => ParseAll(query).FirstOrDefault();

    public IReadOnlyList<ParsedReference> ParseAll(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<ParsedReference>();

        var match = Pattern.Match(query);
        if (!match.Success)
            return Array.Empty<ParsedReference>();

        var books = _bookTable.ResolveAll(match.Groups["book"].Value.Trim());
        if (books.Count == 0)
            return Array.Empty<ParsedReference>();

        var chapter = int.Parse(match.Groups["chapter"].Value);
        int? v1 = match.Groups["v1"].Success ? int.Parse(match.Groups["v1"].Value) : null;
        int? v2 = match.Groups["v2"].Success ? int.Parse(match.Groups["v2"].Value) : null;

        return books.Select(b => new ParsedReference(b, chapter, v1, v2)).ToList();
    }
}
