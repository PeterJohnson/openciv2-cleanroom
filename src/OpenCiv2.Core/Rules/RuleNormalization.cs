using OpenCiv2.Base;

namespace OpenCiv2.Core.Rules;

public static class RuleNormalization
{
    public static byte Code8(int value) => unchecked((byte)value);
    public static sbyte Signed8(int value) => unchecked((sbyte)value);
    public static short Signed16(int value) => unchecked((short)value);
    private static readonly int[] Low = [1, 1, 0, 4, 4, 0, 0, 4, 10, 4, 4, 3, 5, 0, 0, 0, 1, 0, 0, 4, 25, 0];
    private static readonly int[] High = [10, 100, 10, 20, 20, 10, 10, 12, 100, 50, 50, 10, 100, 8, 8, 8, 20, 100, 100, 100, 200, 10];
    public static byte CosmicValue(CosmicParameter parameter, int value)
    {
        int i = (int)parameter; byte result = Code8(Math.Clamp(value, Low[i], High[i]));
        if (parameter == CosmicParameter.FoodBoxRows && result % 2 != 0) ++result;
        return result;
    }
    public static CosmicRules Cosmic(IReadOnlyList<int> values)
    {
        if (values.Count != 22) throw new ArgumentException("Expected 22 cosmic operands");
        return new(new(Enumerable.Range(0, 22).Select(i => CosmicValue((CosmicParameter)i, values[i]))));
    }
    public static byte Movement(int points, byte road) => Code8(Code8(points) * road);
    public static byte ClassicHitPoints(int groups) => Code8(Code8(groups) * 10);
    public static byte UnitCost(int value) => Code8(Math.Clamp(value, 1, 200));
    public static byte LeaderColor(int value) => Code8(Math.Clamp((int)Signed16(value), 1, 7));
    public static byte LeaderStyle(int value) => Code8(Math.Clamp((int)Signed16(value), 0, 3));
    public static AdvanceReference FollowDisabled(AdvanceReference reference, IReadOnlyList<AdvanceRule> advances)
    {
        Span<bool> visited = stackalloc bool[100]; visited.Clear();
        while ((short)reference >= 0)
        {
            int index = (short)reference;
            if (index >= 100) throw new ArgumentException("Advance reference outside table");
            if (advances[index].Enabled) break;
            if (visited[index]) throw new ArgumentException("Disabled advance chain does not terminate");
            visited[index] = true; reference = advances[index].Prerequisites[0];
        }
        return reference;
    }
    public static ushort FixAdvanceReferences(AdvanceRule[] advances)
    {
        ushort count = 0;
        for (int i = 0; i < advances.Length; ++i)
            if (advances[i].Enabled)
            {
                ++count; var prerequisites = advances[i].Prerequisites.ToArray();
                for (int j = 0; j < 2; ++j)
                {
                    prerequisites[j] = FollowDisabled(prerequisites[j], advances);
                    advances[i] = advances[i] with { Prerequisites = new(prerequisites) };
                }
            }
        return count;
    }
}
