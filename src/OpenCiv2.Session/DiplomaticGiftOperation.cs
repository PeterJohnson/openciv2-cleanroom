using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;

namespace OpenCiv2.Session;

/// <summary>V05 §4 private gift choices, including retained V03 research/government children.</summary>
public sealed class DiplomaticGiftOperation(DiplomaticGifts.Operation operation,
    Func<int, ParticipantId> recipient, Func<EmissaryIntelligence, InteractionSpec> intelligence) : IContinuation
{
    private DiplomaticGiftDecision? pending;
    private InteractionSpec? window;
    private OperationStep? completed;
    private bool faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        { throw new InvalidOperationException("A failed diplomatic gift cannot resume"); }
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
            context.Effect("diplomacy.gift.completed", new([]));
            return completed = operation.PresentationFailures.Count == 0 ? new()
                : new(CommandStatus.Failed, Code: "gift_presentation_failed");
        }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    private ResearchReply? Reply(OperationInput input)
    {
        if (pending is DiplomaticGiftDecision.Grant grant)
        { return ResearchGrantInteraction.ResearchReply(grant.Decision, input); }
        if (input.Kind == OperationInputKind.Choice && input.Choice is { } choice)
        { return new(choice); }
        return input.Kind is OperationInputKind.Cancel or OperationInputKind.Timeout
            ? new(pending is DiplomaticGiftDecision.City or DiplomaticGiftDecision.Unit ? -1 : 0) : null;
    }
    private InteractionSpec Prompt(OperationContext context, DiplomaticGiftDecision decision)
    {
        if (decision is DiplomaticGiftDecision.Grant grant)
        { return ResearchGrantInteraction.Prompt(context, grant.Decision, recipient, intelligence, ref window); }
        string title;
        int player;
        List<InteractionOption> options;
        switch (decision)
        {
            case DiplomaticGiftDecision.Menu menu:
                player = menu.Recipient;
                title = $"Offer a gift to the {menu.Counterpart.Plural}";
                options = [new(0, "Cancel"), new(1, "Technology"), new(2, "Gold"), new(3, "Military unit")];
                break;
            case DiplomaticGiftDecision.Technology technology:
                player = technology.Recipient;
                title = "Choose a technology to give";
                options = [new(0, "Cancel"), new(1, technology.Best.Name)];
                if (technology.Second is { } second)
                { options.Add(new(2, second.Name)); }
                break;
            case DiplomaticGiftDecision.Gold gold:
                player = gold.Recipient;
                title = "Choose gold to give";
                options = [new(0, "Cancel"), .. gold.Amounts.Select(a => new InteractionOption(a, a + " gold"))];
                break;
            case DiplomaticGiftDecision.City city:
                player = city.Recipient;
                title = "Choose a city with a unit to give";
                options = [new(-1, "Cancel"), .. city.Options.Select(c => new InteractionOption(c.City.Value, c.Name))];
                break;
            case DiplomaticGiftDecision.Unit unit:
                player = unit.Recipient;
                title = "Choose a unit to give";
                options = [new(-1, "Cancel"), .. unit.Options.Select(u => new InteractionOption(u.Unit.Value, u.Name))];
                break;
            default:
                throw new InvalidOperationException("Unknown gift decision");
        }
        return new(title, recipient(player), null, new(options.Select(o => o.Value))) { Options = new(options) };
    }
    public void Dispose() => operation.Dispose();
}
