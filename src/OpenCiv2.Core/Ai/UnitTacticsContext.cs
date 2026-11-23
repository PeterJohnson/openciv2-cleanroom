using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.Ai;

/// <summary>A04 on-map context capture after barbarian/work-order dispatch.
/// Children retain these values across mutations; later phases explicitly replace mutable fields.</summary>
public sealed class UnitTacticsContext
{
    public UnitSlot Unit { get; }
    public int Owner { get; }
    public int Type { get; }
    public MapPoint Position { get; set; }
    public Terrain Terrain { get; }
    public int Region { get; }
    public CitySlot? NearestCity { get; }
    public CitySlot? HomeCity { get; }
    public CitySlot? CitySelector { get; set; }
    public int NearestDistance { get; }
    public int HomeDistance { get; }
    public int NearestRegion { get; }
    public int HomeRegion { get; }
    public int Role { get; set; }
    public UnitAbilities Abilities { get; }
    public RuleDomain Domain { get; }
    public int Policy { get; }
    public bool EnemyZone { get; set; }
    public int? AdjacentForeignCivilization { get; set; }
    public bool Fortress { get; set; }
    public bool ForceAttack, Hold, Guard, HasOrders, NoMoves, StayHome, SettlerType, RetryTransport;
    public int FortressNeed;

    public UnitTacticsContext(GameState world, RuleTables rules, UnitSlot unit)
    {
        MapKnowledge.RequireLocal(world);
        ref var actor = ref world.Entities.Units.At(unit);
        Unit = unit;
        Owner = actor.Owner.Value;
        Type = actor.Type.Value;
        Position = actor.Position;
        if (!world.Map.Geometry.InBounds(Position))
        { throw new ArgumentOutOfRangeException(nameof(unit), "A04 context requires an on-map actor"); }
        AdjacentForeignCivilization = SpatialControl.InEnemyZone(world, Position, Owner);
        EnemyZone = AdjacentForeignCivilization is not null;
        (NearestCity, NearestDistance) = CityQueries.Nearest(world, Position);
        NearestRegion = NearestCity is { } nearest ? (int?)world.Map.LandRegion(world.Entities.Cities.At(nearest).Position) ?? -1 : -1;
        (HomeCity, HomeDistance) = CityQueries.Nearest(world, Position, Owner);
        HomeRegion = HomeCity is { } home ? (int?)world.Map.LandRegion(world.Entities.Cities.At(home).Position) ?? -1 : -1;
        var tile = world.Map.TileAt(Position);
        Terrain = tile.Terrain;
        Region = tile.Region;
        Fortress = tile.Features.HasFlag(MapFeatures.Fortification) && !tile.Features.HasFlag(MapFeatures.CitySite);
        if ((HomeDistance > 8 || Region != HomeRegion) && !world.Globals.HumanPlayers.Contains(Owner))
        { actor.Flags.Colonist = false; }
        Role = AssessmentQueries.Role(rules, actor);
        Abilities = rules.Units[actor.Type.Value].Abilities;
        Domain = rules.Units[actor.Type.Value].Domain;
        Policy = world.Civilizations[Owner].ContinentPolicy[Region];
    }
}
