using System.Windows;
using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace BiblePresenter.App.Views;

public partial class PromptDialog : Window
{
    public string ResultText { get; private set; } = "";

    public PromptDialog(string title, string message, string initialValue = "")
    {
        InitializeComponent();
        Title = title;
        MessageText.Text = message;
        InputBox.Text = initialValue;
        InputBox.Focus();
        InputBox.SelectAll();
    }

    private void Ok_Click(object sender, RoutedEventArgs e) => TryAccept();

    private void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            TryAccept();
    }

    private void TryAccept()
    {
        if (string.IsNullOrWhiteSpace(InputBox.Text))
            return;

        ResultText = InputBox.Text.Trim();
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
