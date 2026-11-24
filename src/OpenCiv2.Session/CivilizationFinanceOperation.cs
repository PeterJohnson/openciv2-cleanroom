using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;

namespace OpenCiv2.Session;

/// <summary>V02 §11 presentation and actual rate child after F04 assessment. Explicit civilization
/// and recipient routing belong to a trusted parent; the legacy entry keeps player-phase admission.</summary>
public sealed class CivilizationFinanceOperation(IPreferenceWriter preferences, RateDecisionPreferences choices, int startGold,
    int? civilization = null, Func<int, ParticipantId>? recipient = null) : IContinuation
{
    private CivilizationFinances.Operation? operation;
    private CivilizationNotice? shown;
    private CivilizationRateDecision? rates;
    private string failure = "";
    private OperationStep? completed;
    private bool disposed, faulted;

    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (faulted)
        { throw new InvalidOperationException("A failed financial notice cannot resume"); }
        if (completed is not null)
        { return completed; }
        try
        {
            var result = ResumeStep(context, input);
            if (result.Status.Terminal())
            { completed = result; }
            return result;
        }
        catch { faulted = true; rates?.Dispose(); context.Publish(); throw; }
    }

    private OperationStep ResumeStep(OperationContext context, OperationInput input)
    {
        if (civilization.HasValue != (recipient is not null))
        { throw new ArgumentException("An explicit finance civilization and recipient resolver must be supplied together"); }
        string? error = civilization is null ? LocalPlayCommands.ValidatePhase(context.Actor, context.World, false)
            : context.World.Globals.Multiplayer != GameMode.SinglePlayer ? "single_player_required" : null;
        if (error is not null)
        {
            return new(CommandStatus.Failed, Code: error);
        }

        int owner = civilization ?? context.Actor.Civilization.Value;
        if (owner < 0 || owner >= EntityLimits.Civilizations)
        { throw new ArgumentOutOfRangeException(nameof(civilization)); }
        operation ??= new(context.World, owner, startGold);
        if (rates is not null)
        {
            var result = rates.Resume(context, input);
            if (!result.Status.Terminal())
            {
                return result;
            }

            if (result.Status == CommandStatus.Failed)
            {
                failure = result.Code;
            }

            rates.Dispose();
            rates = null;
            operation.Reply(0);
        }
        else if (shown is not null)
        {
            if (input.Kind != OperationInputKind.Choice || input.Choice is not int value
                || (shown.Kind == CivilizationNoticeKind.Population ? value != 0 : value is not (0 or 1)))
            {
                return Decision(shown, owner);
            }

            operation.Reply(value);
            shown = null;
        }
        int oldMilestone = context.World.Civilizations[owner].PopulationMilestone;
        var next = operation.Next();
        if (oldMilestone != context.World.Civilizations[owner].PopulationMilestone)
        {
            context.Effect("civilization.population.milestone", new([new("milestone", new CommandValue.Integer(context.World.Civilizations[owner].PopulationMilestone))]));
        }

        if (next is null)
        {
            return new(failure.Length == 0 ? CommandStatus.Completed : CommandStatus.Failed, Code: failure);
        }

        if (next.Kind == CivilizationNoticeKind.Rates)
        {
            rates = civilization is null ? new(preferences, choices, duringTurn: true)
                : new(preferences, choices, civilization: owner, recipient: recipient!(owner));
            return rates.Resume(context, new());
        }
        shown = next;
        context.Effect(next.Kind == CivilizationNoticeKind.Population ? "civilization.population.notice" : "civilization.funds.notice", OwnedList<NamedValue>.Empty);
        return Decision(next, owner);
    }

    private OperationStep Decision(CivilizationNotice notice, int owner)
    {
        bool population = notice.Kind == CivilizationNoticeKind.Population;
        InteractionOption[] options = population ? [new(0, "Continue")] : [new(0, "Keep rates"), new(1, "Edit rates")];
        return new(CommandStatus.AwaitingInteraction, Interaction: new(
            population ? $"Your civilization has reached {notice.Population * 10000:N0} people."
                : "Your treasury is running low. Adjust the tax rate?", recipient?.Invoke(owner), null,
            new(options.Select(o => o.Value)), Cancellable: false)
        { Options = new(options) });
    }
    public void Dispose() { disposed = true; rates?.Dispose(); }
}
