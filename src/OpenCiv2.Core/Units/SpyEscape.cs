using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Units;

public enum SpyFate { Survived, VeteranSurvived, Died, Caught }
public readonly record struct SpyNotice(UnitTarget Unit, UnitType Type, int Owner, SpyFate Fate, string? Destination = null);
public sealed record SpyIncidentRequest(int Actor, int Victim);
public sealed record SpyEscapeResult(bool Lost, SpyIncidentRequest? Incident = null);

public interface ISpyEscapeEffects
{
    void Notice(SpyNotice notice);
    void Redraw(MapPoint position);
}

/// <summary>V06 §2 local escape/capture with actual C01 nearest-city and U01/U02 movement/loss.
/// The required incident child owns diplomacy, decisions and government consequences.</summary>
public sealed class SpyEscape(GameState world, RuleTables rules, StackOperations stacks, UnitLoss loss,
    GameplayRandom random, Action<int, int> incident, ISpyEscapeEffects effects)
{
    /// <summary>Retained composition: CivilizationElimination owns the actual incident child and its replies.</summary>
    public SpyEscape(GameState world, RuleTables rules, StackOperations stacks, UnitLoss loss,
        GameplayRandom random, ISpyEscapeEffects effects)
        : this(world, rules, stacks, loss, random,
            (_, _) => throw new InvalidOperationException("Use the retained lifecycle operation to resolve espionage incidents"), effects)
    { }

    private bool Human(int owner) => world.Globals.HumanPlayers.Contains(owner & 31);

    /// <returns>True when the unit was lost, false when it escaped.</returns>
    public bool Try(int slot, int mode, int victim, string call)
    {
        var result = Resolve(slot, mode, victim, call);
        if (result.Incident is { } request)
        { incident(request.Actor, request.Victim); }
        return result.Lost;
    }

    /// <summary>V06 escape/loss prefix for a resumable caller. Complete any returned incident before
    /// reporting mission completion; this method does not replace its human decision with a default.</summary>
    public SpyEscapeResult Resolve(int slot, int mode, int victim, string call)
    {
        var resolution = BeginResolve(slot, mode, victim, call);
        foreach (var check in resolution.Steps())
        { loss.CompleteCheck(check); }
        return resolution.Result!;
    }

    internal sealed class Resolution(SpyEscape parent, int slot, int mode, int victim, string call, bool caught = false)
    {
        public SpyEscapeResult? Result;
        public IEnumerable<CivilizationCheck> Steps() => caught
            ? parent.CaughtSteps(slot, victim, call, this) : parent.ResolveSteps(slot, mode, victim, call, this);
    }

    internal Resolution BeginResolve(int slot, int mode, int victim, string call) => new(this, slot, mode, victim, call);
    internal Resolution BeginCaught(int slot, int victim, string call) => new(this, slot, -1, victim, call, true);
    internal void CompleteCheck(CivilizationCheck check) => loss.CompleteCheck(check);
    internal void CompleteIncident(SpyIncidentRequest request) => incident(request.Actor, request.Victim);

    private IEnumerable<CivilizationCheck> ResolveSteps(int slot, int mode, int victim, string call, Resolution resolution)
    {
        MapKnowledge.RequireLocal(world);
        ArgumentException.ThrowIfNullOrEmpty(call);
        ref var unit = ref world.Entities.Units.Storage[slot];
        int owner = unchecked((sbyte)unit.Owner.Value);
        UnitTarget target = new(new((ushort)slot), unit.Token);
        if (mode >= 0)
        {
            unit.MovesUsed = unchecked((byte)(unit.MovesUsed + rules.Cosmic.RoadMovementMultiplier));
        }
        if (unit.Type.Value == UnitTypeIds.Spy)
        {
            int odds = mode < 0 ? 3 : 2;
            if (unit.Flags.Veteran)
            {
                odds *= 2;
            }

            if (mode > 0)
            {
                odds /= 2;
            }

            if (odds < 2 && random.PrimaryModulo(new ChoiceKey("V06.escape.adjust", call, 0), 2) == 0)
            {
                ++odds;
            }

            int roll = odds <= 1 ? 0 : random.PrimaryModulo(new ChoiceKey("V06.escape.survive", call, 0), odds);
            if (roll != 0)
            {
                var origin = unit.Position;
                string? destination = null;
                if (CityQueries.Nearest(world, origin, owner).Slot is { } city)
                {
                    stacks.Relocate(slot, world.Entities.Cities.At(city).Position, false);
                    UnitMovement.Exhaust(world, rules, ref unit);
                    effects.Redraw(origin);
                    effects.Redraw(world.Entities.Cities.At(city).Position);
                    destination = world.Entities.Cities.At(city).Name;
                }
                if (mode >= 0)
                {
                    if (Human(owner) && !world.Multiplayer.Saved.AutoplayActive && owner == world.Multiplayer.LocalHuman)
                    {
                        effects.Notice(new(target, unit.Type, owner, unit.Flags.Veteran ? SpyFate.VeteranSurvived : SpyFate.Survived, destination));
                    }
                    unit.Flags.Veteran = true;
                }
                resolution.Result = new(false);
                yield break;
            }
            if (mode >= 0 && Human(owner) && !world.Multiplayer.Saved.AutoplayActive && owner == world.Multiplayer.LocalHuman)
            {
                effects.Notice(new(target, unit.Type, owner, SpyFate.Died));
            }
        }
        foreach (var check in loss.DestroyAndRedrawSteps(slot, true))
        { yield return check; }
        resolution.Result = new(true, victim > 0 && mode >= 0 ? new(owner, victim) : null);
    }

    public bool Caught(int slot, int victim, string call)
    {
        var resolution = BeginCaught(slot, victim, call);
        foreach (var check in resolution.Steps())
        { loss.CompleteCheck(check); }
        return resolution.Result!.Lost;
    }

    private IEnumerable<CivilizationCheck> CaughtSteps(int slot, int victim, string call, Resolution resolution)
    {
        MapKnowledge.RequireLocal(world);
        ref var unit = ref world.Entities.Units.Storage[slot];
        var type = unit.Type;
        int owner = unchecked((sbyte)unit.Owner.Value);
        UnitTarget target = new(new((ushort)slot), unit.Token);
        var child = BeginResolve(slot, -1, victim, call);
        foreach (var check in child.Steps())
        { yield return check; }
        if (child.Result!.Lost && Human(owner) && !world.Multiplayer.Saved.AutoplayActive)
        {
            effects.Notice(new(target, type, owner, SpyFate.Caught));
        }
        resolution.Result = child.Result;
    }
}
