using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Session;

public static partial class LocalPlayCommands
{
    public const string Wait = "unit.wait";
    public const string Sleep = "unit.sleep";
    public const string Home = "unit.home";

    private static void RegisterUnitOrders(SessionRuntime runtime)
    {
        runtime.RegisterCommand(Define(Wait, "Wait without spending movement and activate the next unit.",
            [new("unit", ValueKind.Unit)], [new("unit", ValueKind.Unit)], WaitUnit, ValidateUnit));
        runtime.RegisterCommand(Define(Sleep, "Put an owned unit to sleep without spending movement.",
            [new("unit", ValueKind.Unit)], [new("unit", ValueKind.Unit)], SleepUnit, ValidateUnit));
        runtime.RegisterCommand(Define(Home, "Set an owned unit's home to the city at its position.",
            [new("unit", ValueKind.Unit)], [new("unit", ValueKind.Unit)], HomeUnit, ValidateUnit));
    }
    private sealed class PlayerOrderEffects(OperationContext context) : IUnitPlayerOrderEffects
    {
        // The authored view refreshes from the committed projection, without invoking gameplay.
        public void Redraw(MapPoint position) => context.Publish();
        public void UnitChanged(UnitSlot unit) => context.Publish();
        public void RefreshHome(CitySlot? city) => context.Publish();
    }
    private static UnitPlayerOrders PlayerOrders(OperationContext context) => new(context.World, context.Process.Rules.Definitions,
        new(context.World, new(100000, 64)), new PlayerOrderEffects(context));
    private static OperationStep OrderResult(OperationContext context, UnitTarget target, UnitPlayerOrderResult result, string effect)
    {
        if (!result.Accepted)
        { return new(CommandStatus.Failed, Code: result.Refusal == UnitOrderRefusal.CaravanHome ? "caravan_home_fixed" : "order_not_available"); }
        var completed = Commit(context, effect, new NamedValue("unit", new CommandValue.Unit(target)));
        return result.PresentationFailures.Count == 0 ? completed
            : completed with { Status = CommandStatus.Failed, Code = "unit_order_presentation_failed" };
    }
    private static OperationStep SleepUnit(OperationContext context, CommandRequest request)
    {
        var target = UnitArgument(request);
        if (!UnitActivation.Selectable(context.World, context.Process.Rules.Definitions, target.Slot.Value))
        { return new(CommandStatus.Failed, Code: "order_not_available"); }
        var result = PlayerOrders(context).Sleep(target.Slot);
        if (result.Accepted)
        { SelectReadyUnit(context); }
        return OrderResult(context, target, result, "unit.sleeping");
    }
    private static OperationStep HomeUnit(OperationContext context, CommandRequest request)
    {
        var target = UnitArgument(request);
        if (CityQueries.At(context.World, context.World.Entities.Units.At(target.Slot).Position) is null)
        { return new(CommandStatus.Failed, Code: "recovery_orders_unavailable"); }
        return OrderResult(context, target, PlayerOrders(context).Home(target.Slot), "unit.home_changed");
    }
    private static OperationStep WaitUnit(OperationContext context, CommandRequest request)
    {
        var target = UnitArgument(request);
        var world = context.World;
        var rules = context.Process.Rules.Definitions;
        if (!UnitActivation.Selectable(world, rules, target.Slot.Value))
        { return new(CommandStatus.Failed, Code: "order_not_available"); }
        world.Globals.ActiveUnit = target.Slot;
        var stacks = new StackOperations(world, new(100000, 64));
        var revelation = new UnitRevelation(world, rules, stacks, new LocalCreation(world, rules, stacks));
        var selection = new UnitSelection(world, rules, new(), revelation, LocalSelectionPresentation.Instance);
        using var operation = PlayerOrders(context).Wait(selection, world.Entities.Units.At(target.Slot).Position, "local-wait");
        while (!operation.Completed)
        {
            if (operation.Resume() is not null)
            { throw new InvalidOperationException("Foreign contact is outside the authored local profile"); }
        }
        world.Globals.TransientFlags.EndOfTurn = world.Globals.ActiveUnit is null;
        ContinueReadyOrders(context);
        return OrderResult(context, target, new(null, operation.PresentationFailures), "unit.waited");
    }
}
