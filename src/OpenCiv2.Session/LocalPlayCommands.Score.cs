using OpenCiv2.Contracts;

namespace OpenCiv2.Session;

public static partial class LocalPlayCommands
{
    public const string Hall = "civilization.hall";
    private static void RegisterHallCommand(SessionRuntime runtime, HallOfFame hall) => runtime.RegisterCommand(new(
        new(1, Hall, "View recorded Hall of Fame games.", AccessClass.Player, new([]), new([new("rank")])),
        (actor, _, world) => new(ValidatePhase(actor, world)), _ => new HallOfFameOperation(hall)));

    public const string Score = "civilization.score";

    private static void RegisterScoreCommand(SessionRuntime runtime) => runtime.RegisterCommand(new(
        new(1, Score, "Prepare the civilization score and rating report.", AccessClass.Player,
            new([]), new([new("score", Required: false), new("rating", Required: false), new("title", Required: false)])),
        (actor, _, world) => new(ValidatePhase(actor, world)), _ => new CivilizationScoreOperation()));
}
