using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using BiblePresenter.App.Models;
using BiblePresenter.App.ViewModels;
using DataObject = System.Windows.DataObject;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;

namespace BiblePresenter.App.Views;

/// <summary>
/// Creates or edits one song: title, author and its sections (V1, C, B...). Which save buttons
/// appear depends on where it was opened from - see <see cref="SongEditOrigin"/>.
/// </summary>
public partial class SongEditorDialog : Window
{
    private readonly ObservableCollection<SongSection> _sections = new();
    private readonly SongEditOrigin _origin;
    private SongSection? _pendingDrag;
    private Point _dragStart;

    public SongEditResult? Result { get; private set; }

    public SongEditorDialog(SongEditOrigin origin, SetItem song)
    {
        InitializeComponent();
        _origin = origin;
        Title = origin == SongEditOrigin.NewSong ? "New song" : "Edit song";

        TitleBox.Text = song.Title;
        AuthorBox.Text = song.Subtitle;
        for (var i = 0; i < song.Slides.Count; i++)
            _sections.Add(new SongSection(song.LabelAt(i), song.Slides[i]));
        SectionsList.ItemsSource = _sections;

        if (origin == SongEditOrigin.NewSong && _sections.Count == 0)
            AddSection(NextLabel("V", numberFirst: true));

        TitleBox.TextChanged += (_, _) => UpdateSaveEnabled();
        ConfigureButtons();
        UpdateSaveEnabled();
        Loaded += (_, _) => TitleBox.Focus();
    }

    private void ConfigureButtons()
    {
        var canOverwrite = _origin is SongEditOrigin.LibrarySearch or SongEditOrigin.SetLibraryReference;
        var canSetOnly = _origin is SongEditOrigin.SetLibraryReference or SongEditOrigin.SetLocalCopy;

        OverwriteButton.Visibility = canOverwrite ? Visibility.Visible : Visibility.Collapsed;
        SetOnlyButton.Visibility = canSetOnly ? Visibility.Visible : Visibility.Collapsed;
        SaveAsNewButton.Content = _origin == SongEditOrigin.NewSong ? "Save to library" : "Save as new song";

        var primary = canOverwrite ? OverwriteButton : canSetOnly ? SetOnlyButton : SaveAsNewButton;
        primary.Style = (Style)FindResource("AccentButton");

        Hint.Text = _origin switch
        {
            SongEditOrigin.NewSong => "The song is saved as a file in your Songs folder, where OpenSong reads it too.",
            SongEditOrigin.LibrarySearch => "Save changes the song's file in your Songs folder, so OpenSong and every set see the change.",
            SongEditOrigin.SetLibraryReference => "Save changes the song everywhere. Save for this set only keeps the change to this set.",
            _ => "This set has its own copy of the song. Save as new song adds it to your library as well."
        };
    }

    private void UpdateSaveEnabled()
    {
        var titled = !string.IsNullOrWhiteSpace(TitleBox.Text);
        foreach (var button in new[] { OverwriteButton, SetOnlyButton, SaveAsNewButton })
            button.IsEnabled = titled;
    }

    private void Overwrite_Click(object sender, RoutedEventArgs e) => Choose(SongEditChoice.Overwrite);

    private void SaveAsNew_Click(object sender, RoutedEventArgs e) => Choose(SongEditChoice.SaveAsNew);

    private void SetOnly_Click(object sender, RoutedEventArgs e) => Choose(SongEditChoice.SetOnly);

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Choose(SongEditChoice choice)
    {
        Result = new SongEditResult(choice, BuildSong());
        DialogResult = true;
    }

    /// <summary>Sections left empty are dropped: a blank slide has nothing to project.</summary>
    private SetItem BuildSong()
    {
        var kept = _sections.Where(s => !string.IsNullOrWhiteSpace(s.Text)).ToList();
        return new SetItem
        {
            Type = SetItemType.Song,
            Title = TitleBox.Text.Trim(),
            Subtitle = AuthorBox.Text.Trim(),
            Slides = kept.Select(s => s.Text.Trim()).ToList(),
            SlideLabels = kept.Select(s => s.Label.Trim()).ToList()
        };
    }

    private void AddVerse_Click(object sender, RoutedEventArgs e) => AddSection(NextLabel("V", numberFirst: true));

    private void AddChorus_Click(object sender, RoutedEventArgs e) => AddSection(NextLabel("C", numberFirst: false));

    private void AddBridge_Click(object sender, RoutedEventArgs e) => AddSection(NextLabel("B", numberFirst: false));

    private void AddOther_Click(object sender, RoutedEventArgs e) => AddSection("");

    private static readonly Regex LabelPattern = new(@"^([A-Za-z]+)(\d*)$", RegexOptions.Compiled);

    /// <summary>
    /// The next unused tag for a prefix: V1, V2, V3... or C, C2, C3... A plain tag counts as 1.
    /// <paramref name="numberFirst"/> makes the first one "V1" rather than plain "C".
    /// </summary>
    private string NextLabel(string prefix, bool numberFirst)
    {
        var highest = 0;
        foreach (var section in _sections)
        {
            var match = LabelPattern.Match(section.Label.Trim());
            if (!match.Success || !match.Groups[1].Value.Equals(prefix, StringComparison.OrdinalIgnoreCase))
                continue;

            var digits = match.Groups[2].Value;
            var number = digits.Length == 0 ? 1 : int.TryParse(digits, out var parsed) ? parsed : 1;
            highest = Math.Max(highest, number);
        }

        if (highest == 0)
            return numberFirst ? prefix + "1" : prefix;
        return prefix + (highest + 1);
    }

    private void AddSection(string label) => _sections.Add(new SongSection(label, ""));

    private void RemoveSection_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: SongSection section })
            return;

        if (!string.IsNullOrWhiteSpace(section.Text))
        {
            var confirm = new ConfirmDialog(
                "Remove section",
                $"Remove the \"{section.Label}\" section?",
                "Its lyrics leave this song when you save it.",
                "Remove")
            {
                Owner = this
            };

            if (confirm.ShowDialog() != true)
                return;
        }

        _sections.Remove(section);
    }

    /// <summary>Adds a copy of the section (same label and lyrics) directly below it, ready to be edited.</summary>
    private void DuplicateSection_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: SongSection section })
            return;

        var index = _sections.IndexOf(section);
        if (index < 0)
            return;

        var tag = LabelPattern.Match(section.Label.Trim());
        var label = tag.Success ? NextLabel(tag.Groups[1].Value, numberFirst: true) : section.Label;
        var copy = new SongSection(label, section.Text);
        _sections.Insert(index + 1, copy);

        SectionsList.UpdateLayout();
        (SectionsList.ItemContainerGenerator.ContainerFromIndex(index + 1) as FrameworkElement)?.BringIntoView();
    }

    // ---- Drag a section by its grip to reorder ----

    private void Sections_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _pendingDrag = null;
        if (e.OriginalSource is not DependencyObject source || FindGrip(source) is null)
            return;

        if (ItemsControl.ContainerFromElement(SectionsList, source) is FrameworkElement { DataContext: SongSection section })
        {
            _pendingDrag = section;
            _dragStart = e.GetPosition(null);
        }
    }

    private void Sections_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _pendingDrag is null)
            return;

        var position = e.GetPosition(null);
        if (Math.Abs(position.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(position.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        var section = _pendingDrag;
        _pendingDrag = null;
        DragDrop.DoDragDrop(SectionsList, new DataObject(typeof(SongSection), section), DragDropEffects.Move);
    }

    private void Sections_DragOver(object sender, DragEventArgs e)
    {
        e.Handled = true;
        e.Effects = e.Data.GetDataPresent(typeof(SongSection)) ? DragDropEffects.Move : DragDropEffects.None;
    }

    private void Sections_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        if (e.Data.GetData(typeof(SongSection)) is not SongSection section)
            return;

        var oldIndex = _sections.IndexOf(section);
        if (oldIndex < 0)
            return;

        var newIndex = SetReorder.DestinationIndex(oldIndex, InsertIndexAt(e), _sections.Count);
        if (newIndex != oldIndex)
            _sections.Move(oldIndex, newIndex);
    }

    /// <summary>Where a drop at the pointer inserts: before a section if the pointer is in its upper half, after it otherwise.</summary>
    private int InsertIndexAt(DragEventArgs e)
    {
        var generator = SectionsList.ItemContainerGenerator;
        var lastRealized = -1;
        for (var i = 0; i < _sections.Count; i++)
        {
            if (generator.ContainerFromIndex(i) is not FrameworkElement container)
                continue;

            lastRealized = i;
            var y = e.GetPosition(container).Y;
            if (y < container.ActualHeight)
                return y < container.ActualHeight / 2 ? i : i + 1;
        }

        return lastRealized + 1;
    }

    private static FrameworkElement? FindGrip(DependencyObject? start)
    {
        for (var current = start; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is FrameworkElement { Tag: "grip" } grip)
                return grip;
        }

        return null;
    }
}
