using System.Text.RegularExpressions;

namespace BiblePresenter.App.Services;

/// <summary>
/// Shapes scripture into projector-sized slides: one verse per slide, and a verse too long for one
/// screen broken into balanced parts at its natural pauses.
///
/// OpenSong packs several verses into one scripture slide with inline numbers ("11 text... 12
/// text..."), so those are pulled apart first. Verse numbers are followed in sequence (11, then 12,
/// then 13...) so a stray number inside a verse's own text is not mistaken for the next verse.
/// </summary>
public static class ScriptureSlideSplitter
{
    public const int DefaultMaxSlideChars = 160;
    public const int MinAllowedSlideChars = 40;
    public const int MaxAllowedSlideChars = 1000;

    private static int _maxSlideChars = DefaultMaxSlideChars;

    /// <summary>
    /// Characters a verse may run to before it continues on the next slide (Settings > Scripture). The
    /// default is roughly what fits at the default body size, about six lines. Kept within a sane range.
    /// </summary>
    public static int MaxSlideChars
    {
        get => _maxSlideChars;
        set => _maxSlideChars = Math.Clamp(value, MinAllowedSlideChars, MaxAllowedSlideChars);
    }

    private static readonly Regex LeadingNumber = new(@"^(\d{1,3})\s+", RegexOptions.Compiled);

    /// <summary>
    /// Re-applies the current length limit to slides already split at an earlier one: continuation
    /// parts (slides without a leading verse number) are folded back into their verse first, so the
    /// limit can go up as well as down.
    /// </summary>
    public static List<string> Resplit(IReadOnlyList<string> slides, int maxChars = 0)
    {
        var merged = new List<string>();
        foreach (var slide in slides)
        {
            if (merged.Count > 0 && !LeadingNumber.IsMatch(slide.TrimStart()))
                merged[^1] = merged[^1].TrimEnd() + " " + slide.Trim();
            else
                merged.Add(slide);
        }

        return SplitIntoVerses(merged, maxChars);
    }

    /// <summary>One verse per slide, then long verses split further. Slides already that shape are returned as they are.</summary>
    public static List<string> SplitIntoVerses(IReadOnlyList<string> slides, int maxChars = 0)
    {
        return slides
            .SelectMany(SplitPackedVerses)
            .SelectMany(verse => SplitLongText(verse, maxChars))
            .ToList();
    }

    /// <summary>Breaks one slide holding several numbered verses into one string per verse.</summary>
    private static List<string> SplitPackedVerses(string slide)
    {
        var text = slide.Trim();
        var lead = LeadingNumber.Match(text);
        if (!lead.Success)
            return new List<string> { slide };

        var verses = new List<string>();
        var number = int.Parse(lead.Groups[1].Value);
        var verseStart = 0;
        var searchFrom = lead.Length;

        while (true)
        {
            var marker = $" {number + 1} ";
            var index = text.IndexOf(marker, searchFrom, StringComparison.Ordinal);
            if (index < 0)
                break;

            verses.Add(text[verseStart..index].Trim());
            verseStart = index + 1;
            searchFrom = verseStart + marker.Length - 1;
            number++;
        }

        verses.Add(text[verseStart..].Trim());

        if (verses.Count == 1)
            return new List<string> { slide };

        return verses.Select(v => LeadingNumber.Replace(v, "$1  ")).ToList();
    }

    /// <summary>
    /// Splits text longer than the limit (<paramref name="maxChars"/>, or <see cref="MaxSlideChars"/> when
    /// omitted) into similar-sized parts, cutting where a sentence ends if it can, else at a
    /// semicolon/colon, else a comma, else any space.
    /// </summary>
    public static List<string> SplitLongText(string text, int maxChars = 0)
    {
        if (maxChars <= 0)
            maxChars = MaxSlideChars;

        var remaining = text.Trim();
        var parts = new List<string>();

        while (remaining.Length > maxChars)
        {
            var partsLeft = (int)Math.Ceiling(remaining.Length / (double)maxChars);
            var ideal = remaining.Length / (double)partsLeft;
            var cut = FindCut(remaining, ideal, maxChars);
            parts.Add(remaining[..cut].TrimEnd());
            remaining = remaining[cut..].TrimStart();
        }

        parts.Add(remaining);
        return parts;
    }

    private static int FindCut(string text, double ideal, int maxChars)
    {
        var low = Math.Max(1, (int)(ideal * 0.7));
        var high = Math.Min(Math.Min(maxChars, text.Length - 1), (int)(ideal * 1.3));

        var best = -1;
        var bestScore = double.MaxValue;
        for (var i = low; i <= high; i++)
        {
            // A line break is a natural place to cut a song; a space is the fallback for running text.
            if (text[i] is not (' ' or '\n'))
                continue;

            var score = Math.Abs(i - ideal) + PausePenalty(text, i);
            if (score < bestScore)
            {
                bestScore = score;
                best = i;
            }
        }

        return best > 0 ? best : Math.Max(1, Math.Min(maxChars, (int)ideal));
    }

    /// <summary>How unwelcome it is to cut at the space at <paramref name="spaceIndex"/>, in characters of distance from the ideal spot.</summary>
    private static double PausePenalty(string text, int spaceIndex)
    {
        if (text[spaceIndex] == '\n')
            return 0;

        var i = spaceIndex - 1;
        while (i > 0 && text[i] is '"' or '\'' or '”' or '’' or ')')
            i--;

        return text[i] switch
        {
            '.' or '?' or '!' => 0,
            ';' or ':' => 4,
            ',' => 10,
            _ => 30
        };
    }
}
