using System.Xml.Linq;
using BiblePresenter.App.Services.Parsers;
using Xunit;

namespace BiblePresenter.Tests;

public class ZefaniaXmlParserTests
{
    private const string Sample = """
        <XMLBIBLE biblename="King James Version">
          <BIBLEBOOK bnumber="1" bname="Genesis" bsname="Gen">
            <CHAPTER cnumber="1">
              <VERS vnumber="1">In the beginning God created the heaven and the earth.</VERS>
              <VERS vnumber="2">And the earth was without form, and void.</VERS>
            </CHAPTER>
          </BIBLEBOOK>
          <BIBLEBOOK bnumber="43" bname="John" bsname="Jn">
            <CHAPTER cnumber="3">
              <VERS vnumber="16">For God so loved the world.</VERS>
            </CHAPTER>
          </BIBLEBOOK>
        </XMLBIBLE>
        """;

    [Fact]
    public void CanParse_RecognizesXmlBibleRoot()
    {
        var doc = XDocument.Parse(Sample);
        Assert.True(new ZefaniaXmlParser().CanParse(doc));
    }

    [Fact]
    public void Parse_ExtractsAllVersesWithCorrectBookChapterVerse()
    {
        var doc = XDocument.Parse(Sample);
        var verses = new ZefaniaXmlParser().Parse(doc);

        Assert.Equal(3, verses.Count);
        Assert.Equal("Genesis", verses[0].RawBookToken);
        Assert.Equal(1, verses[0].Chapter);
        Assert.Equal(1, verses[0].Number);
        Assert.Equal("In the beginning God created the heaven and the earth.", verses[0].Text);

        var john316 = verses.Single(v => v.RawBookToken == "John");
        Assert.Equal(3, john316.Chapter);
        Assert.Equal(16, john316.Number);
    }

    [Fact]
    public void ExtractTranslationName_ReadsBiblenameAttribute()
    {
        var doc = XDocument.Parse(Sample);
        Assert.Equal("King James Version", new ZefaniaXmlParser().ExtractTranslationName(doc));
    }
}
