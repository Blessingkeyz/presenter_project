using System.Windows;
using SelectionChangedEventArgs = System.Windows.Controls.SelectionChangedEventArgs;
using UserControl = System.Windows.Controls.UserControl;

namespace BiblePresenter.App.Views;

/// <summary>A row of preset color swatches plus a hex box, editing one "#RRGGBB" string. Header sits above; IsActive dims the swatches/hex when an optional effect is off.</summary>
public partial class ColorSwatchPicker : UserControl
{
    private static readonly string[] Palette =
    {
        "#FFFFFF", "#000000", "#FFD700", "#E0C060", "#FF0000", "#FFA500",
        "#00A000", "#3070FF", "#00CFCF", "#CF00CF", "#808080"
    };

    public static readonly DependencyProperty ColorProperty = DependencyProperty.Register(
        nameof(Color), typeof(string), typeof(ColorSwatchPicker),
        new FrameworkPropertyMetadata("#FFFFFF", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnColorChanged));

    public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
        nameof(Header), typeof(object), typeof(ColorSwatchPicker),
        new PropertyMetadata(null, (d, e) => ((ColorSwatchPicker)d).HeaderHost.Content = e.NewValue));

    public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(
        nameof(IsActive), typeof(bool), typeof(ColorSwatchPicker),
        new PropertyMetadata(true, (d, e) => ((ColorSwatchPicker)d).ApplyActive((bool)e.NewValue)));

    private bool _syncing;

    public string Color { get => (string)GetValue(ColorProperty); set => SetValue(ColorProperty, value); }
    public object? Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }
    public bool IsActive { get => (bool)GetValue(IsActiveProperty); set => SetValue(IsActiveProperty, value); }

    public ColorSwatchPicker()
    {
        InitializeComponent();
        Swatches.ItemsSource = Palette;
        SyncSelection();
    }

    private static void OnColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((ColorSwatchPicker)d).SyncSelection();

    private void SyncSelection()
    {
        _syncing = true;
        Swatches.SelectedItem = Palette.FirstOrDefault(p => string.Equals(p, Color, StringComparison.OrdinalIgnoreCase));
        _syncing = false;
    }

    private void Swatches_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing || e.AddedItems.Count == 0)
            return;

        Color = (string)e.AddedItems[0]!;
    }

    private void ApplyActive(bool active)
    {
        HexBox.IsEnabled = active;
        Swatches.IsEnabled = active;
        HexBox.Opacity = Swatches.Opacity = active ? 1.0 : 0.35;
    }
}
