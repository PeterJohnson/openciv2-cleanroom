using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Units;

public sealed record ExpulsionOffer(int Actor, int Victim, UnitTarget Mover, UnitTarget Target,
    string VictimAdjective, string TargetName, string ActorPlural);
public enum ExpulsionNoticeKind { CannotExpel, VictimReturned, AlliedReturn, Expelled }
public sealed record ExpulsionNotice(ExpulsionNoticeKind Kind, int Recipient, ExpulsionOffer Offer, string CityName);
public sealed record ExpulsionEntry(UnitTarget Mover, int Actor, MapPoint Origin, MapPoint Destination, int Direction);
public interface IUnitExpulsionEffects
{
    void Notice(ExpulsionNotice notice);
    void Enter(ExpulsionEntry entry);
    void Redraw(MapPoint destination);
}

/// <summary>U02 foreign-unit expulsion, using actual nearest-city/Palace, U01 cargo/placement and
/// V07 race queries. The dispatcher owns stop cleanup. Suppressed unassigned choices are explicit inputs.</summary>
public sealed class UnitExpulsion(GameState world, RuleTables rules, StackOperations stacks, UnitCargo cargo,
    GameplayRandom random, IUnitExpulsionEffects effects)
{
    private GameState World => world;
    private ref Unit Unit(UnitSlot slot) => ref world.Entities.Units.At(slot);
    private UnitRule Rule(UnitSlot slot) => rules.Units[Unit(slot).Type.Value];
    private bool Human(int owner) => world.Globals.HumanPlayers.Contains(owner);
    private ExpulsionOffer Offer(UnitSlot mover, UnitSlot target, int owner, int victim, string? targetName = null) =>
        new(owner, victim, new(mover, Unit(mover).Token), new(target, Unit(target).Token), world.Names[victim].Adjective,
            targetName ?? Rule(target).Identity.Name, world.Names[owner].Tribe);
    public Operation Begin(UnitSlot original, UnitSlot target, MovementContinuation movement, MovementStep step, int victim,
        string incomingCityName, string genericDiplomatName, int? incomingChoice, string call) =>
        new(this, original, target, movement, step, victim, incomingCityName, genericDiplomatName, incomingChoice, call);

    public sealed class Operation : IDisposable
    {
        private readonly UnitExpulsion parent;
        private readonly IEnumerator<ExpulsionOffer> steps;
        private readonly List<Exception> presentationFailures = [];
        private int? reply;
        private bool disposed, faulted;
        public bool Completed { get; private set; }
        public bool Stopped { get; internal set; }
        public bool Expelled { get; internal set; }
        public int? Choice { get; internal set; }
        public ExpulsionOffer? Pending { get; private set; }
        public IReadOnlyList<Exception> PresentationFailures => presentationFailures.AsReadOnly();
        internal Operation(UnitExpulsion parent, UnitSlot original, UnitSlot target, MovementContinuation movement, MovementStep step,
            int victim, string incomingCityName, string genericDiplomatName, int? incomingChoice, string call)
        {
            ArgumentException.ThrowIfNullOrEmpty(call);
            ArgumentNullException.ThrowIfNull(incomingCityName);
            ArgumentNullException.ThrowIfNull(genericDiplomatName);
            this.parent = parent;
            Choice = incomingChoice;
            steps = parent.Execute(this, original, target, movement, step, victim, incomingCityName, genericDiplomatName, call).GetEnumerator();
        }
        internal int TakeReply() { int value = reply!.Value; reply = null; return value; }
        internal void Present(Action action) { try { action(); } catch (Exception error) { presentationFailures.Add(error); } }
        public ExpulsionOffer? Resume(int? answer = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("A failed unit expulsion cannot resume"); }
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
                Completed = true;
                steps.Dispose();
                return null;
            }
            catch { faulted = true; steps.Dispose(); throw; }
        }
        public void Dispose() { disposed = true; steps.Dispose(); }
    }

    private IEnumerable<ExpulsionOffer> Execute(Operation operation, UnitSlot original, UnitSlot target, MovementContinuation movement,
        MovementStep step, int victim, string cityName, string genericDiplomatName, string call)
    {
        int owner = step.Owner;
        var treaty = world.Civilizations[owner].Treaties[victim];
        if (Rule(target).Role != RuleRole.Diplomacy || stacks.Count(target.Value) != 1 || !(treaty.Ceasefire || treaty.Peace))
        { yield break; }
        var nearest = CityQueries.Nearest(world, step.To).Slot;
        if (nearest is not { } nearby || world.Entities.Cities.At(nearby).Owner.Value != owner)
        {
            if (!world.Multiplayer.Saved.AutoplayActive && Human(owner))
            {
                string name = Rule(target).Identity.Name;
                if (Rule(target).Role != RuleRole.Diplomacy)
                { name = genericDiplomatName; }
                effects.Notice(new(ExpulsionNoticeKind.CannotExpel, world.Multiplayer.LocalHuman, Offer(original, target, owner, victim, name), cityName));
            }
            yield break;
        }
        var offer = Offer(original, target, owner, victim);
        if (!world.Civilizations[owner].Treaties[victim].Alliance && Human(owner))
        {
            if (!world.Multiplayer.Saved.AutoplayActive)
            {
                yield return offer;
                operation.Choice = operation.TakeReply();
            }
        }
        else
        { operation.Choice = 0; }
        movement.Unit = original;
        int choice = operation.Choice ?? throw new InvalidOperationException("Autoplay expulsion requires the retained incoming choice");
        movement.Answer = choice;
        if (choice == 2)
        { operation.Stopped = true; yield break; }
        if (choice != 0)
        { yield break; }
        if (Human(owner) || Human(victim))
        {
            cargo.Lift(original.Value, true);
            try
            { operation.Present(() => effects.Enter(new(new(original, Unit(original).Token), owner, step.From, step.To, step.Direction))); }
            finally { stacks.RelocateStack(original.Value, step.From, true); }
        }
        int best = Rule(target).Role == RuleRole.Diplomacy ? -1 : 9999;
        CitySlot? selected = null;
        for (int i = 0; i < world.Entities.Cities.End; ++i)
        {
            ref readonly var candidate = ref world.Entities.Cities.Storage[i];
            if (!candidate.Token.Occupied || candidate.Owner.Value != victim)
            { continue; }
            var slot = new CitySlot((ushort)i);
            if (Rule(target).Role == RuleRole.Diplomacy)
            {
                int score = candidate.Size + (BuildingQueries.HasBuilding(world, slot, BuildingIds.Palace) ? 50 : 0);
                if (score > best)
                { best = score; selected = slot; }
            }
            else
            {
                var destinationCity = step.City ?? throw new InvalidOperationException("Changed-role expulsion requires a captured destination city");
                int score = world.Map.Geometry.WeightedDistance(step.From, world.Entities.Cities.At(destinationCity).Position, world.Globals.Rules.FlatWorld);
                if (score < best)
                { best = score; selected = slot; }
            }
        }
        if (selected is { } destination)
        {
            stacks.Relocate(target.Value, world.Entities.Cities.At(destination).Position, true);
            Unit(target).Visibility = default;
            cityName = world.Entities.Cities.At(destination).Name;
        }
        operation.Present(() => effects.Redraw(step.To));
        if (!world.Multiplayer.Saved.AutoplayActive && Human(victim))
        { effects.Notice(new(ExpulsionNoticeKind.VictimReturned, world.Multiplayer.LocalHuman, offer, cityName)); }
        else if (!world.Multiplayer.Saved.AutoplayActive && Human(owner) && world.Civilizations[owner].Treaties[victim].Alliance)
        { effects.Notice(new(ExpulsionNoticeKind.AlliedReturn, world.Multiplayer.LocalHuman, offer, cityName)); }
        else if (Human(owner))
        {
            if (!world.Multiplayer.Saved.AutoplayActive)
            { effects.Notice(new(ExpulsionNoticeKind.Expelled, world.Multiplayer.LocalHuman, offer, cityName)); }
            if (!SpaceshipQueries.RaceBehind(world, rules, victim))
            { DiplomaticAttitudes.Adjust(world, victim, owner, random.PrimaryModulo(new ChoiceKey("U02.expulsion-attitude", call), 4)); }
            else
            {
                ref var reverse = ref world.Civilizations[victim].Treaties[owner];
                if (!reverse.HostileIntent)
                { reverse.HostileIntent = true; }
                else
                { reverse.Ceasefire = reverse.Peace = reverse.HostileIntent = false; reverse.BrokenTreaty = true; }
            }
        }
        operation.Expelled = operation.Stopped = true;
    }
}
