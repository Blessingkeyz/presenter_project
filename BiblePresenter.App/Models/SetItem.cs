using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BiblePresenter.App.Models;

public enum SetItemType
{
    Scripture,
    Song,
    Custom,
    /// <summary>A picture shown full screen (an event flyer, say). <see cref="SetItem.Slides"/> holds image file paths rather than text.</summary>
    Image
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

    /// <summary>Section tags for a song's slides ("V1", "C", "B"...), parallel to <see cref="Slides"/>. Empty entries are untagged.</summary>
    private List<string>? _slideLabels;
    public List<string>? SlideLabels
    {
        get => _slideLabels;
        set { _slideLabels = value; Raise(); }
    }

    /// <summary>For a song: true when this set holds its own copy of the words (edited "for this set only") instead of referencing the library.</summary>
    private bool _isSetOnly;
    public bool IsSetOnly
    {
        get => _isSetOnly;
        set { _isSetOnly = value; Raise(); }
    }

    /// <summary>File the song was read from in the song library. Not stored in the set file - it's rediscovered by title.</summary>
    public string? LibraryPath { get; set; }

    public string LabelAt(int index)
        => SlideLabels is { } labels && index < labels.Count ? labels[index] : "";

    public SetItem Clone() => new()
    {
        Type = Type,
        Title = Title,
        Subtitle = Subtitle,
        Slides = new List<string>(Slides),
        SlideLabels = SlideLabels is null ? null : new List<string>(SlideLabels),
        IsSetOnly = IsSetOnly,
        LibraryPath = LibraryPath
    };

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
