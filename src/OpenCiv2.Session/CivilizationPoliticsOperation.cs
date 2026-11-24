using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Formats;

namespace OpenCiv2.Session;

/// <summary>F04 §6/V02 §6 government prefix followed by the actual V04/A08 diplomacy turn.</summary>
public sealed class CivilizationPoliticsOperation(int owner, DiplomacyTurn diplomacy, IPreferenceWriter preferences,
    RateDecisionPreferences choices, Func<int, ParticipantId> recipient, Func<EmissaryIntelligence, InteractionSpec> intelligence,
    string call) : IContinuation
{
    private GovernmentTurn? government;
    private GovernmentSelectionOperation? selection;
    private DiplomacyTurnOperation? following;
    private OperationStep? completed;
    private string failure = "";
    private bool faulted, disposed;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (faulted)
        { throw new InvalidOperationException("A failed politics turn cannot resume"); }
        if (completed is not null)
        { return completed; }
        try
        {
            if (context.World.Globals.Multiplayer != GameMode.SinglePlayer)
            { throw new InvalidOperationException("Local politics turn requires single-player mode"); }
            if (owner < 0 || owner >= EntityLimits.Civilizations)
            { throw new ArgumentOutOfRangeException(nameof(owner)); }
            ArgumentException.ThrowIfNullOrEmpty(call);
            var world = context.World;
            government ??= new(world, context.Process.Rules.Definitions, owner, c =>
            {
                if (RecordCompatibility.Find(world.Lifetime) is { } opaque)
                { opaque.Civilizations[c] = opaque.Civilizations[c] with { Flags = (ushort)(opaque.Civilizations[c].Flags & ~0x40) }; }
            });
            while (!government.Completed)
            {
                if (selection is not null)
                {
                    var result = selection.Resume(context, input);
                    if (!result.Status.Terminal())
                    { return result; }
                    Remember(result);
                    selection.Dispose();
                    selection = null;
                    government.ChangeCompleted();
                    input = new();
                }
                if (government.NextChange() is not int requested)
                { break; }
                GovernmentSelectionOperation.Changes(context,
                    c => selection = new(c, recipient(c), preferences, choices),
                    _ => throw new InvalidOperationException("The government selector owns its rate child"))
                    .Change(owner, requested);
                context.Effect("civilization.government.selection", GovernmentSelectionOperation.Values(context, owner));
                if (selection is null)
                { government.ChangeCompleted(); }
                input = new();
            }
            if (following is null)
            { following = new(diplomacy.Begin(owner, call), recipient, intelligence); input = new(); }
            var final = following.Resume(context, input);
            if (!final.Status.Terminal())
            { return final; }
            Remember(final);
            following.Dispose();
            following = null;
            context.Effect("civilization.diplomacy.maintained", GovernmentSelectionOperation.Values(context, owner));
            return completed = new(failure.Length == 0 ? CommandStatus.Completed : CommandStatus.Failed, Code: failure);
        }
        catch { faulted = true; selection?.Dispose(); following?.Dispose(); context.Publish(); throw; }
    }
    private void Remember(OperationStep step)
    { if (step.Status == CommandStatus.Failed && failure.Length == 0) { failure = step.Code; } }
    public void Dispose() { disposed = true; selection?.Dispose(); following?.Dispose(); }
}
