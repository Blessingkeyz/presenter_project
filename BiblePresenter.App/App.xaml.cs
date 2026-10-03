using System.Threading.Tasks;
using System.Windows;
using BiblePresenter.App.Services;
using BiblePresenter.App.ViewModels;
using BiblePresenter.App.Views;

namespace BiblePresenter.App;

public partial class App : System.Windows.Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var splash = new SplashWindow();
        splash.Show();

        var monitorService = new MonitorService();
        var songLibrary = new SongLibraryService(new SongImportService());

        // Scan the whole song library (titles + lyrics) now, off the UI thread, so the first time
        // someone searches - especially "By Lyrics" - it's instant instead of freezing the app for
        // however long a few thousand files take to read.
        await Task.Run(() => songLibrary.WarmUp());

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

        splash.Close();
    }
}
