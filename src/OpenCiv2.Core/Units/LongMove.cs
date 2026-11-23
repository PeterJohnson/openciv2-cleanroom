using OpenCiv2.Contracts;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Units;

public sealed record LongMoveQuestion(int Actor, UnitTarget Unit);

/// <summary>U04 Long-move handling. U02 calls this before writing last direction; it is not goto initialization.</summary>
public sealed class LongMove(GameState world)
{
    public Operation Begin(UnitSlot unit, int direction, bool localHuman) => new(world, unit, direction, localHuman);

    public sealed class Operation(GameState world, UnitSlot slot, int direction, bool localHuman) : IDisposable
    {
        private bool started, faulted, disposed;
        public bool Completed { get; private set; }
        public LongMoveQuestion? Pending { get; private set; }
        public LongMoveQuestion? Resume(int? reply = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("A failed long-move operation cannot resume"); }
            if (Completed)
            { return null; }
            try
            {
                MapKnowledge.RequireLocal(world);
                if (!started)
                {
                    started = true;
                    if (!localHuman)
                    { Completed = true; return null; }
                    ref var unit = ref world.Entities.Units.At(slot);
                    if (unit.Orders != UnitOrder.GoTo && !unit.Flags.Automated)
                    { unit.AuxiliaryTimer = 0; Completed = true; return null; }
                    unit.AuxiliaryTimer = unchecked((byte)(unit.AuxiliaryTimer + 1));
                    if ((unchecked((sbyte)unit.LastDirection) ^ 4) == direction)
                    { unit.AuxiliaryTimer = unchecked((byte)(unit.AuxiliaryTimer + 15)); }
                    if (unchecked((sbyte)unit.AuxiliaryTimer) < 48)
                    { Completed = true; return null; }
                    return Pending = new(unit.Owner.Value, new(slot, unit.Token));
                }
                if (reply is null)
                { return Pending; }
                ref var retained = ref world.Entities.Units.At(slot);
                if (reply != 0)
                { retained.Orders = UnitOrder.None; retained.Flags.Automated = false; }
                retained.AuxiliaryTimer = 0;
                Pending = null;
                Completed = true;
                return null;
            }
            catch { faulted = true; throw; }
        }
        public void Dispose() => disposed = true;
    }
}
