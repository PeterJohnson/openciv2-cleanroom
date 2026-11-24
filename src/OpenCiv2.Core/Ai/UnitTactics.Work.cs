namespace OpenCiv2.Core.Ai;

public sealed partial class UnitTactics
{
    private IEnumerable<UnitTacticsDecision?> TransportWork(Operation op, Frame f, string call)
    {
        var c = f.Context;
        int pass = f.TransportPass++;
        var sea = services.Naval.Choose(f.Naval, f.Damage, call + "/sea-" + pass);
        switch (sea.Route)
        {
            case NavalRoute.Direction:
                op.Result = services.Orders.Direction(c, sea.Direction);
                yield break;
            case NavalRoute.Finish:
                op.Result = Finish(c);
                yield break;
            case NavalRoute.ReturnOne:
                op.Result = 1;
                yield break;
            case NavalRoute.Continue:
                break;
            default:
                throw new InvalidOperationException("Unknown sea tactics route");
        }
        using (var child = services.Partisans.BeginPillage(c))
        {
            if (child is not null)
            {
                while (!child.Completed)
                {
                    if (child.Resume(op.TakeReply()?.Choice) is { } pending)
                    { yield return new UnitTacticsDecision.Pillage(pending); }
                }
                op.Report(child.PresentationFailures);
                op.Result = Finish(c);
                yield break;
            }
        }
        var target = services.Partisans.Targets(c);
        if (target.Route == PartisanTargetRoute.Direction)
        { op.Result = services.Orders.Direction(c, target.Direction); yield break; }
        if (target.Route == PartisanTargetRoute.PlanMove)
        { f.Phase = Phase.PlanMove; yield break; }
        services.Settlement.ClassifyFortress(c);
        if (Apply(op, f, services.Workers.Choose(c)))
        { yield break; }
        switch (services.Orders.RetainedGoto(c))
        {
            case GroundOrderRoute.Finish:
                op.Result = Finish(c);
                yield break;
            case GroundOrderRoute.RetryTransport:
                f.Phase = Phase.TransportWork;
                yield break;
            case GroundOrderRoute.Continue:
                break;
            default:
                throw new InvalidOperationException("Unknown retained Goto route");
        }
        switch (services.Orders.DefenderFortify(c))
        {
            case GroundOrderRoute.Hold:
                op.Result = services.Orders.Direction(c, 8);
                yield break;
            case GroundOrderRoute.PlanMove:
                f.Phase = Phase.PlanMove;
                yield break;
            case GroundOrderRoute.Continue:
                break;
            default:
                throw new InvalidOperationException("Unknown defender route");
        }
        using (var child = services.Population.Begin(c, call + "/population"))
        {
            while (child.Result is null)
            {
                var pending = child.ResumeDecision(op.TakeReply()?.Decision);
                if (pending is not null)
                { yield return new UnitTacticsDecision.Lifecycle(pending); }
                else if (child.Result is null)
                { yield return null; }
            }
            op.Report(child.PresentationFailures);
            if (Apply(op, f, child.Result.Value))
            { yield break; }
        }
        f.Phase = Phase.PlanMove;
    }
}
