using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Session;

/// <summary>F05 §5/§8 and V01 local starting setup with actual unit creation, revelation and research.
/// The caller establishes the fresh world, rules, players and W02 map first, and owns the mutation lane.</summary>
public sealed class GeneratedGameInitialization : IDisposable
{
    private readonly SessionRuntime runtime;
    private readonly StartingPositions hints;
    private readonly GameplayRandom random;
    private readonly string call;
    private readonly CivilizationCreationServices services;
    private SessionActivity? pin;
    private CivilizationCreation? current;
    private int nextCivilization;
    private bool started;

    public bool Completed { get; private set; }
    public bool Cancelled { get; private set; }
    public int CurrentCivilization => nextCivilization;

    public GeneratedGameInitialization(SessionRuntime runtime, StartingPositions hints, GameplayRandom random, string call)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentException.ThrowIfNullOrEmpty(call);
        if (runtime.World.Globals.Multiplayer != GameMode.SinglePlayer || !runtime.World.Map.Allocated)
        {
            throw new InvalidOperationException("Generated starting setup requires a local generated or imported map");
        }
        this.runtime = runtime;
        this.hints = hints;
        this.random = random;
        this.call = call;
        var effects = new StartingEffects(runtime);
        var stacks = new StackOperations(runtime.World, new(50_000_000, 64));
        effects.Revelation = new(runtime.World, runtime.Process.Rules.Definitions, stacks, effects);
        services = new(new(runtime.World, runtime.Process, stacks, effects),
            new(runtime.World, runtime.Process.Rules.Definitions, new(50_000_000, 64), effects),
            null, // NewGameInitialization.Reset establishes turn zero before any creation.
            (c, type) => UnitAvailability.CanBuild(runtime.World, runtime.Process.Rules.Definitions, c, null, type));
        pin = runtime.PinWorld();
    }

    /// <summary>Advances at most one civilization and the requested number of site candidates.
    /// Disposing pending work retains prior setup effects and choices, and releases the world pin.</summary>
    public bool Poll(int candidateBudget = 64)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(candidateBudget);
        if (Completed || Cancelled)
        {
            return Completed;
        }
        try
        {
            if (!started)
            {
                started = true;
                NewGameInitialization.Reset(runtime, random, call);
            }
            if (current is null)
            {
                NewGameInitialization.Publish(runtime);
                current = new(runtime.World, runtime.Process, nextCivilization, hints, random,
                    call + "/civ/" + nextCivilization, services);
            }
            if (current.Advance(candidateBudget) is null)
            {
                if (current.Pending is not null)
                { throw new InvalidOperationException("Fresh turn-zero placement cannot request a catch-up decision"); }
                return false;
            }
            current.Dispose();
            current = null;
            if (++nextCivilization < EntityLimits.Civilizations)
            {
                return false;
            }
            NewGameInitialization.Finish(runtime,
                () => StartingSites.Compensate(runtime.World, runtime.Process, services.Units, random, call + "/balance"));
            Completed = true;
            Release();
            return true;
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
        current?.Dispose();
        current = null;
        pin?.Dispose();
        pin = null;
    }

    public void Dispose()
    {
        Cancelled |= !Completed;
        Release();
    }

    internal sealed class StartingEffects(SessionRuntime runtime, Action<UnitCreationNotice>? capacityNotice = null)
        : IUnitCreationEffects, IUnitDeletionEffects, IUnitRevelationEffects
    {
        public UnitRevelation Revelation { private get; set; } = null!;

        public void RevealSurroundings(int slot, bool allowContact) => Revelation.Reveal(slot, allowContact);

        public void RevealOrigin(MapPoint position) => NewGameInitialization.Publish(runtime);

        public void Meet(int actingCivilization, int otherCivilization, MapPoint position)
        {
            // V04 contact's real entry exits, before audiences or treaty writes. Generated starts are turn zero.
            if (actingCivilization == 0 || otherCivilization == 0 || runtime.World.Globals.Turn == 0)
            {
                return;
            }
            throw new InvalidOperationException("Nonzero-turn meetings require the general diplomacy service");
        }

        public void Notice(UnitCreationNotice notice)
        {
            if (notice == UnitCreationNotice.TooManyUnits && capacityNotice is not null)
            {
                capacityNotice(notice);
                return;
            }
            throw new InvalidOperationException("Fresh turn-zero placement cannot reach a creation tutorial or capacity refusal");
        }

        public void UnitRemoved(MapPoint position) => NewGameInitialization.Publish(runtime);

        public void RefreshHomeCity(int city) => NewGameInitialization.Publish(runtime);

        public void CheckCivilization(int owner, int killer)
        {
            if (owner != 0)
            {
                throw new InvalidOperationException("Starting placement only removes barbarian units");
            }
        }

        public void SynchronizeCreation() => throw new InvalidOperationException("Starting setup is local");

        public void SynchronizeDeletion(MapPoint position, CitySlot? home) => throw new InvalidOperationException("Starting setup is local");
    }
}
