using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Session;
using OpenCiv2.ViewModels;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // P04/P05/X01: presentation recipients are independent of command ownership.
    private static void PlayerNoticesPrivacyAndFailure()
    {
        using var runtime = new SessionRuntime(new(), new(301));
        runtime.RegisterCommand(CommandDefinitionFor(_ => new TestContinuation((context, _) =>
        {
            context.Notify!(new(2), "Foreign private report");
            context.Notify(new(1), "Own report");
            throw new InvalidOperationException("Failure after delivered notices");
        })));
        runtime.BeginClientSession(new()); var actor = SessionClient(runtime); var recipient = SessionClient(runtime, 2);
        var second = runtime.Bind(recipient.Identity.Participant, ControllerKind.Frontend);
        var empty = recipient.ObserveNotices()!;
        var command = actor.Submit(CommandRequestFor()).Correlation.Command; runtime.AdvanceOne();
        Equal(CommandStatus.Failed, RequiredResult(actor, command).Status); Equal(null, recipient.Result(command));
        Equal("Own report", actor.ObserveNotices()!.Items.Single().Text);
        var privateView = recipient.ObserveNotices()!;
        Equal("Foreign private report", privateView.Items.Single().Text); Equal(1UL, privateView.Items[0].Sequence);
        Equal(privateView, second.ObserveNotices()); Equal(0, empty.Items.Count);
        actor.Updates();
        Equal(true, runtime.RetireCommand(command)); Equal(privateView, recipient.ObserveNotices());
        var panel = new SessionPanel(); panel.Bind(actor); Equal("Own report", panel.View.Notices!.Items.Single().Text);
        panel.Bind(recipient); Equal(privateView, panel.View.Notices);
        runtime.Revoke(recipient.Identity.Controller); panel.Refresh(); Equal(null, panel.View.Notices);
        Throws<AccessException>(() => recipient.ObserveNotices()); Equal(privateView, second.ObserveNotices());
        runtime.ReassignParticipant(second.Identity.Participant, new(3));
        Throws<AccessException>(() => second.ObserveNotices());
        Equal(0, SessionClient(runtime, 3).ObserveNotices()!.Items.Count);
    }

    private static void PlayerNoticesBoundsAndSessionReset()
    {
        using var runtime = new SessionRuntime(new(), new(302));
        runtime.RegisterCommand(CommandDefinitionFor(_ => new TestContinuation((context, _) =>
        {
            for (int i = 0; i < 67; ++i) context.Notify!(new(1), "Notice " + i);
            Throws<ArgumentException>(() => context.Notify!(new(8), "Invalid recipient"));
            Throws<ArgumentException>(() => context.Notify!(new(1), " "));
            return CommandDone(1);
        })));
        runtime.BeginClientSession(new()); var client = SessionClient(runtime); var other = SessionClient(runtime, 2);
        client.Submit(CommandRequestFor()); runtime.AdvanceOne();
        var old = client.ObserveNotices()!; Equal(64, old.Items.Count); Equal(3UL, old.Omitted);
        Equal(4UL, old.Items[0].Sequence); Equal("Notice 66", old.Items[^1].Text);
        Equal(0, other.ObserveNotices()!.Items.Count); Equal(0UL, other.ObserveNotices()!.Omitted);
        runtime.BeginClientSession(new()); Throws<AccessException>(() => client.ObserveNotices());
        var fresh = SessionClient(runtime); Equal(0, fresh.ObserveNotices()!.Items.Count);
        fresh.Submit(CommandRequestFor()); runtime.AdvanceOne(); Equal(old.Items, fresh.ObserveNotices()!.Items);
        runtime.PrepareNewGame(); Throws<AccessException>(() => fresh.ObserveNotices());
        runtime.BeginClientSession(new()); Equal(0, SessionClient(runtime).ObserveNotices()!.Items.Count);
        Equal(64, old.Items.Count);
    }

    // Real V03 acquisition and government continuation use the same presentation as desktop science.
    private static void ResearchNoticesRetainedAcquisition()
    {
        using var runtime = new SessionRuntime(new(), new(303));
        var f = new RetainedGrantFixture(data: new RevelationFixture(runtime.Process, runtime.World));
        f.World.Multiplayer.LocalHuman = 1; f.World.Globals.HumanPlayers = CivilizationSet.Only(1);
        f.G.Rules.Advances[AdvanceIds.Monarchy] = f.G.Rules.Advances[AdvanceIds.Monarchy] with { Identity = new("custom", "Crowned Assembly") };
        OperationContext? current = null; IContinuation? child = null;
        OperationContext Borrow() => current ?? throw new InvalidOperationException("No active session step");
        var effects = new SinglePlayerResearchPresentation(Borrow, () => 1);
        runtime.RegisterCommand(new(new(1, "test.acquire", "Actual acquisition", AccessClass.Player, new([]), new([new("granted", ValueKind.Boolean)])),
            (_, _, _) => new(), _ => new TestContinuation((context, input) =>
            {
                current = context;
                try
                {
                    if (child is null)
                    {
                        var services = new SinglePlayerResearch(f.World, runtime.Process.Rules, context.Gameplay, f.G.Data.Stacks,
                            f.Setter, new(f.World, f.G.Rules, _ => { }, _ => { }), effects, effects, f);
                        child = new ResearchGrantOperation(services.Grants.Begin(1, AdvanceIds.Monarchy, 1, false, "notice"),
                            _ => context.Actor.Participant, _ => throw new InvalidOperationException("Unexpected intelligence"));
                    }
                    return child.Resume(context, input);
                }
                finally { current = null; }
            }, () => child?.Dispose())));
        runtime.BeginClientSession(new()); var actor = SessionClient(runtime); var other = SessionClient(runtime, 2);
        var command = actor.Submit(new("test.acquire", new([]))).Correlation.Command; runtime.AdvanceOne();
        Equal(true, f.World.Civilizations[1].Advances.Contains(AdvanceIds.Monarchy));
        var before = actor.ObserveNotices()!; Equal(1, before.Items.Count);
        Equal(true, before.Items[0].Text.Contains("Crowned Assembly", StringComparison.Ordinal));
        Equal(0, other.ObserveNotices()!.Items.Count); Equal(false, runtime.AdvanceOne());
        Equal(before, actor.ObserveNotices()); Throws<InvalidOperationException>(() => effects.RefreshStatus());
        var question = RequiredInteraction(actor, command); Equal(true, actor.Reply(question, 1).Accepted); runtime.AdvanceOne();
        Equal(CommandStatus.Completed, RequiredResult(actor, command).Status);
        Equal(Government.Anarchy, f.World.Civilizations[1].Government);
        Equal(2, actor.ObserveNotices()!.Items.Count); Equal(1, before.Items.Count);
        Equal(0, other.ObserveNotices()!.Items.Count);
    }

    private static void ResearchNoticesDistinctRecipients()
    {
        using var runtime = new SessionRuntime(new(), new(304));
        runtime.Process.Rules.Definitions.Advances[5] = new() { Identity = new("custom", "Shared discovery") };
        runtime.Process.Rules.Definitions.Advances[AdvanceIds.Writing] = new() { Identity = new("script", "Inscribed records") };
        runtime.Process.Rules.Definitions.Units[2] = new() { Identity = new("envoy", "Envoy"), Prerequisite = (AdvanceReference)AdvanceIds.Writing };
        runtime.World.Names[2] = new() { Tribe = "Other people" };
        runtime.RegisterCommand(CommandDefinitionFor(_ => new TestContinuation((context, _) =>
        {
            var effects = new SinglePlayerResearchPresentation(() => context, () => 1);
            effects.Acquired(new(2, 5, 0, "Other people", "Shared discovery", null, false, new([])));
            effects.BarracksSold(new(2, 5, 3, 90, false));
            effects.EmissaryKnowledge(2, 5);
            effects.Encyclopedia(5);
            effects.Tutorial(2, ResearchTutorial.Writing);
            effects.Selected(1, 6); // Must not appear in civilization 2's command result.
            effects.Selected(2, 5);
            return CommandDone(1);
        })));
        runtime.BeginClientSession(new()); var viewer = SessionClient(runtime); var owner = SessionClient(runtime, 2);
        var command = owner.Submit(CommandRequestFor()).Correlation.Command; runtime.AdvanceOne();
        var recorded = RequiredResult(owner, command).Committed.Single();
        Equal(new CommandValue.Integer(5), CommandDescriptor.Value(recorded.Values, "advance"));
        Equal(3, viewer.ObserveNotices()!.Items.Count);
        Equal(true, viewer.ObserveNotices()!.Items[0].Text.Contains("Other people acquired", StringComparison.Ordinal));
        Equal(true, viewer.ObserveNotices()!.Items[2].Text.Contains("Shared discovery", StringComparison.Ordinal));
        Equal(2, owner.ObserveNotices()!.Items.Count);
        Equal(true, owner.ObserveNotices()!.Items[0].Text.Contains("90 gold", StringComparison.Ordinal));
        Equal(true, owner.ObserveNotices()!.Items[1].Text.Contains("Inscribed records", StringComparison.Ordinal));
        Equal(true, owner.ObserveNotices()!.Items[1].Text.Contains("Envoy", StringComparison.Ordinal));
        Equal(false, runtime.World.Civilizations[2].Advances.Contains(5)); Equal(0UL, runtime.GameplayRandom.Draws);
    }
}
