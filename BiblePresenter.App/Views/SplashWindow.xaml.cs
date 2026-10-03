using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace BiblePresenter.App.Views;

public partial class SplashWindow : Window
{
    private static readonly string[] Phrases =
    {
        "Lighting the beacon...",
        "Trimming the wick...",
        "Kindling the flame...",
        "Gathering the light...",
        "Polishing the lens...",
        "Setting the lamp on its stand...",
        "Watching for the dawn...",
        "Tending the fire...",
        "Let there be light...",
        "Trimming the lamps..."
    };

    private readonly Random _random = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(1600) };
    private string _current = "";

    public SplashWindow()
    {
        InitializeComponent();
        StatusText.Text = NextPhrase();
        _timer.Tick += (_, _) => CrossFade();
        _timer.Start();
        Closed += (_, _) => _timer.Stop();
    }

    private string NextPhrase()
    {
        string next;
        do
        {
            next = Phrases[_random.Next(Phrases.Length)];
        } while (next == _current);

        return _current = next;
    }

    private void CrossFade()
    {
        var fadeOut = new DoubleAnimation(0, TimeSpan.FromMilliseconds(180));
        fadeOut.Completed += (_, _) =>
        {
            StatusText.Text = NextPhrase();
            StatusText.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(180)));
        };
        StatusText.BeginAnimation(UIElement.OpacityProperty, fadeOut);
    }
}
