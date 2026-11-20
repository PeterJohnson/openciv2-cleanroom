using OpenCiv2.Core.Rules;
using OpenCiv2.Formats.Classic;

namespace OpenCiv2.Session;

public static class TextConfiguration
{
    /// <summary>P04 §2 process language/cache setup, independent of label parsing.</summary>
    public static void Initialize(
        int language,
        TextDirectories directories,
        Labels labels,
        Action buildGameCache)
    {
        ArgumentNullException.ThrowIfNull(buildGameCache);
        labels.StatusLanguage = unchecked(language - 1);
        if (labels.StatusLanguage > 2)
        {
            labels.StatusLanguage = 0;
        }

        directories.Extension = language switch { 2 => "FRE", 3 => "GER", 4 => "ITA", 5 => "SPA", _ => "TXT" };
        buildGameCache();
    }
}
