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
        var songLibrary = new SongLibraryService(new SongImportService());
        var viewModel = new MainViewModel(
            new BibleImportService(),
            new TranslationStore(),
            new MediaLibraryService(),
            new SearchIndexService(),
            monitorService,
            new SettingsStore(),
            new SetXmlStore(songLibrary),
            songLibrary);

        var mainWindow = new MainWindow(viewModel, monitorService);
        MainWindow = mainWindow;
        mainWindow.Show();
    }
}
