using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Units;

public sealed record UnitStackRow(UnitTarget Unit, string Adjective, bool Veteran, string Name, string? Home, string? Cargo);
public abstract record UnitStackDecision
{
    public sealed record Choose(int Observer, OwnedList<UnitStackRow> Rows) : UnitStackDecision;
    public sealed record Contact(DiplomaticContactDecision Value) : UnitStackDecision;
}
public abstract record UnitStackReply
{
    public sealed record Choose(int Row, bool WakeOnly = false) : UnitStackReply;
    public sealed record Contact(ResearchReply Value) : UnitStackReply;
}

/// <summary>U04 explicit stack selection. Chooser wakes commit before acceptance; selected-unit
/// focus and final original-slot revelation retain real contact decisions. Callers own admission.</summary>
public sealed class UnitStackSelection(GameState world, RuleTables rules, StackOperations stacks,
    UnitFocus focus, UnitRevelation revelation)
{
    private GameState World => world;
    private RuleTables Rules => rules;
    private StackOperations Stacks => stacks;
    private UnitFocus Focus => focus;
    private UnitRevelation Revelation => revelation;
    public Operation Begin(MapPoint cursor, int localObserver, string call) => new(this, cursor, localObserver, call, null);
    /// <summary>The caller has already admitted this explicit slot. Skips only stack discovery/chooser.</summary>
    public Operation BeginSelected(UnitSlot selected, MapPoint cursor, int localObserver, string call) =>
        new(this, cursor, localObserver, call, selected);

    public sealed class Operation : IDisposable
    {
        private readonly UnitStackSelection parent;
        private readonly MapPoint cursor;
        private readonly int localObserver;
        private readonly string call;
        private readonly List<Exception> failures = [];
        private UnitFocus.Operation? focusChild;
        private UnitRevelation.Operation? revealChild;
        private int stage;
        private bool faulted, disposed;
        public bool Completed { get; private set; }
        public UnitSlot? Selected { get; private set; }
        public UnitStackDecision? Pending { get; private set; }
        public IReadOnlyList<Exception> PresentationFailures => failures.AsReadOnly();
        internal Operation(UnitStackSelection parent, MapPoint cursor, int localObserver, string call, UnitSlot? selected)
        {
            ArgumentException.ThrowIfNullOrEmpty(call);
            this.parent = parent;
            this.cursor = cursor;
            this.localObserver = localObserver;
            this.call = call;
            Selected = selected;
            stage = selected is null ? 0 : 2;
        }
        private UnitStackDecision.Choose Menu(int slot)
        {
            List<UnitStackRow> rows = [];
            for (int current = parent.Stacks.First(slot); current >= 0; current = parent.Stacks.Next(current))
            {
                // Traverse and validate the complete stack, even beyond the nine exposed rows.
                if (rows.Count >= 9)
                { continue; }
                ref var unit = ref parent.World.Entities.Units.Storage[current];
                var rule = parent.Rules.Units[unit.Type.Value];
                string? cargo = rule.Role != RuleRole.Trade ? null : unchecked((sbyte)unit.Counter) < 0
                    ? "Freight" : parent.Rules.Commodities[unit.Counter].Name;
                rows.Add(new(new(new((ushort)current), unit.Token), parent.World.Names[unit.Owner.Value].Adjective,
                    unit.Flags.Veteran, rule.Identity.Name, unit.Home is { } home ? parent.World.Entities.Cities.Storage[home.Value].Name : null, cargo));
            }
            return new(localObserver, new(rows));
        }
        private void Wake(int slot)
        {
            // Classic chooser callbacks intentionally use their captured backed slot without revalidation.
            ref var unit = ref parent.World.Entities.Units.Storage[slot];
            unit.Orders = UnitOrder.None;
            unit.Flags.Automated = false;
        }
        public UnitStackDecision? Resume(UnitStackReply? reply = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("Failed stack selection cannot resume"); }
            if (Completed)
            { return null; }
            try
            {
                MapKnowledge.RequireLocal(parent.World);
                if (Pending is not null && reply is null)
                { return Pending; }
                ResearchReply? contactReply = null;
                if (Pending is UnitStackDecision.Choose menu)
                {
                    if (reply is not UnitStackReply.Choose choice)
                    { throw new InvalidOperationException("Expected a stack choice"); }
                    if (choice.Row < 0)
                    {
                        if (choice.WakeOnly)
                        { return Pending; }
                        Pending = null;
                        Completed = true;
                        return null;
                    }
                    if (choice.Row >= menu.Rows.Count)
                    { throw new ArgumentOutOfRangeException(nameof(reply)); }
                    int slot = menu.Rows[choice.Row].Unit.Slot.Value;
                    if (choice.WakeOnly)
                    {
                        Wake(slot);
                        // A fresh decision invalidates the old interaction while preserving captured row order/text.
                        return Pending = menu with { };
                    }
                    Selected = new((ushort)slot);
                    stage = 2;
                }
                else if (Pending is UnitStackDecision.Contact)
                {
                    if (reply is not UnitStackReply.Contact answer)
                    { throw new InvalidOperationException("Expected a contact reply"); }
                    contactReply = answer.Value;
                }
                Pending = null;
                if (stage == 0)
                {
                    int slot;
                    if (parent.World.MovementMode == MovementMode.MoveUnits)
                    {
                        var active = parent.World.Globals.ActiveUnit;
                        if (active is not { } current || !parent.World.Entities.Units.Live(current)
                            || parent.World.Entities.Units.At(current).Owner.Value != localObserver)
                        { Completed = true; return null; }
                        slot = current.Value;
                        if (parent.Stacks.Count(slot) == 1)
                        { Completed = true; return null; }
                    }
                    else
                    {
                        slot = parent.Stacks.FirstAt(cursor);
                        if (slot < 0)
                        { Completed = true; return null; }
                        int owner = parent.World.Entities.Units.Storage[slot].Owner.Value;
                        if (owner != localObserver || owner != parent.World.Globals.TurnCiv && parent.World.Globals.RevealMap == 0)
                        { Completed = true; return null; }
                        if (parent.Stacks.Count(slot) == 1)
                        { Selected = new((ushort)slot); stage = 2; }
                    }
                    if (stage == 0)
                    { stage = 1; return Pending = Menu(slot); }
                }
                if (stage == 2)
                {
                    var slot = Selected!.Value;
                    parent.World.Globals.ActiveUnit = slot;
                    Wake(slot.Value);
                    parent.World.MovementMode = MovementMode.View;
                    focusChild = parent.Focus.Begin(cursor, 0, localObserver, call + "/focus");
                    stage = 3;
                }
                if (stage == 3)
                {
                    var decision = focusChild!.Resume(contactReply);
                    contactReply = null;
                    if (decision is not null)
                    { return Pending = new UnitStackDecision.Contact(decision); }
                    if (!focusChild.Completed)
                    { return null; }
                    failures.AddRange(focusChild.PresentationFailures);
                    focusChild.Dispose();
                    focusChild = null;
                    stage = 4;
                }
                if (stage == 4)
                {
                    int slot = Selected!.Value.Value;
                    if (parent.World.Entities.Units.Storage[slot].MovesUsed == 0)
                    { revealChild = parent.Revelation.Begin(slot, true, call + "/selected-reveal"); }
                    stage = 5;
                }
                if (stage == 5 && revealChild is not null)
                {
                    var decision = revealChild.Resume(contactReply);
                    if (decision is not null)
                    { return Pending = new UnitStackDecision.Contact(decision); }
                    if (!revealChild.Completed)
                    { return null; }
                    failures.AddRange(revealChild.PresentationFailures);
                    revealChild.Dispose();
                    revealChild = null;
                }
                Completed = true;
                return null;
            }
            catch { faulted = true; focusChild?.Dispose(); revealChild?.Dispose(); throw; }
        }
        public void Dispose() { disposed = true; focusChild?.Dispose(); revealChild?.Dispose(); }
    }
}
