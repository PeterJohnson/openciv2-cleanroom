using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Ai;

public sealed partial class UnitTactics
{
    private IEnumerable<UnitTacticsDecision?> Specialized(Operation op, Frame f, StartingPositions starts, string call)
    {
        var c = f.Context;
        if (c.Domain == RuleDomain.Air && Apply(op, f, services.Air.Choose(c)))
        { yield break; }
        if (unchecked((sbyte)rules.Units[c.Type].Attack) > 98)
        {
            int direction = services.Nuclear.Choose(c.Unit);
            c.Position = world.Entities.Units.At(c.Unit).Position;
            op.Result = services.Orders.Direction(c, direction);
            yield break;
        }
        if (c.Role is (int)RuleRole.Diplomacy or (int)RuleRole.Trade)
        {
            using var child = c.Role == (int)RuleRole.Diplomacy ? services.Special.Diplomat(c, call + "/diplomat")
                : services.Special.Caravan(c, call + "/caravan");
            while (child.Result is null)
            {
                var pending = child.ResumeDecision(op.TakeReply()?.Decision);
                if (pending is not null)
                { yield return new UnitTacticsDecision.Lifecycle(pending); }
                else if (child.Result is null)
                { yield return null; }
            }
            if (Apply(op, f, child.Result.Value))
            { yield break; }
        }
        using (var child = services.Settlement.Site(c, starts, call + "/site"))
        {
            while (child.Result is null)
            {
                var pending = child.Resume(op.TakeReply()?.Founding);
                if (pending is not null)
                { yield return new UnitTacticsDecision.Founding(pending); }
                else if (child.Result is null)
                { yield return null; }
            }
            op.Report(child.PresentationFailures);
            if (Apply(op, f, child.Result.Value))
            { yield break; }
        }
        if (rules.Units[world.Entities.Units.At(c.Unit).Type.Value].Abilities.Paradrop)
        {
            using var child = services.Paratroopers.Begin(c, call + "/paratrooper");
            while (child.Result is null)
            {
                var pending = child.Resume(op.TakeReply()?.Research);
                if (pending is not null)
                { yield return new UnitTacticsDecision.Paradrop(pending); }
                else if (child.Result is null)
                { yield return null; }
            }
            op.Report(child.PresentationFailures);
            if (Apply(op, f, child.Result.Value))
            { yield break; }
        }
        f.Phase = Phase.Fortress;
    }
}
