using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;

namespace OpenCiv2.Session;

/// <summary>Shared private gift decisions for direct and audience callers.</summary>
internal static class DiplomaticGiftInteraction
{
    public static ResearchReply? Reply(DiplomaticGiftDecision? pending, OperationInput input)
    {
        if (pending is DiplomaticGiftDecision.Grant grant)
        { return ResearchGrantInteraction.ResearchReply(grant.Decision, input); }
        if (input.Kind == OperationInputKind.Choice && input.Choice is { } choice)
        { return new(choice); }
        return input.Kind is OperationInputKind.Cancel or OperationInputKind.Timeout
            ? new(pending is DiplomaticGiftDecision.City or DiplomaticGiftDecision.Unit ? -1 : 0) : null;
    }
    public static InteractionSpec Prompt(OperationContext context, DiplomaticGiftDecision decision,
        Func<int, ParticipantId> recipient, Func<EmissaryIntelligence, InteractionSpec> intelligence, ref InteractionSpec? window)
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
}
