using OpenCiv2.Contracts;

namespace OpenCiv2.Session;

public static partial class LocalPlayCommands
{
    public const string Spaceship = "spaceship.view";

    private static void RegisterSpaceshipCommand(SessionRuntime runtime) => runtime.RegisterCommand(new(
        new(1, Spaceship, "View existing spaceships and confirm an eligible launch.", AccessClass.Player, new([]), new([])),
        (actor, _, world) => new(ValidatePhase(actor, world)),
        _ => new SpaceshipSelectionOperation(EndPlayerPhase.CreateSpaceshipConstruction, (_, _) => null)));
}
