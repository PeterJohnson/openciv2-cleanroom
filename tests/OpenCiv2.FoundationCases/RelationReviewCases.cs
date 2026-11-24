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
    private sealed class ReviewFixture : IRelationReviewEffects
    {
        public AllyHelpFixture Aid { get; }
        public RevelationFixture Data => Aid.Data;
        public GameState World => Aid.World;
        public RelationReview Review { get; }
        public List<BorderNotice> Notices { get; } = [];
        public List<MapPoint> Tiles { get; } = [];
        public List<int> Maps { get; } = [];
        public BorderNoticeKind? FailNotice;
        public bool FailRedraw;
        public ReviewFixture(List<Choice>? choices = null, RevelationFixture? data = null)
        {
            Aid = new(choices, data);
            var w = World;
            w.Globals.CivsAlive = CivilizationSet.Only(1).With(2);
            w.Civilizations[2].Treaties[1] = new() { Contact = true, Peace = true };
            w.Civilizations[1].Treaties[2] = new() { Contact = true, Peace = true };
            w.Civilizations[1].NumAdvances = w.Civilizations[2].NumAdvances = 10;
            w.Globals.PowerRank[1] = w.Globals.PowerRank[2] = 3;
            w.Civilizations[1].Tribe = 1; w.Civilizations[2].Tribe = 2;
            var government = new GovernmentChanges(w, Data.Process.Rules, Data.Stacks,
                _ => throw new InvalidOperationException("Unexpected selection"),
                _ => throw new InvalidOperationException("Unexpected rates"), _ => { });
            var stance = new DiplomaticStance(w, Data.Process.Rules, new(w, Data.Process.Rules, government, Aid.Random));
            Review = new(w, Data.Process.Rules, Aid.Random, new(w, Data.Rules, Data.Stacks), stance,
                Aid.Emissaries, Data.Stacks, government, War(Data, Aid.WarEffects, Aid.Random), this);
        }
        public void Intruder(bool repeated = false)
        {
            Data.Rules.Units[0] = Data.Rules.Units[0] with { Role = RuleRole.Attack, Domain = RuleDomain.Land };
            Data.AddCity(0, 2, new(10, 8)); World.Entities.Cities.Storage[0].Name = "Border Town";
            Data.AddUnit(0, 1, 0, new(11, 7)); Data.Unit(0).Flags.BorderWarned = repeated;
        }
        public void Notice(BorderNotice notice)
        { Notices.Add(notice); if (FailNotice == notice.Kind) { throw new InvalidOperationException("Missing border notice"); } }
        public void RedrawTile(MapPoint point)
        { Tiles.Add(point); if (FailRedraw) { throw new InvalidOperationException("Missing map presentation"); } }
        public void RedrawMaps(int observer) => Maps.Add(observer);
        public RelationReview.Operation Begin(int? delta = null) => Review.Begin(2, 1, "review", delta);
    }

    private static Choice BorderDraw(string site, int code, int? modulo = 2) =>
        Choice.Create(new(site, "review"), modulo is { } n ? new SamplingLaw.PrimaryModulo(n) : new SamplingLaw.PrimaryCode(), ChoiceCode.Primary(code));

    private static void ReviewUnknownAndSuppliedDelta()
    {
        var f = new ReviewFixture(); var w = f.World;
        f.Data.AddUnit(0, 3, 0, new(20, 8)); f.Data.Unit(0).Flags.BorderIntruder = true;
        w.Diplomacy.SelectedPair = (2, 1); w.Diplomacy.Attitude = 77;
        using var unknown = f.Begin();
        Throws<InvalidOperationException>(() => unknown.Resume());
        Equal(false, f.Data.Unit(0).Flags.BorderIntruder); Equal((byte)50, w.Civilizations[2].Attitude[1]);
        Equal(77, w.Diplomacy.Attitude); Equal(false, unknown.Completed); Equal((int?)null, unknown.Delta);
        Throws<InvalidOperationException>(() => unknown.Resume(0));
        w.Civilizations[2].Treaties[1].Contact = false;
        using var absent = f.Begin(); Equal(null, absent.Resume()); Equal(true, absent.Completed); Equal(0, absent.Delta);
        Equal((byte)50, w.Civilizations[2].Attitude[1]); Equal(77, w.Diplomacy.Attitude);
        w.Civilizations[2].Treaties[1].Contact = true;
        using var supplied = f.Begin(10); supplied.Resume(); Equal(7, supplied.Delta);
        Equal((byte)57, w.Civilizations[2].Attitude[1]); Equal(84, w.Diplomacy.Attitude);
        supplied.Resume(); Equal((byte)57, w.Civilizations[2].Attitude[1]);
        using var wrap = f.Begin(int.MinValue); wrap.Resume(); Equal(2147483645, wrap.Delta);
        Equal((byte)0, w.Civilizations[2].Attitude[1]);
        w.Civilizations[2].Treaties[1].Contact = false; w.Civilizations[2].Attitude[1] = 200;
        using var zero = f.Begin(); zero.Resume(); Equal(0, zero.Delta); Equal((byte)100, w.Civilizations[2].Attitude[1]);
        w.Globals.Multiplayer = GameMode.Tcp;
        using var remote = f.Begin(0); Throws<InvalidOperationException>(() => remote.Resume());
        f.Aid.Random.RequireChoicesConsumed();
    }

    private static void ReviewFirstWarningAndRecentPeace()
    {
        foreach (bool cosmeticFailure in new[] { false, true })
        {
            var f = new ReviewFixture(); f.Intruder(); f.FailRedraw = cosmeticFailure;
            var w = f.World;
            w.Map.TileAt(new(10, 8)).Features |= MapFeatures.Road;
            using var op = f.Begin(999);
            var prompt = (RelationReviewDecision.Border)op.Resume()!;
            Equal(false, prompt.Request.Repeated); Equal(1, prompt.Request.Recipient); Equal("Border Town", prompt.Request.City);
            Equal(prompt, op.Resume()); Equal(true, f.Data.Unit(0).Flags.BorderWarned && f.Data.Unit(0).Flags.BorderIntruder);
            Equal(true, w.Entities.Cities.Storage[0].KnownTo.Contains(1)); Equal((byte)4, w.Entities.Cities.Storage[0].RememberedSize[1]);
            Equal(true, w.Map.TileAt(new(10, 8)).RevealedTo.Contains(1));
            Equal(MapFeatures.None, w.Map.Remembered(1, new(10, 8))); // Exploration does not copy features.
            op.Resume(7); Equal(true, op.Completed && op.KeepMarkers); Equal(-2, op.Delta);
            Equal((byte)48, w.Civilizations[2].Attitude[1]); Equal(new MapPoint(11, 7), f.Data.Unit(0).Position);
            Equal(0, f.Aid.Closed); Equal(cosmeticFailure ? 1 : 0, op.PresentationFailures.Count);
            f.Aid.Random.RequireChoicesConsumed();
        }
        foreach (int age in new[] { 0, 1, 2, 3 })
        {
            var f = new ReviewFixture(); f.Intruder(); f.World.Civilizations[1].LastContactTurn[2] = (short)(100 - age);
            using var op = f.Begin(10); var prompt = op.Resume();
            if (age is 1 or 2)
            { Equal(null, prompt); Equal(7, op.Delta); Equal(0, f.Tiles.Count); }
            else
            { Equal(true, prompt is RelationReviewDecision.Border); op.Resume(0); Equal(-2, op.Delta); }
            Equal(true, op.Completed && op.KeepMarkers);
        }
        foreach (bool peace in new[] { false, true })
        {
            var f = new ReviewFixture(); f.Intruder(); f.World.Civilizations[2].Treaties[1].Peace = peace;
            f.World.Diplomacy.RecentPeace[1] = true;
            using var op = f.Begin(10); Equal(null, op.Resume()); Equal(true, op.KeepMarkers);
            Equal(peace ? 7 : 9, op.Delta); Equal(peace ? 1 : 0, f.Notices.Count);
            if (peace) { Equal(BorderNoticeKind.Terms, f.Notices.Single().Kind); }
            Equal(0, f.Tiles.Count); f.Aid.Random.RequireChoicesConsumed();
        }
    }

    private static void ReviewNoPeaceEmissaryAndIntrusion()
    {
        foreach (bool accept in new[] { false, true })
        {
            List<Choice> choices = [BorderDraw("A08.border-complaint", 1), BorderDraw("A08.border-intrusion", 2, null)];
            if (accept) { choices.Add(GreetingChoice("review/emissary/greet")); }
            var f = new ReviewFixture(choices); f.Intruder(); var w = f.World;
            w.Globals.Turn = 102; w.Civilizations[2].Treaties[1].Peace = false; w.Civilizations[1].Reputation = 3;
            using var op = f.Begin();
            Equal(true, op.Resume() is RelationReviewDecision.Emissary);
            Equal(true, w.Civilizations[1].Treaties[2].HostileIntent && w.Civilizations[2].Treaties[1].HostileIntent);
            op.Resume(accept ? 0 : 1); Equal(true, op.Completed); Equal(false, op.KeepMarkers);
            Equal(1, op.Delta); Equal((byte)51, w.Civilizations[2].Attitude[1]); Equal(false, f.Data.Unit(0).Flags.BorderIntruder);
            Equal(accept, w.Entities.Cities.Storage[0].KnownTo.Contains(1));
            Equal(false, w.Map.TileAt(new(10, 8)).RevealedTo.Contains(1));
            Equal(accept ? 1 : 0, f.Notices.Count); if (accept) { Equal(BorderNoticeKind.NearCity, f.Notices.Single().Kind); }
            Equal(1, f.Aid.Closed); f.Aid.Random.RequireChoicesConsumed();
        }
        var skipped = new ReviewFixture([BorderDraw("A08.border-complaint", 1)]); skipped.Intruder();
        skipped.World.Globals.Turn = 101; skipped.World.Civilizations[2].Treaties[1].Peace = false;
        skipped.World.Civilizations[2].Treaties[1].Vendetta = true;
        using var quiet = skipped.Begin(); Equal(null, quiet.Resume()); Equal(1, skipped.World.Diplomacy.Intrusions.RepeatOffenses);
        Equal(0, skipped.Aid.Closed); skipped.Aid.Random.RequireChoicesConsumed();
    }

    private static void ReviewWithdrawalAndMissingDeltaPrefix()
    {
        foreach (bool knownDelta in new[] { false, true })
        {
            var f = new ReviewFixture(); f.Intruder(true); var w = f.World;
            f.Data.AddCity(1, 1, new(20, 8));
            f.Data.Rules.Units[4] = f.Data.Rules.Units[0] with { Range = 2 };
            f.Data.AddUnit(1, 1, 4, new(11, 7));
            f.Data.AddUnit(2, 3, 0, new(24, 8)); f.Data.Unit(2).Flags.BorderIntruder = true;
            f.Data.AddUnit(3, 1, 0, new(20, 8)); f.Data.Unit(3).Visibility = CivilizationSet.Only(3);
            for (int i = 0; i < 2; ++i)
            { f.Data.Unit(i).Orders = UnitOrder.Fortified; f.Data.Unit(i).Counter = 9; f.Data.Unit(i).Visibility = CivilizationSet.Only(2); }
            using var op = f.Begin(knownDelta ? 10 : null);
            Equal(true, ((RelationReviewDecision.Border)op.Resume()!).Request.Repeated);
            if (knownDelta) { op.Resume(0); Equal(true, op.Completed); Equal(7, op.Delta); }
            else { Throws<InvalidOperationException>(() => op.Resume(0)); Equal(false, op.Completed); }
            for (int i = 0; i < 2; ++i)
            {
                Equal(new MapPoint(20, 8), f.Data.Unit(i).Position); Equal(UnitOrder.None, f.Data.Unit(i).Orders);
                Equal(default(CivilizationSet), f.Data.Unit(i).Visibility); Equal(false, f.Data.Unit(i).Flags.BorderIntruder);
            }
            Equal((byte)9, f.Data.Unit(0).Counter); Equal((byte)0, f.Data.Unit(1).Counter);
            Equal(false, f.Data.Unit(2).Flags.BorderIntruder); Equal(CivilizationSet.Only(3), f.Data.Unit(3).Visibility);
            Equal(BorderNoticeKind.Withdrawn, f.Notices.Single().Kind); Equal(2, f.Notices.Single().Units);
            Equal(1, f.Maps.Single()); Equal(1, f.Aid.Closed); f.Aid.Random.RequireChoicesConsumed();
        }
        var stranded = new ReviewFixture(); stranded.Intruder(true); stranded.Data.Unit(0).Orders = UnitOrder.Fortified;
        using var noCity = stranded.Begin(10); noCity.Resume(); noCity.Resume(0);
        Equal(new MapPoint(11, 7), stranded.Data.Unit(0).Position); Equal(UnitOrder.Fortified, stranded.Data.Unit(0).Orders);
        Equal(false, stranded.Data.Unit(0).Flags.BorderIntruder);
    }

    private static void ReviewFightSenateAndFailures()
    {
        foreach (var government in new[] { Government.Democracy, Government.Republic, Government.Despotism })
            foreach (int roll in new[] { 0, 1 })
                foreach (bool prohibit in new[] { false, true })
                {
                    var f = new ReviewFixture(government == Government.Democracy ? [] : [BorderDraw("A08.border-senate", roll)]);
                    f.Intruder(true); var w = f.World; w.Civilizations[1].Government = government;
                    w.Globals.Status.Scenario = prohibit; w.Scenario.Flags.RevolutionRestriction = true;
                    using var op = f.Begin(10); op.Resume(); op.Resume(9);
                    bool scandal = !prohibit && (government == Government.Democracy || government == Government.Republic && roll == 1);
                    Equal(scandal ? Government.Anarchy : government, w.Civilizations[1].Government);
                    Equal(true, w.Civilizations[1].Treaties[2].War && w.Civilizations[2].Treaties[1].War);
                    Equal((byte)100, w.Civilizations[2].Attitude[1]); Equal(10, op.Delta);
                    Equal(false, f.Data.Unit(0).Flags.BorderIntruder); Equal(1, f.Aid.Closed);
                    Equal(scandal ? 2 : 1, f.Notices.Count); Equal(BorderNoticeKind.Violate, f.Notices[^1].Kind);
                    f.Aid.Random.RequireChoicesConsumed();
                }
        var failed = new ReviewFixture(); failed.Intruder(true); failed.World.Civilizations[1].Government = Government.Democracy;
        failed.FailNotice = BorderNoticeKind.SenateScandal;
        using var operation = failed.Begin(10); operation.Resume(); Throws<InvalidOperationException>(() => operation.Resume(1));
        Equal(Government.Democracy, failed.World.Civilizations[1].Government); Equal(false, failed.World.Civilizations[1].Treaties[2].War);
        Equal(true, failed.Data.Unit(0).Flags.BorderIntruder); Equal(0, failed.Aid.Closed);
        Throws<InvalidOperationException>(() => operation.Resume(0));
        var stopped = new ReviewFixture(); stopped.Intruder(true);
        using var disposed = stopped.Begin(10); disposed.Resume(); disposed.Dispose();
        Throws<ObjectDisposedException>(() => disposed.Resume(1)); Equal(false, stopped.World.Civilizations[1].Treaties[2].War);
    }

    private static void ReviewAttitudeArithmetic()
    {
        static int Delta(int seed, Action<ReviewFixture> configure)
        {
            var f = new ReviewFixture(); f.World.Civilizations[2].Treaties[1].Peace = false;
            f.World.Civilizations[2].UnitsActive[UnitTypeIds.NuclearMissile] = 1;
            configure(f); using var op = f.Begin(seed); op.Resume(); Equal(true, op.Completed);
            f.Aid.Random.RequireChoicesConsumed(); return op.Delta!.Value;
        }
        Equal(10, Delta(10, _ => { }));
        Equal(9, Delta(10, f => f.World.Civilizations[2].UnitsActive[UnitTypeIds.NuclearMissile] = 0));
        Equal(9, Delta(10, f => f.World.Globals.PowerRank[2] = 6)); // -3/2 truncates toward zero.
        Equal(8, Delta(10, f => f.World.Globals.Turn = 201));
        foreach (bool distrusted in new[] { false, true })
        {
            Equal(distrusted ? 12 : 11, Delta(10, f =>
            {
                f.World.Globals.Turn = 201; f.World.Globals.Difficulty = Difficulty.King;
                f.World.Globals.PowerRank[1] = f.World.Globals.PowerRank[2] = 7;
                f.World.Civilizations[1].CityCount = 4; f.World.Civilizations[1].Reputation = (byte)(distrusted ? 2 : 0);
            }));
        }
        Equal(6, Delta(10, f => f.World.Globals.Turn = 400));
        Equal(9, Delta(10, f => { f.World.Globals.Turn = 400; f.World.Globals.WonderCity[WonderIds.Apollo] = -2; }));
        Equal(7, Delta(10, f => { f.World.Civilizations[2].MilitaryStrength = 10; f.World.Civilizations[1].MilitaryStrength = 50; }));
        Equal(10, Delta(10, f =>
        {
            f.World.Civilizations[2].MilitaryStrength = 10; f.World.Civilizations[1].MilitaryStrength = 50;
            f.World.Globals.Status.AiCoordination = true; f.World.Civilizations[1].Reputation = 1;
        }));
        Equal(12, Delta(10, f => f.World.Civilizations[2].MilitaryStrength = 41));
        Equal(8, Delta(10, f => { f.World.Civilizations[1].UnitsActive[UnitTypeIds.NuclearMissile] = 1; f.World.Civilizations[2].UnitsActive[UnitTypeIds.NuclearMissile] = 0; }));
        Equal(1, Delta(2, f => { f.World.Civilizations[1].UnitsActive[UnitTypeIds.NuclearMissile] = 1; f.World.Civilizations[2].UnitsActive[UnitTypeIds.NuclearMissile] = 0; }));
        Equal(14, Delta(10, f =>
        {
            f.World.Civilizations[2].Treaties[1].Alliance = true; f.World.Civilizations[2].Gold = 0;
            f.World.Civilizations[1].Gold = 1; f.World.Civilizations[1].NumAdvances = 20;
        }));
        foreach (int wonder in new[] { WonderIds.GreatWall, WonderIds.UnitedNations })
        {
            void Build(ReviewFixture f) { f.Data.AddCity(0, 1, new(20, 8)); f.World.Globals.WonderCity[wonder] = 0; }
            Equal(5, Delta(10, Build)); Equal(-1, Delta(0, Build));
        }
        void Eiffel(ReviewFixture f) { f.Data.AddCity(0, 1, new(20, 8)); f.World.Globals.WonderCity[WonderIds.EiffelTower] = 0; }
        Equal(3, Delta(10, Eiffel)); Equal(-1, Delta(1, Eiffel));
        Equal(-2147483646, Delta(int.MaxValue, f => f.Data.Process.Rules.Leaders[2].Attack = 1));
        Equal(0, Delta(-10, f => f.World.Civilizations[2].Treaties[1].War = true));
    }

    private static void ReviewTurnPrivateBorder()
    {
        foreach (bool repeated in new[] { false, true })
            foreach (bool cancel in new[] { false, true })
            {
                using var runtime = new SessionRuntime(new(), new(241));
                var f = new ReviewFixture([
                    Choice.Create(new("V04.turn-random", "turn"), new SamplingLaw.PrimaryModulo(100), ChoiceCode.Primary(25)),
                Choice.Create(new("V04.turn-senate", "turn"), new SamplingLaw.PrimaryModulo(3), ChoiceCode.Primary(1))
                ], CreationFixture(new(runtime.Process, runtime.World)));
                f.Intruder(repeated); f.World.Globals.Turn = 101;
                if (repeated) { f.Data.AddCity(1, 1, new(20, 8)); }
                var grants = new RetainedGrantFixture(new GrantFixture(f.Data, f.Aid.Random));
                var requests = new WarAllyRequests(f.World, f.Data.Process.Rules, f.Aid.Emissaries, grants.Grants,
                    War(f.Data, f.Aid.WarEffects, f.Aid.Random), f.Aid.RefreshStatus);
                var effects = new DiplomacyTurnChildren();
                var turn = new DiplomacyTurn(f.World, f.Data.Rules, f.Aid.Random, ExchangeService(grants), requests, f.Review, effects);
                ParticipantId recipient = default;
                runtime.RegisterCommand(new(new(1, "test.review", "Authored relation review", AccessClass.Player, new([]), new([new("completed", ValueKind.Boolean)])),
                    (_, _, _) => new(), _ => new DiplomacyTurnOperation(turn.Begin(2, "turn"), _ => recipient,
                        _ => throw new InvalidOperationException("Unexpected intelligence"))));
                runtime.BeginClientSession(new()); var actor = SessionClient(runtime, 2); var human = SessionClient(runtime, 1);
                recipient = human.Identity.Participant;
                var command = actor.Submit(new("test.review", new([]))).Correlation.Command; runtime.AdvanceOne();
                var prompt = RequiredInteraction(human, command); Equal(null, RequiredResult(actor, command).Interaction);
                Equal(false, actor.Reply(prompt, 0).Accepted);
                if (cancel) { Equal(true, human.Cancel(command).Accepted); }
                else { Equal(true, human.Reply(prompt, 0).Accepted); }
                runtime.AdvanceOne(); Equal(repeated ? CommandStatus.Failed : CommandStatus.Completed, RequiredResult(actor, command).Status);
                Equal(false, effects.Calls.Any(c => c.StartsWith("review/") || c.StartsWith("seek/")));
                Equal(!repeated, f.Data.Unit(0).Flags.BorderIntruder);
                Equal(repeated ? new MapPoint(20, 8) : new MapPoint(11, 7), f.Data.Unit(0).Position);
                f.Aid.Random.RequireChoicesConsumed();
            }
    }
}
