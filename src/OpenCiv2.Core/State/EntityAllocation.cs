using OpenCiv2.Contracts;

namespace OpenCiv2.Core.State;

/// <summary>F02 §§2–3 process-owned independent token sequences, including zero on wrap.</summary>
public sealed class ProcessTokens
{
    public uint NextUnit { get; set; } = 1;
    public uint NextCity { get; set; } = 1;

    public UnitToken AssignUnit()
    {
        var token = new UnitToken(NextUnit);
        NextUnit = unchecked(NextUnit + 1);
        return token;
    }

    public CityToken AssignCity()
    {
        var token = new CityToken(NextCity);
        NextCity = unchecked(NextCity + 1);
        return token;
    }
}
/// <summary>
/// Reserves unit slots under F02 limits while leaving token assignment and gameplay initialization to the
/// owner.
/// </summary>
public static class EntityAllocation
{
    private const int MilitaryReservationThreshold = 1948;
    private static void Admit(CivilizationSlot owner, uint end, int length)
    {
        if (owner.Value >= EntityLimits.Civilizations || end > EntityLimits.Units || length != end)
        {
            throw new ArgumentException("Unit reservation requires owner 0..7 and a complete scan view");
        }
    }

    /// <summary>
    /// Reuses holes directly and applies capacity and human/AI limits only when extending the scan.
    /// </summary>
    private static UnitSlot? Finish(
        uint firstFree,
        ref uint end,
        CivilizationSlot owner,
        short militaryCount,
        CivilizationSet effectiveHumans)
    {
        if (firstFree == end)
        {
            bool human = effectiveHumans.Contains(owner.Value);
            if (end >= EntityLimits.Units || militaryCount > MilitaryReservationThreshold ||
                (!human && end >= EntityLimits.NonHumanUnits))
            {
                return null;
            }

            ++end;
        }
        return new((ushort)firstFree);
    }

    /// <summary>F02 §3 reservation only: no token assignment, payload initialization, census or callback.</summary>
    public static UnitSlot? ReserveUnit(
        ReadOnlySpan<UnitToken> scanned,
        ref uint end,
        CivilizationSlot owner,
        short militaryCount,
        CivilizationSet effectiveHumans)
    {
        Admit(owner, end, scanned.Length);
        uint slot = 0;
        while (slot < end && scanned[(int)slot].Occupied)
        {
            ++slot;
        }

        return Finish(slot, ref end, owner, militaryCount, effectiveHumans);
    }

    /// <summary>
    /// Reserves the first free unit slot and updates the pool scan bound without changing its stored payload.
    /// </summary>
    public static UnitSlot? ReserveUnit(
        UnitPool pool,
        CivilizationSlot owner,
        short militaryCount,
        CivilizationSet effectiveHumans)
    {
        uint end = (uint)pool.End;
        Admit(owner, end, pool.Scanned.Length);
        uint slot = 0;
        while (slot < end && pool.Scanned[(int)slot].Token.Occupied)
        {
            ++slot;
        }

        var result = Finish(slot, ref end, owner, militaryCount, effectiveHumans);
        pool.SetEnd((int)end);
        return result;
    }
}
