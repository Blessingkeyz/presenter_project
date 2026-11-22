using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using BiblePresenter.App.Models;
using BiblePresenter.App.Services;

namespace BiblePresenter.App.Views;

public partial class AddScriptureDialog : Window
{
    private readonly SearchIndexService _searchIndex = new();
    private Translation _translation;
    private SearchResult? _lastResult;
    private readonly List<Verse> _browseSelectedVerses = new();
    private int? _verseDragAnchor;
    private bool _isDragSelectingVerses;

    public SetItem? Result { get; private set; }

    public AddScriptureDialog(IEnumerable<Translation> translations, Translation selected)
    {
        InitializeComponent();
        _translation = selected;
        TranslationBox.ItemsSource = translations.ToList();
        TranslationBox.SelectedItem = selected;
        _searchIndex.Load(_translation);
        Title = $"Add Scripture ({_translation.Abbreviation})";

        BrowseBookList.ItemsSource = BookTable.Instance.Books;
    }

    private void TranslationBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (TranslationBox.SelectedItem is not Translation translation)
            return;

        _translation = translation;
        _searchIndex.Load(_translation);
        Title = $"Add Scripture ({_translation.Abbreviation})";
        SearchBox_TextChanged(this, null);

        // The loaded translation might not have the same chapters/verses as the last one - start over.
        BrowseBookList.SelectedItem = null;
    }

    private void FindByMode_Changed(object sender, RoutedEventArgs e)
    {
        if (SearchPanel is null || BrowsePanel is null)
            return; // fires once during InitializeComponent, before the named elements exist yet

        var browsing = BrowseModeRadio.IsChecked == true;
        SearchPanel.Visibility = browsing ? Visibility.Collapsed : Visibility.Visible;
        ResultsList.Visibility = browsing ? Visibility.Collapsed : Visibility.Visible;
        BrowsePanel.Visibility = browsing ? Visibility.Visible : Visibility.Collapsed;

        if (browsing)
        {
            SearchBox.Text = "";
        }
        else
        {
            BrowseBookList.SelectedItem = null;
            _browseSelectedVerses.Clear();
        }
        AddButton.IsEnabled = false;
    }

    private void BrowseBookList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        _browseSelectedVerses.Clear();
        AddButton.IsEnabled = false;
        BrowseVerseList.ItemsSource = null;
        VerseHint.Visibility = Visibility.Visible;

        if (BrowseBookList.SelectedItem is not Book book)
        {
            BrowseChapterList.ItemsSource = null;
            ChapterHint.Visibility = Visibility.Visible;
            return;
        }

        ChapterHint.Visibility = Visibility.Collapsed;
        BrowseChapterList.ItemsSource = _translation.Verses
            .Where(v => v.BookIndex == book.Index)
            .Select(v => v.Chapter)
            .Distinct()
            .OrderBy(c => c)
            .ToList();
    }

    private void BrowseChapterList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        _browseSelectedVerses.Clear();
        AddButton.IsEnabled = false;

        if (BrowseBookList.SelectedItem is not Book book || BrowseChapterList.SelectedItem is not int chapter)
        {
            BrowseVerseList.ItemsSource = null;
            VerseHint.Visibility = Visibility.Visible;
            return;
        }

        VerseHint.Visibility = Visibility.Collapsed;
        BrowseVerseList.ItemsSource = _translation.Verses
            .Where(v => v.BookIndex == book.Index && v.Chapter == chapter)
            .Select(v => v.Number)
            .OrderBy(n => n)
            .ToList();
    }

    private void BrowseVerseList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        _browseSelectedVerses.Clear();
        AddButton.IsEnabled = false;

        if (BrowseBookList.SelectedItem is not Book book || BrowseChapterList.SelectedItem is not int chapter)
            return;

        var selectedNumbers = BrowseVerseList.SelectedItems.Cast<int>().ToList();
        if (selectedNumbers.Count == 0)
            return;

        _browseSelectedVerses.AddRange(_translation.Verses
            .Where(v => v.BookIndex == book.Index && v.Chapter == chapter && selectedNumbers.Contains(v.Number))
            .OrderBy(v => v.Number));
        AddButton.IsEnabled = _browseSelectedVerses.Count > 0;
    }

    // Click a verse tile and drag to select a contiguous range. Capture only kicks in once the pointer
    // actually leaves the anchor tile - grabbing it on mouse-down unconditionally hijacked the ListBox's
    // own click/double-click handling, breaking plain single-verse selection.
    private void BrowseVerseList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (FindAncestorListBoxItem(e.OriginalSource as DependencyObject)?.DataContext is not int anchor)
            return;

        _verseDragAnchor = anchor;
    }

    private void BrowseVerseList_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _verseDragAnchor is not int anchor)
            return;

        if (FindAncestorListBoxItem(e.OriginalSource as DependencyObject)?.DataContext is not int current)
            return;

        if (!_isDragSelectingVerses)
        {
            if (current == anchor)
                return; // still on the tile that was clicked - not a drag yet

            _isDragSelectingVerses = true;
            BrowseVerseList.CaptureMouse();
        }

        var low = Math.Min(anchor, current);
        var high = Math.Max(anchor, current);
        BrowseVerseList.SelectedItems.Clear();
        foreach (var number in BrowseVerseList.Items.Cast<int>().Where(n => n >= low && n <= high))
            BrowseVerseList.SelectedItems.Add(number);
    }

    private void BrowseVerseList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _verseDragAnchor = null;
        if (!_isDragSelectingVerses)
            return;

        _isDragSelectingVerses = false;
        BrowseVerseList.ReleaseMouseCapture();
    }

    private static ListBoxItem? FindAncestorListBoxItem(DependencyObject? source)
    {
        while (source is not null and not ListBoxItem)
            source = VisualTreeHelper.GetParent(source);
        return source as ListBoxItem;
    }

    private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs? e)
    {
        _lastResult = null;
        ResultsList.ItemsSource = null;
        AddButton.IsEnabled = false;

        if (string.IsNullOrWhiteSpace(SearchBox.Text))
            return;

        _lastResult = _searchIndex.Search(SearchBox.Text);
        ResultsList.ItemsSource = _lastResult.Verses;
    }

    private void ResultsList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        AddButton.IsEnabled = ResultsList.SelectedItem is not null;
    }

    private void Add_Click(object sender, RoutedEventArgs e) => TryAddAndClose();

    private void ResultsList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => TryAddAndClose();

    private void TryAddAndClose()
    {
        if (_lastResult is { WasReferenceJump: true } result && result.Verses.Count > 1)
        {
            var passage = result.Verses;
            Result = new SetItem
            {
                Type = SetItemType.Scripture,
                Title = ReferenceFormatting.FormatRange(passage),
                Subtitle = _translation.Abbreviation,
                Slides = passage.SelectMany(v => ScriptureSlideSplitter.SplitLongText(ReferenceFormatting.FormatVerseBody(v))).ToList()
            };
        }
        else if (_browseSelectedVerses.Count > 1)
        {
            var passage = _browseSelectedVerses;
            Result = new SetItem
            {
                Type = SetItemType.Scripture,
                Title = ReferenceFormatting.FormatRange(passage),
                Subtitle = _translation.Abbreviation,
                Slides = passage.SelectMany(v => ScriptureSlideSplitter.SplitLongText(ReferenceFormatting.FormatVerseBody(v))).ToList()
            };
        }
        else if ((ResultsList.SelectedItem as Verse ?? _browseSelectedVerses.FirstOrDefault()) is { } verse)
        {
            Result = new SetItem
            {
                Type = SetItemType.Scripture,
                Title = verse.Reference,
                Subtitle = _translation.Abbreviation,
                Slides = ScriptureSlideSplitter.SplitLongText(ReferenceFormatting.FormatVerseBody(verse))
            };
        }
        else
        {
            return;
        }

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
