using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.City;

/// <summary>C02–C05 working, yields and upkeep under the authored single-city Despotism rules.</summary>
public static class LocalCityEconomy
{
    public const int Center = 20;

    public readonly record struct Yield(int Food, int Shields, int Trade)
    {
        public static Yield operator +(Yield a, Yield b) => new(a.Food + b.Food, a.Shields + b.Shields, a.Trade + b.Trade);
    }

    public static MapPoint Position(GameState world, MapPoint center, int index) =>
        MapNeighborhoods.Offset(world.Map.Geometry, center, MapNeighborhoods.Radius[index], world.Globals.Rules.FlatWorld);

    public static bool Available(GameState world, in State.City city, int index)
    {
        var point = Position(world, city.Position, index);
        if (!world.Map.Geometry.Canonical(point) || !world.Map.IsRevealedTo(point, city.Owner.Value))
        {
            return false;
        }

        var tile = world.Map.TileAt(point);
        return index == Center || (!tile.City && (!tile.Unit || tile.Owner == city.Owner.Value));
    }

    public static Yield TileYield(GameState world, RuleTables rules, in State.City city, int index)
    {
        var point = Position(world, city.Position, index);
        var tile = world.Map.TileAt(point);
        var terrain = rules.Terrain[(int)tile.Terrain];
        int food = terrain.Food, shields = terrain.Shields, trade = terrain.Trade;
        if (!tile.Ocean)
        {
            bool irrigation = tile.Features.HasFlag(MapFeatures.Irrigation);
            bool mine = tile.Features.HasFlag(MapFeatures.Mine);
            int irrigationBonus = terrain.Work?.Irrigation.YieldBonus ?? 0;
            if (tile.City || irrigation)
            {
                food += irrigationBonus;
            }

            if ((mine && !irrigation) || (tile.City && irrigationBonus == 0))
            {
                shields += terrain.Work?.Mining.YieldBonus ?? 0;
            }

            if (tile.Terrain == Terrain.Grassland && !Geometry.GrasslandShield(point))
            {
                shields = 0;
            }

            if (index == Center && shields == 0)
            {
                shields = 1;
            }

            if (tile.River)
            {
                ++trade;
            }

            if ((tile.City || tile.Features.HasFlag(MapFeatures.Road))
                && (tile.Terrain is Terrain.Desert or Terrain.Plains or Terrain.Grassland || trade != 0))
            {
                ++trade;
            }
        }

        if (tile.Features.HasFlag(MapFeatures.Railroad))
        {
            shields += shields / 2;
        }

        return new(Despotism(food, city.Flags.Celebrating), Despotism(shields, city.Flags.Celebrating),
            Despotism(trade, city.Flags.Celebrating));
    }

    private static int Despotism(int value, bool celebrating) => value > 2 && !celebrating ? value - 1 : value;

    private static Yield ApplyTile(GameState world, RuleTables rules, in State.City city, int index)
    {
        ref var tile = ref world.Map.TileAt(Position(world, city.Position, index));
        tile.Claimant = city.Owner.Value;
        if (!tile.City && !tile.Unit)
        {
            tile.Owner = city.Owner.Value;
        }

        return TileYield(world, rules, city, index);
    }

    /// <summary>Applies the C02 manual tile-click rules and recalculates retained assignments and output.</summary>
    public static void Toggle(GameState world, RuleTables rules, CitySlot slot, int index)
    {
        ref var city = ref world.Entities.Cities.At(slot);
        if (index == Center || (!city.WorkedTiles.Contains(index) && city.SpecialistCount == 0))
        {
            city.WorkedTiles = default;
            city.SpecialistCount = 0;
        }
        else
        {
            bool worked = city.WorkedTiles.Contains(index);
            city.WorkedTiles = city.WorkedTiles.With(index, !worked);
            city.SpecialistCount = (byte)(city.SpecialistCount + (worked ? 1 : -1));
        }

        Recalculate(world, rules, slot);
    }

    /// <summary>Keeps valid manual assignments and fills vacant worker positions by C02's ordered scores.</summary>
    public static int Recalculate(GameState world, RuleTables rules, CitySlot slot)
    {
        ref var city = ref world.Entities.Cities.At(slot);
        int targetSpecialists = city.SpecialistCount;
        int unassigned = city.Size + 1;
        Yield total = default;
        for (int i = 0; i <= Center; i++)
        {
            if (city.WorkedTiles.Contains(i) && !Available(world, city, i))
            {
                city.WorkedTiles = city.WorkedTiles.With(i, false);
            }

            if (city.WorkedTiles.Contains(i))
            {
                --unassigned;
            }
        }

        if (unassigned < 0)
        {
            city.WorkedTiles = default;
            unassigned = city.Size + 1;
        }

        if (city.WorkedTiles.Contains(Center))
        {
            total = ApplyTile(world, rules, city, Center);
        }

        for (int i = 0; i < Center; i++)
        {
            if (city.WorkedTiles.Contains(i))
            {
                total += ApplyTile(world, rules, city, i);
            }
        }

        int foodRequired = city.Size * rules.Cosmic.FoodPerCitizen;
        int support = Support(world, slot);
        if (unassigned > 0 && unassigned != targetSpecialists)
        {
            if (!city.WorkedTiles.Contains(Center))
            {
                city.WorkedTiles = city.WorkedTiles.With(Center);
                total += ApplyTile(world, rules, city, Center);
                --unassigned;
            }

            while (unassigned > targetSpecialists)
            {
                bool foodFirst = city.Size < 3 ||
                    total.Food + unassigned / Math.Max(1, (int)rules.Cosmic.FoodPerCitizen) < foodRequired;
                int next = BestTile(world, rules, city, total, foodRequired, support, unassigned, foodFirst);
                if (next < 0 && foodFirst)
                {
                    next = BestTile(world, rules, city, total, foodRequired, support, unassigned, false);
                }

                if (next < 0)
                {
                    break;
                }

                city.WorkedTiles = city.WorkedTiles.With(next);
                total += ApplyTile(world, rules, city, next);
                --unassigned;
            }
        }

        city.SpecialistCount = (byte)unassigned;
        city.Specialists = default;
        for (int i = 0; i < unassigned; i++)
        {
            city.Specialists[i] = SpecialistKind.Entertainer;
        }

        if (total.Food < foodRequired && city.FoodBox == 0)
        {
            city.FoodBox = -1;
        }
        else if (total.Food >= foodRequired && city.FoodBox < 0)
        {
            city.FoodBox = 0;
        }

        city.TotalFood = (byte)total.Food;
        city.TotalShields = (byte)total.Shields;
        // The capital's corruption and waste are zero in this one-city profile.
        city.BaseTrade = city.Trade = (short)total.Trade;
        ref var civ = ref world.Civilizations[city.Owner.Value];
        int luxury = Math.Clamp((total.Trade * (10 - civ.ScienceRate - civ.TaxRate) + 4) / 10, 0, total.Trade);
        city.Science = (short)Math.Clamp((total.Trade * civ.ScienceRate + 4) / 10, 0, total.Trade - luxury);
        city.Tax = (short)(total.Trade - luxury - city.Science);
        // Under the authored content limit, every worker starts content. C04 caps
        // luxury happiness by the number of workers after counting entertainers.
        city.Happy = (byte)Math.Min(luxury / 2 + unassigned, city.Size - unassigned);
        city.Unhappy = 0;
        return support;
    }

    private static int BestTile(
        GameState world, RuleTables rules, in State.City city, Yield total, int foodRequired, int support,
        int unassigned, bool foodFirst)
    {
        int best = -1, bestScore = 0, bestSecondary = 0;
        for (int i = 0; i < Center; i++)
        {
            if (city.WorkedTiles.Contains(i) || !Available(world, city, i))
            {
                continue;
            }

            var tile = TileYield(world, rules, city, i);
            if (foodFirst && unassigned == 1 && total.Shields == 0 && tile.Shields == 0)
            {
                continue;
            }

            int score = foodFirst ? tile.Food :
                16 / Math.Clamp(total.Food - foodRequired, 1, 99) * tile.Food
                + city.Size * 3 / Math.Clamp(total.Shields - support, 1, 99) * tile.Shields
                + city.Size * 2 / Math.Clamp(total.Trade, 1, 99) * tile.Trade;
            int secondary = foodFirst ? tile.Shields * 2 + tile.Trade : 0;
            if (score > bestScore || (score == bestScore && secondary > bestSecondary))
            {
                best = i;
                bestScore = score;
                bestSecondary = secondary;
            }
        }

        return best;
    }

    /// <summary>Recomputes C05 despotism support for the profile's homed military units, in slot order.</summary>
    public static int Support(GameState world, CitySlot slot)
    {
        var city = world.Entities.Cities.At(slot);
        int count = 0;
        foreach (ref var unit in world.Entities.Units.Scanned)
        {
            if (unit.Token.Occupied && unit.Owner == city.Owner && unit.Home == slot)
            {
                unit.Flags.CostsShield = ++count > city.Size;
                unit.Flags.Aggressive = unit.Position != city.Position && !world.Map.TileAt(unit.Position).City;
            }
        }

        return Math.Max(0, count - city.Size);
    }

    public static void ClaimRadius(GameState world, in State.City city)
    {
        for (int i = 0; i < MapNeighborhoods.Radius.Length; i++)
        {
            var point = Position(world, city.Position, i);
            if (!world.Map.Geometry.Canonical(point))
            {
                continue;
            }

            ref var tile = ref world.Map.TileAt(point);
            tile.Fertility %= 8;
            if (i <= Center)
            {
                tile.Claimant = city.Owner.Value;
            }
        }
    }

    public static void RememberCity(GameState world, ref State.City city)
    {
        int owner = city.Owner.Value;
        city.KnownTo = city.KnownTo.With(owner);
        city.RememberedSize[owner] = (byte)city.Size;
        LocalVisibility.Remember(world, owner, city.Position);
    }
}
