using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static void PrepareSettler(UnitProductionFixture f, bool secondCity = true)
    {
        if (secondCity) f.Data.AddCity(1, 1, new(20, 10));
        f.World.Civilizations[1].CityCount = (short)(secondCity ? 2 : 1);
        f.Data.Rules.Units[4] = f.Data.Rules.Units[4] with { Role = RuleRole.Settle };
        f.City.Size = 1; f.City.ShieldBox = 23;
    }

    // C06 settler production uses the actual creation, deletion, city removal and V01 checks.
    private static void ProductionRetainedAbandonment()
    {
        foreach (int reply in new[] { 0, 1 })
        {
            var f = new UnitProductionFixture(); PrepareSettler(f);
            var incidents = new IncidentFixture(f.Lifecycle.Random, f.Data);
            using var op = f.Production.Begin(new(0), "production", f.Lifecycle.Elimination, incidents.Incidents);
            var pending = (SettlerAbandonment)op.ResumeDecision()!;
            Equal(1, pending.Owner); Equal(new UnitType(4), pending.Type);
            Equal((short)7, f.City.ShieldBox); Equal((sbyte)1, f.City.Size); Equal(1, f.World.Entities.Units.End);
            uint next = f.Data.Process.Tokens.NextUnit; Equal(pending, op.ResumeDecision()); Equal(next, f.Data.Process.Tokens.NextUnit);
            Equal(0, f.AbandonRequests); Equal(0, f.Removed.Count);
            Equal(null, op.ResumeDecision(new(reply)));
            Equal(reply == 0 ? UnitProductionOutcome.SettlerCancelled : UnitProductionOutcome.CityDisbanded, op.Result!.Outcome);
            Equal(reply == 0, f.City.Token.Occupied); Equal((short)7, f.City.ShieldBox);
            Equal(reply == 0 ? 0 : 1, f.World.Entities.Units.End); Equal(0, f.Checks);
            if (reply != 0)
            {
                Equal((uint)3, f.Data.Process.Tokens.NextUnit); Equal((uint)2, f.Data.Unit(0).Token.Value);
                Equal((CitySlot?)new CitySlot(1), f.Data.Unit(0).Home);
                Sequence(new[] { "built", "draw" }, f.Order);
            }
            next = f.Data.Process.Tokens.NextUnit; Equal(null, op.ResumeDecision(new(1))); Equal(next, f.Data.Process.Tokens.NextUnit);
            f.Lifecycle.Random.RequireChoicesConsumed();
        }
        foreach (bool chieftain in new[] { false, true })
        {
            var f = new UnitProductionFixture(); PrepareSettler(f, false);
            if (chieftain) f.World.Globals.Difficulty = Difficulty.Chieftain;
            var incidents = new IncidentFixture(f.Lifecycle.Random, f.Data);
            using var op = f.Production.Begin(new(0), "gate", f.Lifecycle.Elimination, incidents.Incidents);
            Equal(null, op.ResumeDecision());
            Equal(chieftain ? UnitProductionOutcome.ChieftainRefusal : UnitProductionOutcome.LastCitySettler, op.Result!.Outcome);
            Equal((short)(chieftain ? 27 : 7), f.City.ShieldBox); Equal((sbyte)1, f.City.Size);
            Equal(chieftain ? 0 : 1, f.World.Entities.Units.End); Equal(true, f.City.Flags.DefenderLost);
        }
    }

    private static (CityProductionFixture F, AssessmentFixture A, IncidentFixture Incidents, GameplayRandom Restart)
        RetainedProductionFixture(RevelationFixture? data = null)
    {
        var f = new CityProductionFixture(units: new(new EliminationFixture(data: data)));
        var (a, incidents, restart) = RetainedAssessmentFixture(true, f.Units.Data, "production/remove/unit/2/check");
        f.City.Owner = new(4); f.City.Size = 1; f.City.Production = 5; f.City.ShieldBox = 16;
        f.Units.Data.AddCity(1, 4, new(28, 10)); f.World.Civilizations[4].CityCount = 2;
        f.World.Civilizations[1].CityCount = 0; f.World.Globals.HumanPlayers = CivilizationSet.Only(2).With(4);
        f.Rules.Units[5] = f.Rules.Units[0] with { CostRows = 2, Prerequisite = AdvanceReference.None, ObsoleteBy = AdvanceReference.Never };
        f.Units.Data.Unit(2).Home = new(0);
        return (f, a, incidents, restart);
    }

    private static void ProductionRetainedNestedRemoval()
    {
        foreach (bool fail in new[] { false, true })
        {
            var (f, a, incidents, restart) = RetainedProductionFixture();
            using var op = f.Production.Begin(new(0), "production", a.Lifecycle.Elimination, incidents.Incidents);
            Equal(null, op.Resume()); Equal(true, op.UnitDecision is SettlerAbandonment); Equal((short)0, f.City.ShieldBox);
            Equal(null, op.Resume(new(1)));
            var pending = (UnitProductionLifecycle)op.UnitDecision!;
            Equal(2, ((EspionagePretext)pending.Decision).Victim.Civilization);
            Equal(false, op.Finished); Equal(true, f.City.Token.Occupied); Equal((sbyte)0, f.City.Size);
            Equal(0, f.Units.Removed.Count); Equal(0, f.Units.BuiltTypes.Count);
            uint next = f.Units.Data.Process.Tokens.NextUnit;
            Equal(null, op.Resume()); Equal(pending, op.UnitDecision); Equal(next, f.Units.Data.Process.Tokens.NextUnit);
            if (fail)
            {
                a.Lifecycle.Recreate = null;
                Throws<InvalidOperationException>(() => op.Resume(new(0)));
                Equal(true, f.City.Token.Occupied); Equal(0, f.Units.Removed.Count);
                Throws<InvalidOperationException>(() => op.Resume(new(0))); Equal(next, f.Units.Data.Process.Tokens.NextUnit);
            }
            else
            {
                f.World.TurnLoopActive = false;
                Equal(null, op.Resume(new(0))); Equal(true, op.Finished);
                Equal(UnitProductionOutcome.CityDisbanded, op.Result!.Unit!.Outcome); Equal(false, f.City.Token.Occupied);
                Equal(1, f.Units.Removed.Count); Equal(new UnitType(5), f.Units.BuiltTypes.Single());
                Equal(1, f.World.Entities.Units.Scanned.ToArray().Count(u => u.Token.Occupied && u.Owner.Value == 4 && u.Type.Value == 5));
                Equal(0, f.Units.Checks); a.Random.RequireChoicesConsumed(); restart.RequireChoicesConsumed();
            }
        }
    }

    private static void ProductionRetainedCancelDisposeAndResearch()
    {
        foreach (var kind in new[] { OperationInputKind.Cancel, OperationInputKind.Timeout })
        {
            var f = new CityProductionFixture(); PrepareSettler(f.Units);
            var incidents = new IncidentFixture(f.Units.Lifecycle.Random, f.Units.Data);
            var context = OutcomeContext(f.Units.Data); using var op = new CityProductionOperation(
                f.Production.Begin(new(0), "production", f.Units.Lifecycle.Elimination, incidents.Incidents), c => new((ulong)c));
            Equal(new ParticipantId(1), op.Resume(context, new()).Interaction!.Recipient!.Value);
            var done = op.Resume(context, new(kind)); Equal(CommandStatus.Completed, done.Status);
            Equal(UnitProductionOutcome.SettlerCancelled, op.Result!.Unit!.Outcome); Equal((short)7, f.City.ShieldBox);
            Equal(done, op.Resume(context, new())); Equal((uint)2, f.Units.Data.Process.Tokens.NextUnit);
        }
        var (disposed, a, i, _) = RetainedProductionFixture();
        using var abandoned = disposed.Production.Begin(new(0), "production", a.Lifecycle.Elimination, i.Incidents);
        abandoned.Resume(); abandoned.Resume(new(1)); abandoned.Dispose();
        Throws<ObjectDisposedException>(() => abandoned.Resume(new(0)));
        Equal(true, disposed.City.Token.Occupied); Equal((sbyte)0, disposed.City.Size); Equal(0, disposed.Units.BuiltTypes.Count);
        var research = new CityProductionFixture(); research.Research.Enable(0, 1, 2); research.World.Civilizations[1].Researching = 0;
        var effects = new List<string>(); var ctx = OutcomeContext(research.Units.Data, effects);
        using var darwin = new CityProductionOperation(research.Start(57), c => new((ulong)c));
        Equal(new ParticipantId(1), darwin.Resume(ctx, new()).Interaction!.Recipient!.Value);
        Equal(CommandStatus.AwaitingInteraction, darwin.Resume(ctx, new(OperationInputKind.Choice, 1)).Status);
        Equal(CommandStatus.Completed, darwin.Resume(ctx, new(OperationInputKind.Choice, 2)).Status);
        Sequence(new[] { 0, 1 }, research.Research.Completions.Select(n => n.Advance));
        Equal(1, effects.Count(e => e == "city.production.completed"));
    }

    private static void ProductionRetainedPrivateSession()
    {
        using var runtime = new SessionRuntime(new(), new(267));
        var (f, a, incidents, restart) = RetainedProductionFixture(CreationFixture(new(runtime.Process, runtime.World)));
        var recipients = new Dictionary<int, ParticipantId>();
        runtime.RegisterCommand(new(new(1, "test.production", "Production", AccessClass.Player, new([]), new([])),
            (_, _, _) => new(), _ => new CityProductionOperation(
                f.Production.Begin(new(0), "production", a.Lifecycle.Elimination, incidents.Incidents), c => recipients[c])));
        runtime.BeginClientSession(new()); var actor = SessionClient(runtime, 1); var owner = SessionClient(runtime, 4); var victim = SessionClient(runtime, 2);
        recipients[4] = owner.Identity.Participant; recipients[2] = victim.Identity.Participant;
        var command = actor.Submit(new("test.production", new([]))).Correlation.Command; runtime.AdvanceOne();
        var first = RequiredInteraction(owner, command); Equal(null, RequiredResult(actor, command).Interaction);
        Equal(false, victim.Reply(first, 1).Accepted); Equal(true, owner.Reply(first, 1).Accepted); runtime.AdvanceOne();
        var second = RequiredInteraction(victim, command); Equal(false, owner.Reply(second, 1).Accepted);
        Equal(0, f.Units.Removed.Count); Equal(true, victim.Reply(second, 1).Accepted); runtime.AdvanceOne();
        Equal(CommandStatus.Completed, RequiredResult(actor, command).Status); Equal(false, victim.Reply(second, 1).Accepted);
        Equal(true, f.World.Civilizations[3].Treaties[2].War); Equal(1, f.Units.Removed.Count);
        Equal(1, RequiredResult(actor, command).Committed.Count(e => e.Name == "city.production.completed"));
        a.Random.RequireChoicesConsumed(); restart.RequireChoicesConsumed();
    }
}
