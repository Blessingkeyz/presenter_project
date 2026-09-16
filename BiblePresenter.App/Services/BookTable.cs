using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
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

    public IReadOnlyList<Book> Books { get; }
    private readonly Dictionary<string, Book> _byAlias;

    private static BookTable? _instance;
    public static BookTable Instance => _instance ??= LoadEmbedded();

    private BookTable(IReadOnlyList<Book> books)
    {
        Books = books;
        _byAlias = new Dictionary<string, Book>(StringComparer.OrdinalIgnoreCase);
        foreach (var book in books)
        {
            foreach (var alias in book.Aliases)
                _byAlias[Normalize(alias)] = book;
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

    public Book? ByIndex(int index) => Books.FirstOrDefault(b => b.Index == index);

    private static string Normalize(string s) => s.Trim().ToLowerInvariant();
}
