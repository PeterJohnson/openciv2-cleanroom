using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.City;

/// <summary>Alder Valley command/display bridge to shared city calculation; pure display previews use its authored rules.</summary>
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

    /// <summary>Applies the C02 manual tile-click rules and recalculates retained assignments and output.</summary>
    public static void Toggle(GameState world, RuleState rules, CitySlot slot, int index)
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

    /// <summary>Runs the complete C03 calculation and returns its C05 shield support.</summary>
    public static int Recalculate(GameState world, RuleState rules, CitySlot slot)
    {
        new CityEconomy(world, rules, new StackOperations(world, new(5_000_000, 64))).Calculate(slot);
        return world.Calculation.ShieldSupport;
    }

    public static int Support(GameState world, RuleTables rules, CitySlot slot)
    {
        new CitySupport(world, rules).Calculate(slot);
        return world.Calculation.ShieldSupport;
    }

    public static void ClaimRadius(GameState world, in State.City city) => CityArea.Refresh(world, city.Position, city.Owner);

    public static void RememberCity(GameState world, ref State.City city)
    {
        int owner = city.Owner.Value;
        city.KnownTo = city.KnownTo.With(owner);
        city.RememberedSize[owner] = (byte)city.Size;
        LocalVisibility.Remember(world, owner, city.Position);
    }
}
