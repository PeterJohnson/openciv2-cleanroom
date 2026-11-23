using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Units;

/// <summary>U02 presentation inputs. SavedHumans is the real autoplay membership captured by its owner;
/// Shift changes animation only. Undefined original viewer/treaty memory is not a production input.</summary>
public sealed record MovementViewerInputs(bool ShiftDown, int Unanimated = 0,
    CivilizationSet? SavedHumans = null);
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
        { throw new InvalidOperationException("U02 local movement observer must be absent or a civilization slot"); }
        if (observer > 0 && step.Owner == 0 && step.DestinationTerrain != Terrain.Ocean && world.Map.TileAt(step.From).RevealedTo.Contains(observer)
            && !unit.Visibility.Contains(observer) && unchecked((sbyte)unit.Owner.Value) != observer)
        {
            var nearest = CityQueries.Nearest(world, step.From).Slot;
            if (nearest is { } city && world.Entities.Cities.At(city).Owner.Value == observer)
            { unit.Visibility = unit.Visibility.Union(world.Map.TileAt(step.From).RevealedTo); }
        }
        if (observer > 0 && step.City is not null && step.DestinationOwner is { } destinationOwner && destinationOwner != step.Owner
            && world.Map.TileAt(step.To).RevealedTo.Contains(observer))
        {
            if (world.Civilizations[observer].Treaties[step.Owner].Embassy
                || world.Civilizations[observer].Treaties[destinationOwner].Embassy || observer == destinationOwner)
            { MapKnowledge.MarkUnitSeen(world, slot, observer); }
        }
        bool seen = false, animate = false;
        int unanimated = inputs.Unanimated;
        // U02's supported repair: no normal observer means no viewer or animation.
        if (observer > 0 && world.Globals.CivsAlive.Contains(observer)
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
        if (seen)
        {
            Present(() => effects.EnsureOriginVisible(step.From));
            if (originOcean && step.DestinationTerrain != Terrain.Ocean && observer != step.Owner && step.Owner != 0
                && rules.Units[unit.Type.Value].Domain == RuleDomain.Land)
            {
                ref var count = ref world.Civilizations[step.Owner].ContinentMilitary[world.Map.TileAt(step.To).Region];
                if (count == 0)
                { ++count; }
                // An unowned destination has no treaty partner. Preserve the early count,
                // and apply treaty suppression only to an actual captured owner.
                bool protectedByTreaty = step.DestinationOwner is { } owner
                    && (world.Civilizations[step.Owner].Treaties[owner].Ceasefire || world.Civilizations[step.Owner].Treaties[owner].Peace);
                if (!protectedByTreaty)
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
        return new(hut, seen, animate, unanimated, failures.AsReadOnly());
    }
}
