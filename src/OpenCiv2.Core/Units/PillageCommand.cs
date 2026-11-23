using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Units;

public enum PillageFeature { Farmland, Irrigation, Mine, Airbase, Fortress, Railroad, Road }
public enum PillageOutcome { Applied, Cancelled, Unavailable }
public sealed record PillageChoice(PillageFeature Feature, MapFeatures Removal);
public abstract record PillageDecision
{
    public sealed record Menu(int Owner, OwnedList<PillageChoice> Choices) : PillageDecision;
    public sealed record Attack(AttackWarning Warning) : PillageDecision;
}

/// <summary>W04 player pillage eligibility, retained choice and real V04 declaration before core pillage.
/// Native dialog/text lifetimes and unused movement-option backing are not gameplay inputs.</summary>
public sealed class PillageCommand(GameState world, RuleTables rules, AttackConfirmation confirmation,
    DiplomaticWar war, Pillage pillage)
{
    private GameState World => world;
    private ref Unit Unit(UnitSlot slot) => ref world.Entities.Units.At(slot);
    public static bool Available(GameState world, RuleTables rules, UnitSlot slot)
    {
        ref var unit = ref world.Entities.Units.At(slot);
        var rule = rules.Units[unit.Type.Value];
        return rule.Attack != 0 && rule.Domain == RuleDomain.Land && world.Map.CityOwner(unit.Position) is null
            && (world.Map.TileAt(unit.Position).Features & (MapFeatures.Irrigation | MapFeatures.Mine | MapFeatures.Road | MapFeatures.Fortification)) != 0;
    }
    public Operation Begin(UnitSlot unit) => new(this, unit);

    public sealed class Operation : IDisposable
    {
        private readonly PillageCommand parent;
        private readonly IEnumerator<PillageDecision> steps;
        private readonly List<Exception> failures = [];
        private int? reply;
        private bool disposed, faulted;
        public PillageOutcome? Result { get; internal set; }
        public bool Completed => Result is not null;
        public PillageDecision? Pending { get; private set; }
        public IReadOnlyList<Exception> PresentationFailures => failures.AsReadOnly();
        internal Operation(PillageCommand parent, UnitSlot unit)
        { this.parent = parent; steps = parent.Execute(this, unit).GetEnumerator(); }
        internal int? TakeReply() { var answer = reply; reply = null; return answer; }
        internal void Report(IEnumerable<Exception> errors) => failures.AddRange(errors);
        public PillageDecision? Resume(int? answer = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("Failed pillage command cannot resume"); }
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

    private IEnumerable<PillageDecision> Execute(Operation operation, UnitSlot unit)
    {
        int owner = Unit(unit).Owner.Value;
        var point = Unit(unit).Position;
        if (!Available(world, rules, unit))
        { operation.Result = PillageOutcome.Unavailable; yield break; }
        var features = world.Map.TileAt(point).Features;
        List<PillageChoice> choices = [];
        if (features.HasFlag(MapFeatures.Irrigation | MapFeatures.Mine))
        { choices.Add(new(PillageFeature.Farmland, MapFeatures.Irrigation | MapFeatures.Mine)); }
        else if (features.HasFlag(MapFeatures.Irrigation))
        { choices.Add(new(PillageFeature.Irrigation, MapFeatures.Irrigation)); }
        else if (features.HasFlag(MapFeatures.Mine))
        { choices.Add(new(PillageFeature.Mine, MapFeatures.Mine)); }
        if (features.HasFlag(MapFeatures.Fortification))
        {
            bool airbase = features.HasFlag(MapFeatures.CitySite);
            choices.Add(new(airbase ? PillageFeature.Airbase : PillageFeature.Fortress,
                MapFeatures.Fortification | (airbase ? MapFeatures.CitySite : MapFeatures.None)));
        }
        if (features.HasFlag(MapFeatures.Railroad))
        { choices.Add(new(PillageFeature.Railroad, MapFeatures.Railroad)); }
        else if (features.HasFlag(MapFeatures.Road))
        { choices.Add(new(PillageFeature.Road, MapFeatures.Road)); }
        var selection = MapFeatures.None;
        if (choices.Count > 1)
        {
            yield return new PillageDecision.Menu(owner, new(choices));
            int choice = operation.TakeReply()!.Value;
            if (choice < 0)
            { operation.Result = PillageOutcome.Cancelled; yield break; }
            if (choice >= choices.Count)
            { throw new ArgumentOutOfRangeException(nameof(choice)); }
            selection = choices[choice].Removal;
        }
        var nearest = CityQueries.Nearest(world, point).Slot;
        if (nearest is { Value: > 0 } city && world.Entities.Cities.At(city).Owner.Value != owner)
        {
            int other = world.Entities.Cities.At(city).Owner.Value;
            using var permission = confirmation.Begin(owner, other, new() { Ceasefire = true, Peace = true, Alliance = true });
            while (!permission.Completed)
            {
                var warning = permission.Resume(operation.TakeReply());
                if (warning is not null)
                { yield return new PillageDecision.Attack(warning); }
            }
            if (permission.Blocked)
            { operation.Result = PillageOutcome.Cancelled; yield break; }
            if (!world.Civilizations[owner].Treaties[other].War)
            { war.Declare(owner, other); }
        }
        using var child = pillage.Begin(unit, selection);
        while (!child.Completed)
        {
            var warning = child.Resume(operation.TakeReply());
            if (warning is not null)
            { yield return new PillageDecision.Attack(warning); }
        }
        operation.Report(child.PresentationFailures);
        operation.Result = child.Applied == true ? PillageOutcome.Applied : PillageOutcome.Cancelled;
    }
}
