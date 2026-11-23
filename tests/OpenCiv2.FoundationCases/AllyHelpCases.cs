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
    private static Choice GreetingChoice(string call = "receive/greet", uint code = 0) =>
        Choice.Create(new("V05.emissary-greeting", call), new SamplingLaw.SecondaryRange(0, 3), ChoiceCode.Secondary(code));
    private sealed class AllyHelpFixture : IEmissaryEffects, IAllyHelpEffects
    {
        public RevelationFixture Data { get; }
        public GameState World => Data.World;
        public GameplayRandom Random { get; }
        public EmissaryReception Emissaries { get; }
        public AllyHelp Help { get; }
        public WarNotices WarEffects { get; } = new();
        public List<EmissaryNotice> Notices { get; } = [];
        public List<AllyHelpRequest> Refusals { get; } = [];
        public List<(MapPoint Point, int Observer)> Focus { get; } = [];
        public List<int> Leaders { get; } = [];
        public int Closed, Status;
        public string? Failure;
        public AllyHelpFixture(List<Choice>? choices = null, RevelationFixture? data = null)
        {
            Data = data ?? new(); Random = WarRandom((choices ?? []).ToArray());
            World.Globals.HumanPlayers = CivilizationSet.Only(1); World.Multiplayer.LocalHuman = 1;
            World.Globals.Difficulty = Difficulty.Prince; World.Globals.Turn = 100; World.Globals.GameOptions.Diplomacy = true;
            World.Civilizations[1].MilitaryStrength = World.Civilizations[2].MilitaryStrength = 20;
            World.Civilizations[1].CityCount = World.Civilizations[2].CityCount = 2;
            World.Civilizations[2].Attitude[1] = 50; World.Civilizations[2].Gold = 1000;
            World.Civilizations[1].Gold = 10; World.Civilizations[2].Treaties[1].Alliance = true;
            World.Names[1].Tribe = "Players"; World.Names[2].Tribe = "Allies"; World.Names[3].Tribe = "Enemies";
            World.Names[2].Leader = "Ada"; World.Names[2].Adjective = "Allied"; World.Civilizations[2].Flags.FemaleLeader = true;
            World.Names[2].Titles = new(Enumerable.Repeat("Queen", RuleLimits.Governments));
            var governments = new GovernmentChanges(World, Data.Process.Rules, Data.Stacks,
                _ => throw new InvalidOperationException("Unexpected government choice"), _ => throw new InvalidOperationException("Unexpected rate choice"), _ => { });
            var stance = new DiplomaticStance(World, Data.Process.Rules, new(World, Data.Process.Rules, governments, Random));
            Emissaries = new(World, Data.Rules, Random, stance, this);
            Help = new(World, Emissaries, War(Data, WarEffects, Random), this);
        }
        public void Notice(EmissaryNotice notice) { Notices.Add(notice); Fail("notice"); }
        public void OpenLeader(int counterpart) { Leaders.Add(counterpart); Fail("open"); }
        public void CloseLeader() { ++Closed; Fail("close"); }
        public void EnsureVisible(MapPoint point, int observer) { Focus.Add((point, observer)); Fail("focus"); }
        public void DidNotHelp(AllyHelpRequest request) { Refusals.Add(request); Fail("refusal"); }
        public void RefreshStatus() { ++Status; Fail("status"); }
        private void Fail(string effect) { if (Failure == effect) throw new InvalidOperationException("Unavailable " + effect); }
        public EmissaryReception.Operation Receive(MapPoint? point = null) => Emissaries.Begin(1, 2, point, "receive");
        public AllyHelp.Operation Request() => Help.Begin(2, 3, "help");
        public void Threat(int region = 1, ushort attack = 10, ushort military = 1, byte cities = 2)
        { World.Civilizations[2].ContinentCities[region] = cities; World.Civilizations[2].ContinentMilitary[region] = military; World.Civilizations[3].ContinentAttack[region] = attack; }
        public AllyHelp.Operation Demand()
        {
            var op = Request(); Equal(true, op.Resume() is AllyHelpDecision.Emissary);
            Equal(true, op.Resume(0) is AllyHelpDecision.Demand); return op;
        }
    }

    // V05 §2: retained response bypasses, autoplay priority and reset's narrow field ownership.
    private static void EmissaryRetainedResponseAndReset()
    {
        foreach (int response in new[] { -2, -1, 0, 1, 2 })
        {
            var f = new AllyHelpFixture(); f.World.Diplomacy.AskedEmissary = true; f.World.Diplomacy.EmissaryResponse = response;
            using var op = f.Receive(); Equal(null, op.Resume(1)); Equal(response != -1, op.Accepted);
            Equal(0, f.Notices.Count); Equal(0, f.Leaders.Count); Equal(response, f.World.Diplomacy.EmissaryResponse); f.Random.RequireChoicesConsumed();
        }
        foreach (bool asked in new[] { false, true })
        {
            var f = new AllyHelpFixture(); f.World.Multiplayer.Saved.AutoplayActive = true; f.World.Diplomacy.AskedEmissary = asked;
            using var op = f.Receive(); Equal(null, op.Resume()); Equal(false, op.Accepted); Equal(-1, f.World.Diplomacy.EmissaryResponse);
            Equal(asked, f.World.Diplomacy.AskedEmissary); Equal(0, f.Notices.Count); f.Random.RequireChoicesConsumed();
        }
        foreach (int response in new[] { -2, -1, 1, 7 })
        {
            var f = new AllyHelpFixture(response == -1 ? [] : [GreetingChoice()]); f.World.Diplomacy.EmissaryResponse = response;
            using var op = f.Receive(); Equal(null, op.Resume()); Equal(response != -1, op.Accepted); Equal(true, f.World.Diplomacy.AskedEmissary);
            Equal(response == -1 ? 0 : 1, f.Notices.Count); Equal(0, f.Focus.Count); f.Random.RequireChoicesConsumed();
        }
        var reset = new AllyHelpFixture(); var e = reset.World.Diplomacy;
        e.MeetingActive = e.AskedEmissary = e.InitialEvaluationDone = e.OpeningEvaluationActive = true;
        e.EmissaryResponse = -2; e.Attitude = 85; e.SelectedPair = (2, 1); e.DeclareWar = true;
        reset.Emissaries.Reset(); Equal(false, e.MeetingActive || e.AskedEmissary || e.InitialEvaluationDone); Equal(0, e.EmissaryResponse);
        Equal(true, e.OpeningEvaluationActive && e.DeclareWar); Equal(85, e.Attitude); Equal(((int, int)?)(2, 1), e.SelectedPair); Equal(1, reset.Closed);
        var fresh = new GameState(reset.Data.Process).Diplomacy; Equal(false, fresh.MeetingActive || fresh.AskedEmissary); Equal(0, fresh.EmissaryResponse);
    }

    private static void EmissaryReceptionAndIntelligence()
    {
        var f = new AllyHelpFixture([GreetingChoice(code: uint.MaxValue)]); f.World.Civilizations[1].Treaties[2].Embassy = true;
        using var op = f.Receive(new(10, 8));
        var receive = (EmissaryDecision.Receive)op.Resume(1)!; Equal(true, receive.Offer.AllowIntelligence); Equal("Ada", receive.Offer.Counterpart.Leader);
        Equal("Queen", receive.Offer.Counterpart.Title); Equal(true, receive.Offer.Counterpart.Female); Equal(receive, op.Resume());
        Equal(1, f.Focus.Count); Equal(new MapPoint(10, 8), f.Focus[0].Point); Equal(1, f.Focus[0].Observer);
        var report = (EmissaryDecision.Intelligence)op.Resume(2)!; Equal(new EmissaryIntelligence(1, 2), report.Visit);
        Equal(-2, f.World.Diplomacy.EmissaryResponse); Equal(report, op.Resume());
        f.World.Civilizations[1].Treaties[2].Embassy = false;
        receive = (EmissaryDecision.Receive)op.Resume(0)!; Equal(false, receive.Offer.AllowIntelligence); Equal(0, f.World.Diplomacy.EmissaryResponse);
        Equal(2, f.Focus.Count); Equal(null, op.Resume(0)); Equal(true, op.Accepted);
        Equal(3, f.Notices.Single().Variant); Equal(EmissaryNoticeKind.Greeting, f.Notices[0].Kind); Equal(true, f.World.Diplomacy.MeetingActive);
        Equal(2, f.Leaders.Single()); Equal(0, f.Closed); f.Random.RequireChoicesConsumed();

        var refusal = new AllyHelpFixture(); using var denied = refusal.Receive(new(-1, -1));
        Equal(true, denied.Resume() is EmissaryDecision.Receive); Equal(null, denied.Resume(1)); Equal(false, denied.Accepted);
        Equal(-1, refusal.World.Diplomacy.EmissaryResponse); Equal(0, refusal.Notices.Count); Equal(0, refusal.Focus.Count); refusal.Random.RequireChoicesConsumed();
    }

    private static void EmissaryWonderIntelligenceGates()
    {
        foreach (int wonder in new[] { WonderIds.UnitedNations, WonderIds.MarcoPolo })
        {
            var f = new AllyHelpFixture(); f.World.Multiplayer.LocalHuman = 3; f.Data.AddCity(0, 3, new(8, 6));
            f.World.Globals.WonderCity[wonder] = 0;
            using var op = f.Receive(); var question = (EmissaryDecision.Receive)op.Resume()!;
            Equal(true, question.Offer.AllowIntelligence); Equal(1, question.Offer.Actor.Civilization); op.Resume(1); f.Random.RequireChoicesConsumed();
            f.World.Diplomacy.AskedEmissary = false; f.World.Diplomacy.EmissaryResponse = 0; f.World.Globals.WonderCity[wonder] = -1;
            using var absent = f.Receive(); Equal(false, ((EmissaryDecision.Receive)absent.Resume()!).Offer.AllowIntelligence); absent.Resume(1);
        }
    }

    private static void EmissaryNuclearReceptionAndGreeting()
    {
        var f = new AllyHelpFixture([GreetingChoice()]); f.World.Civilizations[2].UnitsActive[UnitTypeIds.NuclearMissile] = 255;
        f.World.Civilizations[2].Treaties[1].Vendetta = true;
        using var forced = f.Receive(); Equal(null, forced.Resume()); Equal(true, forced.Accepted); Equal(0, f.World.Diplomacy.EmissaryResponse);
        Equal(true, f.World.Civilizations[2].Treaties[1].NuclearThreat); // Actual A08 publishes it after the forced flag is captured.
        Equal(2, f.Notices.Count); Equal(EmissaryNoticeKind.ForcedReception, f.Notices[0].Kind);
        Equal(true, f.Notices[1].NuclearWeapons && f.Notices[1].Vendetta); f.Random.RequireChoicesConsumed();
        var known = new AllyHelpFixture(); known.World.Civilizations[2].UnitsActive[UnitTypeIds.NuclearMissile] = 1;
        known.World.Civilizations[2].Treaties[1].NuclearThreat = true;
        using var optional = known.Receive(); Equal(true, optional.Resume() is EmissaryDecision.Receive);
        optional.Resume(1); Equal(false, optional.Accepted); Equal(0, known.Notices.Count); known.Random.RequireChoicesConsumed();
        foreach (bool peace in new[] { false, true })
            foreach (bool threat in new[] { false, true })
                foreach (bool otherNukes in new[] { false, true })
                {
                    var g = new AllyHelpFixture([GreetingChoice("greet")]); g.World.Globals.GameOptions.Diplomacy = false;
                    g.World.Civilizations[1].UnitsActive[UnitTypeIds.NuclearMissile] = 1;
                    g.World.Civilizations[2].UnitsActive[UnitTypeIds.NuclearMissile] = otherNukes ? (byte)1 : (byte)0;
                    g.World.Civilizations[1].Treaties[2].Peace = peace; g.World.Civilizations[1].Treaties[2].NuclearThreat = threat;
                    g.Emissaries.Greet(1, 2, "greet"); bool announce = peace || !threat;
                    Equal(announce ? 2 : 1, g.Notices.Count); Equal(true, g.World.Civilizations[1].Treaties[2].NuclearThreat);
                    if (announce) { Equal(EmissaryNoticeKind.YourNuclearWeapons, g.Notices[1].Kind); Equal(otherNukes ? 2 : 1, g.Notices[1].Variant); }
                    Equal(0, g.Leaders.Count); g.Random.RequireChoicesConsumed();
                }
    }

    private static void EmissaryFailureAndDisposal()
    {
        var remote = new AllyHelpFixture(); remote.World.Globals.Multiplayer = GameMode.Tcp;
        using var denied = remote.Receive(); Throws<InvalidOperationException>(() => denied.Resume()); Equal(false, remote.World.Diplomacy.AskedEmissary);
        Throws<InvalidOperationException>(() => remote.Emissaries.Reset()); Equal(0, remote.Closed);
        var f = new AllyHelpFixture(); using var op = f.Receive(); op.Resume();
        Throws<ChoiceException>(() => op.Resume(0)); Equal(true, f.World.Diplomacy.MeetingActive && f.World.Diplomacy.AskedEmissary);
        Equal(2, f.Leaders.Single()); Equal(0, f.Notices.Count); Throws<InvalidOperationException>(() => op.Resume(0));
        var failure = new AllyHelpFixture([GreetingChoice()]) { Failure = "notice" };
        failure.World.Diplomacy.EmissaryResponse = 1; using var failed = failure.Receive();
        Throws<InvalidOperationException>(() => failed.Resume()); Equal((bool?)null, failed.Accepted); Equal(true, failure.World.Diplomacy.MeetingActive);
        failure.Random.RequireChoicesConsumed();
        var disposed = new AllyHelpFixture(); var stopped = disposed.Receive(); stopped.Resume(); stopped.Dispose();
        Throws<ObjectDisposedException>(() => stopped.Resume(0)); Equal(true, disposed.World.Diplomacy.AskedEmissary); Equal(0, disposed.Closed);
        var reset = new AllyHelpFixture { Failure = "close" }; reset.World.Diplomacy.MeetingActive = reset.World.Diplomacy.AskedEmissary = true;
        reset.World.Diplomacy.EmissaryResponse = 9; Equal(true, reset.Emissaries.Reset() is InvalidOperationException);
        Equal(false, reset.World.Diplomacy.MeetingActive || reset.World.Diplomacy.AskedEmissary); Equal(0, reset.World.Diplomacy.EmissaryResponse);
        foreach (string effect in new[] { "focus", "open" })
        {
            var cosmetic = new AllyHelpFixture([GreetingChoice()]) { Failure = effect };
            using var continued = cosmetic.Receive(new(10, 8)); continued.Resume(); continued.Resume(0);
            Equal(true, continued.Accepted); Equal(1, continued.PresentationFailures.Count); Equal(1, cosmetic.Notices.Count);
            cosmetic.Random.RequireChoicesConsumed();
        }
    }

    // V05 §5: directed admission and reset only after an admitted, completed request.
    private static void AllyHelpAdmissionAndRefusal()
    {
        foreach (int mode in Enumerable.Range(0, 4))
        {
            var f = new AllyHelpFixture(); f.World.Diplomacy.AskedEmissary = true; f.World.Diplomacy.EmissaryResponse = 7;
            if (mode == 0) f.World.Globals.HumanPlayers = default;
            if (mode == 1) { f.World.Civilizations[2].Treaties[1].Alliance = false; f.World.Civilizations[1].Treaties[2].Alliance = true; }
            if (mode == 2) f.World.Civilizations[1].Treaties[3].Alliance = true;
            if (mode == 3) f.World.Civilizations[1].Treaties[3].War = true;
            using var op = f.Request(); Equal(null, op.Resume()); Equal(true, op.Completed); Equal(false, op.Admitted || op.Helped);
            Equal(0, f.Closed); Equal(7, f.World.Diplomacy.EmissaryResponse); f.Random.RequireChoicesConsumed();
        }
        foreach (bool autoplay in new[] { false, true })
        {
            var f = new AllyHelpFixture(); f.World.Multiplayer.Saved.AutoplayActive = autoplay; f.World.Diplomacy.SelectedPair = (2, 1);
            using var op = f.Request(); var pending = op.Resume();
            if (!autoplay) { Equal(true, pending is AllyHelpDecision.Emissary); Equal(null, op.Resume(1)); }
            Equal(true, op.Completed && op.Admitted); Equal(false, op.Helped); Equal((byte)100, f.World.Civilizations[2].Attitude[1]);
            Equal(1, f.Closed); Equal(0, f.World.Diplomacy.EmissaryResponse); Equal(false, f.World.Diplomacy.AskedEmissary);
            Equal(0, f.Refusals.Count); Equal(true, f.World.Civilizations[2].Treaties[1].Alliance); f.Random.RequireChoicesConsumed();
        }
    }

    private static void AllyHelpWarAndCapturedRecipient()
    {
        var f = new AllyHelpFixture([GreetingChoice("help/emissary/greet")]);
        f.World.Civilizations[1].Treaties[3].Peace = true; f.World.Civilizations[2].Attitude[1] = 80;
        using var op = f.Demand(); Equal(true, op.Admitted); Equal(false, op.Completed);
        f.World.Multiplayer.LocalHuman = 4; Equal(null, op.Resume(1)); Equal(true, op.Completed && op.Helped);
        Equal(true, f.World.Civilizations[1].Treaties[3].War && f.World.Civilizations[3].Treaties[1].Vendetta);
        Equal(false, f.World.Civilizations[4].Treaties[3].War); Equal((byte)65, f.World.Civilizations[2].Attitude[1]);
        Equal((byte)1, f.World.Civilizations[1].Reputation); Equal(10, f.World.Civilizations[1].Gold); Equal(1000, f.World.Civilizations[2].Gold);
        Equal(1, f.Closed); Equal(0, f.Status); f.Random.RequireChoicesConsumed();
    }

    private static void AllyHelpThreatAndPayment()
    {
        foreach (var (difficulty, gold, expected) in new[]
        { (Difficulty.Prince, 1000, 500), (Difficulty.Chieftain, 1000, 1000), (Difficulty.Prince, 149, 100), (Difficulty.Prince, -75, -50) })
        {
            var f = new AllyHelpFixture([GreetingChoice("help/emissary/greet")]); f.Threat(); f.World.Globals.Difficulty = difficulty;
            f.World.Civilizations[2].Gold = gold; using var op = f.Demand();
            var bonus = (AllyHelpDecision.Bonus)op.Resume(0)!; Equal(expected, bonus.Request.Payment); Equal(bonus, op.Resume());
            // Real suspension retains price, but accepted payment reads the then-current treasuries.
            f.World.Civilizations[2].Gold = 7; f.World.Civilizations[1].Gold = int.MaxValue; f.World.Multiplayer.LocalHuman = 4;
            Equal(null, op.Resume(1)); Equal(true, op.Helped && op.Completed);
            Equal(unchecked(7 - expected), f.World.Civilizations[2].Gold); Equal(unchecked(int.MaxValue + expected), f.World.Civilizations[1].Gold);
            Equal(1, f.Status); Equal(1, f.Closed); f.Random.RequireChoicesConsumed();
        }
        foreach (int mode in Enumerable.Range(0, 5))
        {
            var f = new AllyHelpFixture([GreetingChoice("help/emissary/greet")]);
            if (mode == 0) { f.Threat(0); f.Threat(63); }
            if (mode == 1) f.Threat(cities: 1);
            if (mode == 2) f.Threat(attack: 1, military: 1);
            if (mode == 3) { f.Threat(); f.World.Civilizations[2].Gold = 49; }
            if (mode == 4) { f.Threat(62, attack: 0, military: 0); }
            using var op = f.Demand(); Equal(null, op.Resume(0)); Equal(true, op.Completed); Equal(false, op.Helped);
            Equal(0, f.Status); Equal(1, f.Refusals.Count); f.Random.RequireChoicesConsumed();
        }
        var edge = new AllyHelpFixture([GreetingChoice("help/emissary/greet")]); edge.Threat(62, attack: 65535, military: 0, cities: 1);
        edge.World.Civilizations[2].CityCount = 1; edge.World.Civilizations[2].Gold = 10000000;
        using var last = edge.Demand(); Equal(6553500, ((AllyHelpDecision.Bonus)last.Resume(7)!).Request.Payment);
        last.Resume(0); Equal(true, last.Completed); edge.Random.RequireChoicesConsumed();
    }

    private static void AllyHelpRejectionAndCommittedFailure()
    {
        foreach (int attitude in new[] { 50, 51 })
        {
            var f = new AllyHelpFixture([GreetingChoice("help/emissary/greet")]); f.Threat();
            f.Data.AddCity(0, 1, new(2, 2)); f.Data.AddCity(1, 2, new(8, 4)); f.Data.AddUnit(0, 1, 0, new(8, 4));
            f.World.Civilizations[2].ContinentCities[0] = 1; f.Data.Unit(0).Orders = UnitOrder.Fortified;
            using var op = f.Demand();
            Equal(true, op.Resume(0) is AllyHelpDecision.Bonus); f.World.Diplomacy.Attitude = attitude;
            f.World.Civilizations[2].Attitude[1] = 5;
            Equal(null, op.Resume(0)); Equal(true, op.Completed); Equal(false, op.Helped); Equal(1, f.Refusals.Count);
            Equal(attitude == 50, f.World.Civilizations[2].Treaties[1].Alliance);
            Equal(attitude == 50 ? (byte)100 : (byte)5, f.World.Civilizations[2].Attitude[1]);
            Equal(attitude == 50 ? 0 : 1, f.WarEffects.Redraws.Count); Equal(1, f.Closed); f.Random.RequireChoicesConsumed();
            Equal(attitude == 50 ? new MapPoint(8, 4) : new(2, 2), f.Data.Unit(0).Position);
            Equal(attitude == 50 ? UnitOrder.Fortified : UnitOrder.None, f.Data.Unit(0).Orders);
        }
        var paid = new AllyHelpFixture([GreetingChoice("help/emissary/greet")]) { Failure = "status" }; paid.Threat();
        using var stopped = paid.Demand(); stopped.Resume(0); Equal(null, stopped.Resume(1));
        Equal(500, paid.World.Civilizations[2].Gold); Equal(510, paid.World.Civilizations[1].Gold);
        Equal(true, paid.World.Civilizations[1].Treaties[3].War); Equal(1, paid.Closed); Equal(true, stopped.Completed && stopped.Helped);
        Equal(1, stopped.PresentationFailures.Count); paid.Random.RequireChoicesConsumed();
        var refused = new AllyHelpFixture([GreetingChoice("help/emissary/greet")]) { Failure = "refusal" };
        using var failed = refused.Demand(); Throws<InvalidOperationException>(() => failed.Resume(0));
        Equal(false, failed.Completed); Equal(0, refused.Closed); Equal((byte)50, refused.World.Civilizations[2].Attitude[1]);
        Throws<InvalidOperationException>(() => failed.Resume(0)); refused.Random.RequireChoicesConsumed();
        var disposed = new AllyHelpFixture(); var nested = disposed.Request(); nested.Resume(); nested.Dispose();
        Throws<ObjectDisposedException>(() => nested.Resume(0)); Equal(true, disposed.World.Diplomacy.AskedEmissary); Equal(0, disposed.Closed);
    }

    private static void AllyHelpSessionPrivacyAndIntelligence()
    {
        using var runtime = new SessionRuntime(new(), new(229));
        var f = new AllyHelpFixture([GreetingChoice("help/emissary/greet")], CreationFixture(new(runtime.Process, runtime.World)));
        f.World.Civilizations[1].Treaties[2].Embassy = true; f.Threat(); ParticipantId recipient = default; int windows = 0;
        runtime.RegisterCommand(new(new(1, "test.ally-help", "Authored ally request", AccessClass.Player, new([]), new([new("admitted", ValueKind.Boolean), new("helped", ValueKind.Boolean)])),
            (_, _, _) => new(), _ => new AllyHelpOperation(f.Request(), _ => recipient, visit =>
            { ++windows; Equal(new EmissaryIntelligence(1, 2), visit); return new("Intelligence", default, null, new([0])); })));
        runtime.BeginClientSession(new()); var other = SessionClient(runtime, 2); var human = SessionClient(runtime, 1); recipient = human.Identity.Participant;
        var command = other.Submit(new("test.ally-help", new([]))).Correlation.Command; runtime.AdvanceOne();
        var prompt = RequiredInteraction(human, command); Equal(null, RequiredResult(other, command).Interaction);
        Equal(false, other.Reply(prompt, 2).Accepted); Equal(true, human.Reply(prompt, 2).Accepted); runtime.AdvanceOne();
        prompt = RequiredInteraction(human, command); Equal(1, windows); runtime.AdvanceOne(); Equal(1, windows);
        Equal(true, human.Reply(prompt, 0).Accepted); runtime.AdvanceOne();
        prompt = RequiredInteraction(human, command); Equal(true, human.Reply(prompt, 0).Accepted); runtime.AdvanceOne();
        prompt = RequiredInteraction(human, command); Equal(true, human.Reply(prompt, 0).Accepted); runtime.AdvanceOne();
        prompt = RequiredInteraction(human, command); Equal(true, human.Reply(prompt, 1).Accepted); runtime.AdvanceOne();
        Equal(CommandStatus.Completed, RequiredResult(other, command).Status); Equal(510, f.World.Civilizations[1].Gold);
        Equal(true, f.World.Civilizations[1].Treaties[3].War); Equal(1, f.Closed); f.Random.RequireChoicesConsumed();
    }

    private static void EmissaryAndHelpSessionCancellation()
    {
        foreach (var input in new[] { OperationInputKind.Cancel, OperationInputKind.Timeout })
        {
            var f = new AllyHelpFixture(); int windows = 0;
            var context = new OperationContext(f.World, f.Data.Process, f.Random, new PresentationRandom(1, "emissary"),
                new(default, new(3)), default, (_, _) => { }, () => { });
            f.World.Civilizations[1].Treaties[2].Embassy = true;
            using var core = f.Receive(); using var session = new EmissaryOperation(core, c => new((ulong)c), _ =>
            { ++windows; return new("Intelligence", default, null, new([0])); });
            Equal(CommandStatus.AwaitingInteraction, session.Resume(context, new()).Status);
            Equal(CommandStatus.AwaitingInteraction, session.Resume(context, new(OperationInputKind.Choice, 2)).Status);
            Equal(CommandStatus.AwaitingInteraction, session.Resume(context, new()).Status); Equal(1, windows);
            Equal(CommandStatus.AwaitingInteraction, session.Resume(context, new(input)).Status); // Close intelligence and rebuild reception.
            Equal(CommandStatus.AwaitingInteraction, session.Resume(context, new(OperationInputKind.Choice, 2)).Status); Equal(2, windows);
            Equal(CommandStatus.AwaitingInteraction, session.Resume(context, new(input)).Status);
            Equal(CommandStatus.Completed, session.Resume(context, new(input)).Status); Equal(false, core.Accepted); Equal(-1, f.World.Diplomacy.EmissaryResponse);
            f.Random.RequireChoicesConsumed();

            foreach (int stage in new[] { 0, 1, 2 })
            {
                var h = new AllyHelpFixture(stage == 0 ? [] : [GreetingChoice("help/emissary/greet")]); h.Threat();
                var helpContext = new OperationContext(h.World, h.Data.Process, h.Random, new PresentationRandom(1, "help"),
                    new(default, new(3)), default, (_, _) => { }, () => { });
                using var request = h.Request(); using var operation = new AllyHelpOperation(request, c => new((ulong)c), _ => throw new InvalidOperationException("Unexpected intelligence"));
                Equal(CommandStatus.AwaitingInteraction, operation.Resume(helpContext, new()).Status);
                if (stage > 0) Equal(CommandStatus.AwaitingInteraction, operation.Resume(helpContext, new(OperationInputKind.Choice, 0)).Status);
                if (stage > 1) Equal(CommandStatus.AwaitingInteraction, operation.Resume(helpContext, new(OperationInputKind.Choice, 0)).Status);
                var result = operation.Resume(helpContext, new(input));
                if (stage == 1) { Equal(CommandStatus.AwaitingInteraction, result.Status); result = operation.Resume(helpContext, new(input)); }
                Equal(CommandStatus.Completed, result.Status); Equal(false, request.Helped); Equal(10, h.World.Civilizations[1].Gold);
                Equal(1000, h.World.Civilizations[2].Gold); Equal(1, h.Closed); h.Random.RequireChoicesConsumed();
            }
        }
    }
}
