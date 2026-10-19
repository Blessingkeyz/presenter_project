using System.IO;

namespace BiblePresenter.App.Services;

/// <summary>
/// Appends unhandled exceptions to a log file so a crash that was kept non-fatal (see
/// App.OnDispatcherUnhandledException) still leaves a trace to diagnose later. Never throws itself -
/// logging failure should never compound the original problem.
/// </summary>
public static class AppCrashLog
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BiblePresenter", "crash.log");

    public static void Record(Exception exception)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {exception}\n\n");
        }
        catch
        {
            // Nowhere left to report a logging failure - swallow it rather than throw from a crash handler.
        }
    }
}
