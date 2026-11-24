using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;

namespace OpenCiv2.Session;

/// <summary>Private F04 council invitation/stock advice and tutorial notices. Closing a tutorial
/// acknowledges it; declining or cancelling the invitation never opens the council.</summary>
public sealed class HumanTurnNoticesOperation(HumanTurnNotices.Operation operation, Func<int, ParticipantId> recipient) : IContinuation
{
    private HumanTurnNotice? pending;
    private InteractionSpec? window;
    private OperationStep? completed;
    private bool faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        { throw new InvalidOperationException("Failed turn notices cannot resume"); }
        if (completed is not null)
        { return completed; }
        try
        {
            int? answer = pending is null ? null : input.Kind switch
            {
                OperationInputKind.Choice => input.Choice,
                OperationInputKind.Cancel or OperationInputKind.Timeout => 1,
                _ => null
            };
            var next = operation.Resume(answer);
            if (!ReferenceEquals(next, pending))
            { window = null; }
            pending = next;
            if (pending is not null)
            {
                window ??= Prompt(context, pending);
                context.Publish();
                return new(CommandStatus.AwaitingInteraction, Interaction: window);
            }
            context.Effect("turn.human.notices.completed", new([]));
            return completed = new(operation.PresentationFailures.Count == 0 ? CommandStatus.Completed : CommandStatus.Failed,
                Code: operation.PresentationFailures.Count == 0 ? "" : "human_turn_notices_presentation_failed");
        }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    private InteractionSpec Prompt(OperationContext context, HumanTurnNotice notice)
    {
        if (notice is HumanTurnNotice.CouncilInvitation invitation)
        {
            return new($"High Council — {OutcomeText.Plural(context, invitation.Civilization)}\n"
                + $"{OutcomeText.Date(context.World, invitation.Year)} — {invitation.CityName}\nConsult your advisors?",
                recipient(invitation.Civilization), null, new([0, 1]))
            { Options = new([new(0, "Consult council"), new(1, "Continue turn")]) };
        }
        int owner;
        string text;
        if (notice is HumanTurnNotice.Council council)
        {
            var s = council.Summary;
            owner = s.Civilization;
            var advances = context.Process.Rules.Definitions.Advances;
            string research = s.Research >= 0 && s.Research < advances.Length ? advances[s.Research].Identity.Name : "No research selected";
            text = $"High Council\nMilitary: {s.Units} units. Review city defenses before sending units abroad.\n"
                + $"Science: {research}; {s.ScienceRate * 10}% allocated to science.\n"
                + $"Treasury: {s.Gold} gold; {s.TaxRate * 10}% allocated to taxes.\n"
                + $"Foreign affairs: {s.Wars} wars.\nDomestic affairs: {s.Cities} cities, {s.Population} citizens, {s.Disorder} cities in disorder.\n"
                + (s.Disorder > 0 ? "Review workers and happiness in cities with disorder." : "Review food and production before expanding.");
        }
        else if (notice is HumanTurnNotice.Tutorial hint)
        {
            owner = hint.Civilization;
            text = hint.Kind switch
            {
                HumanTurnHint.FirstMove => "First move\nSelect a unit to explore or establish your first city.",
                HumanTurnHint.FirstHelp => "Managing your cities\nReview food, production and worked tiles in the city report.",
                HumanTurnHint.SecondHelp => "Planning your civilization\nReview research, tax rates and relations with other civilizations.",
                HumanTurnHint.OneCity => "Building another city\nYour first city has grown. Consider producing Settlers to establish another city.",
                HumanTurnHint.Expansion => "Expanding your civilization\nConsider producing Settlers and finding a suitable location for another city.",
                _ => throw new InvalidOperationException("Unknown turn hint")
            };
        }
        else
        { throw new InvalidOperationException("Unknown turn notice"); }
        return new(text, recipient(owner), null, new([0])) { Options = new([new(0, "Continue")]) };
    }
    public void Dispose() => operation.Dispose();
}
