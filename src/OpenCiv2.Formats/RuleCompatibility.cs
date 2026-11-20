using System.Runtime.CompilerServices;
using OpenCiv2.Base;
using OpenCiv2.Core.Rules;
namespace OpenCiv2.Formats;
/// <summary>Unassigned classic ability bits follow the immutable adapter input, never installed gameplay tables.</summary>
public static class RuleCompatibility
{
    private static readonly ConditionalWeakTable<RuleInput, OwnedList<uint>> retained = new();
    internal static RuleInput Attach(RuleInput input, IEnumerable<uint> bits)
    {
        retained.Add(input, new(bits.Select(v => v & ~StateConversion.UnitAbilitiesMask)));
        return input;
    }
    public static uint RetainedUnitFlags(RuleInput input, int slot) => retained.TryGetValue(input, out var bits) ? bits[slot] : 0;
}
