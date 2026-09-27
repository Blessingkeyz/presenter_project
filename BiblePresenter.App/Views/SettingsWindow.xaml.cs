using System.Collections.ObjectModel;
using System.Windows;
using BiblePresenter.App.Models;
using BiblePresenter.App.Services;
using BiblePresenter.App.ViewModels;
using Screen = System.Windows.Forms.Screen;

namespace BiblePresenter.App.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;

    public TextStyle? ResultScripture { get; private set; }
    public TextStyle? ResultSong { get; private set; }
    public Screen? ResultScreen { get; private set; }

    public SettingsWindow(TextStyle scripture, TextStyle song, ObservableCollection<BackgroundMedia> backgrounds,
        MediaLibraryService mediaService, IReadOnlyList<Screen> screens, Screen? selectedScreen)
    {
        InitializeComponent();
        _viewModel = new SettingsViewModel(scripture, song, backgrounds, mediaService, screens, selectedScreen);
        DataContext = _viewModel;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        ResultScripture = _viewModel.Scripture.ToTextStyle();
        ResultSong = _viewModel.Song.ToTextStyle();
        ResultScreen = _viewModel.SelectedScreen;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
