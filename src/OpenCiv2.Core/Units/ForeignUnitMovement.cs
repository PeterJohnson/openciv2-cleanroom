using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Units;

public enum ForeignUnitNoticeKind { RequiresAmphibious, NoncombatUnit, RequiresFighter }
public sealed record ForeignUnitNotice(ForeignUnitNoticeKind Kind, int Recipient, UnitTarget Mover, string UnitName);
public sealed record FatigueChoice(int Actor, UnitTarget Mover, int MovementLeft, int RoadMultiplier);
public abstract record ForeignUnitDecision
{
    public sealed record Expel(ExpulsionOffer Offer) : ForeignUnitDecision;
    public sealed record Fatigue(FatigueChoice Choice) : ForeignUnitDecision;
    public sealed record Combat(CombatDecision Decision) : ForeignUnitDecision;
}
public sealed record ForeignUnitResult(int Value, bool Finish, MapPoint Destination);
public interface IForeignUnitEffects
{
    void Notice(ForeignUnitNotice notice);
    void Redraw(MapPoint point);
}

/// <summary>U02 foreign-unit phase after land/boarding checks. Composes actual expulsion and U06;
/// returns the specified stop/common-finish route. Outer movement owns finish, bound checks and cleanup.</summary>
public sealed class ForeignUnitMovement(GameState world, RuleTables rules, StackOperations stacks,
    UnitExpulsion expulsion, CombatExecution combat, IForeignUnitEffects effects)
{
    private GameState World => world;
    private ref Unit Unit(UnitSlot slot) => ref world.Entities.Units.At(slot);
    private UnitRule Rule(UnitSlot slot) => rules.Units[Unit(slot).Type.Value];
    private bool Human(int owner) => world.Globals.HumanPlayers.Contains(owner);
    public Operation Begin(UnitSlot original, UnitSlot target, MovementContinuation movement, MovementStep step, bool sourceOcean,
        string incomingCityName, string genericDiplomatName, int? incomingExpulsionChoice, string incomingCombatArgument,
        string call, bool? scenarioShowsCombat = null, int roundBudget = 64) =>
        new(this, original, target, movement, step, sourceOcean, incomingCityName, genericDiplomatName, incomingExpulsionChoice,
            incomingCombatArgument, call, scenarioShowsCombat, roundBudget);

    public sealed class Operation : IDisposable
    {
        private readonly ForeignUnitMovement parent;
        private readonly IEnumerator<ForeignUnitDecision?> steps;
        private readonly List<Exception> presentationFailures = [];
        private ResearchReply? reply;
        private bool disposed, faulted;
        public ForeignUnitResult? Result { get; internal set; }
        public bool Completed => Result is not null;
        public ForeignUnitDecision? Pending { get; private set; }
        public IReadOnlyList<Exception> PresentationFailures => presentationFailures.AsReadOnly();
        internal Operation(ForeignUnitMovement parent, UnitSlot original, UnitSlot target, MovementContinuation movement,
            MovementStep step, bool sourceOcean, string incomingCityName, string genericDiplomatName, int? incomingChoice,
            string argument, string call, bool? scenarioShowsCombat, int roundBudget)
        {
            ArgumentException.ThrowIfNullOrEmpty(call);
            ArgumentNullException.ThrowIfNull(incomingCityName);
            ArgumentNullException.ThrowIfNull(genericDiplomatName);
            ArgumentNullException.ThrowIfNull(argument);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(roundBudget);
            this.parent = parent;
            steps = parent.Execute(this, original, target, movement, step, sourceOcean, incomingCityName, genericDiplomatName,
                incomingChoice, argument, call, scenarioShowsCombat, roundBudget).GetEnumerator();
        }
        internal ResearchReply? TakeReply() { var value = reply; reply = null; return value; }
        internal void Report(IEnumerable<Exception> errors) => presentationFailures.AddRange(errors);
        internal void Present(Action action) { try { action(); } catch (Exception error) { presentationFailures.Add(error); } }
        public ForeignUnitDecision? Resume(ResearchReply? answer = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("A failed foreign-unit movement cannot resume"); }
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
    private void Notice(ForeignUnitNoticeKind kind, UnitSlot mover) =>
        effects.Notice(new(kind, world.Multiplayer.LocalHuman, new(mover, Unit(mover).Token), Rule(mover).Identity.Name));
    private void Restore(UnitSlot original, MovementContinuation movement, bool exhaustAutomated = false)
    {
        movement.Unit = original;
        if (exhaustAutomated && Unit(original).Flags.Automated)
        { UnitMovement.Exhaust(world, rules, ref Unit(original)); }
    }

    private IEnumerable<ForeignUnitDecision?> Execute(Operation operation, UnitSlot original, UnitSlot target,
        MovementContinuation movement, MovementStep step, bool sourceOcean, string incomingCityName, string genericDiplomatName,
        int? incomingChoice, string argument, string call, bool? scenarioShowsCombat, int roundBudget)
    {
        // Zero is the stop result; only the diplomat and no-execute combat branches replace it.
        int value = 0, owner = step.Owner, victim = Unit(target).Owner.Value;
        if (step.Domain == RuleDomain.Land && sourceOcean && !Rule(movement.Unit).Abilities.Amphibious)
        {
            Restore(original, movement);
            if (movement.Options.Prompt && movement.LocalHuman)
            { Notice(ForeignUnitNoticeKind.RequiresAmphibious, movement.Unit); }
        }
        else if (step.DestinationTerrain != Terrain.Ocean && Rule(movement.Unit).Abilities.Submarine)
        { Restore(original, movement); }
        else if (Rule(target).Abilities.Submarine && stacks.Count(target.Value) == 1 && !Unit(target).Visibility.Contains(owner)
            && Unit(target).Owner.Value != owner && Human(owner))
        {
            Restore(original, movement);
            if (movement.Options.Prompt)
            {
                Unit(movement.Unit).Orders = UnitOrder.None;
                MapKnowledge.MarkUnitSeen(world, target, owner);
                operation.Present(() => effects.Redraw(step.To));
            }
        }
        else if (Rule(movement.Unit).Role == RuleRole.Diplomacy)
        { Unit(movement.Unit).Orders = UnitOrder.None; value = Human(owner) ? 1 << owner : 0; Restore(original, movement); }
        else if (Rule(movement.Unit).Attack == 0)
        {
            if (movement.Options.Prompt && movement.LocalHuman && Human(owner))
            { Notice(ForeignUnitNoticeKind.NoncombatUnit, movement.Unit); }
            Restore(original, movement, true);
        }
        else if (step.City is null && world.Map.AirbaseOwner(step.To) is null && stacks.CountLongRangeAircraft(target.Value, rules) != 0
            && !Rule(movement.Unit).Abilities.AttackAir && stacks.CountAircraftCarriers(target.Value, rules) == 0)
        {
            if (movement.Options.Prompt && movement.LocalHuman)
            { Notice(ForeignUnitNoticeKind.RequiresFighter, movement.Unit); }
            Restore(original, movement, true);
        }
        else
        {
            using var expelled = expulsion.Begin(original, target, movement, step, victim, incomingCityName, genericDiplomatName, incomingChoice, call + "/expel");
            while (!expelled.Completed)
            {
                if (expelled.Resume(operation.TakeReply()?.Selected) is { } pending)
                { yield return new ForeignUnitDecision.Expel(pending); }
            }
            operation.Report(expelled.PresentationFailures);
            if (expelled.Stopped)
            { operation.Result = new(0, false, step.To); yield break; }
            if (UnitMovement.Left(world, rules, Unit(movement.Unit)) < rules.Cosmic.RoadMovementMultiplier)
            {
                if (movement.Options.Prompt && movement.LocalHuman)
                {
                    yield return new ForeignUnitDecision.Fatigue(new(owner, new(movement.Unit, Unit(movement.Unit).Token),
                        UnitMovement.Left(world, rules, Unit(movement.Unit)), rules.Cosmic.RoadMovementMultiplier));
                    if (operation.TakeReply()!.Value.Selected != 0)
                    { Restore(original, movement); operation.Result = new(0, false, step.To); yield break; }
                }
                else if (!Human(owner) && UnitMovement.Left(world, rules, Unit(movement.Unit)) < rules.Cosmic.RoadMovementMultiplier - 1)
                { Restore(original, movement); operation.Result = new(0, false, step.To); yield break; }
            }
            world.Globals.MovingUnit = movement.Unit;
            Unit(movement.Unit).Flags.Garrison = false;
            using var attack = combat.Begin(movement.Unit, step.Direction, movement.Options, argument, call + "/combat", scenarioShowsCombat, roundBudget);
            while (!attack.Completed)
            {
                if (attack.Resume(operation.TakeReply()) is { } pending)
                { yield return new ForeignUnitDecision.Combat(pending); }
                else if (!attack.Completed)
                { yield return null; }
            }
            operation.Report(attack.PresentationFailures);
            if (!movement.Options.Execute)
            { value = attack.Result == 0 ? 1 : 0; Restore(original, movement); }
            else if (attack.Result != 0)
            {
                movement.AttackDone = true;
                movement.Unit = world.Globals.MovingUnit ?? throw new InvalidOperationException("Combat did not retain its finishing moving unit");
                operation.Result = new(0, true, step.From);
                yield break;
            }
        }
        operation.Result = new(value, false, step.To);
    }
}
