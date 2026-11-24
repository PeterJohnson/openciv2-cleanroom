using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.City;

/// <summary>The retained last creation can be null after a later allocation failure.</summary>
public sealed record CapturePartisansNotice(int NewOwner, int OldOwner, string Adjective, string CityName, UnitSlot? LastCreation);
public interface ICapturePartisansEffects
{
    void InfantrySound();
    void Redraw(MapPoint point);
    void Notice(CapturePartisansNotice notice);
}

/// <summary>C09 local partisan helper. The parent owns survival/technology/government admission.</summary>
public sealed class CapturePartisans(GameState world, RuleTables rules, UnitCreation creation, GameplayRandom random,
    ICapturePartisansEffects effects)
{
    private GameState World => world;

    /// <summary>Compatibility entry for an explicitly synchronous U01 composition.</summary>
    public int Create(CitySlot city, int newOwner, int oldOwner, MapPoint center, int mode, string originalName, string call)
    {
        using var operation = new Operation(this, city, newOwner, oldOwner, center, mode, originalName, call, false);
        while (operation.Result is null)
        {
            if (operation.Resume() is not null)
            { throw new InvalidOperationException("Synchronous partisan creation cannot answer contact decisions"); }
        }
        return operation.Result.Value;
    }

    /// <summary>Retains each U01 creation through actual W03/V04 contact before fortification,
    /// veteran sampling, visibility, later placement attempts and the final partisan notice.</summary>
    public Operation Begin(CitySlot city, int newOwner, int oldOwner, MapPoint center, int mode, string originalName, string call) =>
        new(this, city, newOwner, oldOwner, center, mode, originalName, call, true);

    public sealed class Operation : IDisposable
    {
        private readonly CapturePartisans parent;
        private readonly IEnumerator<DiplomaticContactDecision?> steps;
        private readonly List<Exception> failures = [];
        private ResearchReply? reply;
        private bool faulted, disposed;
        internal bool Retained { get; }
        public int? Result { get; internal set; }
        public DiplomaticContactDecision? Pending { get; private set; }
        public IReadOnlyList<Exception> PresentationFailures => failures.AsReadOnly();
        internal Operation(CapturePartisans parent, CitySlot city, int newOwner, int oldOwner,
            MapPoint center, int mode, string originalName, string call, bool retained)
        {
            ArgumentException.ThrowIfNullOrEmpty(call);
            this.parent = parent;
            Retained = retained;
            steps = parent.Execute(this, city, newOwner, oldOwner, center, mode, originalName, call).GetEnumerator();
        }
        internal ResearchReply? TakeReply() { var answer = reply; reply = null; return answer; }
        internal void Report(IEnumerable<Exception> errors) => failures.AddRange(errors);
        internal void Present(Action action)
        {
            if (!Retained)
            { action(); return; }
            try
            { action(); }
            catch (Exception error) { failures.Add(error); }
        }
        public DiplomaticContactDecision? Resume(ResearchReply? answer = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("Failed partisan creation cannot resume"); }
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

    private IEnumerable<DiplomaticContactDecision?> Execute(Operation operation, CitySlot city,
        int newOwner, int oldOwner, MapPoint center, int mode, string originalName, string call)
    {
        MapKnowledge.RequireLocal(world);
        bool Knows(int owner, int advance) => CivilizationKnowledge.KnowsAdvance(world.Civilizations, owner, advance);
        int government = Math.Abs((byte)world.Civilizations[oldOwner].Government - (byte)world.Civilizations[newOwner].Government);
        int rank = Math.Abs(world.Globals.PowerRank[oldOwner] - world.Globals.PowerRank[newOwner]);
        int count = unchecked((world.Entities.Cities.At(city).Size + 5) / 8 * (government + rank + 1)) / 2;
        if (mode != 0)
        { count /= 2; }
        if (!Knows(oldOwner, AdvanceIds.Conscription))
        { --count; }
        if (!Knows(oldOwner, AdvanceIds.GuerrillaWarfare))
        {
            if (!Knows(oldOwner, AdvanceIds.Communism))
            { count = 0; }
            if (!Knows(oldOwner, AdvanceIds.Gunpowder))
            { count = 0; }
        }
        else if (!Knows(newOwner, AdvanceIds.GuerrillaWarfare))
        { count = unchecked(count * 2); }
        else
        { ++count; }
        int threshold = Math.Max(0, world.Civilizations[newOwner].Reputation - 4
            + (world.Civilizations[oldOwner].Treaties[newOwner].Vendetta ? 2 : 0));
        int made = 0;
        UnitSlot? lastCreation = null;
        for (int attempt = 0; attempt < count; ++attempt)
        {
            int bestScore = 0;
            MapPoint best = default;
            for (int i = 0; i < 20; ++i)
            {
                var point = MapNeighborhoods.Offset(world.Map.Geometry, center, MapNeighborhoods.City[i], world.Globals.Rules.FlatWorld);
                if (!world.Map.Geometry.InBounds(point) || world.Map.TileAt(point).Ocean || world.Map.CityOrUnitOwner(point) is not null)
                { continue; }
                int score = 2 * unchecked((sbyte)rules.Terrain[(int)world.Map.TileAt(point).Terrain].DefenseFactor);
                if ((world.Map.TileAt(point).Features & MapFeatures.Road) != 0)
                { ++score; }
                if ((world.Map.TileAt(point).Features & MapFeatures.Railroad) != 0)
                { ++score; }
                if ((world.Map.TileAt(point).Features & (MapFeatures.Fortification | MapFeatures.CitySite)) == MapFeatures.Fortification)
                { score *= 2; }
                if (score > bestScore)
                { bestScore = score; best = point; }
            }
            if (bestScore == 0)
            { continue; }
            if (operation.Retained)
            {
                using var child = creation.Begin(new(UnitTypeIds.Partisans), new((byte)oldOwner), best, call + "/attempt/" + attempt);
                while (!child.Completed)
                {
                    var pending = child.Resume(operation.TakeReply());
                    if (!child.Completed)
                    { yield return pending; }
                }
                operation.Report(child.PresentationFailures);
                lastCreation = child.Result;
            }
            else
            { lastCreation = creation.Create(new(UnitTypeIds.Partisans), new((byte)oldOwner), best); }
            if (lastCreation is not { } slot)
            { continue; }
            if (made == 0 && (world.Multiplayer.LocalHuman == newOwner || world.Multiplayer.LocalHuman == oldOwner))
            { operation.Present(effects.InfantrySound); }
            ++made;
            ref var unit = ref world.Entities.Units.At(slot);
            unit.Orders = UnitOrder.Fortified;
            if (threshold > 0 && random.PrimaryModulo(new ChoiceKey("C09.partisans.veteran", call, attempt), threshold + 1) != 0)
            { unit.Flags.Veteran = true; }
            MapKnowledge.MarkUnitSeen(world, slot, newOwner);
            operation.Present(() => effects.Redraw(best));
        }
        if (made > 0 && (world.Globals.HumanPlayers.Contains(newOwner) || world.Globals.HumanPlayers.Contains(oldOwner))
            && !world.Multiplayer.Saved.AutoplayActive && (world.Multiplayer.LocalHuman == newOwner || world.Multiplayer.LocalHuman == oldOwner))
        { effects.Notice(new(newOwner, oldOwner, world.Names[oldOwner].Adjective, originalName, lastCreation)); }
        operation.Result = made;
    }
}
