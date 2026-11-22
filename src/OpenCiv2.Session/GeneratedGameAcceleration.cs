using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Session;

/// <summary>Owned naming content supplied by the selected gameplay profile; F05 owns fixed unit identities.</summary>
public sealed record AccelerationOptions(NameCatalog Names);
public enum StartingNotice { TooManyCities, TooManyUnits }

/// <summary>Cooperative F05 acceleration owner. Each poll processes one captured civilization start.
/// Cancellation/failure retains earlier grants, entities, map edits, notices and consumed choices.</summary>
public sealed class GeneratedGameAcceleration : IDisposable
{
    private readonly SessionRuntime runtime;
    private readonly AcceleratedStarts operation;
    private readonly List<StartingNotice> notices = [];
    private SessionActivity? pin;
    public bool Completed => operation.Completed;
    public bool Cancelled { get; private set; }
    public int CurrentCivilization => operation.CurrentCivilization;
    public OwnedList<StartingNotice> Notices => new(notices);

    public GeneratedGameAcceleration(
        SessionRuntime runtime, GameplayRandom random, string call, int era, AccelerationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.Names);
        this.runtime = runtime;
        var world = runtime.World;
        var stacks = new StackOperations(world, new(50_000_000, 64));
        var effects = new GeneratedGameInitialization.StartingEffects(runtime, _ => notices.Add(StartingNotice.TooManyUnits));
        effects.Revelation = new(world, runtime.Process.Rules.Definitions, stacks, effects);
        var units = new UnitCreation(world, runtime.Process, stacks, effects);
        var cities = new CityCreation(world, runtime.Process, options.Names, units, () => notices.Add(StartingNotice.TooManyCities));
        operation = new(world, runtime.Process, cities, units, stacks, random, call, era);
        pin = runtime.PinWorld();
    }

    public bool Poll()
    {
        if (Completed || Cancelled)
        {
            return Completed;
        }

        try
        {
            bool result = operation.Advance();
            NewGameInitialization.Publish(runtime);
            if (result)
            {
                Release();
            }

            return result;
        }
        catch
        {
            Dispose();
            try
            { NewGameInitialization.Publish(runtime); }
            catch { }
            throw;
        }
    }

    private void Release()
    {
        pin?.Dispose();
        pin = null;
    }

    public void Dispose()
    {
        Cancelled |= !Completed;
        Release();
    }
}
