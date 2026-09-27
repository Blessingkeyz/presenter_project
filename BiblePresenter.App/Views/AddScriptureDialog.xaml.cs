using System.Windows;
using System.Windows.Input;
using BiblePresenter.App.Models;
using BiblePresenter.App.Services;

namespace BiblePresenter.App.Views;

public partial class AddScriptureDialog : Window
{
    private readonly Translation _translation;
    private readonly SearchIndexService _searchIndex = new();
    private SearchResult? _lastResult;

    public SetItem? Result { get; private set; }

    public AddScriptureDialog(Translation translation)
    {
        InitializeComponent();
        _translation = translation;
        _searchIndex.Load(translation);
        Title = $"Add Scripture ({translation.Abbreviation})";
    }

    private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
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
                Slides = passage.Select(ReferenceFormatting.FormatVerseBody).ToList()
            };
        }
        else if (ResultsList.SelectedItem is Verse verse)
        {
            Result = new SetItem
            {
                Type = SetItemType.Scripture,
                Title = verse.Reference,
                Subtitle = _translation.Abbreviation,
                Slides = new List<string> { ReferenceFormatting.FormatVerseBody(verse) }
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
