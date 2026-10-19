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

    private double _borderSize;
    /// <summary>Outline half-width as a percentage of the rendered text size (0-10).</summary>
    public double BorderSize { get => _borderSize; set => SetProperty(ref _borderSize, value); }

    private bool _shadowEnabled;
    public bool ShadowEnabled { get => _shadowEnabled; set => SetProperty(ref _shadowEnabled, value); }

    private string _shadowColor;
    public string ShadowColor { get => _shadowColor; set => SetProperty(ref _shadowColor, value); }

    private double _shadowDistance;
    /// <summary>How far the shadow sits from the letter, as a percentage of the rendered text size (0-20).</summary>
    public double ShadowDistance { get => _shadowDistance; set => SetProperty(ref _shadowDistance, value); }

    private double _shadowAngle;
    /// <summary>Direction the shadow falls in, in degrees (0 = right, 90 = straight down, 45 = the classic down-right look).</summary>
    public double ShadowAngle { get => _shadowAngle; set => SetProperty(ref _shadowAngle, value); }

    private double _shadowThickness;
    /// <summary>Extra stroke added to the shadow silhouette, as a percentage of the rendered text size (0-10). 0 leaves the shadow as thick as the letters themselves.</summary>
    public double ShadowThickness { get => _shadowThickness; set => SetProperty(ref _shadowThickness, value); }

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

    /// <summary>Appended after SampleTitle at SubtitleFontSize, on the same line - e.g. " (KJV)" for Scripture, where the translation joins the reference instead of sitting on its own row. Empty for Song, which keeps SampleSubtitle as a separate row.</summary>
    public string SampleTitleSecondary { get; init; } = "";

    /// <summary>What the Subtitle size box controls, in this tab's own words - "Translation" for Scripture, "Subtitle" for Song.</summary>
    public string SubtitleFieldLabel { get; init; } = "Subtitle";

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
        _borderSize = source.BorderSize;
        _shadowEnabled = source.ShadowEnabled;
        _shadowColor = source.ShadowColor;
        _shadowDistance = source.ShadowDistance;
        _shadowAngle = source.ShadowAngle;
        _shadowThickness = source.ShadowThickness;
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
        BorderSize = Math.Clamp(BorderSize, 0, 10),
        ShadowEnabled = ShadowEnabled,
        ShadowColor = ShadowColor,
        ShadowDistance = Math.Clamp(ShadowDistance, 0, 20),
        ShadowAngle = ((ShadowAngle % 360) + 360) % 360,
        ShadowThickness = Math.Clamp(ShadowThickness, 0, 10),
        MaxSlideChars = Math.Clamp(MaxSlideChars, ScriptureSlideSplitter.MinAllowedSlideChars, ScriptureSlideSplitter.MaxAllowedSlideChars),
        BackgroundPath = SelectedBackground?.FilePath
    };
}
