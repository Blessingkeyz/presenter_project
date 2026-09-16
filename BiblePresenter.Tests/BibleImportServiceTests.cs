using BiblePresenter.App.Services;
using Xunit;

namespace BiblePresenter.Tests;

public class BibleImportServiceTests
{
    private const string ZefaniaSample = """
        <XMLBIBLE biblename="Test Version">
          <BIBLEBOOK bnumber="43" bname="John" bsname="Jn">
            <CHAPTER cnumber="3">
              <VERS vnumber="17">For God sent not his Son into the world to condemn the world.</VERS>
              <VERS vnumber="16">For God so loved the world.</VERS>
            </CHAPTER>
          </BIBLEBOOK>
          <BIBLEBOOK bnumber="1" bname="Genesis" bsname="Gen">
            <CHAPTER cnumber="1">
              <VERS vnumber="1">In the beginning God created the heaven and the earth.</VERS>
            </CHAPTER>
          </BIBLEBOOK>
        </XMLBIBLE>
        """;

    [Fact]
    public void Import_ResolvesBookNamesAndSortsInCanonicalOrder()
    {
        var path = Path.Combine(Path.GetTempPath(), $"bible-{Guid.NewGuid():N}.xml");
        File.WriteAllText(path, ZefaniaSample);
        try
        {
            var translation = new BibleImportService().Import(path);

            Assert.Equal("Test Version", translation.Name);
            Assert.Equal(3, translation.Verses.Count);

            // Genesis (book 1) must sort before John (book 43) even though John appeared first in the XML.
            Assert.Equal("Genesis", translation.Verses[0].BookName);
            Assert.Equal(1, translation.Verses[0].BookIndex);

            // Within John 3, verse 16 must sort before verse 17 even though 17 appeared first in the XML.
            Assert.Equal(16, translation.Verses[1].Number);
            Assert.Equal(17, translation.Verses[2].Number);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Import_ThrowsWhenFileIsNotRecognizedBibleXml()
    {
        var path = Path.Combine(Path.GetTempPath(), $"notabible-{Guid.NewGuid():N}.xml");
        File.WriteAllText(path, "<root><item>hello</item></root>");
        try
        {
            Assert.Throws<BibleImportException>(() => new BibleImportService().Import(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
