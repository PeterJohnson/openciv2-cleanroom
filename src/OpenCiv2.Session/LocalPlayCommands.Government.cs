using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Session;

public static partial class LocalPlayCommands
{
    public const string Revolution = "civilization.revolution";

    private static void RegisterGovernmentCommands(SessionRuntime runtime, IPreferenceWriter preferences, RateDecisionPreferences choices) =>
        runtime.RegisterCommand(new(
            new(1, Revolution, "Request a revolution or choose an already available new government.", AccessClass.Player,
                new([]), new([new("government")])),
            (actor, _, world) => new(ValidatePhase(actor, world)),
            _ => new RevolutionCommand(preferences, choices)));

    private static OwnedList<NamedValue> GovernmentValue(OperationContext context) =>
        new([new("government", new CommandValue.Integer(unchecked((byte)context.World.Civilizations[context.Actor.Civilization.Value].Government)))]);

    private static string GovernmentName(OperationContext context, int government) =>
        RuleText.Display(context.Process.Rules.Definitions.Governments[government].Identity.Name);

    private static GovernmentChanges PlayerGovernments(OperationContext context, Action<int> select, Action<int> rates) =>
        new(context.World, context.Process.Rules, new(context.World, new(5_000_000, 64)), select, rates,
            notice => context.Effect("civilization.government.announced", new([
                new("civilization", new CommandValue.Integer(notice.Civilization)),
                new("old", new CommandValue.Integer(notice.OldGovernment)),
                new("government", new CommandValue.Integer(notice.NewGovernment)),
                new("overthrown", new CommandValue.Boolean(notice.Overthrown))])));

    private sealed class RevolutionCommand(IPreferenceWriter preferences, RateDecisionPreferences choices) : IContinuation
    {
        private GovernmentPicker? picker;
        private bool started;

        public OperationStep Resume(OperationContext context, OperationInput input)
        {
            if (!started && input.Kind is OperationInputKind.Cancel or OperationInputKind.Timeout)
            {
                return new(CommandStatus.Cancelled, Code: "cancelled");
            }
            if (ValidatePhase(context.Actor, context.World) is { } error)
            {
                return new(CommandStatus.Failed, Code: error);
            }
            var world = context.World;
            int owner = context.Actor.Civilization.Value;
            if (picker is not null)
            {
                return picker.Resume(context, input);
            }
            if (!started)
            {
                started = true;
                if (world.Globals.Status.Scenario && world.Scenario.Flags.GovernmentLock)
                {
                    return new(Result: GovernmentValue(context));
                }
                if (world.Civilizations[owner].Flags.GovernmentChanged)
                {
                    PlayerGovernments(context, _ => picker = new(preferences, choices),
                        _ => throw new InvalidOperationException("Selection precedes its rate decision"))
                        .Change(owner, 1);
                    context.Effect("civilization.government.selection", GovernmentValue(context));
                    return picker!.Resume(context, new());
                }
                if (world.Civilizations[owner].Government == Government.Anarchy)
                {
                    return new(Result: GovernmentValue(context));
                }
                return new(CommandStatus.AwaitingInteraction, Interaction: new(
                    $"Start a revolution against {GovernmentName(context, (byte)world.Civilizations[owner].Government)}?",
                    null, null, new([0, 1]))
                { Options = new([new(0, "Start revolution"), new(1, "Keep the current government")]) });
            }
            if (input.Kind is OperationInputKind.Cancel or OperationInputKind.Timeout)
            {
                return new(CommandStatus.Cancelled, Code: "cancelled");
            }
            if (input.Choice == 0)
            {
                context.Effect("civilization.revolution.started", GovernmentValue(context));
                PlayerGovernments(context, _ => throw new InvalidOperationException("Anarchy does not select a government"),
                    _ => throw new InvalidOperationException("Anarchy does not open rates")).Set(owner, 0);
                context.Effect("civilization.government", GovernmentValue(context));
            }
            return new(Result: GovernmentValue(context));
        }
    }

    /// <summary>V02 §5 restricted player selection. Offered identities are captured once;
    /// actual setter and rate children finish before the new-government notice.</summary>
    private sealed class GovernmentPicker(IPreferenceWriter preferences, RateDecisionPreferences choices, bool duringTurn = false) : IContinuation
    {
        private GovernmentSelectionOperation? selection;
        public OperationStep Resume(OperationContext context, OperationInput input)
        {
            if (ValidatePhase(context.Actor, context.World, !duringTurn) is { } error)
            { return new(CommandStatus.Failed, Code: error); }
            selection ??= new(context.Actor.Civilization.Value, context.Actor.Participant, preferences, choices);
            return selection.Resume(context, input);
        }
        public void Dispose() => selection?.Dispose();
    }
}
