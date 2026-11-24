using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static void SpaceshipRetainedSelection()
    {
        var f = new ShipFixture(); f.Parts(39, 8, 7, 4, 4, 4);
        using var op = f.Construction.BeginAddPart(1, 1);
        var first = op.Resume()!; Equal(new SpaceshipPartChoice(1, 1, 8, 7, 0), first);
        Equal(0, f.Choices.Count); Equal(false, f.World.Civilizations[1].SpaceshipFlags.Exists);
        f.Work.Mass = 123; Equal(first, op.Resume()); Equal(123, f.Work.Mass);
        var retry = op.Resume(0)!; Equal((int?)1, retry.FullPart); Sequence(new[] { 1 }, f.FullParts);
        Equal((short)7, f.World.Civilizations[1].SpaceshipParts[2]);
        Equal(null, op.Resume(1)); Equal((int?)2, op.Result); Equal((short)8, f.World.Civilizations[1].SpaceshipParts[2]);
        Equal(true, f.World.Civilizations[1].SpaceshipFlags.Exists); Equal(false, f.World.Civilizations[1].SpaceshipFlags.Launched);
        Equal(null, op.Resume(0)); Equal((short)8, f.World.Civilizations[1].SpaceshipParts[1]);
        foreach (var (group, reply, part) in new[] { (1, -1, 0), (2, -1, 2), (-35, 2, 5) })
        {
            var custom = new ShipFixture(); using var child = custom.Construction.BeginAddPart(1, group);
            Equal(group == 1 ? 1 : 2, child.Resume()!.Group); child.Resume(reply);
            Equal((int?)part, child.Result); Equal((short)1, custom.World.Civilizations[1].SpaceshipParts[part]);
        }
    }

    private static void SpaceshipRetainedLiveRetryAndAi()
    {
        var f = new ShipFixture(); f.Parts(39, 8, 7, 4, 4, 4);
        f.OnFull = () => f.World.Civilizations[1].SpaceshipParts[2] = 8;
        using var full = f.Construction.BeginAddPart(1, 1); full.Resume(); Equal(null, full.Resume(0));
        Equal((int?)1, full.Result); Equal(false, f.World.Civilizations[1].SpaceshipFlags.Exists);
        foreach (var (stock, expected) in new (short, short)[] { (8, 9), (short.MaxValue, short.MinValue) })
        {
            var changed = new ShipFixture(); changed.Parts(39, stock, 7, 1, 0, 0);
            using var op = changed.Construction.BeginAddPart(1, 1); op.Resume();
            changed.World.Globals.HumanPlayers = CivilizationSet.Only(2); Equal(null, op.Resume(0));
            Equal((int?)3, op.Result); Equal(expected, changed.World.Civilizations[1].SpaceshipParts[1]); Equal(0, changed.FullParts.Count);
        }
        var ai = new ShipFixture(false); ai.Parts(39, 8, 7, 4, 4, 4);
        ai.Data.AddCity(0, 1, new(10, 10)); ai.Data.AddCity(1, 2, new(12, 10));
        using var launch = ai.Construction.BeginAddPart(1, 1); Equal(null, launch.Resume()); Equal((int?)3, launch.Result);
        Equal(true, ai.World.Civilizations[1].SpaceshipFlags.Launched); Equal((short)8, ai.World.Civilizations[1].SpaceshipParts[2]);
        Equal(1, ai.Launches.Count); Sequence(new[] { -1 }, ai.Windows); Sequence(new[] { new CitySlot(0) }, ai.Reselections);
        Equal(null, launch.Resume()); Equal(1, ai.Launches.Count);
    }

    private static void SpaceshipRetainedFaultAndDisposal()
    {
        var f = new ShipFixture(); using var invalid = f.Construction.BeginAddPart(1, 1); invalid.Resume();
        Throws<IndexOutOfRangeException>(() => invalid.Resume(99)); Equal(false, f.World.Civilizations[1].SpaceshipFlags.Exists);
        Throws<InvalidOperationException>(() => invalid.Resume(0)); Equal((short)0, f.World.Civilizations[1].SpaceshipParts[1]);
        var disposed = f.Construction.BeginAddPart(1, 2); disposed.Resume(); disposed.Dispose();
        Throws<ObjectDisposedException>(() => disposed.Resume(0)); Equal((short)1900, f.World.Civilizations[1].SpaceshipLaunchYear);
        f = new(); f.Parts(39, 8, 7, 4, 4, 4); f.OnFull = () => throw new InvalidOperationException("NoFurther failed");
        using var failed = f.Construction.BeginAddPart(1, 1); failed.Resume();
        Throws<InvalidOperationException>(() => failed.Resume(0)); Equal(1, f.FullParts.Count);
        Throws<InvalidOperationException>(() => failed.Resume(1)); Equal(1, f.FullParts.Count); Equal((short)7, f.World.Civilizations[1].SpaceshipParts[2]);
        var ai = new ShipFixture(false); ai.Parts(39, 8, 7, 4, 4, 4); ai.OnLaunch = () => throw new InvalidOperationException("Launch notice failed");
        using var launch = ai.Construction.BeginAddPart(1, 1);
        Throws<InvalidOperationException>(() => launch.Resume()); Equal(true, ai.World.Civilizations[1].SpaceshipFlags.Launched);
        Equal((short)8, ai.World.Civilizations[1].SpaceshipParts[2]); Equal(1, ai.Launches.Count);
        Throws<InvalidOperationException>(() => launch.Resume()); Equal(1, ai.Launches.Count);
    }

    private static CityProduction.Operation BeginRetainedShipProduction(CityProductionFixture f, int item = BuildingIds.Component)
    {
        f.City.Production = (sbyte)-item; f.City.ShieldBox = 40;
        var life = f.Units.Lifecycle;
        return f.Production.Begin(new(0), "production", life.Elimination, new IncidentFixture(life.Random, life.Data).Incidents);
    }

    private static void SpaceshipRetainedProductionParent()
    {
        var f = new CityProductionFixture(); using var op = BeginRetainedShipProduction(f);
        Equal(null, op.Resume()); Equal(false, op.Finished); Equal(1, op.PartDecision!.Group);
        Equal((short)0, f.City.ShieldBox); Equal((int?)BuildingIds.Component, f.World.Production.CompletedImprovement);
        Equal(1, f.Notices.Count); Equal(true, f.City.Flags.DefenderLost); Equal(false, f.World.Civilizations[1].SpaceshipFlags.Exists);
        Equal(false, f.Events.Contains("choose-part")); op.Resume(); Equal(1, f.Notices.Count);
        Equal(null, op.Resume(new(1))); Equal(true, op.Finished); Equal(null, op.PartDecision);
        Equal((short)1, f.World.Civilizations[1].SpaceshipParts[2]); Equal(false, f.City.Flags.DefenderLost);
        Equal(CityProductionOutcome.Completed, op.Result!.Outcome); Equal(1, f.Notices.Count);
        op.Resume(new(0)); Equal((short)0, f.World.Civilizations[1].SpaceshipParts[1]);
        var dropped = new CityProductionFixture(); var pending = BeginRetainedShipProduction(dropped, BuildingIds.Module);
        pending.Resume(); Equal(2, pending.PartDecision!.Group); pending.Dispose();
        Throws<ObjectDisposedException>(() => pending.Resume(new(0))); Equal((short)0, dropped.City.ShieldBox);
        Equal(true, dropped.City.Flags.DefenderLost); Equal(false, dropped.World.Civilizations[1].SpaceshipFlags.Exists);
        var filled = new CityProductionFixture(); filled.OnBuilt = () =>
        { filled.World.Civilizations[1].SpaceshipParts[1] = filled.World.Civilizations[1].SpaceshipParts[2] = 8; };
        using var noPart = BeginRetainedShipProduction(filled); noPart.Resume(); Equal(true, noPart.Finished);
        Equal(true, filled.Events.Contains("bad-space:" + BuildingIds.Component)); Equal(false, filled.World.Civilizations[1].SpaceshipFlags.Exists);
        Equal((short)0, filled.City.ShieldBox); Equal(CityProductionOutcome.Completed, noPart.Result!.Outcome);
    }

    private static void SpaceshipRetainedPrivateProduction()
    {
        using var runtime = new SessionRuntime(new(), new(281));
        var data = CreationFixture(new(runtime.Process, runtime.World));
        var f = new CityProductionFixture(units: new(new EliminationFixture(data: data)));
        f.World.Civilizations[1].SpaceshipParts[1] = 8; f.World.Civilizations[1].SpaceshipParts[2] = 7;
        ParticipantId recipient = default;
        runtime.RegisterCommand(new(new(1, "test.assembly", "Assembly", AccessClass.Player, new([]), new([])),
            (_, _, _) => new(), _ => new CityProductionOperation(BeginRetainedShipProduction(f), _ => recipient)));
        runtime.BeginClientSession(new()); var actor = SessionClient(runtime, 2); var owner = SessionClient(runtime, 1); recipient = owner.Identity.Participant;
        var command = actor.Submit(new("test.assembly", new([]))).Correlation.Command; runtime.AdvanceOne();
        var first = RequiredInteraction(owner, command); Equal(null, RequiredResult(actor, command).Interaction);
        Equal(false, actor.Reply(first, 0).Accepted); Equal(false, owner.Reply(first, -1).Accepted); Equal(false, owner.Cancel(command).Accepted);
        Equal(false, RequiredResult(owner, command).Interaction!.Spec.Cancellable);
        Equal(true, owner.Reply(first, 0).Accepted); runtime.AdvanceOne(); var retry = RequiredInteraction(owner, command);
        Equal(true, RequiredResult(owner, command).Interaction!.Spec.Prompt.Contains("Propulsion is already full", StringComparison.Ordinal));
        Equal(false, owner.Reply(first, 1).Accepted); Equal(1, f.Notices.Count); Equal(true, f.City.Flags.DefenderLost);
        Equal(true, owner.Reply(retry, 1).Accepted); runtime.AdvanceOne();
        var done = RequiredResult(actor, command); Equal(CommandStatus.Completed, done.Status);
        Equal(1, done.Committed.Count(e => e.Name == "city.production.completed")); Equal(false, f.City.Flags.DefenderLost);
        Equal((short)8, f.World.Civilizations[1].SpaceshipParts[2]); Equal(false, owner.Reply(retry, 1).Accepted);
        var direct = new CityProductionFixture(); using var adapter = new CityProductionOperation(BeginRetainedShipProduction(direct), c => new((ulong)c));
        var context = OutcomeContext(direct.Units.Data); adapter.Resume(context, new());
        foreach (var kind in new[] { OperationInputKind.Cancel, OperationInputKind.Timeout })
        { Equal(CommandStatus.AwaitingInteraction, adapter.Resume(context, new(kind)).Status); }
        Equal(false, direct.World.Civilizations[1].SpaceshipFlags.Exists); Equal((short)0, direct.World.Civilizations[1].SpaceshipParts[0]);
        Equal(CommandStatus.Completed, adapter.Resume(context, new(OperationInputKind.Choice, 0)).Status);
        Equal((short)1, direct.World.Civilizations[1].SpaceshipParts[1]);
    }
}
