using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

/// <summary>W06 semantic description identities from the configured label catalog.</summary>
public enum BarbarianUprisingKind { Uprising = 98, LargeUprising, Gunpowder, Industrial, Modern, Fanatic }
public sealed record BarbarianUprisingNotice(int Owner, BarbarianUprisingKind Kind, string CityName, UnitTarget Unit, MapPoint Position);
public interface IBarbarianSpawningEffects
{
    void Notice(BarbarianUprisingNotice notice);
    void Redraw(MapPoint point);
}

/// <summary>W06 periodic sea raids and uprisings, using actual U01 children. F04 owns the
/// incoming-turn invocation. Local desktop modes only; network notification routing is deferred.</summary>
public sealed partial class BarbarianSpawning(GameState world, RuleTables rules, GameplayRandom random,
    UnitCreation units, StackOperations stacks, UnitLoss loss, CivilizationElimination lifecycle,
    EspionageIncidents incidents, IBarbarianSpawningEffects effects,
    IReadOnlyDictionary<int, CivilizationSet>? extraAdvancePresence = null)
{
    private GameState World => world;
    private bool Expanded => world.Globals.Rules.ExpandedBarbarianTypes;
    private int ArmorPrerequisite => unchecked((sbyte)(int)rules.Units[UnitTypeIds.Armor].Prerequisite);
    private bool Known(int advance)
    {
        if (advance >= 0 && advance < RuleLimits.Advances)
        { return world.Globals.AdvanceKnownBy[advance].Count != 0; }
        if (extraAdvancePresence is not null && extraAdvancePresence.TryGetValue(advance, out var presence))
        { return presence.Count != 0; }
        throw new InvalidOperationException("Barbarian spawning requires explicit discovery backing for the signed Armor prerequisite");
    }
    private bool HumanKnows(int advance) => world.Globals.AdvanceKnownBy[advance].Intersects(world.Globals.HumanPlayers);
    private int Roll(string site, string call, int bound, int index = 0) => random.PrimaryModulo(new ChoiceKey("W06.spawn-" + site, call, index), bound);
    public Operation Begin(string call) => new(this, call);

    public sealed class Operation : IDisposable
    {
        private readonly BarbarianSpawning parent;
        private readonly IEnumerator<LifecycleDecision?> steps;
        private readonly List<Exception> failures = [];
        private bool disposed, faulted;
        private ResearchReply? answer;
        public bool Completed { get; private set; }
        public LifecycleDecision? Pending { get; private set; }
        public IReadOnlyList<Exception> PresentationFailures => failures.AsReadOnly();
        internal Operation(BarbarianSpawning parent, string call)
        {
            ArgumentException.ThrowIfNullOrEmpty(call);
            this.parent = parent;
            steps = parent.Execute(this, call).GetEnumerator();
        }
        internal ResearchReply? TakeReply() { var reply = answer; answer = null; return reply; }
        internal void Present(Action action) { try { action(); } catch (Exception error) { failures.Add(error); } }
        public LifecycleDecision? Resume(int? reply = null) => ResumeDecision(reply is { } value ? new(value) : null);
        public LifecycleDecision? ResumeDecision(ResearchReply? reply = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("A failed barbarian spawning operation cannot resume"); }
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

    private IEnumerable<LifecycleDecision?> Execute(Operation operation, string call)
    {
        int activity = world.Globals.BarbarianActivity, difficulty = (byte)world.Globals.Difficulty;
        if (activity == 0 || world.Globals.Turn < (difficulty < 5
            ? ((3 - activity) * 3 + 30) * (5 - difficulty) : (3 - activity) * 3 + 15))
        { yield break; }
        int mask = activity switch
        {
            1 => 15,
            2 or 3 => 7,
            _ => throw new InvalidOperationException("Periodic spawning requires barbarian activity 0..3")
        };
        if (((world.Globals.Turn + 1) & mask) == 0 && !Known(ArmorPrerequisite))
        {
            if (SeaPosition(call) is not { } point)
            { yield break; }
            if (CreateRaid(point, call) is { } erase)
            {
                using var child = lifecycle.BeginEraseStack(loss, erase, call + "/raid-loss", incidents);
                while (child.Result is null)
                {
                    var pending = child.ResumeDecision(operation.TakeReply());
                    if (child.Result is null)
                    { yield return pending; }
                }
            }
        }
        int selected = world.Entities.Cities.End <= 1 ? 0 : Roll("city", call, world.Entities.Cities.End);
        if (selected >= world.Entities.Cities.End || !world.Entities.Cities.Storage[selected].Token.Occupied
            || !world.Globals.HumanPlayers.Contains(world.Entities.Cities.Storage[selected].Owner.Value))
        { yield break; }
        Uprising(operation, new((ushort)selected), call);
    }

    private MapPoint? SeaPosition(string call)
    {
        // F01 permits omitting the terminal unused samples, never a 1001st tile test.
        for (int attempt = 0; attempt < 1000; ++attempt)
        {
            int x = world.Map.Geometry.Width <= 1 ? 0 : Roll("sea-x", call, world.Map.Geometry.Width, attempt);
            int y = (world.Map.Geometry.Height <= 7 ? 0 : Roll("sea-y", call, world.Map.Geometry.Height - 6, attempt)) + 3;
            var point = new MapPoint((x & ~1) + (y & 1), y);
            if (world.Map.TileAt(point).Ocean && world.Map.UnitOwner(point) is null
                && world.Map.Regions[world.Map.TileAt(point).Region].Water >= 16)
            { return point; }
        }
        return null;
    }

    /// <returns>The actual stack to erase if ship creation failed.</returns>
    private int? CreateRaid(MapPoint point, string call)
    {
        int activity = world.Globals.BarbarianActivity;
        int n = Arithmetic.ClampAB(world.Globals.Turn / (250 - 50 * activity) + 1, 1, 5) + (activity == 3 ? 2 : 1);
        int ship = UnitTypeIds.Trireme;
        if (Known(AdvanceIds.Navigation))
        { ship = UnitTypeIds.Caravel; }
        if (Known(AdvanceIds.Magnetism))
        { ship = UnitTypeIds.Frigate; }
        int escort = Expanded ? UnitTypeIds.Archers : UnitTypeIds.Warriors;
        if (HumanKnows(AdvanceIds.IronWorking))
        { escort = UnitTypeIds.Legion; }
        if (Known(AdvanceIds.Metallurgy) || activity == 3 && Known(AdvanceIds.Chivalry))
        { escort = (point.X & 1) != 0 && Expanded ? UnitTypeIds.Crusaders : UnitTypeIds.Knights; }
        if (HumanKnows(AdvanceIds.Tactics) && Expanded)
        { escort = UnitTypeIds.Dragoons; }
        for (int remaining = n - 1; remaining >= 0; --remaining)
        {
            int type = remaining == n - 1 && remaining != 0 ? UnitTypeIds.Diplomat : escort;
            if (units.Create(new((byte)type), new(0), point) is { } unit)
            { UnitOrders.SetSleep(ref world.Entities.Units.At(unit)); }
        }
        if (units.Create(new((byte)ship), new(0), point) is not { } created)
        { int first = stacks.FirstAt(point); return first < 0 ? null : first; }
        if (Roll("ship-veteran", call, 2) == 0)
        { world.Entities.Units.At(created).Flags.Veteran = true; }
        stacks.RelocateInPlace(created.Value, true);
        return null;
    }
}
