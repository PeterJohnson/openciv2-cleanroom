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
    // F04 §6/V02 §§5–6: actual government/rate children precede retained V04/A08 maintenance.
    private static CivilizationPoliticsOperation Politics(ContactTurnFixture f, int owner = 1, IPreferenceWriter? preferences = null,
        Func<int, ParticipantId>? recipient = null) => new(owner, f.Service, preferences ?? new BufferedPreferenceWriter(), new(),
            recipient ?? (c => new((ulong)c)), _ => throw new InvalidOperationException("No intelligence in this case"), "turn");
    private static OperationContext PoliticsContext(ContactTurnFixture f, List<string>? effects = null) =>
        OutcomeContext(f.Data, effects) with { Actor = new(new(2), new(2)) };
    private static void PoliticsHumanPrefixAndRates()
    {
        foreach (bool failOutput in new[] { false, true })
        {
            var f = new ContactTurnFixture(data: WorkingFixture()); var effects = new List<string>(); var context = PoliticsContext(f, effects);
            var w = f.World; w.Globals.Turn = (short)(failOutput ? 4 : 12); w.Globals.TurnCiv = 2; w.PlayerPhaseActive = false;
            w.Civilizations[1].Government = Government.Anarchy; w.Civilizations[1].Flags.Revolution = false;
            w.Civilizations[1].Flags.GovernmentChanged = true; w.Civilizations[1].DiploRequestCount = 7;
            w.Civilizations[1].TaxRate = 4; w.Civilizations[1].ScienceRate = 6;
            w.Civilizations[1].Treaties[3].ChangedThisTurn = true;
            var opaque = new RecordCompatibility(w.Lifetime); opaque.Civilizations[1] = new() { Flags = 0x8040 }; opaque.Attach(w.Lifetime);
            using var op = Politics(f, preferences: failOutput ? new FailedRatePreferences() : new BufferedPreferenceWriter());
            var menu = op.Resume(context, new()); Equal(CommandStatus.AwaitingInteraction, menu.Status);
            Equal(new ParticipantId(1), menu.Interaction!.Recipient!.Value); Equal((ushort)0x8000, opaque.Civilizations[1].Flags);
            Equal(Government.Anarchy, w.Civilizations[1].Government); Equal((sbyte)7, w.Civilizations[1].DiploRequestCount);
            var rates = op.Resume(context, new(OperationInputKind.Choice, 1)); Equal(CommandStatus.AwaitingInteraction, rates.Status);
            Equal(new ParticipantId(1), rates.Interaction!.Recipient!.Value); Equal(Government.Despotism, w.Civilizations[1].Government);
            Equal((sbyte)7, w.Civilizations[1].DiploRequestCount); Equal(true, w.Civilizations[1].Treaties[3].ChangedThisTurn);
            var result = op.Resume(context, new(OperationInputKind.Choice, 1));
            Equal(failOutput ? CommandStatus.Failed : CommandStatus.Completed, result.Status);
            Equal(failOutput ? "preferences_output_failed" : "", result.Code);
            // Request aging is every third turn; only the early-turn case persists preferences.
            Equal((sbyte)(failOutput ? 7 : 6), w.Civilizations[1].DiploRequestCount);
            Equal(false, w.Civilizations[1].Treaties[3].ChangedThisTurn);
            Equal(true, effects.IndexOf("civilization.government.established") < effects.IndexOf("diplomacy.turn.completed"));
            int count = effects.Count; Equal(result, op.Resume(context, new())); Equal(count, effects.Count);
            f.F.F.T.T.Random.RequireChoicesConsumed();
        }
    }
    private static void GovernmentSelectionCapturedChoices()
    {
        var f = WorkingFixture(); var effects = new List<string>(); var context = OutcomeContext(f, effects) with { Actor = new(new(2), new(2)) };
        f.World.Civilizations[1].Government = Government.Despotism;
        f.World.Civilizations[1].Advances = default(AdvanceSet).With(AdvanceIds.Monarchy);
        using var restricted = new GovernmentSelectionOperation(1, new(1), new BufferedPreferenceWriter(), new());
        var offered = restricted.Resume(context, new()); Equal(true, offered.Interaction!.Choices.Contains(2));
        f.World.Civilizations[1].Advances = default; // The offered identity is not revalidated at acceptance.
        var rates = restricted.Resume(context, new(OperationInputKind.Choice, 2)); Equal(CommandStatus.AwaitingInteraction, rates.Status);
        Equal(Government.Monarchy, f.World.Civilizations[1].Government);
        Equal(CommandStatus.Completed, restricted.Resume(context, new(OperationInputKind.Cancel)).Status);
        Equal(1, effects.Count(e => e == "civilization.government.established"));
        using var all = new GovernmentSelectionOperation(1, new(1), new BufferedPreferenceWriter(), new(), allGovernments: true);
        Sequence(Enumerable.Range(0, 7), all.Resume(context, new()).Interaction!.Choices);
        Equal(CommandStatus.Completed, all.Resume(context, new(OperationInputKind.Choice, 0)).Status);
        Equal(Government.Anarchy, f.World.Civilizations[1].Government);
        using var nonhuman = new GovernmentSelectionOperation(2, new(2), new BufferedPreferenceWriter(), new(), allGovernments: true);
        int before = effects.Count; Equal(CommandStatus.Completed, nonhuman.Resume(context, new()).Status); Equal(before, effects.Count);
        using var tutorial = new GovernmentSelectionOperation(1, new(1), new BufferedPreferenceWriter(), new(), allGovernments: true);
        f.World.Globals.GameOptions.Tutorial = true; f.World.Globals.ShownEventFlags.Government = false;
        tutorial.Resume(context, new()); Equal(CommandStatus.AwaitingInteraction, tutorial.Resume(context, new(OperationInputKind.Choice, 5)).Status);
        Equal(false, f.World.Globals.ShownEventFlags.Government);
        Equal(CommandStatus.Completed, tutorial.Resume(context, new(OperationInputKind.Timeout)).Status);
        Equal(true, f.World.Globals.ShownEventFlags.Government);
    }
    private static void RateDecisionExplicitCivilization()
    {
        var f = WorkingFixture(); var effects = new List<string>(); var context = OutcomeContext(f, effects) with { Actor = new(new(2), new(2)) };
        f.World.Globals.TurnCiv = 2; f.World.PlayerPhaseActive = false;
        f.World.Civilizations[1].Government = Government.Despotism; f.World.Civilizations[1].TaxRate = 4; f.World.Civilizations[1].ScienceRate = 6;
        f.World.Civilizations[2].TaxRate = 2; f.World.Civilizations[2].ScienceRate = 3;
        var writer = new BufferedPreferenceWriter();
        using var op = new CivilizationRateDecision(writer, new(), civilization: 1, recipient: new(1));
        var decision = op.Resume(context, new()); Equal(new ParticipantId(1), decision.Interaction!.Recipient!.Value);
        op.Resume(context, new(OperationInputKind.Choice, 2)); op.Resume(context, new(OperationInputKind.Choice, 6));
        var result = op.Resume(context, new(OperationInputKind.Choice, 1)); Equal(CommandStatus.Completed, result.Status);
        Equal((byte)6, f.World.Civilizations[1].TaxRate); Equal((byte)4, f.World.Civilizations[1].ScienceRate);
        Equal((byte)2, f.World.Civilizations[2].TaxRate); Equal((byte)3, f.World.Civilizations[2].ScienceRate);
        Equal((short)6, ClassicPreferences.DecodeOrdinary(writer.Capture().AsSpan(0, ClassicPreferences.OrdinarySize)).Tax);
        Equal(result, op.Resume(context, new(OperationInputKind.Choice, 1))); Equal(1, effects.Count(e => e == "civilization.rates"));
        using var changed = new CivilizationRateDecision(writer, new(), civilization: 1, recipient: new(1));
        changed.Resume(context, new()); f.World.Civilizations[1].ScienceRate = 2;
        Equal("rates_changed", changed.Resume(context, new(OperationInputKind.Choice, 1)).Code);
        using var unrouted = new CivilizationRateDecision(writer, new(), civilization: 1);
        Throws<ArgumentException>(() => unrouted.Resume(context, new()));
        Equal((byte)2, f.World.Civilizations[1].ScienceRate);
    }
    private static void PoliticsAiAndRetainedLibrary()
    {
        var ai = new ContactTurnFixture(); var effects = new List<string>(); var context = PoliticsContext(ai, effects);
        ai.World.Globals.Turn = 4; ai.World.Globals.CivsAlive = CivilizationSet.Only(2);
        ai.World.Civilizations[2].Government = Government.Anarchy; ai.World.Civilizations[2].Flags.Revolution = false;
        ai.World.Civilizations[1].Treaties[2].Embassy = true;
        using (var op = Politics(ai, 2)) { Equal(CommandStatus.Completed, op.Resume(context, new()).Status); }
        Equal(Government.Despotism, ai.World.Civilizations[2].Government); Equal(false, ai.World.Civilizations[2].Flags.Revolution);
        Equal(2, effects.Count(e => e == "civilization.government.announced")); ai.F.F.T.T.Random.RequireChoicesConsumed();
        foreach (bool dispose in new[] { false, true })
        {
            var f = new ContactTurnFixture(TurnContactLibraryChoices()); f.Library(); var local = PoliticsContext(f);
            f.World.Civilizations[2].Flags.GovernmentChanged = true; f.World.Civilizations[2].Treaties[7].ChangedThisTurn = true;
            var op = Politics(f, 2); var decision = op.Resume(local, new()); Equal(CommandStatus.AwaitingInteraction, decision.Status);
            Equal(new ParticipantId(1), decision.Interaction!.Recipient!.Value); Equal(false, f.World.Civilizations[2].Flags.GovernmentChanged);
            Equal(true, f.World.Civilizations[2].Treaties[7].ChangedThisTurn);
            if (dispose)
            {
                op.Dispose(); Throws<ObjectDisposedException>(() => op.Resume(local, new(OperationInputKind.Choice, 0)));
                Equal(false, f.World.Civilizations[6].Advances.Contains(94));
            }
            else
            {
                Equal(CommandStatus.Completed, op.Resume(local, new(OperationInputKind.Choice, 0)).Status);
                Equal(true, f.World.Civilizations[6].Advances.Contains(94)); Equal(false, f.World.Civilizations[2].Treaties[7].ChangedThisTurn); op.Dispose();
            }
            f.F.F.T.T.Random.RequireChoicesConsumed();
        }
    }
    private static void PoliticsCancellationAndFailure()
    {
        foreach (var input in new[] { OperationInputKind.Cancel, OperationInputKind.Timeout })
        {
            var f = new ContactTurnFixture(data: WorkingFixture()); var context = PoliticsContext(f);
            f.World.Globals.Turn = 12; f.World.Civilizations[1].Government = Government.Anarchy;
            f.World.Civilizations[1].Flags.Revolution = false;
            using var op = Politics(f); Equal(CommandStatus.AwaitingInteraction, op.Resume(context, new()).Status);
            Equal(CommandStatus.Completed, op.Resume(context, new(input)).Status);
            Equal(Government.Anarchy, f.World.Civilizations[1].Government); Equal((byte)42, f.World.Civilizations[1].DiploRandom);
            f.F.F.T.T.Random.RequireChoicesConsumed();
        }
        var stopped = new ContactTurnFixture(data: WorkingFixture()); var stopContext = PoliticsContext(stopped);
        stopped.World.Globals.Turn = 12; stopped.World.Civilizations[1].Government = Government.Anarchy;
        stopped.World.Civilizations[1].Flags.Revolution = false;
        var disposal = Politics(stopped); disposal.Resume(stopContext, new()); disposal.Resume(stopContext, new(OperationInputKind.Choice, 1));
        disposal.Dispose(); Throws<ObjectDisposedException>(() => disposal.Resume(stopContext, new(OperationInputKind.Choice, 1)));
        Equal(Government.Despotism, stopped.World.Civilizations[1].Government); Equal((byte)0, stopped.World.Civilizations[1].DiploRandom);
        var remote = new ContactTurnFixture(); remote.World.Globals.Multiplayer = GameMode.Tcp;
        using var rejected = Politics(remote); Throws<InvalidOperationException>(() => rejected.Resume(PoliticsContext(remote), new()));
        remote.World.Globals.Multiplayer = GameMode.SinglePlayer; Throws<InvalidOperationException>(() => rejected.Resume(PoliticsContext(remote), new()));
    }
    private static void PoliticsPrivateSession()
    {
        using var runtime = new SessionRuntime(new(), new(259)); var f = new ContactTurnFixture(data: new(runtime.Process, runtime.World));
        f.World.Globals.Turn = 12; f.World.Globals.TurnCiv = 2; f.World.Civilizations[1].Government = Government.Anarchy;
        f.World.Civilizations[1].Flags.Revolution = false;
        ParticipantId recipient = default;
        runtime.RegisterCommand(new(new(1, "test.politics", "Politics turn", AccessClass.Player, new([]), new([])),
            (_, _, _) => new(), _ => Politics(f, recipient: _ => recipient)));
        runtime.BeginClientSession(new()); var actor = SessionClient(runtime, 2); var human = SessionClient(runtime, 1); recipient = human.Identity.Participant;
        var command = actor.Submit(new("test.politics", new([]))).Correlation.Command; runtime.AdvanceOne();
        var government = RequiredInteraction(human, command); Equal(false, actor.Reply(government, 1).Accepted);
        Equal(null, RequiredResult(actor, command).Interaction); Equal(true, human.Reply(government, 1).Accepted); runtime.AdvanceOne();
        Equal(false, human.Reply(government, 1).Accepted); var rates = RequiredInteraction(human, command);
        Equal(false, actor.Reply(rates, 1).Accepted); Equal(null, RequiredResult(actor, command).Interaction);
        Equal(true, human.Reply(rates, 1).Accepted); runtime.AdvanceOne();
        Equal(CommandStatus.Completed, RequiredResult(actor, command).Status); Equal(false, human.Reply(rates, 1).Accepted);
        f.F.F.T.T.Random.RequireChoicesConsumed();
    }
}
