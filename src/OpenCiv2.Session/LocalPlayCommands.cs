using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Session;

/// <summary>Player commands admitted by the authored local game's movement and decision phases.</summary>
public static partial class LocalPlayCommands
{
    public const string Select = "unit.select";
    public const string Move = "unit.move";

    internal static void Register(SessionRuntime runtime)
    {
        runtime.RegisterCommand(Define(Select, "Activate an owned unit.", [new("unit", ValueKind.Unit)],
            [new("unit", ValueKind.Unit)], SelectUnit, ValidateUnit));
        runtime.RegisterCommand(Define(Move, "Move one step in direction 0–7, clockwise from northeast.",
            [new("unit", ValueKind.Unit), new("direction")], [new("unit", ValueKind.Unit)], MoveUnit, ValidateUnit));
        RegisterCityCommands(runtime);
        RegisterTurnCommands(runtime);
        RegisterWorkCommands(runtime);
    }

    private static CommandDefinition Define(
        string name, string help, ValueSchema[] args, ValueSchema[] result,
        Func<OperationContext, CommandRequest, OperationStep> execute,
        Func<CommandActor, CommandRequest, GameState, string?>? validate = null) => new(
            new(1, name, help, AccessClass.Player, new(args), new(result)),
            (actor, request, world) => new(ValidatePhase(actor, world) ?? validate?.Invoke(actor, request, world)),
            request => new ImmediateOperation(request, execute, validate));

    private static string? ValidatePhase(CommandActor actor, GameState world)
    {
        if (world.Globals.Multiplayer != GameMode.SinglePlayer || actor.Source is not null
            || actor.Civilization.Value != world.Globals.TurnCiv
            || !world.Globals.HumanPlayers.Contains(actor.Civilization.Value) || !world.TurnLoopActive
            || !world.PlayerPhaseActive)
        {
            return "player_phase_required";
        }

        return null;
    }

    private static UnitTarget UnitArgument(CommandRequest request) =>
        ((CommandValue.Unit)CommandDescriptor.Value(request.Arguments, "unit")).Value;

    private static int Number(CommandRequest request, string name) =>
        ((CommandValue.Integer)CommandDescriptor.Value(request.Arguments, name)).Value;

    private static string? ValidateUnit(CommandActor actor, CommandRequest request, GameState world)
    {
        var target = UnitArgument(request);
        var units = world.Entities.Units;
        return units.Live(target.Slot) && units.At(target.Slot).Token == target.Token
            && units.At(target.Slot).Owner == actor.Civilization ? null : "unit_not_available";
    }

    private static OperationStep SelectUnit(OperationContext context, CommandRequest request)
    {
        var target = UnitArgument(request);
        var world = context.World;
        ref var unit = ref world.Entities.Units.At(target.Slot);
        unit.Orders = UnitOrder.None;
        unit.Flags.Automated = false;
        if (unit.MovesUsed == 0)
        {
            LocalVisibility.RevealAround(world, unit, context.Process.Rules.Definitions.Units[unit.Type.Value]);
        }

        world.Globals.ActiveUnit = target.Slot;
        world.MovementMode = MovementMode.MoveUnits;
        return Commit(context, "unit.selected", new NamedValue("unit", new CommandValue.Unit(target)));
    }

    private static OperationStep MoveUnit(OperationContext context, CommandRequest request)
    {
        var target = UnitArgument(request);
        var world = context.World;
        var rules = context.Process.Rules.Definitions;
        var (code, changed) = LandMovement.Step(world, rules, target.Slot, Number(request, "direction"), context.Gameplay);
        NamedValue unit = new("unit", new CommandValue.Unit(target));
        if (code == "moved")
        {
            world.Globals.ActiveUnit = target.Slot;
            SelectReadyUnit(context);
            return Commit(context, "unit.moved", unit);
        }

        if (changed)
        {
            if (code == "movement_spent" && world.Globals.ActiveUnit == target.Slot)
            {
                SelectReadyUnit(context);
            }

            context.Effect("unit." + code, new([unit]));
        }

        return new(CommandStatus.Failed, Code: code);
    }

    private static OperationStep Commit(OperationContext context, string effect, params NamedValue[] values)
    {
        OwnedList<NamedValue> result = new(values);
        context.Effect(effect, result);
        return new(Result: result);
    }

    private sealed class ImmediateOperation(
        CommandRequest request,
        Func<OperationContext, CommandRequest, OperationStep> execute,
        Func<CommandActor, CommandRequest, GameState, string?>? validate) : IContinuation
    {
        public OperationStep Resume(OperationContext context, OperationInput input)
        {
            if (input.Kind is OperationInputKind.Cancel or OperationInputKind.Timeout)
            {
                return new(CommandStatus.Cancelled, Code: "cancelled");
            }

            string? error = ValidatePhase(context.Actor, context.World)
                ?? validate?.Invoke(context.Actor, request, context.World);
            return error is null ? execute(context, request) : new(CommandStatus.Failed, Code: error);
        }
    }
}
