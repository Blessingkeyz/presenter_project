using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using BiblePresenter.App.Models;

namespace BiblePresenter.App.Services;

/// <summary>
/// Loads the canonical 66-book name/alias table and resolves arbitrary book
/// names or abbreviations (from imported XML or user search input) to it, so
/// every translation ends up using the same book identity and ordering.
/// </summary>
public sealed class BookTable
{
    private sealed class BookDto
    {
        [JsonPropertyName("index")] public int Index { get; set; }
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("aliases")] public List<string> Aliases { get; set; } = new();
    }

    private static readonly Regex NumberedAlias = new(@"^[1-3]\s*(.+)$", RegexOptions.Compiled);

    public IReadOnlyList<Book> Books { get; }
    private readonly Dictionary<string, Book> _byAlias;

    /// <summary>Bare stem (e.g. "thess") -> every numbered book sharing it (1/2 Thessalonians), so an
    /// ambiguous query without a leading number can surface all plausible candidates.</summary>
    private readonly Dictionary<string, List<Book>> _byStem;

    private static BookTable? _instance;
    public static BookTable Instance => _instance ??= LoadEmbedded();

    private BookTable(IReadOnlyList<Book> books)
    {
        Books = books;
        _byAlias = new Dictionary<string, Book>(StringComparer.OrdinalIgnoreCase);
        _byStem = new Dictionary<string, List<Book>>(StringComparer.OrdinalIgnoreCase);

        foreach (var book in books)
        {
            foreach (var alias in book.Aliases)
            {
                _byAlias[Normalize(alias)] = book;

                var stemMatch = NumberedAlias.Match(alias.Trim());
                if (!stemMatch.Success)
                    continue;

                var stem = Normalize(stemMatch.Groups[1].Value);
                if (stem.Length == 0)
                    continue;

                if (!_byStem.TryGetValue(stem, out var candidates))
                {
                    candidates = new List<Book>();
                    _byStem[stem] = candidates;
                }
                if (!candidates.Contains(book))
                    candidates.Add(book);
            }
            _byAlias[Normalize(book.Name)] = book;
        }
    }

    private static BookTable LoadEmbedded()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .First(n => n.EndsWith("BookAliases.json", StringComparison.OrdinalIgnoreCase));
        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        var dtos = JsonSerializer.Deserialize<List<BookDto>>(stream)!;
        var books = dtos
            .Select(d => new Book { Index = d.Index, Name = d.Name, Aliases = d.Aliases })
            .OrderBy(b => b.Index)
            .ToList();
        return new BookTable(books);
    }

    /// <summary>Resolves a raw book token (e.g. "1cor", "Song of Solomon", "jn") to a canonical Book, or null.</summary>
    public Book? Resolve(string rawToken)
    {
        var key = Normalize(rawToken);
        if (_byAlias.TryGetValue(key, out var book))
            return book;

        // Try without spaces (e.g. "1 cor" vs "1cor" both already covered, but be lenient on odd spacing).
        var collapsed = key.Replace(" ", "");
        return _byAlias.TryGetValue(collapsed, out book) ? book : null;
    }

    /// <summary>
    /// Resolves a book token to every plausible candidate: a single exact match if the token names
    /// one book unambiguously, or every numbered book sharing a bare stem (e.g. "thess" -> 1 & 2
    /// Thessalonians) when the token itself doesn't say which one. Empty if nothing plausible matches.
    /// </summary>
    public IReadOnlyList<Book> ResolveAll(string rawToken)
    {
        var exact = Resolve(rawToken);
        if (exact is not null)
            return new[] { exact };

        var key = Normalize(rawToken);
        return _byStem.TryGetValue(key, out var candidates) && candidates.Count > 1
            ? candidates
            : Array.Empty<Book>();
    }

    public Book? ByIndex(int index) => Books.FirstOrDefault(b => b.Index == index);

    private static string Normalize(string s) => s.Trim().ToLowerInvariant();
}
