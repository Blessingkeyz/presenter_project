using System.Windows;
using System.Windows.Input;
using BiblePresenter.App.Models;

namespace BiblePresenter.App.Views;

/// <summary>
/// Modal editor for a single Set item's Title/Subtitle/Slides, opened on demand instead of being
/// permanently docked in the main window - Set Mode only needs to show the item editor while
/// someone is actively editing, not at all times.
/// </summary>
public partial class EditSetItemDialog : Window
{
    private readonly ICommand _saveToLibraryCommand;

    public EditSetItemDialog(SetItem item, ICommand saveToLibraryCommand)
    {
        InitializeComponent();
        DataContext = item;
        _saveToLibraryCommand = saveToLibraryCommand;
        SaveToLibraryButton.Visibility = item.Type == SetItemType.Song ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SaveToLibrary_Click(object sender, RoutedEventArgs e)
    {
        if (_saveToLibraryCommand.CanExecute(null))
            _saveToLibraryCommand.Execute(null);
    }

    private void Done_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
