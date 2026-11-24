using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Units;

public interface IUnitSelectionEffects
{
    void Focus(UnitTarget unit, MapPoint position, int owner);
    void FlushInput();
    void RedrawMaps();
}

/// <summary>U04 select-and-publish activation with actual W03 revelation/V04 contact.
/// Presentation owns cursor/status/menu layout; the captured selection and timer writes
/// precede revelation, whose real decisions retain the operation.</summary>
public sealed class UnitSelection(GameState world, RuleTables rules, CombatWorkspace combat,
    UnitRevelation revelation, IUnitSelectionEffects effects)
{
    private GameState World => world;
    private RuleTables Rules => rules;
    private CombatWorkspace Combat => combat;
    private UnitRevelation Revelation => revelation;
    private IUnitSelectionEffects Effects => effects;

    public Operation BeginNext(MapPoint cursor, int searchMode, int localObserver, string call) =>
        new(this, cursor, searchMode, localObserver, call);

    public sealed class Operation : IDisposable
    {
        private readonly UnitSelection parent;
        private readonly MapPoint cursor;
        private readonly int searchMode, localObserver;
        private readonly string call;
        private readonly List<Exception> failures = [];
        private UnitRevelation.Operation? child;
        private bool started, faulted, disposed;
        public UnitSlot? Selected { get; private set; }
        public bool Completed { get; private set; }
        public DiplomaticContactDecision? Pending { get; private set; }
        public IReadOnlyList<Exception> PresentationFailures => failures.AsReadOnly();

        internal Operation(UnitSelection parent, MapPoint cursor, int searchMode, int localObserver, string call)
        {
            ArgumentException.ThrowIfNullOrEmpty(call);
            this.parent = parent;
            this.cursor = cursor;
            this.searchMode = searchMode;
            this.localObserver = localObserver;
            this.call = call;
        }
        private void Present(Action action)
        { try { action(); } catch (Exception error) { failures.Add(error); } }

        public DiplomaticContactDecision? Resume(ResearchReply? reply = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("Failed unit activation cannot resume"); }
            if (Completed)
            { return null; }
            try
            {
                MapKnowledge.RequireLocal(parent.World);
                if (Pending is not null && reply is null)
                { return Pending; }
                if (Pending is null)
                { reply = null; }
                Pending = null;
                if (!started)
                {
                    Selected = UnitActivation.NextSelectable(parent.World, parent.Rules,
                        parent.Combat.ActiveUnitLost ? null : parent.World.Globals.ActiveUnit,
                        cursor, searchMode, localObserver);
                    parent.Combat.ActiveUnitLost = false;
                    parent.World.Globals.ActiveUnit = Selected;
                    started = true;
                    if (Selected is { } selected && parent.World.Entities.Units.Live(selected))
                    {
                        ref var unit = ref parent.World.Entities.Units.At(selected);
                        if (unit.Orders is not (UnitOrder.Fortifying or UnitOrder.Fortified)
                            && (unit.Orders == UnitOrder.None || unchecked((sbyte)unit.Orders) > 10))
                        {
                            unit.AuxiliaryTimer = 0;
                            var target = new UnitTarget(selected, unit.Token);
                            var position = unit.Position;
                            int owner = unit.Owner.Value;
                            Present(() => parent.Effects.Focus(target, position, owner));
                        }
                        if (unit.MovesUsed == 0)
                        { child = parent.Revelation.Begin(selected.Value, true, call + "/reveal"); }
                    }
                }
                if (child is not null)
                {
                    Pending = child.Resume(reply);
                    if (!child.Completed)
                    { return Pending; }
                    failures.AddRange(child.PresentationFailures);
                    child.Dispose();
                    child = null;
                }
                if (Selected is { } && !parent.World.Multiplayer.Saved.Simultaneous)
                { Present(parent.Effects.FlushInput); }
                // Revelation may delete the selected slot or change shared ActiveUnit.
                // The activation's final identity check still concerns its captured slot.
                if (Selected is not { } slot || !parent.World.Entities.Units.Storage[slot.Value].Token.Occupied)
                { Present(parent.Effects.RedrawMaps); }
                Completed = true;
                return null;
            }
            catch { faulted = true; child?.Dispose(); throw; }
        }
        public void Dispose() { disposed = true; child?.Dispose(); }
    }
}
