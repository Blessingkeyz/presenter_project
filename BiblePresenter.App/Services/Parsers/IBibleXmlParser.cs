using System.Xml.Linq;
using BiblePresenter.App.Models;

namespace BiblePresenter.App.Services.Parsers;

public sealed class ParsedVerse
{
    public required string RawBookToken { get; init; }
    public required int Chapter { get; init; }
    public required int Number { get; init; }
    public required string Text { get; init; }
}

public interface IBibleXmlParser
{
    /// <summary>Whether this parser recognizes the given root element as its format.</summary>
    bool CanParse(XDocument document);

    /// <summary>Parses raw verses (book identified by whatever token the source XML used).</summary>
    IReadOnlyList<ParsedVerse> Parse(XDocument document);

    /// <summary>Best-effort translation name found in the document, if any.</summary>
    string? ExtractTranslationName(XDocument document);
}
