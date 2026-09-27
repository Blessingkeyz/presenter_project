using BiblePresenter.App.Models;
using Xunit;

namespace BiblePresenter.Tests;

public class SetItemSlideSplitTests
{
    [Fact]
    public void SplitSlides_SeparatesOnDashLine()
    {
        var text = "Slide one line one\nSlide one line two\n---\nSlide two";
        var slides = SetItem.SplitSlides(text);

        Assert.Equal(2, slides.Count);
        Assert.Equal("Slide one line one\nSlide one line two", slides[0]);
        Assert.Equal("Slide two", slides[1]);
    }

    [Fact]
    public void SplitSlides_TrimsWhitespaceAroundSeparator()
    {
        var text = "  Slide one  \n\n---\n\n  Slide two  ";
        var slides = SetItem.SplitSlides(text);

        Assert.Equal(new List<string> { "Slide one", "Slide two" }, slides);
    }

    [Fact]
    public void SplitSlides_DropsEmptySlides()
    {
        var text = "Slide one\n---\n---\nSlide two";
        var slides = SetItem.SplitSlides(text);

        Assert.Equal(new List<string> { "Slide one", "Slide two" }, slides);
    }

    [Fact]
    public void SplitSlides_EmptyInput_ReturnsEmptyList()
    {
        Assert.Empty(SetItem.SplitSlides(""));
        Assert.Empty(SetItem.SplitSlides("   "));
    }

    [Fact]
    public void JoinSlides_ThenSplitSlides_RoundTrips()
    {
        var original = new List<string> { "First slide text", "Second slide\nwith two lines", "Third" };

        var joined = SetItem.JoinSlides(original);
        var roundTripped = SetItem.SplitSlides(joined);

        Assert.Equal(original, roundTripped);
    }
}
