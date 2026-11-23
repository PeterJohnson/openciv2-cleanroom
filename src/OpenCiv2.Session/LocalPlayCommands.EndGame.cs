using OpenCiv2.Contracts;

namespace OpenCiv2.Session;

public static partial class LocalPlayCommands
{
    public const string Power = "civilization.history", Retire = "game.retire", Quit = "game.quit";
    private static void RegisterEndGameCommands(SessionRuntime runtime, HallOfFame hall)
    {
        runtime.RegisterCommand(new(new(1, Power, "View retained civilization power history.", AccessClass.Player, new([]), new([])),
            (actor, _, world) => new(ValidatePhase(actor, world)), _ => new PowerHistoryOperation()));
        foreach (bool retire in new[] { false, true })
        {
            runtime.RegisterCommand(new(new(1, retire ? Retire : Quit, retire ? "Retire after reviewing the game reports." : "Quit the current game.",
                AccessClass.Player, new([]), new([])), (actor, _, world) => new(ValidatePhase(actor, world)),
                _ => new RetireGameOperation(retire, hall)));
        }
    }
}
