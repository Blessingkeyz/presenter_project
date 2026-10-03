using System.Windows;

namespace BiblePresenter.App.Views;

/// <summary>A warning before something is taken away. Returns true from ShowDialog only if the confirm button is pressed.</summary>
public partial class ConfirmDialog : Window
{
    public ConfirmDialog(string title, string message, string detail, string confirmText)
    {
        InitializeComponent();
        Title = title;
        MessageText.Text = message;
        DetailText.Text = detail;
        ConfirmButton.Content = confirmText;
    }

    private void Confirm_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
