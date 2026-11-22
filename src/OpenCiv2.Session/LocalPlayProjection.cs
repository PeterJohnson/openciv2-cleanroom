using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Session;

public sealed partial class SessionRuntime
{
    private sealed partial class PlayerClient
    {
        public LocalPlayObservation? ObserveLocalPlay()
        {
            var (current, grant) = Checked();
            var observer = grant.Identity.Observer;
            if (!current.LocalPlay || observer.Value != LocalGameRules.Owner)
            {
                return null;
            }

            var world = current.World;
            var rules = current.Process.Rules.Definitions;
            var civ = world.Civilizations[observer.Value];
            var units = new List<PlayUnit>();
            var pool = world.Entities.Units.Scanned;
            for (ushort i = 0; i < pool.Length; i++)
            {
                var unit = pool[i];
                if (!unit.Token.Occupied || unit.Owner != observer)
                {
                    continue;
                }

                units.Add(new(new(new(i), unit.Token), rules.Units[unit.Type.Value].Identity.Name,
                    LandMovement.Left(world, rules, unit), unit.Type.Value == LocalGameRules.Settler && civ.CityCount == 0
                    && unit.Orders == UnitOrder.None));
            }

            var research = LocalPlayCommands.ResearchChoices(world, rules, observer.Value)
                .Select(i => new PlayOption(i, rules.Advances[i].Identity.Name));
            var known = Enumerable.Range(0, rules.Advances.Length).Where(civ.Advances.Contains)
                .Select(i => new PlayOption(i, rules.Advances[i].Identity.Name));
            return new(new(current.Identity, current.Session, current.Revision, observer),
                world.Globals.Turn, world.Globals.Year, world.PlayerPhaseActive, civ.Gold, civ.Researching,
                civ.ResearchProgress, LocalResearch.Cost(world, rules, observer.Value), new(research), new(known),
                new(units), ObserveCity(current, observer, rules));
        }

        private static PlayCity? ObserveCity(RuntimeState current, CivilizationSlot observer, RuleTables rules)
        {
            var world = current.World;
            var pool = world.Entities.Cities;
            CitySlot slot = new(0);
            if (!pool.Live(slot))
            {
                return null;
            }

            var city = pool.At(slot);
            if (city.Owner != observer)
            {
                return null;
            }

            var tiles = new List<WorkerTile>();
            for (int i = 0; i <= LocalCityEconomy.Center; i++)
            {
                if (!LocalCityEconomy.Available(world, city, i))
                {
                    continue;
                }

                var position = LocalCityEconomy.Position(world, city.Position, i);
                var yields = LocalCityEconomy.TileYield(world, rules, city, i);
                tiles.Add(new(i, position, city.WorkedTiles.Contains(i), yields.Food, yields.Shields, yields.Trade));
            }

            // Counting checks flags already set by upkeep or recalculation, without changing any record.
            int support = 0;
            foreach (var unit in world.Entities.Units.Scanned)
            {
                if (unit.Token.Occupied && unit.Home == slot && unit.Owner == observer)
                {
                    ++support;
                }
            }

            var production = new[] { LocalGameRules.Guard, LocalGameRules.Rider, -BuildingIds.Temple, -BuildingIds.Library }
                .Where(i => LocalGameRules.ProductionAvailable(world, rules, i, slot))
                .Select(i => new PlayOption(i, i >= 0 ? rules.Units[i].Identity.Name : rules.Improvements[-i].Identity.Name));
            return new(new(slot, city.Token), city.TotalFood, city.Size * rules.Cosmic.FoodPerCitizen,
                (city.Size + 1) * rules.Cosmic.FoodBoxRows, city.TotalShields, Math.Max(0, support - city.Size),
                (city.Production >= 0 ? rules.Units[city.Production].CostRows : rules.Improvements[-city.Production].CostRows) * rules.Cosmic.ShieldBoxRows,
                city.Science, city.Tax, city.SpecialistCount, new(tiles), new(production));
        }
    }
}
