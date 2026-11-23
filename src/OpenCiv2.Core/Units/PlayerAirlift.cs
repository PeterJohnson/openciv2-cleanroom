using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Units;

public enum AirliftNoticeKind { Ship, Aircraft, NoAirport, AlreadyUsed, NoDestinations, ShotDown, Arrived }
public sealed record AirliftNotice(AirliftNoticeKind Kind, UnitSlot Unit, CitySlot? DisplayCity = null,
    string? Name = null, string? Destination = null);
public sealed record AirliftDestination(CitySlot City, string Name);
public abstract record AirliftDecision
{
    public sealed record Destination(int Actor, UnitTarget Unit, OwnedList<AirliftDestination> Cities) : AirliftDecision;
    public sealed record Fighters(int Actor, UnitTarget Unit, int Risk) : AirliftDecision;
    public sealed record Incident(EspionagePretext Pretext) : AirliftDecision;
}
public interface IPlayerAirliftEffects
{
    void Notice(AirliftNotice notice);
    void CityUnitChanged(UnitSlot unit);
}

/// <summary>U05 local player airlift. Command selection retains its actor/origin/owner; the direct
/// executor deliberately bypasses eligibility and uses real U01 placement and resumable deletion.</summary>
public sealed class PlayerAirlift(GameState world, RuleTables rules, StackOperations stacks,
    GameplayRandom random, CivilizationElimination lifecycle, EspionageIncidents incidents, IPlayerAirliftEffects effects)
{
    private GameState World => world;
    private ref Unit Unit(UnitSlot slot) => ref world.Entities.Units.At(slot);
    public Operation Begin(string call) => new(this, null, call);
    public Operation BeginTransfer(UnitSlot actor, CitySlot origin, CitySlot destination, int risk, int? enemy, string call) =>
        new(this, new(actor, origin, destination, risk, enemy), call);
    internal sealed record Transfer(UnitSlot Actor, CitySlot Origin, CitySlot Destination, int Risk, int? Enemy);

    public sealed class Operation : IDisposable
    {
        private readonly PlayerAirlift parent;
        private readonly IEnumerator<AirliftDecision?> steps;
        private int? answer;
        private bool disposed, faulted;
        public bool Completed { get; private set; }
        public bool Transferred { get; internal set; }
        public bool ShotDown { get; internal set; }
        public AirliftDecision? Pending { get; private set; }
        internal Operation(PlayerAirlift parent, Transfer? transfer, string call)
        {
            ArgumentException.ThrowIfNullOrEmpty(call);
            this.parent = parent;
            steps = (transfer is null ? parent.Command(this, call) : parent.Execute(this, transfer, call)).GetEnumerator();
        }
        internal int? TakeReply() { int? result = answer; answer = null; return result; }
        public AirliftDecision? Resume(int? reply = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("A failed airlift cannot resume"); }
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

    private IEnumerable<AirliftDecision?> Command(Operation operation, string call)
    {
        if (world.Globals.ActiveUnit is not { } actor)
        { yield break; }
        int owner = Unit(actor).Owner.Value;
        var domain = rules.Units[Unit(actor).Type.Value].Domain;
        if (domain is RuleDomain.Sea or RuleDomain.Air)
        { effects.Notice(new(domain == RuleDomain.Sea ? AirliftNoticeKind.Ship : AirliftNoticeKind.Aircraft, actor)); yield break; }
        var found = CityQueries.At(world, Unit(actor).Position);
        if (found is not { } origin || !BuildingQueries.HasBuilding(world, origin, BuildingIds.Airport))
        { effects.Notice(new(AirliftNoticeKind.NoAirport, actor, found)); yield break; }
        if (world.Entities.Cities.At(origin).Flags.Airlifted)
        { effects.Notice(new(AirliftNoticeKind.AlreadyUsed, actor, origin, world.Entities.Cities.At(origin).Name)); yield break; }
        var cities = new List<AirliftDestination>();
        for (int i = 0; i < world.Entities.Cities.End; ++i)
        {
            CitySlot slot = new((ushort)i);
            var city = world.Entities.Cities.At(slot);
            if (city.Token.Occupied && city.Owner.Value == owner && slot != origin && BuildingQueries.HasBuilding(world, slot, BuildingIds.Airport))
            { cities.Add(new(slot, city.Name)); }
        }
        if (cities.Count == 0)
        { effects.Notice(new(AirliftNoticeKind.NoDestinations, actor)); yield break; }
        yield return new AirliftDecision.Destination(owner, new(actor, Unit(actor).Token), new(cities));
        int selected = operation.TakeReply()!.Value;
        if (selected < 0)
        { yield break; }
        CitySlot destination = new(checked((ushort)selected));
        if (world.Entities.Cities.At(destination).Flags.Airlifted)
        { effects.Notice(new(AirliftNoticeKind.AlreadyUsed, actor, origin, world.Entities.Cities.At(destination).Name)); yield break; }
        var (risk, enemy) = Threats(owner, origin, destination);
        if (risk != 0)
        {
            yield return new AirliftDecision.Fighters(owner, new(actor, Unit(actor).Token), risk);
            if (operation.TakeReply() == 0)
            { yield break; }
        }
        foreach (var step in Execute(operation, new(actor, origin, destination, risk, enemy), call))
        { yield return step; }
    }

    private (int Risk, int? Enemy) Threats(int owner, CitySlot origin, CitySlot destination)
    {
        int risk = 0;
        int? enemy = null;
        for (int i = 0; i < world.Entities.Units.End; ++i)
        {
            ref var fighter = ref world.Entities.Units.Storage[i];
            if (!fighter.Token.Occupied || fighter.Owner.Value == owner || rules.Units[fighter.Type.Value].Role != RuleRole.AirSuperiority
                || !world.Civilizations[owner].Treaties[fighter.Owner.Value].War)
            { continue; }
            if (!Reaches(fighter, origin) && !Reaches(fighter, destination))
            { continue; }
            enemy = fighter.Owner.Value;
            risk = unchecked(risk + (fighter.Flags.Veteran ? 2 : 1));
        }
        return (risk, enemy);
    }

    private bool Reaches(in Unit fighter, CitySlot city)
    {
        int allowance = UnitMovement.Allowance(world, rules, fighter);
        int road = rules.Cosmic.RoadMovementMultiplier;
        int distance = world.Map.Geometry.StepDistance(world.Entities.Cities.At(city).Position, fighter.Position, world.Globals.Rules.FlatWorld);
        return Arithmetic.Divide32(allowance, road) >= distance;
    }

    private IEnumerable<AirliftDecision?> Execute(Operation operation, Transfer transfer, string call)
    {
        var (actor, origin, destination, risk, enemy) = transfer;
        world.Entities.Cities.At(origin).Flags.Airlifted = true;
        world.Entities.Cities.At(destination).Flags.Airlifted = true;
        UnitMovement.Exhaust(world, rules, ref Unit(actor));
        Unit(actor).Orders = UnitOrder.None;
        stacks.Relocate(actor.Value, world.Entities.Cities.At(destination).Position, true);
        operation.Transferred = true;
        effects.CityUnitChanged(actor);
        for (int i = 0; i < risk; ++i)
        {
            if (random.PrimaryModulo(new ChoiceKey("U05.player-airlift", call, i), 6) == 0)
            {
                if (Unit(actor).Owner.Value == world.Multiplayer.LocalHuman && !world.Multiplayer.Saved.AutoplayActive)
                { effects.Notice(new(AirliftNoticeKind.ShotDown, actor, Name: world.Names[enemy ?? throw new InvalidOperationException("A visible intercepted airlift requires its retained enemy owner")].Tribe)); }
                using var child = lifecycle.BeginDelete(actor.Value, true, call + "/loss", incidents);
                while (child.Result is null)
                {
                    if (child.Resume(operation.TakeReply()) is { } pending)
                    { yield return new AirliftDecision.Incident(pending); }
                    else if (child.Result is null)
                    { yield return null; }
                }
                operation.ShotDown = true;
                yield break;
            }
            if ((i & 63) == 63)
            { yield return null; }
        }
        if (Unit(actor).Owner.Value == world.Multiplayer.LocalHuman && !world.Multiplayer.Saved.AutoplayActive)
        { effects.Notice(new(AirliftNoticeKind.Arrived, actor, Name: world.Entities.Cities.At(origin).Name, Destination: world.Entities.Cities.At(destination).Name)); }
    }
}
