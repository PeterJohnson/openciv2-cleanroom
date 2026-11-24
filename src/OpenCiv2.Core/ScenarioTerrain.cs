using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

/// <summary>S02 whole-byte terrain replacement. Formats owns the retained file flags.</summary>
public interface IScenarioTerrainEncoding
{
    void Replace(MapPoint position, byte value);
}

/// <summary>S02 §9 ChangeTerrain with actual C01/U01 deletion and retained V01 reset.
/// Rectangle scans yield periodically without clamping signed coordinates or changing their order.</summary>
public sealed class ScenarioTerrain(GameState world, StackOperations stacks, CityRemoval cities,
    CivilizationElimination lifecycle, EspionageIncidents incidents, Func<int, string, CivilizationCreation> create,
    IScenarioTerrainEncoding terrain, Action<MapPoint> redrawTile, Action<int, int> redrawMaps)
{
    public ScenarioTerrain(GameState world, StackOperations stacks, CityRemoval cities,
        CivilizationElimination lifecycle, EspionageIncidents incidents, Func<int, CivilizationCreation> create,
        IScenarioTerrainEncoding terrain, Action<MapPoint> redrawTile, Action<int, int> redrawMaps)
        : this(world, stacks, cities, lifecycle, incidents, (owner, _) => create(owner), terrain, redrawTile, redrawMaps) { }

    private GameState World => world;
    public Operation Begin(Event entry, string call) => new(this, entry, call);

    public sealed class Operation : IDisposable
    {
        private readonly ScenarioTerrain parent;
        private readonly IEnumerator<LifecycleDecision?> steps;
        private readonly List<Exception> failures = [];
        private ResearchReply? reply;
        private bool disposed, faulted;
        public int? Result { get; internal set; }
        public LifecycleDecision? Pending { get; private set; }
        public IReadOnlyList<Exception> PresentationFailures => failures.AsReadOnly();
        internal Operation(ScenarioTerrain parent, Event entry, string call)
        {
            ArgumentNullException.ThrowIfNull(entry);
            ArgumentException.ThrowIfNullOrEmpty(call);
            this.parent = parent;
            steps = parent.Execute(this, entry, call).GetEnumerator();
        }
        internal ResearchReply? TakeReply() { var value = reply; reply = null; return value; }
        internal void Report(IEnumerable<Exception> errors) => failures.AddRange(errors);
        internal void Present(Action action) { try { action(); } catch (Exception error) { failures.Add(error); } }
        public LifecycleDecision? Resume(ResearchReply? answer = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("A failed terrain action cannot resume"); }
            if (Result is not null)
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

    private static IEnumerable<LifecycleDecision?> Await(Operation op, CivilizationElimination.Operation child)
    {
        using (child)
        {
            while (child.Result is null)
            {
                var pending = child.ResumeDecision(op.TakeReply());
                if (child.Result is null)
                { yield return pending; }
            }
            op.Report(child.PresentationFailures);
        }
    }

    private IEnumerable<LifecycleDecision?> Execute(Operation op, Event entry, string call)
    {
        var rectangle = entry.Values.TerrainRectangle;
        int budget = 256, changed = 0;
        for (int x = rectangle[0].X; x <= rectangle[1].X; x = unchecked(x + 1))
        {
            for (int y = rectangle[0].Y; y <= rectangle[3].Y; y = unchecked(y + 1))
            {
                var point = new MapPoint(x, y);
                if (world.Map.Geometry.InBounds(point))
                {
                    if (CityQueries.At(world, point) is { } city)
                    {
                        foreach (var pending in Await(op, lifecycle.BeginRemoveCity(cities, city.Value, call + "/city/" + city.Value, incidents)))
                        { yield return pending; }
                    }
                    for (int unit = stacks.FirstAt(point); unit >= 0; unit = stacks.Next(unit))
                    {
                        foreach (var pending in Await(op, lifecycle.BeginDelete(unit, true, call + "/unit/" + unit, incidents)))
                        { yield return pending; }
                    }
                }
                if (--budget == 0)
                { budget = 256; yield return null; }
            }
            if (--budget == 0)
            { budget = 256; yield return null; }
        }
        for (int x = rectangle[0].X; x <= rectangle[1].X; x = unchecked(x + 1))
        {
            for (int y = rectangle[0].Y; y <= rectangle[3].Y; y = unchecked(y + 1))
            {
                var point = new MapPoint(x, y);
                if (world.Map.Geometry.InBounds(point))
                {
                    terrain.Replace(point, unchecked((byte)entry.Values.TerrainType));
                    world.Map.TileAt(point).Features = default;
                    world.Map.TileAt(point).Claimant = 0;
                    world.Map.TileAt(point).Region = 0;
                    for (int owner = 0; owner < EntityLimits.Civilizations; ++owner)
                    { MapKnowledge.Remember(world, owner, point); }
                    op.Present(() => redrawTile(point));
                    changed = unchecked(changed + 1);
                }
                if (--budget == 0)
                { budget = 256; yield return null; }
            }
            if (--budget == 0)
            { budget = 256; yield return null; }
        }
        for (int city = 0; city < world.Entities.Cities.End; ++city)
        {
            if (world.Entities.Cities.Storage[city].Token.Occupied)
            { CityArea.Refresh(world, new((ushort)city)); }
        }
        for (int owner = 0; owner < EntityLimits.Civilizations; ++owner)
        {
            bool found = false;
            for (int city = 0; city < world.Entities.Cities.End; ++city)
            {
                if (world.Entities.Cities.Storage[city].Token.Occupied && world.Entities.Cities.Storage[city].Owner.Value == owner)
                { found = true; }
            }
            if (found)
            { continue; }
            int count = world.Entities.Units.End;
            for (int unit = count - 1; unit >= 0; --unit)
            {
                if (!world.Entities.Units.Storage[unit].Token.Occupied || world.Entities.Units.Storage[unit].Owner.Value != owner)
                { continue; }
                foreach (var pending in Await(op, lifecycle.BeginDelete(unit, true, call + "/cityless/" + owner + "/unit/" + unit, incidents)))
                { yield return pending; }
            }
            if (owner == world.Multiplayer.LocalHuman && world.Globals.HumanPlayers.Contains(owner))
            { world.PendingEndReason = EndReason.Defeat; }
            for (int x = 0; x < world.Map.Geometry.Width; ++x)
            {
                for (int y = 0; y < world.Map.Geometry.Height; ++y)
                { world.Map.Mutate(new TileChange.Revelation(new(x, y), CivilizationSet.Only(owner), false)); }
            }
            using var child = create(owner, call + "/cityless/" + owner + "/creation");
            while (child.AdvanceDecision(64, op.TakeReply()) is null)
            { yield return child.Pending; }
            op.Report(child.PresentationFailures);
            op.Present(() => redrawMaps(world.Multiplayer.LocalHuman, 1));
        }
        op.Result = changed;
    }
}
