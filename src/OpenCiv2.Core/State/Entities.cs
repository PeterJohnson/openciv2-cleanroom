using OpenCiv2.Base;
using OpenCiv2.Contracts;

namespace OpenCiv2.Core.State;

/// <summary>F02 §§3,5: live logical unit state. File/wire layouts are separate codecs.</summary>
public record struct Unit
{
    public Unit()
    {
        Orders = UnitOrder.None;
    }

    public UnitToken Token;
    public MapPoint Position;
    public UnitStatus Flags;
    public UnitType Type;
    public CivilizationSlot Owner;
    public CivilizationSet Visibility;
    public byte MovesUsed, Damage, LastDirection, AiTask, Counter, AuxiliaryTimer;
    public UnitOrder Orders;
    public CitySlot? Home;
    public MapPoint Destination;
    public UnitSlot? Carrier;
    public UnitSlot? Previous, Next;
}

public enum SpecialistKind { Unused, Entertainer, TaxCollector, Scientist }

/// <summary>F02 §§3,6: live city values. Embedded arrays and strings make value copies independent.</summary>
public record struct City
{
    public City()
    {
        Name = string.Empty;
    }

    public CityToken Token;
    public MapPoint Position;
    public CityStatus Flags;
    public CivilizationSlot Owner, Founder;
    public sbyte Size;
    public byte TurnsCaptured;
    public CivilizationSet KnownTo;
    public CivilizationArray<byte> RememberedSize;
    public SpecialistArray<SpecialistKind> Specialists;
    public byte SpecialistCount;
    public short FoodBox, ShieldBox, BaseTrade;
    public string Name;
    public CityWorkSet WorkedTiles;
    public ImprovementSet Improvements;
    public sbyte Production;
    public byte RouteCount;
    public TradeRouteArray<sbyte> Supplies, Demands, RouteGoods;
    public TradeRouteArray<CitySlot?> RoutePartners;
    public short Science, Tax, Trade;
    public byte TotalFood, TotalShields, Happy, Unhappy;
}

/// <summary>Fixed-capacity stable backing; scan bounds and occupancy are separate. Borrowed references stay
/// synchronous.</summary>
public abstract class SlotPool<TRecord, TSlot> where TRecord : struct
{
    private readonly TRecord[] records;
    public int End { get; private set; }

    protected SlotPool(int capacity)
    {
        records = new TRecord[capacity];
        for (int i = 0; i < capacity; ++i)
        {
            records[i] = new TRecord();
        }
    }

    protected abstract int Index(TSlot slot);

    protected abstract bool Occupied(in TRecord record);

    protected abstract void ClearToken(ref TRecord record);

    /// <summary>
    /// Changes the scan bound without allocating, initializing or changing the occupancy of records.
    /// </summary>
    public void SetEnd(int end)
    {
        if (end < 0 || end > records.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(end), "Pool scan bound exceeds supplied backing");
        }

        End = end;
    }

    /// <summary>
    /// Borrows the full fixed backing, including records beyond the current scan bound.
    /// </summary>
    public Span<TRecord> Storage => records;

    /// <summary>
    /// Borrows the active scan range, which may include unoccupied slots.
    /// </summary>
    public Span<TRecord> Scanned => records.AsSpan(0, End);

    /// <summary>
    /// Borrows a record by backing index without requiring that it be occupied or inside the scan range.
    /// </summary>
    public ref TRecord At(TSlot slot)
    {
        int index = Index(slot);
        if (index < 0 || index >= records.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(slot));
        }

        return ref records[index];
    }

    /// <summary>
    /// Tests both membership in the current scan and record occupancy.
    /// </summary>
    public bool Live(TSlot slot)
    {
        int index = Index(slot);
        return index >= 0 && index < End && Occupied(in records[index]);
    }

    /// <summary>
    /// Clears an occupied record's token and shortens the scan by one only when deleting its final slot.
    /// </summary>
    /// <remarks>
    /// Retained payload and earlier holes are preserved. This completes pool bookkeeping; it does not run
    /// gameplay deletion effects.
    /// </remarks>
    public bool FinishDeletion(int index)
    {
        if (index < 0)
        {
            return false;
        }

        if (index >= End)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Deletion outside current pool scan");
        }

        if (!Occupied(in records[index]))
        {
            return false;
        }

        ClearToken(ref records[index]);
        // The scan boundary is retained state: deleting its tail removes one slot, not every earlier hole.
        if (index == End - 1)
        {
            --End;
        }

        return true;
    }
}
/// <summary>
/// Fixed unit storage with token-based occupancy and stable slot indices.
/// </summary>
public sealed class UnitPool() : SlotPool<Unit, UnitSlot>(EntityLimits.Units)
{
    protected override int Index(UnitSlot slot) => slot.Value;

    protected override bool Occupied(in Unit record) => record.Token.Occupied;

    protected override void ClearToken(ref Unit record) => record.Token = new(0);
}
/// <summary>
/// Fixed city storage with a separate gameplay creation limit.
/// </summary>
public sealed class CityPool() : SlotPool<City, CitySlot>(EntityLimits.Cities)
{
    protected override int Index(CitySlot slot) => slot.Value;

    protected override bool Occupied(in City record) => record.Token.Occupied;

    protected override void ClearToken(ref City record) => record.Token = new(0);
}
/// <summary>
/// Owns unit and city storage and the explicit scan/lifetime transitions used by setup and loading.
/// </summary>
public sealed class EntityPools
{
    public UnitPool Units { get; } = new();
    public CityPool Cities { get; } = new();

    // F05 entity portions only. Fresh-world replacement is a separate owning boundary.
    /// <summary>
    /// Invalidates entity tokens and unit links for F05 setup without replacing the rest of the world.
    /// </summary>
    public void InvalidateForNewSession()
    {
        foreach (ref var unit in Units.Storage)
        {
            unit.Token = new(0);
            unit.Previous = null;
            unit.Next = null;
        }
        foreach (ref var city in Cities.Storage)
        {
            city.Token = new(0);
        }

        Units.SetEnd(0);
        Cities.SetEnd(0);
    }

    /// <summary>
    /// Resets both scan bounds and the selected slot without clearing the stored records.
    /// </summary>
    public void ResetScans(ref UnitSlot? selected)
    {
        Units.SetEnd(0);
        Cities.SetEnd(0);
        selected = new(0);
    }

    // F06 caller applies only after BOTH record groups succeed.
    /// <summary>
    /// Assigns fresh process tokens after both loaded entity groups have succeeded.
    /// </summary>
    /// <remarks>
    /// Legacy records without IDs treat every scanned slot as occupied; newer records preserve unoccupied
    /// slots.
    /// </remarks>
    public void RenewLoadedLifetimes(ProcessTokens tokens, bool legacyWithoutIds)
    {
        foreach (ref var unit in Units.Scanned)
        {
            if (legacyWithoutIds || unit.Token.Occupied)
            {
                unit.Token = tokens.AssignUnit();
            }
        }

        foreach (ref var city in Cities.Scanned)
        {
            if (legacyWithoutIds || city.Token.Occupied)
            {
                city.Token = tokens.AssignCity();
            }
        }
    }
}
/// <summary>
/// Retains U01 stack-operation flags, suppression and remote replies across cooperative polls.
/// </summary>
public sealed class UnitStackState
{
    public int Suppression;
    public bool AnimationActive, DetachActive, AttachActive, RelocateActive, BottomActive;
    public bool CargoActive, LiftActive;
    public int DetachReply, AttachReply, RelocateReply, BottomReply;
}
/// <summary>
/// Retains the active flag and reply for a potentially suspended unit deletion.
/// </summary>
public sealed class UnitDeletionState
{
    public bool Active;
    public bool CargoActive, DestroyActive;
    public int Reply;
}
/// <summary>
/// Retains the selected city and mood totals used by cached and full calculation paths.
/// </summary>
public sealed class CityMoodWorkspace
{
    public CitySlot? City;
    public int Happy, Unhappy;

    // F02 §9 / C03: cached path never selects a city; full path retains child effects.
    /// <summary>
    /// Returns cached mood without selecting a city, or delegates a full calculation to the owning pipeline.
    /// </summary>
    public int Calculate(CitySlot? city, bool full, Func<int>? pipeline = null)
    {
        if (city is null)
        {
            return 0;
        }

        if (!full)
        {
            return unchecked(Happy - Unhappy);
        }

        City = city;
        if (pipeline is null)
        {
            throw new ArgumentException("Full city calculation requires its owning pipeline", nameof(pipeline));
        }

        return pipeline();
    }
}
