using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

/// <summary>V05 §8 local offered-city execution. Actual scenario and cargo/lifecycle children
/// retain decisions. The enclosing negotiation owns admission and its final civilization checks.</summary>
public sealed class DiplomaticCities(GameState world, RuleTables rules, CivilizationElimination lifecycle,
    UnitLoss loss, EspionageIncidents incidents, ScenarioTriggers events, WonderEffects wonders,
    ICaptureProductionEffects effects, WonderBuildRestrictions? scenario = null)
{
    private GameState World => world;
    public Operation Begin(OwnedList<CityToken> offered, int recipient, string call) => new(this, offered, recipient, call);
    public sealed class Operation : IDisposable
    {
        private readonly DiplomaticCities parent;
        private readonly IEnumerator<LifecycleDecision?> steps;
        private readonly List<Exception> failures = [];
        private ResearchReply? reply;
        private bool disposed, faulted;
        public bool Completed { get; private set; }
        public LifecycleDecision? Pending { get; private set; }
        public CitySlot? CurrentCity { get; internal set; }
        public IReadOnlyList<Exception> PresentationFailures => failures.AsReadOnly();
        internal Operation(DiplomaticCities parent, OwnedList<CityToken> offered, int recipient, string call)
        {
            ArgumentException.ThrowIfNullOrEmpty(call);
            if (recipient < 0 || recipient >= EntityLimits.Civilizations)
            { throw new ArgumentOutOfRangeException(nameof(recipient)); }
            this.parent = parent;
            steps = parent.Execute(this, offered, recipient, call).GetEnumerator();
        }
        internal ResearchReply? TakeReply() { var value = reply; reply = null; return value; }
        internal void Report(IEnumerable<Exception> errors) => failures.AddRange(errors);
        internal void Present(Action action) { try { action(); } catch (Exception error) { failures.Add(error); } }
        internal CitySlot RequireCity() => CurrentCity ?? throw new InvalidOperationException("City cession has no backed current city");
        public LifecycleDecision? Resume(ResearchReply? answer = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("A failed city cession cannot resume"); }
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
    private CitySlot? Find(CityToken token)
    {
        for (int slot = world.Entities.Cities.End - 1; slot >= 0; --slot)
        {
            ref var city = ref world.Entities.Cities.Storage[slot];
            if (city.Token.Occupied && city.Token == token)
            { return new((ushort)slot); }
        }
        return null;
    }
    private IEnumerable<LifecycleDecision?> Execute(Operation op, OwnedList<CityToken> offered, int recipient, string call)
    {
        for (int item = 0; item < offered.Count; ++item)
        {
            if (Find(offered[item]) is not { } city)
            { continue; }
            foreach (var pending in Cede(op, city, recipient, call + "/city/" + item))
            { yield return pending; }
        }
    }
    private IEnumerable<LifecycleDecision?> Cede(Operation op, CitySlot original, int recipient, string call)
    {
        op.CurrentCity = original;
        var center = world.Entities.Cities.At(original).Position;
        int old = unchecked((sbyte)world.Entities.Cities.At(original).Owner.Value);
        int turn = world.Globals.Turn;
        foreach (int building in new[] { BuildingIds.Palace, BuildingIds.Temple, BuildingIds.Cathedral, BuildingIds.Courthouse })
        { BuildingQueries.SetBuilding(world, original, building, false); }
        world.Civilizations[recipient].LastCityTurn = world.Globals.Turn;
        unchecked
        {
            ++world.Civilizations[recipient].CityCount;
            if (world.Civilizations[old].CityCount != 0)
            { --world.Civilizations[old].CityCount; }
        }
        foreach (var offset in MapNeighborhoods.City)
        {
            var point = MapNeighborhoods.Offset(world.Map.Geometry, center, offset, world.Globals.Rules.FlatWorld);
            if (!world.Map.Geometry.InBounds(point))
            { continue; }
            world.Map.TileAt(point).Claimant = (byte)recipient;
            MapKnowledge.Explore(world, recipient, point);
        }
        world.Entities.Cities.At(original).Owner = new((byte)recipient);
        MapKnowledge.RevealCity(world, old, original);
        if (world.Events.Loaded)
        {
            using var child = events.BeginTaken(original, recipient, old, call + "/taken");
            while (child.Result is null)
            {
                var pending = child.Resume(op.TakeReply());
                if (child.Result is null)
                { yield return pending; }
            }
            op.Report(child.PresentationFailures);
        }
        world.Map.TileAt(center).Owner = (byte)recipient;
        for (int slot = world.Entities.Units.End - 1; slot >= 0; --slot)
        {
            var unit = world.Entities.Units.Storage[slot];
            if (!unit.Token.Occupied || unchecked((sbyte)unit.Owner.Value) != old)
            { continue; }
            var current = op.RequireCity();
            var position = world.Entities.Cities.At(current).Position;
            if (unit.Home == current && unit.Position != position)
            {
                using var child = lifecycle.BeginEraseCargo(loss, slot, call + "/unit/" + slot, incidents);
                while (child.Result is null)
                {
                    var pending = child.ResumeDecision(op.TakeReply());
                    if (child.Result is null)
                    { yield return pending; }
                }
                op.Report(child.PresentationFailures);
            }
            else if (unit.Position == position)
            { TransferUnit(op, slot, recipient, old); }
        }
        var repaired = op.RequireCity();
        TransferredCityProduction.Repair(world, rules, repaired, recipient, scenario);
        op.Present(() => effects.RedrawNeighbors(world.Entities.Cities.At(repaired).Position));
        Reveal(center);
        foreach (var offset in MapNeighborhoods.Directions)
        {
            var point = MapNeighborhoods.Offset(world.Map.Geometry, center, offset, world.Globals.Rules.FlatWorld);
            if (world.Map.Geometry.InBounds(point))
            { Reveal(point); }
        }
        world.Entities.Cities.At(repaired).Founder = new((byte)old);
        world.Entities.Cities.At(repaired).TurnsCaptured = unchecked((byte)turn);
        if (BuildingQueries.ActiveCity(world, rules, WonderIds.Leonardo) == repaired.Value)
        { wonders.UpgradeForLeonardo(recipient); }

        void Reveal(MapPoint point)
        {
            if (MapKnowledge.Explore(world, recipient, point))
            { op.Present(() => effects.Redraw(point)); }
        }
    }
    private void TransferUnit(Operation op, int slot, int recipient, int old)
    {
        ref var unit = ref world.Entities.Units.Storage[slot];
        unchecked
        { ++world.Civilizations[recipient].UnitsActive[unit.Type.Value]; --world.Civilizations[old].UnitsActive[unit.Type.Value]; }
        unit.Owner = new((byte)recipient);
        unit.Home = null;
        unit.MovesUsed = 0;
        unit.Orders = UnitOrder.None;
        op.CurrentCity = CityQueries.Nearest(world, unit.Position, recipient).Slot;
        if (op.CurrentCity is { } home && world.Entities.Cities.At(home).Owner.Value == recipient)
        { unit.Home = home; }
        unit.Visibility = unit.Visibility.With(recipient);
        bool military = unchecked((sbyte)rules.Units[unit.Type.Value].Role) < 5;
        unchecked
        {
            if (military)
            { ++world.Civilizations[recipient].MilitaryCount; }
            ++world.UnitTotals[recipient];
            if (world.Civilizations[old].MilitaryCount != 0 && military)
            { --world.Civilizations[old].MilitaryCount; }
            if (world.UnitTotals[old] != 0)
            { --world.UnitTotals[old]; }
        }
    }
}
