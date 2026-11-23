using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private sealed class CombatEntryFixture
    {
        public CombatFixture Combat { get; } = new();
        public RevelationFixture Data => Combat.Data;
        public GameState World => Data.World;
        public CombatWorkspace State => Combat.State;
        public GameplayRandom Random { get; }
        public CombatEntry Entry { get; }
        public List<AttackNotice> AttackNotices { get; } = [];
        public List<CombatTreatyNotice> TreatyNotices { get; } = [];
        public List<GovernmentChangeNotice> Governments { get; } = [];
        public WarNotices WarEffects { get; } = new();
        public bool FailNotice;
        public CombatEntryFixture(params Choice[] choices)
        {
            Random = new(new ControlledChoices(new(choices)), "entry/test");
            World.Globals.CivsAlive = CivilizationSet.Only(1).With(2); World.Globals.PeaceTurns = 37; World.Globals.Turn = 42;
            World.Civilizations[1].Government = Government.Despotism;
            World.Names[1] = new() { Tribe = "Alderfolk", Adjective = "Alder" };
            World.Names[2] = new() { Tribe = "Birchfolk", Adjective = "Birch" };
            var changes = new GovernmentChanges(World, Data.Process.Rules, Data.Stacks,
                _ => throw new InvalidOperationException("Unexpected player government choice"),
                _ => throw new InvalidOperationException("Unexpected player rate choice"), Governments.Add);
            Entry = new(World, Data.Rules, State, Random, new(World, Data.Rules, AttackNotices.Add), War(Data, WarEffects, Random),
                new(World, Data.Process.Rules, changes, Random), n => { TreatyNotices.Add(n); if (FailNotice) throw new InvalidOperationException("Notice unavailable"); });
        }
        public PreparedCombat Prepare(int? attack = null)
        { var result = Combat.Prepare(true); return attack is { } value ? result with { Attack = value } : result; }
        public CombatEntry.Operation Begin(bool execute = true, int? attack = null) => Entry.Begin(Prepare(attack), new(false, execute), "entry");
        public void Complete(CombatEntry.Operation op) { Equal(null, op.Resume()); Equal(true, op.Completed); Random.RequireChoicesConsumed(); }
    }
    private static Choice SneakChoice(int value) => Choice.Create(new("U06.sneak-government", "entry"), new SamplingLaw.PrimaryModulo(100), ChoiceCode.Primary(value));
    private static Choice CombatGovernmentChoice(int value) => Choice.Create(new("A08.government-limit", "entry/government"), new SamplingLaw.PrimaryModulo(3), ChoiceCode.Primary(value));

    // U06 preparation clears per-call message/explosion inputs, retaining active-loss and array entry zero.
    private static void CombatPreparationMessageReset()
    {
        var f = new CombatEntryFixture(); f.State.AggregateTreatyNotice = true; f.State.AggregateAllyWarning = 2; f.State.ExplosionKind = 46;
        f.State.ActiveUnitLost = true; f.State.TreatyNotices[0] = true; f.State.AllyWarnings[0] = 2;
        f.Prepare(); Equal(false, f.State.AggregateTreatyNotice); Equal(0, f.State.AggregateAllyWarning); Equal(0, f.State.ExplosionKind);
        Equal(true, f.State.ActiveUnitLost); Equal(true, f.State.TreatyNotices[0]); Equal(2, f.State.AllyWarnings[0]);
    }

    private static void CombatEntryChargeAndConfirmation()
    {
        foreach (bool execute in new[] { false, true })
            foreach (int reply in new[] { 0, 1 })
            {
                var f = new CombatEntryFixture(); f.Data.Unit(3).MovesUsed = 5; f.World.Civilizations[1].Treaties[2].Peace = true;
                using var op = f.Begin(execute); var pending = op.Resume()!;
                Equal(AttackWarningKind.Peace, pending.Kind); Equal(pending, op.Resume()); Equal(false, op.Completed);
                Equal((byte)(execute ? 8 : 5), f.Data.Unit(3).MovesUsed); Equal((short)37, f.World.Globals.PeaceTurns);
                Equal(null, op.Resume(reply)); Equal(true, op.Completed); Equal(reply != 1, op.Result!.Cancelled);
                Equal(26, op.Result.Combat.Attack); Equal(reply == 1, f.World.Civilizations[1].Treaties[2].Fighting);
                Equal(reply == 1 ? (short)0 : (short)37, f.World.Globals.PeaceTurns);
                Equal((byte)(execute ? 8 : 5), f.Data.Unit(3).MovesUsed); f.Random.RequireChoicesConsumed();
                op.Resume(1); Equal((byte)(execute ? 8 : 5), f.Data.Unit(3).MovesUsed);
            }
        var wrap = new CombatEntryFixture(); var prepared = wrap.Prepare(); wrap.Data.Unit(3).MovesUsed = 254;
        wrap.Data.Rules.Cosmic = wrap.Data.Rules.Cosmic with { RoadMovementMultiplier = 5 };
        using var charge = wrap.Entry.Begin(prepared, new(false, true), "entry"); wrap.Complete(charge);
        Equal((byte)3, wrap.Data.Unit(3).MovesUsed); Equal(80, charge.Result!.Combat.Attack);
        var ally = new CombatEntryFixture(); ally.World.Civilizations[1].Treaties[2].Alliance = true;
        using var refusal = ally.Begin(); ally.Complete(refusal); Equal(true, refusal.Result!.Cancelled); Equal(AttackNoticeKind.Allied, ally.AttackNotices.Single().Kind);
        var changed = new CombatEntryFixture(); changed.World.Civilizations[1].Treaties[2].Peace = true;
        using var later = changed.Begin(); Equal(true, later.Resume() is not null);
        changed.World.Civilizations[1].Treaties[2].Alliance = true; later.Resume(1);
        Equal(true, later.Result!.Cancelled); Equal((byte)3, changed.Data.Unit(3).MovesUsed); Equal((short)37, changed.World.Globals.PeaceTurns);
    }

    private static void CombatSneakBranchesAndNotices()
    {
        foreach (int variant in Enumerable.Range(0, 7))
        {
            var f = new CombatEntryFixture(); f.World.Globals.HumanPlayers = CivilizationSet.Only(2);
            ref var reverse = ref f.World.Civilizations[2].Treaties[1];
            if (variant == 0) reverse.Ceasefire = true;
            if (variant is 1 or 6) reverse.Peace = true;
            if (variant is 2 or 4) reverse.Contact = true;
            if (variant == 3) f.World.Civilizations[1].Treaties[2].BrokenTreaty = true;
            if (variant is 4 or 6) reverse.War = true;
            if (variant == 5) f.World.Civilizations[1].Treaties[2].Peace = true;
            using var op = f.Begin(attack: 9); f.Complete(op);
            bool sneak = variant is not (4 or 5);
            Equal(sneak ? 18 : 9, op.Result!.Combat.Attack); Equal(sneak ? 1 : 0, f.TreatyNotices.Count);
            Equal(sneak ? (short)42 : (short)0, f.World.Civilizations[2].LastContactTurn[1]);
            if (sneak)
            {
                var n = f.TreatyNotices.Single(); Equal(2, n.Recipient); Equal(1, n.Attacker); Equal(new UnitTarget(new(3), f.Data.Unit(3).Token), n.Unit);
                Equal(variant == 0 ? CombatTreatyNoticeKind.BreakCeasefire : CombatTreatyNoticeKind.SneakAttack, n.Kind);
                Equal(variant == 0 ? "Alderfolk" : "Alder", n.AttackerName);
            }
            if (variant == 5) Equal(true, f.State.AggregateTreatyNotice);
        }
        var autoplay = new CombatEntryFixture(); autoplay.World.Globals.HumanPlayers = CivilizationSet.Only(2);
        autoplay.World.Multiplayer.Saved.AutoplayActive = true; autoplay.World.Civilizations[2].Treaties[1].Peace = true;
        using var silent = autoplay.Begin(); autoplay.Complete(silent); Equal(0, autoplay.TreatyNotices.Count); Equal(160, silent.Result!.Combat.Attack);
    }

    private static void CombatSneakGovernmentPolicy()
    {
        foreach (var (regime, roll, reputation, un, changes) in new[]
        {
            (Government.Republic, 49, 0, false, false), (Government.Republic, 50, 0, false, true),
            (Government.Republic, 99, 5, false, false), (Government.Democracy, 49, 0, true, false),
            (Government.Democracy, 49, 0, false, true), (Government.Democracy, 99, 10, false, false)
        })
        {
            var f = new CombatEntryFixture(changes ? [SneakChoice(roll), CombatGovernmentChoice(2)] : [SneakChoice(roll)]);
            f.World.Globals.HumanPlayers = CivilizationSet.Only(2); f.World.Civilizations[1].Government = regime;
            f.World.Civilizations[2].Reputation = (byte)reputation; f.World.Civilizations[2].Treaties[1].Peace = true;
            if (un) { f.Data.AddCity(0, 1, new(6, 10)); f.World.Globals.WonderCity[WonderIds.UnitedNations] = 0; }
            using var op = f.Begin(); f.Complete(op); Equal(changes ? Government.Despotism : regime, f.World.Civilizations[1].Government);
            Equal(changes, f.World.Civilizations[1].Flags.GovernmentChanged);
            Equal(changes ? 2 : 1, f.Random.Capture().Trace!.Events.Count);
            if (changes) Equal("A08.government-limit", f.Random.Capture().Trace!.Events[1].Key.Site);
        }
        var human = new CombatEntryFixture(SneakChoice(99)); human.World.Globals.HumanPlayers = CivilizationSet.Only(1).With(2);
        human.World.Civilizations[1].Government = Government.Republic; human.World.Civilizations[2].Treaties[1].Peace = true;
        using var unused = human.Begin(); human.Complete(unused); Equal(Government.Republic, human.World.Civilizations[1].Government);
        var locked = new CombatEntryFixture(SneakChoice(99)); locked.World.Globals.HumanPlayers = CivilizationSet.Only(2);
        locked.World.Civilizations[1].Government = Government.Republic; locked.World.Civilizations[2].Treaties[1].Peace = true;
        locked.World.Globals.Status.Scenario = true; locked.World.Scenario.Flags.GovernmentLock = true;
        using var restricted = locked.Begin(); locked.Complete(restricted); Equal(Government.Republic, locked.World.Civilizations[1].Government);
    }

    private static void CombatEntryDifficultyAndForceWar()
    {
        foreach (bool bothHuman in new[] { false, true })
            foreach (Difficulty difficulty in new[] { Difficulty.Chieftain, Difficulty.Warlord, Difficulty.Prince })
            {
                var f = new CombatEntryFixture(); f.World.Globals.HumanPlayers = CivilizationSet.Only(2).With(1, bothHuman);
                f.World.Globals.Difficulty = difficulty; f.World.Civilizations[2].Treaties[1].War = true;
                using var op = f.Begin(attack: -3); f.Complete(op);
                int expected = difficulty < Difficulty.Prince ? -2 : -3;
                if (bothHuman && difficulty == Difficulty.Chieftain) expected *= 2;
                Equal(expected, op.Result!.Combat.Attack);
            }
        var barb = new CombatEntryFixture(); barb.Data.Unit(3).Owner = new(0); barb.World.Globals.HumanPlayers = CivilizationSet.Only(2);
        barb.World.Globals.Difficulty = Difficulty.Chieftain; using var unchanged = barb.Begin(attack: -3); barb.Complete(unchanged);
        Equal(-3, unchanged.Result!.Combat.Attack); Equal((short)37, barb.World.Globals.PeaceTurns); Equal(true, barb.World.Civilizations[0].Treaties[2].Fighting);
        var force = new CombatEntryFixture(); force.World.Globals.HumanPlayers = CivilizationSet.Only(2);
        force.World.Civilizations[1].Treaties[2].Contact = true; force.World.Civilizations[2].Treaties[1].War = true;
        using var war = force.Begin(); force.Complete(war); Equal(true, war.Result!.ForceWar);
        Equal(true, force.World.Civilizations[1].Treaties[2].War && force.World.Civilizations[1].Treaties[2].SneakAttack);
        Equal((byte)100, force.World.Civilizations[1].Attitude[2]); Equal((byte)100, force.World.Civilizations[2].Attitude[1]);
    }

    private static void CombatEntryAllyAndTreatyNotices()
    {
        foreach (int variant in Enumerable.Range(0, 5))
        {
            var f = new CombatEntryFixture(); f.World.Globals.HumanPlayers = CivilizationSet.Only(3); f.World.Multiplayer.LocalHuman = 3;
            f.World.Civilizations[2].Treaties[1].Peace = true;
            f.World.Civilizations[3].Treaties[2].Alliance = variant is 0 or 2;
            f.World.Civilizations[3].Treaties[1].Alliance = variant is 1 or 2;
            f.World.Globals.RevealMap = (byte)(variant == 3 ? 1 : 0);
            using var op = f.Begin(); f.Complete(op); Equal(variant is 0 or 2 ? 1 : variant == 1 ? 2 : 0, f.State.AggregateAllyWarning);
            Equal(variant == 3, f.State.AggregateTreatyNotice); Equal(false, op.Result!.Cancelled);
        }
        var aiAlly = new CombatEntryFixture(); aiAlly.World.Globals.HumanPlayers = CivilizationSet.Only(3);
        aiAlly.World.Civilizations[1].Treaties[2].Alliance = true;
        using var allowed = aiAlly.Begin(); aiAlly.Complete(allowed); Equal(false, allowed.Result!.Cancelled);
        Equal(false, aiAlly.World.Civilizations[1].Treaties[2].Alliance);
    }

    private static void CombatEntryFailurePrefixes()
    {
        var missing = new CombatEntryFixture(SneakChoice(99)); missing.World.Globals.HumanPlayers = CivilizationSet.Only(2);
        missing.World.Civilizations[1].Government = Government.Republic; missing.World.Civilizations[2].Treaties[1].Peace = true;
        using var choice = missing.Begin(); Throws<ChoiceException>(() => choice.Resume()); Equal((byte)3, missing.Data.Unit(3).MovesUsed);
        Equal((short)0, missing.World.Globals.PeaceTurns); Equal(true, missing.World.Civilizations[1].Treaties[2].Fighting);
        Equal(true, missing.World.Civilizations[2].Treaties[1].Peace); Equal(0, missing.TreatyNotices.Count);
        Throws<InvalidOperationException>(() => choice.Resume()); Equal(1, missing.Random.Capture().Trace!.Events.Count);
        var notice = new CombatEntryFixture { FailNotice = true }; notice.World.Globals.HumanPlayers = CivilizationSet.Only(2);
        notice.World.Civilizations[2].Treaties[1].Peace = true; using var failed = notice.Begin(); Throws<InvalidOperationException>(() => failed.Resume());
        Equal((short)0, notice.World.Civilizations[2].LastContactTurn[1]); Equal(true, notice.World.Civilizations[2].Treaties[1].Peace);
        var observer = new CombatEntryFixture(); observer.World.Globals.HumanPlayers = CivilizationSet.Only(3); observer.World.Multiplayer.LocalHuman = -1;
        observer.World.Civilizations[2].Treaties[1].Peace = true; using var invalid = observer.Begin(); Throws<ArgumentOutOfRangeException>(() => invalid.Resume());
        Equal((byte)3, observer.Data.Unit(3).MovesUsed); Equal((short)0, observer.World.Globals.PeaceTurns); Equal(true, observer.World.Civilizations[2].Treaties[1].Peace);
        var cancelled = new CombatEntryFixture(); cancelled.World.Civilizations[1].Treaties[2].Peace = true; var pending = cancelled.Begin(); pending.Resume(); pending.Dispose();
        Throws<ObjectDisposedException>(() => pending.Resume(1)); Equal((byte)3, cancelled.Data.Unit(3).MovesUsed); Equal((short)37, cancelled.World.Globals.PeaceTurns);
        var remote = new CombatEntryFixture(); var prepared = remote.Prepare(); remote.World.Globals.Multiplayer = GameMode.Tcp;
        using var denied = remote.Entry.Begin(prepared, new(false, true), "entry"); Throws<InvalidOperationException>(() => denied.Resume()); Equal((byte)0, remote.Data.Unit(3).MovesUsed);
    }

    private static void CombatEntryRoundsResolutionJoin()
    {
        var f = new ResolutionFixture([CombatChoice("U06.round.attack", 8, 7), CombatChoice("U06.round.defense", 4, 0)]);
        var changes = new GovernmentChanges(f.World, f.Data.Process.Rules, f.Data.Stacks, _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), _ => { });
        var entry = new CombatEntry(f.World, f.Data.Rules, f.State, f.Life.Random, new(f.World, f.Data.Rules, _ => { }),
            War(f.Data, new(), f.Life.Random), new(f.World, f.Data.Process.Rules, changes, f.Life.Random), _ => throw new InvalidOperationException());
        using var admission = entry.Begin(f.Prepare() with { AttackFirepower = 10 }, new(false, true), "entry");
        Equal(null, admission.Resume()); Equal(false, admission.Result!.Cancelled); Equal((byte)3, f.Data.Unit(3).MovesUsed);
        using var rounds = RoundService(f.Combat, f.Life.Random, new()).Begin(admission.Result.Combat, "fight"); Equal(10, rounds.Advance());
        using var resolution = f.Resolution.Begin(admission.Result.Combat, rounds.Result != 0, "resolution"); f.Complete(resolution);
        Equal(true, f.Data.Unit(3).Flags.Veteran); Equal(3, f.State.KillCount); Equal((byte)3, f.Data.Unit(3).MovesUsed);
        Equal(true, f.Data.Unit(3).Token.Occupied); Equal(false, f.Data.Unit(2).Token.Occupied);
    }
}
