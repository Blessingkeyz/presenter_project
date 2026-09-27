using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace BiblePresenter.App.Views;

/// <summary>
/// Draws text as filled + stroked vector geometry (FormattedText.BuildGeometry), giving a crisp
/// bold outline that reads over any busy background photo. This is deliberately not a
/// DropShadowEffect/BlurEffect - those are GPU shader passes that re-run every frame and are
/// costly on old integrated graphics. Geometry is built once per text/font change and then just
/// painted, which is cheap on any GPU.
/// </summary>
public sealed class OutlinedTextBlock : FrameworkElement
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(OutlinedTextBlock),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
        nameof(Fill), typeof(Brush), typeof(OutlinedTextBlock),
        new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(OutlinedTextBlock),
        new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender));

    // NaN means "auto": scale with FontSize so small labels don't get swallowed by a fixed-width
    // outline the way large body text wouldn't be. Set explicitly to override.
    public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(
        nameof(StrokeThickness), typeof(double), typeof(OutlinedTextBlock),
        new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty HasShadowProperty = DependencyProperty.Register(
        nameof(HasShadow), typeof(bool), typeof(OutlinedTextBlock),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ShadowColorProperty = DependencyProperty.Register(
        nameof(ShadowColor), typeof(Brush), typeof(OutlinedTextBlock),
        new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BorderEnabledProperty = DependencyProperty.Register(
        nameof(BorderEnabled), typeof(bool), typeof(OutlinedTextBlock),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsItalicProperty = DependencyProperty.Register(
        nameof(IsItalic), typeof(bool), typeof(OutlinedTextBlock),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsUnderlineProperty = DependencyProperty.Register(
        nameof(IsUnderline), typeof(bool), typeof(OutlinedTextBlock),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FontFamilyProperty = DependencyProperty.Register(
        nameof(FontFamily), typeof(FontFamily), typeof(OutlinedTextBlock),
        new FrameworkPropertyMetadata(new FontFamily("Arial"), FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FontSizeProperty = DependencyProperty.Register(
        nameof(FontSize), typeof(double), typeof(OutlinedTextBlock),
        new FrameworkPropertyMetadata(32.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FontWeightProperty = DependencyProperty.Register(
        nameof(FontWeight), typeof(FontWeight), typeof(OutlinedTextBlock),
        new FrameworkPropertyMetadata(FontWeights.Bold, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TextAlignmentProperty = DependencyProperty.Register(
        nameof(TextAlignment), typeof(TextAlignment), typeof(OutlinedTextBlock),
        new FrameworkPropertyMetadata(TextAlignment.Left, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public Brush Fill { get => (Brush)GetValue(FillProperty); set => SetValue(FillProperty, value); }
    public Brush Stroke { get => (Brush)GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }
    public double StrokeThickness { get => (double)GetValue(StrokeThicknessProperty); set => SetValue(StrokeThicknessProperty, value); }
    public bool HasShadow { get => (bool)GetValue(HasShadowProperty); set => SetValue(HasShadowProperty, value); }
    public Brush ShadowColor { get => (Brush)GetValue(ShadowColorProperty); set => SetValue(ShadowColorProperty, value); }
    public bool BorderEnabled { get => (bool)GetValue(BorderEnabledProperty); set => SetValue(BorderEnabledProperty, value); }
    public bool IsItalic { get => (bool)GetValue(IsItalicProperty); set => SetValue(IsItalicProperty, value); }
    public bool IsUnderline { get => (bool)GetValue(IsUnderlineProperty); set => SetValue(IsUnderlineProperty, value); }
    public FontFamily FontFamily { get => (FontFamily)GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public double FontSize { get => (double)GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public FontWeight FontWeight { get => (FontWeight)GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }
    public TextAlignment TextAlignment { get => (TextAlignment)GetValue(TextAlignmentProperty); set => SetValue(TextAlignmentProperty, value); }

    /// <summary>The outline thickness actually used: the explicit value if set, otherwise scaled to FontSize.</summary>
    private double EffectiveStrokeThickness
    {
        get
        {
            var explicitValue = StrokeThickness;
            return double.IsNaN(explicitValue) ? Math.Max(0.6, FontSize * 0.016) : explicitValue;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (string.IsNullOrEmpty(Text))
            return new Size(0, 0);

        var ft = CreateFormattedText(availableSize.Width);
        var pad = EffectiveStrokeThickness * 2 + 6;
        var width = double.IsInfinity(availableSize.Width) ? ft.Width + pad : availableSize.Width;
        return new Size(width, ft.Height + pad);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        if (string.IsNullOrEmpty(Text))
            return;

        var strokeThickness = EffectiveStrokeThickness;
        var ft = CreateFormattedText(ActualWidth > 0 ? ActualWidth : double.PositiveInfinity);
        var origin = new Point(strokeThickness + 3, strokeThickness + 3);
        var geometry = ft.BuildGeometry(origin);

        if (HasShadow)
        {
            drawingContext.PushTransform(new TranslateTransform(3, 3));
            drawingContext.DrawGeometry(TintedShadowBrush(), null, geometry);
            drawingContext.Pop();
        }

        var pen = BorderEnabled ? new Pen(Stroke, strokeThickness * 2) { LineJoin = PenLineJoin.Round } : null;
        drawingContext.DrawGeometry(Fill, pen, geometry);
    }

    private Brush TintedShadowBrush()
    {
        if (ShadowColor is SolidColorBrush solid)
        {
            var c = solid.Color;
            return new SolidColorBrush(Color.FromArgb(150, c.R, c.G, c.B));
        }
        return ShadowColor;
    }

    private FormattedText CreateFormattedText(double maxWidth)
    {
        var style = IsItalic ? FontStyles.Italic : FontStyles.Normal;
        var typeface = new Typeface(FontFamily, style, FontWeight, FontStretches.Normal);
        var ft = new FormattedText(
            Text,
            CultureInfo.CurrentUICulture,
            System.Windows.FlowDirection.LeftToRight,
            typeface,
            FontSize,
            Fill,
            VisualTreeHelper.GetDpi(this).PixelsPerDip)
        {
            TextAlignment = TextAlignment,
            LineHeight = FontSize * 1.05 // tighter than the font's natural line gap, to read as one dense block like typical presentation software
        };

        if (IsUnderline)
            ft.SetTextDecorations(TextDecorations.Underline);

        var pad = EffectiveStrokeThickness * 2 + 6;
        if (!double.IsInfinity(maxWidth) && maxWidth > pad)
            ft.MaxTextWidth = maxWidth - pad;

        return ft;
    }
}
