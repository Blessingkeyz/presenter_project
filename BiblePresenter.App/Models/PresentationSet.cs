using System.ComponentModel;

namespace BiblePresenter.App.Models;

public sealed class PresentationSet : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    private string _name = "New Set";
    public string Name
    {
        get => _name;
        set
        {
            _name = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
        }
    }

    public List<SetItem> Items { get; set; } = new();
}
