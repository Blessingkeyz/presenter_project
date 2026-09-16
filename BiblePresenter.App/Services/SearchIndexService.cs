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
/// over the whole translation, both fast enough to run on every keystroke.
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
        var reference = _referenceParser.TryParse(query);
        if (reference is not null)
        {
            var verses = ResolveReference(reference);
            if (verses.Count > 0)
                return new SearchResult { Verses = verses, WasReferenceJump = true };
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
            var matches = CandidatesForPrefix(word);
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
