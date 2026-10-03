using System.IO;
using System.Windows.Media;
using FontFamily = System.Windows.Media.FontFamily;

namespace BiblePresenter.App.Views;

/// <summary>
/// Resolves a font family name to one of the fonts embedded in Resources/Fonts (e.g. "Helvetica LT",
/// the genuine Linotype family, bundled with the app so it renders correctly even on machines that
/// don't have it installed), falling back to a normal system font lookup for anything else the user
/// types in Settings.
/// </summary>
public static class BundledFonts
{
    private static readonly Uri FolderUri = new("pack://application:,,,/Resources/Fonts/");
    private static readonly Lazy<HashSet<string>> FamilyNames = new(() =>
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var family in Fonts.GetFontFamilies(FolderUri))
            {
                var name = family.FamilyNames.Values.FirstOrDefault();
                if (name is not null)
                    names.Add(name);
            }
        }
        catch (IOException)
        {
            // No bundled fonts available; callers fall back to system fonts.
        }
        return names;
    });

    public static IReadOnlyCollection<string> Names => FamilyNames.Value;

    private static readonly Lazy<IReadOnlyList<string>> AllChoices = new(() =>
    {
        var bundled = FamilyNames.Value.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        var system = Fonts.SystemFontFamilies
            .Select(f => f.Source)
            .Where(n => !FamilyNames.Value.Contains(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase);
        return bundled.Concat(system).ToList();
    });

    /// <summary>Bundled fonts first, then every installed system font - what the Settings font dropdown offers.</summary>
    public static IReadOnlyList<string> Choices => AllChoices.Value;

    public static FontFamily Resolve(string requestedName)
    {
        if (!string.IsNullOrWhiteSpace(requestedName) && FamilyNames.Value.Contains(requestedName))
            return new FontFamily(FolderUri, "./#" + requestedName);

        return new FontFamily(requestedName);
    }
}
