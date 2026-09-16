using System.Xml.Linq;
using BiblePresenter.App.Services.Parsers;
using Xunit;

namespace BiblePresenter.Tests;

public class GenericXmlParserTests
{
    // A made-up flat shape: no dedicated book/chapter wrapper elements, everything as
    // attributes directly on the verse element - the kind of shape Zefania/OSIS won't match.
    private const string FlatSample = """
        <bible>
          <verse book="Genesis" chapter="1" number="1">In the beginning God created the heaven and the earth.</verse>
          <verse book="Genesis" chapter="1" number="2">And the earth was without form, and void.</verse>
        </bible>
        """;

    // A nested shape using generic wrapper element names.
    private const string NestedSample = """
        <bible>
          <book name="John">
            <chapter number="3">
              <verse number="16">For God so loved the world.</verse>
            </chapter>
          </book>
        </bible>
        """;

    // The real shape of the user's KJV.xml: single-letter tags, "n" used as the attribute name
    // at every level (book name, chapter number, and verse number all use "n").
    private const string SingleLetterTagSample = """
        <bible>
          <b n="Genesis">
            <c n="1">
              <v n="1">In the beginning God created the heaven and the earth.</v>
              <v n="2">And the earth was without form, and void.</v>
            </c>
          </b>
        </bible>
        """;

    [Fact]
    public void Parse_SingleLetterTagShape_ExtractsVersesWithoutBookNameCollision()
    {
        var verses = new GenericXmlParser().Parse(XDocument.Parse(SingleLetterTagSample));

        Assert.Equal(2, verses.Count);
        Assert.All(verses, v => Assert.Equal("Genesis", v.RawBookToken));
        Assert.Equal(1, verses[0].Chapter);
        Assert.Equal(1, verses[0].Number);
        Assert.Equal(2, verses[1].Number);
    }

    [Fact]
    public void CanParse_DetectsVerseLikeElements()
    {
        Assert.True(new GenericXmlParser().CanParse(XDocument.Parse(FlatSample)));
    }

    [Fact]
    public void Parse_FlatAttributeShape_ExtractsVerses()
    {
        var verses = new GenericXmlParser().Parse(XDocument.Parse(FlatSample));

        Assert.Equal(2, verses.Count);
        Assert.Equal("Genesis", verses[0].RawBookToken);
        Assert.Equal(1, verses[0].Chapter);
        Assert.Equal(1, verses[0].Number);

        // Regression check: verse 2's own "number" attribute (=2) must not be misread as the
        // chapter (it should stay chapter 1, from the "chapter" attribute).
        Assert.Equal(1, verses[1].Chapter);
        Assert.Equal(2, verses[1].Number);
    }

    [Fact]
    public void Parse_NestedElementShape_ExtractsVerses()
    {
        var verses = new GenericXmlParser().Parse(XDocument.Parse(NestedSample));

        var verse = Assert.Single(verses);
        Assert.Equal("John", verse.RawBookToken);
        Assert.Equal(3, verse.Chapter);
        Assert.Equal(16, verse.Number);
    }
}
