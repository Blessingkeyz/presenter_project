using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using BiblePresenter.App.Models;
using BiblePresenter.App.Services;
using BiblePresenter.App.ViewModels;
using DataObject = System.Windows.DataObject;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;

namespace BiblePresenter.App.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly MonitorService _monitorService;
    private OutputWindow? _outputWindow;

    public MainWindow(MainViewModel viewModel, MonitorService monitorService)
    {
        InitializeComponent();

        _viewModel = viewModel;
        _monitorService = monitorService;
        DataContext = viewModel;

        // The projector window exists only while something is on air: Go Live/Present opens it and Stop closes it.
        viewModel.Output.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(OutputViewModel.IsBlank))
                return;

            if (viewModel.Output.IsBlank)
                CloseOutputWindow();
            else
                OpenOutputWindow();
        };

        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.SelectedScreen) && _outputWindow is not null)
                PositionOutputWindow(_outputWindow);
        };

        Closed += (_, _) =>
        {
            _viewModel.FlushPendingSave();
            CloseOutputWindow();
        };
    }

    private void OpenOutputWindow()
    {
        if (_outputWindow is not null)
            return;

        var window = new OutputWindow { DataContext = _viewModel.Output };
        window.Closed += (sender, _) =>
        {
            // Closed by the operator (X or Alt+F4) rather than by Stop: nothing is on screen any more, so say so.
            if (!ReferenceEquals(sender, _outputWindow))
                return;

            _outputWindow = null;
            _viewModel.Output.IsBlank = true;
        };

        _outputWindow = window;
        window.Show();
        PositionOutputWindow(window);
        Activate();
    }

    private void CloseOutputWindow()
    {
        if (_outputWindow is not { } window)
            return;

        _outputWindow = null;
        window.Close();
    }

    private void PositionOutputWindow(OutputWindow window)
    {
        var screen = _viewModel.SelectedScreen;
        if (screen is not null && _viewModel.Screens.Count > 1)
        {
            window.WindowStyle = WindowStyle.None;
            window.ResizeMode = ResizeMode.NoResize;
            _monitorService.PositionFullScreenOn(window, screen);
        }
        else
        {
            window.WindowStyle = WindowStyle.SingleBorderWindow;
            window.ResizeMode = ResizeMode.CanResize;
            window.Width = 960;
            window.Height = 540;
        }
    }

    private void SearchBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        if (_viewModel.IsSongSearch)
        {
            if (_viewModel.SelectedSongResult is null && _viewModel.SongResults.Count > 0)
                _viewModel.SelectedSongResult = _viewModel.SongResults[0];
        }
        else if (_viewModel.SelectedResult is null && _viewModel.SearchResults.Count > 0)
        {
            _viewModel.SelectedResult = _viewModel.SearchResults[0];
        }

        if (_viewModel.GoLiveCommand.CanExecute(null))
            _viewModel.GoLiveCommand.Execute(null);
    }

    private void ResultsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel.GoLiveCommand.CanExecute(null))
            _viewModel.GoLiveCommand.Execute(null);
    }

    private void TranslationBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TranslationBox.SelectedItem is not Translation selected)
            return;

        if (ReferenceEquals(selected, MainViewModel.ImportTranslationSentinel))
        {
            // Revert the visible selection to whatever's actually loaded before running Import,
            // so the sentinel row never sticks as "selected" in the dropdown.
            TranslationBox.SelectedItem = _viewModel.SelectedTranslation;
            if (_viewModel.ImportBibleCommand.CanExecute(null))
                _viewModel.ImportBibleCommand.Execute(null);
            return;
        }

        _viewModel.SelectedTranslation = selected;
    }

    private void SetFileMenu_Click(object sender, RoutedEventArgs e)
    {
        var button = (System.Windows.Controls.Button)sender;
        if (button.ContextMenu is not { } menu)
            return;

        menu.PlacementTarget = button;
        menu.IsOpen = true;
    }

    private void SetItemsList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => OpenEditItemDialog();

    // ---- Drag an item to reorder the set ----

    private Point _dragStartPoint;
    private SetItem? _pendingDragItem;
    private long _lastAutoScrollTick;

    private void SetItems_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _pendingDragItem = null;

        // The x button removes; pressing it must not start a drag.
        if (e.OriginalSource is not DependencyObject source || IsWithinButton(source))
            return;

        if (ItemsControl.ContainerFromElement(SetItemsListBox, source) is ListBoxItem { DataContext: SetItem item })
        {
            _pendingDragItem = item;
            _dragStartPoint = e.GetPosition(null);
        }
    }

    private void SetItems_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _pendingDragItem is null)
            return;

        var position = e.GetPosition(null);
        if (Math.Abs(position.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(position.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        var item = _pendingDragItem;
        _pendingDragItem = null;
        try
        {
            DragDrop.DoDragDrop(SetItemsListBox, new DataObject(typeof(SetItem), item), DragDropEffects.Move);
        }
        finally
        {
            HideDropIndicator();
        }
    }

    private void SetItems_DragOver(object sender, DragEventArgs e)
    {
        e.Handled = true;
        if (!e.Data.GetDataPresent(typeof(SetItem)))
        {
            e.Effects = DragDropEffects.None;
            return;
        }

        e.Effects = DragDropEffects.Move;
        ShowDropIndicator(InsertIndexAt(e));
        AutoScrollWhileDragging(e);
    }

    private void SetItems_DragLeave(object sender, DragEventArgs e) => HideDropIndicator();

    private void SetItems_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        HideDropIndicator();
        if (e.Data.GetData(typeof(SetItem)) is SetItem item)
            _viewModel.MoveSetItemTo(item, InsertIndexAt(e));
    }

    /// <summary>Where a drop at the pointer would insert: before the row if the pointer is in its upper half, after it otherwise.</summary>
    private int InsertIndexAt(DragEventArgs e)
    {
        var generator = SetItemsListBox.ItemContainerGenerator;
        var lastRealized = -1;
        for (var i = 0; i < SetItemsListBox.Items.Count; i++)
        {
            if (generator.ContainerFromIndex(i) is not ListBoxItem container)
                continue;

            lastRealized = i;
            var y = e.GetPosition(container).Y;
            if (y < container.ActualHeight)
                return y < container.ActualHeight / 2 ? i : i + 1;
        }

        return lastRealized + 1;
    }

    private void ShowDropIndicator(int insertIndex)
    {
        var generator = SetItemsListBox.ItemContainerGenerator;
        double y;
        if (generator.ContainerFromIndex(insertIndex) is ListBoxItem next)
            y = next.TransformToAncestor(SetListHost).Transform(new Point(0, 0)).Y;
        else if (generator.ContainerFromIndex(insertIndex - 1) is ListBoxItem previous)
            y = previous.TransformToAncestor(SetListHost).Transform(new Point(0, previous.ActualHeight)).Y;
        else
        {
            HideDropIndicator();
            return;
        }

        DropIndicator.Margin = new Thickness(0, Math.Max(0, y - 1), 14, 0);
        DropIndicator.Visibility = Visibility.Visible;
    }

    private void HideDropIndicator() => DropIndicator.Visibility = Visibility.Collapsed;

    /// <summary>Nudges a long list along when an item is dragged near its top or bottom edge.</summary>
    private void AutoScrollWhileDragging(DragEventArgs e)
    {
        const double edge = 36;
        var now = Environment.TickCount64;
        if (now - _lastAutoScrollTick < 90 || FindScrollViewer(SetItemsListBox) is not { } scroller)
            return;

        var y = e.GetPosition(SetItemsListBox).Y;
        if (y < edge)
            scroller.LineUp();
        else if (y > SetItemsListBox.ActualHeight - edge)
            scroller.LineDown();
        else
            return;

        _lastAutoScrollTick = now;
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject root)
    {
        if (root is ScrollViewer viewer)
            return viewer;

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            if (FindScrollViewer(VisualTreeHelper.GetChild(root, i)) is { } found)
                return found;
        }

        return null;
    }

    private static bool IsWithinButton(DependencyObject? element)
    {
        while (element is not null)
        {
            if (element is System.Windows.Controls.Button)
                return true;

            element = element is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(element)
                : LogicalTreeHelper.GetParent(element);
        }

        return false;
    }

    private void EditItem_Click(object sender, RoutedEventArgs e) => OpenEditItemDialog();

    private void OpenEditItemDialog()
    {
        if (_viewModel.SelectedSetItem is not { } item || item.Type == SetItemType.Scripture)
            return;

        if (item.Type == SetItemType.Song)
        {
            _viewModel.EditSetSong(item);
            return;
        }

        var dialog = new EditSetItemDialog(item) { Owner = this };
        dialog.ShowDialog();
    }
}
