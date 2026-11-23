using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private sealed class MovementChargeFixture
    {
        public RevelationFixture Data { get; } = new();
        public GameState World => Data.World;
        public RuleTables Rules => Data.Rules;
        public ref Unit Unit => ref Data.Unit(0);
        public GameplayRandom Random { get; }
        public MovementCharge Charge { get; }
        public MovementStep Step { get; set; } = new(1, RuleDomain.Land, new(10, 10), new(12, 10), Terrain.Grassland, 1);
        public MovementChargeFixture(List<Choice>? choices = null)
        {
            Data.AddUnit(0, 1, 0, Step.From);
            Rules.Units[0] = Rules.Units[0] with { Movement = 6, Attack = 3, Defense = 1, HitPoints = 10, Role = RuleRole.Attack };
            Rules.Terrain[(int)Terrain.Grassland] = Rules.Terrain[(int)Terrain.Grassland] with { MovementCost = 1 };
            Random = new(new ControlledChoices(new(choices ?? [])), "charge");
            Charge = new(World, Rules, Data.Stacks, Random);
        }
        public MovementChargeResult Apply(bool prompt = true, bool execute = true, bool localHuman = true) =>
            Charge.Apply(new(0), Step, new(prompt, execute), localHuman, "charge");
        public void Ai() { World.Globals.HumanPlayers = CivilizationSet.Only(2); World.Multiplayer.LocalHuman = 2; }
        public void Tile(MapPoint point, Terrain terrain, MapFeatures features, byte? owner = null)
        { ref var tile = ref World.Map.TileAt(point); tile.Terrain = terrain; tile.Features = features; tile.Owner = owner; }
    }
    private static Choice MovementRoll(int cost, int roll, string call = "charge") =>
        Choice.Create(new("U02.partial-movement", call), new SamplingLaw.PrimaryModulo(cost), ChoiceCode.Primary(roll));

    private static void MovementCivilizationControl()
    {
        var f = new MovementChargeFixture(); var w = f.World; var origin = new MapPoint(10, 10);
        f.Tile(new(11, 9), Terrain.Ocean, MapFeatures.Unit, 2);
        Equal(null, SpatialControl.AdjacentNonalliedCivilization(w, origin, 1));
        f.Tile(new(11, 9), Terrain.Ocean, MapFeatures.CitySite, 1);
        f.Tile(new(12, 10), Terrain.Ocean, MapFeatures.Unit, 2);
        Equal(2, SpatialControl.AdjacentNonalliedCivilization(w, origin, 1));
        // A friendly city bridges to Ocean; a later friendly land city bridges back.
        f.Tile(new(12, 10), Terrain.Grassland, MapFeatures.Unit, 2);
        Equal(null, SpatialControl.AdjacentNonalliedCivilization(w, origin, 1));
        f.Tile(new(11, 11), Terrain.Grassland, MapFeatures.CitySite, 1);
        f.Tile(new(10, 12), Terrain.Grassland, MapFeatures.Unit, 3);
        Equal(3, SpatialControl.AdjacentNonalliedCivilization(w, origin, 1));
        w.Civilizations[1].Treaties[3].Alliance = true; Equal(null, SpatialControl.AdjacentNonalliedCivilization(w, origin, 1));
        f.Tile(new(11, 9), Terrain.Ocean, MapFeatures.CitySite, 0); Equal(0, SpatialControl.AdjacentNonalliedCivilization(w, origin, 1));
        w.Civilizations[1].Treaties[0].Peace = true; Equal(0, SpatialControl.AdjacentNonalliedCivilization(w, origin, 1));
        // An origin city accepts either neighbor terrain. An airbase is not a city owner.
        f = new(); f.Tile(new(11, 9), Terrain.Ocean, MapFeatures.Unit, 2);
        f.Tile(origin, Terrain.Grassland, MapFeatures.CitySite, 1); Equal(2, SpatialControl.AdjacentNonalliedCivilization(f.World, origin, 1));
        f.Tile(origin, Terrain.Grassland, MapFeatures.CitySite | MapFeatures.Fortification, 1); Equal(null, SpatialControl.AdjacentNonalliedCivilization(f.World, origin, 1));
        f = new(); f.World.Globals.Rules.FlatWorld = false; f.Tile(new(39, 9), Terrain.Grassland, MapFeatures.Unit, 2);
        Equal(2, SpatialControl.AdjacentNonalliedCivilization(f.World, new(0, 10), 1));
        f.World.Globals.Rules.FlatWorld = true; Equal(null, SpatialControl.AdjacentNonalliedCivilization(f.World, new(0, 10), 1));
    }

    private static void MovementStepCostRules()
    {
        var f = new MovementChargeFixture(); MapPoint from = new(10, 10), to = new(11, 9);
        int Cost(RuleDomain domain = RuleDomain.Land, Terrain terrain = Terrain.Grassland) => UnitMovement.StepCost(f.World, f.Rules, f.Unit, domain, from, to, terrain);
        f.Rules.Terrain[(int)Terrain.Grassland] = f.Rules.Terrain[(int)Terrain.Grassland] with { MovementCost = 4 };
        Equal(12, Cost()); Equal(3, Cost(RuleDomain.Sea)); Equal(3, Cost(RuleDomain.Air));
        foreach (var left in new[] { MapFeatures.CitySite, MapFeatures.Railroad, MapFeatures.CitySite | MapFeatures.Fortification })
            foreach (var right in new[] { MapFeatures.CitySite, MapFeatures.Railroad, MapFeatures.CitySite | MapFeatures.Fortification })
            { f.Tile(from, Terrain.Grassland, left); f.Tile(to, Terrain.Grassland, right); Equal(0, Cost()); }
        f.Tile(from, Terrain.Grassland, MapFeatures.CitySite); f.Tile(to, Terrain.Grassland, MapFeatures.Road); Equal(1, Cost());
        f.Tile(from, Terrain.Grassland, MapFeatures.None); f.Tile(to, Terrain.Grassland, MapFeatures.None);
        f.Rules.Units[0] = f.Rules.Units[0] with { Abilities = new() { Alpine = true } }; Equal(1, Cost());
        f.Rules.Units[0] = f.Rules.Units[0] with { Abilities = new() }; f.World.Map.TileAt(from).River = f.World.Map.TileAt(to).River = true; Equal(1, Cost());
        to = new(12, 10); f.World.Map.TileAt(to).River = true; Equal(12, Cost());
        to = new(10, 12); f.World.Map.TileAt(to).River = true; Equal(12, Cost());
        from = new(0, 10); to = new(39, 9); f.World.Globals.Rules.FlatWorld = false;
        f.World.Map.TileAt(from).River = f.World.Map.TileAt(to).River = true; Equal(1, Cost());
        f.World.Globals.Rules.FlatWorld = true; Equal(12, Cost());
        f.World.Map.TileAt(to).River = false; f.World.Map.TileAt(to).Terrain = Terrain.Ocean;
        Equal(12, Cost()); // Captured terrain survives a live tile-terrain change.
        f.Rules.Terrain[(int)Terrain.Grassland] = f.Rules.Terrain[(int)Terrain.Grassland] with { MovementCost = 128 };
        f.Rules.Cosmic = f.Rules.Cosmic with { RoadMovementMultiplier = 255 }; Equal(-32640, Cost());
        f.Rules.Units[0] = f.Rules.Units[0] with { Domain = RuleDomain.Air, Abilities = new() { Alpine = true } };
        Equal(1, Cost()); Equal(255, Cost(RuleDomain.Air)); // Captured domain, live ability.
    }

    private static void MovementChargeLotteryAndPreview()
    {
        foreach (int roll in new[] { 0, 1, 2 })
            foreach (bool execute in new[] { false, true })
            {
                var f = new MovementChargeFixture([MovementRoll(3, roll)]); f.Unit.MovesUsed = 5;
                var result = f.Apply(execute: execute); Equal(roll == 0 ? execute ? MovementChargeOutcome.Proceed : MovementChargeOutcome.Preview : MovementChargeOutcome.PartialFailure, result.Outcome);
                Equal((byte)(roll == 0 ? execute ? 8 : 5 : 6), f.Unit.MovesUsed);
                Equal(roll == 0 && execute ? 0 : 1, result.ReturnValue); Equal(f.Step.From, f.Unit.Position); f.Random.RequireChoicesConsumed();
            }
        foreach (bool prompt in new[] { false, true })
            foreach (bool execute in new[] { false, true })
                foreach (bool human in new[] { false, true })
                    foreach (int local in new[] { 1, 2 })
                        foreach (bool indicator in new[] { false, true })
                        {
                            bool lottery = prompt && (indicator || local == 1) || execute && !human;
                            var f = new MovementChargeFixture(lottery ? [MovementRoll(3, 2)] : []); f.Unit.MovesUsed = 5;
                            f.World.Globals.HumanPlayers = CivilizationSet.Only(human ? 1 : 2); f.World.Multiplayer.LocalHuman = local;
                            Equal(lottery ? MovementChargeOutcome.PartialFailure : execute ? MovementChargeOutcome.Proceed : MovementChargeOutcome.Preview,
                                f.Apply(prompt, execute, indicator).Outcome); f.Random.RequireChoicesConsumed();
                        }
        var fresh = new MovementChargeFixture(); fresh.Rules.Terrain[(int)Terrain.Grassland] = fresh.Rules.Terrain[(int)Terrain.Grassland] with { MovementCost = 4 };
        Equal(MovementChargeOutcome.Proceed, fresh.Apply().Outcome); Equal((byte)12, fresh.Unit.MovesUsed); fresh.Random.RequireChoicesConsumed();
        var spent = new MovementChargeFixture(); spent.Unit.MovesUsed = 6;
        Equal(MovementChargeOutcome.NoMovement, spent.Apply().Outcome); Equal((byte)6, spent.Unit.MovesUsed); spent.Random.RequireChoicesConsumed();
        var sea = new MovementChargeFixture(); sea.Step = sea.Step with { Domain = RuleDomain.Sea }; sea.Unit.MovesUsed = 5;
        Equal(MovementChargeOutcome.Proceed, sea.Apply().Outcome); Equal((byte)8, sea.Unit.MovesUsed); sea.Random.RequireChoicesConsumed();
    }

    private static void MovementChargeAiApproach()
    {
        foreach (bool diplomat in new[] { false, true })
            foreach (int variant in Enumerable.Range(0, 8))
            {
                var f = new MovementChargeFixture(); f.Ai(); f.Unit.MovesUsed = 2; f.Unit.Orders = UnitOrder.GoTo; f.Unit.Destination = new(14, 10);
                if (diplomat) { f.Rules.Units[0] = f.Rules.Units[0] with { Role = RuleRole.Diplomacy }; f.Unit.MovesUsed = 3; }
                if (variant == 0) f.Unit.LastDirection = 5; // Approach wins before reversal/exhaustion.
                if (variant == 1) { f.Unit.Destination = new(20, 10); f.Unit.Visibility = CivilizationSet.Only(2); }
                if (variant == 2) { f.Unit.Destination = new(20, 10); f.Data.AddCity(0, 2, new(14, 10)); }
                if (variant == 3) { f.Unit.Destination = new(20, 10); f.Unit.Visibility = CivilizationSet.Only(2); f.Data.AddCity(0, 2, new(8, 10)); }
                if (variant == 4) f.Unit.MovesUsed = 0;
                if (variant == 5) f.Unit.Orders = UnitOrder.None;
                if (variant == 6) { f.Unit.MovesUsed = 1; }
                if (variant == 7) { f.Rules.Units[0] = f.Rules.Units[0] with { Role = RuleRole.Defend }; }
                var result = f.Apply(); Equal(variant < 3 ? MovementChargeOutcome.Approach : MovementChargeOutcome.Proceed, result.Outcome);
                Equal((byte)(variant == 4 ? 3 : variant == 6 ? 4 : diplomat ? 6 : 5), f.Unit.MovesUsed);
                Equal(f.Step.From, f.Unit.Position); f.Random.RequireChoicesConsumed();
            }
        foreach (byte defense in new byte[] { 1, 2, 128 })
        {
            var f = new MovementChargeFixture(); f.Ai(); f.Unit.MovesUsed = 2; f.Unit.Orders = UnitOrder.GoTo; f.Unit.Destination = new(14, 10);
            f.Rules.Units[0] = f.Rules.Units[0] with { Attack = 3, Defense = defense };
            Equal(defense == 2 ? MovementChargeOutcome.Proceed : MovementChargeOutcome.Approach, f.Apply().Outcome);
        }
    }

    private static void MovementChargeAiReversalAndCrowding()
    {
        foreach (int count in new[] { 1, 2, 3 })
            foreach (RuleDomain domain in new[] { RuleDomain.Land, RuleDomain.Sea, RuleDomain.Air })
            {
                var f = new MovementChargeFixture(); f.Ai(); f.Unit.MovesUsed = 1; f.Unit.LastDirection = 5;
                f.Rules.Units[0] = f.Rules.Units[0] with { Domain = domain };
                for (int i = 1; i < count; ++i) f.Data.AddUnit(i, 1, 0, f.Step.From);
                bool denied = count < 3 && domain != RuleDomain.Air;
                Equal(denied ? MovementChargeOutcome.Reversal : MovementChargeOutcome.Proceed, f.Apply().Outcome);
                Equal((byte)(denied ? 6 : 4), f.Unit.MovesUsed); f.Random.RequireChoicesConsumed();
            }
        foreach (int old in new[] { 1, 3 })
            foreach (int count in new[] { 1, 2, 3, 4 })
                foreach (int exemption in Enumerable.Range(0, 5))
                {
                    var f = new MovementChargeFixture(); f.Ai(); f.Unit.MovesUsed = (byte)old;
                    for (int i = 1; i <= count; ++i) f.Data.AddUnit(i, 1, 0, f.Step.To);
                    f.Step = f.Step with { DestinationUnit = new(1), DestinationOwner = 1 };
                    if (exemption == 1) f.Step = f.Step with { City = new(0) };
                    if (exemption == 2) f.Step = f.Step with { Domain = RuleDomain.Air };
                    if (exemption == 3) f.Rules.Units[0] = f.Rules.Units[0] with { Role = RuleRole.Diplomacy };
                    if (exemption == 4) f.Step = f.Step with { DestinationOwner = 2 };
                    bool denied = exemption == 0 && count >= (old == 3 ? 2 : 4);
                    Equal(denied ? MovementChargeOutcome.Crowding : MovementChargeOutcome.Proceed, f.Apply().Outcome);
                    Equal((byte)(denied ? 6 : old + 3), f.Unit.MovesUsed); f.Random.RequireChoicesConsumed();
                }
        var signed = new MovementChargeFixture(); signed.Ai(); signed.Unit.MovesUsed = 1; signed.Unit.LastDirection = 255;
        signed.Step = signed.Step with { Direction = -5 }; Equal(MovementChargeOutcome.Reversal, signed.Apply().Outcome);
    }

    private static void MovementChargeFailureAndLandJoin()
    {
        var missing = new MovementChargeFixture(); missing.Unit.MovesUsed = 5;
        Throws<ChoiceException>(() => missing.Apply()); Equal((byte)5, missing.Unit.MovesUsed); Equal(missing.Step.From, missing.Unit.Position);
        var remote = new MovementChargeFixture(); remote.World.Globals.Multiplayer = GameMode.Tcp;
        Throws<InvalidOperationException>(() => remote.Apply()); Equal((byte)0, remote.Unit.MovesUsed);
        var negative = new MovementChargeFixture(); negative.Unit.MovesUsed = 1;
        negative.Rules.Terrain[(int)Terrain.Grassland] = negative.Rules.Terrain[(int)Terrain.Grassland] with { MovementCost = 255 };
        Equal(MovementChargeOutcome.Proceed, negative.Apply().Outcome); Equal((byte)254, negative.Unit.MovesUsed); negative.Random.RequireChoicesConsumed();
        foreach (int roll in new[] { 0, 2 })
        {
            var f = new MovementChargeFixture([MovementRoll(3, roll, "land")]); f.Unit.MovesUsed = 5;
            var result = LandMovement.Step(f.World, f.Rules, new(0), 1, f.Random, "land");
            Equal(roll == 0 ? "moved" : "movement_spent", result.Code); Equal(true, result.Changed);
            Equal(roll == 0 ? f.Step.To : f.Step.From, f.Unit.Position); Equal((byte)(roll == 0 ? 8 : 6), f.Unit.MovesUsed);
            Equal(roll == 0, f.Unit.Flags.Moved); f.Random.RequireChoicesConsumed();
        }
        var rails = new MovementChargeFixture(); rails.Tile(rails.Step.From, Terrain.Grassland, MapFeatures.Unit | MapFeatures.Railroad, 1);
        rails.Tile(rails.Step.To, Terrain.Grassland, MapFeatures.Railroad); rails.Unit.MovesUsed = 5;
        Equal("moved", LandMovement.Step(rails.World, rails.Rules, new(0), 1, rails.Random).Code);
        Equal((byte)5, rails.Unit.MovesUsed); rails.Random.RequireChoicesConsumed();
    }
}
