using System.IO;
using System.Text.Json;
using BiblePresenter.App.Models;

namespace BiblePresenter.App.Services;

/// <summary>
/// Caches imported translations as local JSON so re-launching the app never needs to re-parse XML.
/// </summary>
public sealed class TranslationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly string _directory;

    public TranslationStore(string? directory = null)
    {
        _directory = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BiblePresenter", "Translations");
        Directory.CreateDirectory(_directory);
    }

    public void Save(Translation translation)
    {
        var path = Path.Combine(_directory, translation.Id + ".json");
        var json = JsonSerializer.Serialize(translation, JsonOptions);
        File.WriteAllText(path, json);
    }

    public List<Translation> LoadAll()
    {
        var result = new List<Translation>();
        if (!Directory.Exists(_directory))
            return result;

        foreach (var file in Directory.EnumerateFiles(_directory, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var translation = JsonSerializer.Deserialize<Translation>(json, JsonOptions);
                if (translation is not null)
                    result.Add(translation);
            }
            catch (JsonException)
            {
                // Skip a corrupted cache file rather than failing startup.
            }
        }

        return result;
    }

    public void Delete(string translationId)
    {
        var path = Path.Combine(_directory, translationId + ".json");
        if (File.Exists(path))
            File.Delete(path);
    }
}
