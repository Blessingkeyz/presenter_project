using System.Collections.ObjectModel;
using BiblePresenter.App.Models;
using BiblePresenter.App.Services;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using Screen = System.Windows.Forms.Screen;

namespace BiblePresenter.App.ViewModels;

/// <summary>Backs the Settings dialog: two independent style profiles sharing one background library, plus general app settings (projector display).</summary>
public sealed class SettingsViewModel : ObservableObject
{
    private readonly MediaLibraryService _mediaService;

    public StyleEditorViewModel Scripture { get; }
    public StyleEditorViewModel Song { get; }
    public ObservableCollection<BackgroundMedia> Backgrounds { get; }
    public IReadOnlyList<Screen> Screens { get; }

    private Screen? _selectedScreen;
    public Screen? SelectedScreen
    {
        get => _selectedScreen;
        set => SetProperty(ref _selectedScreen, value);
    }

    public RelayCommand ImportBackgroundCommand { get; }
    public RelayCommand RemoveBackgroundCommand { get; }

    public SettingsViewModel(TextStyle scripture, TextStyle song, ObservableCollection<BackgroundMedia> backgrounds,
        MediaLibraryService mediaService, IReadOnlyList<Screen> screens, Screen? selectedScreen)
    {
        _mediaService = mediaService;
        Backgrounds = backgrounds;
        Scripture = new StyleEditorViewModel(scripture, backgrounds)
        {
            SlideLengthHint = "characters per slide. A longer verse continues on the next slide, cut at a natural pause.",
            SampleTitle = "John 3:16",
            SampleBody = "For God so loved the world, that he gave his only begotten Son, that whosoever believeth in him should not perish, but have everlasting life.",
            SampleSubtitle = "King James Version"
        };
        Song = new StyleEditorViewModel(song, backgrounds)
        {
            SlideLengthHint = "characters per slide. A longer stanza is split across slides, cut at a natural pause. The song file is not changed.",
            BodyAlignment = System.Windows.TextAlignment.Center,
            SampleTitle = "Amazing Grace",
            SampleBody = "Amazing grace, how sweet the sound\nThat saved a wretch like me",
            SampleSubtitle = "John Newton"
        };
        Screens = screens;
        _selectedScreen = selectedScreen;

        ImportBackgroundCommand = new RelayCommand(ImportBackground);
        RemoveBackgroundCommand = new RelayCommand(o => RemoveBackground(o as BackgroundMedia), o => o is BackgroundMedia);
    }

    private void ImportBackground()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Images and GIFs (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif",
            Title = "Import Background"
        };
        if (dialog.ShowDialog() != true)
            return;

        try
        {
            var media = _mediaService.Import(dialog.FileName);
            Backgrounds.Add(media);
        }
        catch (NotSupportedException ex)
        {
            MessageBox.Show(ex.Message, "Import failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void RemoveBackground(BackgroundMedia? media)
    {
        if (media is null)
            return;

        _mediaService.Delete(media);
        Backgrounds.Remove(media);
        if (Scripture.SelectedBackground == media)
            Scripture.SelectedBackground = null;
        if (Song.SelectedBackground == media)
            Song.SelectedBackground = null;
    }
}
