using BiblePresenter.App.Models;

namespace BiblePresenter.App.ViewModels;

/// <summary>
/// What the projector output window currently shows. Purely presentational - MainViewModel
/// decides what belongs in each slot (a verse reference, a song title, a custom slide's title,
/// etc.), so scripture, song, and custom Set items all flow through the same three text slots.
/// </summary>
public sealed class OutputViewModel : ObservableObject
{
    private string _topText = "";
    /// <summary>Top line: verse/passage reference, song title, or custom item title.</summary>
    public string TopText
    {
        get => _topText;
        set => SetProperty(ref _topText, value);
    }

    private string _bodyText = "";
    /// <summary>Main text: verse body, song slide lyrics, or custom slide body.</summary>
    public string BodyText
    {
        get => _bodyText;
        set => SetProperty(ref _bodyText, value);
    }

    private string _bottomText = "";
    /// <summary>Bottom line: translation abbreviation, song author, or custom subtitle.</summary>
    public string BottomText
    {
        get => _bottomText;
        set => SetProperty(ref _bottomText, value);
    }

    private BackgroundMedia? _currentBackground;
    public BackgroundMedia? CurrentBackground
    {
        get => _currentBackground;
        set => SetProperty(ref _currentBackground, value);
    }

    private bool _isBlank = true;
    public bool IsBlank
    {
        get => _isBlank;
        set => SetProperty(ref _isBlank, value);
    }

    private string _fontFamilyName = "Helvetica LT";
    public string FontFamilyName
    {
        get => _fontFamilyName;
        set => SetProperty(ref _fontFamilyName, value);
    }

    private double _titleFontSize = 30;
    /// <summary>Size of the top line.</summary>
    public double TitleFontSize
    {
        get => _titleFontSize;
        set => SetProperty(ref _titleFontSize, value);
    }

    private double _bodyFontSize = 60;
    /// <summary>Size of the main body text.</summary>
    public double BodyFontSize
    {
        get => _bodyFontSize;
        set => SetProperty(ref _bodyFontSize, value);
    }

    private double _subtitleFontSize = 26;
    /// <summary>Size of the bottom line.</summary>
    public double SubtitleFontSize
    {
        get => _subtitleFontSize;
        set => SetProperty(ref _subtitleFontSize, value);
    }

    private bool _isBold = true;
    public bool IsBold
    {
        get => _isBold;
        set => SetProperty(ref _isBold, value);
    }

    private bool _isItalic;
    public bool IsItalic
    {
        get => _isItalic;
        set => SetProperty(ref _isItalic, value);
    }

    private bool _isUnderline;
    public bool IsUnderline
    {
        get => _isUnderline;
        set => SetProperty(ref _isUnderline, value);
    }

    private string _textColor = "#FFFFFF";
    public string TextColor
    {
        get => _textColor;
        set => SetProperty(ref _textColor, value);
    }

    private bool _borderEnabled = true;
    public bool BorderEnabled
    {
        get => _borderEnabled;
        set => SetProperty(ref _borderEnabled, value);
    }

    private string _borderColor = "#000000";
    public string BorderColor
    {
        get => _borderColor;
        set => SetProperty(ref _borderColor, value);
    }

    private bool _shadowEnabled = true;
    public bool ShadowEnabled
    {
        get => _shadowEnabled;
        set => SetProperty(ref _shadowEnabled, value);
    }

    private string _shadowColor = "#000000";
    public string ShadowColor
    {
        get => _shadowColor;
        set => SetProperty(ref _shadowColor, value);
    }
}
