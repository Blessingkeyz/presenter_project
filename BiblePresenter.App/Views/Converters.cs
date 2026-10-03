using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BiblePresenter.App.Models;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Screen = System.Windows.Forms.Screen;

namespace BiblePresenter.App.Views;

public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => !(value is true);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => !(value is true);
}

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class SlidesToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is List<string> slides ? SetItem.JoinSlides(slides) : "";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => SetItem.SplitSlides(value as string ?? "");
}

public sealed class ScreenToLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not Screen screen)
            return "";

        var role = screen.Primary ? "Primary" : "Secondary";
        return $"{screen.DeviceName.Replace(@"\\.\", "")} — {role} ({screen.Bounds.Width}x{screen.Bounds.Height})";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class FontNameToFontFamilyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => BundledFonts.Resolve(value as string ?? "");

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class HexToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string hex || string.IsNullOrWhiteSpace(hex))
            return Brushes.White;

        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hex)!;
            return new SolidColorBrush(color);
        }
        catch (FormatException)
        {
            return Brushes.White;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is SolidColorBrush brush ? brush.Color.ToString() : "#FFFFFF";
}

public sealed class BoolToFontWeightConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? FontWeights.Bold : FontWeights.Normal;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Equals(value, FontWeights.Bold);
}

public sealed class PathToImageSourceConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        return new BitmapImage(new Uri(path, UriKind.Absolute));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Small decoded copy of an image, for filmstrip thumbnails (avoids decoding a full-size photo per tile).</summary>
public sealed class PathToThumbnailConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        // ConverterParameter, when given, is the decode width in pixels (default 240).
        var width = parameter is string text && int.TryParse(text, out var parsed) ? parsed : 240;

        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = new Uri(path, UriKind.Absolute);
        image.DecodePixelWidth = width;
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();
        image.Freeze();
        return image;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Shows a centered hint in the results area when the active list (Bible or Song) has nothing in it, instead of leaving bare empty space: [IsSongSearch, SearchResults.Count, SongResults.Count].</summary>
public sealed class SearchEmptyStateVisibilityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        var isSongSearch = values.Length > 0 && values[0] is true;
        var bibleCount = values.Length > 1 && values[1] is int i1 ? i1 : 0;
        var songCount = values.Length > 2 && values[2] is int i2 ? i2 : 0;
        var activeCount = isSongSearch ? songCount : bibleCount;
        return activeCount == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Picks the empty-state hint text: [SearchText, IsSongSearch].</summary>
public sealed class SearchEmptyStateMessageConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        var searchText = values.Length > 0 ? values[0] as string : null;
        var isSongSearch = values.Length > 1 && values[1] is true;

        if (string.IsNullOrWhiteSpace(searchText))
            return isSongSearch ? "Search your song library by title or lyrics" : "Search Scripture by reference or keyword";

        return $"No matches for “{searchText}”";
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Pairs a song's slides with their section labels: [Slides, SlideLabels] -> one view per slide.</summary>
public sealed class SlidesWithLabelsConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length == 0 || values[0] is not IEnumerable<string> slides)
            return Array.Empty<BiblePresenter.App.ViewModels.SongSlideView>();

        var labels = values.Length > 1 && values[1] is IEnumerable<string> l ? l.ToList() : new List<string>();
        return slides
            .Select((text, i) => new BiblePresenter.App.ViewModels.SongSlideView(i < labels.Count ? labels[i] : "", text))
            .ToList();
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Labels the single Go Live/Present/Stop transport button: [IsSetMode, Output.IsBlank] -> "Go Live" / "Present" / "Stop".</summary>
public sealed class LiveButtonLabelConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        var isSetMode = values.Length > 0 && values[0] is true;
        var isBlank = values.Length > 1 && values[1] is true;
        if (!isBlank)
            return "Stop";
        return isSetMode ? "Present" : "Go Live";
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
