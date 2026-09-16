using System.Xml.Linq;

namespace BiblePresenter.App.Services.Parsers;

public static class BibleFormatDetector
{
    private static readonly IBibleXmlParser[] Parsers =
    {
        new ZefaniaXmlParser(),
        new OsisXmlParser(),
        new GenericXmlParser() // fallback, tried last
    };

    public static IBibleXmlParser? Detect(XDocument document)
        => Parsers.FirstOrDefault(p => p.CanParse(document));
}
