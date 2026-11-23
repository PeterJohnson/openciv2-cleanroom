using OpenCiv2.Base;
using OpenCiv2.Contracts;

namespace OpenCiv2.Session;

/// <summary>V08 §7 explicit single-player Quit/Retire. Does not award Won, mark Scored or post an outcome.</summary>
public sealed class RetireGameOperation(bool retire, HallOfFame hall) : IContinuation
{
    private bool confirmed, asked, finished;
    private EndGameReports? reports;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (context.World.Globals.Multiplayer != GameMode.SinglePlayer)
        { return new(CommandStatus.Failed, Code: "single_player_required"); }
        if (finished)
        { throw new InvalidOperationException("Retirement already finished"); }
        if (!confirmed)
        {
            if (input.Kind is OperationInputKind.Cancel or OperationInputKind.Timeout || asked && input.Choice == 0)
            { return new(CommandStatus.Cancelled, Code: "cancelled"); }
            if (!asked || input.Kind != OperationInputKind.Choice || input.Choice is null)
            {
                asked = true;
                return new(CommandStatus.AwaitingInteraction, Interaction: new(retire ? "Retire from this game?" : "Quit this game?", null, null, new([0, 1]))
                { Options = new([new(0, "Keep playing"), new(1, retire ? "Retire" : "Quit game")]) });
            }
            confirmed = true;
            if (retire)
            { reports = new(hall); }
            input = new();
        }
        OperationStep result = reports?.Resume(context, input) ?? new();
        if (!result.Status.Terminal())
        { return result; }
        // This profile has no CD playback. The desktop closes the game/map view on this publication.
        context.World.PlayerPhaseActive = context.World.TurnLoopActive = false;
        finished = true;
        context.Effect("game.closed", new([new("retired", new CommandValue.Boolean(retire))]));
        return new(result.Status, Code: result.Code);
    }
    public void Dispose() => reports?.Dispose();
}
