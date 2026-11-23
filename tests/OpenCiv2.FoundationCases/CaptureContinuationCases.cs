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
    private sealed class CaptureContinuationFixture : ICaptureProductionEffects, ICapturePartisansEffects
    {
        public CaptureLossFixture Loss { get; }
        public RevelationFixture Data => Loss.Data;
        public GameState World => Data.World;
        public List<MapPoint> Draws { get; } = [];
        public List<MapPoint> Neighbors { get; } = [];
        public List<CapturePartisansNotice> Notices { get; } = [];
        public int Sounds;
        public CaptureContinuationFixture(List<Choice>? choices = null, RevelationFixture? data = null)
        {
            Loss = new(choices, data); World.Entities.Cities.Storage[0].Owner = new(1);
            World.Entities.Cities.Storage[0].Size = 11;
            World.Map.TileAt(new(10, 10)).Owner = 1;
            World.Globals.PowerRank[1] = World.Globals.PowerRank[2] = 0;
            World.Civilizations[2].Advances = default(AdvanceSet).With(AdvanceIds.Conscription).With(AdvanceIds.GuerrillaWarfare);
            Data.Rules.Terrain[(int)Terrain.Grassland] = new() { DefenseFactor = 1 };
        }
        public CaptureProduction.Operation Production(int oldOwner = 2, MapPoint? center = null) =>
            new CaptureProduction(World, Data.Rules, Data.Stacks, Loss.Removal, Loss.Lifecycle.Elimination, Loss.Incidents.Incidents,
                new(World, Data.Rules, _ => throw new InvalidOperationException("Human repair must not invoke A02"), _ => { }), this)
            .Begin(new(0), 1, oldOwner, center ?? new(10, 10), "capture");
        public int Partisans(int mode = 0, MapPoint? center = null) =>
            new CapturePartisans(World, Data.Rules, new(World, Data.Process, Data.Stacks, Data), Loss.Lifecycle.Random, this)
                .Create(new(0), 1, 2, center ?? new(10, 10), mode, "Original name", "partisans");
        public void RedrawNeighbors(MapPoint point) => Neighbors.Add(point);
        public void Redraw(MapPoint point) => Draws.Add(point);
        public void InfantrySound() => ++Sounds;
        public void Notice(CapturePartisansNotice notice) => Notices.Add(notice);
    }

    private static void CaptureProductionHumanRepairAndWindow()
    {
        foreach (var (defense, cost, antiHorse, expected) in new[] { (1, 2, false, 60), (0, 2, false, 0),
            (255, 2, false, 0), (1, 255, false, 0), (0, 2, true, 60) })
        {
            var f = new CaptureContinuationFixture(); var w = f.World; ref var city = ref w.Entities.Cities.Storage[0];
            city.Production = 61; city.ShieldBox = 47; w.Civilizations[1].UnitsInProduction[61] = 3;
            w.Globals.Difficulty = Difficulty.Prince; w.Civilizations[2].Government = Government.Republic;
            f.Data.Rules.Units[60] = new()
            {
                Prerequisite = AdvanceReference.None,
                Role = RuleRole.Defend,
                Defense = (byte)defense,
                CostRows = (byte)cost,
                Abilities = new() { AntiHorseDefense = antiHorse }
            };
            f.Data.Rules.Units[59] = f.Data.Rules.Units[60] with { Defense = 100, CostRows = 1 };
            var captured = new MapPoint(24, 10); w.Map.TileAt(new(25, 9)).RevealedTo = CivilizationSet.Only(1);
            w.Map.TileAt(new(26, 10)).Features = MapFeatures.Road;
            using var op = f.Production(center: captured); var pending = (CaptureProductionDecision.Window)op.Resume(0)!;
            Equal(1, pending.Request.Owner); Equal(new CityTarget(new(0), city.Token), pending.Request.City);
            Equal((sbyte)expected, city.Production); Equal((short)47, city.ShieldBox); Equal((byte)3, w.Civilizations[1].UnitsInProduction[61]);
            Equal(true, city.Flags.Disorder && w.Calculation.Processing); Equal(false, op.Completed);
            Equal(new MapPoint(10, 10), f.Neighbors.Single()); Equal(7, f.Draws.Count);
            Equal(new MapPoint(26, 10), f.Draws[0]); Equal(true, w.Map.TileAt(new(26, 10)).RevealedTo.Contains(1));
            Equal(MapFeatures.None, w.Map.Remembered(1, new(26, 10)));
            Equal(pending, op.Resume()); Equal(7, f.Draws.Count); Equal(null, op.Resume(-1));
            Equal(true, op.Completed); Equal(false, w.Calculation.Processing); op.Resume(); Equal(1, f.Neighbors.Count);
            f.Loss.Lifecycle.Random.RequireChoicesConsumed();
        }
        foreach (bool unit in new[] { false, true })
        {
            var f = new CaptureContinuationFixture(); ref var city = ref f.World.Entities.Cities.Storage[0];
            city.Production = unit ? (sbyte)10 : (sbyte)-4;
            f.Data.Rules.Units[10] = new() { Prerequisite = AdvanceReference.None };
            f.Data.Rules.Improvements[4] = new() { Prerequisite = AdvanceReference.None };
            f.World.Civilizations[1].Government = Government.Democracy; // Chieftain suppresses new disorder.
            using var op = f.Production(); op.Resume(); Equal(unit ? (sbyte)10 : (sbyte)-4, city.Production);
            Equal(false, city.Flags.Disorder); op.Resume(0);
        }
    }

    private static void CaptureProductionActualAiChoice()
    {
        foreach (int owner in new[] { 0, 1 })
        {
            var ai = new AiProductionFixture(); var life = new EliminationFixture(data: ai.Data);
            var incidents = new IncidentFixture(life.Random, ai.Data);
            var removal = new CityRemoval(ai.World, ai.Rules, ai.Data.Stacks, life.Loss, _ => { });
            ai.Ai(); ai.Garrison(); ai.Building(5); ai.World.Calculation.Trade = 20;
            ai.City.Owner = new((byte)owner); ai.City.Production = -4; ai.City.ShieldBox = 43;
            ai.World.Civilizations[owner].UnitsInProduction[0] = 255;
            ai.World.Civilizations[owner].UnitsInProduction[9] = 255;
            ai.Data.AddUnit(2, owner, 9, new(20, 10));
            int choices = 0;
            var production = new ProductionOrders(ai.World, ai.Rules, slot =>
            {
                ++choices; Equal((sbyte)(owner == 0 ? 9 : 0), ai.City.Production);
                // C09 increments, C06 decrements before actual A02 selection, both modulo 256.
                Equal((byte)255, ai.World.Civilizations[owner].UnitsInProduction[owner == 0 ? 9 : 0]);
                return ai.Policy.Choose(slot, "capture-ai");
            }, _ => { });
            var effects = new CaptureContinuationFixture();
            using var op = new CaptureProduction(ai.World, ai.Rules, ai.Data.Stacks, removal, life.Elimination,
                incidents.Incidents, production, effects).Begin(new(0), owner, 2, new(20, 10), "ai");
            Equal(null, op.Resume()); Equal(true, op.Completed); Equal(1, choices);
            Equal((sbyte)(owner == 0 ? 9 : -5), ai.City.Production);
            Equal((short)43, ai.City.ShieldBox); Equal(false, ai.World.Calculation.Processing);
            Equal(0, effects.Neighbors.Count + effects.Draws.Count); ai.Random.RequireChoicesConsumed(); life.Random.RequireChoicesConsumed();
        }
    }

    private static void CaptureProductionNestedLossAndFailure()
    {
        var f = new CaptureContinuationFixture(); var w = f.World;
        w.Globals.HumanPlayers = CivilizationSet.Only(1).With(3);
        f.Data.Rules.Units[46] = f.Data.Rules.Units[4] with { Role = RuleRole.Settle };
        f.Data.AddUnit(0, 3, 46, new(14, 10)); f.Data.Unit(0).Home = new(0); w.Civilizations[3].UnitsActive[46] = 1;
        f.Data.AddUnit(1, 4, 4, new(24, 10)); f.Data.Unit(1).Flags.SpyMission = true; f.Data.Unit(1).Counter = 3;
        w.Civilizations[3].Treaties[4].Peace = true;
        f.Loss.Lifecycle.Recreate = c => Creation(f.Data, c, f.Loss.Lifecycle.Random);
        using var op = f.Production(3); var incident = (CaptureProductionDecision.Incident)op.Resume()!;
        Equal(3, incident.Pretext.Victim.Civilization); Equal(false, w.Calculation.Processing); Equal(0, f.Neighbors.Count);
        Equal(incident, op.Resume()); var window = (CaptureProductionDecision.Window)op.Resume(0)!;
        Equal(false, op.Completed); Equal(true, w.Calculation.Processing); Equal(window, op.Resume());
        op.Resume(0); Equal(true, op.Completed); f.Loss.Lifecycle.Random.RequireChoicesConsumed();

        var zero = new CaptureContinuationFixture(); zero.World.Entities.Cities.Storage[0].Production = 61;
        zero.Data.Rules.Units[60] = new() { Role = RuleRole.Defend, Prerequisite = AdvanceReference.None };
        using var fault = zero.Production(); Throws<DivideByZeroException>(() => fault.Resume());
        Throws<InvalidOperationException>(() => fault.Resume()); Equal((sbyte)0, zero.World.Entities.Cities.Storage[0].Production);
        Equal(false, zero.World.Calculation.Processing);
        var held = new CaptureContinuationFixture(); var stopped = held.Production(); stopped.Resume(); stopped.Dispose();
        Throws<ObjectDisposedException>(() => stopped.Resume(0)); Equal(true, held.World.Calculation.Processing);
        var remote = new CaptureContinuationFixture(); remote.World.Globals.Multiplayer = GameMode.Tcp;
        using var denied = remote.Production(); Throws<InvalidOperationException>(() => denied.Resume()); Equal(0, remote.Neighbors.Count);
    }

    private static void CaptureProductionSessionWindow()
    {
        foreach (bool cancel in new[] { false, true })
        {
            using var runtime = new SessionRuntime(new(), new(199));
            var f = new CaptureContinuationFixture(data: CreationFixture(new(runtime.Process, runtime.World)));
            ParticipantId actorSeat = default, otherSeat = default; int windows = 0;
            runtime.RegisterCommand(new(new(1, "test.capture-production", "Authored production continuation", AccessClass.Player, new([]), new([])),
                (_, _, _) => new(), _ => new CaptureProductionOperation(f.Production(), c => c == 1 ? actorSeat : otherSeat, request =>
                {
                    ++windows; Equal(true, f.World.Calculation.Processing);
                    // Tests the required P06 adapter boundary, not a replacement city renderer.
                    return new("Authored city window", otherSeat, new CommandValue.City(request.City), new([0]));
                })));
            runtime.BeginClientSession(new()); var actor = SessionClient(runtime); var other = SessionClient(runtime, 2);
            actorSeat = actor.Identity.Participant; otherSeat = other.Identity.Participant;
            var command = actor.Submit(new("test.capture-production", new([]))).Correlation.Command; runtime.AdvanceOne();
            var window = RequiredInteraction(actor, command); Equal(null, other.Result(command)); Equal(false, other.Reply(window, 0).Accepted);
            if (cancel) Equal(true, actor.Cancel(command).Accepted); else Equal(true, actor.Reply(window, 0).Accepted);
            runtime.AdvanceOne(); Equal(CommandStatus.Completed, RequiredResult(actor, command).Status);
            Equal(false, f.World.Calculation.Processing); Equal(1, windows);
        }
        foreach (bool fail in new[] { false, true })
        {
            var f = new CaptureContinuationFixture(); int windows = 0;
            var context = new OperationContext(f.World, f.Data.Process, f.Loss.Lifecycle.Random, new PresentationRandom(3, "capture"),
                new(default, new(1)), default, (_, _) => { }, () => { });
            using var op = new CaptureProductionOperation(f.Production(), _ => new(1), request =>
            {
                ++windows; if (fail) throw new InvalidOperationException("Required window unavailable");
                return new("Authored city window", null, new CommandValue.City(request.City), new([0]));
            });
            if (fail)
            {
                Throws<InvalidOperationException>(() => op.Resume(context, new())); Throws<InvalidOperationException>(() => op.Resume(context, new()));
                Equal(true, f.World.Calculation.Processing);
            }
            else
            {
                Equal(CommandStatus.AwaitingInteraction, op.Resume(context, new()).Status);
                Equal(CommandStatus.AwaitingInteraction, op.Resume(context, new(OperationInputKind.Proceed)).Status);
                Equal(CommandStatus.Completed, op.Resume(context, new(OperationInputKind.Timeout)).Status); Equal(false, f.World.Calculation.Processing);
            }
            Equal(1, windows);
        }
    }

    private static Choice PartisanChoice(int attempt, int bound, int roll) => Choice.Create(new("C09.partisans.veteran", "partisans", attempt),
        new SamplingLaw.PrimaryModulo(bound), ChoiceCode.Primary(roll));

    private static void CapturePartisanCreationAndPlacement()
    {
        var f = new CaptureContinuationFixture([PartisanChoice(0, 3, 1), PartisanChoice(1, 3, 0)]); var w = f.World;
        w.Civilizations[1].Reputation = 6;
        var first = new MapPoint(12, 10); var second = new MapPoint(11, 9);
        w.Map.TileAt(first).Features = MapFeatures.Road | MapFeatures.Fortification;
        w.Map.TileAt(second).Features = MapFeatures.Road | MapFeatures.Railroad | MapFeatures.CitySite | MapFeatures.Fortification;
        w.Map.TileAt(second).Owner = 3; // Airbase isn't a city/unit owner and must not double its score.
        Equal(2, f.Partisans()); Equal(first, f.Data.Unit(0).Position); Equal(second, f.Data.Unit(1).Position);
        for (int i = 0; i < 2; ++i)
        {
            Equal(new UnitType(9), f.Data.Unit(i).Type); Equal(new CivilizationSlot(2), f.Data.Unit(i).Owner);
            Equal(UnitOrder.Fortified, f.Data.Unit(i).Orders); Equal(i == 0, f.Data.Unit(i).Flags.Veteran);
            Equal(true, f.Data.Unit(i).Visibility.Contains(1)); Equal(false, f.Data.Unit(i).Visibility.Contains(2));
            Equal(true, w.Map.TileAt(f.Data.Unit(i).Position).RevealedTo.Contains(2));
        }
        Equal((byte)2, w.Civilizations[2].UnitsActive[9]); Equal((short)2, w.Civilizations[2].MilitaryCount);
        Equal(2, w.UnitTotals[2]); Equal(1, f.Sounds); Equal(2, f.Draws.Count);
        Equal("Original name", f.Notices.Single().CityName); Equal("Birch", f.Notices.Single().Adjective);
        Equal(new UnitSlot(1), f.Notices.Single().LastCreation); Equal(true, f.Data.PublishedOrigins.Count >= 2);
        f.Loss.Lifecycle.Random.RequireChoicesConsumed();

        var ties = new CaptureContinuationFixture(); Equal(2, ties.Partisans());
        Equal(new MapPoint(11, 9), ties.Data.Unit(0).Position); Equal(new MapPoint(12, 10), ties.Data.Unit(1).Position);
        var wrap = new CaptureContinuationFixture(); wrap.World.Globals.Rules.FlatWorld = false;
        wrap.Data.Rules.Terrain[(int)Terrain.Grassland] = new() { DefenseFactor = 0 };
        wrap.World.Map.TileAt(new(38, 10)).Features = MapFeatures.Road;
        Equal(1, wrap.Partisans(center: new(0, 10))); Equal(new MapPoint(38, 10), wrap.Data.Unit(0).Position);
        wrap.Loss.Lifecycle.Random.RequireChoicesConsumed();
    }

    private static void CapturePartisanCountAndGates()
    {
        foreach (var (size, mode, conscription, guerrilla, communism, gunpowder, newGuerrilla, expected) in new[]
        {
            (11, 0, true, true, false, false, false, 2), (11, 1, true, true, false, false, false, 0),
            (27, 1, true, true, false, false, false, 2), (11, 0, false, true, false, false, false, 0),
            (11, 0, true, false, true, true, false, 1), (11, 0, true, false, false, true, false, 0),
            (11, 0, true, false, true, false, false, 0), (11, 0, true, true, false, false, true, 2),
            (-128, 0, false, true, true, true, false, 0)
        })
        {
            var f = new CaptureContinuationFixture(); var w = f.World; w.Entities.Cities.Storage[0].Size = (sbyte)size;
            w.Civilizations[2].Advances = default;
            if (conscription) w.Civilizations[2].Advances = w.Civilizations[2].Advances.With(AdvanceIds.Conscription);
            if (guerrilla) w.Civilizations[2].Advances = w.Civilizations[2].Advances.With(AdvanceIds.GuerrillaWarfare);
            if (communism) w.Civilizations[2].Advances = w.Civilizations[2].Advances.With(AdvanceIds.Communism);
            if (gunpowder) w.Civilizations[2].Advances = w.Civilizations[2].Advances.With(AdvanceIds.Gunpowder);
            if (newGuerrilla) w.Civilizations[1].Advances = w.Civilizations[1].Advances.With(AdvanceIds.GuerrillaWarfare);
            Equal(expected, f.Partisans(mode)); Equal(expected == 0 ? 0 : 1, f.Notices.Count); f.Loss.Lifecycle.Random.RequireChoicesConsumed();
        }
        var differences = new CaptureContinuationFixture(); differences.World.Civilizations[1].Government = Government.Republic;
        differences.World.Civilizations[2].Government = Government.Monarchy; differences.World.Globals.PowerRank[1] = 5;
        differences.World.Globals.PowerRank[2] = 3; Equal(12, differences.Partisans());
        foreach (int gate in new[] { 0, 1, 2 })
        {
            var f = new CaptureContinuationFixture();
            if (gate == 0) f.World.Multiplayer.Saved.AutoplayActive = true;
            if (gate == 1) f.World.Globals.HumanPlayers = CivilizationSet.Only(3);
            if (gate == 2) f.World.Multiplayer.LocalHuman = 3;
            Equal(2, f.Partisans()); Equal(0, f.Notices.Count); Equal(gate == 2 ? 0 : 1, f.Sounds);
        }
    }

    private static void CapturePartisanFailuresAndKnowledge()
    {
        var failed = new CaptureContinuationFixture(); failed.World.Civilizations[2].MilitaryCount = 1949;
        failed.World.Civilizations[1].Reputation = 9;
        Equal(0, failed.Partisans()); Equal(0, failed.Sounds + failed.Draws.Count + failed.Notices.Count);
        failed.Loss.Lifecycle.Random.RequireChoicesConsumed();
        var last = new CaptureContinuationFixture([PartisanChoice(0, 3, 0)]);
        last.World.Civilizations[2].MilitaryCount = 1948; last.World.Civilizations[1].Reputation = 4;
        last.World.Civilizations[2].Treaties[1].Vendetta = true;
        Equal(1, last.Partisans()); Equal(null, last.Notices.Single().LastCreation); Equal(1, last.Sounds);
        last.Loss.Lifecycle.Random.RequireChoicesConsumed();
        foreach (byte defense in new byte[] { 0, 255 })
        {
            var none = new CaptureContinuationFixture(); none.Data.Rules.Terrain[(int)Terrain.Grassland] = new() { DefenseFactor = defense };
            none.World.Civilizations[1].Reputation = 9; Equal(0, none.Partisans()); none.Loss.Lifecycle.Random.RequireChoicesConsumed();
        }
        var single = new CaptureContinuationFixture(); single.Data.Rules.Terrain[(int)Terrain.Grassland] = new() { DefenseFactor = 0 };
        single.World.Map.TileAt(new(11, 9)).Features = MapFeatures.Road;
        Equal(1, single.Partisans()); Equal(new UnitSlot(0), single.Notices.Single().LastCreation);
        var remote = new CaptureContinuationFixture(); remote.World.Globals.Multiplayer = GameMode.Tcp;
        Throws<InvalidOperationException>(() => remote.Partisans()); Equal(0, remote.World.Entities.Units.End);

        var seen = new RevelationFixture(); seen.AddUnit(0, 2, 9, new(10, 10)); seen.AddUnit(1, 2, 9, new(10, 10));
        seen.Unit(0).Visibility = CivilizationSet.Only(3);
        Equal(true, MapKnowledge.MarkUnitSeen(seen.World, new(0), 1));
        Equal(true, seen.Unit(0).Visibility.Contains(1) && seen.Unit(0).Visibility.Contains(3));
        Equal(false, seen.Unit(1).Visibility.Contains(1)); Equal(false, MapKnowledge.MarkUnitSeen(seen.World, new(0), 2));
        Equal(false, MapKnowledge.MarkUnitSeen(seen.World, new(ushort.MaxValue), -1));
        Equal(false, seen.World.Map.TileAt(new(10, 10)).RevealedTo.Contains(1));

        // A real W03 meeting during creation can change these inputs. The C09 loop keeps
        // its entry count/threshold and only adds Veteran; a zero roll does not clear it.
        var retained = new CaptureContinuationFixture([PartisanChoice(0, 3, 0), PartisanChoice(1, 3, 1)]);
        retained.World.Civilizations[1].Reputation = 6; bool met = false;
        retained.Data.OnMeet = (_, _, _) =>
        {
            if (met) return;
            met = true; retained.Data.Unit(0).Flags.Veteran = retained.Data.Unit(0).Flags.SpyMission = true;
            retained.World.Civilizations[1].Reputation = 255;
            retained.World.Civilizations[2].Government = Government.Democracy;
            retained.World.Entities.Cities.Storage[0].Size = 127;
            retained.World.Civilizations[2].Advances = default;
        };
        Equal(2, retained.Partisans()); Equal(true, met);
        Equal(true, retained.Data.Unit(0).Flags.Veteran && retained.Data.Unit(0).Flags.SpyMission);
        Equal(true, retained.Data.Unit(1).Flags.Veteran); retained.Loss.Lifecycle.Random.RequireChoicesConsumed();

        var ownerQuery = new CaptureContinuationFixture();
        ownerQuery.Data.Rules.Terrain[(int)Terrain.Grassland] = new() { DefenseFactor = 0 };
        ownerQuery.World.Map.TileAt(new(11, 9)).Features = MapFeatures.CitySite | MapFeatures.Road;
        ownerQuery.World.Map.TileAt(new(11, 9)).Owner = null;
        Equal(1, ownerQuery.Partisans()); Equal(new MapPoint(11, 9), ownerQuery.Data.Unit(0).Position);
    }
}
