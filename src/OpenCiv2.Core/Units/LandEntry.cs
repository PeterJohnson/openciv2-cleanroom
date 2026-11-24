using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Units;

/// <summary>Resolved inputs for U02's unspecified optional entry gates. No implicit prompt/autoplay default.</summary>
public readonly record struct LandEntrySelection(bool CaravanEntry, bool CityMissionEntry);
public sealed record LandEntryAnimation(UnitTarget Unit, int Actor, MapPoint Origin, MapPoint Destination, int Direction, int Ticks);
public enum LandEntrySound { ZoneDenied, AlliedRepair }
public sealed record AlliedRepairNotice(int Recipient, UnitTarget Unit, string CivilizationName, string UnitName, bool DiplomaticPortrait);
public abstract record LandEntryDecision
{
    public sealed record Caravan(CaravanDecision Decision) : LandEntryDecision;
    public sealed record City(CityMissionDecision Decision) : LandEntryDecision;
    public sealed record Unit(UnitMissionDecision Decision) : LandEntryDecision;
}
public sealed record LandEntryResult(bool Stopped, int? DestinationOwner);
public sealed record LandEntryServices(CaravanTrade Caravans, CityMissions Cities, UnitEspionage Units);
public interface ILandEntryEffects
{
    void Sound(LandEntrySound sound);
    void ZoneTutorial(int recipient);
    void PrepareEntry(LandEntryAnimation animation);
    void AnimateEntry(LandEntryAnimation animation);
    void Redraw(MapPoint point);
    void AlliedRepair(AlliedRepairNotice notice);
}

/// <summary>U02 ordered captured-Land checks with actual ZOC/revelation, caravan and mission children.
/// Returns the retained destination owner; the dispatcher owns boarding, combat, charge and cleanup.</summary>
public sealed class LandEntry(GameState world, RuleTables rules, StackOperations stacks, UnitCargo cargo,
    LandEntryServices services, ILandEntryEffects effects)
{
    private GameState World => world;
    private ref Unit Unit(UnitSlot slot) => ref world.Entities.Units.At(slot);
    private UnitRule Rule(UnitSlot slot) => rules.Units[Unit(slot).Type.Value];
    private bool Human(int owner) => world.Globals.HumanPlayers.Contains(owner);
    public Operation Begin(UnitSlot original, MovementContinuation movement, MovementStep step, bool sourceOcean,
        LandEntrySelection? selection, int incomingCityArgument, string call) => new(this, original, movement, step, sourceOcean, selection, incomingCityArgument, call);

    public sealed class Operation : IDisposable
    {
        private readonly LandEntry parent;
        private readonly IEnumerator<LandEntryDecision?> steps;
        private readonly List<Exception> presentationFailures = [];
        private ResearchReply? reply;
        private bool faulted, disposed;
        public LandEntryResult? Result { get; internal set; }
        public bool Completed => Result is not null;
        public LandEntryDecision? Pending { get; private set; }
        public IReadOnlyList<Exception> PresentationFailures => presentationFailures.AsReadOnly();
        internal Operation(LandEntry parent, UnitSlot original, MovementContinuation movement, MovementStep step,
            bool sourceOcean, LandEntrySelection? selection, int argument, string call)
        {
            ArgumentException.ThrowIfNullOrEmpty(call);
            this.parent = parent;
            steps = parent.Execute(this, original, movement, step, sourceOcean, selection, argument, call).GetEnumerator();
        }
        internal ResearchReply? TakeReply() { var value = reply; reply = null; return value; }
        internal void Report(IEnumerable<Exception> failures) => presentationFailures.AddRange(failures);
        internal void Present(Action action) { try { action(); } catch (Exception error) { presentationFailures.Add(error); } }
        public LandEntryDecision? Resume(ResearchReply? answer = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("A failed land-entry operation cannot resume"); }
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
    private void Restore(UnitSlot original, MovementContinuation movement, bool exhaustAutomated = false)
    {
        movement.Unit = original;
        if (exhaustAutomated && Unit(original).Flags.Automated)
        { UnitMovement.Exhaust(world, rules, ref Unit(original)); }
    }
    private void Enter(Operation operation, UnitSlot unit, MovementStep step, int ticks)
    {
        var animation = new LandEntryAnimation(new(unit, Unit(unit).Token), step.Owner, step.From, step.To, step.Direction, ticks);
        operation.Present(() => effects.PrepareEntry(animation));
        cargo.Lift(unit.Value, true);
        try
        { operation.Present(() => effects.AnimateEntry(animation)); }
        finally { stacks.Relocate(unit.Value, step.From, true); }
        operation.Present(() => effects.Redraw(step.To));
    }
    private IEnumerable<LandEntryDecision?> Execute(Operation operation, UnitSlot original, MovementContinuation movement,
        MovementStep step, bool sourceOcean, LandEntrySelection? selection, int argument, string call)
    {
        int owner = step.Owner;
        int? destinationOwner = step.DestinationOwner;
        if (step.Domain != RuleDomain.Land)
        { operation.Result = new(false, destinationOwner); yield break; }
        if (step.DestinationUnit is null && SpatialControl.InEnemyZone(world, step.From, owner) is not null
            && SpatialControl.InEnemyZone(world, step.To, owner) is not null && !sourceOcean && !Rule(movement.Unit).Abilities.IgnoreZonesOfControl)
        {
            if (movement.LocalHuman && movement.Options.Prompt)
            { operation.Present(() => effects.Sound(LandEntrySound.ZoneDenied)); }
            if (movement.LocalHuman && world.Globals.GameOptions.Tutorial)
            { effects.ZoneTutorial(world.Multiplayer.LocalHuman); }
            MapKnowledge.RevealAdjacentUnits(world, stacks, owner, step.To);
            Restore(original, movement, true);
            operation.Result = new(true, destinationOwner);
            yield break;
        }
        if (Rule(movement.Unit).Role == RuleRole.Trade && step.City is { } tradeCity)
        {
            if (destinationOwner is { } traderHost && traderHost != owner && Human(traderHost)
                && (selection ?? throw new InvalidOperationException("U02 caravan entry requires a resolved entry selector")).CaravanEntry)
            { Enter(operation, movement.Unit, step, 10); }
            using var trade = services.Caravans.BeginEntry(movement.Unit, tradeCity, call + "/caravan");
            while (!trade.Completed)
            {
                if (trade.ResumeDecision(operation.TakeReply()) is { } pending)
                { yield return new LandEntryDecision.Caravan(pending); }
                else if (!trade.Completed)
                { yield return null; }
            }
            operation.Report(trade.PresentationFailures);
            Restore(original, movement);
            if (trade.Result != 0)
            { operation.Result = new(true, destinationOwner); yield break; }
        }
        if (Rule(movement.Unit).Role == RuleRole.Diplomacy && destinationOwner is { } victim && victim != owner)
        {
            if (step.City is { } city)
            {
                if (Human(victim) && (selection ?? throw new InvalidOperationException("U02 city mission entry requires a resolved entry selector")).CityMissionEntry)
                { Enter(operation, movement.Unit, step, 20); }
                using var mission = services.Cities.Begin(movement.Unit.Value, city, argument, call + "/city");
                while (!mission.Completed)
                {
                    if (mission.Resume(operation.TakeReply()) is { } pending)
                    { yield return new LandEntryDecision.City(pending); }
                    else if (!mission.Completed)
                    { yield return null; }
                }
                operation.Report(mission.PresentationFailures);
                Restore(original, movement);
                operation.Result = new(true, destinationOwner);
                yield break;
            }
            using var unitMission = services.Units.Mission(movement.Unit.Value, step.DestinationUnit?.Value ?? -1, owner, call + "/unit");
            while (unitMission.Result is null)
            {
                if (unitMission.ResumeDecision(operation.TakeReply()) is { } pending)
                { yield return new LandEntryDecision.Unit(pending); }
                else if (unitMission.Result is null)
                { yield return null; }
            }
            operation.Report(unitMission.PresentationFailures);
            Restore(original, movement);
            if (unitMission.Result != 0)
            { operation.Result = new(true, destinationOwner); yield break; }
            destinationOwner = world.Map.CityOrUnitOwner(step.To);
        }
        if (step.City is { } alliedCity && destinationOwner is { } ally && ally != owner && world.Civilizations[owner].Treaties[ally].Alliance)
        {
            int heal = UnitMovement.MaximumHitPoints(rules, Unit(movement.Unit)) / 10;
            if (BuildingQueries.HasBuilding(world, alliedCity, BuildingIds.Barracks))
            { heal *= 2; }
            heal = Arithmetic.ClampAB(heal, 0, Unit(movement.Unit).Damage);
            Unit(movement.Unit).Damage = unchecked((byte)(Unit(movement.Unit).Damage - heal));
            UnitMovement.Exhaust(world, rules, ref Unit(movement.Unit));
            Unit(movement.Unit).Flags.Moved = false;
            if (!world.Multiplayer.Saved.AutoplayActive && Human(owner) && world.Multiplayer.LocalHuman == owner)
            {
                operation.Present(() => effects.Sound(LandEntrySound.AlliedRepair));
                effects.AlliedRepair(new(world.Multiplayer.LocalHuman, new(movement.Unit, Unit(movement.Unit).Token),
                    world.Names[ally].Tribe, Rule(movement.Unit).Identity.Name, world.Globals.AdvanceKnownBy[AdvanceIds.Communism].Count != 0));
            }
            Restore(original, movement, true);
            operation.Result = new(true, destinationOwner);
            yield break;
        }
        operation.Result = new(false, destinationOwner);
    }
}
