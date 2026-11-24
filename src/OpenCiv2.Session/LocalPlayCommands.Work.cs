using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Session;

public static partial class LocalPlayCommands
{
    public const string Work = "unit.work";
    public static OwnedList<PlayOption> WorkJobs { get; } = new(
    [
        new((int)UnitOrder.Road, "Build road or railroad"), new((int)UnitOrder.Irrigate, "Irrigate"),
        new((int)UnitOrder.Mine, "Mine"), new((int)UnitOrder.Fortress, "Build fortress"),
        new((int)UnitOrder.Airbase, "Build airbase"), new((int)UnitOrder.Transform, "Transform terrain"),
        new((int)UnitOrder.Clean, "Clean pollution")
    ]);

    private static void RegisterWorkCommands(SessionRuntime runtime) => runtime.RegisterCommand(Define(Work,
        "Begin terrain work: 4 fortress, 5 road, 6 irrigation, 7 mine, 8 transform, 9 clean pollution, 10 airbase.",
        [new("unit", ValueKind.Unit), new("job")], [new("unit", ValueKind.Unit)], WorkUnit, ValidateUnit));

    private static OperationStep WorkUnit(OperationContext context, CommandRequest request)
    {
        var target = UnitArgument(request);
        int job = Number(request, "job");
        if (job is < (int)UnitOrder.Fortress or > (int)UnitOrder.Airbase
            || !UnitActivation.Selectable(context.World, context.Process.Rules.Definitions, target.Slot.Value))
        {
            return new(CommandStatus.Failed, Code: "work_not_available");
        }

        var effects = new LocalWorkEffects(context);
        var work = new TerrainWorkOrders(context.World, context.Process.Rules,
            new(context.World, new(5_000_000, 64)), effects);
        if (!work.Request(target.Slot, (UnitOrder)job))
        {
            return new(CommandStatus.Failed, Code: "work_" + effects.Rejection);
        }

        SelectReadyUnit(context);
        return Commit(context, "unit.work_started", new NamedValue("unit", new CommandValue.Unit(target)));
    }

    /// <summary>The authored U04 activation loop handles fortification and W04 jobs before
    /// yielding a manual unit. Goto/AI continuation requires the general movement caller.</summary>
    private static void SelectReadyUnit(OperationContext context)
    {
        SelectReadyUnit(context.World, context.Process.Rules.Definitions);
        ContinueReadyOrders(context);
    }

    private static void ContinueReadyOrders(OperationContext context)
    {
        var world = context.World;
        var rules = context.Process.Rules.Definitions;
        var effects = new LocalWorkEffects(context);
        var work = new TerrainWorkOrders(world, context.Process.Rules, new(world, new(5_000_000, 64)), effects);
        while (true)
        {
            if (world.Globals.ActiveUnit is not { } slot)
            {
                return;
            }

            var order = world.Entities.Units.At(slot).Orders;
            if (order == UnitOrder.Fortifying)
            {
                new UnitFortification(world, effects.CityChanged, point => effects.Redraw(point, 0), effects.UnitChanged).Apply(slot);
            }
            else if (TerrainWorkOrders.IsWork(order))
            {
                work.Execute(slot, order);
            }
            else
            {
                return;
            }
            SelectReadyUnit(world, rules);
        }
    }

    private sealed class LocalWorkEffects(OperationContext context) : ITerrainWorkEffects
    {
        public TerrainWorkProblem? Rejection { get; private set; }
        // The current profile admits a Pioneer only before its first city is founded.
        // There is consequently no open city selection on this work path.
        public CitySlot? CityToRefresh => null;
        public void Notice(UnitSlot unit, UnitOrder job, TerrainWorkProblem problem)
        {
            Rejection = problem;
            context.Effect("unit.work_unavailable", new([
                new("unit", new CommandValue.Unit(new(unit, context.World.Entities.Units.At(unit).Token))),
                new("reason", new CommandValue.Text(problem.ToString()))]));
        }
        public void Redraw(MapPoint point, int radius) => context.Effect("map.changed", new([
            new("x", new CommandValue.Integer(point.X)), new("y", new CommandValue.Integer(point.Y)),
            new("radius", new CommandValue.Integer(radius))]));
        public void RedrawStatus() => context.Effect("unit.work_completed", new([]));
        public void UnitChanged(UnitSlot unit) => context.Effect("unit.order_changed", new([
            new("unit", new CommandValue.Unit(new(unit, context.World.Entities.Units.At(unit).Token)))]));
        public void CityChanged(CitySlot city) => context.Effect("city.work_changed", new([
            new("city", new CommandValue.City(new(city, context.World.Entities.Cities.At(city).Token)))]));
    }
}
