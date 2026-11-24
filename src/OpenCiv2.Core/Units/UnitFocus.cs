using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Units;

/// <summary>U04 focusActive for local modes. Actual next-unit activation may retain contact;
/// afterward focus validates the live shared active unit. Presentation layout is caller-owned.</summary>
public sealed class UnitFocus(GameState world, RuleTables rules, UnitSelection selection, IUnitSelectionEffects effects)
{
    private GameState World => world;
    private RuleTables Rules => rules;
    private UnitSelection Selection => selection;
    private IUnitSelectionEffects Effects => effects;
    public Operation Begin(MapPoint cursor, int autoNext, int localObserver, string call) =>
        new(this, cursor, autoNext, localObserver, call);

    public sealed class Operation : IDisposable
    {
        private readonly UnitFocus parent;
        private readonly MapPoint cursor;
        private readonly int autoNext, localObserver;
        private readonly string call;
        private readonly List<Exception> failures = [];
        private UnitSelection.Operation? child;
        private bool started, disposed, faulted;
        public bool Completed { get; private set; }
        public bool InitialFocus { get; private set; }
        public UnitTarget? Focused { get; private set; }
        public DiplomaticContactDecision? Pending { get; private set; }
        public IReadOnlyList<Exception> PresentationFailures => failures.AsReadOnly();
        internal Operation(UnitFocus parent, MapPoint cursor, int autoNext, int localObserver, string call)
        {
            ArgumentException.ThrowIfNullOrEmpty(call);
            this.parent = parent;
            this.cursor = cursor;
            this.autoNext = autoNext;
            this.localObserver = localObserver;
            this.call = call;
        }
        private void Present(Action action) { try { action(); } catch (Exception error) { failures.Add(error); } }
        public DiplomaticContactDecision? Resume(ResearchReply? reply = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("Failed unit focus cannot resume"); }
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
                    bool changedMode = parent.World.MovementMode != MovementMode.MoveUnits;
                    parent.World.MovementMode = MovementMode.MoveUnits;
                    bool selectable = UnitActivation.Selectable(parent.World, parent.Rules, parent.World.Globals.ActiveUnit?.Value ?? -1);
                    InitialFocus = changedMode && selectable;
                    if (!selectable)
                    { child = parent.Selection.BeginNext(cursor, autoNext, localObserver, call + "/activate"); }
                    started = true;
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
                var active = parent.World.Globals.ActiveUnit;
                if (active is { } slot && UnitActivation.Selectable(parent.World, parent.Rules, slot.Value))
                {
                    ref var unit = ref parent.World.Entities.Units.At(slot);
                    var target = new UnitTarget(slot, unit.Token);
                    var point = unit.Position;
                    int owner = unit.Owner.Value;
                    Focused = target;
                    Present(() => parent.Effects.Focus(target, point, owner));
                }
                else
                { Present(parent.Effects.RedrawMaps); }
                Completed = true;
                return null;
            }
            catch { faulted = true; child?.Dispose(); throw; }
        }
        public void Dispose() { disposed = true; child?.Dispose(); }
    }
}
