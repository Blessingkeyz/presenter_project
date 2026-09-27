using System.Text.RegularExpressions;
using BiblePresenter.App.Models;

namespace BiblePresenter.App.Services;

public sealed class SearchResult
{
    public required IReadOnlyList<Verse> Verses { get; init; }
    public required bool WasReferenceJump { get; init; }
}

/// <summary>
/// Fast, ProPresenter-style verse search over a single loaded translation: a direct
/// (book, chapter, verse) reference jump, or an in-memory inverted-index prefix search
/// over the whole translation, both fast enough to run on every keystroke. Falls back to
/// approximate ("closest to") word matching when a query word has no exact/prefix hits,
/// so small typos still find something.
/// </summary>
public sealed class SearchIndexService
{
    private static readonly Regex WordSplit = new(@"[^a-zA-Z0-9']+", RegexOptions.Compiled);

    private readonly ReferenceParser _referenceParser;

    private List<Verse> _verses = new();
    private List<string> _sortedWords = new();
    private Dictionary<string, List<int>> _postings = new(StringComparer.Ordinal);
    private Dictionary<(int book, int chapter, int verse), int> _byReference = new();

    public SearchIndexService(ReferenceParser? referenceParser = null)
    {
        _referenceParser = referenceParser ?? new ReferenceParser();
    }

    public void Load(Translation translation)
    {
        _verses = translation.Verses;
        _byReference = new Dictionary<(int, int, int), int>(_verses.Count);
        var postings = new Dictionary<string, List<int>>(StringComparer.Ordinal);

        for (var i = 0; i < _verses.Count; i++)
        {
            var verse = _verses[i];
            _byReference[(verse.BookIndex, verse.Chapter, verse.Number)] = i;

            foreach (var rawWord in WordSplit.Split(verse.Text))
            {
                if (rawWord.Length == 0)
                    continue;
                var word = rawWord.ToLowerInvariant();
                if (!postings.TryGetValue(word, out var list))
                {
                    list = new List<int>();
                    postings[word] = list;
                }
                list.Add(i);
            }
        }

        _postings = postings;
        _sortedWords = postings.Keys.OrderBy(w => w, StringComparer.Ordinal).ToList();
    }

    public SearchResult Search(string query, int maxResults = 200)
    {
        var references = _referenceParser.ParseAll(query);
        if (references.Count > 0)
        {
            var allVerses = new List<Verse>();
            foreach (var reference in references)
                allVerses.AddRange(ResolveReference(reference));

            if (allVerses.Count > 0)
            {
                // A single matched book forms one continuous passage (used for range
                // Present/Next-Prev stepping); multiple candidate books (an ambiguous stem like
                // "thess") are shown as a flat list of individually selectable verses instead -
                // they don't share one book/chapter, so they can't form one passage.
                return new SearchResult { Verses = allVerses, WasReferenceJump = references.Count == 1 };
            }
        }

        return new SearchResult { Verses = KeywordSearch(query, maxResults), WasReferenceJump = false };
    }

    private List<Verse> ResolveReference(ParsedReference reference)
    {
        if (reference.VerseStart is int v1)
        {
            var v2 = reference.VerseEnd ?? v1;
            var result = new List<Verse>();
            for (var v = v1; v <= v2; v++)
            {
                if (_byReference.TryGetValue((reference.Book.Index, reference.Chapter, v), out var idx))
                    result.Add(_verses[idx]);
            }
            return result;
        }

        // Whole-chapter jump: no verse given.
        return _verses.Where(x => x.BookIndex == reference.Book.Index && x.Chapter == reference.Chapter).ToList();
    }

    private List<Verse> KeywordSearch(string query, int maxResults)
    {
        var words = WordSplit.Split(query)
            .Where(w => w.Length > 0)
            .Select(w => w.ToLowerInvariant())
            .Distinct()
            .ToList();

        if (words.Count == 0)
            return new List<Verse>();

        HashSet<int>? candidates = null;
        foreach (var word in words)
        {
            var matches = CandidatesForWord(word);
            if (candidates is null)
            {
                candidates = matches;
            }
            else
            {
                candidates.IntersectWith(matches);
            }

            if (candidates.Count == 0)
                return new List<Verse>();
        }

        return candidates!
            .OrderBy(i => i)
            .Take(maxResults)
            .Select(i => _verses[i])
            .ToList();
    }

    private HashSet<int> CandidatesForWord(string word)
    {
        var exact = CandidatesForPrefix(word);
        return exact.Count > 0 ? exact : FuzzyCandidates(word);
    }

    private HashSet<int> CandidatesForPrefix(string prefix)
    {
        var result = new HashSet<int>();
        var start = LowerBound(prefix);
        for (var i = start; i < _sortedWords.Count; i++)
        {
            var word = _sortedWords[i];
            if (!word.StartsWith(prefix, StringComparison.Ordinal))
                break;
            foreach (var idx in _postings[word])
                result.Add(idx);
        }
        return result;
    }

    /// <summary>
    /// "Closest to" fallback for a word with no exact/prefix hits: finds indexed words within a
    /// small edit distance (so e.g. "beleive" still finds "believe") and returns the union of
    /// verses for whichever word(s) are closest. Skipped for very short words, where almost
    /// everything is within 1-2 edits and results would be meaningless.
    /// </summary>
    private HashSet<int> FuzzyCandidates(string word)
    {
        var result = new HashSet<int>();
        if (word.Length < 4)
            return result;

        var maxDistance = word.Length <= 6 ? 1 : 2;
        var bestDistance = maxDistance + 1;

        foreach (var candidate in _sortedWords)
        {
            if (Math.Abs(candidate.Length - word.Length) > maxDistance)
                continue;

            var distance = LevenshteinDistance(word, candidate, maxDistance);
            if (distance > maxDistance)
                continue;

            if (distance < bestDistance)
            {
                bestDistance = distance;
                result.Clear();
            }
            if (distance == bestDistance)
            {
                foreach (var idx in _postings[candidate])
                    result.Add(idx);
            }
        }

        return result;
    }

    private static int LevenshteinDistance(string a, string b, int maxDistance)
    {
        var lenA = a.Length;
        var lenB = b.Length;

        var previous = new int[lenB + 1];
        var current = new int[lenB + 1];
        for (var j = 0; j <= lenB; j++)
            previous[j] = j;

        for (var i = 1; i <= lenA; i++)
        {
            current[0] = i;
            var rowMin = current[0];
            for (var j = 1; j <= lenB; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
                rowMin = Math.Min(rowMin, current[j]);
            }

            if (rowMin > maxDistance)
                return maxDistance + 1;

            (previous, current) = (current, previous);
        }

        return previous[lenB];
    }

    private int LowerBound(string prefix)
    {
        var lo = 0;
        var hi = _sortedWords.Count;
        while (lo < hi)
        {
            var mid = (lo + hi) / 2;
            if (string.CompareOrdinal(_sortedWords[mid], prefix) < 0)
                lo = mid + 1;
            else
                hi = mid;
        }
        return lo;
    }
}
