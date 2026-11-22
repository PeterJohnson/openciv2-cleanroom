using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.City;

public enum UnitProductionOutcome
{
    NotReady, Completed, CreationFailed, ChieftainRefusal, LastCitySettler, SettlerCancelled, CityDisbanded
}

public sealed record UnitProductionResult(UnitProductionOutcome Outcome, UnitTarget? Unit = null);

/// <summary>Owning C06/A02/C07/V01 children. These must execute their real policies or suspend for
/// accepted player choices; merely recording a request does not implement the complete city turn.</summary>
public interface ICityUnitProductionContinuations
{
    void ReconsiderProduction(CitySlot city);
    void ChooseTradeCargo(CitySlot city, int owner, UnitSlot unit);
    void Purchase(CitySlot city, int owner, int netShields);
    int CheckCivilization(int owner, int killer);
    bool AbandonCity(CitySlot city, UnitType type);
}

public interface ICityUnitProductionEffects
{
    void Upgraded(CitySlot city, UnitType oldType, UnitType newType);
    void Built(CityTarget city, UnitType type);
    void Redraw(MapPoint position);
}

/// <summary>C06's local unit-production branch, with its entry accumulation and common tail.
/// Uses actual U01 creation/deletion and C01 removal. Nonunit completion is a separate pending branch;
/// a chooser changing the next item does not run a second completion pass.</summary>
public sealed class CityUnitProduction(GameState world, RuleTables rules, UnitCreation creation,
    UnitDeletion deletion, CityRemoval removal, GameplayRandom random,
    ICityUnitProductionContinuations continuations, ICityUnitProductionEffects effects)
{
    private CityCalculationWorkspace Work => world.Calculation;
    private bool Human(int owner) => world.Globals.HumanPlayers.Contains(owner);
    private bool Knows(int owner, int advance) => CivilizationKnowledge.KnowsAdvance(world.Civilizations, owner, advance);
    private bool Has(CitySlot city, int building) => BuildingQueries.HasBuilding(world, city, building);
    private bool Wonder(int owner, int wonder) => BuildingQueries.HasWonder(world, rules, owner, wonder);

    public UnitProductionResult Run(CitySlot slot, string call)
    {
        MapKnowledge.RequireLocal(world);
        ArgumentException.ThrowIfNullOrEmpty(call);
        ref var city = ref world.Entities.Cities.At(slot);
        int type = city.Production;
        if (type < 0 || type >= RuleLimits.UnitTypes)
        {
            throw new ArgumentOutOfRangeException(nameof(slot), "Unit production requires a unit order");
        }

        int owner = unchecked((sbyte)city.Owner.Value);
        CityTarget target = new(slot, city.Token);
        int net = city.Flags.Disorder ? 0 : Math.Max(0, unchecked(Work.Shields - Work.ShieldSupport));
        city.ShieldBox = unchecked((short)(city.ShieldBox + (short)net));
        if (owner == 0 && world.Civilizations[0].ContinentMilitary[world.Map.TileAt(city.Position).Region] > 16
            && world.Map.UnitOwner(city.Position) is not null)
        {
            city.ShieldBox = 0;
        }

        int obsolete = (int)rules.Units[type].ObsoleteBy;
        if (obsolete >= 0 && Knows(owner, obsolete))
        {
            for (int candidate = 0; candidate < RuleLimits.UnitTypes; ++candidate)
            {
                if ((int)rules.Units[candidate].Prerequisite != obsolete || rules.Units[candidate].Role != rules.Units[type].Role)
                {
                    continue;
                }

                if (Human(owner))
                {
                    effects.Upgraded(slot, new((byte)type), new((byte)candidate));
                }

                city.Production = (sbyte)candidate;
                type = candidate;
                break;
            }
        }
        int cost = unchecked(unchecked((sbyte)rules.Units[type].CostRows) * Work.ShieldRows);
        if (cost > city.ShieldBox)
        {
            return Finish(new(UnitProductionOutcome.NotReady));
        }

        city.Flags.WonderBuilt = false;
        if (rules.Units[type].Role == RuleRole.Settle && city.Size == 1 && world.Globals.Difficulty == Difficulty.Chieftain)
        {
            return new(UnitProductionOutcome.ChieftainRefusal);
        }

        city.ShieldBox = unchecked((short)(city.ShieldBox - (short)cost));

        UnitSlot? created = null;
        if (Human(owner) || rules.Units[type].Role != RuleRole.Diplomacy)
        {
            created = creation.Create(new((byte)type), new((byte)owner), city.Position);
            if (created is not { } unit)
            {
                return Finish(new(UnitProductionOutcome.CreationFailed));
            }

            SetVeteran(slot, owner, type, unit);
            if (rules.Units[type].Domain == RuleDomain.Sea && rules.Units[type].Role == RuleRole.SeaTransport)
            {
                world.Entities.Units.At(unit).Counter = (byte)(255 - slot.Value % 64);
            }

            if (rules.Units[type].Role == RuleRole.Settle)
            {
                if (city.Size == 1 && world.Civilizations[owner].CityCount == 1)
                {
                    return new(UnitProductionOutcome.LastCitySettler, UnitTarget(unit));
                }

                if (city.Size == 1 && Human(owner)
                    && (world.Multiplayer.Saved.AutoplayActive || !continuations.AbandonCity(slot, new((byte)type))))
                {
                    deletion.Erase(unit.Value, true);
                    return new(UnitProductionOutcome.SettlerCancelled);
                }
                city.Size = unchecked((sbyte)(city.Size - 1));
                if (city.Size < 1)
                {
                    var position = city.Position;
                    removal.Delete(slot.Value);
                    if (continuations.CheckCivilization(owner, -1) == 0)
                    {
                        creation.Create(new((byte)type), new((byte)owner), position);
                    }

                    if (!world.Globals.MessageOptions.UnitBuilt)
                    {
                        effects.Built(target, new((byte)type));
                    }

                    effects.Redraw(position);
                    return new(UnitProductionOutcome.CityDisbanded);
                }
            }
        }

        if (!Human(owner))
        {
            if (rules.Units[type].Role == RuleRole.Trade)
            {
                continuations.ChooseTradeCargo(slot, owner, created!.Value);
            }

            if (rules.Units[type].Role == RuleRole.Diplomacy)
            {
                created = CreateDiplomat(slot, owner, type, call);
            }

            if (unchecked((sbyte)rules.Units[type].Attack) >= 99 && unchecked((sbyte)world.Civilizations[owner].UnitsActive[type]) == 1)
            {
                for (int observer = 1; observer < EntityLimits.Civilizations; ++observer)
                {
                    world.Civilizations[observer].LastContactTurn[owner] = -1;
                }
            }

            continuations.ReconsiderProduction(slot);
        }
        else
        {
            int role = unchecked((sbyte)rules.Units[type].Role);
            if (role >= (int)RuleRole.Settle)
            {
                if (role == (int)RuleRole.Trade)
                {
                    continuations.ChooseTradeCargo(slot, owner, created!.Value);
                }
                else if (!world.Globals.MessageOptions.UnitBuilt)
                {
                    effects.Built(target, new((byte)type));
                }
            }
            city.ShieldBox = 0;
            if (city.Flags.Autobuild)
            {
                continuations.ReconsiderProduction(slot);
            }
        }
        return Finish(new(UnitProductionOutcome.Completed, created is { } built ? UnitTarget(built) : null));

        UnitTarget UnitTarget(UnitSlot unit) => new(unit, world.Entities.Units.At(unit).Token);

        UnitProductionResult Finish(UnitProductionResult result)
        {
            ref var live = ref world.Entities.Cities.At(slot);
            // This unit branch's retained improvement is None, including after the chooser changes its order.
            if (Human(owner) && live.Flags.Autobuild && (live.ShieldBox == 0 || live.Production < 0))
            {
                continuations.ReconsiderProduction(slot);
                if (live.Production == 99)
                {
                    live.Flags.Autobuild = live.Flags.UnitsOnlyAutomation = live.Flags.ImprovementsOnlyAutomation = false;
                }
            }
            if (!Human(owner))
            {
                continuations.Purchase(slot, owner, net);
            }

            live.Flags.DefenderLost = false;
            return result;
        }
    }

    private void SetVeteran(CitySlot slot, int owner, int type, UnitSlot unit)
    {
        var rule = rules.Units[type];
        bool veteran;
        if (rule.Role == RuleRole.Diplomacy && unchecked((byte)world.Civilizations[owner].Government) == (int)Government.Communism)
        {
            veteran = true;
        }
        else if (rule.Domain == RuleDomain.Land || !world.Globals.Rules.NormalCombat)
        {
            veteran = (Has(slot, BuildingIds.Barracks) || Wonder(owner, WonderIds.SunTzu))
                && unchecked((sbyte)rule.Role) < 6 && !rule.Abilities.DestroyedAfterAttack;
        }
        else if (rule.Domain == RuleDomain.Sea)
        {
            veteran = (Has(slot, BuildingIds.PortFacility) || Wonder(owner, WonderIds.Lighthouse)) && !rule.Abilities.DestroyedAfterAttack;
        }
        else
        {
            veteran = rule.Domain == RuleDomain.Air && Has(slot, BuildingIds.Airport) && !rule.Abilities.DestroyedAfterAttack;
        }

        if (veteran)
        {
            world.Entities.Units.At(unit).Flags.Veteran = true;
        }
    }

    private UnitSlot? CreateDiplomat(CitySlot slot, int owner, int type, string call)
    {
        int bestDistance = 32767, ties = 0;
        UnitSlot? target = null;
        for (int i = 0; i < world.Entities.Cities.End; ++i)
        {
            ref var city = ref world.Entities.Cities.Storage[i];
            if (!city.Token.Occupied)
            {
                continue;
            }

            int other = unchecked((sbyte)city.Owner.Value);
            if (other == owner || world.Civilizations[other].Treaties[owner].Ceasefire || world.Civilizations[other].Treaties[owner].Peace)
            {
                continue;
            }

            var nearest = UnitQueries.Nearest(world, city.Position, owner);
            int distance = unchecked(nearest.Distance - 3);
            if (distance < 0)
            {
                distance = unchecked(-distance);
            }

            if (nearest.Slot is not { } unit)
            {
                continue;
            }

            if (city.Flags.TechnologyStolen && distance < 4)
            {
                distance = 3;
            }

            if (distance > bestDistance)
            {
                continue;
            }

            if (distance == bestDistance && ties++ > 0
                && random.PrimaryModulo(new ChoiceKey("C06.diplomat.tie", call, i), ++ties) != 0)
            {
                continue;
            }

            var position = world.Entities.Units.At(unit).Position;
            if (world.Map.TileAt(position).Ocean || world.Map.TileAt(city.Position).Region != world.Map.TileAt(position).Region)
            {
                continue;
            }
            // The contract's greater-distance update is intentional. Pure queries cannot synthesize a smaller best.
            if (bestDistance < distance)
            { ties = 1; bestDistance = distance; }
            target = unit;
        }
        var origin = world.Entities.Cities.At(slot).Position;
        if (bestDistance < 4 && random.PrimaryModulo(new ChoiceKey("C06.diplomat.placement", call, 0), 10) <= (byte)world.Globals.Difficulty)
        {
            origin = world.Entities.Units.At(target!.Value).Position;
        }

        var created = creation.Create(new((byte)type), new((byte)owner), origin);
        if (created is { } diplomat && unchecked((byte)world.Civilizations[owner].Government) == (int)Government.Communism)
        {
            world.Entities.Units.At(diplomat).Flags.Veteran = true;
        }

        return created;
    }
}
