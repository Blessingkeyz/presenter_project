using System.IO;
using System.Windows;
using System.Windows.Input;
using BiblePresenter.App.Models;
using BiblePresenter.App.Services;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace BiblePresenter.App.Views;

public partial class SongPickerDialog : Window
{
    private readonly SongLibraryService _songLibrary;
    private List<(string Title, string FilePath)> _allSongs = new();

    public SetItem? Result { get; private set; }

    public SongPickerDialog(SongLibraryService songLibrary)
    {
        InitializeComponent();
        _songLibrary = songLibrary;
        Title = $"Add Song ({songLibrary.Folder})";
        RefreshList();
    }

    private void RefreshList()
    {
        _allSongs = _songLibrary.ListSongs();
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var filter = FilterBox.Text;
        var filtered = string.IsNullOrWhiteSpace(filter)
            ? _allSongs
            : _allSongs.Where(s => s.Title.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

        SongsList.ItemsSource = filtered.Select(s => s.Title).ToList();
    }

    private void FilterBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => ApplyFilter();

    private void SongsList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        AddButton.IsEnabled = SongsList.SelectedItem is not null;
    }

    private void Add_Click(object sender, RoutedEventArgs e) => TryAddAndClose();

    private void SongsList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => TryAddAndClose();

    private void TryAddAndClose()
    {
        if (SongsList.SelectedItem is not string title)
            return;

        var match = _allSongs.FirstOrDefault(s => s.Title == title);
        if (match.FilePath is null)
            return;

        try
        {
            Result = _songLibrary.ResolveByTitle(title);
            DialogResult = true;
        }
        catch (BibleImportException ex)
        {
            MessageBox.Show(ex.Message, "Import failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Song files|*.*", Title = "Import Song into Library" };
        if (dialog.ShowDialog() != true)
            return;

        try
        {
            _songLibrary.AddExternalFile(dialog.FileName);
            RefreshList();
            FilterBox.Clear();
        }
        catch (IOException ex)
        {
            MessageBox.Show(ex.Message, "Import failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
