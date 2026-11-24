using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Formats;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private sealed class PostCityFixture
    {
        public AssessmentFixture A { get; }
        public ContactTurnFixture D { get; }
        public IncidentFixture I { get; }
        public GameState World => A.World;
        public PostCityFixture(RevelationFixture? data = null)
        {
            A = new(data ?? WorkingFixture()); D = new(data: A.Data); I = new(A.Random, A.Data);
            for (int t = 0; t < A.Rules.Terrain.Length; ++t)
                A.Rules.Terrain[t] = A.Rules.Terrain[t] with
                { Food = 2, Shields = 1, Trade = 1, Work = new(new(TerrainResult.ImproveInPlace, 1), new(TerrainResult.ImproveInPlace, 2)) };
            A.Data.AddCity(0, 1, new(8, 6)); A.Data.AddCity(1, 1, new(14, 6));
            World.Globals.HumanPlayers = CivilizationSet.Only(1);
            World.Globals.Turn = 4; World.Globals.TurnCiv = 2; World.TurnLoopActive = true; World.PlayerPhaseActive = false;
            A.Civ.Government = Government.Anarchy; A.Civ.Flags.Revolution = false;
            A.Civ.CityCount = 77; A.Civ.TotalCitySize = 99; A.Civ.Gold = 90; A.Civ.PopulationMilestone = 0;
            A.Civ.TaxRate = 4; A.Civ.ScienceRate = 6;
            World.Civilizations[2].TaxRate = 2; World.Civilizations[2].ScienceRate = 3;
        }
        public CivilizationPostCityOperation Begin(IPreferenceWriter? writer = null, Func<int, ParticipantId>? recipient = null) =>
            new(1, 100, D.Service, A.Assessment, I.Incidents, writer ?? new BufferedPreferenceWriter(), new(),
                recipient ?? (c => new((ulong)c)), _ => throw new InvalidOperationException("No intelligence in this case"), "turn", "assessment");
        public OperationContext Context(List<string>? effects = null) =>
            OutcomeContext(A.Data, effects) with { Actor = new(new(2), new(2)) };
        public void Done() { D.F.F.T.T.Random.RequireChoicesConsumed(); A.Random.RequireChoicesConsumed(); }
    }

    // F04 §6 steps 5–7: actual government/rates, diplomacy, census and financial children.
    private static void PostCityHumanOrderAndFailure()
    {
        foreach (bool fail in new[] { false, true })
        {
            var f = new PostCityFixture(); var effects = new List<string>(); var context = f.Context(effects);
            var writer = new BufferedPreferenceWriter();
            using var op = f.Begin(fail ? new FailedRatePreferences() : writer);
            OperationStep Reply(int value) => op.Resume(context, new(OperationInputKind.Choice, value));
            Equal(new ParticipantId(1), op.Resume(context, new()).Interaction!.Recipient!.Value);
            Equal((short)77, f.A.Civ.CityCount); Equal((byte)0, f.A.Civ.DiploRandom);
            Equal(CommandStatus.AwaitingInteraction, Reply(1).Status); // Government accepted; rates still pending.
            Equal((short)77, f.A.Civ.CityCount); Equal((short)0, f.A.Civ.PopulationMilestone);
            var population = Reply(1); Equal(false, population.Interaction!.Cancellable);
            Equal((short)2, f.A.Civ.CityCount); Equal((short)8, f.A.Civ.TotalCitySize);
            Equal((short)2, f.A.Civ.PopulationMilestone); Equal((byte)42, f.A.Civ.DiploRandom);
            Equal(true, effects.IndexOf("civilization.government.established") < effects.IndexOf("diplomacy.turn.completed"));
            Equal(true, effects.IndexOf("diplomacy.turn.completed") < effects.IndexOf("civilization.assessment.completed"));
            Equal(true, effects.IndexOf("civilization.assessment.completed") < effects.IndexOf("civilization.population.notice"));
            Equal(true, Reply(0).Interaction!.Prompt.Contains("treasury", StringComparison.Ordinal));
            Equal(new ParticipantId(1), Reply(1).Interaction!.Recipient!.Value);
            Reply(2); Reply(6); var result = Reply(1);
            Equal(fail ? CommandStatus.Failed : CommandStatus.Completed, result.Status);
            Equal(fail ? "preferences_output_failed" : "", result.Code);
            Equal((byte)6, f.A.Civ.TaxRate); Equal((byte)4, f.A.Civ.ScienceRate);
            Equal((byte)2, f.World.Civilizations[2].TaxRate); Equal((byte)3, f.World.Civilizations[2].ScienceRate);
            if (!fail) Equal((short)6, ClassicPreferences.DecodeOrdinary(writer.Capture().AsSpan(0, ClassicPreferences.OrdinarySize)).Tax);
            int count = effects.Count; Equal(result, op.Resume(context, new())); Equal(count, effects.Count); f.Done();
        }
    }

    private static void PostCityAiLibraryBeforeAssessment()
    {
        var a = new AssessmentFixture(); var d = new ContactTurnFixture(TurnContactLibraryChoices(), a.Data); d.Library();
        var i = new IncidentFixture(a.Random, a.Data); a.World.Globals.Turn = 16;
        a.Rule(0, RuleRole.Settle); a.Data.AddUnit(0, 2, 0, new(20, 10));
        a.World.Civilizations[2].CityCount = 77; a.World.Civilizations[2].PopulationMilestone = -1;
        var effects = new List<string>(); var context = OutcomeContext(a.Data, effects);
        using var op = new CivilizationPostCityOperation(2, 100, d.Service, a.Assessment, i.Incidents,
            new BufferedPreferenceWriter(), new(), c => c == 1 ? new(1) : throw new InvalidOperationException("No AI participant"),
            _ => throw new InvalidOperationException("No intelligence"), "turn", "assessment");
        Equal(new ParticipantId(1), op.Resume(context, new()).Interaction!.Recipient!.Value);
        Equal((short)77, a.World.Civilizations[2].CityCount); Equal((short)-1, a.World.Civilizations[2].PopulationMilestone);
        Equal(false, effects.Contains("civilization.assessment.completed"));
        Equal(CommandStatus.Completed, op.Resume(context, new(OperationInputKind.Choice, 0)).Status);
        Equal((short)0, a.World.Civilizations[2].CityCount); Equal((byte)1, a.World.Civilizations[2].UnitsActive[0]);
        Equal((short)0, a.World.Civilizations[2].PopulationMilestone);
        Equal(false, effects.Contains("civilization.population.notice"));
        d.F.F.T.T.Random.RequireChoicesConsumed(); a.Random.RequireChoicesConsumed();
    }

    private static void PostCityAssessmentBeforeFinance()
    {
        var d = new ContactTurnFixture(data: CreationFixture());
        var (a, i, restart) = RetainedAssessmentFixture(false, d.Data);
        // The contact fixture has foreign cities; this lifecycle case requires the human spy
        // to be its owner's last settler with no surviving city.
        a.World.Entities.Cities.SetEnd(0);
        foreach (ref var tile in a.World.Map.Tiles) { tile.Features &= ~MapFeatures.CitySite; }
        for (int wonder = 0; wonder < RuleLimits.Wonders; ++wonder) { a.World.Globals.WonderCity[wonder] = -1; }
        a.Civ.PopulationMilestone = -1;
        a.Civ.Treaties[2].Contact = false; // This case does not enter A08's unresolved incoming-delta path.
        var effects = new List<string>(); var context = OutcomeContext(a.Data, effects); a.World.Multiplayer.LocalHuman = 2;
        using var op = new CivilizationPostCityOperation(1, 100, d.Service, a.Assessment, i.Incidents,
            new BufferedPreferenceWriter(), new(), c => new((ulong)c), _ => throw new InvalidOperationException("No intelligence"),
            "turn", "assessment");
        Equal(new ParticipantId(2), op.Resume(context, new()).Interaction!.Recipient!.Value);
        Equal(true, effects.Contains("diplomacy.turn.completed")); Equal(false, effects.Contains("civilization.assessment.completed"));
        Equal((short)-1, a.Civ.PopulationMilestone);
        a.World.TurnLoopActive = false;
        Equal(CommandStatus.Completed, op.Resume(context, new(OperationInputKind.Choice, 0)).Status);
        Equal((short)0, a.Civ.PopulationMilestone); Equal(true, effects.Contains("civilization.assessment.completed"));
        Equal(false, effects.Contains("civilization.population.notice"));
        d.F.F.T.T.Random.RequireChoicesConsumed(); a.Random.RequireChoicesConsumed(); restart.RequireChoicesConsumed();
    }

    private static void FinanceExplicitTargetAndLiveGates()
    {
        foreach (int gate in Enumerable.Range(0, 5))
        {
            var f = new PostCityFixture(); f.A.Civ.Government = Government.Despotism;
            var context = f.Context(); using var op = new CivilizationFinanceOperation(new BufferedPreferenceWriter(), new(), 100, 1, _ => new(1));
            Equal(new ParticipantId(1), op.Resume(context, new()).Interaction!.Recipient!.Value);
            Equal(CommandStatus.AwaitingInteraction, op.Resume(context, new(OperationInputKind.Cancel)).Status);
            switch (gate)
            {
                case 0: f.World.Globals.HumanPlayers = default; break;
                case 1: f.World.Multiplayer.Saved.AutoplayActive = true; break;
                case 2: f.World.TurnLoopActive = false; break;
                case 3: f.A.Civ.CityCount = 1; break;
                case 4: f.A.Civ.Gold = 100; break;
            }
            Equal(CommandStatus.Completed, op.Resume(context, new(OperationInputKind.Choice, 0)).Status);
            Equal((short)2, f.A.Civ.PopulationMilestone);
        }
        var invalid = new PostCityFixture(); using var missing = new CivilizationFinanceOperation(new BufferedPreferenceWriter(), new(), 100, civilization: 1);
        Throws<ArgumentException>(() => missing.Resume(invalid.Context(), new())); Equal((short)0, invalid.A.Civ.PopulationMilestone);
        foreach (var input in new[] { OperationInputKind.Cancel, OperationInputKind.Timeout })
        {
            var f = new PostCityFixture(); f.A.Civ.Government = Government.Despotism; f.A.Civ.PopulationMilestone = 100;
            using var op = new CivilizationFinanceOperation(new BufferedPreferenceWriter(), new(), 100, 1, _ => new(1));
            var context = f.Context(); op.Resume(context, new()); op.Resume(context, new(OperationInputKind.Choice, 1));
            var result = op.Resume(context, new(input)); Equal(CommandStatus.Completed, result.Status);
            Equal(result, op.Resume(context, new(OperationInputKind.Choice, 1)));
        }
    }

    private static void PostCityDisposalAndRequiredFailure()
    {
        int[] replies = [1, 1, 0, 1];
        for (int stage = 0; stage <= replies.Length; ++stage)
        {
            var f = new PostCityFixture(); var effects = new List<string>(); var context = f.Context(effects); var op = f.Begin();
            Equal(CommandStatus.AwaitingInteraction, op.Resume(context, new()).Status);
            for (int step = 0; step < stage; ++step)
                Equal(CommandStatus.AwaitingInteraction, op.Resume(context, new(OperationInputKind.Choice, replies[step])).Status);
            int count = effects.Count; op.Dispose(); Throws<ObjectDisposedException>(() => op.Resume(context, new(OperationInputKind.Choice, 1)));
            Equal(count, effects.Count);
            Equal(stage > 0 ? Government.Despotism : Government.Anarchy, f.A.Civ.Government);
            Equal(stage >= 2, effects.Contains("civilization.assessment.completed"));
        }
        var stopped = new PostCityFixture(); stopped.A.Civ.Government = Government.Despotism;
        using var fault = stopped.Begin(recipient: _ => throw new InvalidOperationException("Seat unavailable"));
        Throws<InvalidOperationException>(() => fault.Resume(stopped.Context(), new()));
        Equal((short)2, stopped.A.Civ.CityCount); Equal((short)2, stopped.A.Civ.PopulationMilestone);
        Throws<InvalidOperationException>(() => fault.Resume(stopped.Context(), new())); stopped.Done();
    }

    private static void PostCityPrivateSession()
    {
        using var runtime = new SessionRuntime(new(), new(261)); var f = new PostCityFixture(new(runtime.Process, runtime.World));
        ParticipantId recipient = default;
        runtime.RegisterCommand(new(new(1, "test.post-city", "Post-city upkeep", AccessClass.Player, new([]), new([])),
            (_, _, _) => new(), _ => f.Begin(recipient: _ => recipient)));
        runtime.BeginClientSession(new()); var actor = SessionClient(runtime, 2); var human = SessionClient(runtime, 1); recipient = human.Identity.Participant;
        var command = actor.Submit(new("test.post-city", new([]))).Correlation.Command; runtime.AdvanceOne();
        int step = 0;
        foreach (int answer in new[] { 1, 1, 0, 1, 1 })
        {
            var status = RequiredResult(actor, command);
            if (status.Status != CommandStatus.AwaitingInteraction)
                throw new InvalidOperationException($"Private post-city step {step}: {status.Status}/{status.Code}");
            var decision = RequiredInteraction(human, command); Equal(null, RequiredResult(actor, command).Interaction);
            Equal(false, actor.Reply(decision, answer).Accepted); Equal(true, human.Reply(decision, answer).Accepted); runtime.AdvanceOne();
            Equal(false, human.Reply(decision, answer).Accepted);
            ++step;
        }
        Equal(CommandStatus.Completed, RequiredResult(actor, command).Status); f.Done();
    }
}
