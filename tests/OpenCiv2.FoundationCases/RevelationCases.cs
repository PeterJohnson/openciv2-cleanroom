using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private sealed class RevelationFixture : IUnitRevelationEffects, IUnitCreationEffects, IUnitDeletionEffects
    {
        public ProcessContext Process { get; } = new();
        public GameState World { get; }
        public RuleTables Rules => Process.Rules.Definitions;
        public StackOperations Stacks { get; }
        public List<(int Actor, int Other, MapPoint Position)> Meetings { get; } = [];
        public List<MapPoint> PublishedOrigins { get; } = [];
        public List<UnitCreationNotice> Notices { get; } = [];
        public int Synchronizations;
        public List<MapPoint> Removed { get; } = [];
        public Action<int, int, MapPoint>? OnMeet;
        public Action<int>? OnReveal;
        public Action<UnitCreationNotice>? OnNotice;

        public RevelationFixture()
        {
            World = new(Process);
            World.Map.Allocate(new(40, 24));
            World.Globals.Rules = new() { FlatWorld = true, NormalCombat = true };
            World.Globals.HumanPlayers = CivilizationSet.Only(1);
            World.Multiplayer.LocalHuman = 1;
            foreach (ref var tile in World.Map.Tiles)
            {
                tile.Terrain = Terrain.Grassland;
            }
            foreach (ref var civ in World.Civilizations.AsSpan())
            {
                foreach (ref var goal in (Span<CivilizationGoal>)civ.Goals)
                {
                    goal = new(new(-1, -1), -1, 0);
                }
            }
            for (int i = 0; i < Rules.WonderObsolescence.Length; ++i)
            {
                Rules.WonderObsolescence[i] = AdvanceReference.Never;
                World.Globals.WonderCity[i] = -1;
            }
            Rules.Cosmic = Rules.Cosmic with { RoadMovementMultiplier = 3 };
            Rules.Units[0] = new() { Domain = RuleDomain.Land, Role = RuleRole.Attack, Movement = 6, HitPoints = 10, Attack = 1 };
            Rules.Units[1] = Rules.Units[0] with { Domain = RuleDomain.Sea };
            Rules.Units[2] = Rules.Units[0] with { Domain = RuleDomain.Air };
            Rules.Units[3] = Rules.Units[0] with { Role = RuleRole.Trade };
            Rules.Units[9] = Rules.Units[0];
            Stacks = new(World, new(5_000_000, 64));
        }

        public ref Unit Unit(int slot) => ref World.Entities.Units.Storage[slot];

        public void AddUnit(int slot, int owner, int type, MapPoint point)
        {
            World.Entities.Units.SetEnd(Math.Max(World.Entities.Units.End, slot + 1));
            Unit(slot) = new() { Token = new((uint)slot + 1), Owner = new((byte)owner), Type = new((byte)type), Position = new(-1, -1) };
            Stacks.AttachAtFront(slot, point, false);
        }

        public void AddCity(int slot, int owner, MapPoint point, int size = 4)
        {
            World.Entities.Cities.SetEnd(Math.Max(World.Entities.Cities.End, slot + 1));
            World.Entities.Cities.Storage[slot] = new()
            {
                Token = new((uint)slot + 1),
                Owner = new((byte)owner),
                Position = point,
                Size = (sbyte)size
            };
            World.Map.TileAt(point).Features |= MapFeatures.CitySite;
            World.Map.TileAt(point).Owner = (byte)owner;
        }

        public int Priority(int owner, MapPoint point, RuleRole role) =>
            SharedGoals.PriorityAt(World.Civilizations[owner], point, (sbyte)role);

        public void Reveal(int slot = 0, bool allowContact = true) =>
            new UnitRevelation(World, Rules, Stacks, this).Reveal(slot, allowContact);

        public UnitSlot? Create(int type, int owner, MapPoint point) =>
            new UnitCreation(World, Process, Stacks, this).Create(new((byte)type), new((byte)owner), point);

        public void RevealOrigin(MapPoint position) => PublishedOrigins.Add(position);

        public void Meet(int actingCivilization, int otherCivilization, MapPoint position)
        {
            Meetings.Add((actingCivilization, otherCivilization, position));
            OnMeet?.Invoke(actingCivilization, otherCivilization, position);
        }

        public void RevealSurroundings(int slot, bool allowContact)
        {
            if (OnReveal is { } reveal)
            {
                reveal(slot);
            }
            else
            {
                Reveal(slot, allowContact);
            }
        }

        public void Notice(UnitCreationNotice notice)
        {
            Notices.Add(notice);
            OnNotice?.Invoke(notice);
        }

        public void SynchronizeCreation() => ++Synchronizations;

        public void UnitRemoved(MapPoint position) => Removed.Add(position);
        public void RefreshHomeCity(int city) { }
        public void CheckCivilization(int owner, int killer) => Equal(0, owner);
        public void SynchronizeDeletion(MapPoint position, CitySlot? home) => ++Synchronizations;
    }

    private static void VisibilityKnowledgeChannels()
    {
        var f = new RevelationFixture();
        var world = f.World;
        MapPoint point = new(10, 10);
        world.Map.TileAt(point).Features = MapFeatures.Road;
        Equal(true, MapKnowledge.Remember(world, 1, point));
        Equal(false, world.Map.TileAt(point).RevealedTo.Contains(1));
        Equal(false, MapKnowledge.Remember(world, 0, point));
        f.AddCity(0, 2, point);
        f.AddCity(1, 3, point);
        Equal(new CitySlot(0), CityQueries.At(world, point)!.Value);
        Equal((CitySlot?)null, CityQueries.At(world, new(11, 10)));
        world.Map.TileAt(point).Features = MapFeatures.CitySite | MapFeatures.Fortification; // Airbase, not city.
        Equal((CitySlot?)null, CityQueries.At(world, point));
        world.Map.TileAt(point).Features = MapFeatures.CitySite;
        world.Map.TileAt(point).Owner = null;
        Equal((CitySlot?)null, CityQueries.At(world, point));
        world.Map.TileAt(point).Owner = 7; // Tile ownership is only the gate.
        Equal(new CitySlot(0), CityQueries.At(world, point)!.Value);
        world.Entities.Cities.Storage[0].Token = new(0);
        world.Entities.Cities.Storage[0].Size = 0;
        Equal(true, MapKnowledge.RevealCity(world, 1, new(0)));
        Equal(true, world.Entities.Cities.Storage[0].KnownTo.Contains(1));
        Equal((byte)0, world.Entities.Cities.Storage[0].RememberedSize[1]);
        Equal(false, world.Map.TileAt(point).RevealedTo.Contains(1));
        Equal(false, MapKnowledge.RevealCity(world, -1, new(ushort.MaxValue)));
        Equal(new CitySlot(1), CityQueries.At(world, point)!.Value);
    }

    private static void VisibilityTileAndStack()
    {
        var f = new RevelationFixture();
        MapPoint point = new(12, 10), origin = new(10, 10);
        f.AddUnit(0, 2, 0, point);
        f.AddUnit(1, 2, 1, point);
        f.Rules.Units[1] = f.Rules.Units[1] with { Abilities = new() { Submarine = true } };
        Equal(true, MapKnowledge.RevealTile(f.World, f.Stacks, 1, point));
        Equal(true, f.Unit(0).Visibility.Contains(1));
        Equal(true, f.Unit(1).Visibility.Contains(1));
        Equal(false, f.Unit(1).Visibility.Contains(2));
        Equal(false, f.Stacks.MarkSeen(0, 2));
        Equal(false, MapKnowledge.RevealTile(f.World, f.Stacks, -1, new(-100, -100)));
        Equal(0, f.Meetings.Count);

        f.Unit(0).Visibility = f.Unit(1).Visibility = default;
        f.AddCity(0, 2, point);
        Equal(true, MapKnowledge.RevealTile(f.World, f.Stacks, 1, point));
        Equal(false, f.Unit(1).Visibility.Contains(1));
        Equal(true, f.World.Entities.Cities.Storage[0].KnownTo.Contains(1));
        Equal(false, MapKnowledge.RevealTile(f.World, f.Stacks, 1, point));
        f.World.Map.TileAt(point).Features = MapFeatures.Unit;
        f.World.Map.TileAt(point).Terrain = Terrain.Ocean;
        Equal(false, MapKnowledge.RevealAdjacentUnits(f.World, f.Stacks, 1, origin));
        f.World.Map.TileAt(point).Terrain = Terrain.Grassland;
        f.World.Map.TileAt(point).Owner = null;
        Equal(false, MapKnowledge.RevealAdjacentUnits(f.World, f.Stacks, 1, origin));
        f.World.Map.TileAt(point).Owner = 2;
        Equal(true, MapKnowledge.RevealAdjacentUnits(f.World, f.Stacks, 1, origin));
        Equal(true, f.Unit(0).Visibility.Contains(1));
    }

    private static void VisibilitySharedOrders()
    {
        var f = new RevelationFixture();
        MapPoint point = new(10, 10);
        f.AddUnit(0, 1, 0, point);
        f.AddUnit(1, 1, 1, point);
        f.Unit(0).Orders = f.Unit(1).Orders = UnitOrder.Sleeping;
        f.Unit(0).Carrier = new(7);
        f.Unit(0).Destination = new(7, 12);
        f.World.Map.TileAt(point).Terrain = Terrain.Ocean;
        UnitOrders.WakeSentries(f.World, f.Rules, f.Stacks, 0);
        Equal(UnitOrder.Sleeping, f.Unit(0).Orders);
        Equal(UnitOrder.None, f.Unit(1).Orders);
        f.World.Map.TileAt(point).Terrain = Terrain.Grassland;
        UnitOrders.WakeSentries(f.World, f.Rules, f.Stacks, 0);
        Equal(UnitOrder.None, f.Unit(0).Orders);
        Equal(new UnitSlot(7), f.Unit(0).Carrier!.Value);
        Equal(new MapPoint(7, 12), f.Unit(0).Destination);
        f.Unit(0).AuxiliaryTimer = 9;
        f.Unit(0).Flags.Automated = true;
        UnitOrders.SetGoto(ref f.Unit(0), 300, new(65535, 65538));
        Equal(new MapPoint(-1, 2), f.Unit(0).Destination);
        Equal((byte)44, f.Unit(0).AiTask);
        Equal((byte)9, f.Unit(0).AuxiliaryTimer);
        foreach (int order in new[] { 11, 27, 43, -117 })
        {
            f.Unit(0).Orders = (UnitOrder)order;
            UnitOrders.InterruptGoto(ref f.Unit(0), f.Rules);
            Equal(UnitOrder.None, f.Unit(0).Orders);
        }
        f.Unit(0).Orders = UnitOrder.AiGoTo;
        f.Unit(0).Type = new(3);
        UnitOrders.InterruptGoto(ref f.Unit(0), f.Rules);
        Equal(UnitOrder.AiGoTo, f.Unit(0).Orders);
        Equal(true, f.Unit(0).Flags.Automated);
        Equal(new UnitSlot(7), f.Unit(0).Carrier!.Value);
    }

    private static void VisibilityFootprint()
    {
        foreach (var domain in new[] { RuleDomain.Land, RuleDomain.Sea, RuleDomain.Air })
        {
            var f = new RevelationFixture();
            f.Rules.Units[0] = f.Rules.Units[0] with { Domain = domain, Abilities = new() { SeeTwo = true } };
            f.AddUnit(0, 1, 0, new(10, 10));
            f.World.Map.TileAt(new(12, 10)).Terrain = Terrain.Ocean; // Does not block the outer land tile.
            f.World.Map.TileAt(new(6, 10)).Terrain = Terrain.Ocean;
            f.World.Map.TileAt(new(9, 7)).Features = MapFeatures.Road;
            f.Reveal();
            Equal(true, f.World.Map.TileAt(new(12, 10)).RevealedTo.Contains(1));
            Equal(domain != RuleDomain.Sea, f.World.Map.TileAt(new(14, 10)).RevealedTo.Contains(1));
            Equal(domain != RuleDomain.Land, f.World.Map.TileAt(new(6, 10)).RevealedTo.Contains(1));
            Equal(false, f.World.Map.TileAt(new(13, 7)).RevealedTo.Contains(1)); // Outside B[0..24].
            Equal(domain == RuleDomain.Sea ? MapFeatures.None : MapFeatures.Road, f.World.Map.Remembered(1, new(9, 7)));
            Equal(false, f.Unit(0).Visibility.Contains(1));
        }
        var barbarian = new RevelationFixture();
        barbarian.AddUnit(0, 0, 0, new(10, 10));
        barbarian.AddUnit(1, 0, 0, new(10, 10));
        barbarian.World.Map.TileAt(new(10, 10)).RevealedTo = CivilizationSet.Only(3);
        barbarian.Reveal(0);
        Equal(true, barbarian.Unit(0).Visibility.Contains(3));
        Equal(false, barbarian.Unit(1).Visibility.Contains(3));
        Equal(true, barbarian.World.Map.TileAt(new(10, 10)).RevealedTo.Contains(0));
        Equal(false, barbarian.World.Map.TileAt(new(14, 10)).RevealedTo.Contains(0));
    }

    private static void VisibilityAdjacentCities()
    {
        var f = new RevelationFixture();
        MapPoint origin = new(10, 10), neighbor = new(12, 10);
        f.AddUnit(0, 1, 0, origin);
        f.AddCity(0, 2, neighbor);
        f.World.Civilizations[1].Treaties[2].Peace = true; // City goals read the reverse direction.
        f.Reveal();
        Equal(true, f.World.Entities.Cities.Storage[0].KnownTo.Contains(1));
        Equal(true, f.Unit(0).Visibility.Contains(2));
        Equal(4, f.Priority(2, neighbor, RuleRole.Attack));
        Equal(2, f.Priority(2, neighbor, RuleRole.Defend));
        Equal(1, f.Meetings.Count);
        Equal(1, f.PublishedOrigins.Count);
        f.AddUnit(1, 2, 0, neighbor);
        f.Unit(1).Orders = UnitOrder.Sleeping;
        f.Reveal();
        Equal(false, f.Unit(1).Visibility.Contains(1)); // Garrison is hidden but still wakes.
        Equal(UnitOrder.None, f.Unit(1).Orders);
        Equal(2, f.Meetings.Count);

        var hidden = new RevelationFixture();
        hidden.Rules.Units[0] = hidden.Rules.Units[0] with { Abilities = new() { Submarine = true } };
        hidden.AddUnit(0, 1, 0, origin);
        hidden.AddCity(0, 2, neighbor);
        hidden.Reveal();
        Equal(true, hidden.World.Entities.Cities.Storage[0].KnownTo.Contains(1));
        Equal(false, hidden.Unit(0).Visibility.Contains(2));
        Equal(4, hidden.Priority(2, neighbor, RuleRole.Attack));
        Equal(0, hidden.Meetings.Count);

        foreach (int type in new[] { 2, 9 }) // Empty-city contact has no land-domain or Partisans exclusion.
        {
            var empty = new RevelationFixture();
            empty.AddUnit(0, 1, type, origin);
            empty.World.Map.TileAt(neighbor).Features = MapFeatures.CitySite;
            empty.World.Map.TileAt(neighbor).Owner = 2; // No city record is required by this branch.
            empty.Reveal();
            Equal(1, empty.Meetings.Count);
            empty.World.Civilizations[1].Treaties[2].Contact = true;
            empty.Reveal();
            Equal(1, empty.Meetings.Count);
        }
    }

    private static void VisibilityAdjacentDetection()
    {
        MapPoint origin = new(10, 10), neighbor = new(12, 10);
        var f = new RevelationFixture();
        f.AddUnit(0, 1, 1, origin);
        f.World.Map.TileAt(origin).Terrain = f.World.Map.TileAt(neighbor).Terrain = Terrain.Ocean;
        f.Rules.Units[0] = f.Rules.Units[0] with { Domain = RuleDomain.Sea, Abilities = new() { Submarine = true } };
        f.AddUnit(1, 2, 0, neighbor);
        f.Unit(0).Orders = UnitOrder.GoTo;
        f.Reveal();
        Equal(false, f.Unit(1).Visibility.Contains(1));
        Equal(UnitOrder.GoTo, f.Unit(0).Orders);
        Equal(true, f.Unit(0).Visibility.Contains(2));
        f.AddUnit(2, 2, 0, neighbor);
        f.Reveal();
        Equal(true, f.Unit(1).Visibility.Contains(1));
        Equal(UnitOrder.None, f.Unit(0).Orders);
        Equal(3, f.Priority(1, neighbor, RuleRole.NavalSuperiority));
        Equal(0, f.Meetings.Count);

        var land = new RevelationFixture();
        land.AddUnit(0, 1, 0, origin);
        land.AddUnit(1, 2, 0, neighbor);
        land.Unit(1).Orders = UnitOrder.Sleeping;
        land.World.Civilizations[1].Treaties[2].Alliance = true;
        land.Reveal(allowContact: false);
        Equal(UnitOrder.Sleeping, land.Unit(1).Orders);
        Equal(0, land.Meetings.Count);
        Equal(1, land.PublishedOrigins.Count);
        land.Unit(0).Type = new(9);
        land.Reveal();
        Equal(0, land.Meetings.Count);
        Equal(3, land.Priority(1, neighbor, RuleRole.Attack));
        land.AddCity(0, 1, origin);
        land.Unit(0).Visibility = default;
        land.Reveal();
        Equal(false, land.Unit(0).Visibility.Contains(2));
    }

    private static void VisibilityContactContinuation()
    {
        var f = new RevelationFixture();
        MapPoint origin = new(10, 10), first = new(11, 9), second = new(12, 10);
        f.AddUnit(0, 1, 0, origin);
        f.AddUnit(1, 2, 0, first);
        f.AddUnit(2, 3, 0, second);
        f.OnMeet = (actor, other, point) =>
        {
            Equal(true, f.Unit(0).Visibility.Contains(other));
            Equal(true, f.World.Map.TileAt(new(10, 12)).RevealedTo.Contains(1));
            Equal(1, f.PublishedOrigins.Count);
            f.World.Civilizations[actor].Treaties[other].Peace = true;
            f.Rules.Units[0] = f.Rules.Units[0] with { Role = RuleRole.Diplomacy };
        };
        f.Reveal();
        Equal(2, f.Meetings.Count);
        Equal(first, f.Meetings[0].Position);
        Equal(second, f.Meetings[1].Position);
        Equal(0, f.Priority(2, first, RuleRole.Defend)); // Live role after the meeting.
        Equal(0, f.Priority(1, first, RuleRole.Attack)); // Live treaty after the meeting.
        f.OnMeet = null;
        f.World.Globals.RevealMap = 1;
        f.PublishedOrigins.Clear();
        f.World.Civilizations[1].Treaties[2].Contact = true;
        f.Reveal();
        Equal(4, f.Meetings.Count); // Unit contact does not use the Contact gate.
        Equal(0, f.PublishedOrigins.Count);
    }

    private static void VisibilityOuterObservers()
    {
        var f = new RevelationFixture();
        MapPoint origin = new(10, 10), far = new(14, 10);
        f.AddUnit(0, 1, 0, origin);
        f.AddUnit(1, 2, 0, far);
        f.Rules.Units[0] = f.Rules.Units[0] with { Abilities = new() { SeeTwo = true } };
        f.Unit(1).Orders = UnitOrder.Sleeping;
        f.World.Civilizations[2].Treaties[1].Alliance = true;
        f.Reveal();
        Equal(true, f.Unit(1).Visibility.Contains(1));
        Equal(true, f.Unit(0).Visibility.Contains(2));
        Equal(UnitOrder.None, f.Unit(1).Orders); // Far observer wakes even across Alliance.
        Equal(2, f.Priority(2, origin, RuleRole.NavalSuperiority)); // Deliberate non-Ocean condition.
        Equal(0, f.Meetings.Count);

        var city = new RevelationFixture();
        city.AddUnit(0, 1, 0, origin);
        city.AddCity(0, 2, far);
        city.World.Map.TileAt(origin).RevealedTo = CivilizationSet.Only(2);
        city.Reveal();
        Equal(true, city.Unit(0).Visibility.Contains(2));
        Equal(false, city.World.Entities.Cities.Storage[0].KnownTo.Contains(1));
        city.Unit(0).Visibility = default;
        city.Rules.Units[0] = city.Rules.Units[0] with { Attack = 0 };
        city.Reveal();
        Equal(false, city.Unit(0).Visibility.Contains(2));

        var hidden = new RevelationFixture();
        hidden.AddUnit(0, 1, 0, origin);
        hidden.AddUnit(1, 2, 1, far);
        hidden.AddUnit(2, 2, 1, far);
        hidden.Rules.Units[0] = hidden.Rules.Units[0] with { Abilities = new() { SeeTwo = true, DetectSubmarines = true } };
        hidden.Rules.Units[1] = hidden.Rules.Units[1] with { Domain = RuleDomain.Land, Abilities = new() { Submarine = true } };
        hidden.Reveal();
        Equal(false, hidden.Unit(1).Visibility.Contains(1)); // Detection/multiple members do not expose far submarines.
    }
}
