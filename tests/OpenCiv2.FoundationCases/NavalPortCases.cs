using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.Units;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private sealed class NavalPortFixture
    {
        public RevelationFixture Data { get; } = NavalTargetFixture();
        public NavalPortFixture() => Data.Rules.Units[1] = Data.Rules.Units[1] with { Role = RuleRole.SeaTransport, Capacity = 4 };
        public void Port(int slot, MapPoint p, byte region, byte policy = 4, bool coastal = true)
        {
            Data.AddCity(slot, 1, p); Data.World.Entities.Cities.At(new((ushort)slot)).Flags.Coastal = coastal;
            Data.World.Map.TileAt(p).Terrain = Terrain.Grassland; Data.World.Map.TileAt(p).Region = region;
            Data.World.Civilizations[1].ContinentPolicy[region] = policy;
        }
        public NavalTransportContext Context()
        {
            var c = NavalCargoContext(Data);
            return new(c, ScanNavalCargo(Data, c));
        }
        public NavalPortResult Select(NavalTransportContext c, GameplayRandom? random = null) =>
            new NavalPorts(Data.World, Data.Rules, Data.Stacks,
                new(Data.World, Data.Rules, new MovementGoalQueries(Data.World, Data.Rules)), random ?? PortRandom()).Select(c, "ports");
        public int Goal(MapPoint p, int role) => SharedGoals.PriorityAt(Data.World.Civilizations[1], p, (sbyte)role);
    }
    private static GameplayRandom PortRandom(params Choice[] choices) => new(new ControlledChoices(new(choices)), "naval-port");
    private static Choice PortChoice(uint code) => Choice.Create(new("A05.port-fallback", "ports"), new SamplingLaw.SecondaryRange(0, 3), ChoiceCode.Secondary(code));

    private static void NavalPortsTiesAndRecruitment()
    {
        var f = new NavalPortFixture(); var target = new MapPoint(12, 10);
        f.Port(0, target, 1); f.Port(1, new(10, 12), 2);
        var c = f.Context(); c.ExpandNeighbors = true;
        Equal(NavalPortResult.Continue, f.Select(c));
        Equal(new CitySlot(0), c.SelectedPort); Equal(4, c.SelectedPortPolicy); Equal(false, c.ExpandNeighbors);
        Equal(UnitOrder.GoTo, f.Data.Unit(0).Orders); Equal(target, f.Data.Unit(0).Destination);
        Equal((byte)0x33, f.Data.Unit(0).AiTask); Equal((byte)255, f.Data.Unit(0).Counter); Equal((byte)0, f.Data.Unit(0).MovesUsed);
        Equal(true, f.Data.World.Entities.Cities.At(new(0)).Flags.FerryPort); Equal(true, f.Data.World.Entities.Cities.At(new(0)).Flags.FreshPort);
        foreach (int role in new[] { 0, 1, 5, 6 }) Equal(4, f.Goal(target, role));
        Equal(3, f.Goal(target, 21)); Equal(2, f.Goal(target, 7)); Equal(0, f.Goal(new(10, 12), 7));
        Equal(new MapPoint(10, 10), f.Data.Unit(0).Position);
    }

    private static void NavalPortsFlagsConnectivityAndCongestion()
    {
        foreach (int variant in Enumerable.Range(0, 4))
        {
            var f = new NavalPortFixture(); f.Port(0, new(12, 10), 1); f.Port(1, new(16, 10), 2);
            f.Data.World.Entities.Cities.At(new(0)).Flags.FerryPort = variant < 2;
            f.Data.Unit(0).Counter = (byte)(variant == 1 ? 255 : 0);
            if (variant == 2) f.Data.AddUnit(1, 1, 1, new(12, 10));
            if (variant == 3) f.Data.World.Entities.Cities.At(new(0)).Flags.Coastal = false;
            var c = f.Context(); f.Select(c);
            Equal(new CitySlot((ushort)(variant == 1 ? 0 : 1)), c.SelectedPort);
        }
        var isolated = new NavalPortFixture(); isolated.Port(0, new(20, 10), 1); isolated.Port(1, new(12, 10), 2);
        foreach (var offset in MapNeighborhoods.Directions)
        { isolated.Data.World.Map.TileAt(new(20 + offset.X, 10 + offset.Y)).Region = 3; }
        var outside = isolated.Context(); isolated.Select(outside); Equal(new CitySlot(1), outside.SelectedPort);
        var home = new NavalPortFixture(); home.Port(0, new(10, 10), 1); home.Data.World.Globals.Turn = 1;
        var atHome = home.Context(); home.Select(atHome);
        Equal(true, home.Data.World.Entities.Cities.At(new(0)).Flags.FerryPort); // One ship at distance zero has no congestion penalty.
    }

    private static void NavalPortsDemandAndSelectedPolicy()
    {
        foreach (bool shortageFirst in new[] { false, true })
        {
            var f = new NavalPortFixture(); f.Port(0, new(12, 10), 1, shortageFirst ? (byte)1 : (byte)4);
            f.Port(1, new(20, 10), 2, shortageFirst ? (byte)4 : (byte)1);
            int region = shortageFirst ? 1 : 2;
            f.Data.World.Civilizations[1].ContinentCities[region] = 1;
            f.Data.World.Civilizations[1].ContinentTradeUnits[region] = 1;
            var c = f.Context(); f.Select(c);
            Equal(new CitySlot(0), c.SelectedPort); Equal(!shortageFirst, c.CargoNeeded);
            Equal(shortageFirst ? 1 : 4, c.SelectedPortPolicy);
            Equal(shortageFirst ? 0 : 4, f.Goal(new(12, 10), 5)); Equal(2, f.Goal(new(12, 10), 7));
        }
        var attack = new NavalPortFixture(); attack.Port(0, new(12, 10), 1); attack.Port(1, new(20, 10), 2);
        attack.Data.World.Civilizations[1].ContinentAttack[2] = 28; // Weighted distance 7 minus 28/4 beats the nearer port's 1.
        var scored = attack.Context(); attack.Select(scored); Equal(new CitySlot(1), scored.SelectedPort);
        var rejection = new NavalPortFixture(); rejection.Port(0, new(12, 10), 1, 1);
        rejection.Data.World.Civilizations[1].ContinentCities[1] = 1;
        var rejected = rejection.Context(); rejection.Select(rejected);
        Equal(null, rejected.SelectedPort); Equal(false, rejected.CargoNeeded); Equal(UnitOrder.None, rejection.Data.Unit(0).Orders);
    }

    private static void NavalPortsCargoThresholds()
    {
        foreach (int variant in Enumerable.Range(0, 4))
        {
            var f = new NavalPortFixture(); f.Port(0, new(12, 10), 1);
            if (variant is 1 or 3)
            { f.Port(1, new(10, 10), 2, 0, false); f.Data.World.Globals.Turn = 1; }
            if (variant != 1) f.Data.AddUnit(1, 1, 0, new(10, 10));
            else f.Data.Rules.Units[1] = f.Data.Rules.Units[1] with { Capacity = 255 };
            if (variant == 2)
            {
                f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Role = RuleRole.Settle };
                f.Data.Unit(0).Orders = UnitOrder.GoTo;
            }
            var c = f.Context(); c.ExpandNeighbors = true;
            c.Room = variant == 2;
            f.Select(c);
            Equal(variant >= 2 ? new CitySlot(0) : null, c.SelectedPort);
            Equal(variant != 3, c.ExpandNeighbors);
        }
    }

    private static void NavalPortsFallbackDistanceAndFinish()
    {
        foreach (byte policy in new byte[] { 1, 5 })
        {
            var f = new NavalPortFixture(); f.Port(0, new(10, 10), 1, policy, false); f.Port(1, new(12, 10), 2);
            f.Data.World.Globals.Turn = 2; f.Data.World.Civilizations[1].ContinentMilitary[1] = 3;
            f.Data.World.Civilizations[1].ContinentCities[1] = 1;
            f.Data.Rules.Units[5] = f.Data.Rules.Units[0] with { Role = RuleRole.Settle };
            f.Data.AddUnit(1, 1, 5, new(10, 10)); f.Data.AddUnit(2, 1, 5, new(10, 10));
            var c = f.Context(); Equal(0, c.Tactics.NearestDistance);
            var random = PortRandom(); Equal(NavalPortResult.Hold, f.Select(c, random)); random.RequireChoicesConsumed();
            Equal(2, c.Tactics.NearestDistance); Equal(new CitySlot(0), c.WorkingCity); Equal(new CitySlot(1), c.SelectedPort);
            Equal(new MapPoint(12, 10), f.Data.Unit(0).Destination); Equal(4, f.Goal(new(10, 10), 0));
            Equal(2, f.Goal(new(10, 10), 7)); Equal(0, f.Goal(new(10, 10), 5));
            Equal(policy == 5 ? 4 : 0, f.Goal(new(12, 10), 6)); Equal(0, f.Goal(new(10, 10), 6));
            Equal(0, TacticOrders(f.Data).Direction(c.Tactics, 8));
            Equal(UnitOrder.None, f.Data.Unit(0).Orders); Equal((byte)6, f.Data.Unit(0).MovesUsed);
        }
    }

    private static void NavalPortsFallbackDrawAndMissingPolicy()
    {
        foreach (int variant in Enumerable.Range(0, 4))
        {
            var f = new NavalPortFixture(); f.Port(0, new(10, 10), 1, 0, false); f.Data.World.Globals.Turn = 1;
            var c = f.Context(); if (variant == 2) c.SelectedPortPolicy = 4;
            f.Data.World.Civilizations[1].ContinentFlags[1].StrongerEnemy = variant == 3;
            var random = PortRandom(PortChoice(variant == 0 ? 0 : 0x80000000));
            if (variant == 1)
            {
                Throws<InvalidOperationException>(() => f.Select(c, random));
                Equal(true, f.Data.World.Entities.Cities.At(new(0)).Flags.FerryPort);
                Equal(true, c.CargoNeeded); Equal(0, f.Goal(new(10, 10), 5));
            }
            else
            {
                Equal(variant == 2 ? NavalPortResult.Hold : NavalPortResult.Continue, f.Select(c, random));
                Equal(variant == 2, f.Data.World.Entities.Cities.At(new(0)).Flags.FerryPort);
                Equal(variant == 2 ? 4 : 0, f.Goal(new(10, 10), 5));
            }
            random.RequireChoicesConsumed();
        }
    }

    private static void NavalPortsActualMovement()
    {
        var f = new OrderFixture(); var d = f.Data;
        d.World.Globals.HumanPlayers = CivilizationSet.Only(2); d.World.Multiplayer.LocalHuman = 2;
        d.Unit(0).Type = new(1); d.Unit(0).Orders = UnitOrder.None;
        d.Rules.Units[1] = d.Rules.Units[1] with { Role = RuleRole.SeaTransport, Capacity = 4 };
        d.World.Map.TileAt(new(18, 12)).Terrain = Terrain.Ocean;
        int slot = d.World.Entities.Cities.End; d.AddCity(slot, 1, new(20, 12));
        d.World.Entities.Cities.At(new((ushort)slot)).Flags.Coastal = true;
        d.World.Map.TileAt(new(20, 12)).Region = 1; d.World.Civilizations[1].ContinentPolicy[1] = 4;
        var c = NavalCargoContext(d); var state = new NavalTransportContext(c, ScanNavalCargo(d, c));
        var random = PortRandom();
        var ports = new NavalPorts(d.World, d.Rules, d.Stacks, new(d.World, d.Rules, new MovementGoalQueries(d.World, d.Rules)), random);
        Equal(NavalPortResult.Continue, ports.Select(state, "ports")); Equal(new MapPoint(20, 12), d.Unit(0).Destination);
        using var move = f.Begin(); f.Complete(move);
        Equal(1, move.MovementResult); Equal(new MapPoint(20, 12), d.Unit(0).Position); Equal(true, d.Unit(0).Flags.GoalReached);
        Equal(UnitOrder.None, d.Unit(0).Orders); random.RequireChoicesConsumed();
    }
}
