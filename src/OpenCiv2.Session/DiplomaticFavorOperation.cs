using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;

namespace OpenCiv2.Session;

/// <summary>V05 private favor decisions, including real exchange and grant continuations.</summary>
public sealed class DiplomaticFavorOperation(DiplomaticFavors.Operation operation,
    Func<int, ParticipantId> recipient, Func<EmissaryIntelligence, InteractionSpec> intelligence) : IContinuation
{
    private DiplomaticFavorDecision? pending;
    private InteractionSpec? window;
    private OperationStep? completed;
    private bool faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        { throw new InvalidOperationException("A failed diplomatic favor cannot resume"); }
        if (completed is not null)
        { return completed; }
        try
        {
            pending = operation.Resume(Reply(input));
            if (pending is not null)
            {
                context.Publish();
                return new(CommandStatus.AwaitingInteraction, Interaction: Prompt(context, pending));
            }
            if (!operation.Completed)
            { context.Publish(); return new(CommandStatus.Pending) { ReadyToContinue = true }; }
            context.Effect("diplomacy.favor.completed", new([]));
            return completed = operation.PresentationFailures.Count == 0 ? new() : new(CommandStatus.Failed, Code: "favor_presentation_failed");
        }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    private ResearchReply? Reply(OperationInput input)
    {
        if (pending is DiplomaticFavorDecision.Technology technology)
        { return TechnologyNegotiationInteraction.Reply(technology.Decision, input); }
        if (pending is DiplomaticFavorDecision.Grant grant)
        { return ResearchGrantInteraction.ResearchReply(grant.Decision, input); }
        if (input.Kind == OperationInputKind.Choice && input.Choice is { } choice)
        { return new(choice); }
        return input.Kind is OperationInputKind.Cancel or OperationInputKind.Timeout ? new(0) : null;
    }
    private InteractionSpec Prompt(OperationContext context, DiplomaticFavorDecision decision)
    {
        if (decision is DiplomaticFavorDecision.Technology technology)
        { return TechnologyNegotiationInteraction.Prompt(context, technology.Decision, recipient, intelligence, ref window); }
        if (decision is DiplomaticFavorDecision.Grant grant)
        { return ResearchGrantInteraction.Prompt(context, grant.Decision, recipient, intelligence, ref window); }
        if (decision is DiplomaticFavorDecision.Menu menu)
        {
            return new($"Ask the {menu.Partner.Plural} for a favor", recipient(menu.Recipient), null, new([0, 1, 2, 3]))
            { Options = new([new(0, "Leave"), new(1, "Exchange technology"), new(2, "Join a war"), new(3, "Exchange maps")]) };
        }
        if (decision is DiplomaticFavorDecision.Target target)
        {
            List<InteractionOption> choices = [new(0, "Back"), .. target.Options.Select(p => new InteractionOption(p.Civilization, p.Plural))];
            return new("Choose a civilization to oppose", recipient(target.Recipient), null, new(choices.Select(c => c.Value))) { Options = new(choices) };
        }
        var offer = ((DiplomaticFavorDecision.War)decision).Offer;
        string price = offer.Gold is { } gold ? gold + " gold" : offer.First!.Name + " and " + offer.Second!.Name;
        return new($"Pay the {offer.Partner.Plural} {price} to declare war on the {offer.Target.Plural}?", recipient(offer.Recipient), null, new([0, 1]))
        { Options = new([new(0, "Decline"), new(1, "Accept")]) };
    }
    public void Dispose() => operation.Dispose();
}
