using BiblePresenter.App.Models;

namespace BiblePresenter.App.Services;

public static class ReferenceFormatting
{
    /// <summary>Formats an ordered run of verses from the same book/chapter as a single range label, e.g. "Psalms 126:1-6".</summary>
    public static string FormatRange(IReadOnlyList<Verse> passage)
    {
        var first = passage[0];
        var last = passage[^1];
        return first.Number == last.Number
            ? first.Reference
            : $"{first.BookName} {first.Chapter}:{first.Number}-{last.Number}";
    }

    public static string FormatVerseBody(Verse verse) => $"{verse.Number}  {verse.Text}";
}
