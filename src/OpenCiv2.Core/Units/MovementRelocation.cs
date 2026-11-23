using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Units;

/// <summary>U02 caller-owned presentation and exceptional temporary inputs. Shift is a cosmetic
/// input, not a gameplay callback. Missing backing is rejected only when consumed.</summary>
public sealed record MovementViewerInputs(bool ShiftDown, int Unanimated = 0,
    CivilizationSet? SavedHumans = null, int? SeenZero = null, int? AnimateZero = null,
    Relationship? AbsentDestinationTreaty = null);
public sealed record MovementLandingNotice(int Recipient, int MoverCivilization, CityTarget City, string CityName);
public sealed record MovementSlide(UnitTarget Unit, MapPoint Origin, MapPoint Destination, int Direction);
public sealed record MovementRelocationResult(bool HasHut, bool LocalSeen, bool LocalAnimated,
    int Unanimated, IReadOnlyList<Exception> PresentationFailures);
public interface IMovementRelocationEffects
{
    void MoveCursor(MapPoint point);
    void EnsureOriginVisible(MapPoint point);
    void Landing(MovementLandingNotice notice);
    void Publish();
    void Redraw(MapPoint point);
    /// <summary>Optional MOVPIECE sound and animation; failure never cancels actual relocation.</summary>
    void Slide(MovementSlide movement);
}

/// <summary>U02 Movement publication and viewers, Landing alert and local Authority and relocation.
/// Admission/charge precede this service; ordinary arrival and common finish follow it.</summary>
public sealed class MovementRelocation(GameState world, RuleTables rules, StackOperations stacks,
    UnitCargo cargo, IMovementRelocationEffects effects)
{
    public MovementRelocationResult Apply(UnitSlot slot, MovementStep step, bool originOcean, int cost, MovementViewerInputs inputs)
    {
        MapKnowledge.RequireLocal(world);
        var failures = new List<Exception>();
        void Present(Action action)
        { try { action(); } catch (Exception error) { failures.Add(error); } }
        ref var unit = ref world.Entities.Units.At(slot);
        int observer = world.Multiplayer.LocalHuman;
        if (observer < -1 || observer >= EntityLimits.Civilizations)
        { throw new InvalidOperationException("U02 movement viewer requires supplied backing for this observer"); }
        if (step.Owner == 0 && step.DestinationTerrain != Terrain.Ocean && world.Map.TileAt(step.From).RevealedTo.Contains(observer)
            && !unit.Visibility.Contains(observer) && unchecked((sbyte)unit.Owner.Value) != observer)
        {
            var nearest = CityQueries.Nearest(world, step.From).Slot;
            if (nearest is { } city && world.Entities.Cities.At(city).Owner.Value == observer)
            { unit.Visibility = unit.Visibility.Union(world.Map.TileAt(step.From).RevealedTo); }
        }
        if (step.City is not null && step.DestinationOwner is { } destinationOwner && destinationOwner != step.Owner
            && world.Map.TileAt(step.To).RevealedTo.Contains(observer))
        {
            if (observer < 0)
            { throw new InvalidOperationException("U02 city publication requires a backed observer treaty row"); }
            if (world.Civilizations[observer].Treaties[step.Owner].Embassy
                || world.Civilizations[observer].Treaties[destinationOwner].Embassy || observer == destinationOwner)
            { MapKnowledge.MarkUnitSeen(world, slot, observer); }
        }
        bool seen = false, animate = false;
        int unanimated = inputs.Unanimated;
        if (observer == 0)
        {
            seen = (inputs.SeenZero ?? throw new InvalidOperationException("U02 seen entry zero requires an explicit input")) != 0;
            animate = (inputs.AnimateZero ?? throw new InvalidOperationException("U02 animate entry zero requires an explicit input")) != 0;
        }
        else if (observer > 0 && world.Globals.CivsAlive.Contains(observer)
            && (world.Globals.HumanPlayers.Contains(observer) || world.Multiplayer.Saved.AutoplayActive
                && (inputs.SavedHumans ?? throw new InvalidOperationException("U02 autoplay viewers require saved human membership")).Contains(observer)))
        {
            seen = unit.Visibility.Contains(observer) || unit.Owner.Value == observer || step.Owner == observer || world.Globals.RevealMap != 0;
            for (int i = 0; i < MapLimits.Directions && !seen; ++i)
            {
                var point = MapNeighborhoods.Offset(world.Map.Geometry, step.To, MapNeighborhoods.Directions[i], world.Globals.Rules.FlatWorld);
                seen = world.Map.Geometry.InBounds(point) && world.Map.CityOrUnitOwner(point) == observer;
            }
            if (seen && (observer == step.Owner || world.Globals.GameOptions.EnemyMoves || step.City is not null))
            {
                animate = !inputs.ShiftDown;
                if ((cost == 0 || inputs.ShiftDown || observer == step.Owner && step.Domain == RuleDomain.Air)
                    && (observer != step.Owner || unit.Orders == UnitOrder.GoTo))
                {
                    animate = false;
                    unanimated = observer == step.Owner ? 2 : 1;
                    if (observer == step.Owner)
                    { Present(() => effects.MoveCursor(step.To)); }
                }
            }
        }
        // Local observer -1 selects the initially false hut flag and seen[7] (zero in local play).
        if (seen)
        {
            Present(() => effects.EnsureOriginVisible(step.From));
            if (originOcean && step.DestinationTerrain != Terrain.Ocean && observer != step.Owner && step.Owner != 0
                && rules.Units[unit.Type.Value].Domain == RuleDomain.Land)
            {
                ref var count = ref world.Civilizations[step.Owner].ContinentMilitary[world.Map.TileAt(step.To).Region];
                if (count == 0)
                { ++count; }
                var treaty = step.DestinationOwner is { } owner ? world.Civilizations[step.Owner].Treaties[owner]
                    : inputs.AbsentDestinationTreaty ?? throw new InvalidOperationException("U02 landing requires explicit absent-owner treaty backing");
                if (!treaty.Ceasefire && !treaty.Peace)
                {
                    var nearest = CityQueries.Nearest(world, step.From).Slot;
                    if (nearest is { Value: > 0 } city && world.Entities.Cities.At(city).Owner.Value == observer && !world.Multiplayer.Saved.AutoplayActive)
                    {
                        var record = world.Entities.Cities.At(city);
                        effects.Landing(new(observer, step.Owner, new(city, record.Token), record.Name));
                    }
                }
            }
        }
        effects.Publish();
        cargo.Lift(slot.Value, false);
        if (animate)
        { Present(() => effects.Slide(new(new(slot, world.Entities.Units.At(slot).Token), step.From, step.To, step.Direction))); }
        else if (seen)
        { Present(() => effects.Redraw(step.From)); }
        stacks.ClearVisibility(slot.Value);
        if (step.DestinationTerrain != Terrain.Ocean)
        { stacks.MarkSeen(slot.Value, step.DestinationOwner ?? -1); }
        bool hut = world.Map.Hut(step.To);
        stacks.RelocateStack(slot.Value, step.To, false);
        effects.Publish();
        return new(hut, observer == -1 ? hut : seen, animate, unanimated, failures.AsReadOnly());
    }
}
