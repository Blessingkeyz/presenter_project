using System.Globalization;
using System.Xml.Linq;

namespace BiblePresenter.App.Services.Parsers;

/// <summary>
/// Parses Zefania XML Bibles: &lt;XMLBIBLE&gt; / &lt;BIBLEBOOK bname="..."&gt; / &lt;CHAPTER cnumber="n"&gt; / &lt;VERS vnumber="n"&gt;.
/// This is the most common free/open Bible XML format.
/// </summary>
public sealed class ZefaniaXmlParser : IBibleXmlParser
{
    public bool CanParse(XDocument document)
        => string.Equals(document.Root?.Name.LocalName, "XMLBIBLE", StringComparison.OrdinalIgnoreCase);

    public string? ExtractTranslationName(XDocument document)
        => document.Root?.Attribute("biblename")?.Value
           ?? document.Root?.Attribute("translation")?.Value;

    public IReadOnlyList<ParsedVerse> Parse(XDocument document)
    {
        var result = new List<ParsedVerse>();
        var root = document.Root!;

        foreach (var bookEl in root.Elements().Where(e => e.Name.LocalName.Equals("BIBLEBOOK", StringComparison.OrdinalIgnoreCase)))
        {
            var bookToken = bookEl.Attribute("bname")?.Value
                             ?? bookEl.Attribute("bsname")?.Value
                             ?? bookEl.Attribute("bnumber")?.Value
                             ?? "";
            if (string.IsNullOrWhiteSpace(bookToken))
                continue;

            foreach (var chapterEl in bookEl.Elements().Where(e => e.Name.LocalName.Equals("CHAPTER", StringComparison.OrdinalIgnoreCase)))
            {
                if (!TryGetInt(chapterEl, "cnumber", out var chapterNum))
                    continue;

                foreach (var verseEl in chapterEl.Elements().Where(e => e.Name.LocalName.Equals("VERS", StringComparison.OrdinalIgnoreCase)))
                {
                    if (!TryGetInt(verseEl, "vnumber", out var verseNum))
                        continue;

                    var text = verseEl.Value.Trim();
                    if (text.Length == 0)
                        continue;

                    result.Add(new ParsedVerse
                    {
                        RawBookToken = bookToken,
                        Chapter = chapterNum,
                        Number = verseNum,
                        Text = text
                    });
                }
            }
        }

        return result;
    }

    private static bool TryGetInt(XElement element, string attributeName, out int value)
    {
        var raw = element.Attribute(attributeName)?.Value;
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }
}
