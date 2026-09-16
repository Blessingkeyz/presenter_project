namespace BiblePresenter.App.Models;

public sealed class Book
{
    public required int Index { get; init; }
    public required string Name { get; init; }
    public required IReadOnlyList<string> Aliases { get; init; }
}
