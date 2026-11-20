using Avalonia.Media;
using Avalonia.Media.Fonts;
using OpenCiv2.Formats.Graphics;

namespace OpenCiv2.App;

/// <summary>P04 runtime typefaces. One process font collection supplies stock UI roles.</summary>
internal sealed class GraphicsFonts : FontCollectionBase
{
    private static readonly Uri Source = new("fonts:OpenCiv2");
    public override Uri Key => Source;

    public static FontFamily Sans { get; } = new("fonts:OpenCiv2#DejaVu Sans");
    public static FontFamily Serif { get; } = new("fonts:OpenCiv2#DejaVu Serif");
    public static FontFamily Mono { get; } = new("fonts:OpenCiv2#DejaVu Sans Mono");

    public static void VerifyRoles()
    {
        foreach (var (family, name) in new[] {
            (Sans, "DejaVu Sans"),
            (Serif, "DejaVu Serif"),
            (Mono, "DejaVu Sans Mono")
            })
        {
            if (!FontManager.Current.TryGetGlyphTypeface(new Typeface(family), out var face) || face.FamilyName != name)
            {
                throw new InvalidDataException($"Could not resolve pinned font family '{name}'");
            }
        }
    }

    public GraphicsFonts(PinnedFonts sources)
    {
        foreach (string id in sources.Faces)
        {
            using var stream = sources.OpenFace(id);
            if (!TryAddGlyphTypeface(stream, out _))
            {
                throw new InvalidDataException($"Avalonia rejected pinned face '{id}'");
            }
        }
    }
}
