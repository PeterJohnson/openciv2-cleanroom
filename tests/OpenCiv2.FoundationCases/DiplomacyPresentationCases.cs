using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // V05 §§4–6/W03 §7: actual retained gifts, map exchange and treaty negotiation.
    private static void DiplomacyPresentationConversation()
    {
        foreach (int route in new[] { 0, 1, 2 })
        {
            using var runtime = new SessionRuntime(new(), new(307));
            OperationContext? current = null;
            var presentation = new SinglePlayerDiplomacyPresentation(
                () => current ?? throw new InvalidOperationException("No active session step"), () => 3);
            var f = new SinglePlayerDiplomacyFixture(data: new(runtime.Process, runtime.World), presentation: presentation.Effects);
            f.World.Names[2].Tribe = "River Folk";
            f.World.Civilizations[1].Gold = 150;
            if (route == 1)
            {
                f.Events.Contact.F.T.Know(1, AdvanceIds.MapMaking); f.Events.Contact.F.T.Know(2, AdvanceIds.MapMaking);
                f.World.Diplomacy.Attitude = 0;
                f.World.Map.TileAt(new(2, 2)).RevealedTo = CivilizationSet.Only(2);
            }
            DiplomaticConversationOperation? child = null;
            runtime.RegisterCommand(new(new(1, "test.diplomacy", "Diplomatic conversation", AccessClass.Player, new([]), new([])),
                (_, _, _) => new(), _ => new TestContinuation((context, input) =>
                {
                    current = context;
                    try
                    {
                        child ??= new(f.BeginConversation(), c => new((ulong)c),
                            _ => throw new InvalidOperationException("No intelligence in these conversations"));
                        return child.Resume(context, input);
                    }
                    finally { current = null; }
                }, () => child?.Dispose())));
            runtime.BeginClientSession(new()); var receiver = SessionClient(runtime); var initiator = SessionClient(runtime, 2);
            var viewer = SessionClient(runtime, 3);
            var command = initiator.Submit(new("test.diplomacy", new([]))).Correlation.Command; runtime.AdvanceOne();
            foreach (int reply in route switch { 0 => new[] { 8, 2, 150, 0 }, 1 => [7, 3, 0], _ => [1, 0] })
            {
                Equal(null, RequiredResult(initiator, command).Interaction);
                var prompt = RequiredInteraction(receiver, command);
                var before = receiver.ObserveNotices(); Equal(false, runtime.AdvanceOne()); Equal(before, receiver.ObserveNotices());
                Equal(true, receiver.Reply(prompt, reply).Accepted); runtime.AdvanceOne();
            }
            Equal(CommandStatus.Completed, RequiredResult(initiator, command).Status);
            string expected = route switch { 0 => "accept your gift", 1 => "agree to exchange maps", _ => "have agreed to an alliance" };
            Equal(true, receiver.ObserveNotices()!.Items.Any(n => n.Text.Contains("River Folk", StringComparison.Ordinal)
                && n.Text.Contains(expected, StringComparison.Ordinal)));
            Equal(0, initiator.ObserveNotices()!.Items.Count); Equal(0, viewer.ObserveNotices()!.Items.Count);
            if (route == 0) { Equal(0, f.World.Civilizations[1].Gold); Equal(927, f.World.Civilizations[2].Gold); }
            if (route == 1) Equal(true, f.World.Map.TileAt(new(2, 2)).RevealedTo.Contains(1));
            if (route == 2) Equal(true, f.World.Civilizations[1].Treaties[2].Alliance && f.World.Civilizations[2].Treaties[1].Alliance);
            Equal(false, f.World.Diplomacy.MeetingActive || f.World.Diplomacy.AiMeetingActive);
            Throws<InvalidOperationException>(() => presentation.RefreshStatus()); f.Events.Consumed();
        }
    }

    // V04 §§4/6: notices keep attack permission distinct from war, and preserve Core disclosure.
    private static void DiplomacyPresentationAttackAndObserver()
    {
        foreach (var kind in Enum.GetValues<AttackNoticeKind>())
        {
            using var runtime = new SessionRuntime(new(), new(308));
            var f = new RevelationFixture(runtime.Process, runtime.World); var w = f.World;
            w.Globals.CivsAlive = CivilizationSet.Only(1).With(2);
            w.Names[2].Tribe = "River Folk"; w.Civilizations[1].Treaties[2].Peace = true;
            w.Civilizations[1].Government = kind == AttackNoticeKind.Overruled ? Government.Democracy : Government.Republic;
            f.Rules.Governments[(int)Government.Democracy] = f.Rules.Governments[(int)Government.Democracy] with { Identity = new("assembly", "Citizen Assembly") };
            if (kind == AttackNoticeKind.Allied) w.Civilizations[1].Treaties[2].Alliance = true;
            if (kind == AttackNoticeKind.Aggressor) w.Civilizations[2].Treaties[1].SneakAttack = true;
            if (kind == AttackNoticeKind.UnitedNations)
            {
                f.AddCity(0, 2, new(10, 10)); w.Globals.WonderCity[WonderIds.UnitedNations] = 0;
                int building = 39 + WonderIds.UnitedNations;
                f.Rules.Improvements[building] = f.Rules.Improvements[building] with { Identity = new("league", "World Assembly") };
            }
            runtime.RegisterCommand(CommandDefinitionFor(_ => new TestContinuation((context, _) =>
            {
                var presentation = new SinglePlayerDiplomacyPresentation(() => context, () => 3);
                var confirmation = new AttackConfirmation(w, f.Rules, presentation.Notice);
                var operation = confirmation.Begin(1, 2, ProtectedAttack);
                if (kind == AttackNoticeKind.Allied) Equal(null, operation.Resume());
                else
                {
                    var pending = operation.Resume(); Equal(true, pending is not null); Equal(pending, operation.Resume());
                    Equal(null, operation.Resume(1));
                }
                Equal(true, operation.Completed); Equal(kind is AttackNoticeKind.Allied or AttackNoticeKind.Overruled, operation.Blocked);
                return CommandDone(1);
            })));
            runtime.BeginClientSession(new()); var receiver = SessionClient(runtime); var initiator = SessionClient(runtime, 2);
            var command = initiator.Submit(CommandRequestFor()).Correlation.Command; runtime.AdvanceOne();
            Equal(CommandStatus.Completed, RequiredResult(initiator, command).Status);
            string text = receiver.ObserveNotices()!.Items.Single().Text;
            Equal(true, text.Contains("River Folk", StringComparison.Ordinal));
            Equal(false, text.Contains("declared war", StringComparison.Ordinal));
            if (kind == AttackNoticeKind.UnitedNations) Equal(true, text.Contains("World Assembly", StringComparison.Ordinal));
            if (kind == AttackNoticeKind.Overruled) Equal(true, text.Contains("Citizen Assembly", StringComparison.Ordinal));
            Equal(false, w.Civilizations[1].Treaties[2].War); Equal(true, w.Civilizations[1].Treaties[2].Peace);
            Equal(0, initiator.ObserveNotices()!.Items.Count); Equal(0UL, runtime.GameplayRandom.Draws);
        }
        foreach (var (local, observed) in new[] { (1, false), (3, false), (3, true) })
        {
            using var runtime = new SessionRuntime(new(), new(309));
            var f = new RevelationFixture(runtime.Process, runtime.World); var w = f.World;
            w.Multiplayer.LocalHuman = local;
            w.Names[1].Tribe = "Hill Folk"; w.Names[2].Tribe = "River Folk";
            DiplomaticRelations.Set(w, 1, 2, new() { Alliance = true });
            w.Civilizations[local].Treaties[1].Embassy = observed;
            runtime.RegisterCommand(CommandDefinitionFor(_ => new TestContinuation((context, _) =>
            {
                var presentation = new SinglePlayerDiplomacyPresentation(() => context, () => w.Multiplayer.LocalHuman);
                var war = new DiplomaticWar(w, f.Rules, f.Stacks, new(w, f.Rules, f.Stacks), context.Gameplay, presentation);
                war.CancelAlliance(1, 2); return CommandDone(1);
            })));
            runtime.BeginClientSession(new()); var observer = SessionClient(runtime, (byte)local); var initiator = SessionClient(runtime, 2);
            var command = initiator.Submit(CommandRequestFor()).Correlation.Command; runtime.AdvanceOne();
            Equal(CommandStatus.Completed, RequiredResult(initiator, command).Status);
            Equal(local == 1 || observed ? 1 : 0, observer.ObserveNotices()!.Items.Count);
            Equal(0, initiator.ObserveNotices()!.Items.Count);
            Equal(false, w.Civilizations[1].Treaties[2].Alliance || w.Civilizations[2].Treaties[1].Alliance);
            Equal(true, w.Civilizations[1].Treaties[2].Peace && w.Civilizations[2].Treaties[1].Peace);
        }
    }

    private static void DiplomacyPresentationPayloadsAndRecipients()
    {
        using var runtime = new SessionRuntime(new(), new(310));
        runtime.RegisterCommand(CommandDefinitionFor(_ => new TestContinuation((context, _) =>
        {
            var presentation = new SinglePlayerDiplomacyPresentation(() => context, () => 3);
            var first = new DiplomaticParty(1, "Hill Folk", "Hill");
            var second = new DiplomaticParty(2, "River Folk", "River");
            var target = new DiplomaticParty(4, "Coast Folk", "Coastal");
            presentation.Notice(new DiplomaticDemandNotice(DiplomaticDemandNoticeKind.GiveGold, 2, first, 3, 2, Gold: -7));
            presentation.Notice(new TechnologyNegotiationNotice(TechnologyNegotiationNoticeKind.Gift, 2, first, 0, 3,
                new(7, "Star Charts")));
            presentation.Notice(new TechnologyNegotiationNotice(TechnologyNegotiationNoticeKind.WonderBlocked, 2, first, 0, 3,
                Wonder: "Sky Archive"));
            presentation.Notice(new DiplomaticFavorNotice(DiplomaticFavorNoticeKind.NoContact, 2, first, target, 3));
            presentation.Notice(new TakenTechnologyNotice(1, 2, 7, "Hill Folk", "Star Charts"));
            presentation.Notice(new ContactNotice(ContactNoticeKind.SignNato, first, second, target, new(0), "Harbor"));
            return CommandDone(1);
        })));
        runtime.BeginClientSession(new()); var initiator = SessionClient(runtime); var recipient = SessionClient(runtime, 2);
        var observer = SessionClient(runtime, 3);
        var command = initiator.Submit(CommandRequestFor()).Correlation.Command; runtime.AdvanceOne();
        Equal(CommandStatus.Completed, RequiredResult(initiator, command).Status);
        Equal(0, initiator.ObserveNotices()!.Items.Count); Equal(0, RequiredResult(initiator, command).Committed.Count);
        var messages = recipient.ObserveNotices()!.Items.Select(n => n.Text).ToArray();
        Equal(5, messages.Length); Equal(true, messages[0].Contains("-7 gold", StringComparison.Ordinal));
        Equal(true, messages[1].Contains("Star Charts", StringComparison.Ordinal));
        Equal(true, messages[2].Contains("Sky Archive", StringComparison.Ordinal));
        Equal(true, messages[3].Contains("Coast Folk", StringComparison.Ordinal));
        Equal(true, messages[4].Contains("taken your knowledge of Star Charts", StringComparison.Ordinal));
        string announcement = observer.ObserveNotices()!.Items.Single().Text;
        foreach (string name in new[] { "Hill Folk", "River Folk", "Coast Folk", "Harbor" })
            Equal(true, announcement.Contains(name, StringComparison.Ordinal));
        Equal(0UL, runtime.GameplayRandom.Draws);
        Equal(0, runtime.World.Civilizations[1].Gold); Equal(false, runtime.World.Civilizations[1].Advances.Contains(7));
        Equal(false, runtime.World.Civilizations[1].Treaties[2].Alliance);
    }
}
