using OpenCiv2.Base;
using OpenCiv2.Contracts;

namespace OpenCiv2.Core.Rules;

public static class RuleNormalization
{
    public static byte Code8(int value) => unchecked((byte)value);

    public static sbyte Signed8(int value) => unchecked((sbyte)value);

    public static short Signed16(int value) => unchecked((short)value);

    public static byte Movement(int points, byte road) => Code8(Code8(points) * road);

    public static byte ClassicHitPoints(int groups) => Code8(Code8(groups) * 10);

    public static byte UnitCost(int value) => Code8(Math.Clamp(value, 1, 200));

    public static byte LeaderColor(int value) => Code8(Math.Clamp((int)Signed16(value), 1, EntityLimits.PlayerCivilizations));

    public static byte LeaderStyle(int value) => Code8(Math.Clamp((int)Signed16(value), 0, 3));

    public static AdvanceReference FollowDisabled(AdvanceReference reference, IReadOnlyList<AdvanceRule> advances)
    {
        Span<bool> visited = stackalloc bool[RuleLimits.Advances];
        visited.Clear();
        while ((short)reference >= 0)
        {
            int index = (short)reference;
            if (index >= RuleLimits.Advances)
            {
                throw new ArgumentException("Advance reference outside table");
            }

            if (advances[index].Enabled)
            {
                break;
            }

            if (visited[index])
            {
                throw new ArgumentException("Disabled advance chain does not terminate");
            }

            visited[index] = true;
            reference = advances[index].Prerequisites[0];
        }
        return reference;
    }

    public static ushort FixAdvanceReferences(AdvanceRule[] advances)
    {
        ushort count = 0;
        for (int i = 0; i < advances.Length; ++i)
        {
            if (advances[i].Enabled)
            {
                ++count;
                var prerequisites = advances[i].Prerequisites.ToArray();
                for (int j = 0; j < RuleLimits.AdvancePrerequisites; ++j)
                {
                    prerequisites[j] = FollowDisabled(prerequisites[j], advances);
                    advances[i] = advances[i] with { Prerequisites = new(prerequisites) };
                }
            }
        }

        return count;
    }
}
