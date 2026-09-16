namespace BiblePresenter.App.Models;

public sealed class Verse
{
    public required string TranslationId { get; init; }
    public required int BookIndex { get; init; }
    public required string BookName { get; init; }
    public required int Chapter { get; init; }
    public required int Number { get; init; }
    public required string Text { get; init; }

    public string Reference => $"{BookName} {Chapter}:{Number}";
}
