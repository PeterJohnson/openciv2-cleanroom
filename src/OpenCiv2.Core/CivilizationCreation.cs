using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

/// <summary>Required lifecycle children. Catch-up is V03 giveAdvance(c,a,c,0,0);
/// eligibility is C06 canBuildUnit(c,NoCity,type). Creation/deletion are actual U01 operations.
/// Turn-zero-only setup may omit CatchupResearch; a reached catch-up grant requires the actual V03 service.</summary>
public sealed record CivilizationCreationServices(
    UnitCreation Units,
    UnitDeletion Deletion,
    ResearchGrants? CatchupResearch,
    Func<int, UnitType, bool> CanBuildUnit);

/// <summary>V01 §§2–5 local civilization creation. Advance bounds only the candidate search;
/// Pending identifies a retained catch-up or placement contact choice. The owning session serializes gameplay
/// and pins the world across pending calls.</summary>
public sealed class CivilizationCreation : IDisposable
{
    private readonly GameState world;
    private readonly ProcessContext process;
    private readonly int civilization;
    private readonly StartingPositions hints;
    private readonly GameplayRandom random;
    private readonly string call;
    private readonly CivilizationCreationServices services;
    private StartingSiteSearch? search;
    private short oldTribe;
    private int blocked;
    private bool started, faulted, disposed;
    private IEnumerator<LifecycleDecision?>? initialization, placement;
    private ResearchReply? reply;
    private readonly List<Exception> presentationFailures = [];
    public LifecycleDecision? Pending { get; private set; }
    public IReadOnlyList<Exception> PresentationFailures => presentationFailures.AsReadOnly();
    private ResearchReply? TakeReply() { var answer = reply; reply = null; return answer; }

    public int? Result { get; private set; }
    public StartingSite? Site { get; private set; }

    public CivilizationCreation(GameState world, ProcessContext process, int civilization, StartingPositions hints,
        GameplayRandom random, string call, CivilizationCreationServices services)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(civilization);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(civilization, EntityLimits.Civilizations);
        ArgumentException.ThrowIfNullOrEmpty(call);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(services.Units);
        ArgumentNullException.ThrowIfNull(services.Deletion);
        ArgumentNullException.ThrowIfNull(services.CanBuildUnit);
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.process = process ?? throw new ArgumentNullException(nameof(process));
        this.civilization = civilization;
        this.hints = hints;
        this.random = random ?? throw new ArgumentNullException(nameof(random));
        this.call = call;
        this.services = services;
    }

    public int? Advance(int candidateBudget, int? answer = null) => AdvanceDecision(candidateBudget, answer is { } value ? new(value) : null);

    public int? AdvanceDecision(int candidateBudget, ResearchReply? answer = null)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(candidateBudget);
        if (faulted)
        {
            throw new InvalidOperationException("A failed civilization creation cannot be resumed");
        }
        if (Result is not null)
        {
            return Result;
        }
        try
        {
            if (!started)
            {
                started = true;
                initialization = Begin().GetEnumerator();
            }
            else
            { MapKnowledge.RequireLocal(world); }
            if (Pending is not null && answer is null)
            { return null; }
            reply = Pending is null ? null : answer;
            Pending = null;
            if (initialization is not null)
            {
                if (initialization.MoveNext())
                { Pending = initialization.Current; return null; }
                initialization.Dispose();
                initialization = null;
            }
            if (placement is not null)
            { return AdvancePlacement(); }
            if (Result is not null || search!.Advance(candidateBudget) is not { } site)
            {
                return Result;
            }
            if (world.Globals.Turn == 0)
            {
                site = StartingSites.ApplyHint(world, site, hints.Positions[world.Civilizations[civilization].Tribe]);
            }
            Site = site;
            if (site.Attempts > 2000 || site.Position.Y < 3 || site.Position.Y >= world.Map.Geometry.Height - 3)
            {
                blocked = 1;
            }
            ref var civ = ref world.Civilizations[civilization];
            if (!world.Globals.HumanPlayers.Contains(civilization))
            {
                civ.ScienceRate = unchecked((byte)(process.Rules.Leaders[civ.Tribe].Civilize + 3));
                civ.TaxRate = unchecked((byte)(9 - civ.ScienceRate));
            }
            if (world.Globals.HumanPlayers.Contains(civilization)
                || blocked == 0 && (world.Globals.Turn == 0 || !world.Globals.Rules.NoRestart)
                    && world.Globals.Turn <= 350 - 35 * unchecked((byte)world.Globals.Difficulty))
            {
                placement = Place(site.Position).GetEnumerator();
                return AdvancePlacement();
            }
            world.Globals.CivsAlive = world.Globals.CivsAlive.With(civilization, false);
            civ.Tribe = oldTribe;
            if (world.Globals.CivsAlive.Except(CivilizationSet.Only(0)).Count >= 2)
            {
                return Result = 0;
            }
            if (!world.Globals.Status.Won)
            {
                world.PendingEndReason = EndReason.Conquest;
                world.Globals.Status.Won = true;
            }
            return Result = -1;
        }
        catch
        {
            faulted = true;
            initialization?.Dispose();
            placement?.Dispose();
            throw;
        }
    }

    private int? AdvancePlacement()
    {
        if (placement!.MoveNext())
        { Pending = placement.Current; return null; }
        placement.Dispose();
        placement = null;
        return Result = 1;
    }
    public void Dispose() { disposed = true; initialization?.Dispose(); placement?.Dispose(); }

    private IEnumerable<LifecycleDecision?> Begin()
    {
        if (world.Globals.HumanPlayers.Contains(civilization) && world.Globals.Turn != 0)
        {
            Result = 0;
            yield break;
        }
        MapKnowledge.RequireLocal(world);
        oldTribe = world.Civilizations[civilization].Tribe;
        int[] regionalCounts = ResetPrefix();
        if (world.Globals.Turn != 0)
        {
            bool alive = world.Globals.CivsAlive.Contains(civilization);
            world.Globals.CivsAlive = world.Globals.CivsAlive.With(civilization, false);
            for (int advance = 0; advance < RuleLimits.Advances - 1; ++advance)
            {
                if ((!world.Globals.HumanPlayers.Contains(world.Globals.FirstDiscoverer[advance])
                        || world.Globals.AdvanceKnownBy[advance].Count > 2)
                    && world.Globals.AdvanceKnownBy[advance].Intersects(world.Globals.HumanPlayers))
                {
                    int difficulty = unchecked((byte)world.Globals.Difficulty);
                    if (difficulty != 0 && random.PrimaryModulo(new ChoiceKey("V01.creation.catch-up", call, advance), difficulty + 1) != 0)
                    {
                        var research = services.CatchupResearch
                            ?? throw new InvalidOperationException("Nonzero-turn catch-up requires the research grant service");
                        using var grant = research.Begin(civilization, advance, civilization, false, call + "/catch-up/" + advance);
                        while (!grant.Completed)
                        {
                            if (grant.Resume(TakeReply()?.Selected) is { } decision)
                            { yield return decision; }
                            else if (!grant.Completed)
                            { yield return null; }
                        }
                        presentationFailures.AddRange(grant.PresentationFailures);
                    }
                }
            }
            if (alive)
            {
                world.Globals.CivsAlive = world.Globals.CivsAlive.With(civilization);
            }
        }
        world.Civilizations[civilization].UnitsActive = world.Civilizations[civilization].UnitsInProduction = world.Civilizations[civilization].UnitsLost = default;
        if (civilization == 0 || !world.Globals.HumanPlayers.Contains(civilization) && !SelectLeader()
            || !world.Globals.CivsAlive.Contains(civilization))
        {
            Result = 1;
            yield break;
        }
        search = new(world, civilization, regionalCounts, random, call + "/site");
    }

    private int[] ResetPrefix()
    {
        ref var civ = ref world.Civilizations[civilization];
        civ.Gold = 0;
        civ.ResearchProgress = 0;
        civ.NumAdvances = civ.ResearchCostAdvances = 0;
        civ.LastCityTurn = world.Globals.Turn >= 11 ? world.Globals.Turn : (short)10;
        civ.Government = Government.Despotism;
        civ.TaxRate = civ.ScienceRate = 4;
        civ.SubversionCount = civ.BestShieldSurplus = 0;
        civ.Reputation = 0;
        civ.DiploRequestCount = 0;
        civ.ThroneRoom = default;
        civ.ThroneRoom[0] = civ.ThroneRoom[1] = civ.ThroneRoom[3] = civ.ThroneRoom[6] = 1;
        civ.ThroneRoomUpgrades = 0;
        for (int other = 0; other < EntityLimits.Civilizations; ++other)
        {
            civ.ReputationCredit[other] = world.Civilizations[other].ReputationCredit[civilization] = 0;
            civ.AggressionCounter[other] = world.Civilizations[other].AggressionCounter[civilization] = 0;
        }
        civ.Attitude[0] = 100;
        for (int other = 1; other < EntityLimits.Civilizations; ++other)
        {
            if (civilization == 0)
            {
                civ.Attitude[other] = 100;
                continue;
            }
            int attitude = random.PrimaryModulo(new ChoiceKey("V01.creation.attitude", call, other), 80) + 10;
            if (world.Globals.HumanPlayers.Contains(other))
            {
                attitude = Math.Clamp(attitude + 5 * unchecked((byte)world.Globals.Difficulty), 10, 75);
            }
            world.Civilizations[other].Attitude[civilization] = unchecked((byte)attitude);
            civ.Attitude[other] = world.Civilizations[other].Attitude[civilization];
        }
        var rules = process.Rules.Definitions;
        new SharedGoals(world, rules, new MovementGoalQueries(world, rules)).Refresh(civilization);
        SharedGoals.ResetStanding(ref civ);
        int[] regionalCounts = new int[MapLimits.Regions];
        for (int region = 0; region < MapLimits.Regions; ++region)
        {
            civ.ContinentPolicy[region] = 5; // A01 Expansion policy.
            for (int other = 1; other < EntityLimits.Civilizations; ++other)
            {
                regionalCounts[region] += world.Civilizations[other].ContinentCities[region];
            }
        }
        civ.CityCount = civ.MilitaryCount = civ.TotalAttack = civ.TotalCitySize = civ.ShipCount = civ.PopulationMilestone = 0;
        world.UnitTotals[civilization] = 0;
        civ.MilitaryStrength = 0;
        civ.WondersByGroup = default;
        for (int other = 0; other < EntityLimits.Civilizations; ++other)
        {
            world.Civilizations[other].LastContactTurn[civilization] = -1;
        }
        for (int advance = 0; advance < RuleLimits.Advances; ++advance)
        {
            civ.AdvanceSource[advance] = byte.MaxValue;
            world.Globals.AdvanceKnownBy[advance] = world.Globals.AdvanceKnownBy[advance].With(civilization, false);
        }
        for (int other = 0; other < EntityLimits.Civilizations; ++other)
        {
            civ.Treaties[other] = world.Civilizations[other].Treaties[civilization] = default;
        }
        civ.Advances = default;
        return regionalCounts;
    }

    private bool SelectLeader()
    {
        var leaders = process.Rules.Leaders;
        var candidates = new List<int>();
        for (int pass = 0; pass < 2 && candidates.Count == 0; ++pass)
        {
            for (int i = 0; i < leaders.Length; ++i)
            {
                var leader = leaders[i];
                if (leader.Color == civilization && !world.Globals.HumanPlayers.Contains(leader.Color)
                    && (pass != 0 || leader.TimesUsed == 0))
                {
                    candidates.Add(i);
                }
            }
        }
        if (candidates.Count == 0)
        {
            world.Globals.CivsAlive = world.Globals.CivsAlive.With(civilization, false);
            return false;
        }
        int selected = candidates[candidates.Count == 1 ? 0 : random.PrimaryModulo(new ChoiceKey("V01.creation.leader", call), candidates.Count)];
        if (world.Globals.Turn == 0 && world.PendingLeaderOverrides[civilization] >= 0)
        {
            selected = world.PendingLeaderOverrides[civilization];
            world.PendingLeaderOverrides[civilization] = -1;
        }
        var chosen = leaders[selected];
        blocked = chosen.TimesUsed == 0 ? 0 : random.PrimaryModulo(new ChoiceKey("V01.creation.reuse", call), chosen.TimesUsed + 1);
        ref var civ = ref world.Civilizations[civilization];
        civ.Tribe = (short)selected;
        chosen.TimesUsed = unchecked((byte)(chosen.TimesUsed + 1));
        chosen.CityNameIndex = 0;
        if (random.PrimaryModulo(new ChoiceKey("V01.creation.gender", call), 3) == 0)
        {
            chosen.Female = !chosen.Female;
        }
        chosen.LeaderName = chosen.Names[chosen.Female ? 1 : 0];
        civ.LeaderPortrait = unchecked((byte)(selected + (chosen.Female ? 21 : 0)));
        civ.Flags.FemaleLeader = chosen.Female;
        return true;
    }

    private IEnumerable<LifecycleDecision?> Place(MapPoint point)
    {
        world.Civilizations[civilization].NumAdvances = 1;
        for (int slot = world.Entities.Units.End - 1; slot >= 0; --slot)
        {
            ref readonly var unit = ref world.Entities.Units.Storage[slot];
            if (unit.Token.Occupied && unit.Owner.Value == 0
                && world.Map.Geometry.WeightedDistance(point, unit.Position, world.Globals.Rules.FlatWorld) < 9)
            {
                services.Deletion.Erase(slot, true);
            }
        }
        if (civilization == world.Multiplayer.LocalHuman)
        {
            world.Views[0] = world.Views[0] with { Center = point };
        }
        foreach (var decision in Create(0, point, "first"))
        { yield return decision; }
        world.Civilizations[civilization].CapitalX = unchecked((short)point.X);
        if (world.Globals.Turn > 20 && blocked == 0
            && !world.Map.TileAt(point).RevealedTo.Intersects(world.Globals.HumanPlayers))
        {
            foreach (var decision in Create(0, point, "second"))
            { yield return decision; }
            if (world.Globals.Turn > 40 && services.CanBuildUnit(civilization, new(3)))
            {
                foreach (var decision in Create(3, point, "defender"))
                { yield return decision; }
            }
        }
        for (int i = 0; i <= 20; ++i)
        {
            var tile = MapNeighborhoods.Offset(world.Map.Geometry, point, MapNeighborhoods.Radius[i], world.Globals.Rules.FlatWorld);
            if (world.Map.Geometry.InBounds(tile))
            {
                MapKnowledge.Explore(world, civilization, tile);
            }
        }
    }
    private IEnumerable<LifecycleDecision?> Create(int type, MapPoint point, string name)
    {
        using var child = services.Units.Begin(new((byte)type), new((byte)civilization), point, call + "/placement/" + name);
        while (!child.Completed)
        {
            var decision = child.Resume(TakeReply());
            if (!child.Completed)
            { yield return decision is null ? null : new LifecycleContact(decision); }
        }
        presentationFailures.AddRange(child.PresentationFailures);
    }

}
