using System.Windows;
using System.Windows.Input;
using BiblePresenter.App.Services;
using BiblePresenter.App.ViewModels;

namespace BiblePresenter.App.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly MonitorService _monitorService;
    private readonly OutputWindow _outputWindow;

    public MainWindow(MainViewModel viewModel, MonitorService monitorService)
    {
        InitializeComponent();

        _viewModel = viewModel;
        _monitorService = monitorService;
        DataContext = viewModel;

        _outputWindow = new OutputWindow { DataContext = viewModel.Output };
        _outputWindow.Show();
        PositionOutputWindow();

        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.SelectedScreen))
                PositionOutputWindow();
        };

        Closed += (_, _) => _outputWindow.Close();
    }

    private void PositionOutputWindow()
    {
        var screen = _viewModel.SelectedScreen;
        if (screen is not null && _viewModel.Screens.Count > 1)
        {
            _outputWindow.WindowStyle = WindowStyle.None;
            _outputWindow.ResizeMode = ResizeMode.NoResize;
            _monitorService.PositionFullScreenOn(_outputWindow, screen);
        }
        else
        {
            _outputWindow.WindowStyle = WindowStyle.SingleBorderWindow;
            _outputWindow.ResizeMode = ResizeMode.CanResize;
            _outputWindow.Width = 960;
            _outputWindow.Height = 540;
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

    private void SetItemsList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => OpenEditItemDialog();

    private void EditItem_Click(object sender, RoutedEventArgs e) => OpenEditItemDialog();

    private void OpenEditItemDialog()
    {
        if (_viewModel.SelectedSetItem is not { } item)
            return;

        var dialog = new EditSetItemDialog(item, _viewModel.SaveSongToLibraryCommand) { Owner = this };
        dialog.ShowDialog();
    }
}
