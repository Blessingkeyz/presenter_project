using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BiblePresenter.App.Models;

public enum SetItemType
{
    Scripture,
    Song,
    Custom
}

/// <summary>
/// One item in a Set. Every item type is edited the same way: a Title, a Subtitle, and a body
/// split into slides. Scripture/song text is baked in at add-time (not live-linked), matching
/// OpenSong's own behavior, so a set still presents correctly even if its source translation
/// changes or is removed later.
///
/// Implements INotifyPropertyChanged (standard practice for a WPF-bindable model) so the Set
/// Items list and the Slide Editor can bind directly to Title/Subtitle/Slides and stay in sync
/// with each other without extra plumbing.
/// </summary>
public sealed class SetItem : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private const string SlideSeparator = "\n---\n";

    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    private SetItemType _type = SetItemType.Custom;
    public SetItemType Type
    {
        get => _type;
        set { _type = value; Raise(); }
    }

    private string _title = "";
    public string Title
    {
        get => _title;
        set { _title = value; Raise(); }
    }

    private string _subtitle = "";
    public string Subtitle
    {
        get => _subtitle;
        set { _subtitle = value; Raise(); }
    }

    private List<string> _slides = new();
    public List<string> Slides
    {
        get => _slides;
        set { _slides = value; Raise(); }
    }

    public static string JoinSlides(IEnumerable<string> slides) => string.Join(SlideSeparator, slides);

    public static List<string> SplitSlides(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new List<string>();

        return text
            .Replace("\r\n", "\n")
            .Split("---", StringSplitOptions.None)
            .Select(s => s.Trim('\n', '\r', ' ', '\t'))
            .Where(s => s.Length > 0)
            .ToList();
    }
}
