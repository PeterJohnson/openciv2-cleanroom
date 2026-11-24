using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // U06/S02: promotion and casualty information precede the retained death event.
    private static void CombatPresentationCasualtyAndFailure()
    {
        foreach (bool fail in new[] { false, true })
        {
            using var runtime = new SessionRuntime(new(), new(314));
            OperationContext? current = null;
            var presentation = new SinglePlayerCombatPresentation(
                () => current ?? throw new InvalidOperationException("No active step"), () => 1,
                c => c == 1 ? 197 : throw new InvalidOperationException("Unexpected opponent"),
                _ => throw new InvalidOperationException("No targeting in combat"));
            var f = new SinglePlayerCombatFixture(ExecutionWin(), new(runtime.Process, runtime.World), presentation.Effects);
            f.World.Names[1].Tribe = "River Folk";
            f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Identity = new("guard", "River Guard") };
            var entry = f.Event(OpenCiv2.Core.State.EventTrigger.UnitKilled);
            CombatOperation? child = null;
            runtime.RegisterCommand(new(new(1, "test.presentation-combat", "Combat presentation", AccessClass.Player,
                new([]), new([new("result", ValueKind.Integer)])), (_, _, _) => new(), _ => new TestContinuation((context, input) =>
                {
                    current = fail ? context with
                    {
                        Notify = (recipient, text) =>
                    { context.Notify!(recipient, text); throw new InvalidOperationException("Notice unavailable"); }
                    } : context;
                    try
                    {
                        child ??= new(f.Begin(), c => new((ulong)c), _ => throw new InvalidOperationException("No intelligence"),
                            _ => throw new InvalidOperationException("No city window"));
                        return child.Resume(context, input);
                    }
                    finally { current = null; }
                }, () => child?.Dispose())));
            runtime.BeginClientSession(new()); var owner = SessionClient(runtime); var victim = SessionClient(runtime, 2);
            var initiator = SessionClient(runtime, 3);
            var command = initiator.Submit(new("test.presentation-combat", new([]))).Correlation.Command; runtime.AdvanceOne();
            Equal(true, f.Data.Unit(0).Flags.Veteran); Equal(true, f.Data.Unit(1).Token.Occupied);
            Equal(true, owner.ObserveNotices()!.Items.Single().Text.Contains("River Guard", StringComparison.Ordinal));
            Equal(0, victim.ObserveNotices()!.Items.Count); Equal(0, initiator.ObserveNotices()!.Items.Count);
            Equal(fail ? 0 : 1, f.World.Casualties[2].Count);
            if (fail)
            { Equal(CommandStatus.Failed, RequiredResult(initiator, command).Status); }
            else
            {
                Equal((short)197, f.World.Casualties[2].At(0).OpponentColor);
                Equal("River Folk", f.World.Casualties[2].At(0).OpponentName);
                var pending = RequiredInteraction(owner, command); var before = owner.ObserveNotices();
                Equal(false, runtime.AdvanceOne()); Equal(before, owner.ObserveNotices());
                Equal(false, initiator.Reply(pending, 0).Accepted); Equal(true, owner.Reply(pending, 0).Accepted); runtime.AdvanceOne();
                pending = RequiredInteraction(owner, command); Equal(true, owner.Reply(pending, 0).Accepted); runtime.AdvanceOne();
                Equal(CommandStatus.Completed, RequiredResult(initiator, command).Status);
                Equal(false, f.Data.Unit(1).Token.Occupied); Equal(true, entry.Values.Actions.Consumed);
                Equal(1, f.World.Casualties[2].Count); Equal(1, owner.ObserveNotices()!.Items.Count);
            }
            Equal(false, runtime.AdvanceOne()); Throws<InvalidOperationException>(() => presentation.Publish()); f.Consumed();
        }
    }

    // U07: interception addresses both owners; notice failure cannot cancel the nuclear result.
    private static void CombatPresentationNuclearRecipients()
    {
        foreach (bool intercept in new[] { false, true })
            foreach (bool fail in new[] { false, true })
            {
                using var runtime = new SessionRuntime(new(), new(315));
                OperationContext? current = null; int delivered = 0;
                var presentation = new SinglePlayerCombatPresentation(
                    () => current ?? throw new InvalidOperationException("No active step"), () => 1,
                    _ => throw new InvalidOperationException("No casualty history for nuclear damage"),
                    _ => throw new InvalidOperationException("No targeting"));
                var f = new SinglePlayerCombatFixture(data: new(runtime.Process, runtime.World), presentation: presentation.Effects);
                f.F.F.Missile(); f.F.F.Ocean(); f.World.Names[1].Tribe = "River Folk";
                f.World.Entities.Cities.Storage[0].Name = "Stone Haven";
                if (intercept) BuildingQueries.SetBuilding(f.World, new(0), BuildingIds.SdiDefense, true);
                CombatOperation? child = null;
                runtime.RegisterCommand(new(new(1, "test.presentation-nuclear", "Nuclear presentation", AccessClass.Player,
                    new([]), new([new("result", ValueKind.Integer)])), (_, _, _) => new(), _ => new TestContinuation((context, input) =>
                    {
                        current = context with
                        {
                            Notify = (recipient, text) =>
                        {
                            context.Notify!(recipient, text);
                            if (recipient.Value == 2 && ++delivered == 1 && fail) throw new InvalidOperationException("Notice unavailable");
                        }
                        };
                        try
                        {
                            child ??= new(f.Begin(), c => new((ulong)c), _ => throw new InvalidOperationException("No intelligence"),
                                _ => throw new InvalidOperationException("No city window"));
                            return child.Resume(context, input);
                        }
                        finally { current = null; }
                    }, () => child?.Dispose())));
                runtime.BeginClientSession(new()); var attacker = SessionClient(runtime); var defender = SessionClient(runtime, 2);
                var initiator = SessionClient(runtime, 3);
                var command = initiator.Submit(new("test.presentation-nuclear", new([]))).Correlation.Command;
                for (int i = 0; i < 100 && runtime.AdvanceOne(); ++i) { }
                Equal(fail ? CommandStatus.Failed : CommandStatus.Completed, RequiredResult(initiator, command).Status);
                Equal(false, f.Data.Unit(0).Token.Occupied); Equal(intercept, f.Data.Unit(1).Token.Occupied);
                Equal((sbyte)(intercept ? 5 : 3), f.World.Entities.Cities.Storage[0].Size);
                var notices = defender.ObserveNotices()!.Items;
                Equal(intercept ? 2 : 1, notices.Count); Equal(true, notices[0].Text.Contains("River Folk", StringComparison.Ordinal));
                var attackNotices = attacker.ObserveNotices()!.Items;
                Equal(intercept ? 2 : 1, attackNotices.Count);
                // This custom nuclear unit has Land domain; its ocean origin produces an amphibious opening.
                Equal(true, attackNotices[0].Text.Contains("amphibious attack", StringComparison.Ordinal));
                if (intercept)
                {
                    Equal(true, notices[1].Text.Contains("Stone Haven", StringComparison.Ordinal));
                    Equal(notices[1].Text, attackNotices[1].Text);
                }
                Equal(0, initiator.ObserveNotices()!.Items.Count); Equal(0, f.World.Casualties[2].Count); f.Consumed();
            }
    }

    // C09/S02: the local victim receives capture information after retained scenario choices.
    private static void CombatPresentationCaptureAndFocus()
    {
        using var runtime = new SessionRuntime(new(), new(316));
        OperationContext? current = null;
        var presentation = new SinglePlayerCombatPresentation(
            () => current ?? throw new InvalidOperationException("No active step"), () => 2,
            _ => throw new InvalidOperationException("No casualty history"), _ => throw new InvalidOperationException("No targeting"));
        var f = new SinglePlayerCombatFixture(data: new(runtime.Process, runtime.World), presentation: presentation.Effects);
        var entry = f.Event(OpenCiv2.Core.State.EventTrigger.CityTaken);
        f.World.Multiplayer.LocalHuman = 2; f.World.Entities.Cities.Storage[0].Name = "Stone Haven";
        entry.SetString(1, new("Stone Haven"));
        f.World.Names[1].Tribe = "River Folk"; f.World.Names[2].Adjective = "Highland";
        var actorView = f.World.Views[1]; var center = f.World.Entities.Cities.Storage[0].Position;
        CityCaptureOperation? child = null;
        runtime.RegisterCommand(new(new(1, "test.presentation-capture", "Capture presentation", AccessClass.Player,
            new([]), new([new("city", ValueKind.Integer)])), (_, _, _) => new(), _ => new TestContinuation((context, input) =>
            {
                current = context;
                try
                {
                    child ??= new(f.Services.Capture.Begin(new(0), 1, 1, 77, "capture"), c => new((ulong)c),
                        request => new("Captured " + request.Name, new((ulong)request.Owner),
                            new CommandValue.City(request.City), new([0])));
                    return child.Resume(context, input);
                }
                finally { current = null; }
            }, () => child?.Dispose())));
        runtime.BeginClientSession(new()); var actor = SessionClient(runtime); var victim = SessionClient(runtime, 2);
        var initiator = SessionClient(runtime, 3);
        var command = initiator.Submit(new("test.presentation-capture", new([]))).Correlation.Command; runtime.AdvanceOne();
        Equal(0, victim.ObserveNotices()!.Items.Count);
        // The direct scenario grant has source zero; the remote Library recipient does not
        // open a local government prompt. Only the local scenario text requires a reply.
        var prompt = RequiredInteraction(victim, command); Equal(false, actor.Reply(prompt, 0).Accepted);
        Equal(false, runtime.AdvanceOne()); Equal(prompt, RequiredInteraction(victim, command));
        Equal(true, victim.Reply(prompt, 0).Accepted); runtime.AdvanceOne();
        Equal(true, entry.Values.Actions.Consumed);
        string text = victim.ObserveNotices()!.Items.Single().Text;
        foreach (string name in new[] { "Stone Haven", "River Folk", "Highland" }) Equal(true, text.Contains(name, StringComparison.Ordinal));
        Equal(actorView, f.World.Views[1]); Equal(center, f.World.Views[2].Center);
        Equal(new CivilizationSlot(1), f.World.Entities.Cities.Storage[0].Owner);
        var window = RequiredInteraction(actor, command); Equal(false, victim.Reply(window, 0).Accepted);
        Equal(true, f.World.Calculation.Processing); Equal(true, actor.Reply(window, 0).Accepted); runtime.AdvanceOne();
        Equal(CommandStatus.Completed, RequiredResult(initiator, command).Status); Equal(false, f.World.Calculation.Processing);
        Equal((byte)41, f.World.Entities.Cities.Storage[0].TurnsCaptured);
        Equal(0, actor.ObserveNotices()!.Items.Count); Equal(0, initiator.ObserveNotices()!.Items.Count); f.Consumed();
    }
}
