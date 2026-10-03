using System.Windows;
using BiblePresenter.App.Models;

namespace BiblePresenter.App.Views;

/// <summary>
/// Modal editor for a Custom or Image Set item's Title/Subtitle/Slides. Songs are edited in
/// <see cref="SongEditorDialog"/> and scripture can't be edited at all.
/// </summary>
public partial class EditSetItemDialog : Window
{
    public EditSetItemDialog(SetItem item)
    {
        InitializeComponent();
        DataContext = item;
        Title = $"Edit {item.Type}";

        if (item.Type == SetItemType.Image)
        {
            // An image item's "slides" are file paths, not text - only its name is editable.
            ImageHint.Visibility = Visibility.Visible;
            SubtitleLabel.Visibility = SubtitleBox.Visibility = Visibility.Collapsed;
            SlidesLabel.Visibility = SlidesBox.Visibility = Visibility.Collapsed;
        }
    }

    private void Done_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
