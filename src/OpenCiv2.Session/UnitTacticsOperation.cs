using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Session;

/// <summary>Private choices and text from actual A04 children. The selected orders are
/// returned to the owning turn pass for U04 execution.</summary>
public sealed class UnitTacticsOperation(UnitTactics.Operation operation, Func<int, ParticipantId> recipient,
    Func<CaptureCityVisit, InteractionSpec> window, Func<EmissaryIntelligence, InteractionSpec>? intelligence = null) : IContinuation
{
    private UnitTacticsDecision? pending;
    private readonly UnitTacticsInteraction interaction = new(recipient, window, intelligence);
    private OperationStep? completed;
    private bool faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        { throw new InvalidOperationException("Failed unit tactics presentation cannot resume"); }
        if (completed is not null)
        { return completed; }
        try
        {
            var answer = interaction.Reply(pending, input);
            pending = operation.Resume(answer);
            if (pending is not null)
            {
                context.Publish();
                var prompt = interaction.Prompt(context, pending);
                return new(CommandStatus.AwaitingInteraction, Interaction: prompt);
            }
            if (operation.Result is null)
            { context.Publish(); return new(CommandStatus.Pending) { ReadyToContinue = true }; }
            var result = new OwnedList<NamedValue>([new("result", new CommandValue.Integer(operation.Result.Value))]);
            context.Effect("ai.unit-tactics.completed", result);
            return completed = new(Result: result);
        }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    public void Dispose() => operation.Dispose();
}

internal sealed class UnitTacticsInteraction(Func<int, ParticipantId> recipient, Func<CaptureCityVisit, InteractionSpec> window,
    Func<EmissaryIntelligence, InteractionSpec>? intelligence)
{
    private InteractionSpec? visit, intelligenceWindow;
    internal UnitTacticsReply? Reply(UnitTacticsDecision? pending, OperationInput input) => pending switch
    {
        UnitTacticsDecision.Barbarian barb => BarbarianOrderInteraction.Reply(barb.Decision, input) is { } b ? new(Research: b) : null,
        UnitTacticsDecision.Lifecycle life => LifecycleInteraction.ResearchReply(life.Decision, input) is { } l ? new(Research: l) : null,
        UnitTacticsDecision.Founding found => CityFoundingInteraction.Reply(found.Decision, input) is { } f ? new(Founding: f) : null,
        UnitTacticsDecision.Mission mission => UnitMissionInteraction.Reply(mission.Decision, input) is { } m ? new(Research: m) : null,
        UnitTacticsDecision.Pillage => AttackConfirmationInteraction.Reply(input) is { } p ? new(p) : null,
        UnitTacticsDecision.Paradrop drop => DropReply(drop.Decision, input),
        _ => null
    };
    internal InteractionSpec Prompt(OperationContext context, UnitTacticsDecision pending) => pending switch
    {
        UnitTacticsDecision.Barbarian barb => BarbarianOrderInteraction.Prompt(context, barb.Decision, recipient, ref intelligenceWindow, intelligence),
        UnitTacticsDecision.Lifecycle life => LifecycleInteraction.Prompt(context, life.Decision, recipient, ref intelligenceWindow, intelligence),
        UnitTacticsDecision.Founding found => CityFoundingInteraction.Prompt(context, found.Decision, recipient, window, ref visit, ref intelligenceWindow, intelligence),
        UnitTacticsDecision.Mission mission => UnitMissionInteraction.Prompt(context, mission.Decision, recipient, ref intelligenceWindow, intelligence),
        UnitTacticsDecision.Pillage pillage => AttackConfirmationInteraction.Prompt(pillage.Warning, recipient(pillage.Warning.Actor.Civilization)),
        UnitTacticsDecision.Paradrop { Decision: ParadropDecision.Attack attack } =>
            AttackConfirmationInteraction.Prompt(attack.Warning, recipient(attack.Warning.Actor.Civilization)),
        UnitTacticsDecision.Paradrop { Decision: ParadropDecision.Capture capture } =>
            CityCaptureInteraction.Prompt(context, capture.Decision, recipient, window, ref visit, ref intelligenceWindow, intelligence),
        _ => throw new InvalidOperationException("Unknown unit tactics decision")
    };
    private static UnitTacticsReply? DropReply(ParadropDecision pending, OperationInput input)
    {
        ResearchReply? reply = pending is ParadropDecision.Capture capture ? CityCaptureInteraction.Reply(capture.Decision, input)
            : AttackConfirmationInteraction.Reply(input) is { } answer ? new(answer) : null;
        return reply is { } supplied ? new(Research: supplied) : null;
    }
}
