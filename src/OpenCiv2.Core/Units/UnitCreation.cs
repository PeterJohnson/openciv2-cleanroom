using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Units;

public enum UnitCreationNotice { TooManyUnits, Ships, Aircraft, Caravan, FirstMilitary, SecondMilitary }

/// <summary>Owning children of creation. Revelation must run its W03 interactions before creation notices.</summary>
public interface IUnitCreationEffects
{
    void RevealSurroundings(int slot, bool allowContact);

    void Notice(UnitCreationNotice notice);

    void SynchronizeCreation();
}

/// <summary>U01 §5 authoritative creation. Session owns remote requests and semantic publications.</summary>
public sealed class UnitCreation(GameState world, ProcessContext process, StackOperations stacks, IUnitCreationEffects effects)
{
    private readonly UnitRevelation? revelation;
    public UnitCreation(GameState world, ProcessContext process, StackOperations stacks,
        UnitRevelation revelation, IUnitCreationEffects effects) : this(world, process, stacks, effects)
    { this.revelation = revelation; }

    public Operation Begin(UnitType type, CivilizationSlot owner, MapPoint position, string call) => new(this, type, owner, position, call);
    public sealed class Operation : IDisposable
    {
        private readonly IEnumerator<DiplomaticContactDecision?> steps;
        private readonly List<Exception> failures = [];
        private ResearchReply? reply;
        private bool disposed, faulted;
        public bool Completed { get; private set; }
        public UnitSlot? Result { get; internal set; }
        public DiplomaticContactDecision? Pending { get; private set; }
        public IReadOnlyList<Exception> PresentationFailures => failures.AsReadOnly();
        internal Operation(UnitCreation parent, UnitType type, CivilizationSlot owner, MapPoint position, string call)
        {
            ArgumentException.ThrowIfNullOrEmpty(call);
            steps = parent.Execute(this, type, owner, position, call).GetEnumerator();
        }
        internal ResearchReply? TakeReply() { var answer = reply; reply = null; return answer; }
        internal void Report(IEnumerable<Exception> errors) => failures.AddRange(errors);
        public DiplomaticContactDecision? Resume(ResearchReply? answer = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("Failed unit creation cannot resume"); }
            if (Completed)
            { return null; }
            try
            {
                if (Pending is not null && answer is null)
                { return Pending; }
                reply = Pending is null ? null : answer;
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
    public UnitSlot? Create(UnitType type, CivilizationSlot owner, MapPoint position)
    {
        if (revelation is not null)
        { throw new InvalidOperationException("Use Begin for creation with retained revelation"); }
        using var operation = Begin(type, owner, position, "creation");
        while (!operation.Completed)
        { operation.Resume(); }
        return operation.Result;
    }
    private IEnumerable<DiplomaticContactDecision?> Execute(Operation operation, UnitType type, CivilizationSlot owner, MapPoint position, string call)
    {
        if (revelation is not null)
        { MapKnowledge.RequireLocal(world); }
        if (Initialize(type, owner, position) is not { } slot)
        { yield break; }
        if (revelation is null)
        { effects.RevealSurroundings(slot.Value, true); }
        else
        {
            using var child = revelation.Begin(slot.Value, true, call + "/revelation");
            while (!child.Completed)
            {
                var decision = child.Resume(operation.TakeReply());
                if (!child.Completed)
                { yield return decision; }
            }
            operation.Report(child.PresentationFailures);
        }
        Finish(type, owner);
        operation.Result = slot;
    }
    private UnitSlot? Initialize(UnitType type, CivilizationSlot owner, MapPoint position)
    {
        if (world.Globals.Multiplayer >= GameMode.Tcp && !world.Multiplayer.NetworkHost)
        {
            throw new InvalidOperationException("Local creation requires authority; route through the session unit operation");
        }
        var pool = world.Entities.Units;
        ref var civ = ref world.Civilizations[owner.Value];
        if (EntityAllocation.ReserveUnit(pool, owner, civ.MilitaryCount, world.Globals.HumanPlayers) is not { } slot)
        {
            if (world.Globals.HumanPlayers.Contains(owner.Value) && world.Globals.Multiplayer < GameMode.Tcp)
            {
                effects.Notice(UnitCreationNotice.TooManyUnits);
            }
            return null;
        }
        world.Globals.UnitEnd = (short)pool.End;
        var rules = process.Rules.Definitions;
        if (unchecked((sbyte)rules.Units[type.Value].Role) < 5)
        {
            civ.MilitaryCount = unchecked((short)(civ.MilitaryCount + 1));
        }
        civ.UnitsActive[type.Value] = unchecked((byte)(civ.UnitsActive[type.Value] + 1));
        world.UnitTotals[owner.Value] = unchecked(world.UnitTotals[owner.Value] + 1);

        ref var unit = ref pool.At(slot);
        unit.Type = type;
        unit.Owner = owner;
        unit.Token = process.Tokens.AssignUnit();
        unit.MovesUsed = world.Globals.Multiplayer >= GameMode.Tcp && world.Multiplayer.Saved.Simultaneous
            ? unchecked((byte)UnitMovement.Allowance(world, rules, unit)) : (byte)0;
        unit.Damage = 0;
        unit.AiTask = 0x58;
        unit.Flags = default;
        unit.Visibility = default;
        unit.Orders = UnitOrder.None;
        unit.Home = null;
        var nearest = CityQueries.Nearest(world, position).Slot;
        if (owner.Value != 0 && nearest is { } city && world.Entities.Cities.At(city).Owner == owner)
        {
            unit.Home = city;
        }
        unit.Counter = unit.AuxiliaryTimer = 0;
        unit.LastDirection = byte.MaxValue;
        unit.Previous = unit.Next = null;
        unit.Position = unit.Destination = new(-1, -1);
        unit.Carrier = null;
        stacks.AttachAtFront(slot.Value, position, false);
        return slot;
    }
    private void Finish(UnitType type, CivilizationSlot owner)
    {
        var rules = process.Rules.Definitions;
        if (world.Globals.Turn != 0 && world.Globals.HumanPlayers.Contains(owner.Value)
            && world.Globals.Multiplayer == GameMode.SinglePlayer)
        {
            var rule = rules.Units[type.Value];
            if (rule.Domain == RuleDomain.Sea)
            {
                if (!world.Globals.ShownEventFlags.Ships)
                {
                    Tutorial(UnitCreationNotice.Ships);
                    world.Globals.ShownEventFlags.Ships = true;
                }
            }
            else if (rule.Domain == RuleDomain.Air)
            {
                if (!world.Globals.ShownEventFlags.Aircraft)
                {
                    Tutorial(UnitCreationNotice.Aircraft);
                    world.Globals.ShownEventFlags.Aircraft = true;
                }
            }
            else if (rule.Role == RuleRole.Trade)
            {
                if (!world.Globals.ShownEventFlags.Caravan)
                {
                    Tutorial(UnitCreationNotice.Caravan);
                    world.Globals.ShownEventFlags.Caravan = true;
                }
            }
            else if (world.Globals.GameOptions.Tutorial && unchecked((sbyte)rule.Role) < 5)
            {
                if (world.Civilizations[owner.Value].MilitaryCount == 1)
                {
                    effects.Notice(UnitCreationNotice.FirstMilitary);
                }
                if (world.Civilizations[owner.Value].MilitaryCount == 2)
                {
                    effects.Notice(UnitCreationNotice.SecondMilitary);
                }
            }
        }
        if (world.Globals.Multiplayer >= GameMode.Tcp)
        {
            effects.SynchronizeCreation();
        }
    }

    private void Tutorial(UnitCreationNotice notice)
    {
        if (world.Globals.GameOptions.Tutorial)
        {
            effects.Notice(notice);
        }
    }
}
