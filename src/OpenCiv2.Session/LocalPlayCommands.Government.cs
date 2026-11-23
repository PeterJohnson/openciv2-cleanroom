using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Session;

public static partial class LocalPlayCommands
{
    public const string Revolution = "civilization.revolution";

    private static void RegisterGovernmentCommands(SessionRuntime runtime, IPreferenceWriter preferences, LocalRateChoices choices) =>
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

    private sealed class RevolutionCommand(IPreferenceWriter preferences, LocalRateChoices choices) : IContinuation
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
    private sealed class GovernmentPicker(IPreferenceWriter preferences, LocalRateChoices choices, bool duringTurn = false) : IContinuation
    {
        private InteractionSpec? decision;
        private RateEditor? rates;
        private int selected = -1;

        public OperationStep Resume(OperationContext context, OperationInput input)
        {
            if (ValidatePhase(context.Actor, context.World, !duringTurn) is { } error)
            {
                return new(CommandStatus.Failed, Code: error);
            }
            int owner = context.Actor.Civilization.Value;
            if (rates is not null)
            {
                var result = rates.Resume(context, input);
                return result.Status == CommandStatus.AwaitingInteraction ? result : Finish(context, result);
            }
            var governments = PlayerGovernments(context,
                _ => throw new InvalidOperationException("Selection cannot recursively request selection"),
                _ => rates = new(preferences, choices, duringTurn));
            if (decision is null)
            {
                var options = Enumerable.Range(1, 6).Where(g => governments.Available(owner, g))
                    .Select(g => new InteractionOption(g, GovernmentName(context, g))).ToArray();
                decision = new("Choose a new government.", null, null, new(options.Select(o => o.Value)))
                { Options = new(options) };
                return new(CommandStatus.AwaitingInteraction, Interaction: decision);
            }
            if (input.Kind is OperationInputKind.Cancel or OperationInputKind.Timeout)
            {
                return new(CommandStatus.Cancelled, Code: "cancelled");
            }
            if (input.Choice is not int value || !decision.Choices.Contains(value))
            {
                return new(CommandStatus.AwaitingInteraction, Interaction: decision);
            }
            selected = value;
            governments.Set(owner, value);
            context.Effect("civilization.government", GovernmentValue(context));
            if (rates is not null)
            {
                var result = rates.Resume(context, new());
                return result.Status == CommandStatus.AwaitingInteraction ? result : Finish(context, result);
            }
            return Finish(context, new());
        }

        private OperationStep Finish(OperationContext context, OperationStep child)
        {
            context.Effect("civilization.government.established", new([
                new("government", new CommandValue.Integer(selected)),
                new("name", new CommandValue.Text(GovernmentName(context, selected)))]));
            if (selected > 4 && context.World.Globals.GameOptions.Tutorial && !context.World.Globals.ShownEventFlags.Government)
            {
                context.Effect("civilization.government.tutorial", GovernmentValue(context));
                context.World.Globals.ShownEventFlags.Government = true;
            }
            return new(child.Status == CommandStatus.Failed ? CommandStatus.Failed : CommandStatus.Completed,
                GovernmentValue(context), child.Status == CommandStatus.Failed ? child.Code : "");
        }
    }
}
