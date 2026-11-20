using SkiaSharp;

namespace OpenCiv2.MapRendering;

/// <summary>One foreground traversal, followed by atlas admission and ordered submission.
/// Commands live for one paint only. They never own GPU resources or survive a scene update.</summary>
internal sealed class MapForegroundCommands : IDisposable
{
    internal enum Kind { Sprite, Image, Rect, RoundRect, Circle, Line, Text, Save, Clip, Restore, Flush }

    internal readonly record struct Command(
        Kind Kind,
        SKRect Box = default,
        float A = 0,
        float B = 0,
        SKImage? Image = null,
        MapGpuSpriteAtlas.Key Key = default,
        SKPaint? Paint = null,
        SKFont? Font = null,
        string? Text = null,
        SKTextAlign Align = SKTextAlign.Left,
        SKSamplingOptions Sampling = default,
        SKColor? Tint = null);

    private readonly record struct PaintKey(
        SKColorF Color,
        SKPaintStyle Style,
        bool Antialias,
        float Width,
        SKStrokeCap Cap,
        SKStrokeJoin Join,
        float Miter,
        SKBlendMode Blend,
        nint Filter,
        nint Effect,
        nint Shader,
        nint ImageFilter);

    private readonly record struct FontKey(nint Face, float Size, SKFontEdging Edging);

    private readonly List<Command> commands = [];
    private readonly Dictionary<PaintKey, SKPaint> paints = [];
    private readonly Dictionary<FontKey, SKFont> fonts = [];
    private readonly Dictionary<SKPaint, PaintKey> paintTemplates = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<SKFont, FontKey> fontTemplates = new(ReferenceEqualityComparer.Instance);
    public int Count => commands.Count;

    public void Add(Command command)
    {
        if (commands.Count >= 262144)
        {
            throw new MapCommandCapacityException();
        }

        commands.Add(command);
    }

    public SKPaint? Capture(SKPaint? paint)
    {
        if (paint is null)
        {
            return null;
        }

        if (!paintTemplates.TryGetValue(paint, out var template))
        {
            template = new(
                paint.ColorF,
                paint.Style,
                paint.IsAntialias,
                paint.StrokeWidth,
                paint.StrokeCap,
                paint.StrokeJoin,
                paint.StrokeMiter,
                paint.BlendMode,
                0,
                0,
                paint.Shader?.Handle ?? 0,
                paint.ImageFilter?.Handle ?? 0);
            paintTemplates.Add(paint, template);
        }
        // Foreground helpers vary only color, color filter and path effect on a
        // paint instance; geometry/style are fixed at construction. Avoid twelve
        // native property reads for every rectangle and text command.
        var key = template with
        {
            Color = paint.ColorF,
            Filter = paint.ColorFilter?.Handle ?? 0,
            Effect = paint.PathEffect?.Handle ?? 0
        };
        if (!paints.TryGetValue(key, out var copy))
        {
            paints.Add(key, copy = paint.Clone());
        }

        return copy;
    }

    public SKFont Capture(SKFont font)
    {
        // MapText creates these fonts with only face, size and edging varied.
        if (!fontTemplates.TryGetValue(font, out var template))
        {
            fontTemplates.Add(font, template = new(font.Typeface?.Handle ?? 0, font.Size, font.Edging));
        }

        var key = template with { Size = font.Size };
        if (!fonts.TryGetValue(key, out var copy))
        {
            fonts.Add(key, copy = new(font.Typeface, font.Size) { Edging = font.Edging });
        }

        return copy;
    }

    public void Prepare(MapGpuSpriteAtlas atlas)
    {
        atlas.BeginPreparation();
        foreach (var command in commands)
        {
            if (command.Kind == Kind.Sprite && MapGpuSpriteAtlas.CanBatch(command.Image!, command.Box, command.Paint))
            {
                atlas.Collect(command.Key, command.Image!);
            }
        }

        atlas.FinishPreparation();
    }

    public void Submit(SKCanvas canvas, MapGpuSpriteAtlas atlas)
    {
        foreach (var c in commands)
        {
            switch (c.Kind)
            {
                case Kind.Sprite:
                    atlas.Draw(canvas, c.Key, c.Image!, c.Box, c.Sampling, c.Paint, c.Tint);
                    break;
                case Kind.Flush:
                    atlas.Flush();
                    break;
                case Kind.Image:
                    canvas.DrawImage(c.Image!, c.Box, c.Sampling, c.Paint);
                    break;
                case Kind.Rect:
                    canvas.DrawRect(c.Box, c.Paint!);
                    break;
                case Kind.RoundRect:
                    canvas.DrawRoundRect(c.Box, c.A, c.B, c.Paint!);
                    break;
                case Kind.Circle:
                    canvas.DrawCircle(c.Box.Left, c.Box.Top, c.A, c.Paint!);
                    break;
                case Kind.Line:
                    canvas.DrawLine(c.Box.Left, c.Box.Top, c.Box.Right, c.Box.Bottom, c.Paint!);
                    break;
                case Kind.Text:
                    canvas.DrawText(c.Text!, c.Box.Left, c.Box.Top, c.Align, c.Font!, c.Paint!);
                    break;
                case Kind.Save:
                    canvas.Save();
                    break;
                case Kind.Clip:
                    canvas.ClipRect(c.Box);
                    break;
                case Kind.Restore:
                    canvas.Restore();
                    break;
            }
        }

        atlas.Flush();
    }

    public void Reset()
    {
        commands.Clear();
        foreach (var paint in paints.Values)
        {
            paint.Dispose();
        }

        foreach (var font in fonts.Values)
        {
            font.Dispose();
        }

        paints.Clear();
        fonts.Clear();
        paintTemplates.Clear();
        fontTemplates.Clear();
    }

    public void Dispose() => Reset();
}

/// <summary>The small canvas vocabulary used by foreground art. Immediate drawing remains
/// available for the CPU and baseline GPU paths; recording snapshots mutable paint/font state.</summary>
internal readonly struct MapArtCanvas
{
    public SKCanvas? Immediate { get; }
    public MapForegroundCommands? Commands { get; }

    public MapArtCanvas(MapForegroundCommands commands) => Commands = commands;

    private MapArtCanvas(SKCanvas canvas) => Immediate = canvas;

    public static implicit operator MapArtCanvas(SKCanvas canvas) => new(canvas);

    public void Flush(MapGpuSpriteAtlas atlas)
    {
        if (Commands is { } list)
        {
            list.Add(new(MapForegroundCommands.Kind.Flush));
        }
        else
        {
            atlas.Flush();
        }
    }

    public void DrawImage(SKImage image, SKRect box, SKSamplingOptions sampling, SKPaint? paint = null)
    {
        if (Commands is { } list)
        {
            list.Add(
                new(
                    MapForegroundCommands.Kind.Image,
                    box,
                    Image: image,
                    Sampling: sampling,
                    Paint: list.Capture(paint)));
        }
        else
        {
            Immediate!.DrawImage(image, box, sampling, paint);
        }
    }

    public void DrawRect(SKRect box, SKPaint paint)
    {
        if (Commands is { } list)
        {
            list.Add(new(MapForegroundCommands.Kind.Rect, box, Paint: list.Capture(paint)));
        }
        else
        {
            Immediate!.DrawRect(box, paint);
        }
    }

    public void DrawRect(float x, float y, float width, float height, SKPaint paint) =>
        DrawRect(SKRect.Create(x, y, width, height), paint);

    public void DrawRoundRect(SKRect box, float rx, float ry, SKPaint paint)
    {
        if (Commands is { } list)
        {
            list.Add(new(MapForegroundCommands.Kind.RoundRect, box, rx, ry, Paint: list.Capture(paint)));
        }
        else
        {
            Immediate!.DrawRoundRect(box, rx, ry, paint);
        }
    }

    public void DrawRoundRect(float x, float y, float width, float height, float rx, float ry, SKPaint paint) =>
        DrawRoundRect(SKRect.Create(x, y, width, height), rx, ry, paint);

    public void DrawCircle(float x, float y, float radius, SKPaint paint)
    {
        if (Commands is { } list)
        {
            list.Add(new(MapForegroundCommands.Kind.Circle, new(x, y, x, y), radius, Paint: list.Capture(paint)));
        }
        else
        {
            Immediate!.DrawCircle(x, y, radius, paint);
        }
    }

    public void DrawLine(float x1, float y1, float x2, float y2, SKPaint paint)
    {
        if (Commands is { } list)
        {
            list.Add(new(MapForegroundCommands.Kind.Line, new(x1, y1, x2, y2), Paint: list.Capture(paint)));
        }
        else
        {
            Immediate!.DrawLine(x1, y1, x2, y2, paint);
        }
    }

    public void DrawText(string value, float x, float y, SKTextAlign align, SKFont font, SKPaint paint)
    {
        if (Commands is { } list)
        {
            list.Add(new(MapForegroundCommands.Kind.Text, new(x, y, x, y),
                Text: value, Align: align, Font: list.Capture(font), Paint: list.Capture(paint)));
        }
        else
        {
            Immediate!.DrawText(value, x, y, align, font, paint);
        }
    }

    public void Save()
    {
        if (Commands is { } list)
        {
            list.Add(new(MapForegroundCommands.Kind.Save));
        }
        else
        {
            Immediate!.Save();
        }
    }

    public void ClipRect(SKRect box)
    {
        if (Commands is { } list)
        {
            list.Add(new(MapForegroundCommands.Kind.Clip, box));
        }
        else
        {
            Immediate!.ClipRect(box);
        }
    }

    public void Restore()
    {
        if (Commands is { } list)
        {
            list.Add(new(MapForegroundCommands.Kind.Restore));
        }
        else
        {
            Immediate!.Restore();
        }
    }
}
