using SkiaSharp;

namespace OpenCiv2.MapRendering;

// P02 nearest-cache sampling: separate physical pixel coverage from source
// selection. Fractional GPU quads and normalized nearest lookups can otherwise
// select the neighboring texel near backing-tier boundaries on SwiftShader.
internal sealed class MapGpuNearest : IDisposable
{
    private SKRuntimeEffect? effect;

    internal void Draw(SKCanvas canvas, SKImage image, SKRect source, SKRect target)
    {
        var matrix = canvas.TotalMatrix;
        if (matrix.SkewX != 0 || matrix.SkewY != 0 || matrix.Persp0 != 0 || matrix.Persp1 != 0 ||
            matrix.Persp2 != 1 || matrix.ScaleX <= 0 || matrix.ScaleY <= 0)
        {
            canvas.DrawImage(image, source, target, new SKSamplingOptions(SKFilterMode.Nearest));
            return;
        }

        var physical = matrix.MapRect(target);
        if (physical.Width <= 0 || physical.Height <= 0)
        {
            return;
        }

        effect ??= SKRuntimeEffect.CreateShader("""
            uniform shader image;
            uniform float4 mapping;
            half4 main(float2 p) {
                float2 texel = floor(p * mapping.xy + mapping.zw);
                return image.eval(texel + float2(0.5));
            }
            """, out string error) ?? throw new MapGpuResourceException(error);
        float sx = source.Width / physical.Width, sy = source.Height / physical.Height;
        using var child = image.ToShader(SKShaderTileMode.Clamp, SKShaderTileMode.Clamp, new SKSamplingOptions(SKFilterMode.Nearest));
        using var uniforms = new SKRuntimeEffectUniforms(effect)
        { ["mapping"] = new float[] { sx, sy, source.Left - physical.Left * sx, source.Top - physical.Top * sy } };
        var children = new SKRuntimeEffectChildren(effect) { ["image"] = child };
        using var shader = effect.ToShader(uniforms, children);
        using var paint = new SKPaint { Shader = shader };
        using var restore = new SKAutoCanvasRestore(canvas, true);
        canvas.ResetMatrix();
        // Adjacent chunks share the same rounded edge. Keep the original
        // transform in the sampling uniform, so map geometry/picking is unchanged.
        canvas.DrawRect(new SKRect(MathF.Floor(physical.Left + .5f), MathF.Floor(physical.Top + .5f),
            MathF.Floor(physical.Right + .5f), MathF.Floor(physical.Bottom + .5f)), paint);
    }

    public void Dispose() => effect?.Dispose();
}
