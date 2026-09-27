using System.ComponentModel;
using System.Windows;
using System.Windows.Media.Animation;
using BiblePresenter.App.ViewModels;

namespace BiblePresenter.App.Views;

public partial class OutputWindow : Window
{
    public OutputWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is OutputViewModel oldVm)
            oldVm.PropertyChanged -= OnOutputPropertyChanged;
        if (e.NewValue is OutputViewModel newVm)
            newVm.PropertyChanged += OnOutputPropertyChanged;
    }

    private void OnOutputPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(OutputViewModel.BodyText))
            return;

        var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180));
        TextPanel.BeginAnimation(OpacityProperty, fade);
    }
}
