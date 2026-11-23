using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Units;

/// <summary>U04/F04 local global reset with real V06 escape and nested lifecycle decisions.
/// Original out-of-range mission bookkeeping writes are excluded by X01.</summary>
public sealed class UnitTurnReset(GameState world, RuleTables rules, CivilizationElimination lifecycle, EspionageIncidents incidents)
{
    public const int All = -1, AllAlternate = -2, ExhaustAll = -3;
    private GameState World => world;
    public Operation Begin(int selector, string call) => new(this, selector, call);

    /// <summary>Rebuild through W01 setters, including their fallback-tile behavior.
    /// Local play has no tile-network synchronization state to toggle.</summary>
    public static void RebuildPresence(GameState world)
    {
        MapKnowledge.RequireLocal(world);
        for (int i = 0; i < world.Map.Tiles.Length; ++i)
        {
            world.Map.Mutate(new TileChange.Features(world.Map.Geometry.Coordinates(i), MapFeatures.Unit, false), synchronize: true);
        }

        for (int i = 0; i < world.Entities.Units.End; ++i)
        {
            if (world.Entities.Units.Storage[i].Token.Occupied)
            {
                Mark(world, i);
            }
        }
    }
    private static void Mark(GameState world, int slot) =>
        world.Map.Mutate(new TileChange.Features(world.Entities.Units.Storage[slot].Position, MapFeatures.Unit, true), synchronize: true);

    /// <summary>Ordinary admitted-record reset. Moved/Paradropped remain for civilization recovery.</summary>
    public static void ResetOrdinary(GameState world, int slot)
    {
        MapKnowledge.RequireLocal(world);
        ref var unit = ref world.Entities.Units.Storage[slot];
        if (unit.Flags.SpyMission)
        {
            throw new InvalidOperationException("Mission expiry requires the complete reset operation");
        }

        unit.MovesUsed = unit.AuxiliaryTimer = 0;
        unit.Flags.Waiting = false;
        if (unit.Flags.BorderWarned && !unit.Flags.BorderIntruder && (world.Globals.Turn & 3) == (slot & 3))
        {
            unit.Flags.BorderWarned = false;
        }

        unit.Flags.BorderIntruder = false;
    }
    public sealed class Operation : IDisposable
    {
        private readonly UnitTurnReset parent;
        private readonly IEnumerator<LifecycleDecision?> steps;
        private int? reply;
        private bool disposed, faulted;
        public bool Completed { get; internal set; }
        public LifecycleDecision? Pending { get; private set; }
        internal Operation(UnitTurnReset parent, int selector, string call)
        {
            ArgumentException.ThrowIfNullOrEmpty(call);
            this.parent = parent;
            steps = parent.Execute(this, selector, call).GetEnumerator();
        }
        internal int? TakeReply() { var answer = reply; reply = null; return answer; }
        public LifecycleDecision? Resume(int? answer = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            {
                throw new InvalidOperationException("Failed unit reset cannot resume");
            }

            if (Completed)
            {
                return null;
            }

            try
            {
                MapKnowledge.RequireLocal(parent.World);
                if (Pending is not null && answer is null)
                {
                    return Pending;
                }

                reply = Pending is null ? null : answer;
                Pending = null;
                if (steps.MoveNext())
                {
                    return Pending = steps.Current;
                }

                steps.Dispose();
                return null;
            }
            catch { faulted = true; steps.Dispose(); throw; }
        }
        public void Dispose() { disposed = true; steps.Dispose(); }
    }
    private IEnumerable<LifecycleDecision?> Execute(Operation op, int selector, string call)
    {
        RebuildPresence(world);
        if (selector == ExhaustAll)
        {
            for (int i = 0; i < world.Entities.Units.End; ++i)
            {
                if (world.Entities.Units.Storage[i].Token.Occupied)
                {
                    UnitMovement.Exhaust(world, rules, ref world.Entities.Units.Storage[i]);
                }
            }
        }

        for (int i = 0; i < world.Entities.Units.End; ++i)
        {
            if (!world.Entities.Units.Storage[i].Token.Occupied)
            {
                continue;
            }

            Mark(world, i);
            if (selector is not (All or AllAlternate) && world.Entities.Units.Storage[i].Owner.Value != selector)
            {
                continue;
            }

            if (!world.Entities.Units.Storage[i].Flags.SpyMission)
            { ResetOrdinary(world, i); continue; }
            world.Entities.Units.Storage[i].AuxiliaryTimer = unchecked((byte)(world.Entities.Units.Storage[i].AuxiliaryTimer - 1));
            if (world.Entities.Units.Storage[i].AuxiliaryTimer != 0)
            {
                continue;
            }

            world.Entities.Units.Storage[i].Flags.SpyMission = false;
            int counter = unchecked((sbyte)world.Entities.Units.Storage[i].Counter);
            if (selector is >= 0 and < EntityLimits.Civilizations && counter is >= 0 and < EntityLimits.Civilizations)
            {
                world.Civilizations[selector].Treaties[counter].SpyChat = false;
            }

            using var child = lifecycle.BeginEscape(i, 0, unchecked((sbyte)world.Entities.Units.Storage[i].Counter), call + "/expiry-" + i, incidents);
            while (child.Result is null)
            {
                var pending = child.Resume(op.TakeReply());
                if (child.Result is null)
                {
                    yield return pending;
                }
            }
        }
        op.Completed = true;
    }
}
