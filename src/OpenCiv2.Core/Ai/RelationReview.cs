using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.Ai;

public enum BorderNoticeKind { Terms, NearCity, AdmireCity, Withdrawn, SenateScandal, Violate }
public sealed record BorderNotice(BorderNoticeKind Kind, int Recipient, DiplomaticParty Owner, string City, int Units);
public sealed record BorderRequest(int Recipient, DiplomaticParty Owner, string City, int Cities, int AttitudeCategory, bool Repeated);
public abstract record RelationReviewDecision
{
    public sealed record Emissary(EmissaryDecision Decision) : RelationReviewDecision;
    public sealed record Border(BorderRequest Request) : RelationReviewDecision;
}
public interface IRelationReviewEffects
{
    void Notice(BorderNotice notice);
    void RedrawTile(MapPoint point);
    void RedrawMaps(int observer);
}

/// <summary>A08 §7 local relation review with actual border, stance, knowledge,
/// relocation, government and war children. An absent incoming delta is unknown.</summary>
public sealed partial class RelationReview(GameState world, RuleState rules, GameplayRandom random,
    BorderIntrusions borders, DiplomaticStance stance, EmissaryReception emissaries,
    StackOperations stacks, GovernmentChanges governments, DiplomaticWar war, IRelationReviewEffects effects)
{
    private GameState World => world;
    private DiplomaticParty Party(int owner) => new(owner, world.Names[owner].Tribe, world.Names[owner].Adjective);
    public Operation Begin(int owner, int other, string call, int? incomingDelta = null) => new(this, owner, other, call, incomingDelta);

    public sealed class Operation : IDisposable
    {
        private readonly RelationReview parent;
        private readonly IEnumerator<RelationReviewDecision> steps;
        private readonly List<Exception> failures = [];
        private int? answer;
        private bool faulted, disposed;
        public bool Completed { get; private set; }
        public bool KeepMarkers { get; internal set; }
        public int? Delta { get; internal set; }
        public RelationReviewDecision? Pending { get; private set; }
        public IReadOnlyList<Exception> PresentationFailures => failures.AsReadOnly();
        internal Operation(RelationReview parent, int owner, int other, string call, int? incoming)
        {
            ArgumentException.ThrowIfNullOrEmpty(call);
            this.parent = parent;
            Delta = incoming;
            steps = parent.Execute(this, owner, other, call).GetEnumerator();
        }
        internal int? TakeAnswer() { var result = answer; answer = null; return result; }
        internal void Report(Exception? error) { if (error is not null) { failures.Add(error); } }
        internal void Report(IEnumerable<Exception> errors) => failures.AddRange(errors);
        internal void Present(Action action) { try { action(); } catch (Exception error) { Report(error); } }
        public RelationReviewDecision? Resume(int? reply = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("A failed relation review cannot resume"); }
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

    private IEnumerable<RelationReviewDecision> Execute(Operation operation, int owner, int other, string call)
    {
        foreach (var decision in Border(operation, owner, other, call))
        { yield return decision; }
        if (!operation.KeepMarkers)
        {
            foreach (ref var unit in world.Entities.Units.Scanned)
            { if (unit.Token.Occupied) { unit.Flags.BorderIntruder = false; } }
        }
        operation.Delta = AdjustedDelta(owner, other, operation.Delta);
        if (operation.Delta is not { } delta)
        { throw new InvalidOperationException("A08 relation review requires an established incoming attitude delta on this path"); }
        DiplomaticAttitudes.Adjust(world, owner, other, delta);
    }

    private IEnumerable<RelationReviewDecision> Border(Operation operation, int owner, int other, string call)
    {
        int count = borders.Count(owner, other);
        var selected = world.Diplomacy.Intrusions.SelectedCity;
        var relation = world.Civilizations[owner].Treaties[other];
        if (count == 0 || relation.Alliance || relation.War)
        { yield break; }
        var city = selected ?? throw new InvalidOperationException("A counted intrusion requires a selected city");
        var position = world.Entities.Cities.At(city).Position;
        if (!relation.Peace)
        {
            if (world.Diplomacy.RecentPeace[other])
            { operation.KeepMarkers = true; yield break; }
            operation.Delta = world.Diplomacy.Intrusions.Weight;
            int complaint = random.PrimaryModulo(new ChoiceKey("A08.border-complaint", call), 2);
            if (world.Civilizations[owner].Treaties[other].Vendetta)
            { world.Diplomacy.Intrusions = world.Diplomacy.Intrusions with { RepeatOffenses = unchecked(world.Diplomacy.Intrusions.RepeatOffenses + 1) }; }
            int bound = unchecked(world.Civilizations[other].Reputation + world.Diplomacy.Intrusions.RepeatOffenses);
            if (bound > 2)
            {
                int roll = bound == 1 || unchecked(bound - 1) < 0 ? 0
                    : random.PrimaryCode(new ChoiceKey("A08.border-intrusion", call))
                        % unchecked(world.Civilizations[other].Reputation + world.Diplomacy.Intrusions.RepeatOffenses);
                if (roll > 1)
                { complaint = 0; DiplomaticRelations.Set(world, owner, other, new() { HostileIntent = true }); }
            }
            if ((world.Globals.Turn + owner) % 4 == 0)
            {
                using var reception = emissaries.Begin(other, owner, position, call + "/emissary");
                while (reception.Accepted is null)
                {
                    if (reception.Resume(operation.TakeAnswer()) is { } prompt)
                    { yield return new RelationReviewDecision.Emissary(prompt); }
                }
                operation.Report(reception.PresentationFailures);
                if (reception.Accepted == true)
                {
                    Reveal(operation, city, position, other, false);
                    Notice(complaint == 0 ? BorderNoticeKind.NearCity : BorderNoticeKind.AdmireCity, owner, other, city);
                }
                operation.Report(emissaries.Reset());
            }
            yield break;
        }
        if (world.Diplomacy.RecentPeace[other])
        {
            Notice(BorderNoticeKind.Terms, owner, other, city);
            operation.KeepMarkers = true;
            yield break;
        }
        int last = world.Civilizations[other].LastContactTurn[owner];
        if (last < world.Globals.Turn && world.Globals.Turn - last < 3)
        { operation.KeepMarkers = true; yield break; }
        bool repeated = world.Diplomacy.Intrusions.RepeatOffenses != 0;
        stance.Evaluate(other, owner, call + "/stance");
        int attitude = DiplomaticAttitudes.Category(world.Diplomacy.Attitude);
        Reveal(operation, city, position, other, true);
        if (!repeated)
        { operation.Delta = unchecked(world.Diplomacy.Intrusions.Weight - 1); }
        yield return new RelationReviewDecision.Border(new(other, Party(owner), world.Entities.Cities.At(city).Name,
            world.Diplomacy.Intrusions.Cities, attitude, repeated));
        int reply = operation.TakeAnswer()!.Value;
        if (!repeated)
        { operation.KeepMarkers = true; yield break; }
        if (reply == 0)
        {
            Withdraw(other);
            operation.Present(() => effects.RedrawMaps(world.Multiplayer.LocalHuman));
            Notice(BorderNoticeKind.Withdrawn, owner, other, city);
        }
        else
        {
            bool scandal = world.Civilizations[other].Government == Government.Democracy;
            if (!scandal)
            {
                int roll = random.PrimaryModulo(new ChoiceKey("A08.border-senate", call), 2);
                scandal = roll != 0 && world.Civilizations[other].Government == Government.Republic;
            }
            if (scandal && !(world.Globals.Status.Scenario && world.Scenario.Flags.RevolutionRestriction))
            {
                Notice(BorderNoticeKind.SenateScandal, owner, other, city);
                governments.Change(other, (int)Government.Anarchy);
            }
            DiplomaticAttitudes.Adjust(world, owner, other, 50);
            war.Declare(other, owner);
            Notice(BorderNoticeKind.Violate, owner, other, city);
        }
        operation.Report(emissaries.Reset());
    }

    private void Notice(BorderNoticeKind kind, int owner, int other, CitySlot city) =>
        effects.Notice(new(kind, other, Party(owner), world.Entities.Cities.At(city).Name, world.Diplomacy.Intrusions.Units));

    private void Reveal(Operation operation, CitySlot city, MapPoint position, int observer, bool explore)
    {
        bool reveal = !world.Entities.Cities.At(city).KnownTo.Contains(observer) && world.Entities.Cities.At(city).Owner.Value != observer;
        if (reveal)
        {
            MapKnowledge.RevealCity(world, observer, city);
            if (explore)
            { MapKnowledge.Explore(world, observer, position); }
        }
        if (reveal || !explore)
        { operation.Present(() => effects.RedrawTile(position)); }
    }

    private void Withdraw(int owner)
    {
        for (int i = 0; i < world.Entities.Units.End; ++i)
        {
            ref var unit = ref world.Entities.Units.Storage[i];
            if (!unit.Token.Occupied || !unit.Flags.BorderIntruder)
            { continue; }
            unit.Flags.BorderIntruder = false;
            if (unit.Owner.Value != owner)
            { continue; }
            if (CityQueries.Nearest(world, unit.Position, owner).Slot is not { } target)
            { continue; }
            stacks.Relocate(i, world.Entities.Cities.At(target).Position, true);
            unit.Visibility = default;
            unit.Orders = UnitOrder.None;
            if (rules.Definitions.Units[unit.Type.Value].Range != 0)
            { unit.Counter = 0; }
        }
    }
}
