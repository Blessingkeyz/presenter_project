using BiblePresenter.App.Models;

namespace BiblePresenter.App.ViewModels;

/// <summary>Where the song editor was opened from - decides which save choices make sense.</summary>
public enum SongEditOrigin
{
    /// <summary>Create song: only saving a new library song.</summary>
    NewSong,

    /// <summary>Edit a library song from the Songs search.</summary>
    LibrarySearch,

    /// <summary>Edit a song in a set that references the library.</summary>
    SetLibraryReference,

    /// <summary>Edit a song in a set that holds its own copy of the words.</summary>
    SetLocalCopy
}

public enum SongEditChoice
{
    /// <summary>Replace the library file this song came from.</summary>
    Overwrite,

    /// <summary>Write a new library song, leaving the original untouched.</summary>
    SaveAsNew,

    /// <summary>Keep the changes in this set only.</summary>
    SetOnly
}

public sealed record SongEditResult(SongEditChoice Choice, SetItem Song);

/// <summary>One slide of a song with its section tag ("V1", "C"...), for showing lyrics with their labels.</summary>
public sealed record SongSlideView(string Label, string Text);

/// <summary>One section of a song (a verse, chorus, bridge...) as edited in the song editor.</summary>
public sealed class SongSection : ObservableObject
{
    private string _label;
    public string Label { get => _label; set => SetProperty(ref _label, value); }

    private string _text;
    public string Text { get => _text; set => SetProperty(ref _text, value); }

    public SongSection(string label, string text)
    {
        _label = label;
        _text = text;
    }
}
