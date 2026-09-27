namespace BiblePresenter.App.Models;

/// <summary>One match from searching the song library (by title or by lyrics content).</summary>
public sealed class SongSearchResult
{
    public required string Title { get; init; }
    public required string FilePath { get; init; }
}
