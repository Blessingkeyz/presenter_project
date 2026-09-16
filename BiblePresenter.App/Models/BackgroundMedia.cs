namespace BiblePresenter.App.Models;

public enum BackgroundMediaKind
{
    Image,
    Gif
}

public sealed class BackgroundMedia
{
    public required string FilePath { get; init; }
    public required string Name { get; init; }
    public required BackgroundMediaKind Kind { get; init; }
}
