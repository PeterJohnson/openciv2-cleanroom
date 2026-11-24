using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.Ai;

/// <summary>A01 §§4–7 local assessment: staging, ordered census, regional policy, assignment and
/// goal aging. Actual C06/A02, A08/V02, C05 and U01/V01 children retain their own effects.</summary>
public sealed partial class CivilizationAssessment(GameState world, RuleState installed, StackOperations stacks,
    ProductionOrders production, GovernmentPolicy governments, UnitLoss loss, CivilizationElimination elimination,
    GameplayRandom random)
{
    private readonly GameState world = world;
    private readonly RuleState installed = installed;
    private readonly StackOperations stacks = stacks;
    private readonly ProductionOrders production = production;
    private readonly GovernmentPolicy governments = governments;
    private readonly UnitLoss loss = loss;
    private readonly CivilizationElimination elimination = elimination;
    private readonly GameplayRandom random = random;

    public void Assess(int owner, string call)
    {
        MapKnowledge.RequireLocal(world);
        foreach (var work in new Assessment(this, owner, call).Run())
        {
            if (work.Unit is { } unit)
            { loss.DestroyAndRedraw(unit, true); }
            else
            { elimination.Check(owner, 0, call + "/eliminate"); }
        }
    }

    public Operation Begin(int owner, string call, EspionageIncidents incidents, int candidateBudget = 64) =>
        new(this, owner, call, incidents, candidateBudget);

    private sealed record Work(int? Unit = null);

    private sealed partial class Assessment(CivilizationAssessment parent, int owner, string call)
    {
        private GameState World => parent.world;
        private RuleTables Rules => parent.installed.Definitions;
        private ref Civilization Civ => ref World.Civilizations[owner];
        private bool Human => World.Globals.HumanPlayers.Contains(owner);
        private bool Flat => World.Globals.Rules.FlatWorld;
        private int Expansion => parent.installed.Leaders[Civ.Tribe].Expand;
        private readonly int[] settlers = new int[MapLimits.Regions], settleGoals = new int[MapLimits.Regions];
        private int settlersTotal, landCities;
        private SharedGoals Goals => new(World, Rules, new MovementGoalQueries(World, Rules));
        private bool Knows(int advance) => CivilizationKnowledge.KnowsAdvance(World.Civilizations, owner, advance);
        private int StackAt(MapPoint point) => parent.stacks.FirstAt(point);
        private int CountAt(MapPoint point) => parent.stacks.Count(StackAt(point));
        private int Region(MapPoint point) => World.Map.LandRegion(point) is byte region ? region : -1;
        private bool NearbyEnemy(MapPoint point) => SpatialControl.AdjacentNonalliedUnit(World, point, owner, true) is not null;
        private static int Signed(byte value) => unchecked((sbyte)value);

        public IEnumerable<Work> Run()
        {
            StageParatroopers();
            Reset();
            InitialSettlers();
            Units();
            Cities();
            Regions();
            foreach (var work in Assign())
            { yield return work; }
            for (int i = 0; i < World.Entities.Cities.End; ++i)
            {
                ref var city = ref World.Entities.Cities.Storage[i];
                if (!city.Token.Occupied || city.Owner.Value != owner)
                {
                    continue;
                }

                if (!city.Flags.FreshPort)
                {
                    city.Flags.FerryPort = false;
                }

                city.Flags.FreshPort = false;
            }
            Goals.Refresh(owner);
        }

        private void StageParatroopers()
        {
            if (!Knows((int)Rules.Units[UnitTypeIds.Paratroopers].Prerequisite) || Civ.UnitsActive[UnitTypeIds.Paratroopers] == 0 || Human)
            {
                return;
            }

            for (int target = 0; target < World.Entities.Cities.End; ++target)
            {
                ref var foreign = ref World.Entities.Cities.Storage[target];
                if (!foreign.Token.Occupied || foreign.Owner.Value == owner || !Civ.Treaties[foreign.Owner.Value].War)
                {
                    continue;
                }

                var objective = foreign.Position;
                if (World.Map.UnitOwner(objective) is not null)
                {
                    continue;
                }

                for (int destination = 0; destination < World.Entities.Cities.End; ++destination)
                {
                    ref var city = ref World.Entities.Cities.Storage[destination];
                    if (!city.Token.Occupied || city.Owner.Value != owner
                        || World.Map.Geometry.StepDistance(city.Position, objective, Flat) > Rules.Cosmic.ParadropRange)
                    {
                        continue;
                    }

                    var arrival = city.Position;
                    bool done = false;
                    for (int u = StackAt(arrival); u >= 0; u = parent.stacks.Next(u))
                    {
                        if (Rules.Units[World.Entities.Units.Storage[u].Type.Value].Abilities.Paradrop)
                        { done = true; break; }
                    }

                    if (done)
                    {
                        break;
                    }

                    for (int origin = 0; origin < World.Entities.Cities.End; ++origin)
                    {
                        ref var source = ref World.Entities.Cities.Storage[origin];
                        if (!source.Token.Occupied || source.Owner.Value != owner
                            || World.Map.Geometry.StepDistance(source.Position, objective, Flat) <= Rules.Cosmic.ParadropRange
                            || World.Map.Geometry.StepDistance(source.Position, arrival, Flat) > Rules.Cosmic.ParadropRange)
                        {
                            continue;
                        }

                        for (int u = StackAt(source.Position); u >= 0; u = parent.stacks.Next(u))
                        {
                            ref var unit = ref World.Entities.Units.Storage[u];
                            if (!Rules.Units[unit.Type.Value].Abilities.Paradrop || UnitMovement.Left(World, Rules, unit) == 0)
                            {
                                continue;
                            }

                            parent.stacks.Relocate(u, arrival, true);
                            unit.Visibility = default;
                            UnitMovement.Exhaust(World, Rules, ref unit);
                            done = true;
                            break;
                        }
                        if (done)
                        {
                            break;
                        }
                    }
                    if (done)
                    {
                        break;
                    }
                }
            }
        }

        private void Reset()
        {
            Civ.WondersByGroup = default;
            for (int w = 0; w < RuleLimits.Wonders; ++w)
            {
                if (BuildingQueries.HasWonder(World, Rules, owner, w))
                {
                    ++Civ.WondersByGroup[w / 7];
                }
            }

            Civ.ContinentMilitary = default;
            Civ.ContinentAttack = default;
            Civ.ContinentCitySize = default;
            Civ.ContinentTradeUnits = default;
            Civ.ContinentFlags = default;
            World.UnitTotals[owner] = 0;
            Civ.MilitaryCount = Civ.CityCount = Civ.ShipCount = Civ.TotalCitySize = Civ.TotalAttack = 0;
            Civ.MilitaryStrength = 0;
            Civ.UnitsActive = default;
            Civ.UnitsInProduction = default;
            for (int g = 0; g < Civilization.CurrentGoalCapacity; ++g)
            {
                if (Civ.Goals[g].Role == AiGoalRoles.FoundCity && Civ.Goals[g].Priority > 0 && Region(Civ.Goals[g].Position) is int r && r is >= 0 and < MapLimits.Regions)
                {
                    ++settleGoals[r];
                }
            }
        }

        private void InitialSettlers()
        {
            for (int i = 0; i < World.Entities.Units.End; ++i)
            {
                ref var unit = ref World.Entities.Units.Storage[i];
                if (!unit.Token.Occupied || unit.Owner.Value != owner)
                {
                    continue;
                }

                unit.Flags.GoalReached = unit.Flags.Garrison = unit.Flags.Colonist = false;
                if (Rules.Units[unit.Type.Value].Role != RuleRole.Settle)
                {
                    continue;
                }

                if (Human)
                {
                    unit.Flags.Colonist = true;
                }

                if (unit.Home is null)
                {
                    ++settlersTotal;
                }

                if (!World.Map.TileAt(unit.Position).Ocean)
                {
                    unit.Flags.Colonist = true;
                    int region = World.Map.TileAt(unit.Position).Region;
                    if (region < MapLimits.Regions)
                    {
                        ++settlers[region];
                    }

                    var nearest = CityQueries.Nearest(World, unit.Position);
                    if (nearest.Distance < 4 && nearest.Slot is { } slot && World.Entities.Cities.At(slot).Owner.Value == owner)
                    {
                        World.Entities.Cities.At(slot).Flags.SettlerNearby = true;
                    }
                }
            }
        }

        private void Units()
        {
            unchecked
            {
                for (int i = 0; i < World.Entities.Units.End; ++i)
                {
                    ref var unit = ref World.Entities.Units.Storage[i];
                    if (!unit.Token.Occupied)
                    {
                        continue;
                    }

                    if (unit.Owner.Value != owner)
                    {
                        var treaty = Civ.Treaties[unit.Owner.Value];
                        if (!World.Globals.Rules.NormalCombat || treaty.Ceasefire || treaty.Peace || !unit.Visibility.Contains(owner))
                        {
                            continue;
                        }

                        int maximum = UnitMovement.MaximumHitPoints(Rules, unit);
                        if (UnitMovement.RemainingHitPoints(World, Rules, ref unit) <= maximum / 2)
                        {
                            Goals.AddCurrent(owner, unit.Position, World.Map.TileAt(unit.Position).Ocean ? (int)RuleRole.NavalSuperiority : (int)RuleRole.Attack, 1);
                        }

                        continue;
                    }
                    int type = unit.Type.Value;
                    if (type < RuleLimits.UnitTypes)
                    {
                        ++Civ.UnitsActive[type]; // Exclude invalid destinations; the following rule read still needs backing.
                    }

                    var rule = Rules.Units[type];
                    if (Signed(rule.Attack) >= 99)
                    {
                        continue;
                    }

                    if (unit.Orders is UnitOrder.Fortifying or UnitOrder.Fortified)
                    {
                        unit.Flags.Garrison = true;
                    }

                    var point = unit.Position;
                    int region = -1;
                    if (!World.Map.Geometry.InBounds(point))
                    {
                        UnitOrders.SetSleep(ref unit);
                    }
                    else
                    {
                        region = Region(point);
                        if (!Human)
                        {
                            if (unit.Orders == UnitOrder.Sleeping && !(rule.Domain == RuleDomain.Land && World.Map.TileAt(point).Ocean))
                            {
                                unit.Orders = UnitOrder.None;
                            }

                            if ((int)unit.Orders > 15)
                            {
                                unit.Orders = UnitOrder.None;
                            }

                            if (unit.Orders == UnitOrder.None && NearbyEnemy(point))
                            {
                                unit.Orders = UnitOrder.AiIdle;
                            }
                        }
                        if (Rules.Units[unit.Type.Value].Domain == RuleDomain.Land && World.Map.TileAt(point).Ocean)
                        {
                            UnitOrders.SetSleep(ref unit);
                        }
                    }
                    ++World.UnitTotals[owner];
                    int role = (sbyte)rule.Role;
                    if (role < (int)RuleRole.Settle)
                    {
                        ++Civ.MilitaryCount;
                        if (region is >= 0 and < MapLimits.Regions)
                        {
                            ++Civ.ContinentMilitary[region];
                        }
                    }
                    if (Rules.Units[unit.Type.Value].Role == RuleRole.Trade && unit.Home is { } tradeHome)
                    {
                        World.Entities.Cities.At(tradeHome).Flags.HadCaravan = true;
                        if (region is >= 0 and < MapLimits.Regions)
                        {
                            ++Civ.ContinentTradeUnits[region];
                        }
                    }
                    if (role == (int)RuleRole.Settle && unit.Home is { } home)
                    {
                        ++settlersTotal;
                        if (region < 0)
                        {
                            continue;
                        }

                        if (region < MapLimits.Regions)
                        {
                            ++settlers[region];
                        }

                        if (Civ.ContinentCities[region] != 0)
                        {
                            int government = (byte)Civ.Government;
                            int divisor = government <= (int)Government.Despotism ? 4 : government < (int)Government.Republic ? 3 : 2;
                            int quota = (divisor / 2 - Expansion - 1 + Civ.ContinentCities[region]) / divisor;
                            if (type == UnitTypeIds.Engineers)
                            {
                                ++quota;
                            }

                            quota = Math.Min(quota, settleGoals[region]);
                            if (settlers[region] <= quota)
                            {
                                unit.Flags.Colonist = true;
                            }
                        }
                        if (World.Entities.Cities.At(home).Flags.SettlerNearby && unit.Flags.Colonist && !Human)
                        { --settlers[region]; unit.Flags.Colonist = false; }
                        if (unit.Flags.Colonist && World.Map.Geometry.WeightedDistance(point, World.Entities.Cities.At(home).Position, Flat) < 5)
                        {
                            World.Entities.Cities.At(home).Flags.SettlerNearby = true;
                        }
                    }
                    if (Rules.Units[unit.Type.Value].Domain == RuleDomain.Sea)
                    {
                        ++Civ.ShipCount;
                    }

                    Civ.TotalAttack = (short)(Civ.TotalAttack + Signed(rule.Attack));
                    Civ.MilitaryStrength = (ushort)(Civ.MilitaryStrength + Signed(rule.Attack) + Signed(rule.Defense));
                    if (region is >= 0 and < MapLimits.Regions)
                    {
                        Civ.ContinentAttack[region] = (ushort)(Civ.ContinentAttack[region] + Signed(rule.Attack));
                    }
                }
            }
        }

        private void Cities()
        {
            unchecked
            {
                Civ.ContinentCities = default;
                for (int i = 0; i < World.Entities.Cities.End; ++i)
                {
                    ref var city = ref World.Entities.Cities.Storage[i];
                    if (!city.Token.Occupied || !World.Map.Geometry.InBounds(city.Position))
                    {
                        continue;
                    }

                    var point = city.Position;
                    if (city.Owner.Value == owner)
                    {
                        ++Civ.CityCount;
                        Civ.TotalCitySize += city.Size;
                        if (city.Production >= 0 && city.Production < RuleLimits.UnitTypes)
                        {
                            ++Civ.UnitsInProduction[city.Production];
                        }

                        int region = Region(point);
                        if (region is >= 0 and < MapLimits.Regions)
                        {
                            Civ.ContinentCitySize[region] += (byte)city.Size;
                            ++Civ.ContinentCities[region];
                            if (region is > 0 and < 63)
                            {
                                ++landCities;
                            }
                        }
                        if (World.Map.UnitOwner(point) is null)
                        {
                            Goals.AddCurrent(owner, point, (int)RuleRole.Defend, 5);
                        }
                    }
                    else if ((byte)World.Globals.Difficulty * World.Globals.Turn > 200 || city.KnownTo.Contains(owner)
                        || city.Owner.Value == (byte)owner || !World.Globals.HumanPlayers.Contains(city.Owner.Value))
                    {
                        bool hostile = DiplomaticAttitudes.Hostile(World, owner, city.Owner.Value)
                            || Civ.Treaties[city.Owner.Value].HostileIntent && !Civ.Treaties[city.Owner.Value].Alliance;
                        if (hostile && (World.Globals.Turn + i) % 3 != 0)
                        {
                            Goals.AddCurrent(owner, point, (int)RuleRole.Attack, BuildingQueries.HasBuilding(World, new((ushort)i), 8) ? 2 : 4);
                        }

                        var treaty = Civ.Treaties[city.Owner.Value];
                        if (World.Map.UnitOwner(point) is null && !treaty.Ceasefire && !treaty.Peace && !treaty.Alliance)
                        {
                            Goals.AddCurrent(owner, point, (int)RuleRole.Defend, 2);
                            Goals.AddCurrent(owner, point, (int)RuleRole.Attack, 3);
                        }
                    }
                }
            }
        }
    }
}
