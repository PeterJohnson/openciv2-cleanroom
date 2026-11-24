using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;

namespace OpenCiv2.Session;

internal static class SpaceshipPartInteraction
{
    private static readonly string[] Names = ["Structural", "Propulsion", "Fuel", "Habitation", "Life support", "Solar energy"];
    internal static InteractionSpec Prompt(SpaceshipPartChoice choice, Func<int, ParticipantId> recipient)
    {
        int first = choice.Group == 1 ? 1 : 3;
        int[] counts = [choice.FirstCount, choice.SecondCount, choice.ThirdCount];
        int length = choice.Group == 1 ? 2 : 3;
        var options = new OwnedList<InteractionOption>(Enumerable.Range(0, length)
            .Select(i => new InteractionOption(i, Names[first + i], "Current count: " + counts[i])));
        string prompt = choice.Group == 1 ? "Choose a spaceship component." : "Choose a spaceship module.";
        if (choice.FullPart is { } full)
        {
            prompt += "\n" + Names[full] + " is already full. Choose another part.";
        }
        // V07 has no cancellation sentinel. The player prompt admits only nominal subtype replies.
        return new(prompt, recipient(choice.Civilization), null, new(options.Select(o => o.Value)), Cancellable: false)
        { Options = options };
    }
}
