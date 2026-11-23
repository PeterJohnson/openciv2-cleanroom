using OpenCiv2.Contracts;

namespace OpenCiv2.Session;

public static partial class LocalPlayCommands
{
    public const string Score = "civilization.score";

    private static void RegisterScoreCommand(SessionRuntime runtime) => runtime.RegisterCommand(new(
        new(1, Score, "Prepare the civilization score and rating report.", AccessClass.Player,
            new([]), new([new("score", Required: false), new("rating", Required: false), new("title", Required: false)])),
        (actor, _, world) => new(ValidatePhase(actor, world)), _ => new CivilizationScoreOperation()));
}
