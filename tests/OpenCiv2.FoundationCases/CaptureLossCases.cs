using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private sealed class CaptureLossFixture
    {
        public EliminationFixture Lifecycle { get; }
        public RevelationFixture Data => Lifecycle.Data;
        public GameState World => Data.World;
        public IncidentFixture Incidents { get; }
        public CityRemoval Removal { get; }
        public CapturePopulationLoss Population { get; }
        public List<CityTarget> Removed { get; } = [];
        public bool FailRemoved;
        public CaptureLossFixture(List<Choice>? choices = null, RevelationFixture? data = null)
        {
            Lifecycle = new(choices, data); Incidents = new(Lifecycle.Random, Data);
            Data.AddCity(0, 2, new(10, 10), 1); World.Civilizations[2].CityCount = 1;
            World.Globals.WorstHumanRank = 1; World.Globals.PowerRank[1] = 1; World.Globals.PowerRank[3] = 3;
            Removal = new(World, Data.Rules, Data.Stacks, Lifecycle.Loss, target =>
            { Removed.Add(target); if (FailRemoved) throw new InvalidOperationException("City removal publication failed"); });
            Population = new(World, Lifecycle.Random, Removal, Lifecycle.Elimination, Incidents.Incidents);
        }
        public CapturePopulationLoss.Operation Begin(int newOwner = 3, MapPoint? center = null) =>
            Population.Begin(new(0), newOwner, 2, center ?? new(10, 10), "population");
        public CivilizationElimination.Operation Support(int owner = 2) => Lifecycle.Elimination.BeginSupportedLoss(Removal, 0, owner, "support", Incidents.Incidents);
    }
    private static Choice PopulationChoice(int roll) => Choice.Create(new("C09.capture.size-one", "population"),
        new SamplingLaw.PrimaryModulo(2), ChoiceCode.Primary(roll));

    // C09 exceptional size-one preservation: membership/knowledge/rank gates and the retained selector-as-index.
    private static void CapturePopulationExceptionalGates()
    {
        foreach (var (gate, roll, survives) in new[] { (0, 0, true), (0, 1, false), (1, -1, false),
            (2, -1, false), (3, -1, false), (4, -1, false), (5, -1, true) })
        {
            var f = new CaptureLossFixture(roll < 0 ? [] : [PopulationChoice(roll)]); var w = f.World;
            if (gate == 1) w.Globals.HumanPlayers = w.Globals.HumanPlayers.With(3);
            if (gate == 2) w.Globals.HumanPlayers = w.Globals.HumanPlayers.With(2);
            if (gate == 3) w.Entities.Cities.Storage[0].KnownTo = CivilizationSet.Only(1);
            if (gate == 4) w.Globals.PowerRank[3] = 7;
            if (gate == 5) w.Globals.PowerRank[1] = 3;
            // No alive filter: human seat 1 is removed from the alive mask.
            w.Globals.CivsAlive = CivilizationSet.Only(2).With(3);
            using var op = f.Begin(); op.Resume(); Equal(true, op.Completed);
            Equal(survives ? new CitySlot(0) : (CitySlot?)null, op.RemainingCity);
            Equal(survives, w.Entities.Cities.Storage[0].Token.Occupied); Equal(survives ? 0 : 1, f.Removed.Count);
            Equal((sbyte)(survives ? 1 : 0), w.Entities.Cities.Storage[0].Size); f.Lifecycle.Random.RequireChoicesConsumed();
            op.Resume(); Equal(survives ? 0 : 1, f.Removed.Count);
        }
        foreach (bool humanOnRegion in new[] { false, true })
        {
            var f = new CaptureLossFixture(humanOnRegion ? [PopulationChoice(0)] : []); var w = f.World;
            w.Globals.HumanPlayers = CivilizationSet.Only(2); w.Globals.CivsAlive = CivilizationSet.Only(3);
            w.Globals.PowerRank[0] = 3; w.Map.TileAt(new(8, 8)).Region = 1; w.Map.TileAt(new(10, 10)).Region = 2;
            w.Civilizations[2].ContinentCities[1] = humanOnRegion ? (byte)1 : (byte)0;
            using var op = f.Begin(0, new(8, 8)); op.Resume(); Equal(true, op.Completed);
            Equal(humanOnRegion, op.RemainingCity is not null); f.Lifecycle.Random.RequireChoicesConsumed();
        }
    }

    private static void CapturePopulationSignedSizesAndDeletion()
    {
        foreach (var (size, expected, deleted) in new[] { (-128, 127, false), (-1, -2, false), (0, -1, false), (1, 0, true), (2, 1, false), (127, 126, false) })
        {
            var f = new CaptureLossFixture(); f.World.Entities.Cities.Storage[0].Size = (sbyte)size;
            f.World.Civilizations[2].TotalCitySize = 47; f.World.Globals.WonderCity[0] = 0;
            f.World.Entities.Cities.Storage[0].FoodBox = 29;
            using var op = f.Begin(1); op.Resume(); Equal(true, op.Completed); Equal((sbyte)expected, f.World.Entities.Cities.Storage[0].Size);
            Equal(deleted, op.RemainingCity is null); Equal((short)47, f.World.Civilizations[2].TotalCitySize);
            Equal((short)29, f.World.Entities.Cities.Storage[0].FoodBox); Equal((short)(deleted ? -2 : 0), f.World.Globals.WonderCity[0]);
            Equal((short)(deleted ? 0 : 1), f.World.Civilizations[2].CityCount); Equal((byte?)2, f.World.Map.Owner(new(10, 10)));
            Equal(!deleted, f.World.Map.TileAt(new(10, 10)).City); Equal(false, f.World.CityDeletionActive); f.Lifecycle.Random.RequireChoicesConsumed();
        }
        var zero = new CaptureLossFixture(); zero.World.Entities.Cities.Storage[0].Size = 0; zero.World.Globals.PowerRank[1] = 3;
        using var preservedZero = zero.Begin(); preservedZero.Resume(); Equal(true, preservedZero.Completed);
        Equal(null, preservedZero.RemainingCity); Equal((sbyte)0, zero.World.Entities.Cities.Storage[0].Size); zero.Lifecycle.Random.RequireChoicesConsumed();
    }

    private static CaptureLossFixture CaptureNestedLoss(RevelationFixture? data = null)
    {
        var f = new CaptureLossFixture(data: data); var w = f.World;
        w.Globals.HumanPlayers = CivilizationSet.Only(1).With(3);
        f.Data.Rules.Units[46] = f.Data.Rules.Units[4] with { Role = RuleRole.Settle };
        f.Data.AddUnit(0, 3, 46, new(14, 10)); f.Data.Unit(0).Home = new(0);
        w.Civilizations[3].UnitsActive[46] = 1;
        f.Data.AddUnit(1, 4, 4, new(24, 10)); f.Data.Unit(1).Flags.SpyMission = true; f.Data.Unit(1).Counter = 3;
        w.Civilizations[3].Treaties[4].Peace = true;
        f.Lifecycle.Recreate = c => Creation(f.Data, c, f.Lifecycle.Random);
        f.Data.AddCity(1, 5, new(20, 20)); w.Entities.Cities.Storage[1].RouteCount = 1; TradeRoutes.Write(w, new(1), 0, 0, 2);
        w.Globals.WonderCity[0] = 0;
        return f;
    }

    // Actual C01 city deletion keeps its route/wonder/spatial tail behind nested foreign last-settler incidents.
    private static void CapturePopulationNestedDeletion()
    {
        var f = CaptureNestedLoss(); using var op = f.Begin(1); var pending = op.Resume()!;
        Equal(3, pending.Victim.Civilization); Equal(false, op.Completed); Equal(new CitySlot(0), op.RemainingCity);
        Equal((sbyte)0, f.World.Entities.Cities.Storage[0].Size); Equal(true, f.World.Entities.Cities.Storage[0].Token.Occupied);
        Equal(true, f.World.CityDeletionActive && f.World.Map.TileAt(new(10, 10)).City);
        Equal((byte)1, f.World.Entities.Cities.Storage[1].RouteCount); Equal((short)0, f.World.Globals.WonderCity[0]); Equal(0, f.Removed.Count);
        Equal(false, f.Data.Unit(0).Token.Occupied || f.Data.Unit(1).Token.Occupied); Equal(0, f.Lifecycle.Checks);
        Equal(pending, op.Resume()); op.Resume(0); Equal(true, op.Completed); Equal(null, op.RemainingCity);
        Equal(false, f.World.Entities.Cities.Storage[0].Token.Occupied || f.World.CityDeletionActive);
        Equal((byte)0, f.World.Entities.Cities.Storage[1].RouteCount); Equal((short)-2, f.World.Globals.WonderCity[0]); Equal(1, f.Removed.Count);
        f.Lifecycle.Random.RequireChoicesConsumed();
    }

    // C09 selects only the retained old owner; C01 deletion's default continues to scan every owner.
    private static void CaptureSupportedUnitOwnerAndRestart()
    {
        foreach (bool human in new[] { false, true })
        {
            var f = new CaptureLossFixture(); var w = f.World; w.Entities.Cities.Storage[0].Owner = new(1);
            if (human) w.Globals.HumanPlayers = w.Globals.HumanPlayers.With(2);
            f.Data.AddCity(1, 3, new(14, 6)); f.Data.AddCity(2, 2, new(16, 8));
            f.Data.Rules.Units[4] = f.Data.Rules.Units[4] with { Role = RuleRole.Defend };
            f.Data.Rules.Units[5] = f.Data.Rules.Units[4] with { Role = RuleRole.Attack };
            f.Data.AddUnit(0, 2, 9, new(10, 10)); f.Data.AddUnit(1, 2, 4, new(14, 6));
            f.Data.AddUnit(2, 2, 4, new(16, 8)); f.Data.AddUnit(3, 2, 4, new(16, 8));
            f.Data.AddUnit(4, 1, 4, new(12, 12)); f.Data.AddUnit(5, 2, 5, new(14, 6)); f.Data.AddUnit(6, 3, 5, new(22, 12));
            for (int i = 0; i < 7; ++i) f.Data.Unit(i).Home = new(0);
            w.Civilizations[2].UnitsActive[4] = 3; w.Civilizations[2].UnitsActive[5] = 1; w.UnitTotals[2] = 5;
            using var op = f.Support(); Equal(null, op.Resume()); Equal(1, op.Result);
            Equal(null, f.Data.Unit(0).Home); Equal(true, f.Data.Unit(0).Token.Occupied);
            Equal(!human, f.Data.Unit(1).Token.Occupied); Equal(!human, f.Data.Unit(2).Token.Occupied);
            if (!human) { Equal(new CitySlot(1), f.Data.Unit(1).Home); Equal(new CitySlot(2), f.Data.Unit(2).Home); }
            Equal(false, f.Data.Unit(3).Token.Occupied || f.Data.Unit(5).Token.Occupied);
            Equal(true, f.Data.Unit(4).Token.Occupied && f.Data.Unit(6).Token.Occupied);
            Equal(new CitySlot(0), f.Data.Unit(4).Home); Equal(new CitySlot(0), f.Data.Unit(6).Home);
            Equal(!human, w.Entities.Cities.Storage[1].Flags.DefenderLost); Equal(!human, w.Entities.Cities.Storage[2].Flags.DefenderLost);
            Equal((byte)(human ? 0 : 2), w.Civilizations[2].UnitsActive[4]); Equal((byte)0, w.Civilizations[2].UnitsActive[5]);
            Equal(human ? 1 : 3, w.UnitTotals[2]); Equal(true, w.Entities.Cities.Storage[0].Token.Occupied);
            Equal((sbyte)1, w.Entities.Cities.Storage[0].Size); Equal(0, f.Removed.Count); f.Lifecycle.Random.RequireChoicesConsumed();
        }
        var sea = new CaptureLossFixture(); MapPoint point = new(14, 14); sea.World.Map.TileAt(point).Terrain = Terrain.Ocean;
        sea.Data.Rules.Units[1] = sea.Data.Rules.Units[4] with { Domain = RuleDomain.Sea, Capacity = 1 };
        sea.Data.AddUnit(0, 2, 1, point); sea.Data.Unit(0).Home = new(0); sea.Data.AddUnit(1, 2, 4, point);
        sea.Data.Unit(1).Orders = UnitOrder.Sleeping; sea.Data.Unit(1).Carrier = new(0);
        using var cargo = sea.Support(); cargo.Resume(); Equal(1, cargo.Result); Equal(false, sea.Data.Unit(0).Token.Occupied || sea.Data.Unit(1).Token.Occupied);
        Equal(true, sea.Lifecycle.Order.Contains("units")); sea.Lifecycle.Random.RequireChoicesConsumed();
    }

    private static void CapturePopulationFailureAndDisposal()
    {
        var f = new CaptureLossFixture { FailRemoved = true }; using var op = f.Begin(1);
        Throws<InvalidOperationException>(() => op.Resume()); Throws<InvalidOperationException>(() => op.Resume());
        Equal(false, op.Completed); Equal(false, f.World.Entities.Cities.Storage[0].Token.Occupied);
        Equal(true, f.World.CityDeletionActive); Equal(1, f.Removed.Count); Equal((sbyte)0, f.World.Entities.Cities.Storage[0].Size);
        var stopped = CaptureNestedLoss(); var partial = stopped.Begin(1); partial.Resume(); partial.Dispose();
        Throws<ObjectDisposedException>(() => partial.Resume(0)); Equal(true, stopped.World.Entities.Cities.Storage[0].Token.Occupied);
        Equal((sbyte)0, stopped.World.Entities.Cities.Storage[0].Size); Equal(0, stopped.Removed.Count);
        var network = new CaptureLossFixture(); network.World.Globals.Multiplayer = GameMode.Tcp;
        using var denied = network.Begin(1); Throws<InvalidOperationException>(() => denied.Resume()); Equal((sbyte)1, network.World.Entities.Cities.Storage[0].Size);
        using var support = network.Support(); Throws<InvalidOperationException>(() => support.Resume()); Equal(true, network.World.Entities.Cities.Storage[0].Token.Occupied);
    }

    private static void CapturePopulationSessionPrivacy()
    {
        using var runtime = new SessionRuntime(new(), new(198));
        var f = CaptureNestedLoss(CreationFixture(new(runtime.Process, runtime.World))); ParticipantId victimSeat = default;
        runtime.RegisterCommand(new(new(1, "test.capture-population", "Authored population loss", AccessClass.Player, new([]), new([new("city")])),
            (_, _, _) => new(), _ => new CapturePopulationOperation(f.Begin(1), _ => victimSeat)));
        runtime.BeginClientSession(new()); var actor = SessionClient(runtime); var victim = SessionClient(runtime, 3); victimSeat = victim.Identity.Participant;
        var command = actor.Submit(new("test.capture-population", new([]))).Correlation.Command; runtime.AdvanceOne();
        var incident = RequiredInteraction(victim, command); Equal(null, RequiredResult(actor, command).Interaction);
        Equal(false, actor.Reply(incident, 0).Accepted); Equal(0, f.Removed.Count);
        Equal(true, victim.Reply(incident, 0).Accepted); runtime.AdvanceOne(); Equal(CommandStatus.Completed, RequiredResult(actor, command).Status);
        Equal(false, f.World.Entities.Cities.Storage[0].Token.Occupied); Equal(1, f.Removed.Count); f.Lifecycle.Random.RequireChoicesConsumed();
    }
}
