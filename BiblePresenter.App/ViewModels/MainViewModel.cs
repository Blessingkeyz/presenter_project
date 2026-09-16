using System.Collections.ObjectModel;
using System.IO;
using BiblePresenter.App.Models;
using BiblePresenter.App.Services;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using Screen = System.Windows.Forms.Screen;

namespace BiblePresenter.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly BibleImportService _importService;
    private readonly TranslationStore _translationStore;
    private readonly MediaLibraryService _mediaService;
    private readonly SearchIndexService _searchIndex;
    private readonly MonitorService _monitorService;

    public ObservableCollection<Translation> Translations { get; } = new();
    public ObservableCollection<Verse> SearchResults { get; } = new();
    public ObservableCollection<BackgroundMedia> Backgrounds { get; } = new();
    public IReadOnlyList<Screen> Screens { get; }

    public OutputViewModel Output { get; } = new();

    private Translation? _selectedTranslation;
    public Translation? SelectedTranslation
    {
        get => _selectedTranslation;
        set
        {
            if (SetProperty(ref _selectedTranslation, value) && value is not null)
            {
                _searchIndex.Load(value);
                RunSearch();
            }
        }
    }

    private string _searchText = "";
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
                RunSearch();
        }
    }

    private Verse? _selectedResult;
    public Verse? SelectedResult
    {
        get => _selectedResult;
        set => SetProperty(ref _selectedResult, value);
    }

    private BackgroundMedia? _selectedBackground;
    public BackgroundMedia? SelectedBackground
    {
        get => _selectedBackground;
        set => SetProperty(ref _selectedBackground, value);
    }

    private Screen? _selectedScreen;
    public Screen? SelectedScreen
    {
        get => _selectedScreen;
        set => SetProperty(ref _selectedScreen, value);
    }

    private string? _statusMessage;
    public string? StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public RelayCommand ImportBibleCommand { get; }
    public RelayCommand ImportMediaCommand { get; }
    public RelayCommand RemoveTranslationCommand { get; }
    public RelayCommand RemoveMediaCommand { get; }
    public RelayCommand GoLiveCommand { get; }
    public RelayCommand ClearCommand { get; }
    public RelayCommand NextVerseCommand { get; }
    public RelayCommand PrevVerseCommand { get; }

    public MainViewModel(
        BibleImportService importService,
        TranslationStore translationStore,
        MediaLibraryService mediaService,
        SearchIndexService searchIndex,
        MonitorService monitorService)
    {
        _importService = importService;
        _translationStore = translationStore;
        _mediaService = mediaService;
        _searchIndex = searchIndex;
        _monitorService = monitorService;

        ImportBibleCommand = new RelayCommand(ImportBible);
        ImportMediaCommand = new RelayCommand(ImportMedia);
        RemoveTranslationCommand = new RelayCommand(o => RemoveTranslation(o as Translation), o => o is Translation);
        RemoveMediaCommand = new RelayCommand(o => RemoveMedia(o as BackgroundMedia), o => o is BackgroundMedia);
        GoLiveCommand = new RelayCommand(GoLive, () => SelectedResult is not null);
        ClearCommand = new RelayCommand(() => Output.IsBlank = true);
        NextVerseCommand = new RelayCommand(() => StepVerse(1), () => Output.CurrentVerse is not null);
        PrevVerseCommand = new RelayCommand(() => StepVerse(-1), () => Output.CurrentVerse is not null);

        foreach (var t in _translationStore.LoadAll())
            Translations.Add(t);

        if (Translations.Count == 0)
        {
            var bundled = TryImportBundledDefaultBible();
            if (bundled is not null)
                Translations.Add(bundled);
        }

        foreach (var m in _mediaService.LoadAll())
            Backgrounds.Add(m);

        var defaultBackground = _mediaService.EnsureBundledDefault();
        if (defaultBackground is not null && Backgrounds.All(b => b.FilePath != defaultBackground.FilePath))
            Backgrounds.Add(defaultBackground);
        SelectedBackground = Backgrounds.FirstOrDefault(b => b.Name == "Scripture") ?? Backgrounds.FirstOrDefault();

        Screens = _monitorService.GetScreens();
        SelectedScreen = _monitorService.GetProjectorScreen();

        if (Translations.Count > 0)
            SelectedTranslation = Translations[0];
    }

    private void RunSearch()
    {
        SearchResults.Clear();
        if (SelectedTranslation is null || string.IsNullOrWhiteSpace(SearchText))
            return;

        var result = _searchIndex.Search(SearchText);
        foreach (var verse in result.Verses)
            SearchResults.Add(verse);
    }

    private void ImportBible()
    {
        var dialog = new OpenFileDialog { Filter = "Bible XML files (*.xml)|*.xml", Title = "Import Bible XML" };
        if (dialog.ShowDialog() != true)
            return;

        try
        {
            var translation = _importService.Import(dialog.FileName);
            _translationStore.Save(translation);
            Translations.Add(translation);
            SelectedTranslation = translation;
            StatusMessage = $"Imported \"{translation.Name}\" ({translation.Verses.Count} verses).";
        }
        catch (BibleImportException ex)
        {
            MessageBox.Show(ex.Message, "Import failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>Imports the bundled Resources/KJV.xml (shipped next to the .exe) on first run, so there's a Bible to search out of the box.</summary>
    private Translation? TryImportBundledDefaultBible()
    {
        var bundledPath = Path.Combine(AppContext.BaseDirectory, "Resources", "KJV.xml");
        if (!File.Exists(bundledPath))
            return null;

        try
        {
            var translation = _importService.Import(bundledPath);
            _translationStore.Save(translation);
            return translation;
        }
        catch (BibleImportException)
        {
            return null;
        }
    }

    private void ImportMedia()
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
            SelectedBackground = media;
        }
        catch (NotSupportedException ex)
        {
            MessageBox.Show(ex.Message, "Import failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void RemoveTranslation(Translation? translation)
    {
        if (translation is null)
            return;

        _translationStore.Delete(translation.Id);
        Translations.Remove(translation);
        if (SelectedTranslation == translation)
            SelectedTranslation = Translations.FirstOrDefault();
    }

    private void RemoveMedia(BackgroundMedia? media)
    {
        if (media is null)
            return;

        _mediaService.Delete(media);
        Backgrounds.Remove(media);
        if (SelectedBackground == media)
            SelectedBackground = null;
    }

    private void GoLive()
    {
        if (SelectedResult is null)
            return;

        Output.CurrentVerse = SelectedResult;
        Output.CurrentBackground = SelectedBackground;
        Output.IsBlank = false;
    }

    private void StepVerse(int direction)
    {
        var current = Output.CurrentVerse;
        var translation = SelectedTranslation;
        if (current is null || translation is null)
            return;

        var verses = translation.Verses;
        var index = verses.FindIndex(v => v.BookIndex == current.BookIndex && v.Chapter == current.Chapter && v.Number == current.Number);
        var nextIndex = index + direction;
        if (index < 0 || nextIndex < 0 || nextIndex >= verses.Count)
            return;

        Output.CurrentVerse = verses[nextIndex];
        Output.IsBlank = false;
    }
}
