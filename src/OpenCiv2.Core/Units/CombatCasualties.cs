using OpenCiv2.Contracts;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Units;

/// <summary>Owning content queries and presentation; history mutation itself stays in Core.</summary>
public interface ICombatHistoryEffects
{
    int SecondaryColor(int civilization);
    string PluralName(int civilization);
    void RefreshHistory(int civilization);
}

/// <summary>U06 casualty ring. Reload the loser owner/next index after each owning query; no retained
/// ref crosses a callback. F02/X01 logical names remain managed strings.</summary>
public sealed class CombatHistory(GameState world, ICombatHistoryEffects effects)
{
    private ref Unit Unit(UnitSlot slot) => ref world.Entities.Units.At(slot);
    private CasualtyLog Log(UnitSlot loser) => world.Casualties[Unit(loser).Owner.Value];
    private ref CasualtyRecord Entry(UnitSlot loser)
    {
        var log = Log(loser);
        return ref log.At(log.Next);
    }
    public void Record(UnitSlot loser, UnitSlot opponent, MapPoint battle, int year)
    {
        MapKnowledge.RequireLocal(world);
        Entry(loser).Type = Unit(loser).Type.Value;
        Entry(loser).Position = Entry(loser).Position with { X = unchecked((short)battle.X) };
        Entry(loser).Position = Entry(loser).Position with { Y = unchecked((short)battle.Y) };
        Entry(loser).Year = unchecked((short)year);
        int color = effects.SecondaryColor(Unit(opponent).Owner.Value);
        Entry(loser).OpponentColor = unchecked((short)color);
        string name = effects.PluralName(Unit(opponent).Owner.Value);
        Entry(loser).OpponentName = name;
        var log = Log(loser);
        log.Next = unchecked(log.Next + 1);
        if (log.Next == CasualtyLog.Capacity)
        { log.Next = 0; }
        if (log.Next == log.First)
        {
            log.First = unchecked(log.First + 1);
            if (log.First == CasualtyLog.Capacity)
            { log.First = 0; }
        }
        else
        { log.Count = unchecked(log.Count + 1); }
        effects.RefreshHistory(Unit(loser).Owner.Value);
    }
}

public readonly record struct CombatDeath(UnitType Type, int Winner, int Loser);

/// <summary>U06 ordered casualty/event/basic-deletion composition. The required UnitKilled dispatcher
/// owns S02 when events are loaded. Lifecycle executes actual nested V01/V06 continuations.</summary>
public sealed class CombatCasualties(GameState world, StackOperations stacks, CombatWorkspace state,
    CombatHistory history, CivilizationElimination lifecycle, EspionageIncidents incidents, Action<CombatDeath> unitKilled)
{
    private GameState World => world;
    private ref Unit Unit(UnitSlot slot) => ref world.Entities.Units.At(slot);
    public Operation Begin(UnitSlot loser, UnitSlot opponent, MapPoint battle, string call) => new(this, loser, opponent, battle, call, false);
    public Operation BeginStack(UnitSlot member, UnitSlot opponent, MapPoint battle, string call) => new(this, member, opponent, battle, call, true);

    public sealed class Operation : IDisposable
    {
        private readonly CombatCasualties parent;
        private readonly IEnumerator<EspionagePretext?> steps;
        private int? reply;
        private bool faulted, disposed;
        public bool Completed { get; private set; }
        public EspionagePretext? Pending { get; private set; }
        internal Operation(CombatCasualties parent, UnitSlot loser, UnitSlot opponent, MapPoint battle, string call, bool stack)
        {
            ArgumentException.ThrowIfNullOrEmpty(call);
            this.parent = parent;
            steps = parent.Execute(this, loser, opponent, battle, call, stack).GetEnumerator();
        }
        internal int? TakeReply() { int? answer = reply; reply = null; return answer; }
        /// <summary>Null without completion yields between casualties or during bounded lifecycle work.</summary>
        public EspionagePretext? Resume(int? response = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("A failed casualty operation cannot resume"); }
            if (Completed)
            { return null; }
            try
            {
                MapKnowledge.RequireLocal(parent.World);
                if (Pending is not null && response is null)
                { return Pending; }
                reply = Pending is null ? null : response;
                Pending = null;
                if (steps.MoveNext())
                { return Pending = steps.Current; }
                Completed = true;
                steps.Dispose();
                return null;
            }
            catch { faulted = true; steps.Dispose(); throw; }
        }
        public void Dispose() { disposed = true; steps.Dispose(); }
    }
    private IEnumerable<EspionagePretext?> Execute(Operation operation, UnitSlot loser, UnitSlot opponent, MapPoint battle, string call, bool stack)
    {
        int current = loser.Value;
        if (stack)
        { state.KillCount = 0; current = stacks.First(current); }
        int ordinal = 0;
        while (current >= 0)
        {
            var slot = new UnitSlot(checked((ushort)current));
            // U06 retains raw next before history, events and deletion; do not validate or re-follow it afterward.
            int next = stack ? Unit(slot).Next?.Value ?? -1 : -1;
            byte lost = world.Civilizations[Unit(slot).Owner.Value].UnitsLost[Unit(slot).Type.Value];
            if (lost != byte.MaxValue)
            { world.Civilizations[Unit(slot).Owner.Value].UnitsLost[Unit(slot).Type.Value] = (byte)(lost + 1); }
            history.Record(slot, opponent, battle, world.Globals.Year);
            state.KillCount = unchecked(state.KillCount + 1);
            if (world.Events.Loaded)
            { unitKilled(new(Unit(slot).Type, Unit(opponent).Owner.Value, Unit(slot).Owner.Value)); }
            using (var child = lifecycle.BeginDelete(current, true, call + "/casualty/" + ordinal, incidents))
            {
                while (child.Result is null)
                {
                    if (child.Resume(operation.TakeReply()) is { } pending)
                    { yield return pending; }
                    else if (child.Result is null)
                    { yield return null; }
                }
            }
            current = next;
            ordinal = checked(ordinal + 1);
            if (current >= 0)
            { yield return null; }
        }
    }
}
