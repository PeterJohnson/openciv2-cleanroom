using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;

namespace OpenCiv2.Session;

/// <summary>V07 §7 window request result. Opened includes an already-active window/council;
/// false selects the text fallback after required window cleanup.</summary>
public interface ISpaceshipWindowOperation : IContinuation
{
    bool Opened { get; }
}

/// <summary>F04/V07 retained window request and actual text fallback. A null window factory
/// result means this host has no available graphical ship resources.</summary>
public sealed class SpaceshipPresentationOperation(int owner, OwnedList<short> beforeParts,
    Func<OperationContext, SpaceshipConstruction> construction,
    Func<int, OwnedList<short>, ISpaceshipWindowOperation?> window,
    ParticipantId? recipient = null, SpaceshipValues? values = null, CalendarValues? calendar = null) : IContinuation
{
    private bool requested, windowFinished, disposed, faulted;
    private ISpaceshipWindowOperation? graphical;
    private SpaceshipStatusOperation? status;
    private OperationStep? completed;
    private string failure = "";
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (faulted)
        { throw new InvalidOperationException("A failed ship presentation cannot resume"); }
        if (completed is not null)
        { return completed; }
        try
        {
            if (!requested)
            { requested = true; graphical = window(owner, beforeParts); input = new(); }
            if (!windowFinished)
            {
                if (graphical is not null)
                {
                    var result = graphical.Resume(context, input);
                    if (!result.Status.Terminal())
                    { return result; }
                    bool opened = graphical.Opened;
                    if (result.Status == CommandStatus.Failed)
                    { failure = result.Code; }
                    graphical.Dispose();
                    graphical = null;
                    if (opened)
                    { return completed = result; }
                }
                windowFinished = true;
                // This membership is read after the window request, not when upkeep began.
                status = new(owner, context.World.Globals.HumanPlayers.Contains(owner), construction, recipient, values, calendar);
                input = new();
            }
            var next = status!.Resume(context, input);
            if (!next.Status.Terminal())
            { return next; }
            status.Dispose();
            status = null;
            return completed = failure.Length == 0 ? next : new(CommandStatus.Failed, Code: failure);
        }
        catch { faulted = true; DisposeChildren(); context.Publish(); throw; }
    }
    private void DisposeChildren()
    {
        var g = graphical;
        graphical = null;
        var s = status;
        status = null;
        try
        { g?.Dispose(); }
        finally { s?.Dispose(); }
    }
    public void Dispose() { if (disposed) { return; } disposed = true; DisposeChildren(); }
}

/// <summary>V07 §7 selector includes every Exists ship in civilization order without contact,
/// alive or human filtering. The selected ship's counts are captured before requesting its window.</summary>
public sealed class SpaceshipSelectionOperation(Func<OperationContext, SpaceshipConstruction> construction,
    Func<int, OwnedList<short>, ISpaceshipWindowOperation?> window,
    SpaceshipValues? values = null, CalendarValues? calendar = null) : IContinuation
{
    private InteractionSpec? selection;
    private bool empty, disposed;
    private SpaceshipPresentationOperation? child;
    private OperationStep? completed;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (completed is not null)
        { return completed; }
        if (selection is null)
        {
            var options = Enumerable.Range(1, 7).Where(c => context.World.Civilizations[c].SpaceshipFlags.Exists)
                .Select(c => new InteractionOption(c, OutcomeText.Plural(context, c))).ToList();
            empty = options.Count == 0;
            if (empty)
            { options.Add(new(0, "Close")); }
            else
            { options.Add(new(-1, "Cancel")); }
            selection = new(empty ? "No spaceships." : "View a spaceship", context.Actor.Participant, null,
                new(options.Select(o => o.Value)))
            { Options = new(options) };
            return new(CommandStatus.AwaitingInteraction, Interaction: selection);
        }
        if (child is null)
        {
            if (input.Kind is OperationInputKind.Cancel or OperationInputKind.Timeout
                || input.Kind == OperationInputKind.Choice && (empty || input.Choice < 0))
            { return completed = new(); }
            if (input.Kind != OperationInputKind.Choice || input.Choice is not { } owner)
            { return new(CommandStatus.AwaitingInteraction, Interaction: selection); }
            child = new(owner, SpaceshipUpkeep.Capture(context.World, owner), construction, window, context.Actor.Participant, values, calendar);
            input = new();
        }
        var result = child.Resume(context, input);
        if (!result.Status.Terminal())
        { return result; }
        child.Dispose();
        child = null;
        return completed = result;
    }
    public void Dispose() { disposed = true; child?.Dispose(); }
}
