using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.City;

public enum ProductionCategory { Units, Buildings, OtherNonunit }
public record struct WonderNoticeStatus { public bool Started, Abandoned; }
public enum ProductionWonderNoticeKind { Start, Switch, Abandon }
public sealed record ProductionOption(int Item, string Name, int Stock, int Turns, bool ExpiredWonder);
public sealed record ProductionWonderNotice(ProductionWonderNoticeKind Kind, CitySlot City, int Owner,
    int Wonder, int? PreviousWonder, string Civilization, string Name, string? PreviousName);

/// <summary>C06 retained selection snapshot, per-world announcement state and owning F04 phase input.
/// Saving a build snapshot records no city identity; previews and Set do not refresh it.</summary>
public sealed class ProductionWorkspace
{
    public ProductionCategory SavedCategory;
    public int SavedStock;
    public int? CompletedImprovement;
    public bool CivilizationTurnInProgress;
    public CivilizationArray<WonderArray<WonderNoticeStatus>> WonderNotices;
}

/// <summary>C06 local production setting, AI wonder-change continuation and civilization reset.
/// Automatic requests require the real mutating A02 chooser; this service does not invent a choice.</summary>
public sealed class ProductionOrders(GameState world, RuleTables rules, Func<CitySlot, int> choose,
    Action<ProductionWonderNotice> notice)
{
    private ProductionWorkspace Work => world.Production;
    private bool Human(int owner) => world.Globals.HumanPlayers.Contains(owner & 31);
    private static bool IsUnit(int item) => item is >= 0 and < RuleLimits.UnitTypes;
    private static bool IsWonder(int item) => item < -BuildingIds.Capitalization;

    public static ProductionCategory Category(int item) => item >= 0 ? ProductionCategory.Units
        : item >= -BuildingIds.Last ? ProductionCategory.Buildings : ProductionCategory.OtherNonunit;

    public void SaveBuildState(CitySlot slot)
    {
        Work.SavedCategory = Category(world.Entities.Cities.At(slot).Production);
        Work.SavedStock = world.Entities.Cities.At(slot).ShieldBox;
    }

    public int ShieldsAfterChange(int request)
    {
        int stock = Work.SavedStock;
        return unchecked((byte)world.Globals.Difficulty) != (byte)Difficulty.Chieftain && Category(request) != Work.SavedCategory
            ? unchecked(stock - rules.Cosmic.ProductionSwitchLossPercent * unchecked((ushort)stock) / 100) : stock;
    }

    public int BuildTurns(int item, int stock)
    {
        unchecked
        {
            int cost = item >= 0 ? (sbyte)rules.Units[item].CostRows : rules.Improvements[-item].CostRows;
            int rate = Arithmetic.ClampC(world.Calculation.Shields - world.Calculation.ShieldSupport, 1, 99);
            return Arithmetic.ClampC((cost * world.Calculation.ShieldRows - 1 - stock) / rate + 1, 1, 999);
        }
    }

    /// <summary>C06 offered items in unit, improvement and wonder order, using the retained
    /// opening snapshot. The caller runs cached calculation/advice before constructing this list.</summary>
    public IReadOnlyList<ProductionOption> Options(CitySlot slot, WonderBuildRestrictions? scenario = null)
    {
        int owner = unchecked((sbyte)world.Entities.Cities.At(slot).Owner.Value);
        var result = new List<ProductionOption>();
        void Add(int item, string name, bool expired = false)
        {
            int stock = ShieldsAfterChange(item);
            result.Add(new(item, name, stock, BuildTurns(item, stock), expired));
        }
        for (int i = 0; i < RuleLimits.UnitTypes; ++i)
        {
            if (UnitAvailability.CanBuild(world, rules, owner, slot, new((byte)i)))
            {
                Add(i, rules.Units[i].Identity.Name);
            }
        }

        for (int i = 1; i < rules.Improvements.Length; ++i)
        {
            if (BuildingAvailability.Improvement(world, rules, owner, slot, i, scenario))
            {
                Add(-i, rules.Improvements[i].Identity.Name, i > BuildingIds.Capitalization && BuildingQueries.Expired(world, rules, i - 39));
            }
        }

        return result;
    }

    public void Set(CitySlot slot, int request)
    {
        MapKnowledge.RequireLocal(world);
        ref var city = ref world.Entities.Cities.At(slot);
        int oldItem = city.Production, owner = unchecked((sbyte)city.Owner.Value);
        if (IsUnit(city.Production))
        {
            world.Civilizations[owner].UnitsInProduction[city.Production] = unchecked((byte)(world.Civilizations[owner].UnitsInProduction[city.Production] - 1));
        }

        if (request >= 99)
        {
            request = choose(slot);
        }

        if (request < 99)
        {
            if (Human(owner) && !Work.CivilizationTurnInProgress)
            {
                city.ShieldBox = unchecked((short)ShieldsAfterChange(request));
            }

            city.Production = unchecked((sbyte)request);
        }
        if (city.Production != oldItem && !Human(owner))
        {
            WonderChange(slot, owner, oldItem);
        }

        if (IsUnit(city.Production))
        {
            world.Civilizations[owner].UnitsInProduction[city.Production] = unchecked((byte)(world.Civilizations[owner].UnitsInProduction[city.Production] + 1));
        }
    }

    private void WonderChange(CitySlot slot, int owner, int oldItem)
    {
        ref var city = ref world.Entities.Cities.At(slot);
        bool oldWonder = IsWonder(oldItem);
        if (!oldWonder && !IsWonder(city.Production))
        {
            return;
        }

        int oldCount = 0, newCount = 0;
        for (int i = 0; i < world.Entities.Cities.End; ++i)
        {
            ref var other = ref world.Entities.Cities.Storage[i];
            if (i == slot.Value || !other.Token.Occupied || other.Owner != city.Owner)
            {
                continue;
            }

            if (other.Production == oldItem)
            {
                ++oldCount;
            }

            if (other.Production == city.Production)
            {
                ++newCount;
            }
        }
        if (IsWonder(city.Production))
        {
            if (!oldWonder)
            {
                city.ShieldBox = unchecked((short)(city.ShieldBox / 2));
            }

            int improvement = -city.Production, wonder = improvement - 39, previous = -oldItem - 39;
            if (newCount == 0)
            {
                if (oldWonder && (oldCount == 0 || world.Globals.WonderCity[previous] != BuildingQueries.Unbuilt))
                {
                    if (oldCount != 0)
                    {
                        for (int i = 0; i < world.Entities.Cities.End; ++i)
                        {
                            ref var other = ref world.Entities.Cities.Storage[i];
                            if (other.Token.Occupied && unchecked((sbyte)other.Owner.Value) == owner && other.Production == oldItem)
                            {
                                other.Production = city.Production;
                            }
                        }
                    }

                    Emit(ProductionWonderNoticeKind.Switch, slot, owner, wonder, previous);
                }
                else if (!Work.WonderNotices[owner][wonder].Started)
                {
                    Work.WonderNotices[owner][wonder].Started = true;
                    Emit(ProductionWonderNoticeKind.Start, slot, owner, wonder, null);
                }
            }
            int group = wonder / 7;
            if (city.ShieldBox == 0 && world.Civilizations[owner].WondersByGroup[group] == 0)
            {
                bool behindEveryHuman = true;
                for (int c = 1; c < EntityLimits.Civilizations; ++c)
                {
                    if (Human(c) && world.Civilizations[c].WondersByGroup[group] == 0)
                    { behindEveryHuman = false; break; }
                }

                if (behindEveryHuman)
                {
                    int bonus = Math.Max(unchecked((byte)world.Globals.Difficulty) - 2 * world.Civilizations[owner].WondersByGroup[group], 0);
                    city.ShieldBox = unchecked((short)(city.ShieldBox + bonus * rules.Improvements[improvement].CostRows));
                }
            }
        }
        else if (oldWonder && oldCount == 0)
        {
            int wonder = -oldItem - 39;
            if (world.Globals.WonderCity[wonder] != slot.Value && !BuildingQueries.HasWonder(world, rules, owner, wonder)
                && !Work.WonderNotices[owner][wonder].Abandoned)
            {
                Work.WonderNotices[owner][wonder].Abandoned = true;
                Emit(ProductionWonderNoticeKind.Abandon, slot, owner, wonder, null);
            }
        }
    }

    private void Emit(ProductionWonderNoticeKind kind, CitySlot city, int owner, int wonder, int? previous)
    {
        if (!world.Multiplayer.Saved.AutoplayActive)
        {
            notice(new(kind, city, owner, wonder, previous, world.Names[owner].Tribe,
                rules.Improvements[39 + wonder].Identity.Name,
                previous is { } old ? rules.Improvements[39 + old].Identity.Name : null));
        }
    }

    public void ResetCivilization(int owner, int regionOrAny = -1)
    {
        MapKnowledge.RequireLocal(world);
        if (world.Globals.Multiplayer == GameMode.Hotseat && world.Multiplayer.HotseatParticipants.Contains(owner & 31))
        {
            return;
        }

        for (int i = 0; i < world.Entities.Cities.End; ++i)
        {
            ref var city = ref world.Entities.Cities.Storage[i];
            if (city.Token.Occupied && unchecked((sbyte)city.Owner.Value) == unchecked((byte)owner)
                && (regionOrAny < 0 || world.Map.TileAt(city.Position).Region == regionOrAny))
            {
                Set(new((ushort)i), 99);
            }
        }
    }
}
