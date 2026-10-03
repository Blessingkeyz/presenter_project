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

    // NaN means "auto": an outline in proportion to the text as drawn (see StrokeFor). Set explicitly to override.
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

    /// <summary>When set, the text shrinks (never grows) until it fits the height it's given, so a long verse stays on screen.</summary>
    public static readonly DependencyProperty FitToHeightProperty = DependencyProperty.Register(
        nameof(FitToHeight), typeof(bool), typeof(OutlinedTextBlock),
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
    public bool FitToHeight { get => (bool)GetValue(FitToHeightProperty); set => SetValue(FitToHeightProperty, value); }

    private double _fitScale = 1;
    public FontFamily FontFamily { get => (FontFamily)GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public double FontSize { get => (double)GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public FontWeight FontWeight { get => (FontWeight)GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }
    public TextAlignment TextAlignment { get => (TextAlignment)GetValue(TextAlignmentProperty); set => SetValue(TextAlignmentProperty, value); }

    /// <summary>
    /// Outline half-width as a fraction of the text as it is actually drawn. Tying it to the rendered size
    /// (not the requested one) keeps the outline in proportion: a big body size that is fitted down doesn't
    /// get a heavy edge, and small text doesn't get one either. 1% of the size gives 0.6 at a 60pt body.
    /// A soft gradient scrim sits behind the text in the output/preview panels and carries most of the contrast.
    /// </summary>
    private const double AutoStrokeFraction = 0.010;

    /// <summary>
    /// The outline half-width used at a given fit scale: the explicit StrokeThickness if set, otherwise
    /// <see cref="AutoStrokeFraction"/> of the rendered font size.
    /// </summary>
    private double StrokeFor(double scale)
    {
        var explicitValue = StrokeThickness;
        if (!double.IsNaN(explicitValue))
            return explicitValue;

        return Math.Max(0.05, FontSize * scale * AutoStrokeFraction);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (string.IsNullOrEmpty(Text))
            return new Size(0, 0);

        _fitScale = FitToHeight ? FitScaleFor(availableSize) : 1;
        var ft = CreateFormattedText(availableSize.Width, _fitScale);
        var pad = StrokeFor(_fitScale) * 2 + 6;
        var width = double.IsInfinity(availableSize.Width) ? ft.Width + pad : availableSize.Width;
        var height = ft.Height + pad;
        return new Size(width, FitToHeight && !double.IsInfinity(availableSize.Height) ? Math.Min(height, availableSize.Height) : height);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        if (string.IsNullOrEmpty(Text))
            return;

        var strokeThickness = StrokeFor(_fitScale);
        var ft = CreateFormattedText(ActualWidth > 0 ? ActualWidth : double.PositiveInfinity, _fitScale);
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

    /// <summary>
    /// The largest scale (at most 1) at which all the text fits <paramref name="available"/>: lines wrap to the
    /// width, and the scale shrinks until the wrapped block fits the height. Text height only grows with
    /// scale, so a bisection finds the largest size that still shows everything, with no fixed floor.
    /// </summary>
    private double FitScaleFor(Size available)
    {
        if (string.IsNullOrEmpty(Text) || double.IsInfinity(available.Height) || available.Height <= 0)
            return 1;

        bool Fits(double scale) => CreateFormattedText(available.Width, scale).Height + StrokeFor(scale) * 2 + 6 <= available.Height;

        if (Fits(1))
            return 1;

        var low = 0.02;
        var high = 1.0;
        for (var step = 0; step < 16; step++)
        {
            var mid = (low + high) / 2;
            if (Fits(mid))
                low = mid;
            else
                high = mid;
        }

        return low;
    }

    private FormattedText CreateFormattedText(double maxWidth, double scale = 1)
    {
        var style = IsItalic ? FontStyles.Italic : FontStyles.Normal;
        var typeface = new Typeface(FontFamily, style, FontWeight, FontStretches.Normal);
        var size = FontSize * scale;
        var ft = new FormattedText(
            Text,
            CultureInfo.CurrentUICulture,
            System.Windows.FlowDirection.LeftToRight,
            typeface,
            size,
            Fill,
            VisualTreeHelper.GetDpi(this).PixelsPerDip)
        {
            TextAlignment = TextAlignment,
            LineHeight = size * 1.05 // tighter than the font's natural line gap, to read as one dense block like typical presentation software
        };

        if (IsUnderline)
            ft.SetTextDecorations(TextDecorations.Underline);

        var pad = StrokeFor(scale) * 2 + 6;
        if (!double.IsInfinity(maxWidth) && maxWidth > pad)
            ft.MaxTextWidth = maxWidth - pad;

        return ft;
    }
}
