using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.City;

public enum FamineCause { SettlerSupport, Population, FoodRoute }

/// <summary>Semantic C05 notices and view invalidation. The view owns privacy, message preferences,
/// tutorial hints and presentation; native drawing/widget flags are not simulation state.</summary>
public interface ICityFoodEffects
{
    void Famine(CitySlot city, FamineCause cause, string? detail);
    void GrowthHalted(CitySlot city, int facility);
    void FoodShortage(CitySlot city);
    void RedrawCity(CitySlot city);
}

/// <summary>Required owning gameplay operations, distinct from optional presentation.
/// ReconsiderProduction means C06 request99, not a literal production item.</summary>
public interface ICityFoodContinuations
{
    void CheckCivilization(int owner, int killer);
    void ReconsiderProduction(CitySlot city);
}

/// <summary>C05 starvation/ordinary growth and the separate post-recalculation food accumulation.
/// The incoming food-route workspace belongs to the preceding calculation, as required by F04.</summary>
public sealed partial class CityFood(GameState world, RuleTables rules, CityRemoval removal, UnitLoss loss,
    ICityFoodContinuations continuations, ICityFoodEffects effects)
{
    private GameState World => world;
    private UnitLoss Loss => loss;
    private CityRemoval Removal => removal;
    private CityCalculationWorkspace Work => world.Calculation;
    private bool Human(int owner) => world.Globals.HumanPlayers.Contains(owner & 31);
    private static int Partner(CitySlot? slot) => slot is { } value ? unchecked((short)value.Value) : -1;

    /// <returns>True only after famine removes the city and runs the elimination continuation.</returns>
    public bool Run(CitySlot slot)
    {
        bool removed = false;
        foreach (var action in Steps(slot, value => removed = value))
        {
            switch (action.Kind)
            {
                case FoodWork.RemoveCity:
                    removal.Delete(action.Target);
                    break;
                case FoodWork.CheckOwner:
                    continuations.CheckCivilization(action.Target, -1);
                    break;
                case FoodWork.LoseSettler:
                    loss.EraseWithCargo(action.Target, true);
                    break;
            }
        }
        return removed;
    }

    /// <summary>Retain famine's loss and removal tails through actual lifecycle decisions.</summary>
    public Operation Begin(CitySlot slot, CivilizationElimination elimination, EspionageIncidents incidents,
        string call, int candidateBudget = 64) => new(this, slot, elimination, incidents, call, candidateBudget);

    private enum FoodWork { RemoveCity, CheckOwner, LoseSettler }
    private readonly record struct WorkItem(FoodWork Kind, int Target);

    private IEnumerable<WorkItem> Steps(CitySlot slot, Action<bool> complete)
    {
        MapKnowledge.RequireLocal(world);
        int owner = unchecked((sbyte)world.Entities.Cities.At(slot).Owner.Value);
        var support = new CitySupport(world, rules);
        support.FoodFactor(owner);
        if (world.Entities.Cities.At(slot).FoodBox < 0)
        {
            foreach (var action in Famine(slot, owner, complete))
            {
                yield return action;
            }

            yield break;
        }
        ref var city = ref world.Entities.Cities.At(slot);
        int limit = unchecked((city.Size + 1) * Work.FoodRows);
        if (city.FoodBox >= limit)
        {
            int? facility = support.GrowthFacilityNeeded(slot);
            city.Size = unchecked((sbyte)(city.Size + 1));
            city.FoodBox = BuildingQueries.HasBuilding(world, slot, BuildingIds.Granary)
                || BuildingQueries.HasWonder(world, rules, owner, WonderIds.Pyramids)
                ? unchecked((short)((city.Size + 1) * (rules.Cosmic.FoodBoxRows / 2))) : (short)0;
            if (facility is { } missing)
            {
                city.Size = unchecked((sbyte)(city.Size - 1));
                effects.GrowthHalted(slot, missing);
            }
            if (!Human(owner) && city.Size == 2)
            {
                continuations.ReconsiderProduction(slot);
            }
        }
        effects.RedrawCity(slot);
        complete(false);
    }

    private IEnumerable<WorkItem> Famine(CitySlot slot, int owner, Action<bool> complete)
    {
        ref var city = ref world.Entities.Cities.At(slot);
        int selected = -1;
        for (int i = 0; i < world.Entities.Units.End; ++i)
        {
            ref var unit = ref world.Entities.Units.Storage[i];
            if (unit.Token.Occupied && (unit.Home?.Value ?? 255) == slot.Value
                && rules.Units[unit.Type.Value].Role == RuleRole.Settle)
            {
                selected = i;
                break;
            }
        }
        var cause = selected < 0 ? FamineCause.Population : FamineCause.SettlerSupport;
        string? detail = selected < 0 ? null : RuleText.Display(rules.Units[world.Entities.Units.Storage[selected].Type.Value].Identity.Name);
        if (Partner(Work.FoodPartner) is >= 0 and var partner)
        {
            cause = FamineCause.FoodRoute;
            detail = world.Entities.Cities.Storage[partner].Name;
        }
        if (!world.Multiplayer.Saved.AutoplayActive)
        {
            effects.Famine(slot, cause, detail);
        }
        if (Work.FoodOut < 1)
        {
            if (selected < 0)
            {
                city.Size = unchecked((sbyte)(city.Size - 1));
                if (city.Size < 1)
                {
                    yield return new(FoodWork.RemoveCity, slot.Value);
                    yield return new(FoodWork.CheckOwner, owner);
                    complete(true);
                    yield break;
                }
            }
            else if (Human(owner) || city.KnownTo.Intersects(world.Globals.HumanPlayers))
            {
                yield return new(FoodWork.LoseSettler, selected);
            }
        }
        else
        {
            for (int i = unchecked((sbyte)city.RouteCount) - 1; i >= 0; --i)
            {
                if (city.RouteGoods[i] < 0 && Partner(city.RoutePartners[i]) == Partner(Work.FoodPartner))
                {
                    TradeRoutes.Remove(world, slot, i);
                }
            }
        }
        world.Entities.Cities.At(slot).FoodBox = 0;
        // X01 explicitly permits refreshing the affected city instead of using undefined native redraw locals.
        effects.RedrawCity(slot);
        complete(false);
    }

    /// <summary>Call after C03, with the stock captured before Run. Does not run famine or growth again.</summary>
    public int Accumulate(CitySlot slot, int previousFood)
    {
        MapKnowledge.RequireLocal(world);
        ref var city = ref world.Entities.Cities.At(slot);
        int surplus = unchecked(Work.Food - city.Size * rules.Cosmic.FoodPerCitizen - Work.SupportedSettlers * Work.SettlerFood);
        city.FoodBox = unchecked((short)(city.FoodBox + (short)surplus));
        if (previousFood > 0 && city.FoodBox < previousFood && unchecked(city.FoodBox + surplus * 3) < 0
            && !world.Globals.MessageOptions.FoodLow && !world.Multiplayer.Saved.AutoplayActive)
        {
            effects.FoodShortage(slot);
        }
        return surplus;
    }
}
