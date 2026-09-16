using System.Xml.Linq;
using BiblePresenter.App.Services.Parsers;
using Xunit;

namespace BiblePresenter.Tests;

public class OsisXmlParserTests
{
    private const string ContainerStyle = """
        <osis>
          <osisText>
            <div type="book" osisID="Gen">
              <chapter osisID="Gen.1">
                <verse osisID="Gen.1.1">In the beginning God created the heaven and the earth.</verse>
                <verse osisID="Gen.1.2">And the earth was without form, and void.</verse>
              </chapter>
            </div>
          </osisText>
        </osis>
        """;

    private const string MilestoneStyle = """
        <osis>
          <osisText>
            <div type="book" osisID="John">
              <chapter osisID="John.3">
                <verse sID="John.3.16" osisID="John.3.16"/>For God so loved the world<note>a footnote</note>, that he gave his only begotten Son.<verse eID="John.3.16"/>
              </chapter>
            </div>
          </osisText>
        </osis>
        """;

    [Fact]
    public void CanParse_RecognizesOsisRoot()
    {
        Assert.True(new OsisXmlParser().CanParse(XDocument.Parse(ContainerStyle)));
    }

    [Fact]
    public void Parse_ContainerStyle_ExtractsVerses()
    {
        var verses = new OsisXmlParser().Parse(XDocument.Parse(ContainerStyle));

        Assert.Equal(2, verses.Count);
        Assert.Equal("Gen", verses[0].RawBookToken);
        Assert.Equal(1, verses[0].Chapter);
        Assert.Equal(1, verses[0].Number);
        Assert.Equal("In the beginning God created the heaven and the earth.", verses[0].Text);
    }

    [Fact]
    public void Parse_MilestoneStyle_ExtractsVerseAndStripsNotes()
    {
        var verses = new OsisXmlParser().Parse(XDocument.Parse(MilestoneStyle));

        var verse = Assert.Single(verses);
        Assert.Equal("John", verse.RawBookToken);
        Assert.Equal(3, verse.Chapter);
        Assert.Equal(16, verse.Number);
        Assert.DoesNotContain("footnote", verse.Text);
        Assert.Contains("For God so loved the world", verse.Text);
        Assert.Contains("that he gave his only begotten Son.", verse.Text);
    }
}
