using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.State;

namespace OpenCiv2.Session;

public sealed record PremadeWorldChoices(Func<int> UseSeed, Func<int> UseStartLocations, Func<uint> Ticks)
{
    internal void Validate()
    {
        ArgumentNullException.ThrowIfNull(UseSeed);
        ArgumentNullException.ThrowIfNull(UseStartLocations);
        ArgumentNullException.ThrowIfNull(Ticks);
    }
}
/// <summary>Owned map input. A source adapter may retain its opaque metadata when installation succeeds.</summary>
public record PremadeWorldInput(MapInput Map, StartingPositions Starts)
{
    public virtual void Install(MapStorage destination) => Map.Install(destination, false);
}
public interface IPremadeWorldSource
{
    /// <summary>Decode and close before returning owned map operands; null reports input failure.</summary>
    PremadeWorldInput? Load(string path);
}
public sealed record PremadeWorldServices(IPremadeWorldSource Source, Action LoadFailure, PremadeWorldChoices Choices)
{
    internal void Validate()
    {
        ArgumentNullException.ThrowIfNull(Source);
        ArgumentNullException.ThrowIfNull(LoadFailure);
        ArgumentNullException.ThrowIfNull(Choices);
        Choices.Validate();
    }
}
/// <summary>F05 §3 / F06 §4: install owned map inputs, normalize live metadata and apply seed/hint choices.
/// Successful installation and accepted choices survive later failures. No terrain generation or region
/// rebuild.</summary>
public static class PremadeWorld
{
    private static void Publish(SessionRuntime runtime)
    {
        if (runtime.Active)
        {
            runtime.Publish();
        }
    }

    private static void PublishFailure(SessionRuntime runtime)
    {
        try
        {
            Publish(runtime);
        }
        catch { }
    }

    public static WorldSource Prepare(SessionRuntime runtime, ref StartingPositions starts, PremadeWorldChoices choices)
    {
        ArgumentNullException.ThrowIfNull(choices);
        choices.Validate();
        using var pin = runtime.PinWorld();
        try
        {
            var map = runtime.World.Map;
            if (!map.Allocated || map.Tiles.Length != map.Geometry.Area)
            {
                throw new ArgumentException("Premade preparation requires complete map backing");
            }

            foreach (ref var tile in map.Tiles)
            {
                tile.Features = MapFeatures.None;
                tile.Claimant = 0;
                tile.RevealedTo = default;
                tile.Region = 0;
                tile.Owner = null;
                tile.Fertility = 0;
            }
            for (int civ = 1; civ < EntityLimits.Civilizations; ++civ)
            {
                var layer = map.RememberedLayer(civ);
                if (layer.Length != map.Geometry.Area)
                {
                    throw new ArgumentException("Premade remembered layer has incomplete backing");
                }

                layer.Clear();
            }
            bool replaceSeed = map.PatternSeed < 2;
            if (!replaceSeed)
            {
                Publish(runtime);
                replaceSeed = choices.UseSeed() == 0;
            }
            if (replaceSeed)
            {
                Publish(runtime);
                map.PatternSeed = Arithmetic.MapPatternSeedFromTicks(choices.Ticks());
            }
            bool hasHint = false;
            for (int i = 0; i < RuleLimits.Tribes; ++i)
            {
                if (starts.Positions[i].X >= 0)
                {
                    hasHint = true;
                    break;
                }
            }

            if (hasHint)
            {
                Publish(runtime);
                if (choices.UseStartLocations() == 0)
                {
                    starts = new();
                }
            }
            runtime.World.Globals.Rules.FlatWorld = false;
            var source = WorldSource.PremadeCylindrical;
            if (map.StoredTopology != 0)
            {
                runtime.World.Globals.Rules.FlatWorld = true;
                source = WorldSource.PremadeFlat;
            }
            Publish(runtime);
            return source;
        }
        catch
        {
            PublishFailure(runtime);
            throw;
        }
    }

    public static WorldSource? Load(
        SessionRuntime runtime,
        ref StartingPositions starts,
        string? selectedPath,
        PremadeWorldServices? services)
    {
        if (selectedPath is null)
        {
            return null;
        }

        if (selectedPath.Contains('\0'))
        {
            throw new ArgumentException("Premade map path contains NUL", nameof(selectedPath));
        }

        ArgumentNullException.ThrowIfNull(services);
        services.Validate();
        if (runtime.HasPendingCommands)
        {
            throw new InvalidOperationException("Premade load requires settled session commands");
        }

        using var pin = runtime.PinWorld();
        try
        {
            Publish(runtime);
            var input = services.Source.Load(selectedPath);
            if (input is null)
            {
                Publish(runtime);
                services.LoadFailure();
                Publish(runtime);
                return null;
            }
            input.Install(runtime.World.Map);
            starts = input.Starts;
            runtime.World.WorldSetup.Width = unchecked((short)input.Map.Width);
            runtime.World.WorldSetup.Height = unchecked((short)input.Map.Height);
            var result = Prepare(runtime, ref starts, services.Choices);
            Publish(runtime);
            return result;
        }
        catch
        {
            PublishFailure(runtime);
            throw;
        }
    }
}
