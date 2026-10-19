using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using BiblePresenter.App.Services;
using BiblePresenter.App.ViewModels;
using BiblePresenter.App.Views;

namespace BiblePresenter.App;

public partial class App : System.Windows.Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // A crash mid-service is the worst case for a presentation app - better to log it and keep the
        // show running than to take the whole app down over one bad operation (a locked settings file,
        // a corrupted song, a disk-full save). See AppCrashLog for where this goes.
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        var splash = new SplashWindow();
        splash.Show();

        var monitorService = new MonitorService();
        var songLibrary = new SongLibraryService(new SongImportService());
        var translationStore = new TranslationStore();
        var importService = new BibleImportService();

        // Scan the whole song library (titles + lyrics) now, off the UI thread, so the first time
        // someone searches - especially "By Lyrics" - it's instant instead of freezing the app for
        // however long a few thousand files take to read.
        await Task.Run(() => songLibrary.WarmUp());

        // First run only: the bundled KJV.xml is a full Bible (~31,000 verses) - parsing and re-saving it
        // as the cached translation is the one piece of startup work big enough to notice, so it's done
        // here, off the UI thread, before MainViewModel exists. Every run after the first just reads the
        // small cached JSON instead, which MainViewModel's own constructor already does fast on the UI
        // thread. If this fails for any reason, MainViewModel.TryImportBundledDefaultBible() retries the
        // same import synchronously as a fallback, so first run still gets a Bible either way.
        await Task.Run(() =>
        {
            if (translationStore.LoadAll().Count > 0)
                return;

            var bundledPath = Path.Combine(AppContext.BaseDirectory, "Resources", "KJV.xml");
            if (!File.Exists(bundledPath))
                return;

            try
            {
                translationStore.Save(importService.Import(bundledPath));
            }
            catch (BibleImportException)
            {
            }
        });

        var viewModel = new MainViewModel(
            importService,
            translationStore,
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

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        AppCrashLog.Record(e.Exception);
        System.Windows.MessageBox.Show(
            "Something went wrong, but Beacon Worship is staying open so the service isn't interrupted.\n\n" +
            $"What happened: {e.Exception.Message}\n\nDetails were saved to a log file if this keeps happening.",
            "Beacon Worship", MessageBoxButton.OK, MessageBoxImage.Warning);
        e.Handled = true;
    }
}
