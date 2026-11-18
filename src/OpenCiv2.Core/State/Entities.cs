using OpenCiv2.Base;
using OpenCiv2.Contracts;

namespace OpenCiv2.Core.State;

public readonly record struct UnitLink(short Value)
{
    public UnitLink() : this(-1) { }
    public UnitSlot? Slot => Value < 0 ? null : new((ushort)Value);
}
public readonly record struct CityLink(short Value)
{
    public CityLink() : this(-1) { }
    public CitySlot? Slot => Value < 0 ? null : new((ushort)Value);
}

/// <summary>F02 §§3,5: live logical unit state. File/wire layouts are separate codecs.</summary>
public record struct Unit
{
    public Unit() { Orders = UnitOrder.None; Previous = new(); Next = new(); }
    public UnitToken Token;
    public short X, Y;
    public ushort Flags;
    public UnitType Type;
    public CivilizationSlot Owner;
    public byte MovesUsed, Visibility, Damage, LastDirection, AiTask, Counter, AuxiliaryTimer;
    public UnitOrder Orders;
    public CitySlot? Home;
    public short DestinationX, DestinationY;
    public UnitLink Previous, Next;
}

/// <summary>F02 §§3,6: live city values. Embedded arrays and immutable bytes make value copies independent.</summary>
public record struct City
{
    public City()
    {
        Name = OwnedList<byte>.Empty;
        for (int i = 0; i < 3; ++i) RoutePartners[i] = new();
    }
    public CityToken Token;
    public short X, Y;
    public uint Flags;
    public CivilizationSlot Owner, Founder;
    public sbyte Size;
    public byte TurnsCaptured, KnownTo;
    public Fixed8<byte> RememberedSize;
    public uint SpecialistKinds;
    public short FoodBox, ShieldBox, BaseTrade;
    public OwnedList<byte> Name;
    public uint WorkedTiles;
    public Fixed5<byte> Improvements;
    public sbyte Production;
    public byte RouteCount;
    public Fixed3<sbyte> Supplies, Demands, RouteGoods;
    public Fixed3<CityLink> RoutePartners;
    public short Science, Tax, Trade;
    public byte TotalFood, TotalShields, Happy, Unhappy;
}

/// <summary>Fixed-capacity stable backing; scan bounds and occupancy are separate. Borrowed references stay synchronous.</summary>
public abstract class SlotPool<TRecord, TSlot> where TRecord : struct
{
    private readonly TRecord[] records;
    public int End { get; private set; }
    protected SlotPool(int capacity)
    {
        records = new TRecord[capacity];
        for (int i = 0; i < capacity; ++i) records[i] = new TRecord();
    }
    protected abstract int Index(TSlot slot);
    protected abstract bool Occupied(in TRecord record);
    protected abstract void ClearToken(ref TRecord record);
    public void SetEnd(int end)
    {
        if (end < 0 || end > records.Length) throw new ArgumentOutOfRangeException(nameof(end), "Pool scan bound exceeds supplied backing");
        End = end;
    }
    public Span<TRecord> Storage => records;
    public Span<TRecord> Scanned => records.AsSpan(0, End);
    public ref TRecord At(TSlot slot)
    {
        int index = Index(slot);
        if (index < 0 || index >= records.Length) throw new ArgumentOutOfRangeException(nameof(slot));
        return ref records[index];
    }
    public bool Live(TSlot slot)
    {
        int index = Index(slot); return index >= 0 && index < End && Occupied(in records[index]);
    }
    public bool FinishDeletion(int index)
    {
        if (index < 0) return false;
        if (index >= End) throw new ArgumentOutOfRangeException(nameof(index), "Deletion outside current pool scan");
        if (!Occupied(in records[index])) return false;
        ClearToken(ref records[index]); if (index == End - 1) --End; return true;
    }
}
public sealed class UnitPool() : SlotPool<Unit, UnitSlot>(EntityLimits.Units)
{
    protected override int Index(UnitSlot slot) => slot.Value;
    protected override bool Occupied(in Unit record) => record.Token.Occupied;
    protected override void ClearToken(ref Unit record) => record.Token = new(0);
}
public sealed class CityPool() : SlotPool<City, CitySlot>(EntityLimits.Cities)
{
    public const int CreationLimit = 255;
    protected override int Index(CitySlot slot) => slot.Value;
    protected override bool Occupied(in City record) => record.Token.Occupied;
    protected override void ClearToken(ref City record) => record.Token = new(0);
}
public sealed class EntityPools
{
    public UnitPool Units { get; } = new();
    public CityPool Cities { get; } = new();
    // F05 entity portions only. Fresh-world replacement is a separate owning boundary.
    public void InvalidateForNewSession()
    {
        foreach (ref var unit in Units.Storage) { unit.Token = new(0); unit.Previous = new(); unit.Next = new(); }
        foreach (ref var city in Cities.Storage) city.Token = new(0);
        Units.SetEnd(0); Cities.SetEnd(0);
    }
    public void ResetScans(ref UnitSlot? selected) { Units.SetEnd(0); Cities.SetEnd(0); selected = new(0); }
    // F06 caller applies only after BOTH record groups succeed.
    public void RenewLoadedLifetimes(ProcessTokens tokens, bool legacyWithoutIds)
    {
        foreach (ref var unit in Units.Scanned) if (legacyWithoutIds || unit.Token.Occupied) unit.Token = tokens.AssignUnit();
        foreach (ref var city in Cities.Scanned) if (legacyWithoutIds || city.Token.Occupied) city.Token = tokens.AssignCity();
    }
}
public sealed class UnitStackState
{
    public int Suppression;
    public bool AnimationActive, DetachActive, AttachActive, RelocateActive, BottomActive;
    public int DetachReply, AttachReply, RelocateReply, BottomReply;
}
public sealed class UnitDeletionState { public bool Active; public int Reply; }
public sealed class CityMoodWorkspace
{
    public CitySlot? City;
    public int Happy, Unhappy;
    // F02 §9 / C03: cached path never selects a city; full path retains child effects.
    public int Calculate(CitySlot? city, bool full, Func<int>? pipeline = null)
    {
        if (city is null) return 0;
        if (!full) return unchecked(Happy - Unhappy);
        City = city;
        if (pipeline is null) throw new ArgumentException("Full city calculation requires its owning pipeline", nameof(pipeline));
        return pipeline();
    }
}
