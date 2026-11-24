using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Rules;

namespace OpenCiv2.Session;

/// <summary>V02 §5 local selection for a captured civilization, with actual setter/rate children.
/// The admitted parent supplies its decision recipient; command/phase admission belongs to that parent.</summary>
public sealed class GovernmentSelectionOperation(int owner, ParticipantId recipient,
    IPreferenceWriter preferences, RateDecisionPreferences choices, bool allGovernments = false) : IContinuation
{
    private InteractionSpec? decision;
    private CivilizationRateDecision? rates;
    private int selected = -1;
    private OperationStep? completed;
    private bool disposed, faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (faulted)
        { throw new InvalidOperationException("A failed government selection cannot resume"); }
        if (completed is not null)
        { return completed; }
        try
        {
            if (owner < 0 || owner >= EntityLimits.Civilizations)
            { throw new ArgumentOutOfRangeException(nameof(owner)); }
            if (context.World.Globals.Multiplayer != GameMode.SinglePlayer)
            { throw new InvalidOperationException("Local government selection requires single-player mode"); }
            var result = ResumeStep(context, input);
            if (result.Status.Terminal())
            { completed = result; }
            return result;
        }
        catch { faulted = true; rates?.Dispose(); context.Publish(); throw; }
    }
    private OperationStep ResumeStep(OperationContext context, OperationInput input)
    {
        if (rates is not null)
        {
            var result = rates.Resume(context, input);
            return !result.Status.Terminal() ? result : Finish(context, result);
        }
        var governments = Changes(context,
            _ => throw new InvalidOperationException("Selection cannot recursively request selection"),
            _ => rates = new(preferences, choices, civilization: owner, recipient: recipient));
        if (decision is null)
        {
            if (!context.World.Globals.HumanPlayers.Contains(owner))
            { return new(Result: Values(context, owner)); }
            var options = Enumerable.Range(allGovernments ? 0 : 1, allGovernments ? 7 : 6)
                .Where(g => allGovernments || governments.Available(owner, g))
                .Select(g => new InteractionOption(g, Name(context, g))).ToArray();
            decision = new("Choose a new government.", recipient, null, new(options.Select(o => o.Value))) { Options = new(options) };
            return new(CommandStatus.AwaitingInteraction, Interaction: decision);
        }
        if (input.Kind is OperationInputKind.Cancel or OperationInputKind.Timeout)
        { return new(CommandStatus.Cancelled, Code: "cancelled"); }
        if (input.Choice is not int value || !decision.Choices.Contains(value))
        { return new(CommandStatus.AwaitingInteraction, Interaction: decision); }
        selected = value;
        governments.Set(owner, value);
        context.Effect("civilization.government", Values(context, owner));
        if (rates is not null)
        {
            var result = rates.Resume(context, new());
            return !result.Status.Terminal() ? result : Finish(context, result);
        }
        return Finish(context, new());
    }
    private OperationStep Finish(OperationContext context, OperationStep child)
    {
        context.Effect("civilization.government.established", new([
            new("government", new CommandValue.Integer(selected)), new("name", new CommandValue.Text(Name(context, selected)))]));
        if (selected > 4 && context.World.Globals.GameOptions.Tutorial && !context.World.Globals.ShownEventFlags.Government)
        {
            context.Effect("civilization.government.tutorial", Values(context, owner));
            context.World.Globals.ShownEventFlags.Government = true;
        }
        return new(child.Status == CommandStatus.Failed ? CommandStatus.Failed : CommandStatus.Completed,
            Values(context, owner), child.Status == CommandStatus.Failed ? child.Code : "");
    }
    internal static OwnedList<NamedValue> Values(OperationContext context, int owner) =>
        new([new("government", new CommandValue.Integer(unchecked((byte)context.World.Civilizations[owner].Government)))]);
    private static string Name(OperationContext context, int government) =>
        RuleText.Display(context.Process.Rules.Definitions.Governments[government].Identity.Name);
    internal static GovernmentChanges Changes(OperationContext context, Action<int> select, Action<int> rates) =>
        new(context.World, context.Process.Rules, new(context.World, new(5_000_000, 64)), select, rates,
            notice => context.Effect("civilization.government.announced", new([
                new("civilization", new CommandValue.Integer(notice.Civilization)), new("old", new CommandValue.Integer(notice.OldGovernment)),
                new("government", new CommandValue.Integer(notice.NewGovernment)), new("overthrown", new CommandValue.Boolean(notice.Overthrown))])));
    public void Dispose() { disposed = true; rates?.Dispose(); }
}
