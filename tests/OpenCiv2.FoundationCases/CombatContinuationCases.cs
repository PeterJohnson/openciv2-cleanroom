using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private sealed class ContinuationFixture : ICombatContinuationEffects, INuclearEffects, ICombatResolutionEffects, ICombatHistoryEffects
    {
        public ParadropFixture Drop { get; }
        public RevelationFixture Data => Drop.Data;
        public GameState World => Data.World;
        public EliminationFixture Life => Drop.Capture.Life;
        public IncidentFixture Incidents { get; }
        public CombatWorkspace State { get; } = new();
        public PreparedCombat Prepared { get; }
        public CombatContinuation Continuation { get; }
        public List<string> Calls { get; } = [];
        public List<NuclearNotice> Notices { get; } = [];
        public bool FailRedraw, FailNuclearNotice;
        public ContinuationFixture(List<Choice>? choices = null, RevelationFixture? data = null)
        {
            Drop = new(choices, data); Incidents = new(Life.Random, Data);
            Data.Rules.Units[0] = Data.Rules.Units[0] with { Attack = 10, Defense = 1, Firepower = 10, HitPoints = 10 };
            Data.Rules.Units[4] = Data.Rules.Units[4] with { Attack = 2, Defense = 1, Firepower = 1, HitPoints = 10 };
            Data.AddUnit(1, 2, 4, new(12, 8)); World.Globals.MovingUnit = new(0);
            World.Globals.Rules.NoRestart = true; World.Globals.CivsAlive = CivilizationSet.Only(1).With(2).With(3);
            Life.Recreate = owner => Creation(Data, owner, Life.Random);
            Prepared = new CombatPreparation(World, Data.Rules, Data.Stacks, State).Prepare(new(0), 1, true)!;
            State.AnimationDefender = Prepared.Defender;
            Continuation = new(World, Data.Rules, State, Data.Stacks, Life.Elimination, Incidents.Incidents,
                new(World, Life.Random, Data.Stacks, Life.Loss, Life.Elimination, Incidents.Incidents, this),
                new(World, Data.Rules, Data.Stacks, Drop.Drop), this);
        }
        public void ClearDefender() => Life.Deletion.Erase(1, false);
        public void Air(byte range, byte counter = 0, int slot = 0)
        {
            Data.Rules.Units[20] = Data.Rules.Units[0] with { Domain = RuleDomain.Air, Range = range, Movement = 9 };
            Data.Unit(slot).Type = new(20); Data.Unit(slot).Counter = counter; Data.Unit(slot).MovesUsed = 2;
        }
        public void Missile(bool settler = false)
        {
            Data.Rules.Units[20] = Data.Rules.Units[0] with
            { Attack = 99, Role = settler ? RuleRole.Settle : RuleRole.Attack, Abilities = new() { DestroyedAfterAttack = true } };
            Data.Unit(0).Type = new(20); World.Civilizations[1].UnitsActive[20] = 1;
        }
        public void Ocean() { foreach (ref var tile in World.Map.Tiles) tile.Terrain = Terrain.Ocean; }
        public void Complete(CombatContinuation.Operation op)
        {
            for (int i = 0; i < 100 && !op.Completed; ++i) Equal(null, op.Resume());
            Equal(true, op.Completed); Life.Random.RequireChoicesConsumed();
        }
        public void Redraw(MapPoint point) { Calls.Add("redraw:" + point); if (FailRedraw) throw new InvalidOperationException("No renderer"); }
        public void RedrawNeighbors(MapPoint point) { Calls.Add("neighbors:" + point); if (FailRedraw) throw new InvalidOperationException("No renderer"); }
        public void Notice(NuclearNotice notice) { Notices.Add(notice); if (FailNuclearNotice) throw new InvalidOperationException("Notice unavailable"); }
        public void Animate(MapPoint target) => Calls.Add("nuclear");
        public void Sound(CombatSound sound, int argument) => Calls.Add("sound");
        public void Synchronize() => Calls.Add("sync");
        public void Redraw(MapPoint origin, MapPoint target) => Calls.Add("redraw-both");
        public void Explosion(MapPoint point, int kind) => Calls.Add("explosion");
        public int SecondaryColor(int civilization) => civilization;
        public string PluralName(int civilization) => World.Names[civilization].Tribe;
        public void RefreshHistory(int civilization) => Calls.Add("history");
        public CombatResolution Resolution()
        {
            var removal = new CityRemoval(World, Data.Rules, Data.Stacks, Life.Loss, _ => Calls.Add("remove-city"));
            var airlift = new AiProductionAirlift(World, Data.Rules, Data.Stacks, Life.Deletion, Life.Random, _ => Calls.Add("airlift"));
            var chooser = new CityProductionChoice(World, Data.Process.Rules, new(), new(World, Data.Process.Rules, Data.Stacks), Data.Stacks,
                new(World, Data.Rules, ClassicShip, ClassicCalendar), airlift);
            var orders = new ProductionOrders(World, Data.Rules, city => chooser.Choose(city, "production"), _ => Calls.Add("production"));
            return new(World, Data.Rules, State, new(new(World, Data.Rules, Life.Random, _ => Calls.Add("promote")),
                new(World, Data.Stacks, State, new(World, this), Life.Elimination, Incidents.Incidents, _ => Calls.Add("killed")),
                new(World, Data.Rules, Data.Stacks), removal, Life.Elimination, Incidents.Incidents, orders,
                new(World, Data.Rules, Data.Stacks)), this);
        }
    }

    private static void CombatSurvivorCurrentActor()
    {
        foreach (byte range in new byte[] { 0, 1, 2 })
        {
            var f = new ContinuationFixture(); f.Data.AddUnit(2, 3, 0, new(18, 10)); f.World.Globals.MovingUnit = new(2);
            f.Air(range, slot: 2); using var op = f.Continuation.Survive(f.Prepared, "finish"); f.Complete(op);
            Equal(1, op.Result); Equal(range == 1 ? (byte)2 : (byte)9, f.Data.Unit(2).MovesUsed);
            Equal((byte)0, f.Data.Unit(0).MovesUsed); Equal((byte)0, f.Data.Unit(2).Counter); Equal((MapPoint?)null, f.World.Paths.CachedGoal);
        }
        var land = new ContinuationFixture(); land.Data.Unit(0).MovesUsed = 2; land.Data.Unit(0).Counter = 255;
        using var ordinary = land.Continuation.Survive(land.Prepared, "finish"); land.Complete(ordinary);
        Equal(1, ordinary.Result); Equal((byte)2, land.Data.Unit(0).MovesUsed); Equal((byte)255, land.Data.Unit(0).Counter);
        var missile = new ContinuationFixture(); missile.Data.AddUnit(2, 3, 0, new(18, 10)); missile.Missile();
        missile.Data.Unit(2).Type = new(20); missile.Data.Unit(0).Type = new(0); missile.World.Globals.MovingUnit = new(2);
        using var consumed = missile.Continuation.Survive(missile.Prepared, "finish"); missile.Complete(consumed);
        Equal(0, consumed.Result); Equal(true, missile.Data.Unit(0).Token.Occupied); Equal(false, missile.Data.Unit(2).Token.Occupied);
        Equal(0, missile.State.KillCount); Equal(0, missile.World.Casualties[3].Count);
    }

    private static void CombatSurvivorAiFuelAndResponse()
    {
        foreach (var (range, counter, expectedCounter, result) in new (byte, byte, byte, int)[]
        { (0, 255, 255, 0), (1, 0, 1, 1), (2, 0, 1, 0), (2, 1, 2, 1), (128, 127, 128, 1), (255, 254, 255, 1), (2, 127, 128, 0), (2, 255, 0, 0), (1, 255, 0, 0) })
        {
            var f = new ContinuationFixture(); f.World.Globals.HumanPlayers = CivilizationSet.Only(2); f.ClearDefender(); f.Air(range, counter);
            // A separate ground unit makes actual A04's path call observable when fuel permits it.
            f.Data.AddUnit(2, 1, 0, new(10, 10)); f.World.Paths.UniformCost = true;
            using var op = f.Continuation.Survive(f.Prepared, "finish"); f.Complete(op);
            Equal(result, op.Result); Equal(expectedCounter, f.Data.Unit(0).Counter); Equal(range == 1 ? (byte)2 : (byte)9, f.Data.Unit(0).MovesUsed);
            Equal(true, f.Data.Unit(0).Token.Occupied); Equal(result == 0 ? f.Prepared.Target : (MapPoint?)null, f.World.Paths.CachedGoal);
        }
        foreach (bool city in new[] { false, true })
        {
            var f = new ContinuationFixture(); f.World.Globals.HumanPlayers = CivilizationSet.Only(2);
            if (!city) { f.ClearDefender(); f.State.City = null; }
            using var stopped = f.Continuation.Survive(f.Prepared, "finish"); f.Complete(stopped);
            Equal(1, stopped.Result); Equal((MapPoint?)null, f.World.Paths.CachedGoal); // Present defender or absent cached city disables AI response.
        }
        var missile = new ContinuationFixture { FailRedraw = true }; missile.World.Globals.HumanPlayers = CivilizationSet.Only(2);
        missile.ClearDefender(); missile.Missile(); missile.Data.AddUnit(2, 1, 0, new(10, 10)); missile.World.Paths.UniformCost = true;
        using var response = missile.Continuation.Survive(missile.Prepared, "finish"); missile.Complete(response);
        Equal(0, response.Result); Equal(false, missile.Data.Unit(0).Token.Occupied); Equal(missile.Prepared.Target, missile.World.Paths.CachedGoal);
        Equal(1, response.PresentationFailures.Count); Equal(true, missile.Calls.Single().StartsWith("neighbors:"));
    }

    private static ContinuationFixture NestedCombatContinuation(RevelationFixture? data = null)
    {
        var f = new ContinuationFixture(data: data); f.Missile(true); f.World.Civilizations[1].CityCount = 0;
        f.Data.AddUnit(2, 3, 4, new(24, 10)); f.Data.Unit(2).Flags.SpyMission = true; f.Data.Unit(2).Counter = 1;
        f.World.Civilizations[1].Treaties[3].Peace = true; f.State.KillCount = 9;
        return f;
    }
    private static void CombatSurvivorNestedDeletion()
    {
        var f = NestedCombatContinuation(); using var op = f.Continuation.Survive(f.Prepared, "finish");
        var pending = (CombatContinuationDecision.Incident)op.Resume()!;
        Equal(1, pending.Question.Victim.Civilization); Equal(3, pending.Question.Actor.Civilization); Equal(pending, op.Resume());
        Equal(false, f.Data.Unit(0).Token.Occupied || f.Data.Unit(2).Token.Occupied); Equal(false, op.Completed);
        Equal(9, f.State.KillCount); Equal(0, f.World.Casualties[1].Count); Equal(0, f.Life.Checks);
        op.Resume(new(0)); f.Complete(op); Equal(0, op.Result); Equal(EndReason.Defeat, f.World.PendingEndReason);
        var disposed = NestedCombatContinuation(); var stopped = disposed.Continuation.Survive(disposed.Prepared, "finish"); stopped.Resume(); stopped.Dispose();
        Throws<ObjectDisposedException>(() => stopped.Resume(new(0))); Equal(false, disposed.Data.Unit(0).Token.Occupied); Equal(9, disposed.State.KillCount);
    }

    private static void CombatNuclearGateAndInterception()
    {
        var gate = new ContinuationFixture();
        foreach (byte attack in new byte[] { 98, 99, 127, 128, 255 })
        {
            gate.Data.Rules.Units[0] = gate.Data.Rules.Units[0] with { Attack = attack };
            Equal(attack is 99 or 127, gate.Continuation.IsNuclear(gate.Prepared));
        }
        var f = new ContinuationFixture { FailRedraw = true }; f.Missile(); f.World.Civilizations[1].AggressionCounter[2] = 7;
        f.Data.AddUnit(2, 1, 0, new(24, 10)); f.World.Globals.MovingUnit = new(2); f.State.KillCount = 9;
        BuildingQueries.SetBuilding(f.World, new(0), BuildingIds.SdiDefense, true);
        using var op = f.Continuation.Nuclear(f.Prepared, "delivery"); f.Complete(op);
        Equal(0, op.Result); Equal((UnitSlot?)null, f.State.AnimationDefender); Equal(false, f.Data.Unit(0).Token.Occupied);
        Equal(true, f.Data.Unit(1).Token.Occupied && f.Data.Unit(2).Token.Occupied); Equal((sbyte)5, f.World.Entities.Cities.Storage[0].Size);
        Equal((byte)7, f.World.Civilizations[1].AggressionCounter[2]); Equal(9, f.State.KillCount); Equal(0, f.World.Casualties[1].Count);
        Equal(1, op.PresentationFailures.Count); Equal(false, f.Calls.Contains("nuclear"));
    }

    private static void CombatNuclearDetonationAndResponse()
    {
        foreach (bool city in new[] { false, true })
        {
            var f = new ContinuationFixture(); f.Missile(); f.Ocean(); f.World.Globals.HumanPlayers = CivilizationSet.Only(2);
            f.World.Civilizations[1].AggressionCounter[2] = 19; f.State.KillCount = 9; f.Data.AddUnit(2, 1, 0, new(8, 12));
            if (!city) f.State.City = null;
            using var op = f.Continuation.Nuclear(f.Prepared, "delivery"); f.Complete(op);
            Equal(0, op.Result); Equal(false, f.Data.Unit(0).Token.Occupied || f.Data.Unit(1).Token.Occupied);
            Equal(true, f.Data.Unit(2).Token.Occupied); Equal((sbyte)3, f.World.Entities.Cities.Storage[0].Size);
            Equal((byte)0, f.World.Civilizations[1].AggressionCounter[2]); Equal(9, f.State.KillCount); Equal(0, f.World.Casualties[2].Count);
            Equal(city ? f.Prepared.Target : (MapPoint?)null, f.World.Paths.CachedGoal); Equal(true, f.Calls.Contains("nuclear"));
        }
        var nested = NestedCombatContinuation(); nested.Ocean(); nested.World.Civilizations[1].AggressionCounter[2] = 19;
        using var delayed = nested.Continuation.Nuclear(nested.Prepared, "delivery");
        Equal(true, delayed.Resume() is CombatContinuationDecision.Incident); Equal(false, delayed.Completed);
        Equal((sbyte)5, nested.World.Entities.Cities.Storage[0].Size); Equal((byte)19, nested.World.Civilizations[1].AggressionCounter[2]);
        delayed.Resume(new(0)); nested.Complete(delayed); Equal(0, delayed.Result); Equal((sbyte)3, nested.World.Entities.Cities.Storage[0].Size);
        Equal((byte)0, nested.World.Civilizations[1].AggressionCounter[2]);
    }

    private static void CombatContinuationFailuresAndPrivacy()
    {
        var absent = new ContinuationFixture(); absent.World.Globals.MovingUnit = null;
        using var missing = absent.Continuation.Survive(absent.Prepared, "finish"); Throws<InvalidOperationException>(() => missing.Resume());
        Throws<InvalidOperationException>(() => missing.Resume()); Equal((byte)0, absent.Data.Unit(0).MovesUsed);
        var remote = new ContinuationFixture(); remote.World.Globals.Multiplayer = GameMode.Tcp;
        using var denied = remote.Continuation.Nuclear(remote.Prepared, "delivery"); Throws<InvalidOperationException>(() => denied.Resume());
        Equal(remote.Prepared.Defender, remote.State.AnimationDefender); Equal(0, remote.Notices.Count);
        var failure = new ContinuationFixture { FailNuclearNotice = true }; failure.World.Globals.HumanPlayers = CivilizationSet.Only(1).With(2);
        using var failed = failure.Continuation.Nuclear(failure.Prepared, "delivery"); Throws<InvalidOperationException>(() => failed.Resume());
        Equal((UnitSlot?)null, failure.State.AnimationDefender); Equal(true, failure.Data.Unit(0).Token.Occupied && failure.Data.Unit(1).Token.Occupied);
        Throws<InvalidOperationException>(() => failed.Resume());

        using var runtime = new SessionRuntime(new(), new(221)); var f = NestedCombatContinuation(CreationFixture(new(runtime.Process, runtime.World)));
        ParticipantId victimSeat = default;
        runtime.RegisterCommand(new(new(1, "test.combat-survivor", "Authored combat continuation", AccessClass.Player, new([]), new([new("result")])),
            (_, _, _) => new(), _ => new CombatContinuationOperation(f.Continuation.Survive(f.Prepared, "finish"), _ => victimSeat, _ => throw new InvalidOperationException("Unexpected city window"))));
        runtime.BeginClientSession(new()); var actor = SessionClient(runtime, 2); var victim = SessionClient(runtime, 1); victimSeat = victim.Identity.Participant;
        var command = actor.Submit(new("test.combat-survivor", new([]))).Correlation.Command; runtime.AdvanceOne();
        var question = RequiredInteraction(victim, command); Equal(null, RequiredResult(actor, command).Interaction);
        Equal(false, actor.Reply(question, 0).Accepted); Equal(true, victim.Reply(question, 0).Accepted);
        runtime.AdvanceOne(); Equal(CommandStatus.Completed, RequiredResult(actor, command).Status); Equal(0, f.Life.Checks); f.Life.Random.RequireChoicesConsumed();
    }

    private static void CombatResponseParadropCaptureDecision()
    {
        var choices = Enumerable.Range(0, 8).Select(i => Choice.Create(new("U05.paradrop-approach", "finish/response/paradrop", i),
            new SamplingLaw.PrimaryModulo(6), ChoiceCode.Primary(0))).ToList();
        var f = new ContinuationFixture(choices); f.ClearDefender(); f.World.Globals.HumanPlayers = CivilizationSet.Only(2); f.World.Multiplayer.LocalHuman = 2;
        f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Abilities = new() { Paradrop = true } };
        f.Data.AddCity(2, 1, f.Prepared.Origin); f.World.Civilizations[1].CityCount = 1;
        BuildingQueries.SetBuilding(f.World, new(0), BuildingIds.Palace, true);
        f.World.Entities.Cities.Storage[0].Size = 10; f.World.Entities.Cities.Storage[1].Size = 9;
        f.World.Civilizations[2].Gold = 1500; f.World.Civilizations[2].TotalCitySize = 19;
        var context = new OperationContext(f.World, f.Data.Process, f.Life.Random, new PresentationRandom(1, "continuation"),
            new(default, new(3)), default, (_, _) => { }, () => { });
        var op = f.Continuation.Survive(f.Prepared, "finish");
        using var session = new CombatContinuationOperation(op, c => new((ulong)c), _ => throw new InvalidOperationException("Unexpected city window"));
        var prompt = session.Resume(context, new()); Equal(CommandStatus.AwaitingInteraction, prompt.Status);
        Equal(new ParticipantId(2), prompt.Interaction!.Recipient);
        Equal(true, op.Pending is CombatContinuationDecision.Response { Decision: ParadropDecision.Capture { Decision: CityCaptureDecision.Palace } });
        Equal(false, op.Completed); Equal(f.Prepared.Target, f.Data.Unit(0).Position); Equal(true, f.Data.Unit(0).Flags.Paradropped);
        Equal(new CivilizationSlot(2), f.World.Entities.Cities.Storage[0].Owner); Equal(1500, f.World.Civilizations[2].Gold);
        Equal(CommandStatus.AwaitingInteraction, session.Resume(context, new()).Status); f.Life.Random.RequireChoicesConsumed();
        session.Dispose(); Throws<ObjectDisposedException>(() => op.Resume(new(0))); Equal(f.Prepared.Target, f.Data.Unit(0).Position);
    }

    private static void CombatEntryThroughSurvivor()
    {
        var f = new ContinuationFixture([CombatChoice("U06.round.attack", 80, 79), CombatChoice("U06.round.defense", 4, 0),
            Choice.Create(new("U06.promotion", "resolution/promotion"), new SamplingLaw.PrimaryModulo(84), ChoiceCode.Primary(0))]);
        f.World.Globals.HumanPlayers = CivilizationSet.Only(1).With(2);
        var government = new GovernmentChanges(f.World, f.Data.Process.Rules, f.Data.Stacks, _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), _ => { });
        var entry = new CombatEntry(f.World, f.Data.Rules, f.State, f.Life.Random, new(f.World, f.Data.Rules, _ => { }),
            War(f.Data, new(), f.Life.Random), new(f.World, f.Data.Process.Rules, government, f.Life.Random), _ => { });
        using var admitted = entry.Begin(f.Prepared, new(false, true), "entry"); Equal(null, admitted.Resume()); Equal(false, admitted.Result!.Cancelled);
        Equal(80, admitted.Result.Combat.Attack); Equal(4, admitted.Result.Combat.Defense);
        var opening = new CombatOpening(f.World, f.Data.Rules, f.State, f.Data.Stacks, new(f.World, f.Data.Rules, f.Data.Stacks), () => { }, _ => { });
        Equal(true, opening.Present(admitted.Result.Combat, 1, "").Visible);
        using var rounds = new CombatRounds(f.World, f.Data.Rules, f.Life.Random, f.State, new RoundEffects()).Begin(admitted.Result.Combat, "fight");
        Equal(10, rounds.Advance());
        using var resolution = f.Resolution().Begin(admitted.Result.Combat, true, "resolution");
        for (int i = 0; i < 100 && !resolution.Completed; ++i) Equal(null, resolution.Resume());
        Equal(true, resolution.Completed);
        using var survivor = f.Continuation.Survive(admitted.Result.Combat, "finish"); f.Complete(survivor);
        Equal(1, survivor.Result); Equal((byte)3, f.Data.Unit(0).MovesUsed); Equal(true, f.Data.Unit(0).Flags.Veteran);
        Equal(false, f.Data.Unit(1).Token.Occupied); Equal((sbyte)4, f.World.Entities.Cities.Storage[0].Size);
        Equal(1, f.World.Casualties[2].Count); Equal(1, f.State.KillCount); Equal(f.Prepared.Origin, f.Data.Unit(0).Position);
    }
}
