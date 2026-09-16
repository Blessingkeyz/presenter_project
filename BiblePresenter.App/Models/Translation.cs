namespace BiblePresenter.App.Models;

public sealed class Translation
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Abbreviation { get; init; }
    public string Language { get; init; } = "en";
    public required List<Verse> Verses { get; init; }
}
