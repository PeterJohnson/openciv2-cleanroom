using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Units;

public enum UnitOrderRefusal { CannotDo, CaravanHome }
public sealed record UnitPlayerOrderResult(UnitOrderRefusal? Refusal, IReadOnlyList<Exception> PresentationFailures)
{
    public bool Accepted => Refusal is null;
}
public interface IUnitPlayerOrderEffects
{
    void Redraw(MapPoint position);
    void UnitChanged(UnitSlot unit);
    /// <summary>Absent home remains absent; a classic presentation adapter may encode it as 255.</summary>
    void RefreshHome(CitySlot? city);
}

/// <summary>U04 player order mutations and U01 explicit home affiliation. Session owns actor
/// admission and subsequent scheduling. Queuing Fortify is distinct from applying fortification;
/// sleeping does not exhaust, and Home without a city invokes actual recovery search.</summary>
public sealed class UnitPlayerOrders(GameState world, RuleTables rules, StackOperations stacks, IUnitPlayerOrderEffects effects)
{
    private ref Unit Unit(UnitSlot slot) => ref world.Entities.Units.At(slot);
    private UnitRule Rule(UnitSlot slot) => rules.Units[Unit(slot).Type.Value];
    private static void Present(List<Exception> failures, Action action)
    { try { action(); } catch (Exception error) { failures.Add(error); } }

    public UnitPlayerOrderResult Fortify(UnitSlot slot)
    {
        MapKnowledge.RequireLocal(world);
        var point = Unit(slot).Position;
        if (world.Map.TileAt(point).Ocean || Rule(slot).Domain == RuleDomain.Air
            && world.Map.CityOwner(point) is null && world.Map.AirbaseOwner(point) is null)
        { return new(UnitOrderRefusal.CannotDo, Array.Empty<Exception>()); }
        Unit(slot).Orders = UnitOrder.Fortifying;
        UnitMovement.Exhaust(world, rules, ref Unit(slot));
        List<Exception> failures = [];
        Present(failures, () => effects.Redraw(point));
        Present(failures, () => effects.UnitChanged(slot));
        return new(null, failures.AsReadOnly());
    }

    public UnitPlayerOrderResult Sleep(UnitSlot slot)
    {
        MapKnowledge.RequireLocal(world);
        var point = Unit(slot).Position;
        if (Rule(slot).Domain == RuleDomain.Air)
        {
            bool permitted = false;
            if (world.Map.TileAt(point).Ocean)
            {
                permitted = stacks.CountAircraftCarriers(slot.Value, rules) != 0;
                if (!permitted && Rule(slot).Abilities.DestroyedAfterAttack)
                {
                    for (int current = stacks.First(slot.Value); current >= 0; current = stacks.Next(current))
                    {
                        if (rules.Units[world.Entities.Units.Storage[current].Type.Value].Abilities.Submarine)
                        { permitted = true; }
                    }
                }
            }
            if (!permitted && world.Map.CityOwner(point) is null)
            { return new(UnitOrderRefusal.CannotDo, Array.Empty<Exception>()); }
        }
        if (Rule(slot).Abilities.CoastBound)
        {
            bool coast = false;
            for (int direction = 0; direction <= MapLimits.Directions; direction++)
            {
                var neighbor = direction == MapLimits.Directions ? point : MapNeighborhoods.Offset(world.Map.Geometry, point,
                    MapNeighborhoods.Directions[direction], world.Globals.Rules.FlatWorld);
                if (world.Map.Geometry.InBounds(neighbor) && !world.Map.TileAt(neighbor).Ocean)
                { coast = true; }
            }
            if (!coast)
            { return new(UnitOrderRefusal.CannotDo, Array.Empty<Exception>()); }
        }
        UnitOrders.SetSleep(ref Unit(slot));
        return new(null, Array.Empty<Exception>());
    }

    public UnitPlayerOrderResult Home(UnitSlot slot)
    {
        MapKnowledge.RequireLocal(world);
        var city = CityQueries.At(world, Unit(slot).Position);
        if (city is null)
        {
            new UnitRecovery(world, rules).GoHome(slot);
            return new(null, Array.Empty<Exception>());
        }
        if (Rule(slot).Role == RuleRole.Trade && Unit(slot).Home is not null)
        { return new(UnitOrderRefusal.CaravanHome, Array.Empty<Exception>()); }
        var old = Unit(slot).Home;
        Unit(slot).Home = city;
        List<Exception> failures = [];
        Present(failures, () => effects.RefreshHome(old));
        Present(failures, () => effects.RefreshHome(city));
        return new(null, failures.AsReadOnly());
    }

    /// <summary>Wait commits before actual next-unit selection and any retained revelation/contact.</summary>
    public UnitSelection.Operation Wait(UnitSelection selection, MapPoint cursor, string call)
    {
        MapKnowledge.RequireLocal(world);
        ArgumentException.ThrowIfNullOrEmpty(call);
        var slot = world.Globals.ActiveUnit ?? throw new InvalidOperationException("Wait requires an active unit");
        Unit(slot).Flags.Waiting = true;
        return selection.BeginNext(cursor, 0, world.Multiplayer.LocalHuman, call);
    }

    public void BeginAutomation()
    {
        MapKnowledge.RequireLocal(world);
        var slot = world.Globals.ActiveUnit ?? throw new InvalidOperationException("Automation requires an active unit");
        if (Rule(slot).Role == RuleRole.Settle)
        { Unit(slot).Flags.Automated = true; }
    }

    /// <summary>U04 End Player Turn only exhausts live local units. It does not run movement
    /// completion, change phase state, or advance either turn counter.</summary>
    public static void EndPlayerTurn(GameState world, RuleTables rules)
    {
        MapKnowledge.RequireLocal(world);
        for (int i = 0; i < world.Entities.Units.End; i++)
        {
            ref var unit = ref world.Entities.Units.Storage[i];
            if (unit.Token.Occupied && unit.Owner.Value == world.Multiplayer.LocalHuman)
            { UnitMovement.Exhaust(world, rules, ref unit); }
        }
    }
}
