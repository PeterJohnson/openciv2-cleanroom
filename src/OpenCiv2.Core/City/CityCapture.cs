using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.City;

public sealed record PalaceEscapeChoice(int Owner, CitySlot Destination, string CityName, string Adjective, int Cost);
public enum CaptureNoticeKind { Incited, PalaceEscape, AbandonedWonder, StillOneWonder, StillWonders, CapturedWonder, LostWonder, Capture, WinnerAlly, LoserAlly, General }
/// <summary>Semantic local presentation. City can be absent after population loss; it must not be dereferenced then.
/// NumericArgument retains the caller's text number on ally/general notices.</summary>
public sealed record CaptureNotice(CaptureNoticeKind Kind, CitySlot? City, int NewOwner, int OldOwner, int Viewer,
    string CityName, string WinnerName, string LoserName, int NumericArgument, bool Restored,
    int? Improvement = null, string? ImprovementName = null, bool ScopedWonder = false);
public interface ICityCaptureEffects
{
    void Notice(CaptureNotice notice);
    void SpaceshipLost(SpaceshipLossNotice notice);
    void EnsureVisible(MapPoint point, int owner);
    void Redraw(MapPoint point);
    void RedrawStatus();
    void BarracksSound();
}
public abstract record CityCaptureDecision
{
    public sealed record Palace(PalaceEscapeChoice Choice) : CityCaptureDecision;
    public sealed record Lifecycle(LifecycleDecision Decision) : CityCaptureDecision;
    public sealed record Technology(CapturedTechnologyChoice Choice) : CityCaptureDecision;
    public sealed record Grant(ResearchGrantDecision Decision) : CityCaptureDecision;
    public sealed record Window(CaptureCityVisit Request) : CityCaptureDecision;
}
public sealed record CityCaptureServices(DiplomaticWar War, CivilWar CivilWar, CapturePopulationLoss Population,
    CaptureProduction Production, CapturePartisans Partisans, CapturedTechnology Technology,
    CivilizationElimination Lifecycle, EspionageIncidents Incidents, ProductionOrders Orders, WonderEffects Wonders,
    SharedGoals Goals);

/// <summary>C09 ordered local capture parent. Actual children own gameplay; the required CityTaken dispatcher
/// owns S02 matching/actions when events are loaded. Admission belongs to movement/mission callers.</summary>
public sealed partial class CityCapture(GameState world, RuleTables rules, StackOperations stacks, GameplayRandom random,
    CityCaptureServices services, Action<CitySlot, int, int> cityTaken, ICityCaptureEffects effects)
{
    private readonly ScenarioTriggers? retainedEvents;
    public CityCapture(GameState world, RuleTables rules, StackOperations stacks, GameplayRandom random,
        CityCaptureServices services, ScenarioTriggers events, ICityCaptureEffects effects)
        : this(world, rules, stacks, random, services,
            (_, _, _) => throw new InvalidOperationException("Retained scenario events require their continuation"), effects)
    { retainedEvents = events; }
    private GameState World => world;
    private bool Human(int owner) => world.Globals.HumanPlayers.Contains(owner);
    private bool Autoplay => world.Multiplayer.Saved.AutoplayActive;
    private int Local => world.Multiplayer.LocalHuman;
    private bool Wonder(int owner, int wonder) => BuildingQueries.HasWonder(world, rules, owner, wonder);
    private bool Facility(CitySlot city, int building) => BuildingQueries.HasBuilding(world, city, building);
    private ref State.City City(CitySlot slot) => ref world.Entities.Cities.At(slot);

    /// <param name="numericArgument">The caller's retained presentation number; ally/general announcements
    /// do not overwrite it with plunder. Palace/wonder notices can replace it along the way.</param>
    public Operation Begin(CitySlot city, int newOwner, int mode, int numericArgument, string call) =>
        new(this, city, newOwner, mode, numericArgument, call);
    public Operation BeginIncited(CitySlot city, int newOwner, int reason, int numericArgument, string call) =>
        new(this, city, newOwner, reason, numericArgument, call, true);

    public sealed class Operation : IDisposable
    {
        private readonly CityCapture parent;
        private readonly IEnumerator<CityCaptureDecision?> steps;
        private ResearchReply? reply;
        private bool faulted, disposed;
        internal int Number;
        internal string Name = string.Empty;
        internal bool Restored;
        public CitySlot? RemainingCity { get; internal set; }
        private readonly List<Exception> failures = [];
        public IReadOnlyList<Exception> PresentationFailures => failures.AsReadOnly();
        internal void Report(IEnumerable<Exception> errors) => failures.AddRange(errors);
        public bool Completed { get; private set; }
        public CityCaptureDecision? Pending { get; private set; }
        internal Operation(CityCapture parent, CitySlot city, int newOwner, int mode, int number, string call, bool incited = false)
        {
            ArgumentException.ThrowIfNullOrEmpty(call);
            this.parent = parent;
            Number = number;
            RemainingCity = city;
            steps = (incited ? parent.Incite(this, city, newOwner, mode, call) : parent.Execute(this, city, newOwner, mode, call)).GetEnumerator();
        }
        internal ResearchReply? TakeReply() { var value = reply; reply = null; return value; }
        /// <summary>Null without completion means bounded nested work. A reply is consumed by one pending decision.</summary>
        public CityCaptureDecision? Resume(ResearchReply? response = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("A failed city capture cannot resume"); }
            if (Completed)
            { return null; }
            if (Pending is not null && response is null)
            { return Pending; }
            reply = Pending is null ? null : response;
            Pending = null;
            try
            {
                MapKnowledge.RequireLocal(parent.World);
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

    private IEnumerable<CityCaptureDecision?> Incite(Operation op, CitySlot city, int n, int reason, string call)
    {
        byte oldByte = City(city).Owner.Value;
        int o = unchecked((sbyte)oldByte);
        var center = City(city).Position;
        if (n < 0)
        { yield break; }
        foreach (var offset in MapNeighborhoods.City)
        {
            var point = MapNeighborhoods.Offset(world.Map.Geometry, center, offset, world.Globals.Rules.FlatWorld);
            if (world.Map.Geometry.InBounds(point))
            { MapKnowledge.Explore(world, n, point); MapKnowledge.Remember(world, n, point); }
        }
        if (!Autoplay && (Human(n) || Human(oldByte) || world.Civilizations[Local].Treaties[n].Embassy
            || Wonder(Local, WonderIds.UnitedNations) || Wonder(Local, WonderIds.MarcoPolo)
            || world.Civilizations[Local].Treaties[o].Embassy || Wonder(Local, WonderIds.UnitedNations)
            || Wonder(Local, WonderIds.MarcoPolo) || world.Globals.RevealMap != 0))
        {
            if (world.Globals.RevealMap != 0 || City(city).KnownTo.Contains(Local) || City(city).Owner.Value == Local)
            { effects.EnsureVisible(City(city).Position, unchecked((sbyte)City(city).Owner.Value)); }
            effects.Notice(Notice(op, CaptureNoticeKind.Incited, city, n, o, Local) with
            { CityName = City(city).Name, WinnerName = world.Names[n].Adjective, LoserName = world.Names[o].Tribe });
        }
        for (int i = world.Entities.Units.End - 1; i >= 0; --i)
        {
            ref var unit = ref world.Entities.Units.Storage[i];
            if (!unit.Token.Occupied || unchecked((sbyte)unit.Owner.Value) != o)
            { continue; }
            int distance = world.Map.Geometry.WeightedDistance(unit.Position, center, world.Globals.Rules.FlatWorld);
            if (distance >= 2 || distance != 0 && world.Map.CityOwner(unit.Position) is not null)
            { continue; }
            unchecked
            { --world.Civilizations[o].UnitsActive[unit.Type.Value]; ++world.Civilizations[n].UnitsActive[unit.Type.Value]; }
            unit.Owner = new((byte)n);
            unit.Home = new(unchecked((byte)city.Value));
            unit.MovesUsed = 0;
            if (unit.Orders is not (UnitOrder.Fortifying or UnitOrder.Fortified))
            { unit.Orders = UnitOrder.None; }
            world.Map.TileAt(unit.Position).Owner = (byte)n;
            if (distance != 0)
            { effects.Redraw(unit.Position); }
        }
        foreach (var pending in Execute(op, city, n, reason, call))
        { yield return pending; }
    }

    private IEnumerable<CityCaptureDecision?> Execute(Operation op, CitySlot originalCity, int n, int mode, string call)
    {
        int o = unchecked((sbyte)City(originalCity).Owner.Value);
        var center = City(originalCity).Position;
        if (mode < 2)
        { services.War.Force(n, o); }
        op.Restored = unchecked((sbyte)City(originalCity).Founder.Value) == n;
        op.Name = City(originalCity).Name;
        if (Human(n) || Human(o))
        {
            if (!world.Civilizations[o].SpaceshipFlags.Launched && !world.Civilizations[n].SpaceshipFlags.Launched
                || world.Globals.Difficulty < Difficulty.Prince)
            {
                DiplomaticRelations.Set(world, n, o, new() { Incident = true });
                if (Human(n) && world.Civilizations[n].Government == Government.Democracy && world.Globals.PowerRank[n] == 7)
                { world.Civilizations[o].Treaties[n].CityCaptured = true; }
                else if (Human(n) && world.Globals.PowerRank[o] < world.Globals.PowerRank[n])
                {
                    int count = 0;
                    for (int i = 0; i < world.Entities.Cities.End; ++i)
                    {
                        ref var candidate = ref world.Entities.Cities.Storage[i];
                        if (i != originalCity.Value && candidate.Token.Occupied && unchecked((sbyte)candidate.Founder.Value) == o
                            && unchecked((sbyte)candidate.Owner.Value) == n)
                        { ++count; }
                    }
                    if (count > 1 && count % 2 == 0)
                    { world.Civilizations[o].Treaties[n].CityCaptured = true; }
                }
            }
        }
        DiplomaticRelations.Clear(world, n, o, new() { Aggressor = true });
        if (!Human(o) && City(originalCity).Size > 14 && !op.Restored)
        { world.Civilizations[o].Treaties[n].Vendetta = true; }
        CitySlot? replacement = null;
        if (!Human(o) && Facility(originalCity, BuildingIds.Palace) && world.Civilizations[o].CityCount > 4
            && world.Globals.PowerRank[Human(n) ? n : world.Globals.WorstHumanRank] < world.Globals.PowerRank[o])
        {
            using var schism = services.CivilWar.Begin(o, call + "/schism");
            while (schism.AdvanceDecision(answer: op.TakeReply()) is null)
            { yield return schism.Pending switch { ResearchGrantDecision grant => new CityCaptureDecision.Grant(grant), { } decision => new CityCaptureDecision.Lifecycle(decision), _ => null }; }
            op.Report(schism.PresentationFailures);
            if (schism.Result == 0)
            {
                City(originalCity).Owner = new(byte.MaxValue);
                replacement = CityCaptureQueries.CentralCity(world, o);
                City(originalCity).Owner = new((byte)o);
            }
        }
        int plunder = CityCaptureQueries.TreasuryShare(world, originalCity);
        if (Facility(originalCity, BuildingIds.Palace))
        {
            foreach (var pending in Palace(op, originalCity, n, o, center, replacement))
            { yield return pending; }
        }
        if (City(originalCity).Production < -BuildingIds.Capitalization && !Autoplay)
        { Abandoned(op, originalCity, n, o); }
        if ((Human(n) || Human(o)) && !Autoplay)
        { BuiltWonders(op, originalCity, n, o); }
        plunder = Arithmetic.ClampAB(plunder, 0, world.Civilizations[o].Gold);
        world.Civilizations[o].Gold = unchecked(world.Civilizations[o].Gold - plunder);
        if (n != 0)
        { world.Civilizations[n].Gold = unchecked(world.Civilizations[n].Gold + plunder); }
        if (Local == n || Local == o)
        { effects.RedrawStatus(); }
        Reset(originalCity, o, mode, op.Restored, call);
        if (mode == 0 && !op.Restored || City(originalCity).Size > 1)
        {
            using var population = services.Population.Begin(originalCity, n, o, center, call + "/population");
            while (!population.Completed)
            {
                var incident = population.ResumeDecision(op.TakeReply());
                if (incident is not null)
                { yield return new CityCaptureDecision.Lifecycle(incident); }
                else if (!population.Completed)
                { yield return null; }
            }
            op.RemainingCity = population.RemainingCity;
        }
        if (op.RemainingCity is { } city)
        {
            Takeover(city, n, o, center);
            if (world.Events.Loaded)
            {
                if (retainedEvents is null)
                { cityTaken(city, n, o); }
                else
                {
                    using var trigger = retainedEvents.BeginTaken(city, n, o, call + "/city-taken");
                    while (trigger.Result is null)
                    {
                        var pending = trigger.Resume(op.TakeReply());
                        if (trigger.Result is null)
                        { yield return pending is null ? null : new CityCaptureDecision.Lifecycle(pending); }
                    }
                    op.Report(trigger.PresentationFailures);
                }
            }
        }
        world.Map.TileAt(center).Owner = (byte)n;
        if (Local == n || Local == o)
        { effects.BarracksSound(); }
        MapKnowledge.Remember(world, n, center);
        MapKnowledge.Remember(world, o, center);
        Announce(op, n, o, center, plunder);
        if (!(world.Globals.Status.HasScenario && world.Scenario.Flags.NoConquestTechnology))
        {
            using var technology = services.Technology.Begin(n, o, call + "/technology");
            while (!technology.Finished)
            {
                var pending = technology.Resume(op.TakeReply());
                if (pending is CapturedTechnologyChoice choice)
                { yield return new CityCaptureDecision.Technology(choice); }
                else if (pending is CapturedTechnologyDecision.Grant grant)
                { yield return new CityCaptureDecision.Grant(grant.Decision); }
                else if (!technology.Finished)
                { yield return null; }
            }
        }
        if (op.RemainingCity is { } surviving)
        {
            using var production = services.Production.Begin(surviving, n, o, center, call + "/production");
            while (!production.Completed)
            {
                var pending = production.ResumeDecision(op.TakeReply());
                if (pending is CaptureProductionDecision.Lifecycle incident)
                { yield return new CityCaptureDecision.Lifecycle(incident.Decision); }
                else if (pending is CaptureProductionDecision.Window window)
                { yield return new CityCaptureDecision.Window(window.Request); }
                else if (!production.Completed)
                { yield return null; }
            }
        }
        using (var check = services.Lifecycle.Begin(o, n, call + "/check", services.Incidents))
        {
            while (check.Result is null)
            {
                if (check.ResumeDecision(op.TakeReply()) is { } incident)
                { yield return new CityCaptureDecision.Lifecycle(incident); }
                else if (check.Result is null)
                { yield return null; }
            }
            if (check.Result == 0 && op.RemainingCity is { } partisanCity && !op.Restored
                && (world.Civilizations[o].Government is Government.Communism or Government.Democracy
                    || world.Globals.FirstDiscoverer[AdvanceIds.GuerrillaWarfare] != 0
                    || CivilizationKnowledge.KnowsAdvance(world.Civilizations, o, AdvanceIds.Communism)))
            { services.Partisans.Create(partisanCity, n, o, center, mode, op.Name, call + "/partisans"); }
        }
        if (op.RemainingCity is { } finalCity)
        {
            City(finalCity).Founder = new((byte)o);
            City(finalCity).TurnsCaptured = unchecked((byte)world.Globals.Turn);
            if (BuildingQueries.ActiveCity(world, rules, WonderIds.Leonardo) == finalCity.Value)
            { services.Wonders.UpgradeForLeonardo(n); }
        }
    }

    private void Reset(CitySlot city, int oldOwner, int mode, bool restored, string call)
    {
        ref var record = ref City(city);
        record.ShieldBox = 0;
        record.Flags.Disorder = record.Flags.Celebrating = record.Flags.TechnologyStolen = record.Flags.Autobuild = record.Flags.DefenderLost = false;
        if (record.Production >= 0)
        { unchecked { --world.Civilizations[oldOwner].UnitsInProduction[record.Production]; } }
        if (!restored)
        {
            foreach (int building in new[] { BuildingIds.Palace, BuildingIds.Temple, BuildingIds.Cathedral, BuildingIds.Courthouse })
            { BuildingQueries.SetBuilding(world, city, building, false); }
            if (mode == 0)
            {
                int parity = random.PrimaryModulo(new ChoiceKey("C09.capture.building-parity", call), 2);
                for (int building = BuildingIds.First; building <= BuildingIds.Last; ++building)
                { if (building % 2 == parity) { BuildingQueries.SetBuilding(world, city, building, false); } }
            }
        }
    }
    private void Takeover(CitySlot city, int n, int o, MapPoint center)
    {
        world.Civilizations[n].LastCityTurn = world.Globals.Turn;
        services.Goals.ClearStandingNear(n, (int)RuleRole.Settle, center, 4);
        foreach (var offset in MapNeighborhoods.City)
        {
            var point = MapNeighborhoods.Offset(world.Map.Geometry, center, offset, world.Globals.Rules.FlatWorld);
            if (!world.Map.Geometry.InBounds(point))
            { continue; }
            world.Map.TileAt(point).Claimant = (byte)n;
            if (n == 0)
            { MapKnowledge.Explore(world, 0, point); }
        }
        City(city).Owner = new((byte)n);
        MapKnowledge.RevealCity(world, o, city);
    }
}
