using BiblePresenter.App.Models;

namespace BiblePresenter.App.ViewModels;

/// <summary>What the projector output window currently shows. Mutated by MainViewModel on Go Live/Clear.</summary>
public sealed class OutputViewModel : ObservableObject
{
    private Verse? _currentVerse;
    public Verse? CurrentVerse
    {
        get => _currentVerse;
        set => SetProperty(ref _currentVerse, value);
    }

    private BackgroundMedia? _currentBackground;
    public BackgroundMedia? CurrentBackground
    {
        get => _currentBackground;
        set => SetProperty(ref _currentBackground, value);
    }

    private bool _isBlank = true;
    public bool IsBlank
    {
        get => _isBlank;
        set => SetProperty(ref _isBlank, value);
    }
}
