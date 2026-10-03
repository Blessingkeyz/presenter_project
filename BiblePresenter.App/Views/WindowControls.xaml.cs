using System.Windows;
using UserControl = System.Windows.Controls.UserControl;

namespace BiblePresenter.App.Views;

/// <summary>Minimize / maximize-restore / close for a chromeless window. Shows only what the host window's ResizeMode allows.</summary>
public partial class WindowControls : UserControl
{
    private Window? _window;

    public WindowControls()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            _window = Window.GetWindow(this);
            if (_window is null)
                return;

            _window.StateChanged += (_, _) => Refresh();
            Refresh();
        };
    }

    private void Refresh()
    {
        if (_window is null)
            return;

        MinimizeButton.Visibility = _window.ResizeMode == ResizeMode.NoResize ? Visibility.Collapsed : Visibility.Visible;
        MaximizeButton.Visibility = _window.ResizeMode == ResizeMode.CanResize ? Visibility.Visible : Visibility.Collapsed;

        var maximized = _window.WindowState == WindowState.Maximized;
        MaximizeButton.Content = maximized ? "" : "";
        MaximizeButton.ToolTip = maximized ? "Restore" : "Maximize";
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        if (_window is not null)
            _window.WindowState = WindowState.Minimized;
    }

    private void MaximizeRestore_Click(object sender, RoutedEventArgs e)
    {
        if (_window is not null)
            _window.WindowState = _window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => _window?.Close();
}
