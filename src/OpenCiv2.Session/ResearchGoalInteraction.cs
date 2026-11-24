using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;

namespace OpenCiv2.Session;

/// <summary>Private V03 goal lists, captured Help operands and informational path pages.
/// Encoded choices are presentation inputs only; the core receives the distinct extra result.</summary>
internal static class ResearchGoalInteraction
{
    internal const int HelpBase = 1000, CategoryBase = 200;
    internal static string Label(ResearchOption option) => option.FutureNumber is { } future ? $"{option.Name} {future}"
        : option.PrerequisiteName is { } prerequisite ? $"{option.Name} ({prerequisite})" : option.Name;
    internal static ResearchReply? Reply(ResearchGoalDecision decision, OperationInput input)
    {
        if (input.Kind is OperationInputKind.Cancel or OperationInputKind.Timeout)
        {
            return new(-1);
        }

        if (input.Kind != OperationInputKind.Choice || input.Choice is not { } value)
        {
            return null;
        }

        if (decision is ResearchGoalNotice list)
        {
            if (value >= CategoryBase && value < CategoryBase + list.OtherCategories.Count)
            {
                return new(list.Options[0].Identity, value - CategoryBase + 2);
            }

            if (list.Icons && value >= HelpBase && list.Options.Any(o => o.Identity == value - HelpBase))
            {
                return new(value - HelpBase, 1);
            }

            return list.Options.Any(o => o.Identity == value) ? new(value) : null;
        }
        return decision is ResearchGoalHelp && value is 0 or 1 || decision is ResearchPathNotice or ResearchEncyclopediaNotice && value == 0 ? new(value) : null;
    }
    internal static InteractionSpec Prompt(OperationContext context, ResearchGoalDecision decision, Func<int, ParticipantId> recipient)
    {
        switch (decision)
        {
            case ResearchGoalNotice list:
                var options = list.Options.Select(o => new InteractionOption(o.Identity, Label(o))).ToList();
                if (list.Icons)
                {
                    options.AddRange(list.Options.Select(o => new InteractionOption(HelpBase + o.Identity, $"Help: {Label(o)}")));
                }

                options.AddRange(list.OtherCategories.Select((category, i) => new InteractionOption(CategoryBase + i, category switch
                { ResearchTopic.Advance => "Browse advances", ResearchTopic.Unit => "Browse units", _ => "Browse improvements" })));
                return new("Choose a research goal to see the advances leading to it.", recipient(list.Civilization), null, new(options.Select(o => o.Value)))
                { Options = new(options) };
            case ResearchGoalHelp help:
                return new("Which subject would you like help with?", recipient(help.Civilization), null, new([0, 1]))
                { Options = new([new(0, help.Name), new(1, help.PrerequisiteName)]) };
            case ResearchEncyclopediaNotice page:
                return new(ResearchReference.Text(context.Process.Rules.Definitions, page.Topic, page.Identity), recipient(page.Civilization), null, new([0]))
                { Options = new([new(0, "Back")]) };
            case ResearchPathNotice path:
                string action = path.Stealing ? "steal" : "research";
                string message = path.Path.Count == 0 ? $"No available advances to {action} toward {path.Name}."
                    : $"Advances to {action} toward {path.Name}: {string.Join(", ", path.Path.Select(Label))}.";
                return new(message, recipient(path.Civilization), null, new([0])) { Options = new([new(0, "Continue")]) };
            default:
                throw new InvalidOperationException("Unknown research goal decision");
        }
    }
}
