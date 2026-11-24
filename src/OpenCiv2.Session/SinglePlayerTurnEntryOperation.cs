using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;

namespace OpenCiv2.Session;

/// <summary>F04 §3 entry to one local civilization round. Actual global/outcome/event children
/// finish in order; the caller then owns civilization upkeep/movement and resume-field clearing.</summary>
public sealed class SinglePlayerTurnEntryOperation(GlobalTurn global, Func<ScenarioTriggers> events,
    HallOfFame hall, Func<int, ParticipantId> recipient, short accelerationSelection, string call,
    Func<EmissaryIntelligence, InteractionSpec>? intelligence = null) : IContinuation
{
    private IContinuation? child;
    private ScenarioTriggers? triggers;
    private OperationStep? completed;
    private int stage;
    private string failure = "";
    private bool faulted, disposed;
    public bool Quit { get; private set; }
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (faulted)
        { throw new InvalidOperationException("A failed turn entry cannot resume"); }
        if (completed is not null)
        { return completed; }
        try
        {
            if (context.World.Globals.Multiplayer != GameMode.SinglePlayer)
            { throw new InvalidOperationException("This turn-entry workflow requires single-player mode"); }
            ArgumentException.ThrowIfNullOrEmpty(call);
            while (true)
            {
                if (child is null)
                {
                    var world = context.World;
                    switch (stage++)
                    {
                        case 0:
                            world.HumanWonders.Update(world);
                            if (world.Multiplayer.ResumeCiv == 0)
                            { child = new GlobalTurnOperation(global.Begin(accelerationSelection, call), recipient, intelligence); }
                            break;
                        case 1:
                            child = new TurnOpeningOperation(hall);
                            break;
                        case 2:
                            if (!world.Events.Loaded || world.Multiplayer.ResumeCiv != 0 && world.Multiplayer.ResumeEvent == 0)
                            { stage = 6; break; }
                            triggers = events();
                            ArgumentNullException.ThrowIfNull(triggers);
                            child = new ScenarioTriggerOperation(triggers.BeginTurn(world.Globals.Turn, call + "/events/turn"), recipient, intelligence);
                            break;
                        case 3:
                            child = new ScenarioTriggerOperation(triggers!.BeginInterval(world.Globals.Turn, call + "/events/interval"), recipient, intelligence);
                            break;
                        case 4:
                            child = new ScenarioTriggerOperation(triggers!.BeginRandom(call + "/events/random"), recipient, intelligence);
                            break;
                        case 5:
                            child = new ScenarioTriggerOperation(triggers!.BeginTechnology(call + "/events/technology"), recipient, intelligence);
                            break;
                        default:
                            return Finish(context);
                    }
                    input = new();
                    if (child is null)
                    { continue; }
                }
                var result = child.Resume(context, input);
                if (!result.Status.Terminal())
                { return result; }
                if (result.Status == CommandStatus.Failed && failure.Length == 0)
                { failure = result.Code; }
                if (child is TurnOpeningOperation opening)
                { Quit = opening.Quit; }
                child.Dispose();
                child = null;
                if (Quit)
                { return Finish(context); }
            }
        }
        catch { faulted = true; Release(); context.Publish(); throw; }
    }
    private OperationStep Finish(OperationContext context)
    {
        context.Publish();
        return completed = new(failure.Length == 0 ? CommandStatus.Completed : CommandStatus.Failed, Code: failure);
    }
    private void Release() { child?.Dispose(); child = null; }
    public void Dispose() { disposed = true; Release(); }
}
