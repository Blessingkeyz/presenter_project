using System.IO;
using BiblePresenter.App.Models;

namespace BiblePresenter.App.Services;

/// <summary>Manages the local library of background images/GIFs used behind verse text.</summary>
public sealed class MediaLibraryService
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".bmp" };
    private static readonly HashSet<string> GifExtensions = new(StringComparer.OrdinalIgnoreCase) { ".gif" };

    private readonly string _directory;

    public MediaLibraryService(string? directory = null)
    {
        _directory = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BiblePresenter", "Media");
        Directory.CreateDirectory(_directory);
    }

    public BackgroundMedia Import(string sourceFilePath)
    {
        var extension = Path.GetExtension(sourceFilePath);
        var kind = ClassifyOrThrow(extension);

        var destFileName = Path.GetFileName(sourceFilePath);
        var destPath = Path.Combine(_directory, destFileName);
        destPath = MakeUnique(destPath);

        File.Copy(sourceFilePath, destPath, overwrite: false);

        return new BackgroundMedia
        {
            FilePath = destPath,
            Name = Path.GetFileNameWithoutExtension(destPath),
            Kind = kind
        };
    }

    public List<BackgroundMedia> LoadAll()
    {
        if (!Directory.Exists(_directory))
            return new List<BackgroundMedia>();

        return Directory.EnumerateFiles(_directory)
            .Select(TryDescribe)
            .Where(m => m is not null)
            .Select(m => m!)
            .OrderBy(m => m.Name)
            .ToList();
    }

    public void Delete(BackgroundMedia media)
    {
        if (File.Exists(media.FilePath))
            File.Delete(media.FilePath);
    }

    /// <summary>
    /// Copies the app's bundled default background (Resources/Scripture.jpg, shipped next to the
    /// .exe) into the media library on first run, so verses have a sensible background out of the box.
    /// </summary>
    public BackgroundMedia? EnsureBundledDefault()
    {
        var bundledPath = Path.Combine(AppContext.BaseDirectory, "Resources", "Scripture.jpg");
        if (!File.Exists(bundledPath))
            return null;

        var destPath = Path.Combine(_directory, "Scripture.jpg");
        if (!File.Exists(destPath))
            File.Copy(bundledPath, destPath);

        return new BackgroundMedia { FilePath = destPath, Name = "Scripture", Kind = BackgroundMediaKind.Image };
    }

    private BackgroundMedia? TryDescribe(string path)
    {
        var extension = Path.GetExtension(path);
        BackgroundMediaKind kind;
        if (ImageExtensions.Contains(extension)) kind = BackgroundMediaKind.Image;
        else if (GifExtensions.Contains(extension)) kind = BackgroundMediaKind.Gif;
        else return null;

        return new BackgroundMedia { FilePath = path, Name = Path.GetFileNameWithoutExtension(path), Kind = kind };
    }

    private static BackgroundMediaKind ClassifyOrThrow(string extension)
    {
        if (ImageExtensions.Contains(extension)) return BackgroundMediaKind.Image;
        if (GifExtensions.Contains(extension)) return BackgroundMediaKind.Gif;
        throw new NotSupportedException($"Unsupported background media type '{extension}'. Use PNG, JPG, BMP, or GIF.");
    }

    private static string MakeUnique(string path)
    {
        if (!File.Exists(path))
            return path;

        var dir = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        var i = 1;
        string candidate;
        do
        {
            candidate = Path.Combine(dir, $"{name} ({i}){ext}");
            i++;
        } while (File.Exists(candidate));

        return candidate;
    }
}
