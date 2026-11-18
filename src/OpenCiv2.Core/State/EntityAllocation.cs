using OpenCiv2.Contracts;

namespace OpenCiv2.Core.State;

/// <summary>F02 §§2–3 process-owned independent token sequences, including zero on wrap.</summary>
public sealed class ProcessTokens
{
    public uint NextUnit { get; set; } = 1;
    public uint NextCity { get; set; } = 1;
    public UnitToken AssignUnit() { var token = new UnitToken(NextUnit); NextUnit = unchecked(NextUnit + 1); return token; }
    public CityToken AssignCity() { var token = new CityToken(NextCity); NextCity = unchecked(NextCity + 1); return token; }
}
public static class EntityAllocation
{
    private static void Admit(CivilizationSlot owner, uint end, int length)
    {
        if (owner.Value >= EntityLimits.Civilizations || end > EntityLimits.Units || length != end)
            throw new ArgumentException("Unit reservation requires owner 0..7 and a complete scan view");
    }
    private static UnitSlot? Finish(uint firstFree, ref uint end, CivilizationSlot owner, short militaryCount, byte effectiveHumans)
    {
        if (firstFree == end)
        {
            bool human = (effectiveHumans & (1 << owner.Value)) != 0;
            if (end >= EntityLimits.Units || militaryCount > 1948 || (!human && end >= 2040)) return null;
            ++end;
        }
        return new((ushort)firstFree);
    }
    /// <summary>F02 §3 reservation only: no token assignment, payload initialization, census or callback.</summary>
    public static UnitSlot? ReserveUnit(ReadOnlySpan<UnitToken> scanned, ref uint end, CivilizationSlot owner, short militaryCount, byte effectiveHumans)
    {
        Admit(owner, end, scanned.Length); uint slot = 0;
        while (slot < end && scanned[(int)slot].Occupied) ++slot;
        return Finish(slot, ref end, owner, militaryCount, effectiveHumans);
    }
    public static UnitSlot? ReserveUnit(UnitPool pool, CivilizationSlot owner, short militaryCount, byte effectiveHumans)
    {
        uint end = (uint)pool.End; Admit(owner, end, pool.Scanned.Length); uint slot = 0;
        while (slot < end && pool.Scanned[(int)slot].Token.Occupied) ++slot;
        var result = Finish(slot, ref end, owner, militaryCount, effectiveHumans); pool.SetEnd((int)end); return result;
    }
}
