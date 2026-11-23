using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Units;

public sealed record OccupationDenied(int Recipient, UnitTarget Unit, string UnitName);
public abstract record ForeignCityDecision
{
    public sealed record Attack(AttackWarning Warning) : ForeignCityDecision;
    public sealed record Incident(EspionagePretext Question) : ForeignCityDecision;
}
public sealed record ForeignCityResult(bool Proceed, int Value);
public interface IForeignCityEffects
{
    void CannotOccupy(OccupationDenied notice);
    void Redraw(MapPoint point);
}

/// <summary>U02 foreign-city admission after landfall. Actual U07 delivery and cargo-aware
/// intercepted loss are distinct from U06 combat delivery. The outer dispatcher owns cleanup.</summary>
public sealed class ForeignCityMovement(GameState world, RuleTables rules, AttackConfirmation confirmation,
    NuclearEffects nuclear, CivilizationElimination lifecycle, UnitLoss loss, EspionageIncidents incidents, IForeignCityEffects effects)
{
    private GameState World => world;
    private ref Unit Unit(UnitSlot slot) => ref world.Entities.Units.At(slot);
    private UnitRule Rule(UnitSlot slot) => rules.Units[Unit(slot).Type.Value];
    public Operation Begin(UnitSlot original, MovementContinuation movement, MovementStep step, string call) => new(this, original, movement, step, call);

    public sealed class Operation : IDisposable
    {
        private readonly ForeignCityMovement parent;
        private readonly IEnumerator<ForeignCityDecision?> steps;
        private readonly List<Exception> presentationFailures = [];
        private int? reply;
        private bool disposed, faulted;
        public ForeignCityResult? Result { get; internal set; }
        public bool Completed => Result is not null;
        public ForeignCityDecision? Pending { get; private set; }
        public IReadOnlyList<Exception> PresentationFailures => presentationFailures.AsReadOnly();
        internal Operation(ForeignCityMovement parent, UnitSlot original, MovementContinuation movement, MovementStep step, string call)
        {
            ArgumentException.ThrowIfNullOrEmpty(call);
            if (step.City is null)
            { throw new ArgumentException("Foreign-city admission requires its captured city", nameof(step)); }
            this.parent = parent;
            steps = parent.Execute(this, original, movement, step, call).GetEnumerator();
        }
        internal int? TakeReply() { var answer = reply; reply = null; return answer; }
        internal void Present(Action action) { try { action(); } catch (Exception error) { presentationFailures.Add(error); } }
        public ForeignCityDecision? Resume(int? answer = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("A failed foreign-city movement cannot resume"); }
            if (Completed)
            { return null; }
            try
            {
                MapKnowledge.RequireLocal(parent.World);
                if (Pending is not null && answer is null)
                { return Pending; }
                reply = Pending is null ? null : answer;
                Pending = null;
                if (steps.MoveNext())
                { return Pending = steps.Current; }
                steps.Dispose();
                return null;
            }
            catch { faulted = true; steps.Dispose(); throw; }
        }
        public void Dispose() { disposed = true; steps.Dispose(); }
    }
    private IEnumerable<ForeignCityDecision?> Execute(Operation operation, UnitSlot original, MovementContinuation movement, MovementStep step, string call)
    {
        int owner = step.Owner, victim = world.Entities.Cities.At(step.City!.Value).Owner.Value;
        bool human = world.Globals.HumanPlayers.Contains(owner);
        if (step.Domain != RuleDomain.Land && !(step.Domain == RuleDomain.Air && Rule(movement.Unit).Range == 0))
        {
            int result = 0;
            if (unchecked((sbyte)Rule(movement.Unit).Attack) < 99)
            {
                movement.Unit = original;
                if (movement.Options.Prompt && movement.LocalHuman && human)
                { effects.CannotOccupy(new(world.Multiplayer.LocalHuman, new(original, Unit(original).Token), Rule(original).Identity.Name)); }
            }
            else if (!movement.Options.Execute)
            { result = 1; movement.Unit = original; }
            else
            {
                using var delivery = nuclear.Begin(owner, step.To, true, call + "/nuclear");
                while (delivery.Result is null)
                {
                    if (delivery.Resume(operation.TakeReply()) is { } pending)
                    { yield return new ForeignCityDecision.Incident(pending); }
                    else if (delivery.Result is null)
                    { yield return null; }
                }
                if (delivery.Result == 0)
                {
                    using var removed = lifecycle.BeginDestroy(loss, movement.Unit.Value, call + "/intercept", incidents);
                    while (removed.Result is null)
                    {
                        if (removed.Resume(operation.TakeReply()) is { } pending)
                        { yield return new ForeignCityDecision.Incident(pending); }
                        else if (removed.Result is null)
                        { yield return null; }
                    }
                    operation.Present(() => effects.Redraw(step.From));
                }
                else
                { world.Civilizations[owner].AggressionCounter[victim] = 0; }
                movement.Unit = original;
            }
            operation.Result = new(false, result);
            yield break;
        }
        var relation = world.Civilizations[owner].Treaties[victim];
        if (relation.Ceasefire || relation.Peace || relation.Alliance)
        {
            if (!human)
            { Unit(movement.Unit).Orders = UnitOrder.None; operation.Result = new(false, 0); yield break; }
            if (movement.Options.Prompt && movement.LocalHuman)
            {
                using var allowed = confirmation.Begin(owner, victim, new() { Ceasefire = true, Peace = true, Alliance = true });
                while (!allowed.Completed)
                {
                    if (allowed.Resume(operation.TakeReply()) is { } pending)
                    { yield return new ForeignCityDecision.Attack(pending); }
                }
                if (allowed.Blocked)
                { operation.Result = new(false, 0); yield break; }
            }
        }
        operation.Result = new(true, 0);
    }
}
