using System.Text;
using System.Xml.Linq;

namespace BiblePresenter.App.Services.Parsers;

/// <summary>
/// Parses OSIS XML Bibles (&lt;osis&gt;&lt;osisText&gt;&lt;div type="book"&gt;&lt;chapter&gt;&lt;verse&gt;...).
/// Supports the two common shapes: container-style verses (&lt;verse osisID="Gen.1.1"&gt;text&lt;/verse&gt;)
/// and milestone-style verses (self-closing &lt;verse sID=".."/&gt;text&lt;verse eID=".."/&gt;) as direct
/// children of a container &lt;chapter&gt; element. Footnotes (&lt;note&gt;) are excluded from verse text.
/// </summary>
public sealed class OsisXmlParser : IBibleXmlParser
{
    public bool CanParse(XDocument document)
        => string.Equals(document.Root?.Name.LocalName, "osis", StringComparison.OrdinalIgnoreCase);

    public string? ExtractTranslationName(XDocument document)
    {
        var titleEl = document.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals("title", StringComparison.OrdinalIgnoreCase));
        return titleEl?.Value.Trim();
    }

    public IReadOnlyList<ParsedVerse> Parse(XDocument document)
    {
        var result = new List<ParsedVerse>();
        var bookDivs = document.Descendants()
            .Where(e => e.Name.LocalName.Equals("div", StringComparison.OrdinalIgnoreCase)
                        && string.Equals(e.Attribute("type")?.Value, "book", StringComparison.OrdinalIgnoreCase));

        foreach (var bookDiv in bookDivs)
        {
            var bookToken = ExtractBookToken(bookDiv.Attribute("osisID")?.Value) ?? bookDiv.Attribute("osisID")?.Value ?? "";
            if (string.IsNullOrWhiteSpace(bookToken))
                continue;

            foreach (var chapterEl in bookDiv.Elements().Where(e => e.Name.LocalName.Equals("chapter", StringComparison.OrdinalIgnoreCase)))
            {
                var chapterNum = ExtractTrailingNumber(chapterEl.Attribute("osisID")?.Value ?? chapterEl.Attribute("n")?.Value);
                if (chapterNum is null)
                    continue;

                ParseChapterContent(chapterEl, bookToken, chapterNum.Value, result);
            }
        }

        return result;
    }

    private static void ParseChapterContent(XElement chapterEl, string bookToken, int chapterNum, List<ParsedVerse> output)
    {
        StringBuilder? buffer = null;
        int? openVerseNum = null;

        void Flush()
        {
            if (buffer is not null && openVerseNum is not null)
            {
                var text = CollapseWhitespace(buffer.ToString()).Trim();
                if (text.Length > 0)
                {
                    output.Add(new ParsedVerse
                    {
                        RawBookToken = bookToken,
                        Chapter = chapterNum,
                        Number = openVerseNum.Value,
                        Text = text
                    });
                }
            }
            buffer = null;
            openVerseNum = null;
        }

        foreach (var node in chapterEl.Nodes())
        {
            if (node is XText text)
            {
                buffer?.Append(text.Value);
                continue;
            }

            if (node is not XElement el)
                continue;

            if (el.Name.LocalName.Equals("note", StringComparison.OrdinalIgnoreCase))
                continue;

            if (el.Name.LocalName.Equals("verse", StringComparison.OrdinalIgnoreCase))
            {
                var sID = el.Attribute("sID")?.Value;
                var eID = el.Attribute("eID")?.Value;
                var osisId = el.Attribute("osisID")?.Value;

                if (eID is not null && sID is null)
                {
                    Flush();
                    continue;
                }

                if (sID is not null || (osisId is not null && !el.Nodes().Any()))
                {
                    Flush();
                    openVerseNum = ExtractTrailingNumber(osisId ?? sID);
                    buffer = new StringBuilder();
                    continue;
                }

                // Container-style: verse element wraps its own text directly.
                var verseNum = ExtractTrailingNumber(osisId);
                if (verseNum is not null)
                {
                    var innerText = CollapseWhitespace(StripNotes(el)).Trim();
                    if (innerText.Length > 0)
                    {
                        output.Add(new ParsedVerse
                        {
                            RawBookToken = bookToken,
                            Chapter = chapterNum,
                            Number = verseNum.Value,
                            Text = innerText
                        });
                    }
                }
                continue;
            }

            // Any other inline markup (w, seg, transChange, l, lg, title, ...): its text still belongs
            // to the currently open milestone verse, if any.
            if (buffer is not null)
                buffer.Append(el.Value);
        }

        Flush();
    }

    private static string StripNotes(XElement verseEl)
    {
        var clone = new XElement(verseEl);
        clone.Descendants().Where(d => d.Name.LocalName.Equals("note", StringComparison.OrdinalIgnoreCase))
            .ToList()
            .ForEach(n => n.Remove());
        return clone.Value;
    }

    private static string? ExtractBookToken(string? osisId)
        => string.IsNullOrEmpty(osisId) ? null : osisId.Split('.')[0];

    private static int? ExtractTrailingNumber(string? osisId)
    {
        if (string.IsNullOrEmpty(osisId))
            return null;
        var parts = osisId.Split('.');
        return int.TryParse(parts[^1], out var n) ? n : null;
    }

    private static string CollapseWhitespace(string s)
        => string.Join(' ', s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
