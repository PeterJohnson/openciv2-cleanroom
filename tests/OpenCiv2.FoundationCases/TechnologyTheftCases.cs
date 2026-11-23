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
    private sealed class TheftFixture : ITechnologyTheftEffects, IResearchChoiceEffects
    {
        public CityDamageFixture Damage { get; }
        public RevelationFixture Data => Damage.Data;
        public GameState World => Data.World;
        public RuleTables Rules => Data.Rules;
        public ref OpenCiv2.Core.State.City City => ref Damage.City;
        public GameplayRandom Random => Damage.Lifecycle.Random;
        public GrantFixture Grant { get; }
        public CapturedTechnology Captured { get; }
        public CityTechnologyTheft Theft { get; }
        public List<TechnologyTheftNotice> Notices { get; } = [];
        public List<TakenTechnologyNotice> Taken { get; } = [];
        public List<string> Order { get; } = [];
        public List<ResearchGoalNotice> GoalNotices { get; } = [];
        public List<ResearchPathNotice> Paths { get; } = [];
        public Queue<ResearchReply> GoalReplies { get; } = new();
        public bool FailNotice;
        public bool DiplomacyActive => false;
        public TheftFixture(List<Choice>? choices = null, RevelationFixture? data = null)
        {
            Damage = new(choices, data);
            Rules.Cosmic = Rules.Cosmic with { FoodPerCitizen = 2, FoodBoxRows = 10, ShieldBoxRows = 10 };
            for (int a = 0; a < RuleLimits.Advances; ++a)
                Rules.Advances[a] = new() { Identity = new("advance" + a, "Advance " + a), AiValue = 10, Prerequisites = new([AdvanceReference.None, AdvanceReference.None]) };
            for (int u = 0; u < RuleLimits.UnitTypes; ++u) Rules.Units[u] = Rules.Units[u] with { Prerequisite = AdvanceReference.Never, ObsoleteBy = AdvanceReference.Never };
            for (int i = 0; i < RuleLimits.Improvements; ++i) Rules.Improvements[i] = Rules.Improvements[i] with { Prerequisite = AdvanceReference.Never };
            Grant = new(Data, Random);
            var production = new CityProductionChoice(World, Data.Process.Rules, new(), new(World, Data.Process.Rules, Data.Stacks), Data.Stacks,
                new(World, Rules, ClassicShip, ClassicCalendar), new(World, Rules, Data.Stacks, Damage.Lifecycle.Deletion, Random, _ => throw new InvalidOperationException("Unexpected airlift")));
            Captured = new(World, Data.Process.Rules, Grant.Grants,
                new(World, Rules, city => production.Choose(city, "take-production"), _ => { }),
                new(World, Rules, this), Taken.Add);
            Theft = new(World, Rules, Random, Data.Stacks, Grant.Grants, Captured, Damage.Lifecycle.Elimination, Damage.Incidents.Incidents, this);
        }
        public void Known(params int[] advances)
        {
            foreach (int a in advances) { World.Civilizations[2].Advances = World.Civilizations[2].Advances.With(a); World.Globals.AdvanceKnownBy[a] = CivilizationSet.Only(2); }
        }
        public CityTechnologyTheft.Operation Begin() => Theft.Begin(Damage.Mission, "theft");
        public void Sound() => Order.Add("sound");
        public void Redraw(MapPoint point) => Order.Add("redraw");
        public void Notice(TechnologyTheftNotice notice) { Notices.Add(notice); Order.Add("notice:" + notice.Kind); if (FailNotice) throw new InvalidOperationException("Notice failed"); }
        public void Advice(ResearchAdvice _) => throw new InvalidOperationException("Unexpected advice");
        public void CloseAdvice() => throw new InvalidOperationException("Unexpected advice close");
        public void Selected(int c, int a) => throw new InvalidOperationException("Informational goal cannot select research");
        public void Encyclopedia(ResearchTopic topic, int identity) => Order.Add("pedia");
        public ResearchReply GoalChoice(ResearchGoalNotice notice) { GoalNotices.Add(notice); return GoalReplies.Dequeue(); }
        public int GoalHelp(ResearchTopic topic, int identity, int prerequisite) => -1;
        public void GoalPath(ResearchPathNotice notice) => Paths.Add(notice);
    }
    private static Choice TheftStart(int value = 0) => Choice.Create(new("V06.theft.advance-start", "theft"), new SamplingLaw.PrimaryModulo(100), ChoiceCode.Primary(value));
    private static Choice TheftDetect(int unit, int value) => Choice.Create(new("V06.theft.detect", "theft", unit), new SamplingLaw.PrimaryModulo(100), ChoiceCode.Primary(value));

    // V03 §12: independent eligibility, last ties and best-value retention through actual research-goal help.
    private static void CapturedTechnologySelectionAndHelp()
    {
        var f = new TheftFixture(); f.Known(90, 91); // Disabled Enabled flag does not remove a capture option.
        var op = f.Captured.Begin(1, 2, "take"); var first = op.Resume()!;
        Sequence(new[] { 90, 91 }, first.Options.Select(o => o.Identity)); Equal(91, first.Recommendation); Equal(true, first.Interactive);
        Equal(first, op.Resume()); f.Rules.Advances[91] = f.Rules.Advances[91] with { AiValue = 1 };
        f.Rules.Advances[90] = f.Rules.Advances[90] with { AiValue = 1, Enabled = true };
        f.GoalReplies.Enqueue(new(90)); var rebuilt = op.Resume(new(0, 9))!;
        Equal(91, rebuilt.Recommendation); Equal(1, f.GoalNotices.Count); Equal(true, f.Paths.Single().Stealing);
        Equal(0, f.Grant.Acquisitions.Count); Equal(false, f.World.Civilizations[1].Advances.Contains(91));
        // Accepted custom reply is not checked against victim knowledge or the options.
        op.Resume(new(88)); Equal(88, op.Result); Equal(true, op.Finished);
        Equal(true, f.World.Civilizations[1].Advances.Contains(88)); Equal((byte)2, f.World.Civilizations[1].AdvanceSource[88]);
        op.Resume(new(90)); Equal((byte)1, f.World.Civilizations[1].NumAdvances); f.Random.RequireChoicesConsumed();
        f = new(); f.Known(90, 91); f.Rules.Advances[90] = f.Rules.Advances[90] with { Prerequisites = new([AdvanceReference.Never, AdvanceReference.Never]) };
        f.Rules.Advances[91] = f.Rules.Advances[91] with { Prerequisites = new([AdvanceReference.Never, (AdvanceReference)80]) };
        Sequence(new[] { 91 }, f.Captured.Begin(1, 2, "eligibility").Resume()!.Options.Select(o => o.Identity));
        f.World.Globals.Status.Scenario = true; f.World.Scenario.Flags.GovernmentLock = true;
        f.Known(AdvanceIds.Monarchy, AdvanceIds.Communism, AdvanceIds.Fundamentalism, AdvanceIds.Republic, AdvanceIds.Democracy);
        Sequence(new[] { 91 }, f.Captured.Begin(1, 2, "scenario").Resume()!.Options.Select(o => o.Identity));
        f.World.Globals.Status.Scenario = false;
        Equal(6, f.Captured.Begin(1, 2, "no-scenario").Resume()!.Options.Count);
    }

    private static void CapturedTechnologyGatesAndActualProduction()
    {
        foreach (bool autoplay in new[] { false, true })
        {
            var f = new TheftFixture(); f.Known(90); f.World.Multiplayer.LocalHuman = 2; f.World.Multiplayer.Saved.AutoplayActive = autoplay;
            f.World.Globals.HumanPlayers = CivilizationSet.Only(1).With(2);
            var op = f.Captured.Begin(1, 2, "take"); var choice = op.Resume()!; Equal(false, choice.Interactive); Equal(0, choice.Options.Count);
            op.Resume(new(90)); Equal(autoplay ? 0 : 1, f.Taken.Count); Equal(true, f.World.Civilizations[1].Advances.Contains(90));
            f.Random.RequireChoicesConsumed();
        }
        var ai = new TheftFixture(); ai.Known(90, 91); ai.World.Globals.HumanPlayers = CivilizationSet.Only(2);
        ai.Data.AddCity(1, 1, new(4, 4)); ai.World.Civilizations[1].CityCount = 1;
        ai.World.Civilizations[1].ContinentPolicy[1] = 4; ai.World.Globals.Difficulty = Difficulty.Prince;
        ai.World.Entities.Cities.At(new(1)).Production = 0;
        ai.Rules.Units[4] = ai.Rules.Units[4] with { Prerequisite = AdvanceReference.None, Role = RuleRole.Defend, Attack = 1, Defense = 1, CostRows = 4 };
        ai.World.Civilizations[1].UnitsInProduction[0] = 1;
        var taken = ai.Captured.Begin(1, 2, "take"); Equal(null, taken.Resume()); Equal(true, taken.Finished); Equal(91, taken.Result);
        Equal((sbyte)4, ai.World.Entities.Cities.At(new(1)).Production); Equal((sbyte)4, ai.City.Production); // Foreign production unchanged.
        Equal((byte)0, ai.World.Civilizations[1].UnitsInProduction[0]); Equal((byte)1, ai.World.Civilizations[1].UnitsInProduction[4]);
        ai.Random.RequireChoicesConsumed();
        foreach (int zero in new[] { 0, 1 })
        {
            var f = new TheftFixture(); f.Known(90);
            var op = f.Captured.Begin(zero == 0 ? 0 : 1, zero == 0 ? 2 : 0, "barbarian"); Equal(null, op.Resume()); Equal(true, op.Finished); Equal(null, op.Result);
            Equal((byte)0, f.World.Civilizations[1].NumAdvances); f.Random.RequireChoicesConsumed();
        }
        var invalid = new TheftFixture(); invalid.Known(90); var bad = invalid.Captured.Begin(1, 2, "bad"); bad.Resume();
        Throws<ArgumentOutOfRangeException>(() => bad.Resume(new(-1))); Throws<InvalidOperationException>(() => bad.Resume(new(90)));
        Equal(false, invalid.World.Civilizations[1].Advances.Contains(90));
    }

    // V06 §5: actual V07 race changes cyclic candidate selection, including last ordinary qualifier.
    private static void TheftCyclicSelectionAndRace()
    {
        foreach (bool race in new[] { false, true })
            foreach (bool human in new[] { false, true })
                foreach (Difficulty difficulty in new[] { Difficulty.Warlord, Difficulty.Prince })
                {
                    var f = new TheftFixture([TheftStart(90)]); f.Known(88, 90, 91); f.Data.Unit(0).Type = new(46);
                    f.World.Globals.HumanPlayers = human ? CivilizationSet.Only(1) : CivilizationSet.Only(2);
                    f.World.Globals.Difficulty = difficulty; f.World.Globals.WonderCity[WonderIds.Apollo] = (short)(race ? -2 : -1);
                    using var op = f.Begin(); Equal(null, op.Resume()); Equal(true, op.Completed);
                    int selected = race && (human || difficulty >= Difficulty.Prince) ? 88 : 90;
                    Equal(true, f.World.Civilizations[1].Advances.Contains(selected)); Equal((byte)2, f.World.Civilizations[1].AdvanceSource[selected]);
                    Equal(true, f.City.Flags.TechnologyStolen); Equal(false, f.Data.Unit(0).Token.Occupied); f.Random.RequireChoicesConsumed();
                }
        foreach (int preferred in new[] { 91, AdvanceIds.FusionPower })
        {
            var f = new TheftFixture([TheftStart(90)]); f.Known(88, 90, 91, preferred); f.Data.Unit(0).Type = new(46);
            f.World.Globals.WonderCity[WonderIds.Apollo] = -2;
            if (preferred == 91) f.Rules.Improvements[BuildingIds.Module] = f.Rules.Improvements[BuildingIds.Module] with { Prerequisite = (AdvanceReference)91 };
            using var op = f.Begin(); op.Resume(); Equal(true, f.World.Civilizations[1].Advances.Contains(preferred)); f.Random.RequireChoicesConsumed();
        }
    }

    private static void TheftNoCandidateAndCancellation()
    {
        foreach (bool human in new[] { false, true })
            foreach (bool peaceful in new[] { false, true })
            {
                var f = new TheftFixture([TheftStart()]); f.World.Globals.HumanPlayers = human ? CivilizationSet.Only(1) : default;
                f.World.Civilizations[1].Treaties[2].Peace = peaceful; f.Data.Unit(0).MovesUsed = 254;
                using var op = f.Begin();
                if (human && peaceful) { DamageQuestion(op.Resume(), CityEspionageQuestion.TreatyBreach); op.Resume(new(1)); } else op.Resume();
                Equal(true, op.Completed); Equal(human ? null : (CityMission?)(peaceful ? CityMission.Embassy : CityMission.Sabotage), op.Fallback);
                Equal(human ? (byte)1 : (byte)254, f.Data.Unit(0).MovesUsed); Equal(true, f.Data.Unit(0).Token.Occupied);
                Equal(false, f.City.Flags.TechnologyStolen); Equal(human ? 1 : 0, f.Notices.Count); f.Random.RequireChoicesConsumed();
            }
        var cancel = new TheftFixture(); cancel.Damage.Incidents.Treaty(); cancel.World.Multiplayer.Saved.AutoplayActive = true;
        using var denied = cancel.Begin(); DamageQuestion(denied.Resume(), CityEspionageQuestion.TreatyBreach); denied.Resume(new(0));
        Equal(true, denied.Completed); Equal(0, cancel.Notices.Count); cancel.Random.RequireChoicesConsumed();
        var blocked = new TheftFixture([TheftStart()]); blocked.Known(90);
        blocked.Rules.Advances[90] = blocked.Rules.Advances[90] with { Prerequisites = new([AdvanceReference.Never, AdvanceReference.Never]) };
        using var empty = blocked.Begin(); empty.Resume(); Equal(TechnologyTheftNoticeKind.Unavailable, blocked.Notices.Single().Kind); blocked.Random.RequireChoicesConsumed();
    }

    private static void TheftDefenderDetectionAndEscape()
    {
        foreach (int defenderType in new[] { 46, 47 })
            foreach (bool veteran in new[] { false, true })
                foreach (bool detected in new[] { false, true })
                {
                    int threshold = defenderType == 46 ? 20 : veteran ? 60 : 40;
                    var f = new TheftFixture([TheftStart(), TheftDetect(1, detected ? threshold - 1 : threshold)]); f.Known(90);
                    f.Data.Unit(0).Type = new(46); f.Data.AddUnit(1, 3, defenderType, f.City.Position); f.Data.Unit(1).Flags.Veteran = veteran;
                    using var op = f.Begin(); Equal(null, op.Resume()); Equal(true, op.Completed);
                    Equal(!detected, f.World.Civilizations[1].Advances.Contains(90)); Equal(!detected, f.City.Flags.TechnologyStolen);
                    Equal(detected, f.Data.Unit(0).Token.Occupied); Equal(detected ? (byte)6 : (byte)3, f.Data.Unit(0).MovesUsed);
                    Equal(detected ? TechnologyTheftNoticeKind.Detected : TechnologyTheftNoticeKind.Stolen, f.Notices.Single().Kind);
                    if (detected) { Equal(new UnitType((byte)defenderType), f.Notices.Single().Detector); Equal(0, f.World.Civilizations[2].Attitude[1]); }
                    f.Random.RequireChoicesConsumed();
                }
        // The physical stack's head is tested first, with no owner filter; later detecting units do not roll.
        var ordered = new TheftFixture([TheftStart(), TheftDetect(3, 20), TheftDetect(2, 39)]); ordered.Known(90); ordered.Data.Unit(0).Type = new(46);
        ordered.Data.AddUnit(1, 3, 47, ordered.City.Position); ordered.Data.AddUnit(2, 3, 47, ordered.City.Position); ordered.Data.AddUnit(3, 3, 46, ordered.City.Position);
        using var scan = ordered.Begin(); scan.Resume(); Equal(new UnitType(47), ordered.Notices.Single().Detector); ordered.Random.RequireChoicesConsumed();
    }

    private static void TheftHardAndTargetedOrdering()
    {
        var ordinary = new TheftFixture([TheftStart(), EscapeChoice("theft/escape", 2, 0), IncidentChoice(0, call: "theft/escape/incident")]);
        ordinary.Known(90); ordinary.World.Globals.HumanPlayers = CivilizationSet.Only(1).With(2); ordinary.World.Civilizations[2].Treaties[1].Peace = true;
        using (var op = ordinary.Begin())
        {
            op.Resume(); Equal(true, op.Resume(new(0)) is CityEspionageDecision.Incident);
            Equal(true, ordinary.City.Flags.TechnologyStolen); Equal(true, ordinary.World.Civilizations[1].Advances.Contains(90));
            Equal(false, op.Completed); op.Resume(new(0)); Equal(true, op.Completed); ordinary.Random.RequireChoicesConsumed();
        }
        foreach (bool targeted in new[] { false, true })
        {
            var choices = new List<Choice> { TheftStart(), EscapeChoice("theft/capture", 3, 1), EscapeChoice("theft/escape", 2, 1, true),
                IncidentChoice(0, call: "theft/escape/incident") };
            var f = new TheftFixture(choices); f.Known(90, 91); f.World.Globals.HumanPlayers = CivilizationSet.Only(1).With(2);
            f.World.Civilizations[2].Treaties[1].Peace = true; f.City.Flags.TechnologyStolen = !targeted;
            f.Data.AddCity(1, 1, new(4, 4));
            using var op = f.Begin(); DamageQuestion(op.Resume(), targeted ? CityEspionageQuestion.SpecificTheft : CityEspionageQuestion.HardTheft);
            var next = op.Resume(new(1));
            if (targeted)
            {
                var menu = ((CityEspionageDecision.Technology)next!).Choice;
                Sequence(new[] { 90, 91 }, menu.Options.Select(o => o.Identity)); Equal(new MapPoint(4, 4), f.Data.Unit(0).Position);
                next = op.Resume(new(91));
            }
            Equal(true, next is CityEspionageDecision.Incident); Equal(!targeted, f.City.Flags.TechnologyStolen);
            Equal(false, f.Data.Unit(0).Token.Occupied); Equal(true, f.World.Civilizations[1].Advances.Contains(targeted ? 91 : 90));
            Equal(null, op.Resume(new(0))); Equal(true, op.Completed); Equal(true, f.City.Flags.TechnologyStolen); f.Random.RequireChoicesConsumed();
        }
        foreach (bool targeted in new[] { false, true })
        {
            var f = new TheftFixture([TheftStart(), EscapeChoice("theft/capture", 3, 0)]); f.Known(90); f.City.Flags.TechnologyStolen = !targeted;
            using var op = f.Begin(); op.Resume(); op.Resume(new(1)); Equal(true, op.Completed);
            Equal(false, f.World.Civilizations[1].Advances.Contains(90)); Equal(SpyFate.Caught, f.Damage.Lifecycle.SpyNotices.Single().Fate);
            Equal(0, f.Notices.Count); f.Random.RequireChoicesConsumed();
        }
    }

    private static void TheftGrantEffectsAndFailurePrefixes()
    {
        var f = new TheftFixture([TheftStart(AdvanceIds.Gunpowder)]); f.Known(AdvanceIds.Gunpowder); f.Data.Unit(0).Type = new(46);
        f.Data.AddCity(1, 1, new(4, 4)); BuildingQueries.SetBuilding(f.World, new(1), BuildingIds.Barracks, true);
        f.Rules.Improvements[BuildingIds.Barracks] = f.Rules.Improvements[BuildingIds.Barracks] with { CostRows = 3 };
        f.World.Civilizations[1].Gold = 10;
        using (var op = f.Begin()) { op.Resume(); Equal(true, op.Completed); }
        Equal(false, BuildingQueries.HasBuilding(f.World, new(1), BuildingIds.Barracks)); Equal(40, f.World.Civilizations[1].Gold);
        Equal(1, f.Grant.Sales.Count); Equal(true, f.City.Flags.TechnologyStolen); f.Random.RequireChoicesConsumed();
        f = new([TheftStart()]) { FailNotice = true }; f.Known(90); f.Data.Unit(0).Type = new(46);
        using var notice = f.Begin(); Throws<InvalidOperationException>(() => notice.Resume()); Throws<InvalidOperationException>(() => notice.Resume());
        Equal(false, f.World.Civilizations[1].Advances.Contains(90)); Equal(false, f.City.Flags.TechnologyStolen); Equal(true, f.Data.Unit(0).Token.Occupied); f.Random.RequireChoicesConsumed();
        f = new([TheftStart()]); f.Known(AdvanceIds.Monarchy); f.Data.Unit(0).Type = new(46); f.Grant.OnGovernmentChoice = () => throw new InvalidOperationException("Government choice unavailable");
        using var grant = f.Begin(); Throws<InvalidOperationException>(() => grant.Resume());
        Equal(true, f.World.Civilizations[1].Advances.Contains(AdvanceIds.Monarchy)); Equal(false, f.City.Flags.TechnologyStolen); Equal(true, f.Data.Unit(0).Token.Occupied); f.Random.RequireChoicesConsumed();
        f = new(); f.World.Globals.Multiplayer = GameMode.Tcp; using var remote = f.Begin(); Throws<InvalidOperationException>(() => remote.Resume());
        Equal((byte)0, f.Data.Unit(0).MovesUsed); f.Random.RequireChoicesConsumed();
    }

    private static void TheftSessionSelectionAndIncidentPrivacy()
    {
        using var runtime = new SessionRuntime(new(), new(194));
        var f = new TheftFixture([TheftStart(), EscapeChoice("theft/capture", 3, 1), EscapeChoice("theft/escape", 2, 1, true),
            IncidentChoice(0, call: "theft/escape/incident")], CreationFixture(new(runtime.Process, runtime.World)));
        f.Known(90, 91); f.World.Globals.HumanPlayers = CivilizationSet.Only(1).With(2); f.World.Civilizations[2].Treaties[1].Peace = true;
        ParticipantId actorSeat = default, victimSeat = default;
        runtime.RegisterCommand(new(new(1, "test.theft", "Authored theft integration", AccessClass.Player, new([]), new([new("fallback")])),
            (_, _, _) => new(), _ => new CityTechnologyTheftOperation(f.Begin(), c => c == 1 ? actorSeat : victimSeat)));
        runtime.BeginClientSession(new()); var actor = SessionClient(runtime); var victim = SessionClient(runtime, 2);
        actorSeat = actor.Identity.Participant; victimSeat = victim.Identity.Participant;
        var command = actor.Submit(new("test.theft", new([]))).Correlation.Command; runtime.AdvanceOne();
        var specific = RequiredInteraction(actor, command); Equal(true, actor.Reply(specific, 1).Accepted); runtime.AdvanceOne();
        var choose = RequiredInteraction(actor, command); Equal(false, actor.Reply(choose, 89).Accepted); Equal(false, victim.Reply(choose, 91).Accepted);
        Equal(false, actor.Cancel(command).Accepted); f.Rules.Advances[90] = f.Rules.Advances[90] with { Enabled = true };
        f.GoalReplies.Enqueue(new(-1)); Equal(true, actor.Reply(choose, 100).Accepted); runtime.AdvanceOne();
        choose = RequiredInteraction(actor, command); Equal(1, f.GoalNotices.Count); Equal(true, actor.Reply(choose, 91).Accepted); runtime.AdvanceOne();
        var incident = RequiredInteraction(victim, command); Equal(false, actor.Reply(incident, 1).Accepted);
        Equal(true, f.World.Civilizations[1].Advances.Contains(91)); Equal(false, f.City.Flags.TechnologyStolen);
        Equal(true, victim.Reply(incident, 0).Accepted); runtime.AdvanceOne();
        Equal(CommandStatus.Completed, RequiredResult(actor, command).Status); Equal(true, f.City.Flags.TechnologyStolen); f.Random.RequireChoicesConsumed();
    }
}
