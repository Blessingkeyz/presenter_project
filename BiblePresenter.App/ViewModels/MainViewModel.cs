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
    private readonly SettingsStore _settingsStore;
    private readonly SetXmlStore _setStore;
    private readonly SongLibraryService _songLibrary;

    public ObservableCollection<Translation> Translations { get; } = new();
    public ObservableCollection<Verse> SearchResults { get; } = new();
    public ObservableCollection<SongSearchResult> SongResults { get; } = new();
    public ObservableCollection<BackgroundMedia> Backgrounds { get; } = new();
    public ObservableCollection<PresentationSet> Sets { get; } = new();
    public ObservableCollection<SetItem> CurrentSetItems { get; } = new();
    public IReadOnlyList<Screen> Screens { get; }

    public OutputViewModel Output { get; } = new();

    /// <summary>Mirrors what would go live next (the current selection), rendered small in the main window so an operator can confirm content/style before committing with Go Live/Present/Next.</summary>
    public OutputViewModel Preview { get; } = new();

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
        set
        {
            if (SetProperty(ref _selectedResult, value) && value is not null)
                UpdatePreviewForVerse(value);
        }
    }

    private SongSearchResult? _selectedSongResult;
    public SongSearchResult? SelectedSongResult
    {
        get => _selectedSongResult;
        set
        {
            if (SetProperty(ref _selectedSongResult, value))
            {
                PreviewedSong = value is null ? null : _songLibrary.ResolveByTitle(value.Title);
                if (PreviewedSong is not null)
                    UpdatePreviewForSetItem(PreviewedSong);
            }
        }
    }

    private SetItem? _previewedSong;
    /// <summary>Full title/subtitle/lyrics of the currently-selected song search result, so the user can see what they're about to go live with before clicking - useful when two songs share a similar title.</summary>
    public SetItem? PreviewedSong
    {
        get => _previewedSong;
        set => SetProperty(ref _previewedSong, value);
    }

    private bool _isSongSearch;
    /// <summary>False = search the Bible index; true = search the song library.</summary>
    public bool IsSongSearch
    {
        get => _isSongSearch;
        set
        {
            if (SetProperty(ref _isSongSearch, value))
                RunSearch();
        }
    }

    private bool _searchSongsByLyrics;
    /// <summary>Only relevant when <see cref="IsSongSearch"/>: false = match song titles, true = match lyrics content.</summary>
    public bool SearchSongsByLyrics
    {
        get => _searchSongsByLyrics;
        set
        {
            if (SetProperty(ref _searchSongsByLyrics, value) && IsSongSearch)
                RunSearch();
        }
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

    private PresentationSet? _selectedSet;
    public PresentationSet? SelectedSet
    {
        get => _selectedSet;
        set
        {
            if (SetProperty(ref _selectedSet, value))
            {
                CurrentSetItems.Clear();
                if (value is not null)
                    foreach (var item in value.Items)
                        CurrentSetItems.Add(item);
                SelectedSetItem = CurrentSetItems.FirstOrDefault();
            }
        }
    }

    private SetItem? _selectedSetItem;
    public SetItem? SelectedSetItem
    {
        get => _selectedSetItem;
        set
        {
            if (!SetProperty(ref _selectedSetItem, value))
                return;

            if (value is not null)
            {
                _songLibrary.EnsureResolved(value);
                UpdatePreviewForSetItem(value);
            }
            else
            {
                Preview.IsBlank = true;
            }
        }
    }

    private bool _isSetMode;
    public bool IsSetMode
    {
        get => _isSetMode;
        set => SetProperty(ref _isSetMode, value);
    }

    private SearchResult? _lastSearchResult;

    /// <summary>
    /// The currently-presented run of slides (a searched verse range, or a Set item's slides).
    /// Top/Bottom text stay fixed for the whole run while Next/Prev step BodyText through it.
    /// Null when nothing multi-slide is active, in which case Next/Prev fall back to walking
    /// sequentially through the whole Bible from <see cref="_liveVerse"/> (casual browsing).
    /// </summary>
    private List<string>? _activeSlides;
    private int _activeSlideIndex;
    private Verse? _liveVerse;

    /// <summary>Index into <see cref="CurrentSetItems"/> of the Set item currently live, or null when the live slides came from a scripture search/song search instead of a Set. When set, running out of slides on Next/Prev rolls into the adjacent Set item instead of stopping.</summary>
    private int? _activeSetItemIndex;

    private TextStyle _scriptureStyle = new();
    private TextStyle _songStyle = new();

    /// <summary>Which style is currently applied to the live output, so re-opening Settings and clicking OK can refresh it immediately.</summary>
    private SetItemType? _liveStyleType;

    public RelayCommand ImportBibleCommand { get; }
    public RelayCommand RemoveTranslationCommand { get; }
    public RelayCommand GoLiveCommand { get; }
    public RelayCommand ClearCommand { get; }
    public RelayCommand NextVerseCommand { get; }
    public RelayCommand PrevVerseCommand { get; }
    public RelayCommand OpenSettingsCommand { get; }

    public RelayCommand ShowSearchModeCommand { get; }
    public RelayCommand ShowSetModeCommand { get; }
    public RelayCommand NewSetCommand { get; }
    public RelayCommand RenameSetCommand { get; }
    public RelayCommand SaveSetCommand { get; }
    public RelayCommand SaveSetAsCommand { get; }
    public RelayCommand DeleteSetCommand { get; }
    public RelayCommand AddScriptureItemCommand { get; }
    public RelayCommand AddSongItemCommand { get; }
    public RelayCommand AddCustomItemCommand { get; }
    public RelayCommand RemoveSetItemCommand { get; }
    public RelayCommand MoveItemUpCommand { get; }
    public RelayCommand MoveItemDownCommand { get; }
    public RelayCommand PresentSetItemCommand { get; }
    public RelayCommand SaveSongToLibraryCommand { get; }

    public MainViewModel(
        BibleImportService importService,
        TranslationStore translationStore,
        MediaLibraryService mediaService,
        SearchIndexService searchIndex,
        MonitorService monitorService,
        SettingsStore settingsStore,
        SetXmlStore setStore,
        SongLibraryService songLibrary)
    {
        _importService = importService;
        _translationStore = translationStore;
        _mediaService = mediaService;
        _searchIndex = searchIndex;
        _monitorService = monitorService;
        _settingsStore = settingsStore;
        _setStore = setStore;
        _songLibrary = songLibrary;

        ImportBibleCommand = new RelayCommand(ImportBible);
        RemoveTranslationCommand = new RelayCommand(o => RemoveTranslation(o as Translation), o => o is Translation);
        GoLiveCommand = new RelayCommand(GoLive, () => SelectedResult is not null || SelectedSongResult is not null);
        ClearCommand = new RelayCommand(() => Output.IsBlank = true);
        NextVerseCommand = new RelayCommand(() => StepSlide(1));
        PrevVerseCommand = new RelayCommand(() => StepSlide(-1));
        OpenSettingsCommand = new RelayCommand(OpenSettings);

        ShowSearchModeCommand = new RelayCommand(() => IsSetMode = false);
        ShowSetModeCommand = new RelayCommand(() => IsSetMode = true);
        NewSetCommand = new RelayCommand(NewSet);
        RenameSetCommand = new RelayCommand(RenameSet, () => SelectedSet is not null);
        SaveSetCommand = new RelayCommand(SaveSet, () => SelectedSet is not null);
        SaveSetAsCommand = new RelayCommand(SaveSetAs, () => SelectedSet is not null);
        DeleteSetCommand = new RelayCommand(DeleteSet, () => SelectedSet is not null);
        AddScriptureItemCommand = new RelayCommand(AddScriptureItem, () => SelectedSet is not null && SelectedTranslation is not null);
        AddSongItemCommand = new RelayCommand(AddSongItem, () => SelectedSet is not null);
        AddCustomItemCommand = new RelayCommand(AddCustomItem, () => SelectedSet is not null);
        RemoveSetItemCommand = new RelayCommand(() => RemoveSetItem(SelectedSetItem), () => SelectedSetItem is not null);
        MoveItemUpCommand = new RelayCommand(() => MoveSetItem(-1), () => CanMoveSetItem(-1));
        MoveItemDownCommand = new RelayCommand(() => MoveSetItem(1), () => CanMoveSetItem(1));
        PresentSetItemCommand = new RelayCommand(() => PresentSetItem(SelectedSetItem), () => SelectedSetItem is not null);
        SaveSongToLibraryCommand = new RelayCommand(SaveSongToLibrary, () => SelectedSetItem is { Type: SetItemType.Song });

        var settings = _settingsStore.Load();
        _scriptureStyle = settings.Scripture;
        _songStyle = settings.Song;

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
        _scriptureStyle.BackgroundPath ??= defaultBackground?.FilePath;
        _songStyle.BackgroundPath ??= defaultBackground?.FilePath;

        Screens = _monitorService.GetScreens();
        SelectedScreen = _monitorService.GetProjectorScreen();

        if (Translations.Count > 0)
            SelectedTranslation = Translations[0];

        foreach (var s in _setStore.LoadAll())
            Sets.Add(s);
        SelectedSet = Sets.FirstOrDefault();
    }

    private void RunSearch()
    {
        SearchResults.Clear();
        SongResults.Clear();
        _lastSearchResult = null;

        if (string.IsNullOrWhiteSpace(SearchText))
            return;

        if (IsSongSearch)
        {
            foreach (var song in _songLibrary.Search(SearchText, SearchSongsByLyrics))
                SongResults.Add(song);
            return;
        }

        if (SelectedTranslation is null)
            return;

        var result = _searchIndex.Search(SearchText);
        _lastSearchResult = result;
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

    private void RemoveTranslation(Translation? translation)
    {
        if (translation is null)
            return;

        _translationStore.Delete(translation.Id);
        Translations.Remove(translation);
        if (SelectedTranslation == translation)
            SelectedTranslation = Translations.FirstOrDefault();
    }

    private void GoLive()
    {
        if (IsSongSearch)
        {
            GoLiveSong();
            return;
        }

        if (SelectedResult is null)
            return;

        _activeSetItemIndex = null;

        if (_lastSearchResult is { WasReferenceJump: true } result && result.Verses.Count > 1)
        {
            var passage = result.Verses.ToList();
            var index = passage.IndexOf(SelectedResult);
            _activeSlides = passage.Select(ReferenceFormatting.FormatVerseBody).ToList();
            _activeSlideIndex = index >= 0 ? index : 0;
            _liveVerse = null;
            Output.TopText = ReferenceFormatting.FormatRange(passage);
        }
        else
        {
            _activeSlides = null;
            _liveVerse = SelectedResult;
            Output.TopText = SelectedResult.Reference;
        }

        Output.BodyText = ReferenceFormatting.FormatVerseBody(SelectedResult);
        Output.BottomText = SelectedTranslation?.Abbreviation ?? "";
        ApplyStyle(_scriptureStyle, SetItemType.Scripture);
        Output.IsBlank = false;
    }

    /// <summary>Go Live from a song search result: resolves the full song and presents it exactly like a Set song item.</summary>
    private void GoLiveSong()
    {
        if (SelectedSongResult is null)
            return;

        var item = _songLibrary.ResolveByTitle(SelectedSongResult.Title);
        if (item is not null)
            PresentSetItem(item);
    }

    private void PresentSetItem(SetItem? item)
    {
        if (item is null)
            return;

        _songLibrary.EnsureResolved(item);
        if (item.Slides.Count == 0)
            return;

        _activeSlides = item.Slides;
        _activeSlideIndex = 0;
        _liveVerse = null;
        var indexInSet = CurrentSetItems.IndexOf(item);
        _activeSetItemIndex = indexInSet >= 0 ? indexInSet : null;

        Output.TopText = item.Title;
        Output.BottomText = item.Subtitle;
        Output.BodyText = item.Slides[0];
        ApplyStyle(item.Type == SetItemType.Scripture ? _scriptureStyle : _songStyle, item.Type == SetItemType.Scripture ? SetItemType.Scripture : SetItemType.Song);
        Output.IsBlank = false;
    }

    /// <summary>Pushes a full style (font/sizes/weight/colors/border/shadow/background) into the output window.</summary>
    private void ApplyStyle(TextStyle style, SetItemType type)
    {
        _liveStyleType = type;
        ApplyStyleTo(Output, style);
    }

    private void ApplyStyleTo(OutputViewModel target, TextStyle style)
    {
        target.FontFamilyName = style.FontFamily;
        target.TitleFontSize = style.TitleFontSize;
        target.BodyFontSize = style.BodyFontSize;
        target.SubtitleFontSize = style.SubtitleFontSize;
        target.IsBold = style.Bold;
        target.IsItalic = style.Italic;
        target.IsUnderline = style.Underline;
        target.TextColor = style.TextColor;
        target.BorderEnabled = style.BorderEnabled;
        target.BorderColor = style.BorderColor;
        target.ShadowEnabled = style.ShadowEnabled;
        target.ShadowColor = style.ShadowColor;
        target.CurrentBackground = Backgrounds.FirstOrDefault(b => b.FilePath == style.BackgroundPath);
    }

    /// <summary>Updates the mini preview pane to show a single verse (e.g. a search result under the cursor) without touching the live output.</summary>
    private void UpdatePreviewForVerse(Verse verse)
    {
        Preview.TopText = verse.Reference;
        Preview.BodyText = ReferenceFormatting.FormatVerseBody(verse);
        Preview.BottomText = SelectedTranslation?.Abbreviation ?? "";
        ApplyStyleTo(Preview, _scriptureStyle);
        Preview.IsBlank = false;
    }

    /// <summary>Updates the mini preview pane to show a Set item's (or resolved song's) first slide without touching the live output.</summary>
    private void UpdatePreviewForSetItem(SetItem item)
    {
        _songLibrary.EnsureResolved(item);
        Preview.TopText = item.Title;
        Preview.BottomText = item.Subtitle;
        Preview.BodyText = item.Slides.Count > 0 ? item.Slides[0] : "";
        ApplyStyleTo(Preview, item.Type == SetItemType.Scripture ? _scriptureStyle : _songStyle);
        Preview.IsBlank = false;
    }

    private void StepSlide(int direction)
    {
        if (_activeSlides is not null)
        {
            var next = _activeSlideIndex + direction;
            if (next >= 0 && next < _activeSlides.Count)
            {
                _activeSlideIndex = next;
                Output.BodyText = _activeSlides[next];
                Output.IsBlank = false;
                Preview.BodyText = _activeSlides[next];
                return;
            }

            // Ran off the end/start of this item's slides. If it's a Set item (not a scripture
            // passage or a lone song), roll into the adjacent item so an operator can just keep
            // clicking Next through a whole service without reselecting each item by hand.
            if (_activeSetItemIndex is int itemIndex)
            {
                var nextItemIndex = itemIndex + direction;
                if (nextItemIndex >= 0 && nextItemIndex < CurrentSetItems.Count)
                {
                    var nextItem = CurrentSetItems[nextItemIndex];
                    SelectedSetItem = nextItem;
                    PresentSetItem(nextItem);
                    if (direction < 0 && _activeSlides is { Count: > 0 })
                    {
                        // Stepping backward into the previous item should land on its LAST slide,
                        // not its first - otherwise Prev would feel like it skips content. Keep the
                        // mini preview in sync too, since PresentSetItem/SelectedSetItem above just
                        // primed it with slide 0.
                        _activeSlideIndex = _activeSlides.Count - 1;
                        Output.BodyText = _activeSlides[_activeSlideIndex];
                        Preview.BodyText = _activeSlides[_activeSlideIndex];
                    }
                }
            }
            return;
        }

        if (_liveVerse is null || SelectedTranslation is null)
            return;

        var verses = SelectedTranslation.Verses;
        var index = verses.FindIndex(v => v.BookIndex == _liveVerse.BookIndex && v.Chapter == _liveVerse.Chapter && v.Number == _liveVerse.Number);
        var nextIndex = index + direction;
        if (index < 0 || nextIndex < 0 || nextIndex >= verses.Count)
            return;

        _liveVerse = verses[nextIndex];
        Output.TopText = _liveVerse.Reference;
        Output.BodyText = ReferenceFormatting.FormatVerseBody(_liveVerse);
        Output.BottomText = SelectedTranslation.Abbreviation;
        Output.IsBlank = false;

        Preview.TopText = Output.TopText;
        Preview.BodyText = Output.BodyText;
        Preview.BottomText = Output.BottomText;
    }

    private void NewSet()
    {
        var dialog = new Views.PromptDialog("New Set", "Set name:", "New Set") { Owner = System.Windows.Application.Current.MainWindow };
        if (dialog.ShowDialog() != true)
            return;

        var set = new PresentationSet { Name = dialog.ResultText };
        _setStore.Save(set);
        Sets.Add(set);
        SelectedSet = set;
        IsSetMode = true;
    }

    private void RenameSet()
    {
        if (SelectedSet is null)
            return;

        var dialog = new Views.PromptDialog("Rename Set", "New name:", SelectedSet.Name) { Owner = System.Windows.Application.Current.MainWindow };
        if (dialog.ShowDialog() != true)
            return;

        SelectedSet.Name = dialog.ResultText;
        _setStore.Save(SelectedSet);
    }

    private void SaveSet()
    {
        if (SelectedSet is null)
            return;

        SelectedSet.Items = CurrentSetItems.ToList();
        _setStore.Save(SelectedSet);
        StatusMessage = $"Saved \"{SelectedSet.Name}\".";
    }

    private void SaveSetAs()
    {
        if (SelectedSet is null)
            return;

        var dialog = new Views.PromptDialog("Save Set As", "New set name:", SelectedSet.Name + " copy") { Owner = System.Windows.Application.Current.MainWindow };
        if (dialog.ShowDialog() != true)
            return;

        var copy = new PresentationSet
        {
            Name = dialog.ResultText,
            Items = CurrentSetItems.Select(CloneItem).ToList()
        };
        _setStore.Save(copy);
        Sets.Add(copy);
        SelectedSet = copy;
    }

    private static SetItem CloneItem(SetItem item) => new()
    {
        Type = item.Type,
        Title = item.Title,
        Subtitle = item.Subtitle,
        Slides = new List<string>(item.Slides)
    };

    private void DeleteSet()
    {
        if (SelectedSet is null)
            return;

        _setStore.Delete(SelectedSet);
        Sets.Remove(SelectedSet);
        SelectedSet = Sets.FirstOrDefault();
    }

    private void AddScriptureItem()
    {
        if (SelectedSet is null || SelectedTranslation is null)
            return;

        var dialog = new Views.AddScriptureDialog(SelectedTranslation) { Owner = System.Windows.Application.Current.MainWindow };
        if (dialog.ShowDialog() != true || dialog.Result is null)
            return;

        CurrentSetItems.Add(dialog.Result);
        SelectedSetItem = dialog.Result;
    }

    private void AddSongItem()
    {
        if (SelectedSet is null)
            return;

        var dialog = new Views.SongPickerDialog(_songLibrary) { Owner = System.Windows.Application.Current.MainWindow };
        if (dialog.ShowDialog() != true || dialog.Result is null)
            return;

        CurrentSetItems.Add(dialog.Result);
        SelectedSetItem = dialog.Result;
    }

    /// <summary>Writes the selected song item's current title/subtitle/slides back into the Songs library, so edits made here persist for future sets too.</summary>
    private void SaveSongToLibrary()
    {
        if (SelectedSetItem is not { Type: SetItemType.Song } item)
            return;

        _songLibrary.SaveToLibrary(item);
        StatusMessage = $"Saved \"{item.Title}\" to the song library.";
    }

    private void AddCustomItem()
    {
        if (SelectedSet is null)
            return;

        var item = new SetItem { Type = SetItemType.Custom, Title = "New Item", Slides = new List<string> { "" } };
        CurrentSetItems.Add(item);
        SelectedSetItem = item;
    }

    private void RemoveSetItem(SetItem? item)
    {
        if (item is null)
            return;

        CurrentSetItems.Remove(item);
        if (SelectedSetItem == item)
            SelectedSetItem = CurrentSetItems.FirstOrDefault();
    }

    private bool CanMoveSetItem(int direction)
    {
        if (SelectedSetItem is null)
            return false;

        var index = CurrentSetItems.IndexOf(SelectedSetItem);
        var newIndex = index + direction;
        return index >= 0 && newIndex >= 0 && newIndex < CurrentSetItems.Count;
    }

    private void MoveSetItem(int direction)
    {
        if (SelectedSetItem is null)
            return;

        var index = CurrentSetItems.IndexOf(SelectedSetItem);
        var newIndex = index + direction;
        if (index < 0 || newIndex < 0 || newIndex >= CurrentSetItems.Count)
            return;

        CurrentSetItems.Move(index, newIndex);
    }

    private void OpenSettings()
    {
        var dialog = new Views.SettingsWindow(_scriptureStyle, _songStyle, Backgrounds, _mediaService, Screens, SelectedScreen)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };

        if (dialog.ShowDialog() != true || dialog.ResultScripture is null || dialog.ResultSong is null)
            return;

        _scriptureStyle = dialog.ResultScripture;
        _songStyle = dialog.ResultSong;
        _settingsStore.Save(new PresentationSettings { Scripture = _scriptureStyle, Song = _songStyle });

        if (dialog.ResultScreen is not null)
            SelectedScreen = dialog.ResultScreen;

        // If something is already live, refresh it immediately so a mid-service style edit shows right away.
        if (_liveStyleType is { } type)
            ApplyStyle(type == SetItemType.Scripture ? _scriptureStyle : _songStyle, type);
    }
}
