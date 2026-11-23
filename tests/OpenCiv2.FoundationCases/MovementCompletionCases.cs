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
    private sealed class CompletionFixture : IMovementCompletionEffects, ISpecialMovementEffects, ITransportMovementEffects, IHutEffects
    {
        public CityCaptureFixture Capture { get; }
        public RevelationFixture Data => Capture.Data;
        public GameState World => Data.World;
        public EliminationFixture Life => Capture.Life;
        public MovementCompletion Completion { get; }
        public MovementContinuation Movement { get; }
        public MovementStep Step { get; set; }
        public MovementFinishInputs Inputs { get; set; } = new(true, false, false, false, 0, false, false, 77);
        public List<string> Effects { get; } = [];
        public List<MovementLossNotice> Losses { get; } = [];
        public List<HutNotice> Huts { get; } = [];
        public List<int> Pauses { get; } = [];
        public List<MapPoint> OldPositions { get; } = [];
        public bool FailDisplay, FailLoss;
        public CompletionFixture(List<Choice>? choices = null, RevelationFixture? data = null, CityCaptureFixture? capture = null)
        {
            Capture = capture ?? new(choices, data); World.Globals.Rules.NoRestart = true;
            if (capture is null) { Data.Unit(0).Type = new(4); Data.Stacks.Relocate(0, new(18, 12), false); }
            Movement = new(new(0), World.Entities.Units.End, new(10, 8)) { LocalHuman = true, Options = new(true, true) };
            Step = new(1, RuleDomain.Land, Movement.Origin, Data.Unit(0).Position, Terrain.Grassland, 1);
            var incidents = Capture.Theft.Damage.Incidents.Incidents;
            var transport = new TransportMovement(World, Data.Rules, Data.Stacks, new(World, Data.Rules, Data.Stacks), this);
            var special = new SpecialMovementFinish(World, Data.Rules, Data.Stacks, Life.Random, Life.Elimination, Life.Loss, incidents, this);
            var huts = new HutEntry(World, Data.Rules, Life.Random, new(World, Data.Process, Data.Stacks, Data), CityCreator(Data), Capture.Theft.Research, this);
            Completion = new(World, Data.Rules, Data.Stacks, new(Capture.Capture, new(World, Data.Rules, Data.Stacks, Data), special, huts, transport, new(World)), this);
        }
        public MovementCompletion.Operation Begin() => Completion.Begin(Movement, Step, Inputs, "finish");
        public void Complete(MovementCompletion.Operation op)
        {
            for (int i = 0; i < 100 && !op.Completed; ++i) Equal(null, op.Resume());
            Equal(true, op.Completed); Life.Random.RequireChoicesConsumed();
        }
        public void AtCity(bool foreign)
        {
            Data.Stacks.Relocate(0, Capture.City.Position, false);
            if (!foreign) { Capture.City.Owner = new(1); World.Map.TileAt(Capture.City.Position).Owner = 1; }
            Step = Step with { To = Capture.City.Position, City = new(0), DestinationOwner = foreign ? 2 : 1 };
        }
        public void Sea()
        {
            foreach (ref var tile in World.Map.Tiles) tile.Terrain = Terrain.Ocean;
            Data.Unit(0).Type = new(1); Data.Rules.Units[1] = Data.Rules.Units[4] with { Domain = RuleDomain.Sea, Capacity = 1 };
            Step = Step with { Domain = RuleDomain.Sea, DestinationTerrain = Terrain.Ocean }; Inputs = Inputs with { OrdinaryArrival = false };
        }
        public void CityUnitsChanged(UnitTarget unit, MapPoint oldPosition) { OldPositions.Add(oldPosition); Effects.Add("city-units"); if (FailDisplay) throw new InvalidOperationException("City display unavailable"); }
        public void PauseEnemyMovement(int ticks) { Pauses.Add(ticks); if (FailDisplay) throw new InvalidOperationException("Delay unavailable"); }
        public void Sound(MovementLossSound sound) { Effects.Add("sound"); if (FailDisplay) throw new InvalidOperationException("Sound unavailable"); }
        public void Lost(MovementLossNotice notice) { Losses.Add(notice); if (FailLoss) throw new InvalidOperationException("Required loss notice unavailable"); }
        public void Redraw(MapPoint point) { Effects.Add("draw"); if (FailDisplay) throw new InvalidOperationException("Draw unavailable"); }
        public void NoLandfall(UnitSlot ship, string name) => throw new InvalidOperationException("No landfall in completion");
        public void Publish() => Effects.Add("publish");
        public void FocusSelectedUnit(UnitSlot unit, MapPoint point) => throw new InvalidOperationException("No unload in completion");
        public void Notice(HutNotice notice) => Huts.Add(notice);
        public void RedrawNeighbors(MapPoint point) => Effects.Add("neighbors");
        public void RefreshStatus() => Effects.Add("status");
    }
    private static Choice CompletionCaptureParity() => Choice.Create(new("C09.capture.building-parity", "finish/capture"), new SamplingLaw.PrimaryModulo(2), ChoiceCode.Primary(0));
    private static List<Choice> CompletionHut(int outcome, params Choice[] choices) => HutStart(outcome, choices).Select(c => c with { Key = c.Key with { Call = "finish/hut" } }).ToList();
    private static void MovementArrivalCaptureAndCounts()
    {
        var f = new CompletionFixture([CompletionCaptureParity()]); f.AtCity(true); f.Data.Unit(0).Damage = 7;
        f.Inputs = f.Inputs with { OriginOcean = true }; int region = f.World.Map.TileAt(f.Step.To).Region;
        f.World.Civilizations[1].ContinentMilitary[region] = ushort.MaxValue;
        using var op = f.Begin(); var window = (MovementCompletionDecision.Capture)op.Resume()!;
        Equal(true, window.Decision is CityCaptureDecision.Window); Equal((byte)0, f.Data.Unit(0).Damage);
        Equal((ushort)0, f.World.Civilizations[1].ContinentMilitary[region]); Equal(new CivilizationSlot(1), f.Capture.City.Owner);
        Equal((UnitSlot?)new UnitSlot(0), f.World.Globals.MovingUnit); Equal((sbyte)4, f.Capture.City.Size); Equal(0, f.OldPositions.Count);
        Equal(window, op.Resume()); op.Resume(new(0)); f.Complete(op); Equal(1, op.Result);
        Equal((byte)1, f.Data.Unit(0).LastDirection); Equal(f.Step.From, f.OldPositions.Single());
        f = new([CompletionCaptureParity()]); f.AtCity(true); f.Data.AddUnit(1, 1, 4, f.Step.To); f.Movement.UnitBound = 2;
        f.Data.Unit(0).Damage = 7; f.Data.Unit(0).LastDirection = 44; f.Data.Unit(1).Damage = 3;
        using (var changedActor = f.Begin())
        {
            Equal(true, changedActor.Resume() is MovementCompletionDecision.Capture { Decision: CityCaptureDecision.Window });
            // A retained capture decision is a real input boundary; finish reloads the shared actor afterwards.
            f.World.Globals.MovingUnit = new(1); changedActor.Resume(new(0)); f.Complete(changedActor);
            Equal(new UnitSlot(1), f.Movement.Unit); Equal((byte)1, f.Data.Unit(1).LastDirection);
            Equal((byte)44, f.Data.Unit(0).LastDirection); Equal((byte)0, f.Data.Unit(0).Damage); Equal((byte)3, f.Data.Unit(1).Damage);
        }
        foreach (int variant in new[] { 0, 1, 2 })
        {
            f = new(); f.Inputs = f.Inputs with { OriginOcean = variant != 0 };
            if (variant == 1) f.Step = f.Step with { Domain = RuleDomain.Sea };
            if (variant == 2) f.Step = f.Step with { DestinationTerrain = Terrain.Ocean };
            using var excluded = f.Begin(); f.Complete(excluded); Equal((ushort)0, f.World.Civilizations[1].ContinentMilitary[f.World.Map.TileAt(f.Step.To).Region]);
        }
        f = new([CompletionCaptureParity()]); f.AtCity(true); f.Data.Unit(0).Damage = 7;
        f.Capture.OnNotice = _ => throw new InvalidOperationException("Capture notice unavailable");
        using var failed = f.Begin(); Throws<InvalidOperationException>(() => failed.Resume()); Equal((byte)0, f.Data.Unit(0).Damage);
        Equal(new CivilizationSlot(1), f.Capture.City.Owner); Equal(0, f.OldPositions.Count); Throws<InvalidOperationException>(() => failed.Resume());
    }
    private static void MovementArrivalBarbarianVisibilityAndPort()
    {
        foreach (bool capturedBarbarian in new[] { false, true })
        {
            var f = new CompletionFixture(); f.Capture.City.Owner = new(1); f.World.Map.TileAt(f.Capture.City.Position).Owner = 1;
            f.Data.AddUnit(1, 1, 4, f.Step.To); f.Movement.UnitBound = 2;
            f.Data.Unit(0).Visibility = f.Data.Unit(1).Visibility = default;
            f.World.Map.TileAt(f.Step.To).RevealedTo = CivilizationSet.Only(1).With(3);
            f.Step = f.Step with { Owner = capturedBarbarian ? 0 : 1 };
            using var op = f.Begin(); f.Complete(op); Equal(capturedBarbarian, f.Data.Unit(1).Visibility.Contains(3));
        }
        var port = new CompletionFixture(); port.AtCity(false); port.Data.Unit(0).Type = new(1);
        port.Data.Rules.Units[1] = port.Data.Rules.Units[4] with { Domain = RuleDomain.Sea, Capacity = 1 };
        port.Data.AddUnit(1, 1, 4, port.Step.To); port.Data.Unit(1).Orders = UnitOrder.Sleeping; port.Movement.UnitBound = 2;
        port.Step = port.Step with { Domain = RuleDomain.Sea }; port.Inputs = port.Inputs with { OriginOcean = true };
        using var arrival = port.Begin(); port.Complete(arrival); Equal(UnitOrder.None, port.Data.Unit(1).Orders); Equal((ushort)0, port.World.Civilizations[1].ContinentMilitary[port.World.Map.TileAt(port.Step.To).Region]);
    }
    private static void MovementFinishOrderingAndLongMove()
    {
        var f = new CompletionFixture(); f.Data.AddUnit(1, 1, 4, f.Step.To); f.Data.Stacks.Relocate(0, f.Step.To, false); f.Movement.UnitBound = 2;
        f.Data.Rules.Units[4] = f.Data.Rules.Units[4] with { Attack = 0 }; f.Data.Unit(0).Orders = UnitOrder.GoTo;
        f.Data.Unit(0).Flags.Automated = f.Data.Unit(0).Flags.Garrison = true; f.Data.Unit(0).AuxiliaryTimer = 32; f.Data.Unit(0).LastDirection = 1;
        f.Step = f.Step with { Direction = 5 }; f.Inputs = f.Inputs with { OrdinaryArrival = false };
        using var op = f.Begin(); var warning = (MovementCompletionDecision.LongMove)op.Resume()!;
        Equal(1, f.Data.Stacks.FirstAt(f.Step.To)); Equal((byte)48, f.Data.Unit(0).AuxiliaryTimer); Equal((byte)1, f.Data.Unit(0).LastDirection);
        Equal(true, f.World.Map.TileAt(f.Step.To).RevealedTo.Contains(1)); Equal(warning, op.Resume()); Equal(1, f.OldPositions.Count);
        op.Resume(new(1)); f.Complete(op); Equal((byte)5, f.Data.Unit(0).LastDirection); Equal(UnitOrder.None, f.Data.Unit(0).Orders);
        Equal(false, f.Data.Unit(0).Flags.Automated || f.Data.Unit(0).Flags.Garrison); Equal(1, op.Result);
        foreach (int variant in new[] { 0, 1, 2, 3 })
        {
            f = new() { FailDisplay = true }; f.World.Multiplayer.LocalHuman = 2; f.Inputs = f.Inputs with { OrdinaryArrival = false, LocalSeen = true, Unanimated = variant == 1 };
            f.World.Globals.GameOptions.EnemyMoves = variant != 2; f.World.Globals.GameOptions.SkipEnemyPauses = variant == 3;
            using var cosmetic = f.Begin(); f.Complete(cosmetic); Equal(1, cosmetic.Result); Equal(variant == 3 ? 1 : 2, cosmetic.PresentationFailures.Count);
            if (variant != 3) Equal(variant == 0 ? 15 : 10, f.Pauses.Single());
        }
        f = new(); f.Inputs = f.Inputs with { OrdinaryArrival = false }; f.Step = f.Step with { Direction = -1 };
        using var negative = f.Begin(); f.Complete(negative); Equal((byte)255, f.Data.Unit(0).LastDirection); Equal(0, f.Huts.Count);
    }
    private static void MovementFinishHutsAndBoundResult()
    {
        var f = new CompletionFixture(CompletionHut(2, HutDraw("metals-size", 3, 1))); f.Inputs = f.Inputs with { HasHut = true };
        int gold = f.World.Civilizations[1].Gold; using var op = f.Begin(); f.Complete(op); Equal(gold + 50, f.World.Civilizations[1].Gold); Equal(1, op.Result);
        Equal(HutNoticeKind.Metals, f.Huts.Single().Kind); Equal(true, f.Effects.IndexOf("city-units") < f.Effects.IndexOf("status"));
        f = new(CompletionHut(1, HutDraw("mounted", 3, 0), HutDraw("mercenary-type", 2, 0)));
        f.Inputs = f.Inputs with { HasHut = true }; f.Data.Unit(0).Flags.Garrison = true;
        using var spawned = f.Begin(); f.Complete(spawned); Equal(2, f.World.Entities.Units.End); Equal(0, spawned.Result);
        Equal((byte)1, f.Data.Unit(0).LastDirection); Equal(true, f.Data.Unit(0).Flags.Garrison); // Bound change bypasses the unit cleanup writes.
        f = new(CompletionHut(0)); f.Inputs = f.Inputs with { HasHut = true }; f.World.Map.TileAt(f.Step.To).Fertility = 15;
        using var city = f.Begin(); var visit = (MovementCompletionDecision.Hut)city.Resume()!;
        Equal(3, f.World.Entities.Cities.End); Equal(visit, city.Resume()); city.Resume(new(0)); f.Complete(city); Equal(1, city.Result);
    }
    private static void MovementFinishAircraftCoastAndBoarding()
    {
        var f = new CompletionFixture() { FailDisplay = true }; f.Sea(); f.Data.Unit(0).Type = new(2);
        f.Data.Rules.Units[2] = f.Data.Rules.Units[4] with { Domain = RuleDomain.Air, Range = 1 };
        f.Data.Unit(0).MovesUsed = 6; f.Data.Unit(0).LastDirection = 99; f.Inputs = f.Inputs with { PriorResult = 7, HasHut = true };
        f.Data.AddUnit(1, 2, 1, new(30, 20)); f.Movement.UnitBound = 2;
        using var crash = f.Begin(); f.Complete(crash); Equal(7, crash.Result); Equal(false, f.Data.Unit(0).Token.Occupied);
        Equal((byte)99, f.Data.Unit(0).LastDirection); Equal(0, f.Huts.Count); Equal(f.Step.To, f.Movement.Origin); Equal(1, f.Losses.Count); Equal(3, crash.PresentationFailures.Count);
        f = new(); f.Sea(); f.Data.Rules.Units[1] = f.Data.Rules.Units[1] with { Abilities = new() { CoastBound = true } };
        f.Data.Unit(0).MovesUsed = 6; f.Data.AddUnit(1, 1, 4, f.Step.To); f.Data.Unit(1).Orders = UnitOrder.Sleeping; f.Data.Unit(1).Carrier = new(0); f.Movement.UnitBound = 2;
        using var sink = f.Begin(); f.Complete(sink); Equal(0, sink.Result); Equal(false, f.Data.Unit(0).Token.Occupied || f.Data.Unit(1).Token.Occupied); Equal(true, f.Losses.Single().Sinking);
        f = new() { FailDisplay = true }; f.World.Map.TileAt(f.Step.To).Terrain = Terrain.Ocean; f.Step = f.Step with { DestinationTerrain = Terrain.Ocean };
        f.Data.Rules.Units[1] = f.Data.Rules.Units[4] with { Domain = RuleDomain.Sea, Capacity = 1 };
        f.Data.AddUnit(1, 1, 1, f.Step.To); f.Movement.UnitBound = 2; f.Inputs = f.Inputs with { Boarding = true, OrdinaryArrival = false };
        using var board = f.Begin(); f.Complete(board); Equal(1, board.Result); Equal((UnitSlot?)new UnitSlot(1), f.Data.Unit(0).Carrier);
        Equal(UnitOrder.Sleeping, f.Data.Unit(0).Orders); Equal((byte)6, f.Data.Unit(0).MovesUsed); Equal((UnitSlot?)new UnitSlot(1), f.World.Globals.ActiveUnit);
        Equal(true, f.Effects.Contains("publish")); Equal(2, board.PresentationFailures.Count);
    }
    private static void MovementFinishExplorationAndNestedFailures()
    {
        var f = new CompletionFixture(); f.Sea(); f.Data.Unit(0).MovesUsed = 6;
        var shore = new MapPoint(f.Step.To.X + 1, f.Step.To.Y - 1); f.World.Map.TileAt(shore).Terrain = Terrain.Grassland;
        f.Data.AddUnit(1, 1, 4, f.Step.To); f.Data.Rules.Units[4] = f.Data.Rules.Units[4] with { Attack = 0 };
        f.Data.Unit(1).Orders = UnitOrder.Sleeping; f.Data.Unit(1).MovesUsed = 0; f.Movement.UnitBound = 2;
        using var exploration = f.Begin(); f.Complete(exploration); Equal(UnitOrder.None, f.Data.Unit(1).Orders); Equal(true, f.Movement.WorkFlag);
        f = new() { FailLoss = true }; f.Sea(); f.Data.Unit(0).Type = new(2);
        f.Data.Rules.Units[2] = f.Data.Rules.Units[4] with { Domain = RuleDomain.Air, Range = 1 }; f.Data.Unit(0).MovesUsed = 6; f.Data.Unit(0).LastDirection = 88;
        using var failed = f.Begin(); Throws<InvalidOperationException>(() => failed.Resume()); Equal(false, f.Data.Unit(0).Token.Occupied);
        Equal((byte)88, f.Data.Unit(0).LastDirection); Equal(null, failed.Result); Throws<InvalidOperationException>(() => failed.Resume());
        f = new(); f.World.Globals.Multiplayer = GameMode.Tcp;
        using var remote = f.Begin(); Throws<InvalidOperationException>(() => remote.Resume()); Equal(0, f.Effects.Count);
        using var runtime = new SessionRuntime(new(), new(230));
        f = new(data: CreationFixture(new(runtime.Process, runtime.World))); f.Sea();
        f.Data.Unit(0).Type = new(2); f.Data.Unit(0).MovesUsed = 6;
        f.Data.Rules.Units[2] = f.Data.Rules.Units[4] with { Domain = RuleDomain.Air, Range = 1, Role = RuleRole.Settle };
        f.World.Civilizations[1].CityCount = 0; f.Data.AddUnit(1, 3, 4, new(24, 10));
        f.Data.Unit(1).Flags.SpyMission = true; f.Data.Unit(1).Counter = 1; f.World.Civilizations[1].Treaties[3].Peace = true; f.Movement.UnitBound = 2;
        ParticipantId victimSeat = default;
        runtime.RegisterCommand(new(new(1, "test.finish-loss", "Authored finish loss", AccessClass.Player, new([]), new([new("result")])), (_, _, _) => new(),
            _ => new MovementCompletionOperation(f.Begin(), _ => victimSeat, _ => throw new InvalidOperationException("No capture"), _ => throw new InvalidOperationException("No hut"))));
        runtime.BeginClientSession(new()); var victim = SessionClient(runtime); var actor = SessionClient(runtime, 3); victimSeat = victim.Identity.Participant;
        var id = actor.Submit(new("test.finish-loss", new([]))).Correlation.Command; runtime.AdvanceOne();
        var incident = RequiredInteraction(victim, id); Equal(null, RequiredResult(actor, id).Interaction);
        Equal(false, f.Data.Unit(0).Token.Occupied); Equal(0, f.Losses.Count); Equal(false, actor.Reply(incident, 0).Accepted);
        Equal(true, victim.Reply(incident, 0).Accepted); runtime.AdvanceOne(); Equal(CommandStatus.Completed, RequiredResult(actor, id).Status);
        Equal(1, f.Losses.Count); f.Life.Random.RequireChoicesConsumed();
    }
    private static void MovementCompletionPrivateSession()
    {
        using var runtime = new SessionRuntime(new(), new(229));
        var f = new CompletionFixture([CompletionCaptureParity()], CreationFixture(new(runtime.Process, runtime.World))); f.AtCity(true);
        f.Data.Unit(0).Orders = UnitOrder.GoTo; f.Data.Unit(0).AuxiliaryTimer = 47; f.Data.Unit(0).LastDirection = 0;
        ParticipantId actorSeat = default, otherSeat = default; int windows = 0;
        runtime.RegisterCommand(new(new(1, "test.finish", "Authored movement finish", AccessClass.Player, new([]), new([new("result")])), (_, _, _) => new(),
            _ => new MovementCompletionOperation(f.Begin(), c => c == 1 ? actorSeat : otherSeat, visit =>
            { ++windows; return new("Captured city", otherSeat, new CommandValue.City(visit.City), new([0])); }, _ => throw new InvalidOperationException("No hut city"))));
        runtime.BeginClientSession(new()); var actor = SessionClient(runtime); var observer = SessionClient(runtime, 2);
        actorSeat = actor.Identity.Participant; otherSeat = observer.Identity.Participant;
        var id = actor.Submit(new("test.finish", new([]))).Correlation.Command; runtime.AdvanceOne();
        var city = RequiredInteraction(actor, id); Equal(false, observer.Reply(city, 0).Accepted); Equal(0, f.OldPositions.Count);
        Equal(true, actor.Reply(city, 0).Accepted); runtime.AdvanceOne(); var warning = RequiredInteraction(actor, id);
        Equal(1, windows); Equal(1, f.OldPositions.Count); Equal((byte)0, f.Data.Unit(0).LastDirection); Equal(false, observer.Reply(warning, 1).Accepted);
        Equal(true, actor.Reply(warning, 1).Accepted); runtime.AdvanceOne(); Equal(CommandStatus.Completed, RequiredResult(actor, id).Status);
        Equal((byte)1, f.Data.Unit(0).LastDirection); Equal(UnitOrder.None, f.Data.Unit(0).Orders); f.Life.Random.RequireChoicesConsumed();
    }
}
