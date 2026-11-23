using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Formats;
using OpenCiv2.Formats.Classic;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private sealed class RansomFixture : IBarbarianRansomEffects
    {
        public EliminationFixture Life { get; }
        public RevelationFixture Data => Life.Data;
        public GameState World => Data.World;
        public ClassicTextInput Text { get; } = new();
        public BarbarianRansom Ransom { get; }
        public List<string> Effects { get; } = [];
        public bool FailCosmetic;
        public MapPoint Target { get; } = new(12, 10);
        public RansomFixture(RevelationFixture? data = null, List<Choice>? choices = null)
        {
            Life = new(choices, data); var incidents = new IncidentFixture(Life.Random, Data);
            World.Globals.Year = 200; World.Globals.HumanPlayers = CivilizationSet.Only(1);
            World.Civilizations[1].Gold = 150; World.Civilizations[1].CityCount = 2;
            World.Civilizations[2].CityCount = 1;
            Data.AddCity(0, 2, new(8, 10), 4); Data.AddCity(1, 1, Target, 9);
            World.Entities.Cities.Storage[1].Name = "Willow";
            Data.AddUnit(0, 0, 4, new(10, 10));
            Text.Files["scenario/CITY.TXT"] = "@BARBARIANS\n1\nLeave your gold!\n";
            Ransom = new(World, Data.Stacks, Life.Loss, Life.Elimination, incidents.Incidents, new ClassicRansomText(Text.Reader()), this);
        }
        public BarbarianRansom.Operation Begin(CitySlot? nearest = null) => Ransom.Begin(new(0), Target, nearest ?? new(0), "ransom");
        public void EnsureVisible(MapPoint point, int observer) => Cosmetic("visible:" + observer + ":" + point.X);
        public void RefreshStatus() => Cosmetic("status");
        public void Redraw(MapPoint point) => Cosmetic("draw:" + point.X + "," + point.Y);
        private void Cosmetic(string value) { Effects.Add(value); if (FailCosmetic) { throw new InvalidOperationException("Presentation unavailable"); } }
        public void Complete(BarbarianRansom.Operation op, int reply)
        {
            for (int i = 0; i < 100 && op.Result is null; ++i) { op.Resume(reply); }
            Equal(true, op.Result is not null); Life.Random.RequireChoicesConsumed();
        }
    }

    // W06 ransom and P04/F03 required content: scenario/install lookup, decimal counts, variant identity.
    private static void RansomClassicContent()
    {
        var f = new ClassicTextInput(); f.Files["scenario/CITY.TXT"] = "@OTHER\nignored\n";
        f.Files["home/CITY.TXT"] = "@BARBARIANS\n3\nFirst\nCaf\xe9 raiders\nThird\n";
        var content = new ClassicRansomText(f.Reader()).Read(new(11, 9));
        Equal(new BarbarianRansomText(true, "Café raiders"), content);
        Sequence(new[] { "scenario/CITY.TXT", "home/CITY.TXT" }, f.Opens); Equal("caller", f.Directories.Working);
        Equal("scenario", f.Directories.Search); Equal(1, f.Traces["home/CITY.TXT"].Closes);
        f.Directories.Extension = "GER"; f.Files["scenario/CITY.GER"] = "@BARBARIANS\n2\nFirst German\nSecond German\n";
        Equal("First German", new ClassicRansomText(f.Reader()).Read(new(12, 10)).Message);
        Equal("scenario/CITY.GER", f.Opens[^1]);
        f = new(); Equal(new BarbarianRansomText(false, null), new ClassicRansomText(f.Reader()).Read(new(12, 10)));
        Equal(true, f.Opens.Contains("scenario/CITY.TXT")); Equal(true, f.Opens.Contains("home/CITY.TXT"));
    }

    private static void RansomContentCountsAndFailures()
    {
        foreach (string count in new[] { "0", "-3", "bad", "0x2", "0b1", "4294967296", "" })
        {
            var f = new ClassicTextInput(); f.Files["scenario/CITY.TXT"] = "@BARBARIANS\n" + count + "\n";
            Equal(new BarbarianRansomText(true, null), new ClassicRansomText(f.Reader()).Read(new(12, 10)));
        }
        foreach (string count in new[] { "1", "+1suffix", "4294967297", "-4294967295" })
        {
            var f = new ClassicTextInput(); f.Files["scenario/CITY.TXT"] = "@BARBARIANS\n" + count + "\n\n";
            Equal(new BarbarianRansomText(true, ""), new ClassicRansomText(f.Reader()).Read(new(12, 10)));
        }
        var empty = new ClassicTextInput(); empty.Files["scenario/CITY.TXT"] = "@BARBARIANS\n";
        Equal(new BarbarianRansomText(true, null), new ClassicRansomText(empty.Reader()).Read(new(12, 10)));
        var negative = new ClassicTextInput(); negative.Files["scenario/CITY.TXT"] = "@BARBARIANS\n7\nunused\n";
        Equal("7", new ClassicRansomText(negative.Reader()).Read(new(-1, 0)).Message);
        var broken = new ClassicTextInput(); broken.Files["scenario/CITY.TXT"] = "@BARBARIANS\n1\n";
        Throws<InvalidDataException>(() => new ClassicRansomText(broken.Reader()).Read(new(12, 10)));
        Equal(1, broken.Traces["scenario/CITY.TXT"].Closes);
    }

    private static void RansomDemandAndRetainedCity()
    {
        foreach (var (gold, size, demand) in new[] { (150, 4, 100), (300, 4, 150), (50, -128, 50), (3175, 127, 3175) })
        {
            var f = new RansomFixture(); f.World.Civilizations[1].Gold = gold; f.World.Entities.Cities.Storage[0].Size = (sbyte)size;
            f.Data.Unit(0).Orders = UnitOrder.GoTo; f.Data.Unit(0).Destination = f.Data.Unit(0).Position;
            using var op = f.Begin(); var offer = (BarbarianRansomDecision.Offer)op.Resume()!;
            Equal(demand, offer.Demand); Equal("Willow", offer.CityName); Equal(new CitySlot(1), offer.City.Slot);
            Equal((CitySlot?)new CitySlot(1), op.City); Equal(1, offer.Victim); Equal(new UnitSlot(0), offer.Actor.Slot);
            Equal("Leave your gold!", offer.Message); Equal(gold, f.World.Civilizations[1].Gold);
            Equal(offer, op.Resume()); Equal(1, f.Text.Opens.Count); Equal(1, f.Effects.Count);
            f.Complete(op, 0); Equal(BarbarianRansomOutcome.Continue, op.Result); Equal(gold, f.World.Civilizations[1].Gold);
            Equal(UnitOrder.GoTo, f.Data.Unit(0).Orders); // Only the owning chooser runs Finish.
        }
    }

    private static void RansomStrongestAndAdmission()
    {
        foreach (var (year, cities, rank, gold, expected) in new[]
        { (200, 5, 7, 999, 499), (201, 4, 7, 999, 499), (201, 5, 6, 999, 499),
            (201, 5, 7, 999, -1), (201, 5, 7, 150, -1), (201, 5, 7, 1000, 1000), (201, 5, 7, 1199, 1100) })
        {
            var f = new RansomFixture(); f.World.Globals.Year = (short)year; f.World.Globals.PowerRank[1] = (byte)rank;
            f.World.Civilizations[1].CityCount = (short)cities; f.World.Civilizations[1].Gold = gold;
            using var op = f.Begin(); var offer = op.Resume();
            if (expected < 0) { Equal(null, offer); Equal(BarbarianRansomOutcome.Continue, op.Result); Equal(0, f.Text.Opens.Count); }
            else { Equal(expected, ((BarbarianRansomDecision.Offer)offer!).Demand); f.Complete(op, 0); }
        }
        foreach (bool human in new[] { false, true })
        {
            var f = new RansomFixture(); if (human) { f.World.Civilizations[1].Gold = 99; } else { f.World.Globals.HumanPlayers = default; }
            using var op = f.Begin(); f.Complete(op, 0); Equal(BarbarianRansomOutcome.Continue, op.Result);
            Equal((CitySlot?)new CitySlot(0), op.City); Equal(0, f.Text.Opens.Count);
        }
    }

    private static void RansomContentRoutesAndAutoplay()
    {
        foreach (int variant in new[] { 0, 1, 2 })
        {
            var f = new RansomFixture();
            if (variant == 0) { f.Text.Files.Clear(); }
            if (variant == 1) { f.Text.Files["scenario/CITY.TXT"] = "@BARBARIANS\n0\n"; }
            if (variant == 2) { f.World.Multiplayer.Saved.AutoplayActive = true; }
            f.Data.Unit(0).Orders = UnitOrder.GoTo; f.Data.Unit(0).Destination = f.Data.Unit(0).Position;
            using var op = f.Begin(); f.Complete(op, 0);
            Equal(variant == 0 ? BarbarianRansomOutcome.Finish : BarbarianRansomOutcome.Continue, op.Result);
            Equal((CitySlot?)new CitySlot(1), op.City); Equal(150, f.World.Civilizations[1].Gold);
            Equal(UnitOrder.GoTo, f.Data.Unit(0).Orders); Equal(variant == 2 ? 1 : 0, f.Effects.Count);
            Equal(true, f.Data.Unit(0).Token.Occupied);
        }
    }

    private static void RansomActualStackRemoval()
    {
        var f = new RansomFixture();
        f.Data.AddUnit(1, 2, 4, new(13, 9)); f.Data.AddUnit(2, 0, 4, new(13, 9));
        f.Data.AddUnit(3, 0, 4, new(14, 10)); f.Data.AddUnit(4, 2, 4, new(14, 10));
        // Imported mixed-owner links: the actual first unit gates removal of the whole linked stack.
        f.Data.Unit(2).Next = new(1); f.Data.Unit(1).Previous = new(2);
        f.Data.Unit(4).Next = new(3); f.Data.Unit(3).Previous = new(4);
        f.Data.AddUnit(5, 0, 4, new(20, 10));
        using var op = f.Begin(); Equal(true, op.Resume() is BarbarianRansomDecision.Offer);
        // The response commits against current state, including newly placed units; no precomputed deletion list.
        f.Data.AddUnit(6, 0, 4, f.Target); // Radius center remains excluded.
        f.Complete(op, 1); Equal(BarbarianRansomOutcome.Paid, op.Result); Equal(50, f.World.Civilizations[1].Gold);
        foreach (int i in new[] { 0, 1, 2 }) { Equal(false, f.Data.Unit(i).Token.Occupied); }
        foreach (int i in new[] { 3, 4, 5, 6 }) { Equal(true, f.Data.Unit(i).Token.Occupied); }
        Equal(0, f.World.Civilizations[0].Gold); Equal(new CivilizationSlot(1), f.World.Entities.Cities.Storage[1].Owner);
        Sequence(new[] { "visible:1:12", "status", "draw:13,9", "draw:10,10" }, f.Effects);
        op.Resume(1); Equal(50, f.World.Civilizations[1].Gold); Equal(4, f.Effects.Count);
    }

    private static void RansomLivePaymentAndActorSurvival()
    {
        foreach (int currentGold in new[] { 20, int.MinValue })
        {
            var f = new RansomFixture(); f.Data.Stacks.Relocate(0, new(30, 10), false);
            f.Data.Unit(0).Orders = UnitOrder.GoTo; f.Data.Unit(0).Destination = new(30, 10);
            using var op = f.Begin(); var offer = (BarbarianRansomDecision.Offer)op.Resume()!;
            Equal(100, offer.Demand); f.World.Civilizations[1].Gold = currentGold;
            f.Complete(op, 7); Equal(unchecked(currentGold - 100), f.World.Civilizations[1].Gold);
            Equal(BarbarianRansomOutcome.Paid, op.Result); Equal(true, f.Data.Unit(0).Token.Occupied);
            Equal(UnitOrder.GoTo, f.Data.Unit(0).Orders); Equal(2, f.Effects.Count);
        }
    }

    private static void RansomFailuresAndDisposal()
    {
        var cosmetic = new RansomFixture { FailCosmetic = true }; using var success = cosmetic.Begin();
        Equal(true, success.Resume() is BarbarianRansomDecision.Offer); cosmetic.Complete(success, 1);
        Equal(BarbarianRansomOutcome.Paid, success.Result); Equal(3, success.PresentationFailures.Count);
        Equal(false, cosmetic.Data.Unit(0).Token.Occupied); Equal(50, cosmetic.World.Civilizations[1].Gold);
        var missing = new RansomFixture(); missing.Text.Files["scenario/CITY.TXT"] = "@BARBARIANS\n1\n";
        using var malformed = missing.Begin(); Throws<InvalidDataException>(() => malformed.Resume());
        Equal((CitySlot?)new CitySlot(1), malformed.City); Equal(150, missing.World.Civilizations[1].Gold);
        Throws<InvalidOperationException>(() => malformed.Resume());
        var late = new RansomFixture(); using var failed = late.Begin(); failed.Resume();
        late.Data.Unit(0).Type = new(255); Throws<ArgumentOutOfRangeException>(() => failed.Resume(1));
        Equal(50, late.World.Civilizations[1].Gold); Equal(true, late.Data.Unit(0).Token.Occupied);
        Throws<InvalidOperationException>(() => failed.Resume(1));
        var abandoned = new RansomFixture(); var disposed = abandoned.Begin(); disposed.Resume(); disposed.Dispose();
        Equal(150, abandoned.World.Civilizations[1].Gold); Equal((CitySlot?)new CitySlot(1), disposed.City);
        Throws<ObjectDisposedException>(() => disposed.Resume(1));
        var network = new RansomFixture(); network.World.Globals.Multiplayer = GameMode.Tcp;
        using var unsupported = network.Begin(); Throws<InvalidOperationException>(() => unsupported.Resume()); Equal(0, network.Text.Opens.Count);
    }

    private static RansomFixture RansomNested(RevelationFixture? data = null)
    {
        var f = new RansomFixture(data, [EscapeChoice("ransom/stack-0/unit/1/check/spy/3", 2, 0),
            IncidentChoice(0, call: "ransom/stack-0/unit/1/check/spy/3/incident")]);
        f.Life.Recreate = owner => Creation(f.Data, owner, f.Life.Random);
        f.World.Globals.HumanPlayers = f.World.Globals.CivsAlive = CivilizationSet.Only(1).With(3);
        f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Role = RuleRole.Settle };
        f.Data.AddUnit(1, 3, 0, new(13, 9)); f.World.Civilizations[3].UnitsActive[0] = 1;
        f.Data.AddUnit(2, 0, 4, new(13, 9));
        f.Data.Unit(2).Next = new(1); f.Data.Unit(1).Previous = new(2);
        f.Data.AddUnit(3, 1, 47, new(30, 10)); f.Data.Unit(3).Flags.SpyMission = true; f.Data.Unit(3).Counter = 3;
        DiplomaticRelations.Set(f.World, 1, 3, new() { Peace = true });
        f.Data.AddUnit(4, 0, 4, new(14, 10));
        return f;
    }
    private static void RansomNestedLifecycle()
    {
        var f = RansomNested(); using var op = f.Begin(); op.Resume();
        var pending = (BarbarianRansomDecision.Lifecycle)op.Resume(1)!;
        Equal(3, ((EspionagePretext)pending.Decision).Victim.Civilization);
        Equal(50, f.World.Civilizations[1].Gold); Equal(false, f.Data.Unit(1).Token.Occupied);
        Equal(false, f.Data.Unit(2).Token.Occupied); Equal(false, f.Data.Unit(3).Token.Occupied);
        Equal(true, f.Data.Unit(0).Token.Occupied); Equal(true, f.Data.Unit(4).Token.Occupied);
        Equal(2, f.Effects.Count); Equal(pending, op.Resume());
        f.Complete(op, 0); Equal(BarbarianRansomOutcome.Paid, op.Result); Equal(50, f.World.Civilizations[1].Gold);
        Equal(1, f.Life.Restarts); // Actual V01 creation rejects a nonzero-turn human restart.
        Equal(false, f.Data.Unit(0).Token.Occupied); Equal(false, f.Data.Unit(4).Token.Occupied);
        Sequence(new[] { "visible:1:12", "status", "draw:13,9", "draw:14,10", "draw:10,10" }, f.Effects);
    }

    private static void RansomPrivateSession()
    {
        foreach (bool pay in new[] { false, true })
        {
            using var runtime = new SessionRuntime(new(), new(726));
            var f = new RansomFixture(CreationFixture(new(runtime.Process, runtime.World))); ParticipantId victimSeat = default;
            runtime.RegisterCommand(new(new(1, "test.ransom", "Ransom", AccessClass.Player, new([]), new([new("outcome", ValueKind.Text)])),
                (_, _, _) => new(), _ => new BarbarianRansomOperation(f.Begin(), _ => victimSeat)));
            runtime.BeginClientSession(new()); var caller = SessionClient(runtime, 2); var victim = SessionClient(runtime); victimSeat = victim.Identity.Participant;
            var command = caller.Submit(new("test.ransom", new([]))).Correlation.Command; runtime.AdvanceOne();
            Equal(null, RequiredResult(caller, command).Interaction); var prompt = RequiredInteraction(victim, command);
            Equal(false, caller.Reply(prompt, 1).Accepted); Equal(false, victim.Reply(prompt, 9).Accepted);
            if (pay) { Equal(true, victim.Reply(prompt, 1).Accepted); } else { Equal(true, victim.Cancel(command).Accepted); }
            runtime.AdvanceOne(); Equal(CommandStatus.Completed, RequiredResult(caller, command).Status);
            Equal(new CommandValue.Text(pay ? "Paid" : "Continue"), RequiredResult(caller, command).Result.Single().Value);
            Equal(pay ? 50 : 150, f.World.Civilizations[1].Gold); Equal(!pay, f.Data.Unit(0).Token.Occupied);
            Equal(false, victim.Reply(prompt, 1).Accepted);
        }
    }

    private static void RansomTimeoutAndPrivateLifecycle()
    {
        var f = new RansomFixture();
        var context = new OperationContext(f.World, f.Data.Process, f.Life.Random, new PresentationRandom(2, "ransom"),
            new(default, new(1)), default, (_, _) => { }, () => { });
        using (var op = new BarbarianRansomOperation(f.Begin(), _ => new(1)))
        {
            Equal(CommandStatus.AwaitingInteraction, op.Resume(context, new()).Status);
            Equal(CommandStatus.Completed, op.Resume(context, new(OperationInputKind.Timeout)).Status);
            Equal(150, f.World.Civilizations[1].Gold); Equal(true, f.Data.Unit(0).Token.Occupied);
        }
        using var runtime = new SessionRuntime(new(), new(727));
        f = RansomNested(CreationFixture(new(runtime.Process, runtime.World)));
        Dictionary<int, ParticipantId> seats = [];
        runtime.RegisterCommand(new(new(1, "test.ransom", "Ransom", AccessClass.Player, new([]), new([new("outcome", ValueKind.Text)])),
            (_, _, _) => new(), _ => new BarbarianRansomOperation(f.Begin(), c => seats[c])));
        runtime.BeginClientSession(new()); var caller = SessionClient(runtime, 2); var victim = SessionClient(runtime); var other = SessionClient(runtime, 3);
        seats[1] = victim.Identity.Participant; seats[3] = other.Identity.Participant;
        var command = caller.Submit(new("test.ransom", new([]))).Correlation.Command; runtime.AdvanceOne();
        var payment = RequiredInteraction(victim, command); victim.Reply(payment, 1); runtime.AdvanceOne();
        var incident = RequiredInteraction(other, command); Equal(false, victim.Reply(incident, 0).Accepted);
        Equal(50, f.World.Civilizations[1].Gold); Equal(true, f.Data.Unit(4).Token.Occupied);
        other.Reply(incident, 0); runtime.AdvanceOne(); Equal(CommandStatus.Completed, RequiredResult(caller, command).Status);
        Equal(false, f.Data.Unit(4).Token.Occupied); f.Life.Random.RequireChoicesConsumed();
    }
}
