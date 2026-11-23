using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private sealed class SpecialDestinationFixture
    {
        public RevelationFixture Data { get; }
        public GameState World => Data.World;
        public EliminationFixture Lifecycle { get; }
        public IncidentFixture Incidents { get; }
        public SpecialUnitDestinations Choices { get; }
        public SpecialDestinationFixture(RevelationFixture? data = null)
        {
            Data = data ?? new(); Lifecycle = new(data: Data); Incidents = new(Lifecycle.Random, Data);
            foreach (ref var tile in World.Map.Tiles) tile.Region = 1;
            World.Globals.HumanPlayers = CivilizationSet.Only(2); World.Multiplayer.LocalHuman = 2;
            World.Civilizations[1].CityCount = 1;
            Data.AddUnit(0, 1, 3, CargoPoint);
            Choices = new(World, Data.Rules, Lifecycle.Loss, Lifecycle.Elimination, Incidents.Incidents);
        }
        public void City(int slot, int owner, MapPoint p, short trade = 24)
        { Data.AddCity(slot, owner, p); World.Entities.Cities.At(new((ushort)slot)).BaseTrade = trade; }
        public UnitTacticsContext Context() => new(World, Data.Rules, new(0));
        public SpecialUnitChoice Run(bool caravan, UnitTacticsContext? c = null)
        {
            using var op = caravan ? Choices.Caravan(c ?? Context(), "special") : Choices.Diplomat(c ?? Context(), "special");
            Equal(null, op.Resume()); if (op.Result is null) throw new InvalidOperationException("Unexpected lifecycle continuation");
            Lifecycle.Random.RequireChoicesConsumed(); return op.Result.Value;
        }
    }
    private static SpecialDestinationFixture CaravanDestinationFixture()
    {
        var f = new SpecialDestinationFixture();
        f.City(0, 1, new(4, 10)); f.City(1, 2, new(18, 10)); f.City(2, 3, new(26, 10), 6);
        f.Data.Unit(0).Home = new(0); return f;
    }

    // A07 §2: existing orders precede landing/home reads; landing uses captured R and first positive tie.
    private static void CaravanDestinationLandingAndOrders()
    {
        var retained = new SpecialDestinationFixture(); retained.Data.Unit(0).Orders = UnitOrder.GoTo;
        retained.Data.Unit(0).Destination = new(30, 10); retained.Data.Unit(0).AiTask = 99;
        Equal(SpecialUnitRoute.Finish, retained.Run(true).Route); Equal(new MapPoint(30, 10), retained.Data.Unit(0).Destination);
        Equal((byte)99, retained.Data.Unit(0).AiTask);
        foreach (bool positive in new[] { false, true })
        {
            var f = new SpecialDestinationFixture();
            foreach (ref var tile in f.World.Map.Tiles) { tile.Terrain = Terrain.Ocean; tile.Region = 0; }
            foreach (var p in new[] { new MapPoint(11, 9), new(12, 10) })
            { f.World.Map.TileAt(p).Terrain = Terrain.Grassland; f.World.Map.TileAt(p).Region = 2; }
            f.World.Civilizations[1].ContinentFlags[2].TransportRequested = true;
            f.World.Civilizations[1].ContinentCitySize[0] = (byte)(positive ? 1 : 0);
            f.World.Civilizations[1].ContinentCitySize[2] = 200;
            var choice = f.Run(true);
            Equal(positive ? SpecialUnitRoute.Direction : SpecialUnitRoute.ReturnOne, choice.Route);
            if (positive) { Equal(0, choice.Direction); Equal((byte)0x74, f.Data.Unit(0).AiTask); Equal(CargoPoint, f.Data.Unit(0).Position); }
            else Equal(false, f.Data.Unit(0).Token.Occupied);
        }
    }

    private static void CaravanDestinationScoresAndRoutes()
    {
        foreach (int variant in Enumerable.Range(0, 5))
        {
            var f = CaravanDestinationFixture();
            if (variant == 1) { f.World.Entities.Cities.At(new(0)).RouteCount = 1; f.World.Entities.Cities.At(new(0)).RoutePartners[0] = new(1); }
            if (variant == 2) f.World.Civilizations[1].Attitude[2] = 255;
            if (variant == 3) f.World.Entities.Cities.At(new(1)).RouteCount = 5;
            if (variant == 4) { f.World.Entities.Cities.At(new(0)).RouteCount = 255; f.World.Civilizations[1].Treaties[2].War = true; }
            Equal(SpecialUnitRoute.Finish, f.Run(true).Route);
            Equal(variant is 1 or 2 or 3 ? new MapPoint(26, 10) : new(18, 10), f.Data.Unit(0).Destination);
            Equal((byte)0x63, f.Data.Unit(0).AiTask); Equal((byte)0, f.Data.Unit(0).MovesUsed);
        }
        var malformed = CaravanDestinationFixture(); malformed.World.Entities.Cities.At(new(0)).RouteCount = 4;
        malformed.World.Entities.Cities.At(new(0)).RoutePartners[0] = new(0);
        using var op = malformed.Choices.Caravan(malformed.Context(), "routes");
        Throws<InvalidOperationException>(() => op.Resume()); Throws<InvalidOperationException>(() => op.Resume());
        Equal(UnitOrder.None, malformed.Data.Unit(0).Orders); // A matched earlier route does not hide an unsupported later read.
    }

    private static void CaravanDestinationNearestAndFallback()
    {
        foreach (bool human in new[] { false, true })
        {
            var f = new SpecialDestinationFixture(); f.City(0, 1, new(2, 10)); f.City(1, 2, new(12, 10)); f.City(2, 3, new(26, 10), 72);
            f.World.Globals.HumanPlayers = human ? CivilizationSet.Only(2) : default; f.Data.Unit(0).Home = new(0);
            Equal(SpecialUnitRoute.Finish, f.Run(true).Route);
            Equal(human ? new MapPoint(12, 10) : new(26, 10), f.Data.Unit(0).Destination);
        }
        foreach (bool actual in new[] { false, true })
        {
            var f = new SpecialDestinationFixture(); f.City(0, 1, new(2, 10), 0);
            if (actual) f.Data.Unit(0).Home = new(0);
            Equal(actual ? SpecialUnitRoute.Finish : SpecialUnitRoute.Continue, f.Run(true).Route);
            Equal(actual ? UnitOrder.GoTo : UnitOrder.None, f.Data.Unit(0).Orders);
        }
        var retained = new SpecialDestinationFixture(); retained.City(0, 1, new(2, 10), 0); var c = retained.Context();
        retained.Data.Unit(0).Home = new(0); retained.World.Entities.Cities.Storage[0].Token = default;
        Equal(SpecialUnitRoute.Finish, retained.Run(true, c).Route); Equal(new MapPoint(2, 10), retained.Data.Unit(0).Destination);
    }

    private static void DiplomatDestinationExistingAndOcean()
    {
        var f = new SpecialDestinationFixture(); f.City(0, 2, new(18, 10));
        f.Data.Unit(0).Orders = UnitOrder.GoTo; f.Data.Unit(0).Destination = new(18, 10); f.Data.Unit(0).AiTask = 99;
        var c = f.Context(); Equal(SpecialUnitRoute.PlanMove, f.Run(false, c).Route); Equal(true, c.HasOrders);
        Equal((byte)99, f.Data.Unit(0).AiTask); // Neither War nor a fresh destination is required.
        foreach (bool intent in new[] { false, true })
        {
            var sea = new SpecialDestinationFixture(); sea.World.Map.TileAt(CargoPoint).Terrain = Terrain.Ocean;
            var sc = sea.Context(); sc.HasOrders = intent;
            Equal(SpecialUnitRoute.PlanMove, sea.Run(false, sc).Route); Equal(intent, sc.HasOrders); Equal(UnitOrder.None, sea.Data.Unit(0).Orders);
        }
    }

    private static void DiplomatDestinationPartitionAndScores()
    {
        foreach (bool road in new[] { false, true })
        {
            var f = new SpecialDestinationFixture(); f.City(0, 2, new(20, 10)); f.City(1, 3, new(16, 10));
            if (road) f.World.Map.TileAt(CargoPoint).Features |= MapFeatures.Road;
            Equal(SpecialUnitRoute.PlanMove, f.Run(false).Route);
            Equal(road ? new MapPoint(16, 10) : new(20, 10), f.Data.Unit(0).Destination); // Absent home 255 shares partition zero.
        }
        foreach (int variant in Enumerable.Range(0, 4))
        {
            var f = new SpecialDestinationFixture(); f.City(0, 2, new(20, 10)); f.City(3, 3, new(18, 12));
            f.World.Civilizations[1].Attitude[2] = 255;
            f.World.Civilizations[1].Treaties[3].War = true;
            if (variant is 1 or 3) f.World.Entities.Cities.At(new(0)).Flags.TechnologyStolen = true;
            if (variant is 2 or 3) f.World.Civilizations[2].NumAdvances = 7;
            Equal(SpecialUnitRoute.PlanMove, f.Run(false).Route);
            Equal(variant == 0 ? new MapPoint(20, 10) : new(18, 12), f.Data.Unit(0).Destination);
        }
    }

    private static void DiplomatDestinationTreatyRaceAndSpy()
    {
        foreach (int variant in Enumerable.Range(0, 7))
        {
            var f = new SpecialDestinationFixture(); f.City(0, 1, new(2, 10)); f.City(1, 2, new(18, 10));
            f.World.Civilizations[1].Treaties[2].Peace = true;
            f.World.Globals.PowerRank[1] = 4; f.World.Globals.PowerRank[2] = 7;
            f.World.Civilizations[1].Government = Government.Republic;
            if (variant is 1 or 4 or 5 or 6) f.Data.Unit(0).Type = new(47);
            if (variant == 2) f.World.Civilizations[2].NumAdvances = 10;
            if (variant is 3 or 4 or 5)
            {
                f.World.Globals.Difficulty = Difficulty.Prince; f.World.Globals.WonderCity[WonderIds.Apollo] = 0;
                f.World.Civilizations[2].SpaceshipFlags.Launched = true;
                f.World.Globals.PowerRank[2] = 0;
            }
            if (variant == 4) f.World.Entities.Cities.At(new(1)).Flags.TechnologyStolen = true;
            if (variant == 5) f.World.Globals.HumanPlayers = CivilizationSet.Only(3);
            if (variant == 6)
            {
                f.World.Civilizations[2].Government = Government.Republic;
                f.World.Civilizations[2].ContinentAttack[1] = 1; f.World.Civilizations[1].ContinentCities[1] = 1;
            }
            var c = f.Context(); Equal(SpecialUnitRoute.PlanMove, f.Run(false, c).Route); Equal(true, c.HasOrders);
            Equal(variant is 1 or 2 or 3 ? new MapPoint(18, 10) : new(2, 10), f.Data.Unit(0).Destination);
        }
    }

    private static void DiplomatDestinationRetainAndDestroy()
    {
        foreach (bool city in new[] { false, true })
        {
            var f = new SpecialDestinationFixture(); if (city) f.City(0, 1, new(2, 10));
            f.Data.Unit(0).Orders = UnitOrder.GoTo; f.Data.Unit(0).Destination = new(30, 10); f.Data.Unit(0).AiTask = 99;
            var c = f.Context(); Equal(city ? SpecialUnitRoute.PlanMove : SpecialUnitRoute.Finish, f.Run(false, c).Route);
            Equal(city, c.HasOrders); Equal(new MapPoint(30, 10), f.Data.Unit(0).Destination); Equal((byte)99, f.Data.Unit(0).AiTask);
        }
        var removed = new SpecialDestinationFixture(); Equal(SpecialUnitRoute.ReturnOne, removed.Run(false).Route);
        Equal(false, removed.Data.Unit(0).Token.Occupied); Equal(1, removed.Lifecycle.Redraws.Count);
        var remote = new SpecialDestinationFixture(); var context = remote.Context(); remote.World.Globals.Multiplayer = GameMode.Tcp;
        using var denied = remote.Choices.Diplomat(context, "remote");
        Throws<InvalidOperationException>(() => denied.Resume()); Equal(true, remote.Data.Unit(0).Token.Occupied);
    }

    private static void SpecialDestinationLifecycleAndSession()
    {
        foreach (bool caravan in new[] { false, true })
            foreach (bool cancel in new[] { false, true })
            {
                using var runtime = new SessionRuntime(new(), new(650));
                var f = new SpecialDestinationFixture(CreationFixture(new(runtime.Process, runtime.World)));
                f.Lifecycle.Recreate = owner => Creation(f.Data, owner, f.Lifecycle.Random);
                f.World.Globals.HumanPlayers = CivilizationSet.Only(1).With(2); f.World.Multiplayer.LocalHuman = 1;
                f.World.Civilizations[1].CityCount = 0; f.World.Civilizations[1].UnitsActive[3] = 1;
                f.Data.Rules.Units[3] = f.Data.Rules.Units[3] with { Role = RuleRole.Settle };
                f.Data.AddUnit(1, 3, 4, new(14, 10)); f.Data.Unit(1).Flags.SpyMission = true; f.Data.Unit(1).Counter = 1;
                f.World.Civilizations[1].Treaties[3].Peace = true;
                ParticipantId actorSeat = default, otherSeat = default;
                runtime.RegisterCommand(new(new(1, "test.special", "Special-unit destination", AccessClass.Player, new([]), new([new("route"), new("direction")])),
                    (_, _, _) => new(), _ => new SpecialUnitDestinationOperation(caravan ? f.Choices.Caravan(f.Context(), "special") : f.Choices.Diplomat(f.Context(), "special"),
                        civ => civ == 1 ? actorSeat : otherSeat)));
                runtime.BeginClientSession(new()); var actor = SessionClient(runtime); var other = SessionClient(runtime, 2);
                actorSeat = actor.Identity.Participant; otherSeat = other.Identity.Participant;
                var command = actor.Submit(new("test.special", new([]))).Correlation.Command; runtime.AdvanceOne();
                var prompt = RequiredInteraction(actor, command); Equal(null, other.Result(command)); Equal(false, other.Reply(prompt, 0).Accepted);
                Equal(false, f.Data.Unit(0).Token.Occupied || f.Data.Unit(1).Token.Occupied);
                if (cancel) Equal(true, actor.Cancel(command).Accepted); else Equal(true, actor.Reply(prompt, 0).Accepted);
                runtime.AdvanceOne(); var result = RequiredResult(actor, command);
                if (result.Status != CommandStatus.Completed) throw new InvalidOperationException($"Special lifecycle: {caravan}, {cancel}, {result}");
                Equal(EndReason.Defeat, f.World.PendingEndReason); Equal(false, f.World.UnitDeletion.DestroyActive);
                f.Lifecycle.Random.RequireChoicesConsumed();
            }
    }
}
