using System.Windows;
using BiblePresenter.App.Services;
using BiblePresenter.App.ViewModels;
using BiblePresenter.App.Views;

namespace BiblePresenter.App;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var monitorService = new MonitorService();
        var viewModel = new MainViewModel(
            new BibleImportService(),
            new TranslationStore(),
            new MediaLibraryService(),
            new SearchIndexService(),
            monitorService);

        var mainWindow = new MainWindow(viewModel, monitorService);
        MainWindow = mainWindow;
        mainWindow.Show();
    }
}
