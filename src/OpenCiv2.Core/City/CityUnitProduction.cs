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
    int CheckCivilization(int owner, int killer);
    bool AbandonCity(CitySlot city, UnitType type);
}

public interface ICityUnitProductionEffects
{
    void Upgraded(CitySlot city, UnitType oldType, UnitType newType);
    void Built(CityTarget city, UnitType type);
    void Redraw(MapPoint position);
    void AlmostWonder(AlmostWonderNotice notice);
}

/// <summary>C06's local unit-production branch, with its entry accumulation and common tail.
/// Uses actual U01 creation/deletion and C01 removal. CityProduction owns nonunit completion;
/// a chooser changing the next item does not run a second completion pass.</summary>
public sealed partial class CityUnitProduction(GameState world, RuleTables rules, UnitCreation creation,
    UnitDeletion deletion, CityRemoval removal, GameplayRandom random,
    ICityUnitProductionContinuations continuations, ICityUnitProductionEffects effects, CargoSelection? cargo = null)
{
    private readonly AiProductionPurchase purchase = new(world, rules, continuations.ReconsiderProduction, effects.AlmostWonder);
    private GameState World => world;
    private CityRemoval Removal => removal;
    private CargoSelection Cargo => cargo ?? throw new InvalidOperationException("Retained trade production requires cargo selection services");
    private ref State.City CityAt(CitySlot slot) => ref world.Entities.Cities.At(slot);
    private CityCalculationWorkspace Work => world.Calculation;
    private bool Human(int owner) => world.Globals.HumanPlayers.Contains(owner);
    private bool Knows(int owner, int advance) => CivilizationKnowledge.KnowsAdvance(world.Civilizations, owner, advance);
    private bool Has(CitySlot city, int building) => BuildingQueries.HasBuilding(world, city, building);
    private bool Wonder(int owner, int wonder) => BuildingQueries.HasWonder(world, rules, owner, wonder);

    public UnitProductionResult Run(CitySlot slot, string call)
    {
        UnitProductionResult? result = null;
        foreach (var action in Steps(slot, call, value => result = value))
        {
            switch (action.Kind)
            {
                case ProductionWorkKind.Abandon:
                    action.Reply!(continuations.AbandonCity(slot, action.Choice!.Type) ? 1 : 0);
                    break;
                case ProductionWorkKind.DeleteUnit:
                    deletion.Erase(action.Target, true);
                    break;
                case ProductionWorkKind.RemoveCity:
                    removal.Delete(action.Target);
                    break;
                case ProductionWorkKind.CheckOwner:
                    action.Reply!(continuations.CheckCivilization(action.Target, -1));
                    break;
                case ProductionWorkKind.Cargo:
                    continuations.ChooseTradeCargo(slot, action.Owner, new((ushort)action.Target));
                    break;
            }
        }
        return result!;
    }

    /// <summary>Retain GhostTown and actual deletion/elimination before completing C06's unit branch.
    /// Production policy remains a required owning continuation; trade requires C07 services.</summary>
    public Operation Begin(CitySlot slot, string call, CivilizationElimination lifecycle,
        EspionageIncidents incidents, int candidateBudget = 64) => new(this, slot, call, lifecycle, incidents, candidateBudget);

    private enum ProductionWorkKind { Abandon, DeleteUnit, RemoveCity, CheckOwner, Cargo }
    private sealed record ProductionWork(ProductionWorkKind Kind, int Target, Action<int>? Reply = null, SettlerAbandonment? Choice = null,
        int Owner = 0, bool Human = false);

    private IEnumerable<ProductionWork> Steps(CitySlot slot, string call, Action<UnitProductionResult> complete)
    {
        MapKnowledge.RequireLocal(world);
        ArgumentException.ThrowIfNullOrEmpty(call);
        int type = CityAt(slot).Production;
        if (type < 0 || type >= RuleLimits.UnitTypes)
        {
            throw new ArgumentOutOfRangeException(nameof(slot), "Unit production requires a unit order");
        }

        world.Production.CompletedImprovement = null;
        int owner = unchecked((sbyte)CityAt(slot).Owner.Value);
        CityTarget target = new(slot, CityAt(slot).Token);
        int net = CityAt(slot).Flags.Disorder ? 0 : Math.Max(0, unchecked(Work.Shields - Work.ShieldSupport));
        CityAt(slot).ShieldBox = unchecked((short)(CityAt(slot).ShieldBox + (short)net));
        if (owner == 0 && world.Civilizations[0].ContinentMilitary[world.Map.TileAt(CityAt(slot).Position).Region] > 16
            && world.Map.UnitOwner(CityAt(slot).Position) is not null)
        {
            CityAt(slot).ShieldBox = 0;
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

                CityAt(slot).Production = (sbyte)candidate;
                type = candidate;
                break;
            }
        }
        int cost = unchecked(unchecked((sbyte)rules.Units[type].CostRows) * Work.ShieldRows);
        if (cost > CityAt(slot).ShieldBox)
        {
            complete(Finish(new(UnitProductionOutcome.NotReady)));
            yield break;
        }

        CityAt(slot).Flags.WonderBuilt = false;
        if (rules.Units[type].Role == RuleRole.Settle && CityAt(slot).Size == 1 && world.Globals.Difficulty == Difficulty.Chieftain)
        {
            complete(new(UnitProductionOutcome.ChieftainRefusal));
            yield break;
        }

        CityAt(slot).ShieldBox = unchecked((short)(CityAt(slot).ShieldBox - (short)cost));

        UnitSlot? created = null;
        if (Human(owner) || rules.Units[type].Role != RuleRole.Diplomacy)
        {
            created = creation.Create(new((byte)type), new((byte)owner), CityAt(slot).Position);
            if (created is not { } unit)
            {
                complete(Finish(new(UnitProductionOutcome.CreationFailed)));
                yield break;
            }

            SetVeteran(slot, owner, type, unit);
            if (rules.Units[type].Domain == RuleDomain.Sea && rules.Units[type].Role == RuleRole.SeaTransport)
            {
                world.Entities.Units.At(unit).Counter = (byte)(255 - slot.Value % 64);
            }

            if (rules.Units[type].Role == RuleRole.Settle)
            {
                if (CityAt(slot).Size == 1 && world.Civilizations[owner].CityCount == 1)
                {
                    complete(new(UnitProductionOutcome.LastCitySettler, UnitTarget(unit)));
                    yield break;
                }

                if (CityAt(slot).Size == 1 && Human(owner))
                {
                    int abandon = 0;
                    if (!world.Multiplayer.Saved.AutoplayActive)
                    {
                        var choice = new SettlerAbandonment(owner, target, CityAt(slot).Name,
                            new((byte)type), RuleText.Display(rules.Units[type].Identity.Name));
                        yield return new(ProductionWorkKind.Abandon, owner, value => abandon = value, choice);
                    }
                    if (abandon == 0)
                    {
                        yield return new(ProductionWorkKind.DeleteUnit, unit.Value);
                        complete(new(UnitProductionOutcome.SettlerCancelled));
                        yield break;
                    }
                }
                CityAt(slot).Size = unchecked((sbyte)(CityAt(slot).Size - 1));
                if (CityAt(slot).Size < 1)
                {
                    var position = CityAt(slot).Position;
                    yield return new(ProductionWorkKind.RemoveCity, slot.Value);
                    int eliminated = 0;
                    yield return new(ProductionWorkKind.CheckOwner, owner, value => eliminated = value);
                    if (eliminated == 0)
                    {
                        creation.Create(new((byte)type), new((byte)owner), position);
                    }

                    if (!world.Globals.MessageOptions.UnitBuilt)
                    {
                        effects.Built(target, new((byte)type));
                    }

                    effects.Redraw(position);
                    complete(new(UnitProductionOutcome.CityDisbanded));
                    yield break;
                }
            }
        }

        if (!Human(owner))
        {
            if (rules.Units[type].Role == RuleRole.Trade)
            {
                yield return new(ProductionWorkKind.Cargo, created!.Value.Value, Owner: owner);
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
                    yield return new(ProductionWorkKind.Cargo, created!.Value.Value, Owner: owner, Human: true);
                }
                else if (!world.Globals.MessageOptions.UnitBuilt)
                {
                    effects.Built(target, new((byte)type));
                }
            }
            CityAt(slot).ShieldBox = 0;
            if (CityAt(slot).Flags.Autobuild)
            {
                continuations.ReconsiderProduction(slot);
            }
        }
        complete(Finish(new(UnitProductionOutcome.Completed, created is { } built ? UnitTarget(built) : null)));
        yield break;

        UnitTarget UnitTarget(UnitSlot unit) => new(unit, world.Entities.Units.At(unit).Token);

        UnitProductionResult Finish(UnitProductionResult result)
        {
            ref var live = ref CityAt(slot);
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
                purchase.Run(slot, owner, net);
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
        var origin = CityAt(slot).Position;
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
