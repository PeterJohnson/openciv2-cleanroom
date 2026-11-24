using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

public enum HutNoticeKind { Tribe, Mercenaries, Metals, Nothing, Barbarians, Scrolls, Nomads }
public sealed record HutNotice(HutNoticeKind Kind, int Actor, MapPoint Position, int? Amount = null, int? Advance = null);
public abstract record HutDecision
{
    public sealed record Grant(ResearchGrantDecision Decision) : HutDecision;
    public sealed record Contact(DiplomaticContactDecision Decision) : HutDecision;
}
public sealed record HutCityVisit(int Actor, CityTarget City, string Name) : HutDecision;
public interface IHutEffects
{
    void Notice(HutNotice notice);
    void RedrawNeighbors(MapPoint point);
    void Redraw(MapPoint point);
    void RefreshStatus();
}

/// <summary>W06 local hut entry with actual creation and research children. U02 owns the retained hut
/// predicate before relocation; this helper deliberately does not test or consume it again.</summary>
public sealed class HutEntry(GameState world, RuleTables rules, GameplayRandom random, UnitCreation units,
    CityCreation cities, ResearchGrants grants, IHutEffects effects,
    IReadOnlyDictionary<int, CivilizationSet>? extraAdvancePresence = null)
{
    private GameState World => world;
    private bool Known(int advance)
    {
        if (advance >= 0 && advance < RuleLimits.Advances)
        { return world.Globals.AdvanceKnownBy[advance].Count != 0; }
        if (extraAdvancePresence is not null && extraAdvancePresence.TryGetValue(advance, out var presence))
        { return presence.Count != 0; }
        throw new InvalidOperationException("Hut unit selection requires explicit discovery backing for the signed Armor prerequisite");
    }
    private int Roll(string site, string call, int bound, int index = 0) => random.PrimaryModulo(new ChoiceKey("W06.hut-" + site, call, index), bound);
    public Operation Begin(UnitSlot unit, MovementContinuation movement, string call) => new(this, unit, movement, call);
    public sealed class Operation : IDisposable
    {
        private readonly HutEntry parent;
        private readonly IEnumerator<HutDecision?> steps;
        private readonly List<Exception> presentationFailures = [];
        private bool disposed, faulted;
        public bool Completed { get; private set; }
        private ResearchReply? answer;
        public HutDecision? Pending { get; private set; }
        public IReadOnlyList<Exception> PresentationFailures => presentationFailures.AsReadOnly();
        internal Operation(HutEntry parent, UnitSlot unit, MovementContinuation movement, string call)
        {
            ArgumentException.ThrowIfNullOrEmpty(call);
            this.parent = parent;
            steps = parent.Execute(this, unit, movement, call).GetEnumerator();
        }
        internal void Present(Action action) { try { action(); } catch (Exception error) { presentationFailures.Add(error); } }
        internal ResearchReply? TakeReply() { var reply = answer; answer = null; return reply; }
        internal void Report(IEnumerable<Exception> failures) => presentationFailures.AddRange(failures);
        public HutDecision? Resume(int? reply = null) => ResumeDecision(reply is { } selected ? new(selected) : null);
        public HutDecision? ResumeDecision(ResearchReply? reply = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("A failed hut operation cannot resume"); }
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
    private int UnsupportedSettlers(int? owner)
    {
        int count = 0;
        for (int i = 0; i < world.Entities.Units.End; ++i)
        {
            ref var unit = ref world.Entities.Units.Storage[i];
            if (unit.Token.Occupied && (owner is null || unit.Owner.Value == owner) && unit.Home is null
                && rules.Units[unit.Type.Value].Role == RuleRole.Settle)
            { ++count; }
        }
        return count;
    }
    private IEnumerable<HutDecision?> Execute(Operation operation, UnitSlot slot, MovementContinuation movement, string call)
    {
        int actor = unchecked((sbyte)world.Entities.Units.At(slot).Owner.Value);
        MapPoint point = world.Entities.Units.At(slot).Position;
        if (actor == 0 || rules.Units[world.Entities.Units.At(slot).Type.Value].Domain != RuleDomain.Land)
        { yield break; }
        int near = CityQueries.Nearest(world, point).Distance;
        int outcome = Roll("outcome", call, 5);
        if ((!world.Globals.HumanPlayers.Contains(actor) || Roll("settlement-override", call, 3) == 0)
            && world.Civilizations[actor].CityCount != 0)
        {
            int region = world.Map.TileAt(point).Region;
            if (world.Civilizations[actor].ContinentCities[region] == 0
                && (!world.Civilizations[actor].ContinentFlags[region].HasSettlementRestrictions || near > 12)
                && world.Civilizations[actor].ContinentCities[region] == 0)
            { outcome = 0; }
        }
        int redirect = 0;
        while (true)
        {
            switch (outcome)
            {
                case 0:
                    if (near < 4 || world.Civilizations[actor].CityCount == 0 && world.Globals.Turn < 50 && UnsupportedSettlers(null) > 1)
                    { outcome = 1; break; }
                    if (world.Map.ClaimantOrFertility(point) <= 11)
                    { outcome = 5; break; }
                    Notice(HutNoticeKind.Tribe);
                    CitySlot? city;
                    using (var creation = cities.Begin(new((byte)actor), point, call + "/city"))
                    {
                        while (!creation.Completed)
                        {
                            var contact = creation.Resume(operation.TakeReply());
                            if (!creation.Completed)
                            { yield return contact is null ? null : new HutDecision.Contact(contact); }
                        }
                        operation.Report(creation.PresentationFailures);
                        city = creation.Result;
                    }
                    if (world.Globals.Year >= 1000)
                    {
                        int size = Roll("city-size", call, 4) + 1;
                        if (city is not { } created)
                        { throw new InvalidOperationException("Hut city creation failed before late-city awards; invalid city writes are excluded"); }
                        world.Entities.Cities.At(created).Size = (sbyte)size;
                        BuildingQueries.SetBuilding(world, created, BuildingIds.Temple, true);
                        foreach (var (building, name, bound) in new[] { (BuildingIds.Marketplace, "marketplace", 2), (BuildingIds.Granary, "granary", 3), (BuildingIds.Library, "library", 4) })
                        {
                            if (Roll("city-" + name, call, bound) == 0)
                            {
                                BuildingQueries.SetBuilding(world, created, building, true);
                            }
                        }
                    }
                    if (movement.LocalHuman)
                    {
                        operation.Present(() => effects.RedrawNeighbors(point));
                        if (city is { } visit)
                        { yield return new HutCityVisit(actor, new(visit, world.Entities.Cities.At(visit).Token), world.Entities.Cities.At(visit).Name); }
                    }
                    yield break;
                case 1:
                    Notice(HutNoticeKind.Mercenaries);
                    int mounted = UnitTypeIds.Knights;
                    if (!Known(AdvanceIds.Chivalry))
                    {
                        mounted = Roll("mounted", call, 3) == 0 ? UnitTypeIds.Chariot : UnitTypeIds.Horsemen;
                        if (Known(AdvanceIds.Polytheism) && Roll("elephant", call, 2) != 0)
                        { mounted = UnitTypeIds.Elephant; }
                    }
                    if (Known(AdvanceIds.Monotheism))
                    {
                        mounted = UnitTypeIds.Crusaders;
                    }

                    if (Known(AdvanceIds.Leadership))
                    {
                        mounted = UnitTypeIds.Dragoons;
                    }

                    if (Known(AdvanceIds.Conscription))
                    {
                        mounted = UnitTypeIds.Riflemen;
                    }

                    int foot = UnitTypeIds.Fanatics;
                    if (!Known(AdvanceIds.GuerrillaWarfare))
                    {
                        foot = UnitTypeIds.Musketeers;
                    }

                    if (!Known(AdvanceIds.Gunpowder))
                    {
                        foot = UnitTypeIds.Legion;
                    }

                    if (!Known(AdvanceIds.IronWorking))
                    {
                        foot = UnitTypeIds.Archers;
                    }

                    int type = Roll("mercenary-type", call, 2) == 0 ? mounted : foot;
                    UnitSlot? mercenary;
                    using (var creation = units.Begin(new((byte)type), new((byte)actor), point, call + "/mercenary"))
                    {
                        while (!creation.Completed)
                        {
                            var contact = creation.Resume(operation.TakeReply());
                            if (!creation.Completed)
                            { yield return contact is null ? null : new HutDecision.Contact(contact); }
                        }
                        operation.Report(creation.PresentationFailures);
                        mercenary = creation.Result;
                    }
                    if (!movement.LocalHuman && world.Multiplayer.LocalHuman != actor)
                    {
                        if (mercenary is { } unsupported)
                        {
                            world.Entities.Units.At(unsupported).Home = null;
                        }
                    }
                    else
                    { operation.Present(() => effects.RedrawNeighbors(point)); }
                    yield break;
                case 2:
                    int amount = 50;
                    if (Roll("metals-size", call, 3) == 0)
                    { amount = Roll("metals-value", call, 10) - (byte)world.Globals.Difficulty + 2 < 5 ? 25 : 100; }
                    if (world.Globals.Year > 1000)
                    {
                        amount *= 2;
                    }

                    world.Civilizations[actor].Gold = unchecked(world.Civilizations[actor].Gold + amount);
                    Notice(HutNoticeKind.Metals, amount);
                    if (world.Multiplayer.LocalHuman == actor)
                    {
                        operation.Present(effects.RefreshStatus);
                    }

                    yield break;
                case 3:
                    if (near < 4 || world.Civilizations[actor].CityCount == 0 && world.Globals.Turn < 50)
                    { outcome = 1; break; }
                    if (!world.Globals.HumanPlayers.Contains(actor) && world.Globals.PowerRank[actor] < world.Globals.PowerRank[world.Globals.WorstHumanRank]
                        && Roll("barbarian-redirect", call, 8) < (byte)world.Globals.Difficulty)
                    { outcome = 0; break; }
                    if (world.Globals.BarbarianActivity < 3 && (world.Globals.Turn < 30 || world.Globals.Turn < 50 && Roll("early-barbarians", call, 2) == 0))
                    {
                        near = CityQueries.Nearest(world, point, actor).Distance;
                        if (world.Civilizations[actor].ContinentCities[world.Map.TileAt(point).Region] != 0 && near < 24)
                        { Notice(HutNoticeKind.Nothing); yield break; }
                    }
                    bool single = world.Globals.Turn < 50 || world.Globals.Turn < 75 && Roll("single-barbarian", call, 2) == 0;
                    Notice(HutNoticeKind.Barbarians);
                    for (int step = 0; step < 8;)
                    {
                        int direction = (world.Globals.Turn + step) % 8;
                        if (direction < 0)
                        { throw new InvalidOperationException("Hut barbarian direction requires valid neighborhood backing"); }
                        var target = MapNeighborhoods.Offset(world.Map.Geometry, point, MapNeighborhoods.Directions[direction], world.Globals.Rules.FlatWorld);
                        if (world.Map.Geometry.InBounds(target) && world.Map.UnitOwner(target) is null && world.Map.CityOwner(target) is null && !world.Map.TileAt(target).Ocean)
                        {
                            int rough = UnitTypeIds.Legion, flat = UnitTypeIds.Horsemen;
                            if (Known(AdvanceIds.Metallurgy))
                            { rough = UnitTypeIds.Musketeers; flat = UnitTypeIds.Knights; }
                            if (Known(unchecked((sbyte)(int)rules.Units[UnitTypeIds.Armor].Prerequisite)))
                            { rough = UnitTypeIds.Riflemen; flat = UnitTypeIds.Cavalry; }
                            if (Known(AdvanceIds.GuerrillaWarfare))
                            { rough = UnitTypeIds.Fanatics; flat = UnitTypeIds.Partisans; }
                            int selected = unchecked((sbyte)rules.Terrain[(int)world.Map.TileAt(target).Terrain].MovementCost) < 3 ? flat : rough;
                            UnitSlot? barbarian;
                            using (var creation = units.Begin(new((byte)selected), new(0), target, call + "/barbarian/" + step))
                            {
                                while (!creation.Completed)
                                {
                                    var contact = creation.Resume(operation.TakeReply());
                                    if (!creation.Completed)
                                    { yield return contact is null ? null : new HutDecision.Contact(contact); }
                                }
                                operation.Report(creation.PresentationFailures);
                                barbarian = creation.Result;
                            }
                            if (barbarian is { } spawned)
                            {
                                world.Entities.Units.At(spawned).Visibility = world.Entities.Units.At(spawned).Visibility.Union(world.Map.TileAt(point).RevealedTo);
                                operation.Present(() => effects.Redraw(target));
                            }
                            if (single)
                            { yield break; }
                        }
                        step += Arithmetic.ClampAB(4 - world.Civilizations[actor].CityCount, 1, 4);
                    }
                    yield break;
                case 4:
                    if (world.Globals.Turn == 0 || CivilizationKnowledge.KnowsAdvance(world.Civilizations, actor, AdvanceIds.Invention))
                    { outcome = 2; break; }
                    int advance = Roll("scroll-start", call, 100, redirect), attempts = 0;
                    for (; attempts < 999; ++attempts)
                    {
                        if (Research.Available(world, rules, actor, advance))
                        { break; }
                        advance = (advance + 1) % 100;
                    }
                    if (attempts == 999)
                    { outcome = 0; break; }
                    Notice(HutNoticeKind.Scrolls, advance: advance);
                    using (var grant = grants.Begin(actor, advance, actor, false, call + "/scroll"))
                    {
                        while (!grant.Completed)
                        {
                            if (grant.Resume(operation.TakeReply()?.Selected) is { } decision)
                            { yield return new HutDecision.Grant(decision); }
                            else if (!grant.Completed)
                            { yield return null; }
                        }
                        operation.Report(grant.PresentationFailures);
                    }
                    yield break;
                case 5:
                    if (Known(AdvanceIds.Explosives))
                    { outcome = 2; break; }
                    if (UnsupportedSettlers(actor) > (world.Civilizations[actor].CityCount >> 3))
                    { outcome = 4; break; }
                    Notice(HutNoticeKind.Nomads);
                    UnitSlot? settler;
                    using (var creation = units.Begin(new(UnitTypeIds.Settlers), new((byte)actor), point, call + "/nomad"))
                    {
                        while (!creation.Completed)
                        {
                            var contact = creation.Resume(operation.TakeReply());
                            if (!creation.Completed)
                            { yield return contact is null ? null : new HutDecision.Contact(contact); }
                        }
                        operation.Report(creation.PresentationFailures);
                        settler = creation.Result;
                    }
                    if (settler is { } nomad)
                    {
                        world.Entities.Units.At(nomad).Home = null;
                    }

                    if (movement.LocalHuman)
                    {
                        operation.Present(() => effects.RedrawNeighbors(point));
                    }

                    yield break;
                default:
                    throw new InvalidOperationException("Unknown hut outcome");
            }
            // A repeating 0 -> 5 -> 4 cycle is not replaced with a reward or a total-attempt cap.
            ++redirect;
            yield return null;
        }
        void Notice(HutNoticeKind kind, int? amount = null, int? advance = null)
        {
            if (movement.LocalHuman)
            {
                effects.Notice(new(kind, actor, point, amount, advance));
            }
        }
    }
}
