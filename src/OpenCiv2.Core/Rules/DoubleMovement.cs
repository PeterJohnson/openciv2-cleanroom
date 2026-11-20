using OpenCiv2.Contracts;

namespace OpenCiv2.Core.Rules;

public static class DoubleMovement
{
    /// <summary>F03 §8: transform the current land table, preserving byte wrap and signed removal.
    /// The complete table and flag precede redraw; existing units are not changed.</summary>
    public static void Apply(GameMode mode, bool enabled, RuleState rules, Action<int>? redraw)
    {
        ArgumentNullException.ThrowIfNull(rules);
        if (mode == GameMode.SinglePlayer || enabled == rules.DoubleMovementApplied)
        {
            return;
        }

        rules.DoubleMovementApplied = enabled;
        var units = rules.Definitions.Units;
        for (int i = 0; i < units.Length; ++i)
        {
            var unit = units[i];
            if (unit.Domain != RuleDomain.Land)
            {
                continue;
            }

            units[i] = unit with
            {
                Movement = enabled
                        ? unchecked((byte)(unit.Movement * 2))
                        : unchecked((byte)(unchecked((sbyte)unit.Movement) / 2))
            };
        }
        // A missing or failing child retains the already completed transform.
        if (redraw is null)
        {
            throw new InvalidOperationException("Double movement requires a redraw service");
        }

        redraw(1);
    }
}
