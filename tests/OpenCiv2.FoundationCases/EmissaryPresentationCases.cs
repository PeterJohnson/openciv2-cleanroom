using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Rules;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // V05 §2/P04/X01: real reception, private notice delivery and custom installed text.
    private static void EmissaryPresentationRetainedReception()
    {
        foreach (int? reply in new int?[] { 0, 1, null })
        {
            using var runtime = new SessionRuntime(new(), new(305));
            OperationContext? current = null;
            OperationContext Borrow() => current ?? throw new InvalidOperationException("No active session step");
            var presentation = new SinglePlayerEmissaryPresentation(Borrow);
            var f = new AllyHelpFixture(reply == 0 ? [GreetingChoice()] : [], new(runtime.Process, runtime.World), presentation);
            foreach (int i in Enumerable.Range(0, RuleLimits.DiplomaticAttitudes))
                f.Data.Rules.DiplomaticAttitudes[i] = new("custom", "Measured stance");
            var origin = f.World.Views[2];
            EmissaryOperation? child = null;
            runtime.RegisterCommand(new(new(1, "test.emissary", "Receive emissary", AccessClass.Player,
                new([]), new([new("accepted", ValueKind.Boolean)])), (_, _, _) => new(), _ => new TestContinuation((context, input) =>
            {
                current = context;
                try
                {
                    child ??= new(f.Receive(new(10, 8)), owner => new((ulong)owner),
                        _ => throw new InvalidOperationException("No intelligence without an embassy"));
                    var result = child.Resume(context, input);
                    if (result.Status == CommandStatus.Completed) Equal(null, f.Emissaries.Reset());
                    return result;
                }
                finally { current = null; }
            }, () => child?.Dispose())));
            runtime.BeginClientSession(new()); var receiver = SessionClient(runtime); var initiator = SessionClient(runtime, 2);
            var command = initiator.Submit(new("test.emissary", new([]))).Correlation.Command; runtime.AdvanceOne();
            Equal(null, RequiredResult(initiator, command).Interaction);
            var question = RequiredResult(receiver, command).Interaction!;
            Equal(true, question.Spec.Prompt.Contains("Queen Ada of the Allies", StringComparison.Ordinal));
            Equal(new MapPoint(10, 8), f.World.Views[1].Center); Equal(origin, f.World.Views[2]);
            Equal(0, receiver.ObserveNotices()!.Items.Count); Equal(false, runtime.AdvanceOne());
            Equal(question, RequiredResult(receiver, command).Interaction); Throws<InvalidOperationException>(() => presentation.CloseLeader());
            if (reply is { } answer) Equal(true, receiver.Reply(question.Id, answer).Accepted);
            else Equal(true, receiver.Cancel(command).Accepted);
            runtime.AdvanceOne();
            Equal(CommandStatus.Completed, RequiredResult(initiator, command).Status);
            Equal(new CommandValue.Boolean(reply == 0), RequiredResult(initiator, command).Result.Single().Value);
            var notices = receiver.ObserveNotices()!;
            Equal(reply == 0 ? 1 : 0, notices.Items.Count); Equal(0, initiator.ObserveNotices()!.Items.Count);
            if (reply == 0)
            {
                Equal(true, notices.Items[0].Text.Contains("Queen Ada of the Allies", StringComparison.Ordinal));
                Equal(true, notices.Items[0].Text.Contains("Measured stance", StringComparison.Ordinal));
                Equal(false, notices.Items[0].Text.Contains("nuclear", StringComparison.Ordinal));
                Equal(false, notices.Items[0].Text.Contains("vendetta", StringComparison.Ordinal));
                f.World.Names[2].Leader = "Changed";
                Equal(notices, receiver.ObserveNotices());
            }
            Equal(false, f.World.Diplomacy.MeetingActive || f.World.Diplomacy.AskedEmissary);
            Equal(false, runtime.AdvanceOne()); f.Random.RequireChoicesConsumed();
        }
    }

    private static void EmissaryPresentationNuclearAndFailure()
    {
        foreach (bool fail in new[] { false, true })
        {
            using var runtime = new SessionRuntime(new(), new(306));
            OperationContext? current = null;
            var presentation = new SinglePlayerEmissaryPresentation(() => current ?? throw new InvalidOperationException("No active session step"));
            var f = new AllyHelpFixture(fail ? [] : [GreetingChoice()], new(runtime.Process, runtime.World), presentation);
            f.World.Civilizations[1].UnitsActive[UnitTypeIds.NuclearMissile] = 1;
            f.World.Civilizations[2].UnitsActive[UnitTypeIds.NuclearMissile] = 1;
            f.World.Civilizations[2].Treaties[1].Vendetta = true;
            runtime.RegisterCommand(CommandDefinitionFor(_ => new TestContinuation((context, _) =>
            {
                current = fail ? context with
                {
                    Notify = (recipient, text) =>
                {
                    context.Notify!(recipient, text);
                    throw new InvalidOperationException("Required notice failed after publication");
                }
                } : context;
                try
                {
                    using var operation = f.Receive();
                    Equal(null, operation.Resume()); Equal(true, operation.Accepted);
                    return CommandDone(1);
                }
                finally { current = null; }
            })));
            runtime.BeginClientSession(new()); var receiver = SessionClient(runtime); var other = SessionClient(runtime, 2);
            var command = other.Submit(CommandRequestFor()).Correlation.Command; runtime.AdvanceOne();
            Equal(fail ? CommandStatus.Failed : CommandStatus.Completed, RequiredResult(other, command).Status);
            var notices = receiver.ObserveNotices()!;
            Equal(fail ? 1 : 3, notices.Items.Count);
            Equal(true, notices.Items[0].Text.Contains("insists", StringComparison.Ordinal));
            if (!fail)
            {
                Equal(true, notices.Items[1].Text.Contains("vendetta", StringComparison.Ordinal));
                Equal(true, notices.Items[1].Text.Contains("nuclear weapons", StringComparison.Ordinal));
                Equal(true, notices.Items[2].Text.Contains("acknowledges your nuclear weapons", StringComparison.Ordinal));
                Equal(true, notices.Items[2].Text.Contains("also has nuclear weapons", StringComparison.Ordinal));
            }
            Equal(!fail, f.World.Civilizations[1].Treaties[2].NuclearThreat);
            Equal(0, other.ObserveNotices()!.Items.Count); Equal(false, runtime.AdvanceOne());
            Equal(notices, receiver.ObserveNotices()); f.Random.RequireChoicesConsumed();
        }
    }
}
