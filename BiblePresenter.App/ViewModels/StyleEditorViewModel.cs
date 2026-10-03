using BiblePresenter.App.Models;
using BiblePresenter.App.Services;

namespace BiblePresenter.App.ViewModels;

/// <summary>Editable copy of one TextStyle (Scripture or Song), backing one tab of the Settings dialog.</summary>
public sealed class StyleEditorViewModel : ObservableObject
{
    private string _fontFamily;
    public string FontFamily { get => _fontFamily; set => SetProperty(ref _fontFamily, value); }

    private double _titleFontSize;
    public double TitleFontSize { get => _titleFontSize; set => SetProperty(ref _titleFontSize, value); }

    private double _bodyFontSize;
    public double BodyFontSize { get => _bodyFontSize; set => SetProperty(ref _bodyFontSize, value); }

    private double _subtitleFontSize;
    public double SubtitleFontSize { get => _subtitleFontSize; set => SetProperty(ref _subtitleFontSize, value); }

    private bool _bold;
    public bool Bold { get => _bold; set => SetProperty(ref _bold, value); }

    private bool _italic;
    public bool Italic { get => _italic; set => SetProperty(ref _italic, value); }

    private bool _underline;
    public bool Underline { get => _underline; set => SetProperty(ref _underline, value); }

    private string _textColor;
    public string TextColor { get => _textColor; set => SetProperty(ref _textColor, value); }

    private bool _borderEnabled;
    public bool BorderEnabled { get => _borderEnabled; set => SetProperty(ref _borderEnabled, value); }

    private string _borderColor;
    public string BorderColor { get => _borderColor; set => SetProperty(ref _borderColor, value); }

    private bool _shadowEnabled;
    public bool ShadowEnabled { get => _shadowEnabled; set => SetProperty(ref _shadowEnabled, value); }

    private string _shadowColor;
    public string ShadowColor { get => _shadowColor; set => SetProperty(ref _shadowColor, value); }

    private int _maxSlideChars;
    /// <summary>Characters before a long verse (Scripture) or stanza (Song) continues on the next slide.</summary>
    public int MaxSlideChars { get => _maxSlideChars; set => SetProperty(ref _maxSlideChars, value); }

    /// <summary>Explains the length setting under its box, in words that fit this tab's kind of content.</summary>
    public string SlideLengthHint { get; init; } = "";

    /// <summary>How the sample body sits in the preview, matching what the projector does for this kind of content.</summary>
    public System.Windows.TextAlignment BodyAlignment { get; init; } = System.Windows.TextAlignment.Left;

    /// <summary>Placeholder text shown in the Settings preview so each tab reads like real content of its own kind.</summary>
    public string SampleTitle { get; init; } = "Sample Title";
    public string SampleBody { get; init; } = "Sample body text";
    public string SampleSubtitle { get; init; } = "Sample subtitle";

    private BackgroundMedia? _selectedBackground;
    public BackgroundMedia? SelectedBackground { get => _selectedBackground; set => SetProperty(ref _selectedBackground, value); }

    public StyleEditorViewModel(TextStyle source, IEnumerable<BackgroundMedia> backgrounds)
    {
        _fontFamily = source.FontFamily;
        _titleFontSize = source.TitleFontSize;
        _bodyFontSize = source.BodyFontSize;
        _subtitleFontSize = source.SubtitleFontSize;
        _bold = source.Bold;
        _italic = source.Italic;
        _underline = source.Underline;
        _textColor = source.TextColor;
        _borderEnabled = source.BorderEnabled;
        _borderColor = source.BorderColor;
        _shadowEnabled = source.ShadowEnabled;
        _shadowColor = source.ShadowColor;
        _maxSlideChars = source.MaxSlideChars;
        _selectedBackground = backgrounds.FirstOrDefault(b => b.FilePath == source.BackgroundPath);
    }

    public TextStyle ToTextStyle() => new()
    {
        FontFamily = FontFamily,
        TitleFontSize = TitleFontSize,
        BodyFontSize = BodyFontSize,
        SubtitleFontSize = SubtitleFontSize,
        Bold = Bold,
        Italic = Italic,
        Underline = Underline,
        TextColor = TextColor,
        BorderEnabled = BorderEnabled,
        BorderColor = BorderColor,
        ShadowEnabled = ShadowEnabled,
        ShadowColor = ShadowColor,
        MaxSlideChars = Math.Clamp(MaxSlideChars, ScriptureSlideSplitter.MinAllowedSlideChars, ScriptureSlideSplitter.MaxAllowedSlideChars),
        BackgroundPath = SelectedBackground?.FilePath
    };
}
