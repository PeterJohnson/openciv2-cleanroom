using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static Relationship ProtectedAttack => new() { Ceasefire = true, Peace = true, Alliance = true };
    private sealed class AttackFixture
    {
        public RevelationFixture Data { get; } = new();
        public GameState World => Data.World;
        public List<AttackNotice> Notices { get; } = [];
        public AttackConfirmation Confirmation { get; }
        public bool FailNotice;
        public AttackFixture()
        {
            World.Globals.CivsAlive = CivilizationSet.Only(1).With(2);
            World.Civilizations[1].Government = Government.Despotism;
            World.Civilizations[1].Treaties[2].Peace = true;
            World.Names[1] = new() { Tribe = "Alders", Adjective = "Alder" };
            World.Names[2] = new() { Tribe = "Birches", Adjective = "Birch" };
            Confirmation = new(World, Data.Rules, n => { Notices.Add(n); if (FailNotice) throw new InvalidOperationException("Notice unavailable"); });
        }
        public AttackConfirmation.Operation Begin(Relationship? flags = null) => Confirmation.Begin(1, 2, flags ?? ProtectedAttack);
        public void UnitedNations(int owner)
        { Data.AddCity(0, owner, new(10, 10)); World.Globals.WonderCity[WonderIds.UnitedNations] = 0; }
    }

    // V04 §4: retained diplomacy random, unsigned reputation, strict threshold and directed sneak attack.
    private static void SenateRetainedThresholds()
    {
        foreach (var (government, active, random, reputation, sneak, wonder, blocked) in new[]
        {
            (Government.Monarchy, true, 99, 0, false, false, false),
            (Government.Republic, false, 99, 0, false, false, false),
            (Government.Republic, true, 50, 0, false, false, true),
            (Government.Republic, true, 50, 4, false, false, false),
            (Government.Democracy, false, 0, 0, false, false, true),
            (Government.Democracy, false, 24, 0, true, false, false),
            (Government.Democracy, false, 25, 0, true, false, true),
            (Government.Democracy, false, 49, 0, true, true, false),
            (Government.Democracy, false, 50, 0, true, true, true),
            (Government.Democracy, false, 74, 255, false, false, false),
            (Government.Democracy, false, 75, 255, false, false, true),
            ((Government)7, false, 99, 0, false, false, true)
        })
        {
            var f = new AttackFixture(); var w = f.World; w.Civilizations[1].Government = government;
            w.Civilizations[1].Flags.SenateActive = active; w.Civilizations[1].DiploRandom = (byte)random;
            w.Civilizations[2].Reputation = (byte)reputation; w.Civilizations[2].Treaties[1].SneakAttack = sneak;
            if (wonder) f.UnitedNations(1);
            Equal(blocked, f.Confirmation.SenateBlocks(1, 2)); Equal((byte)random, w.Civilizations[1].DiploRandom);
            Equal(0, f.Notices.Count);
        }
        foreach (bool scenario in new[] { false, true })
            foreach (bool disabled in new[] { false, true })
            {
                var f = new AttackFixture(); f.World.Civilizations[1].Government = Government.Democracy;
                f.World.Globals.Status.Scenario = scenario; f.World.Scenario.Flags.RevolutionRestriction = disabled;
                Equal(!(scenario && disabled), f.Confirmation.SenateBlocks(1, 2));
            }
        var direction = new AttackFixture(); direction.World.Civilizations[1].Government = Government.Democracy;
        direction.World.Civilizations[1].DiploRandom = 24; direction.World.Civilizations[1].Treaties[2].SneakAttack = true;
        direction.UnitedNations(2); Equal(true, direction.Confirmation.SenateBlocks(1, 2));
        direction.UnitedNations(1); direction.Data.Rules.WonderObsolescence[WonderIds.UnitedNations] = (AdvanceReference)0;
        direction.World.Civilizations[3].Advances = default(AdvanceSet).With(0);
        Equal(true, direction.Confirmation.SenateBlocks(1, 2));
    }

    private static void AttackAdmissionAndWarningPriority()
    {
        foreach (int variant in Enumerable.Range(0, 4))
        {
            var f = new AttackFixture();
            if (variant == 0) f.World.Civilizations[1].Treaties[2].War = true;
            if (variant == 1) f.World.Civilizations[1].Treaties[2].Peace = false;
            if (variant == 2) f.World.Globals.CivsAlive = CivilizationSet.Only(1);
            using var op = f.Begin(variant == 3 ? new() { Ceasefire = true } : null);
            Equal(null, op.Resume()); Equal(true, op.Completed); Equal(false, op.Blocked); Equal(0, f.Notices.Count);
        }
        var allied = new AttackFixture(); allied.World.Civilizations[1].Treaties[2].Alliance = true;
        using var alliance = allied.Begin(new() { Peace = true }); Equal(null, alliance.Resume()); Equal(true, alliance.Blocked);
        Equal(AttackNoticeKind.Allied, allied.Notices.Single().Kind); Equal(true, allied.World.Civilizations[1].Treaties[2].Alliance);
        foreach (var (peace, ceasefire, tribute, expected) in new[]
        {
            (true, true, true, AttackWarningKind.Peace), (false, true, true, AttackWarningKind.TributeCeasefire),
            (false, true, false, AttackWarningKind.Ceasefire), (false, false, true, AttackWarningKind.Annoy)
        })
        {
            var f = new AttackFixture(); f.World.Civilizations[1].Treaties[2] = new() { Contact = true, Peace = peace, Ceasefire = ceasefire, Tribute = tribute };
            using var op = f.Begin(new() { Contact = true }); var warning = op.Resume()!;
            Equal(expected, warning.Kind); Equal("Birches", warning.Victim.Plural); Equal(warning, op.Resume()); Equal(false, op.Completed);
        }
    }

    private static void AttackRepliesAndLiveSenate()
    {
        foreach (int reply in new[] { -1, 0, 1, 2 })
        {
            var f = new AttackFixture(); var before = f.World.Civilizations[1].Treaties[2]; using var op = f.Begin(); op.Resume(); op.Resume(reply);
            Equal(true, op.Completed); Equal(reply != 1, op.Blocked); Equal(before, f.World.Civilizations[1].Treaties[2]);
            Equal((byte)0, f.World.Civilizations[1].Reputation); Equal(0, f.Notices.Count);
        }
        var changed = new AttackFixture(); using var pending = changed.Begin(); pending.Resume();
        changed.World.Civilizations[1].Treaties[2] = new() { War = true };
        changed.World.Globals.CivsAlive = default; changed.World.Civilizations[1].Government = Government.Democracy;
        pending.Resume(1); Equal(true, pending.Blocked); Equal(AttackNoticeKind.Overruled, changed.Notices.Single().Kind);
        Equal(Government.Democracy, changed.Notices.Single().Government); // Entry gates are not repeated after the warning.
        foreach (var (reputation, sneak, un, expected) in new[]
        {
            (0, false, false, AttackNoticeKind.Hawks), (1, false, false, AttackNoticeKind.Aggressor),
            (0, true, false, AttackNoticeKind.Aggressor), (0, true, true, AttackNoticeKind.UnitedNations)
        })
        {
            var f = new AttackFixture(); f.World.Civilizations[1].Government = Government.Republic;
            f.World.Civilizations[2].Reputation = (byte)reputation; f.World.Civilizations[2].Treaties[1].SneakAttack = sneak;
            if (un) f.UnitedNations(2);
            using var op = f.Begin(); op.Resume(); f.World.Names[2] = new() { Adjective = "Live adjective" }; op.Resume(1);
            Equal(false, op.Blocked); Equal(expected, f.Notices.Single().Kind); Equal("Live adjective", f.Notices.Single().Victim.Adjective);
            Equal(true, f.World.Civilizations[1].Treaties[2].Peace); Equal(false, f.World.Civilizations[1].Treaties[2].War);
        }
    }

    private static void AttackSessionAndFailure()
    {
        foreach (var kind in new[] { OperationInputKind.Cancel, OperationInputKind.Timeout, OperationInputKind.Choice })
        {
            var f = new AttackFixture(); var random = WarRandom();
            var context = new OperationContext(f.World, f.Data.Process, random, new PresentationRandom(1, "attack"), new(default, new(3)), default, (_, _) => { }, () => { });
            using var session = new AttackConfirmationOperation(f.Begin(), c => new((ulong)c));
            var prompt = session.Resume(context, new()); Equal(CommandStatus.AwaitingInteraction, prompt.Status);
            Equal(new ParticipantId(1), prompt.Interaction!.Recipient); Sequence(new[] { 0, 1 }, prompt.Interaction.Choices);
            var result = session.Resume(context, new(kind, 1)); Equal(CommandStatus.Completed, result.Status);
            Equal(new CommandValue.Boolean(kind != OperationInputKind.Choice), result.Result!.Single().Value); random.RequireChoicesConsumed();
        }
        foreach (bool allied in new[] { false, true })
        {
            var f = new AttackFixture { FailNotice = true }; f.World.Civilizations[1].Treaties[2].Alliance = allied;
            f.World.Civilizations[1].Government = Government.Democracy; using var op = f.Begin();
            if (!allied) op.Resume();
            Throws<InvalidOperationException>(() => op.Resume(1)); Throws<InvalidOperationException>(() => op.Resume(1));
            Equal(true, f.World.Civilizations[1].Treaties[2].Peace); Equal(1, f.Notices.Count);
        }
        var disposed = new AttackFixture().Begin(); disposed.Resume(); disposed.Dispose(); Throws<ObjectDisposedException>(() => disposed.Resume(1));
        var remote = new AttackFixture(); remote.World.Globals.Multiplayer = GameMode.Tcp;
        using var rejected = remote.Begin(); Throws<InvalidOperationException>(() => rejected.Resume()); Equal(0, remote.Notices.Count);
    }
}
