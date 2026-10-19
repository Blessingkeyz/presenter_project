namespace BiblePresenter.App.Models;

/// <summary>One complete presentation style: font, sizes, weight/style, colors, outline, shadow, and background.</summary>
public sealed class TextStyle
{
    public string FontFamily { get; set; } = "Helvetica LT";

    /// <summary>Reference line at the top, e.g. "Genesis 22:2".</summary>
    public double TitleFontSize { get; set; } = 30;

    /// <summary>Main body text.</summary>
    public double BodyFontSize { get; set; } = 180;

    /// <summary>Translation/subtitle line at the bottom.</summary>
    public double SubtitleFontSize { get; set; } = 26;

    public bool Bold { get; set; } = true;
    public bool Italic { get; set; }
    public bool Underline { get; set; }

    public string TextColor { get; set; } = "#FFFFFF";

    public bool BorderEnabled { get; set; } = true;
    public string BorderColor { get; set; } = "#000000";
    /// <summary>Outline half-width as a percentage of the text's own rendered size, so it stays in proportion at any size instead of a fixed pixel width looking right at only one. 1.5 is a crisp, fairly thin edge.</summary>
    public double BorderSize { get; set; } = 1.5;

    public bool ShadowEnabled { get; set; } = true;
    public string ShadowColor { get; set; } = "#000000";
    /// <summary>How far the shadow sits from the letter, as a percentage of the text's own rendered size - small enough to read as the letter's own depth, not a separate offset copy.</summary>
    public double ShadowDistance { get; set; } = 5;
    /// <summary>Direction the shadow falls in, in degrees: 0 = right, 90 = straight down, 45 = the classic down-right 3D look.</summary>
    public double ShadowAngle { get; set; } = 45;
    /// <summary>Extra stroke added to the shadow silhouette, as a percentage of the text's own rendered size. 0 leaves the shadow as thick as the letters themselves; raise it to make the shadow bolder than the border.</summary>
    public double ShadowThickness { get; set; } = 5;

    /// <summary>File path of the selected background image/GIF, or null for none.</summary>
    public string? BackgroundPath { get; set; }

    /// <summary>A verse (Scripture) or a stanza/slide (Song, Custom) longer than this many characters continues on the next slide.</summary>
    public int MaxSlideChars { get; set; } = 160;
}

public sealed class PresentationSettings
{
    public TextStyle Scripture { get; set; } = new();

    /// <summary>Also used for Custom Set items (announcements, etc.) - anything that isn't Scripture. Songs run longer than verses, so the default is higher.</summary>
    public TextStyle Song { get; set; } = new() { MaxSlideChars = 300 };
}
