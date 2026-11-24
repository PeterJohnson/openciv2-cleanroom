using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Rules;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // U01/W03/V04/S02: creation information waits for actual recursive contact decisions.
    private static void LifecyclePresentationRetainedCreation()
    {
        using var runtime = new SessionRuntime(new(), new(311));
        OperationContext? current = null;
        var presentation = new SinglePlayerLifecyclePresentation(
            () => current ?? throw new InvalidOperationException("No active step"), () => 1);
        var f = new SinglePlayerLifecycleFixture(data: new(runtime.Process, runtime.World), presentation: presentation.Effects);
        var entry = ScenarioNestedCreation(f.Diplomacy.Events);
        f.World.Globals.GameOptions.Tutorial = true; f.World.Civilizations[1].MilitaryCount = 0;
        ScenarioEventOperation? child = null;
        runtime.RegisterCommand(new(new(1, "test.lifecycle", "Retained scenario creation", AccessClass.Player,
            new([]), new([new("result", ValueKind.Integer)])), (_, _, _) => new(), _ => new TestContinuation((context, input) =>
            {
                current = context;
                try
                {
                    child ??= new(f.Services.Events.Begin(entry, "scenario"), c => new((ulong)c));
                    return child.Resume(context, input);
                }
                finally { current = null; }
            }, () => child?.Dispose())));
        runtime.BeginClientSession(new()); var receiver = SessionClient(runtime); var initiator = SessionClient(runtime, 2);
        var command = initiator.Submit(new("test.lifecycle", new([]))).Correlation.Command; runtime.AdvanceOne();
        var prompt = RequiredInteraction(receiver, command);
        Equal(0, receiver.ObserveNotices()!.Items.Count); Equal(false, runtime.AdvanceOne());
        Equal(prompt, RequiredInteraction(receiver, command)); Equal((short)1, f.World.Civilizations[1].MilitaryCount);
        Equal(true, receiver.Reply(prompt, 0).Accepted); runtime.AdvanceOne();
        Equal(true, receiver.ObserveNotices()!.Items.Single().Text.Contains("first military unit", StringComparison.Ordinal));
        Equal(false, entry.Values.Actions.Consumed);
        prompt = RequiredInteraction(receiver, command); Equal(true, receiver.Reply(prompt, 0).Accepted); runtime.AdvanceOne();
        Equal(CommandStatus.Completed, RequiredResult(initiator, command).Status); Equal(true, entry.Values.Actions.Consumed);
        Equal(1, receiver.ObserveNotices()!.Items.Count); Equal(0, initiator.ObserveNotices()!.Items.Count);
        Throws<InvalidOperationException>(() => presentation.RedrawMaps()); f.Consumed();
    }

    // V06 §2: relocation precedes notice; promotion follows it. A required failure keeps that prefix.
    private static void LifecyclePresentationSpyEscapeAndFailure()
    {
        foreach (bool fail in new[] { false, true })
        {
            using var runtime = new SessionRuntime(new(), new(312));
            OperationContext? current = null;
            var presentation = new SinglePlayerLifecyclePresentation(
                () => current ?? throw new InvalidOperationException("No active step"), () => 3);
            var f = new SinglePlayerLifecycleFixture([EscapeChoice("presentation", 2, 1)],
                new(runtime.Process, runtime.World), presentation.Effects);
            f.World.Globals.HumanPlayers = CivilizationSet.Only(1); f.World.Multiplayer.LocalHuman = 1;
            for (int i = 0; i < f.World.Entities.Cities.End; ++i)
                if (f.World.Entities.Cities.Storage[i].Owner.Value == 1) f.World.Entities.Cities.Storage[i].Owner = new(2);
            int city = f.World.Entities.Cities.End; f.Data.AddCity(city, 1, new(14, 10));
            f.World.Entities.Cities.Storage[city].Name = "Safe Haven";
            int unit = f.World.Entities.Units.End; f.Data.AddUnit(unit, 1, UnitTypeIds.Spy, new(10, 10));
            f.Data.Rules.Units[UnitTypeIds.Spy] = f.Data.Rules.Units[UnitTypeIds.Spy] with { Identity = new("courier", "Cipher Courier") };
            runtime.RegisterCommand(CommandDefinitionFor(_ => new TestContinuation((context, _) =>
            {
                current = fail ? context with
                {
                    Notify = (recipient, text) =>
                {
                    context.Notify!(recipient, text); throw new InvalidOperationException("Notice publication failed");
                }
                } : context;
                try
                {
                    using var operation = f.Services.Elimination.BeginEscape(unit, 0, 0, "presentation", f.Services.Incidents);
                    Equal(null, operation.ResumeDecision()); Equal(0, operation.Result); return CommandDone(1);
                }
                finally { current = null; }
            })));
            runtime.BeginClientSession(new()); var owner = SessionClient(runtime); var initiator = SessionClient(runtime, 2);
            var observer = SessionClient(runtime, 3);
            var command = initiator.Submit(CommandRequestFor()).Correlation.Command; runtime.AdvanceOne();
            Equal(fail ? CommandStatus.Failed : CommandStatus.Completed, RequiredResult(initiator, command).Status);
            var notices = owner.ObserveNotices()!; string text = notices.Items.Single().Text;
            Equal(true, text.Contains("Cipher Courier", StringComparison.Ordinal)); Equal(true, text.Contains("Safe Haven", StringComparison.Ordinal));
            Equal(new MapPoint(14, 10), f.Data.Unit(unit).Position); Equal(!fail, f.Data.Unit(unit).Flags.Veteran);
            Equal(true, f.Data.Unit(unit).Token.Occupied); Equal(0, initiator.ObserveNotices()!.Items.Count);
            Equal(0, observer.ObserveNotices()!.Items.Count); Equal(false, runtime.AdvanceOne()); Equal(notices, owner.ObserveNotices());
            f.Consumed();
        }
    }

    // V01 §6/V07 §8: destruction is an observer announcement, followed by ship loss/reset.
    private static void LifecyclePresentationDestructionAndFailure()
    {
        foreach (bool fail in new[] { false, true })
        {
            using var runtime = new SessionRuntime(new(), new(313));
            OperationContext? current = null;
            var presentation = new SinglePlayerLifecyclePresentation(
                () => current ?? throw new InvalidOperationException("No active step"), () => 3);
            var f = new SinglePlayerLifecycleFixture(data: new(runtime.Process, runtime.World), presentation: presentation.Effects);
            f.World.Globals.HumanPlayers = CivilizationSet.Only(1);
            f.World.Globals.CivsAlive = CivilizationSet.Only(1).With(2);
            for (int i = 0; i < f.World.Entities.Cities.End; ++i)
                if (f.World.Entities.Cities.Storage[i].Owner.Value == 1) f.World.Entities.Cities.Storage[i].Owner = new(2);
            for (int i = 0; i < f.World.Entities.Units.End; ++i)
                if (f.World.Entities.Units.Storage[i].Owner.Value == 1) f.World.Entities.Units.Storage[i].Owner = new(2);
            f.World.Civilizations[1].SpaceshipFlags = new() { Exists = true, Launched = true };
            f.World.Names[1].Adjective = "Highland"; f.World.Names[2].Tribe = "River Folk";
            runtime.RegisterCommand(CommandDefinitionFor(_ => new TestContinuation((context, _) =>
            {
                current = fail ? context with
                {
                    Notify = (recipient, text) =>
                {
                    context.Notify!(recipient, text); throw new InvalidOperationException("Notice publication failed");
                }
                } : context;
                try
                {
                    using var operation = f.Services.Elimination.Begin(1, 2, "destruction", f.Services.Incidents);
                    Equal(null, operation.ResumeDecision()); Equal(0, operation.Result); return CommandDone(1);
                }
                finally { current = null; }
            })));
            runtime.BeginClientSession(new()); var owner = SessionClient(runtime); var initiator = SessionClient(runtime, 2);
            var observer = SessionClient(runtime, 3);
            var command = initiator.Submit(CommandRequestFor()).Correlation.Command; runtime.AdvanceOne();
            Equal(fail ? CommandStatus.Failed : CommandStatus.Completed, RequiredResult(initiator, command).Status);
            var notices = observer.ObserveNotices()!; Equal(fail ? 1 : 2, notices.Items.Count);
            Equal(true, notices.Items[0].Text.Contains("Highland", StringComparison.Ordinal));
            Equal(true, notices.Items[0].Text.Contains("River Folk", StringComparison.Ordinal));
            if (!fail) Equal(true, notices.Items[1].Text.Contains("returning home", StringComparison.Ordinal));
            Equal(fail, f.World.Civilizations[1].SpaceshipFlags.Exists);
            Equal(fail ? 0 : 1, f.World.Eliminations.Count);
            Equal(0, owner.ObserveNotices()!.Items.Count); Equal(0, initiator.ObserveNotices()!.Items.Count);
            Equal(false, runtime.AdvanceOne()); Equal(notices, observer.ObserveNotices()); f.Consumed();
        }
    }
}
