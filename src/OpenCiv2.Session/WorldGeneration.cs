using OpenCiv2.Base.Randomness;
using OpenCiv2.Core;
using OpenCiv2.Core.State;
using OpenCiv2.Formats;

namespace OpenCiv2.Session;

/// <summary>Explicit W02 clock/layout inputs for F05 world entry. The outer caller owns time conversion.</summary>
public sealed record WorldGenerationOptions(uint Ticks, bool WideLayout = false);

/// <summary>Session-owned W02 operation, usable as F05's generation child before civilization setup.</summary>
public static class WorldGeneration
{
    public static void Generate(
        SessionRuntime runtime,
        ref StartingPositions starts,
        GameplayRandom random,
        string call,
        WorldGenerationOptions options,
        bool retainMap = false,
        MapCompatibility? compatibility = null,
        GenerationPathWorkspace? paths = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        using var pin = runtime.PinWorld();
        MapGenerator.Generate(
            runtime.World,
            ref starts,
            runtime.Process.Rules.Definitions,
            random,
            call,
            options.Ticks,
            retainMap,
            options.WideLayout,
            paths,
            retainMap ? compatibility?.GenerationMarkers(runtime.World.Map) : null);
        if (runtime.Active)
        {
            runtime.Publish();
        }
    }
}
