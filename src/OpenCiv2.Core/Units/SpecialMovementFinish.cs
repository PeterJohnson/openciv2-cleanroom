using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Units;

public enum MovementLossSound { EngineSputter, JetSputter, Sinking }
public sealed record MovementLossNotice(UnitType Type, string Name, bool Sinking, int Viewer);
public interface ISpecialMovementEffects
{
    void Sound(MovementLossSound sound);
    void Redraw(MapPoint point);
    void Lost(MovementLossNotice notice);
}

/// <summary>U05 local finish-time basing/fuel and Coast-bound loss. Movement owns sequencing these
/// around hut/exploration/boarding; real basic versus cargo-aware lifecycle children remain distinct.</summary>
public sealed class SpecialMovementFinish(GameState world, RuleTables rules, StackOperations stacks,
    GameplayRandom random, CivilizationElimination lifecycle, UnitLoss loss, EspionageIncidents incidents, ISpecialMovementEffects effects)
{
    private GameState World => world;
    private ref Unit Unit(int slot) => ref world.Entities.Units.Storage[slot];
    public Operation BeginAircraft(MovementContinuation movement, MapPoint destination, string call) => new(this, movement, destination, 0, false, call);
    public Operation BeginCoastBound(MovementContinuation movement, int owner, MapPoint destination, string call) => new(this, movement, destination, owner, true, call);
    public sealed class Operation : IDisposable
    {
        private readonly SpecialMovementFinish parent;
        private readonly IEnumerator<EspionagePretext?> steps;
        private int? answer;
        private readonly List<Exception> presentationFailures = [];
        public IReadOnlyList<Exception> PresentationFailures => presentationFailures.AsReadOnly();
        internal void Present(Action action) { try { action(); } catch (Exception error) { presentationFailures.Add(error); } }
        private bool disposed, faulted;
        public bool Completed { get; private set; }
        public bool Stopped { get; internal set; }
        public EspionagePretext? Pending { get; private set; }
        internal Operation(SpecialMovementFinish parent, MovementContinuation movement, MapPoint destination, int owner, bool coast, string call)
        {
            ArgumentException.ThrowIfNullOrEmpty(call);
            this.parent = parent;
            steps = parent.Execute(this, movement, destination, owner, coast, call).GetEnumerator();
        }
        internal int? TakeReply() { int? result = answer; answer = null; return result; }
        public EspionagePretext? Resume(int? reply = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("A failed special movement cannot resume"); }
            if (Completed)
            { return null; }
            try
            {
                MapKnowledge.RequireLocal(parent.World);
                if (Pending is not null && reply is null)
                { return Pending; }
                answer = Pending is null ? null : reply;
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
    private bool Aircraft(Operation operation, MovementContinuation movement, MapPoint destination)
    {
        int slot = movement.Unit.Value;
        movement.WorkFlag = world.Map.CityOwner(destination) is not null || world.Map.AirbaseOwner(destination) is not null;
        if (!movement.WorkFlag)
        {
            for (int current = stacks.First(slot); current >= 0; current = stacks.Next(current))
            {
                var abilities = rules.Units[Unit(current).Type.Value].Abilities;
                if (abilities.CarryAircraft || abilities.Submarine && rules.Units[Unit(slot).Type.Value].Abilities.DestroyedAfterAttack)
                { movement.WorkFlag = true; break; }
            }
        }
        if (movement.WorkFlag && rules.Units[Unit(slot).Type.Value].Range == 1
            && UnitMovement.Left(world, rules, Unit(slot)) != 0 && movement.AttackDone)
        { movement.WorkFlag = false; }
        if (movement.WorkFlag)
        {
            Unit(slot).Counter = 0;
            UnitMovement.Exhaust(world, rules, ref Unit(slot));
            stacks.MoveToBottom(slot, true);
            operation.Present(() => effects.Redraw(destination));
            return false;
        }
        if (rules.Units[Unit(slot).Type.Value].Range == 0 || UnitMovement.Left(world, rules, Unit(slot)) != 0)
        { return false; }
        Unit(slot).Counter = unchecked((byte)(Unit(slot).Counter + 1));
        return unchecked((sbyte)Unit(slot).Counter) >= unchecked((sbyte)rules.Units[Unit(slot).Type.Value].Range);
    }
    private bool Coast(MovementContinuation movement, int owner, MapPoint destination, string call)
    {
        int slot = movement.Unit.Value;
        if (!rules.Units[Unit(slot).Type.Value].Abilities.CoastBound || !movement.LocalHuman
            || UnitMovement.Left(world, rules, Unit(slot)) != 0 || BuildingQueries.HasWonder(world, rules, owner, WonderIds.Lighthouse))
        { return false; }
        int odds = rules.Cosmic.TriremeLossDenominator;
        if (CivilizationKnowledge.KnowsAdvance(world.Civilizations, owner, AdvanceIds.Seafaring))
        { odds = Math.Max(2, odds * 2); }
        if (CivilizationKnowledge.KnowsAdvance(world.Civilizations, owner, AdvanceIds.Navigation))
        { odds *= 2; }
        int roll = unchecked(odds - 1) < 1 ? 0 : random.PrimaryModulo(new ChoiceKey("U05.coast-loss", call), odds);
        if (roll != 0)
        { return false; }
        movement.WorkFlag = false;
        for (int i = 0; i <= MapLimits.Directions; ++i)
        {
            var point = i == MapLimits.Directions ? destination
                : MapNeighborhoods.Offset(world.Map.Geometry, destination, MapNeighborhoods.Directions[i], world.Globals.Rules.FlatWorld);
            if (world.Map.Geometry.InBounds(point) && !world.Map.TileAt(point).Ocean)
            { movement.WorkFlag = true; return false; }
        }
        return true;
    }
    private IEnumerable<EspionagePretext?> Execute(Operation operation, MovementContinuation movement, MapPoint destination, int owner, bool coast, string call)
    {
        bool lost = coast ? Coast(movement, owner, destination, call) : Aircraft(operation, movement, destination);
        if (!lost)
        { yield break; }
        int slot = movement.Unit.Value;
        if (movement.LocalHuman)
        { operation.Present(() => effects.Sound(coast ? MovementLossSound.Sinking : Unit(slot).Type.Value < 30 ? MovementLossSound.EngineSputter : MovementLossSound.JetSputter)); }
        movement.Origin = Unit(slot).Position;
        UnitType type = Unit(slot).Type;
        using var child = coast ? lifecycle.BeginEraseCargo(loss, slot, call + "/loss", incidents)
            : lifecycle.BeginDelete(slot, true, call + "/loss", incidents);
        while (child.Result is null)
        {
            if (child.Resume(operation.TakeReply()) is { } pending)
            { yield return pending; }
            else if (child.Result is null)
            { yield return null; }
        }
        operation.Present(() => effects.Redraw(movement.Origin));
        if (movement.LocalHuman)
        { effects.Lost(new(type, rules.Units[type.Value].Identity.Name, coast, world.Multiplayer.LocalHuman)); }
        operation.Stopped = true;
    }
}
