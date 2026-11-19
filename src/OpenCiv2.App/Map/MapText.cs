using System.Globalization;
using System.Text;
using OpenCiv2.Formats.Graphics;
using SkiaSharp;

namespace OpenCiv2.App.Map;

/// <summary>Shared measurement and painting for map text using the pinned regular faces.</summary>
internal sealed class MapText : IDisposable
{
    private readonly List<SKTypeface> faces = [];
    internal readonly record struct Run(int Face, string Text, float Width);
    internal readonly record struct Line(string Text, float Width, List<Run>? Runs = null);

    public MapText(PinnedFonts? sources)
    {
        if (sources is null) return;
        try
        {
            foreach (string name in new[] { "sans.regular", "serif.regular", "mono.regular" })
            {
                using var stream = sources.OpenFace(name);
                faces.Add(SKTypeface.FromStream(stream) ?? throw new InvalidDataException($"Skia rejected pinned map face '{name}'"));
            }
        }
        catch { Dispose(); throw; }
    }

    public SKFont Font(float size) => new(faces.Count == 0 ? null : faces[0], size) { Edging = SKFontEdging.Antialias };

    public Line Measure(string text, SKFont font, SKPaint paint)
    {
        if (faces.Count == 0 || text.AsSpan().IndexOfAnyExceptInRange(' ', '~') < 0 || font.ContainsGlyphs(text))
            return new(text, font.MeasureText(text, paint));

        var runs = new List<Run>();
        var current = new StringBuilder();
        using var serif = FallbackFont(1, font);
        using var mono = FallbackFont(2, font);
        int face = 0;
        float width = 0;
        foreach (Rune character in text.EnumerateRunes())
        {
            Rune glyph = character;
            int next = font.ContainsGlyph(glyph.Value) ? 0 :
                serif.ContainsGlyph(glyph.Value) ? 1 : mono.ContainsGlyph(glyph.Value) ? 2 : -1;
            if (next < 0)
            {
                next = 0;
                glyph = Rune.ReplacementChar;
            }
            if (next != face)
            {
                AddRun();
                face = next;
            }
            current.Append(glyph.ToString());
        }
        AddRun();
        return new(text, width, runs);

        void AddRun()
        {
            if (current.Length == 0) return;
            string value = current.ToString();
            var selected = face switch { 1 => serif, 2 => mono, _ => font };
            float advance = selected.MeasureText(value, paint);
            width += advance;
            runs.Add(new(face, value, advance));
            current.Clear();
        }
    }

    public Line Fit(string text, float maximum, SKFont font, SKPaint paint)
    {
        var full = Measure(text, font, paint);
        if (full.Width <= maximum + .001f) return full;
        var ellipsis = Measure("…", font, paint);
        if (ellipsis.Width > maximum) return new("", 0);
        int[] elements = StringInfo.ParseCombiningCharacters(text);
        int low = 1, high = elements.Length - 1;
        while (low <= high)
        {
            int middle = (low + high) / 2;
            var candidate = Measure(string.Concat(text.AsSpan(0, elements[middle]), "…"), font, paint);
            if (candidate.Width <= maximum)
            {
                ellipsis = candidate;
                low = middle + 1;
            }
            else high = middle - 1;
        }
        return ellipsis;
    }

    public void DrawCentered(MapArtCanvas canvas, Line line, float center, float baseline, SKFont font, SKPaint paint)
    {
        if (line.Runs is null)
        {
            canvas.DrawText(line.Text, center, baseline, SKTextAlign.Center, font, paint);
            return;
        }
        float x = center - line.Width / 2;
        foreach (var run in line.Runs)
        {
            using var alternate = run.Face == 0 ? null : FallbackFont(run.Face, font);
            canvas.DrawText(run.Text, x, baseline, SKTextAlign.Left, alternate ?? font, paint);
            x += run.Width;
        }
    }

    private SKFont FallbackFont(int face, SKFont font) => new(faces[face], font.Size) { Edging = font.Edging };

    public void Dispose()
    {
        foreach (var face in faces) face.Dispose();
        faces.Clear();
    }
}
