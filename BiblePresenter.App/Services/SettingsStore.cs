using System.IO;
using System.Text.Json;
using BiblePresenter.App.Models;

namespace BiblePresenter.App.Services;

/// <summary>Persists the user's presentation font settings between launches.</summary>
public sealed class SettingsStore
{
    private readonly string _filePath;

    public SettingsStore(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BiblePresenter", "settings.json");
    }

    public PresentationSettings Load()
    {
        if (!File.Exists(_filePath))
            return new PresentationSettings();

        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<PresentationSettings>(json) ?? new PresentationSettings();
        }
        catch (JsonException)
        {
            return new PresentationSettings();
        }
    }

    public void Save(PresentationSettings settings)
    {
        var dir = Path.GetDirectoryName(_filePath)!;
        Directory.CreateDirectory(dir);
        File.WriteAllText(_filePath, JsonSerializer.Serialize(settings));
    }
}
