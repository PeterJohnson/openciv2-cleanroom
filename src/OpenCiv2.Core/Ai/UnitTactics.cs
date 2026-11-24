using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.Ai;

/// <summary>Actual children of the shared A04 chooser. No child-result providers are used.</summary>
public sealed record UnitTacticsServices(BarbarianOrders Barbarians, GroundTacticsEntry Entry, GroundOrders Orders,
    GroundGuard Guard, AirTactics Air, NuclearTargets Nuclear, SpecialUnitDestinations Special,
    SettlementTactics Settlement, ParatrooperTactics Paratroopers, PartisanFortress Fortress,
    NavalSeaTactics Naval, GroundPartisans Partisans, TerrainWorkerTactics Workers,
    SettlerPopulationReturn Population, GroundMovementPlanner Planner);

public abstract record UnitTacticsDecision
{
    public sealed record Barbarian(BarbarianOrderDecision Decision) : UnitTacticsDecision;
    public sealed record Lifecycle(LifecycleDecision Decision) : UnitTacticsDecision;
    public sealed record Founding(FoundingDecision Decision) : UnitTacticsDecision;
    public sealed record Paradrop(ParadropDecision Decision) : UnitTacticsDecision;
    public sealed record Mission(UnitMissionDecision Decision) : UnitTacticsDecision;
    public sealed record Pillage(AttackWarning Warning) : UnitTacticsDecision;
}
public sealed record UnitTacticsReply(int? Choice = null, FoundingReply? Founding = null, ResearchReply? Research = null)
{
    public ResearchReply? Decision => Research ?? (Choice is { } value ? new(value) : null);
}

/// <summary>A04 shared active-unit chooser. Captured context survives specialized children,
/// transport retry and pending decisions. U04 executes the selected orders afterwards.</summary>
public sealed partial class UnitTactics(GameState world, RuleTables rules, UnitTacticsServices services)
{
    private GameState World => world;
    private enum Phase { Scenario, Specialized, Fortress, CityGuard, TransportWork, PlanMove }
    private sealed class Frame(UnitTacticsContext context, int damage, Phase phase)
    {
        public UnitTacticsContext Context { get; } = context;
        public int Damage { get; } = damage;
        public Phase Phase = phase;
        public NavalTransportContext Naval { get; } = NavalTransportContext.ForChooser(context);
        public int TransportPass;
    }
    public Operation Begin(UnitSlot unit, StartingPositions starts, string call) => new(this, unit, starts, call);
    public sealed class Operation : IDisposable
    {
        private readonly UnitTactics parent;
        private readonly IEnumerator<UnitTacticsDecision?> steps;
        private readonly List<Exception> failures = [];
        private UnitTacticsReply? reply;
        private bool disposed, faulted;
        public int? Result { get; internal set; }
        public UnitTacticsContext? Context { get; internal set; }
        public UnitTacticsDecision? Pending { get; private set; }
        public IReadOnlyList<Exception> PresentationFailures => failures.AsReadOnly();
        internal Operation(UnitTactics parent, UnitSlot unit, StartingPositions starts, string call)
        {
            ArgumentException.ThrowIfNullOrEmpty(call);
            this.parent = parent;
            steps = parent.Execute(this, unit, starts, call).GetEnumerator();
        }
        internal UnitTacticsReply? TakeReply() { var answer = reply; reply = null; return answer; }
        internal void Report(IEnumerable<Exception> errors) => failures.AddRange(errors);
        public UnitTacticsDecision? Resume(UnitTacticsReply? answer = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("Failed unit tactics cannot resume"); }
            if (Result is not null)
            { return null; }
            try
            {
                MapKnowledge.RequireLocal(parent.World);
                if (Pending is not null && answer is null)
                { return Pending; }
                reply = Pending is null ? null : answer;
                Pending = null;
                if (steps.MoveNext())
                { return Pending = steps.Current; }
                steps.Dispose();
                return null;
            }
            catch { faulted = true; steps.Dispose(); throw; }
        }
        public void Dispose() { disposed = true; steps.Dispose(); }
    }
    private int Finish(UnitTacticsContext c) => services.Orders.Finish(c.Unit, c.Domain, c.StayHome);
    private bool Apply(Operation op, Frame f, SpecialUnitChoice choice)
    {
        switch (choice.Route)
        {
            case SpecialUnitRoute.Continue:
                return false;
            case SpecialUnitRoute.PlanMove:
                f.Phase = Phase.PlanMove;
                break;
            case SpecialUnitRoute.Direction:
                op.Result = services.Orders.Direction(f.Context, choice.Direction);
                break;
            case SpecialUnitRoute.Finish:
                op.Result = Finish(f.Context);
                break;
            case SpecialUnitRoute.ReturnOne:
                op.Result = 1;
                break;
            case SpecialUnitRoute.ReturnZero:
                op.Result = 0;
                break;
            default:
                throw new InvalidOperationException("Unknown specialized tactics route");
        }
        return true;
    }
    private IEnumerable<UnitTacticsDecision?> Execute(Operation op, UnitSlot unit, StartingPositions starts, string call)
    {
        if (world.Entities.Units.At(unit).Owner.Value == 0)
        {
            using var child = services.Barbarians.Begin(unit, call + "/barbarian");
            while (child.Result is null)
            {
                var pending = child.ResumeDecision(op.TakeReply()?.Decision);
                if (pending is not null)
                { yield return new UnitTacticsDecision.Barbarian(pending); }
                else if (child.Result is null)
                { yield return null; }
            }
            op.Report(child.PresentationFailures);
            op.Result = child.Result;
            yield break;
        }
        var entry = services.Entry.EnterOrdinary(unit, call + "/entry");
        op.Context = entry.Context;
        if (entry.Result is { } completed)
        { op.Result = completed; yield break; }
        var c = entry.Context ?? throw new InvalidOperationException("A04 entry did not produce context");
        var recovery = entry.Recovery ?? throw new InvalidOperationException("A04 entry did not produce recovery");
        var f = new Frame(c, recovery.DamageClass, recovery.Route switch
        {
            GroundRecoveryRoute.ScenarioGuard => Phase.Scenario,
            GroundRecoveryRoute.CityGuard => Phase.CityGuard,
            GroundRecoveryRoute.TransportWork => Phase.TransportWork,
            GroundRecoveryRoute.PlanMove => Phase.PlanMove,
            _ => throw new InvalidOperationException("A04 entry did not complete its terminal route")
        });
        while (op.Result is null)
        {
            switch (f.Phase)
            {
                case Phase.Scenario:
                    switch (services.Guard.Scenario(c))
                    {
                        case GroundGuardRoute.Hold:
                            op.Result = services.Orders.Direction(c, 8);
                            break;
                        case GroundGuardRoute.Specialized:
                            f.Phase = Phase.Specialized;
                            break;
                        case GroundGuardRoute.PartisanFortress:
                            f.Phase = Phase.Fortress;
                            break;
                        default:
                            throw new InvalidOperationException("Unknown scenario gate route");
                    }
                    break;
                case Phase.Specialized:
                    foreach (var pending in Specialized(op, f, starts, call))
                    { yield return pending; }
                    break;
                case Phase.Fortress:
                    services.Fortress.Apply(c);
                    f.Phase = services.Guard.ForeignFortress(c) switch
                    {
                        GroundGuardRoute.CityGuard => Phase.CityGuard,
                        GroundGuardRoute.PlanMove => Phase.PlanMove,
                        _ => throw new InvalidOperationException("Unknown foreign fortress route")
                    };
                    break;
                case Phase.CityGuard:
                    switch (services.Guard.City(c))
                    {
                        case GroundGuardRoute.Hold:
                            op.Result = services.Orders.Direction(c, 8);
                            break;
                        case GroundGuardRoute.PlanMove:
                            f.Phase = Phase.PlanMove;
                            break;
                        case GroundGuardRoute.TransportWork:
                            f.Phase = Phase.TransportWork;
                            break;
                        default:
                            throw new InvalidOperationException("Unknown city guard route");
                    }
                    break;
                case Phase.TransportWork:
                    foreach (var pending in TransportWork(op, f, call))
                    { yield return pending; }
                    break;
                case Phase.PlanMove:
                    using (var child = services.Planner.Begin(c, call + "/plan"))
                    {
                        while (!child.Completed)
                        {
                            var pending = child.ResumeDecision(op.TakeReply()?.Decision);
                            if (pending is not null)
                            { yield return new UnitTacticsDecision.Mission(pending); }
                            else if (!child.Completed)
                            { yield return null; }
                        }
                        op.Result = child.Result!.Direction is { } direction ? services.Orders.Direction(c, direction) : Finish(c);
                    }
                    break;
            }
        }
    }
}
