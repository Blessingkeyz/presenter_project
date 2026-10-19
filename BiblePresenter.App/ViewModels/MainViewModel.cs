using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Windows.Threading;
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

    /// <summary>Sits as the last row in the Translation dropdown, styled like any other entry; selecting it triggers Import instead of a real translation switch - see MainWindow's SelectionChanged handler.</summary>
    public static readonly Translation ImportTranslationSentinel = new()
    {
        Id = "__import__",
        Name = "Import Bible XML...",
        Abbreviation = "Import...",
        Verses = new List<Verse>()
    };

    public ObservableCollection<Translation> Translations { get; } = new();

    /// <summary>Translations plus the Import sentinel at the end, for the Translation ComboBox to display as one list.</summary>
    public ObservableCollection<Translation> TranslationPickerItems { get; } = new();
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
                RefreshLiveState();
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
            // Save the set we're leaving while CurrentSetItems still holds its items.
            FlushPendingSave();

            if (SetProperty(ref _selectedSet, value))
            {
                // Loading a set isn't an edit, so it must not trigger an autosave.
                _suppressAutosave = true;
                try
                {
                    foreach (var item in CurrentSetItems)
                        item.PropertyChanged -= OnSetItemChanged;
                    CurrentSetItems.Clear();
                    if (value is not null)
                        foreach (var item in value.Items)
                            CurrentSetItems.Add(item);
                }
                finally
                {
                    _suppressAutosave = false;
                }

                SelectedSetItem = CurrentSetItems.FirstOrDefault();
            }
        }
    }

    // ---- Autosave: any change to the open set is written to disk shortly after the last edit ----

    private readonly DispatcherTimer _autosaveTimer = new() { Interval = TimeSpan.FromMilliseconds(700) };
    private PresentationSet? _setPendingSave;
    private bool _suppressAutosave;

    private void OnSetItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
            foreach (SetItem item in e.OldItems)
                item.PropertyChanged -= OnSetItemChanged;
        if (e.NewItems is not null)
            foreach (SetItem item in e.NewItems)
                item.PropertyChanged += OnSetItemChanged;

        MarkSetDirty();
    }

    private void OnSetItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        // A song's words live in the song library and the set file only names it, so resolving or
        // editing those in the set isn't a change to the set itself.
        if (sender is SetItem { Type: SetItemType.Song, IsSetOnly: false } && e.PropertyName != nameof(SetItem.Title))
            return;

        MarkSetDirty();
    }

    private void MarkSetDirty()
    {
        if (_suppressAutosave || SelectedSet is null)
            return;

        _setPendingSave = SelectedSet;
        _autosaveTimer.Stop();
        _autosaveTimer.Start();
    }

    /// <summary>Writes any unsaved change to the set right now (also called when switching sets and when the app closes).</summary>
    public void FlushPendingSave()
    {
        _autosaveTimer.Stop();
        var set = _setPendingSave;
        _setPendingSave = null;
        if (set is null)
            return;

        if (ReferenceEquals(set, _selectedSet))
            set.Items = CurrentSetItems.ToList();

        try
        {
            _setStore.Save(set);
            StatusMessage = $"Saved \"{set.Name}\".";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusMessage = $"Couldn't save \"{set.Name}\": {ex.Message}";
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

                // Once the set is already running (Present Set, or presenting any one item directly), picking
                // a different item jumps straight to it live - no separate Present click per item while running
                // through a service. Checked fresh each time, not a sticky "mode", so it turns itself off the
                // moment nothing's live any more (Stop clears _activeItem - see the Output.IsBlank handler).
                if (IsAnyCurrentSetItemLive())
                    PresentSetItem(value);
            }
            else
            {
                Preview.IsBlank = true;
            }
            RefreshLiveState();
        }
    }

    /// <summary>True when the item currently live belongs to the set shown right now - i.e. the set is "running".</summary>
    private bool IsAnyCurrentSetItemLive()
        => !Output.IsBlank && _activeItem is not null && CurrentSetItems.Contains(_activeItem);

    private bool _isSetMode;
    public bool IsSetMode
    {
        get => _isSetMode;
        set
        {
            if (SetProperty(ref _isSetMode, value))
                RefreshLiveState();
        }
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
    /// <summary>True while the live item is an Image, i.e. <see cref="_activeSlides"/> holds file paths to show, not text.</summary>
    private bool _activeIsImage;
    private Verse? _liveVerse;
    // A single live verse too long for one screen is shown in parts; Next/Prev walk the parts before moving to the neighbouring verse.
    private List<string>? _liveVerseParts;
    private int _livePartIndex;

    private static List<string> VerseParts(Verse verse)
        => ScriptureSlideSplitter.SplitLongText(ReferenceFormatting.FormatVerseBody(verse));

    /// <summary>Index into <see cref="CurrentSetItems"/> of the Set item currently live, or null when the live slides came from a scripture search/song search instead of a Set. When set, running out of slides on Next/Prev rolls into the adjacent Set item instead of stopping.</summary>
    private int? _activeSetItemIndex;
    /// <summary>The Set or library item currently live, or null for a scripture passage. Kept so a changed song length can re-split it.</summary>
    private SetItem? _activeItem;

    private TextStyle _scriptureStyle = new();
    private TextStyle _songStyle = new();

    /// <summary>Which style is currently applied to the live output, so re-opening Settings and clicking OK can refresh it immediately.</summary>
    private SetItemType? _liveStyleType;

    /// <summary>
    /// The verse/song/item that was selected the last time Go Live/Present actually ran, so the button can
    /// grey out when the current selection is already on screen - nothing to click. Compared by reference
    /// (Verse, SetItem and SongSearchResult are all plain classes with no value equality), which is right
    /// for "is this literally the same row", and is left null once nothing is live (Stop, Escape, or
    /// closing the projector window), so any selection re-enables the button.
    /// </summary>
    private object? _lastPresentedSelection;

    public RelayCommand ImportBibleCommand { get; }
    public RelayCommand GoLiveCommand { get; }
    /// <summary>Closes the projector window outright (see MainWindow's handling of Output.IsBlank). Bound to the Stop button and the Escape key.</summary>
    public RelayCommand StopCommand { get; }
    /// <summary>Always presents the current selection, live or not - switching to a different item is one click, not Stop then Go Live.</summary>
    public RelayCommand GoLiveOrPresentCommand { get; }
    public RelayCommand NextVerseCommand { get; }
    public RelayCommand PrevVerseCommand { get; }
    /// <summary>Jumps straight to one slide card of whatever's live, instead of stepping Next/Prev one at a time. Parameter is the section's index.</summary>
    public RelayCommand JumpToSlideCommand { get; }
    public RelayCommand OpenSettingsCommand { get; }

    public RelayCommand ShowSearchModeCommand { get; }
    public RelayCommand ShowSetModeCommand { get; }
    public RelayCommand NewSetCommand { get; }
    public RelayCommand RenameSetCommand { get; }
    public RelayCommand SaveSetAsCommand { get; }
    public RelayCommand DeleteSetCommand { get; }
    public RelayCommand AddScriptureItemCommand { get; }
    public RelayCommand AddSongItemCommand { get; }
    public RelayCommand AddCustomItemCommand { get; }
    public RelayCommand AddImageItemCommand { get; }
    /// <summary>Takes the item to remove as its parameter and asks for confirmation first.</summary>
    public RelayCommand RemoveSetItemCommand { get; }
    public RelayCommand PresentSetItemCommand { get; }
    /// <summary>Starts the set live from its first item. After this (or presenting any one item directly), picking a different item in the list jumps straight to it until Stop.</summary>
    public RelayCommand PresentSetCommand { get; }
    public RelayCommand NewSongCommand { get; }
    public RelayCommand EditPreviewedSongCommand { get; }

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
        GoLiveCommand = new RelayCommand(GoLive, () => SelectedResult is not null || SelectedSongResult is not null);
        StopCommand = new RelayCommand(() => Output.IsBlank = true, () => !Output.IsBlank);
        GoLiveOrPresentCommand = new RelayCommand(GoLiveOrPresent, CanGoLiveOrPresent);
        // Nothing is live once the output goes blank - by Stop, Escape, or the operator closing the
        // projector window directly - so any selection (even the one just shown) should re-enable the button,
        // and what Next/Prev/a slide-card click would act on needs to be cleared too. Without this, those
        // controls could silently reopen the projector after an explicit Stop, since they'd still find a
        // "live" item to resume.
        Output.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(OutputViewModel.IsBlank) || !Output.IsBlank)
                return;

            _lastPresentedSelection = null;
            _activeSlides = null;
            _activeSlideIndex = 0;
            _activeIsImage = false;
            _activeSetItemIndex = null;
            _activeItem = null;
            _liveVerse = null;
            _liveVerseParts = null;
            _livePartIndex = 0;
            RefreshLiveState();
        };
        NextVerseCommand = new RelayCommand(() => StepSlide(1));
        PrevVerseCommand = new RelayCommand(() => StepSlide(-1));
        JumpToSlideCommand = new RelayCommand(JumpToSlide, CanJumpToSlide);
        OpenSettingsCommand = new RelayCommand(OpenSettings);

        ShowSearchModeCommand = new RelayCommand(() => IsSetMode = false);
        ShowSetModeCommand = new RelayCommand(() => IsSetMode = true);
        NewSetCommand = new RelayCommand(NewSet);
        RenameSetCommand = new RelayCommand(RenameSet, () => SelectedSet is not null);
        _autosaveTimer.Tick += (_, _) => FlushPendingSave();
        CurrentSetItems.CollectionChanged += OnSetItemsChanged;
        SaveSetAsCommand = new RelayCommand(SaveSetAs, () => SelectedSet is not null);
        DeleteSetCommand = new RelayCommand(DeleteSet, () => SelectedSet is not null);
        AddScriptureItemCommand = new RelayCommand(AddScriptureItem, () => SelectedSet is not null && Translations.Count > 0);
        AddSongItemCommand = new RelayCommand(AddSongItem, () => SelectedSet is not null);
        AddCustomItemCommand = new RelayCommand(AddCustomItem, () => SelectedSet is not null);
        AddImageItemCommand = new RelayCommand(AddImageItem, () => SelectedSet is not null);
        RemoveSetItemCommand = new RelayCommand(o => ConfirmRemoveSetItem(o as SetItem), o => o is SetItem);
        PresentSetItemCommand = new RelayCommand(() => PresentSetItem(SelectedSetItem), () => SelectedSetItem is not null);
        PresentSetCommand = new RelayCommand(PresentSet, () => SelectedSet is not null && CurrentSetItems.Count > 0);
        NewSongCommand = new RelayCommand(CreateSong);
        EditPreviewedSongCommand = new RelayCommand(EditPreviewedSong, () => PreviewedSong is not null);

        var settings = _settingsStore.Load();
        _scriptureStyle = settings.Scripture;
        ScriptureSlideSplitter.MaxSlideChars = _scriptureStyle.MaxSlideChars;
        _songStyle = settings.Song;

        foreach (var t in _translationStore.LoadAll())
            Translations.Add(t);

        if (Translations.Count == 0)
        {
            var bundled = TryImportBundledDefaultBible();
            if (bundled is not null)
                Translations.Add(bundled);
        }

        foreach (var t in Translations)
            TranslationPickerItems.Add(t);
        TranslationPickerItems.Add(ImportTranslationSentinel);

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
            TranslationPickerItems.Insert(TranslationPickerItems.Count - 1, translation);
            SelectedTranslation = translation;
            StatusMessage = $"Imported \"{translation.Name}\" ({translation.Verses.Count} verses).";
        }
        catch (BibleImportException ex)
        {
            MessageBox.Show(ex.Message, "Import failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show($"Couldn't save the imported Bible: {ex.Message}", "Import failed", MessageBoxButton.OK, MessageBoxImage.Warning);
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
        catch (Exception ex) when (ex is BibleImportException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Scripture's translation abbreviation, formatted to render right after the reference on the same line but
    /// at its own (usually smaller) size - e.g. "Genesis 4:8" + " (KJV)" - via OutlinedTextBlock.SecondaryText,
    /// rather than baking it into one string at one size.
    /// </summary>
    private static string TranslationSuffix(string? abbreviation) =>
        string.IsNullOrEmpty(abbreviation) ? "" : $" ({abbreviation})";

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
        _activeItem = null;
        _activeIsImage = false;
        Output.SlideImagePath = null;

        var abbreviation = SelectedTranslation?.Abbreviation ?? "";
        if (_lastSearchResult is { WasReferenceJump: true } result && result.Verses.Count > 1)
        {
            var passage = result.Verses.ToList();
            var index = passage.IndexOf(SelectedResult);
            var partsPerVerse = passage.Select(VerseParts).ToList();
            _activeSlides = partsPerVerse.SelectMany(p => p).ToList();
            _activeSlideIndex = index >= 0 ? partsPerVerse.Take(index).Sum(p => p.Count) : 0;
            _liveVerse = null;
            _liveVerseParts = null;
            Output.TopText = ReferenceFormatting.FormatRange(passage);
            Output.BodyText = _activeSlides[_activeSlideIndex];
        }
        else
        {
            _activeSlides = null;
            _liveVerse = SelectedResult;
            _liveVerseParts = VerseParts(SelectedResult);
            _livePartIndex = 0;
            Output.TopText = SelectedResult.Reference;
            Output.BodyText = _liveVerseParts[0];
        }

        Output.TopSecondaryText = TranslationSuffix(abbreviation);
        Output.BottomText = "";
        ApplyStyle(_scriptureStyle, SetItemType.Scripture);
        Output.IsBlank = false;
    }

    /// <summary>
    /// Go Live/Present always sends the current selection to the projector, live or not, so switching
    /// to a different item while something is already on screen is one click instead of Stop then
    /// Go Live again. Stop is its own button/Escape shortcut for actually ending the output.
    /// </summary>
    private bool CanGoLiveOrPresent()
    {
        var selected = CurrentSelection();
        return selected is not null && !ReferenceEquals(selected, _lastPresentedSelection);
    }

    private void GoLiveOrPresent()
    {
        _lastPresentedSelection = CurrentSelection();

        if (IsSetMode)
            PresentSetItem(SelectedSetItem);
        else
            GoLive();
    }

    /// <summary>Whatever's selected in whichever list is showing right now - the one thing Go Live/Present would act on.</summary>
    private object? CurrentSelection()
        => IsSetMode ? SelectedSetItem : (object?)SelectedResult ?? SelectedSongResult;

    /// <summary>Go Live from a song search result: resolves the full song and presents it exactly like a Set song item.</summary>
    private void GoLiveSong()
    {
        if (SelectedSongResult is null)
            return;

        var item = _songLibrary.ResolveByTitle(SelectedSongResult.Title);
        if (item is not null)
            PresentSetItem(item);
    }

    /// <summary>
    /// Doubles as the set's one start/end control: starts it live from its first item when idle - "play from
    /// the top", regardless of what's currently selected - and ends it (same as Stop) once running, so there's
    /// one obvious button for the whole service instead of hunting for a separate Stop.
    /// </summary>
    private void PresentSet()
    {
        if (IsSetRunning)
        {
            Output.IsBlank = true;
            return;
        }

        var first = CurrentSetItems.FirstOrDefault();
        if (first is null)
            return;

        SelectedSetItem = first;
        PresentSetItem(first);
    }

    private void PresentSetItem(SetItem? item)
    {
        if (item is null)
            return;

        _songLibrary.EnsureResolved(item);
        if (item.Slides.Count == 0)
            return;

        _activeItem = item;
        _activeSlides = PresentationSlides(item);
        _activeSlideIndex = 0;
        _liveVerse = null;
        _liveVerseParts = null;
        _activeIsImage = item.Type == SetItemType.Image;
        var indexInSet = CurrentSetItems.IndexOf(item);
        _activeSetItemIndex = indexInSet >= 0 ? indexInSet : null;

        if (_activeIsImage)
        {
            // A picture (e.g. an event flyer) fills the screen on its own - no title/verse text over it.
            Output.TopText = "";
            Output.TopSecondaryText = "";
            Output.BodyText = "";
            Output.BottomText = "";
            Output.SlideImagePath = item.Slides[0];
            Output.IsBlank = false;
            RefreshLiveState();
            return;
        }

        Output.SlideImagePath = null;
        if (item.Type == SetItemType.Scripture)
        {
            Output.TopText = item.Title;
            Output.TopSecondaryText = TranslationSuffix(item.Subtitle);
            Output.BottomText = "";
        }
        else
        {
            Output.TopText = item.Title;
            Output.TopSecondaryText = "";
            Output.BottomText = item.Subtitle;
        }
        Output.BodyText = _activeSlides[0];
        ApplyStyle(item.Type == SetItemType.Scripture ? _scriptureStyle : _songStyle, item.Type == SetItemType.Scripture ? SetItemType.Scripture : SetItemType.Song);
        Output.IsBlank = false;
        RefreshLiveState();
    }

    /// <summary>
    /// The slides a Set or library item shows on the projector. Song and Custom slides longer than the
    /// Song limit are split here, at presentation time, so the song file itself is never changed. Scripture
    /// was already split when it was loaded, and an Image is one picture.
    /// </summary>
    private List<string> PresentationSlides(SetItem item)
    {
        if (item.Type is SetItemType.Scripture or SetItemType.Image)
            return item.Slides;

        var limit = Math.Clamp(_songStyle.MaxSlideChars, ScriptureSlideSplitter.MinAllowedSlideChars, ScriptureSlideSplitter.MaxAllowedSlideChars);
        return item.Slides
            .SelectMany(slide => ScriptureSlideSplitter.SplitLongText(slide, limit))
            .ToList();
    }

    /// <summary>
    /// Where each of <paramref name="item"/>'s sections (its Slides/SlideLabels - what the slide cards show)
    /// begins within <see cref="PresentationSlides"/>'s output (what Next/Prev and the projector actually step
    /// through). A section longer than the slide-length limit becomes more than one projector slide, so a
    /// later section's start index runs ahead of its own position once any splitting has happened.
    /// </summary>
    private List<int> PresentationSlideStartIndices(SetItem item)
    {
        if (item.Type is SetItemType.Scripture or SetItemType.Image)
            return Enumerable.Range(0, item.Slides.Count).ToList();

        var limit = Math.Clamp(_songStyle.MaxSlideChars, ScriptureSlideSplitter.MinAllowedSlideChars, ScriptureSlideSplitter.MaxAllowedSlideChars);
        var starts = new List<int>();
        var running = 0;
        foreach (var slide in item.Slides)
        {
            starts.Add(running);
            running += ScriptureSlideSplitter.SplitLongText(slide, limit).Count;
        }
        return starts;
    }

    /// <summary>
    /// The item whose slides are showing in the visible detail/preview panel right now - a Set item's detail
    /// pane in Set mode, or a song's lyrics preview in Song search - or null when neither applies (Scripture
    /// search has no slide-card list to click).
    /// </summary>
    private SetItem? CurrentlyDisplayedItem()
        => IsSetMode ? SelectedSetItem : (IsSongSearch ? PreviewedSong : null);

    /// <summary>
    /// True when <paramref name="item"/> is literally what's live right now. Set items are compared by
    /// reference - the same SetItem instance flows from selection through to presenting - but a song's
    /// preview and its live copy are two separate instances resolved from the same file at different times,
    /// so songs fall back to comparing the library file itself.
    /// </summary>
    private bool IsLive(SetItem? item)
    {
        if (item is null || _activeItem is null)
            return false;
        if (ReferenceEquals(item, _activeItem))
            return true;
        return item.Type == SetItemType.Song && item.LibraryPath is not null && item.LibraryPath == _activeItem.LibraryPath;
    }

    private bool CanJumpToSlide(object? parameter)
    {
        var item = CurrentlyDisplayedItem();
        return item is not null && IsLive(item);
    }

    /// <summary>
    /// Jumps straight to one section of whatever's already live - e.g. slide 28 of a 30-slide song in one
    /// click - instead of stepping through Next/Prev one at a time. Only acts when the clicked card belongs
    /// to the item that's actually on screen, so browsing a different song or item never changes what's live;
    /// clicking it then is a no-op (the cards are also visibly dimmed/disabled for anything not live).
    /// </summary>
    private void JumpToSlide(object? parameter)
    {
        var item = CurrentlyDisplayedItem();
        if (item is null || !IsLive(item) || _activeSlides is null || parameter is not int sectionIndex)
            return;

        var starts = PresentationSlideStartIndices(item);
        if (sectionIndex < 0 || sectionIndex >= starts.Count)
            return;

        _activeSlideIndex = Math.Min(starts[sectionIndex], _activeSlides.Count - 1);
        ShowSlide(_activeSlides[_activeSlideIndex]);
        Output.IsBlank = false;
        RefreshLiveState();
    }

    private int _liveSectionIndex = -1;
    /// <summary>
    /// Which slide card of the currently-displayed item (PreviewedSong or SelectedSetItem) is live right now,
    /// by its section index - or -1 when that item isn't the one live, or nothing is live at all. Drives the
    /// "you are here" highlight on the slide cards. Kept in sync wherever the live slide, the live item, or
    /// which item is being displayed can change - see every call site of this method.
    /// </summary>
    public int LiveSectionIndex
    {
        get => _liveSectionIndex;
        private set => SetProperty(ref _liveSectionIndex, value);
    }

    private bool _isSetRunning;
    /// <summary>
    /// True once the set is "running" - something in it is live, so picking any item in the list presents it
    /// immediately (see SelectedSetItem's setter). While this is true, Go Live/Present/Show Item would always
    /// be disabled anyway (the selection always already matches what's live), so the main window hides that
    /// button entirely instead of showing a perpetually-greyed one - clicking a list item is the only action
    /// needed, and a visible-but-dead button would read as "you still need to click this too."
    /// </summary>
    public bool IsSetRunning
    {
        get => _isSetRunning;
        private set => SetProperty(ref _isSetRunning, value);
    }

    private void RefreshLiveState()
    {
        var item = CurrentlyDisplayedItem();
        if (item is null || !IsLive(item) || _activeSlides is null)
        {
            LiveSectionIndex = -1;
        }
        else
        {
            var starts = PresentationSlideStartIndices(item);
            var section = -1;
            for (var i = 0; i < starts.Count; i++)
            {
                if (starts[i] > _activeSlideIndex)
                    break;
                section = i;
            }
            LiveSectionIndex = section;
        }

        IsSetRunning = IsSetMode && IsAnyCurrentSetItemLive();
    }

    /// <summary>Pushes a full style (font/sizes/weight/colors/border/shadow/background) into the output window.</summary>
    private void ApplyStyle(TextStyle style, SetItemType type)
    {
        _liveStyleType = type;
        ApplyStyleTo(Output, style, type);
    }

    private void ApplyStyleTo(OutputViewModel target, TextStyle style, SetItemType type)
    {
        target.BodyAlignment = type == SetItemType.Scripture
            ? System.Windows.TextAlignment.Left
            : System.Windows.TextAlignment.Center;
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
        target.BorderSize = style.BorderSize;
        target.ShadowEnabled = style.ShadowEnabled;
        target.ShadowColor = style.ShadowColor;
        target.ShadowDistance = style.ShadowDistance;
        target.ShadowAngle = style.ShadowAngle;
        target.ShadowThickness = style.ShadowThickness;
        target.CurrentBackground = Backgrounds.FirstOrDefault(b => b.FilePath == style.BackgroundPath);
    }

    /// <summary>Updates the mini preview pane to show a single verse (e.g. a search result under the cursor) without touching the live output.</summary>
    private void UpdatePreviewForVerse(Verse verse)
    {
        Preview.SlideImagePath = null;
        Preview.TopText = verse.Reference;
        Preview.TopSecondaryText = TranslationSuffix(SelectedTranslation?.Abbreviation ?? "");
        Preview.BodyText = VerseParts(verse)[0];
        Preview.BottomText = "";
        ApplyStyleTo(Preview, _scriptureStyle, SetItemType.Scripture);
        Preview.IsBlank = false;
    }

    /// <summary>Updates the mini preview pane to show a Set item's (or resolved song's) first slide without touching the live output.</summary>
    private void UpdatePreviewForSetItem(SetItem item)
    {
        _songLibrary.EnsureResolved(item);

        if (item.Type == SetItemType.Image)
        {
            Preview.TopText = "";
            Preview.TopSecondaryText = "";
            Preview.BodyText = "";
            Preview.BottomText = "";
            Preview.SlideImagePath = item.Slides.Count > 0 ? item.Slides[0] : null;
            Preview.IsBlank = false;
            return;
        }

        Preview.SlideImagePath = null;
        if (item.Type == SetItemType.Scripture)
        {
            Preview.TopText = item.Title;
            Preview.TopSecondaryText = TranslationSuffix(item.Subtitle);
            Preview.BottomText = "";
        }
        else
        {
            Preview.TopText = item.Title;
            Preview.TopSecondaryText = "";
            Preview.BottomText = item.Subtitle;
        }
        var previewSlides = PresentationSlides(item);
        Preview.BodyText = previewSlides.Count > 0 ? previewSlides[0] : "";
        ApplyStyleTo(Preview, item.Type == SetItemType.Scripture ? _scriptureStyle : _songStyle, item.Type);
        Preview.IsBlank = false;
    }

    /// <summary>Puts one slide on the live output and the mini preview: a picture for Image items, text for everything else.</summary>
    private void ShowSlide(string slide)
    {
        if (_activeIsImage)
        {
            Output.SlideImagePath = slide;
            Preview.SlideImagePath = slide;
            return;
        }

        Output.BodyText = slide;
        Preview.BodyText = slide;
    }

    private void StepSlide(int direction)
    {
        if (_activeSlides is not null)
        {
            var next = _activeSlideIndex + direction;
            if (next >= 0 && next < _activeSlides.Count)
            {
                _activeSlideIndex = next;
                ShowSlide(_activeSlides[next]);
                Output.IsBlank = false;
                RefreshLiveState();
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
                        ShowSlide(_activeSlides[_activeSlideIndex]);
                        RefreshLiveState();
                    }
                }
            }
            return;
        }

        if (_liveVerse is null || SelectedTranslation is null)
            return;

        if (_liveVerseParts is not null)
        {
            var nextPart = _livePartIndex + direction;
            if (nextPart >= 0 && nextPart < _liveVerseParts.Count)
            {
                _livePartIndex = nextPart;
                Output.BodyText = _liveVerseParts[nextPart];
                Output.IsBlank = false;
                Preview.BodyText = Output.BodyText;
                return;
            }
        }

        var verses = SelectedTranslation.Verses;
        var index = verses.FindIndex(v => v.BookIndex == _liveVerse.BookIndex && v.Chapter == _liveVerse.Chapter && v.Number == _liveVerse.Number);
        var nextIndex = index + direction;
        if (index < 0 || nextIndex < 0 || nextIndex >= verses.Count)
            return;

        _liveVerse = verses[nextIndex];
        _liveVerseParts = VerseParts(_liveVerse);
        _livePartIndex = direction < 0 ? _liveVerseParts.Count - 1 : 0;
        Output.TopText = _liveVerse.Reference;
        Output.TopSecondaryText = TranslationSuffix(SelectedTranslation.Abbreviation);
        Output.BodyText = _liveVerseParts[_livePartIndex];
        Output.BottomText = "";
        Output.IsBlank = false;

        Preview.TopText = Output.TopText;
        Preview.TopSecondaryText = Output.TopSecondaryText;
        Preview.BodyText = Output.BodyText;
        Preview.BottomText = Output.BottomText;
    }

    private void NewSet()
    {
        var dialog = new Views.PromptDialog("New Set", "Set name:", "New Set") { Owner = System.Windows.Application.Current.MainWindow };
        if (dialog.ShowDialog() != true)
            return;

        var set = new PresentationSet { Name = dialog.ResultText };
        try
        {
            _setStore.Save(set);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusMessage = $"Couldn't create \"{set.Name}\": {ex.Message}";
            return;
        }
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
        _setPendingSave = SelectedSet;
        FlushPendingSave();
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
        try
        {
            _setStore.Save(copy);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusMessage = $"Couldn't save \"{copy.Name}\": {ex.Message}";
            return;
        }
        Sets.Add(copy);
        SelectedSet = copy;
    }

    private static SetItem CloneItem(SetItem item) => item.Clone();

    private void DeleteSet()
    {
        if (SelectedSet is null)
            return;

        // Nothing left to autosave for a set that is about to be deleted.
        _autosaveTimer.Stop();
        _setPendingSave = null;

        _setStore.Delete(SelectedSet);
        Sets.Remove(SelectedSet);
        SelectedSet = Sets.FirstOrDefault();
    }

    private void AddScriptureItem()
    {
        if (SelectedSet is null || Translations.Count == 0)
            return;

        var defaultTranslation = SelectedTranslation ?? Translations[0];
        var dialog = new Views.AddScriptureDialog(Translations, defaultTranslation) { Owner = System.Windows.Application.Current.MainWindow };
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

    // ---- Songs: create, and edit with three save choices ----

    private void CreateSong()
    {
        var dialog = new Views.SongEditorDialog(SongEditOrigin.NewSong, new SetItem { Type = SetItemType.Song })
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        if (dialog.ShowDialog() != true || dialog.Result is not { } result)
            return;

        var path = _songLibrary.SaveSong(result.Song, null);
        ShowSavedSong(result.Song.Title, path);
    }

    private void EditPreviewedSong()
    {
        if (PreviewedSong is not null)
            EditSong(SongEditOrigin.LibrarySearch, PreviewedSong, null);
    }

    /// <summary>Opens the song editor for a song in the current set: either a library reference or a copy kept for this set only.</summary>
    public void EditSetSong(SetItem item)
    {
        if (item.Type != SetItemType.Song)
            return;

        _songLibrary.EnsureResolved(item);
        var origin = item.IsSetOnly ? SongEditOrigin.SetLocalCopy : SongEditOrigin.SetLibraryReference;
        EditSong(origin, item, item);
    }

    private void EditSong(SongEditOrigin origin, SetItem song, SetItem? setItem)
    {
        var dialog = new Views.SongEditorDialog(origin, song)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        if (dialog.ShowDialog() != true || dialog.Result is not { } result)
            return;

        ApplySongEdit(song, setItem, result);
    }

    private void ApplySongEdit(SetItem original, SetItem? setItem, SongEditResult result)
    {
        var edited = result.Song;
        string? path = null;

        if (result.Choice == SongEditChoice.Overwrite
            && _songLibrary.WouldRewriteChordedLyrics(edited, original.LibraryPath))
        {
            var confirm = new Views.ConfirmDialog(
                "Change the lyrics?",
                $"The file for \"{edited.Title}\" has chord rows and verse numbers that this app doesn't keep.",
                "Changing its lyrics rewrites the file without them, so OpenSong would show the plain lyrics. Title and author changes never remove anything.",
                "Save and drop chords")
            {
                Owner = System.Windows.Application.Current.MainWindow
            };
            if (confirm.ShowDialog() != true)
                return;
        }

        switch (result.Choice)
        {
            case SongEditChoice.Overwrite:
                path = _songLibrary.SaveSong(edited, original.LibraryPath);
                RefreshSongReferences(original.Title, edited.Title, except: setItem);
                break;

            case SongEditChoice.SaveAsNew:
                path = _songLibrary.SaveSong(edited, null);
                break;
        }

        if (setItem is not null)
        {
            setItem.Title = edited.Title;
            setItem.Subtitle = edited.Subtitle;
            setItem.Slides = new List<string>(edited.Slides);
            setItem.SlideLabels = new List<string>(edited.SlideLabels ?? new List<string>());
            setItem.LibraryPath = path;
            setItem.IsSetOnly = result.Choice == SongEditChoice.SetOnly;
            MarkSetDirty();
        }

        if (path is null)
            StatusMessage = $"Saved \"{edited.Title}\" for this set only.";
        else
            ShowSavedSong(edited.Title, path, asNew: result.Choice == SongEditChoice.SaveAsNew);
    }

    /// <summary>
    /// Sets refer to a library song by title, so after a song changes every set item for it - in every
    /// set - picks up the new title and re-reads its words the next time it is shown. Sets that changed are saved.
    /// </summary>
    private void RefreshSongReferences(string oldTitle, string newTitle, SetItem? except)
    {
        foreach (var set in Sets)
        {
            IEnumerable<SetItem> items = set == SelectedSet ? CurrentSetItems : set.Items;
            var changed = false;

            foreach (var item in items)
            {
                if (item == except || item.Type != SetItemType.Song || item.IsSetOnly)
                    continue;
                if (!string.Equals(item.Title, oldTitle, StringComparison.Ordinal)
                    && !string.Equals(item.Title, newTitle, StringComparison.Ordinal))
                    continue;

                item.Title = newTitle;
                item.Slides = new List<string>();
                item.SlideLabels = null;
                changed = true;
            }

            if (!changed)
                continue;

            if (set == SelectedSet)
            {
                MarkSetDirty();
                continue;
            }

            try
            {
                _setStore.Save(set);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                StatusMessage = $"Couldn't update \"{set.Name}\": {ex.Message}";
            }
        }
    }

    /// <summary>After a library save, show that song in the Songs search so it can be seen and previewed right away.</summary>
    private void ShowSavedSong(string title, string path, bool asNew = false)
    {
        StatusMessage = asNew
            ? $"Saved \"{title}\" as a new song in your library."
            : $"Saved \"{title}\" to your song library.";

        if (!IsSongSearch || IsSetMode)
            return;

        SearchText = title;
        RunSearch();
        SelectedSongResult = SongResults.FirstOrDefault(r => r.FilePath == path);
    }

    private void AddCustomItem()
    {
        if (SelectedSet is null)
            return;

        var item = new SetItem { Type = SetItemType.Custom, Title = "New Item", Slides = new List<string> { "" } };
        CurrentSetItems.Add(item);
        SelectedSetItem = item;
    }

    /// <summary>Adds each chosen picture (an event flyer, a photo) as its own Image item; it fills the screen when presented.</summary>
    private void AddImageItem()
    {
        if (SelectedSet is null)
            return;

        var dialog = new OpenFileDialog
        {
            Title = "Add Image",
            Filter = "Images (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif",
            Multiselect = true
        };
        if (dialog.ShowDialog() != true)
            return;

        SetItem? last = null;
        foreach (var file in dialog.FileNames)
        {
            try
            {
                var storedPath = _setStore.StoreImage(file);
                last = new SetItem
                {
                    Type = SetItemType.Image,
                    Title = Path.GetFileNameWithoutExtension(file),
                    Slides = new List<string> { storedPath }
                };
                CurrentSetItems.Add(last);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                MessageBox.Show($"Couldn't add \"{Path.GetFileName(file)}\": {ex.Message}", "Add Image", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        if (last is not null)
            SelectedSetItem = last;
    }

    private void ConfirmRemoveSetItem(SetItem? item)
    {
        if (item is null)
            return;

        var dialog = new Views.ConfirmDialog(
            "Remove item",
            $"Remove \"{item.Title}\" from this set?",
            "It leaves the set list now, but the saved set file only changes when you save the set.",
            "Remove")
        {
            Owner = System.Windows.Application.Current.MainWindow
        };

        if (dialog.ShowDialog() == true)
            RemoveSetItem(item);
    }

    private void RemoveSetItem(SetItem item)
    {
        var index = CurrentSetItems.IndexOf(item);
        if (index < 0)
            return;

        CurrentSetItems.RemoveAt(index);

        // Keep "roll into the next item" pointing at the same live item after the list shifts.
        if (_activeSetItemIndex is int live)
            _activeSetItemIndex = SetReorder.IndexAfterRemove(live, index);

        if (SelectedSetItem == item)
            SelectedSetItem = CurrentSetItems.FirstOrDefault();
    }

    /// <summary>Drag-and-drop reorder: puts <paramref name="item"/> where an insertion point at <paramref name="insertIndex"/> (0..Count, in the list as it is now) would land it.</summary>
    public void MoveSetItemTo(SetItem item, int insertIndex)
    {
        var oldIndex = CurrentSetItems.IndexOf(item);
        if (oldIndex < 0)
            return;

        var newIndex = SetReorder.DestinationIndex(oldIndex, insertIndex, CurrentSetItems.Count);
        if (newIndex == oldIndex)
            return;

        CurrentSetItems.Move(oldIndex, newIndex);

        // Keep "roll into the next item" pointing at the same live item after the list shifts.
        if (_activeSetItemIndex is int live)
            _activeSetItemIndex = SetReorder.IndexAfterMove(live, oldIndex, newIndex);

        SelectedSetItem = item;
    }

    /// <summary>After the verse-length setting changes, re-cut every scripture item already in a loaded set to the new length (saved only when the set is).</summary>
    private void ResplitScriptureItems()
    {
        var items = Sets.SelectMany(s => s.Items).Concat(CurrentSetItems).Distinct()
            .Where(i => i.Type == SetItemType.Scripture);
        foreach (var item in items)
            item.Slides = ScriptureSlideSplitter.Resplit(item.Slides);
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
        try
        {
            _settingsStore.Save(new PresentationSettings { Scripture = _scriptureStyle, Song = _songStyle });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusMessage = $"Settings applied, but couldn't be saved: {ex.Message}";
        }

        if (dialog.ResultScreen is not null)
            SelectedScreen = dialog.ResultScreen;

        var previousLimit = ScriptureSlideSplitter.MaxSlideChars;
        ScriptureSlideSplitter.MaxSlideChars = _scriptureStyle.MaxSlideChars;
        if (ScriptureSlideSplitter.MaxSlideChars != previousLimit)
            ResplitScriptureItems();

        // A live song or Custom slide is split again so a new length shows at once; the operator stays on the same part number.
        if (_activeItem is { Type: not SetItemType.Scripture } live && !_activeIsImage && _activeSlides is not null)
        {
            _activeSlides = PresentationSlides(live);
            _activeSlideIndex = Math.Min(_activeSlideIndex, _activeSlides.Count - 1);
            ShowSlide(_activeSlides[_activeSlideIndex]);
        }

        // If something is already live, refresh it immediately so a mid-service style edit shows right away.
        if (_liveStyleType is { } type)
            ApplyStyle(type == SetItemType.Scripture ? _scriptureStyle : _songStyle, type);
    }
}
