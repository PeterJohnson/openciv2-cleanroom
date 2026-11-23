using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Formats;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static RevelationFixture TerrainGoalFixture()
    {
        var f = new RevelationFixture();
        foreach (ref var tile in f.World.Map.Tiles) tile.Region = 1;
        f.Rules.Units[UnitTypeIds.Warriors] = f.Rules.Units[0];
        f.AddCity(0, 1, new(10, 10));
        f.World.Civilizations[1].ContinentCities[1] = 1;
        return f;
    }
    private static CityTerrainGoals TerrainGoals(RevelationFixture f) => new(f.World, f.Rules);
    private static CivilizationGoal[] CurrentTerrainGoals(RevelationFixture f) =>
        ((ReadOnlySpan<CivilizationGoal>)f.World.Civilizations[1].Goals).ToArray().Where(g => g.Role == 21).ToArray();

    private static void CityTerrainPollutionGoals()
    {
        var f = TerrainGoalFixture(); var work = f.World.Calculation;
        work.JobFound = true; work.JobPriority = 17; work.JobPosition = new(20, 10);
        foreach (var point in new[] { new MapPoint(11, 9), new(9, 9), new(10, 10) })
            f.World.Map.TileAt(point).Features |= MapFeatures.Pollution;
        TerrainGoals(f).Publish(new(0));
        var goals = CurrentTerrainGoals(f);
        Equal(2, goals.Length); Equal(true, goals.All(g => g.Priority == 6));
        Equal(false, goals.Any(g => g.Position == new MapPoint(10, 10))); // Radius excludes center.
        Equal(true, f.World.Entities.Cities.Storage[0].Flags.TerrainWorkGoal);
        Equal(17, work.JobPriority); Equal(new MapPoint(20, 10), work.JobPosition);
        foreach (ref var goal in (Span<CivilizationGoal>)f.World.Civilizations[1].Goals)
            goal = new(new(18, 18), 3, 7);
        f.World.Entities.Cities.Storage[0].Flags.TerrainWorkGoal = false;
        TerrainGoals(f).Publish(new(0));
        Equal(0, CurrentTerrainGoals(f).Length);
        Equal(true, f.World.Entities.Cities.Storage[0].Flags.TerrainWorkGoal); // Insertion failure does not suppress flag.

        f = TerrainGoalFixture(); f.World.Map.Allocate(new(4, 8));
        f.World.Globals.Rules.FlatWorld = false;
        f.World.Entities.Cities.Storage[0].Position = new(0, 4);
        f.World.Map.TileAt(new(1, 3)).Features = MapFeatures.Pollution;
        TerrainGoals(f).Publish(new(0));
        Equal(1, CurrentTerrainGoals(f).Length); // Repeated wrapped coordinates use real A01 duplicate handling.
        Equal(new MapPoint(1, 3), CurrentTerrainGoals(f).Single().Position);
    }

    private static void CityTerrainPriorityAndNarrowing()
    {
        foreach (var (priority, size, palace, expected) in new[]
        { (0, 5, true, 2), (1, 5, true, 5), (1, 3, true, 3), (0, 3, true, 1), (1, 4, false, 3), (int.MaxValue, 5, true, int.MinValue + 1) })
        {
            var f = TerrainGoalFixture(); var work = f.World.Calculation;
            work.JobFound = true; work.JobPriority = priority; work.JobPosition = new(12, 10);
            f.World.Entities.Cities.Storage[0].Size = (sbyte)size;
            BuildingQueries.SetBuilding(f.World, new(0), BuildingIds.Palace, palace);
            TerrainGoals(f).Publish(new(0));
            Equal(expected, work.JobPriority);
            Equal(unchecked((sbyte)expected), CurrentTerrainGoals(f).Single().Priority);
        }
    }

    private static void CityTerrainRailroadRetention()
    {
        var f = TerrainGoalFixture(); var work = f.World.Calculation;
        f.World.Entities.Cities.Storage[0].Flags.TerrainWorkGoal = true;
        f.World.Civilizations[1].Advances = default(AdvanceSet).With(AdvanceIds.Railroad);
        TerrainGoals(f).Publish(new(0));
        Equal(3, work.JobPriority); Equal(false, work.JobFound);
        Equal(true, f.World.Entities.Cities.Storage[0].Flags.TerrainWorkGoal);
        f.World.Civilizations[1].Advances = default;
        TerrainGoals(f).Publish(new(0));
        Equal(2, work.JobPriority); Equal(false, f.World.Entities.Cities.Storage[0].Flags.TerrainWorkGoal);

        foreach (bool human in new[] { false, true })
        {
            f = TerrainGoalFixture(); work = f.World.Calculation;
            f.AddCity(1, 1, new(18, 10)); f.World.Civilizations[1].ContinentCities[1] = 2;
            f.World.Civilizations[1].Advances = default(AdvanceSet).With(AdvanceIds.Railroad);
            f.World.Globals.HumanPlayers = CivilizationSet.Only(human ? 1 : 2);
            work.JobFound = true; work.JobPosition = new(20, 12);
            TerrainGoals(f).Publish(new(0));
            Equal(human ? new MapPoint(20, 12) : new(12, 10), work.JobPosition);
            Equal(human ? 2 : 5, work.JobPriority);
        }
        f = TerrainGoalFixture(); work = f.World.Calculation;
        work.JobFound = true; work.JobPosition = new(20, 12); work.JobPriority = 9;
        f.World.Globals.HumanPlayers = default;
        f.World.Civilizations[1].Advances = default(AdvanceSet).With(AdvanceIds.Railroad);
        TerrainGoals(f).Publish(new(0));
        Equal(11, work.JobPriority); Equal(new MapPoint(20, 12), work.JobPosition); // Failed road search retains existing suggestion.
    }

    private static void CityRoadPlanningCityOrderAndTreaties()
    {
        var f = TerrainGoalFixture();
        f.AddCity(1, 2, new(10, 6)); f.AddCity(2, 1, new(18, 10));
        f.World.Civilizations[1].ContinentCities[1] = 2;
        f.World.Civilizations[1].Treaties[2].Peace = true;
        Equal(true, TerrainGoals(f).FindRoad(new(0)));
        Equal(new MapPoint(12, 10), f.World.Calculation.JobPosition); // Owned pass precedes lower-slot peaceful city.
        foreach (ref var tile in f.World.Map.Tiles) tile.Features |= MapFeatures.Road;
        f.World.Map.TileAt(new(10, 8)).Features &= ~MapFeatures.Road;
        Equal(true, TerrainGoals(f).FindRoad(new(0))); Equal(new MapPoint(10, 8), f.World.Calculation.JobPosition);
        f.World.Civilizations[1].Treaties[2].Peace = false;
        f.World.Civilizations[2].Treaties[1].Peace = true;
        f.World.Civilizations[1].Treaties[2].Ceasefire = true;
        Equal(false, TerrainGoals(f).FindRoad(new(0)));
        f.World.Civilizations[1].Treaties[2].Alliance = true;
        Equal(true, TerrainGoals(f).FindRoad(new(0)));
    }

    private static void CityRoadPlanningWaterRailAndEndpoint()
    {
        var f = TerrainGoalFixture(); f.AddCity(1, 1, new(18, 10));
        f.World.Civilizations[1].ContinentCities[1] = 2;
        f.World.Map.TileAt(new(12, 10)).River = true;
        Equal(true, TerrainGoals(f).FindRoad(new(0))); Equal(new MapPoint(14, 10), f.World.Calculation.JobPosition);
        f.World.Civilizations[1].Advances = default(AdvanceSet).With(AdvanceIds.BridgeBuilding);
        Equal(true, TerrainGoals(f).FindRoad(new(0))); Equal(new MapPoint(12, 10), f.World.Calculation.JobPosition);
        f.World.Civilizations[1].Advances = default(AdvanceSet).With(AdvanceIds.Railroad);
        foreach (int x in new[] { 12, 14, 16 }) f.World.Map.TileAt(new(x, 10)).Features |= MapFeatures.Road;
        Equal(true, TerrainGoals(f).FindRoad(new(0))); Equal(new MapPoint(12, 10), f.World.Calculation.JobPosition);
        foreach (int x in new[] { 12, 14, 16 }) f.World.Map.TileAt(new(x, 10)).Features |= MapFeatures.Railroad;
        Equal(false, TerrainGoals(f).FindRoad(new(0))); // Destination city is skipped before infrastructure checks.
        Equal(false, f.World.Paths.UniformCost); Equal(-1, f.World.Paths.Owner);
        Equal(UnitTypeIds.Warriors, f.World.Paths.SelectedUnitType); Equal(new MapPoint(18, 10), f.World.Paths.Goal);
    }

    private static void CityRoadPlanningFirstJobAndRetainedState()
    {
        foreach (bool occupant in new[] { false, true })
        {
            var f = TerrainGoalFixture(); f.AddCity(1, 1, new(18, 10));
            f.World.Civilizations[1].ContinentCities[1] = 2;
            if (occupant) f.AddUnit(0, 2, 0, new(12, 10));
            else
            {
                f.World.Map.TileAt(new(12, 10)).Claimant = 2;
                f.World.Civilizations[1].Treaties[2].Peace = true;
            }
            var work = f.World.Calculation;
            work.JobFound = true; work.JobPosition = new(4, 4); work.JobPriority = 17;
            f.World.Paths.RoadsOnly = true;
            Equal(false, TerrainGoals(f).FindRoad(new(0)));
            Equal(new MapPoint(4, 4), work.JobPosition); Equal(17, work.JobPriority); Equal(true, work.JobFound);
            Equal(true, f.World.Paths.RoadsOnly); Equal(false, f.World.Paths.UniformCost);
            // Neither candidate/pass resumes beyond the rejected first job at (12,10).
            if (occupant) f.World.Map.TileAt(new(12, 10)).Owner = 1;
            else f.World.Civilizations[1].Treaties[2].Alliance = true;
            Equal(true, TerrainGoals(f).FindRoad(new(0))); Equal(new MapPoint(12, 10), work.JobPosition);
        }
    }

    private static void CityTerrainCalculationAndBoundary()
    {
        var f = WorkingFixture(); var work = f.World.Calculation;
        f.World.Entities.Cities.Storage[0].Size = 1;
        f.World.Entities.Cities.Storage[0].WorkedTiles = default(CityWorkSet).With(20).With(0);
        work.Processing = work.Reallocating = true;
        Economy(f).Calculate(new(0)); work.Reallocating = false;
        Equal(true, work.JobFound); Equal(new MapPoint(9, 5), work.JobPosition);
        TerrainGoals(f).Publish(new(0));
        Equal((sbyte)1, CurrentTerrainGoals(f).Single().Priority);
        Equal(true, f.World.Entities.Cities.Storage[0].Flags.TerrainWorkGoal);

        f = TerrainGoalFixture(); f.World.Civilizations[1].Advances = default(AdvanceSet).With(AdvanceIds.Railroad);
        f.World.Map.TileAt(new(10, 10)).Terrain = Terrain.Ocean;
        f.World.Paths.Owner = 7; f.World.Paths.UniformCost = true;
        Throws<InvalidOperationException>(() => TerrainGoals(f).Publish(new(0)));
        Equal(3, f.World.Calculation.JobPriority); Equal(7, f.World.Paths.Owner); Equal(true, f.World.Paths.UniformCost);
    }

    public static (byte[] Initial, byte[] Completed, byte[] Continued) LocalCityTerrainGoalReference() =>
        PlayCityTerrainGoals(false, 0);

    private static (byte[] Initial, byte[] Completed, byte[] Continued) PlayCityTerrainGoals(bool console, short incomingScore)
    {
        using var game = new LocalGame();
        var pioneer = game.Player.OwnUnits().Records.Single(u => u.Type.Value == 0);
        RunLocal(game, console, $"city.found unit={pioneer.Slot.Value}:{pioneer.Token.Value} name=Jobs", LocalPlayCommands.Found,
            new("unit", new CommandValue.Unit(new(pioneer.Slot, pioneer.Token))), new("name", new CommandValue.Text("Jobs")));
        var saved = ClassicFiles.DecodeStoredSave(game.SaveClassic());
        var cities = saved.Cities.ToArray(); var city = StateConversion.ToLive(cities[0]);
        city.Flags.SettlerNearby = city.Flags.Investigated = city.Flags.Airlifted = true;
        city.Founder = new(2); city.TurnsCaptured = 3;
        cities[0] = StateConversion.ToStored(city);
        var civilizations = saved.Civilizations.ToArray(); var civ = StateConversion.ToLive(civilizations[1]);
        foreach (ref var score in (Span<short>)civ.GovernmentScore) score = incomingScore;
        civilizations[1] = StateConversion.ToStored(civ);
        var tiles = saved.Map.Tiles.ToArray(); var shape = new Geometry(saved.Map.Header.Width, saved.Map.Header.Height);
        foreach (var point in new[] { new MapPoint(9, 7), new(7, 7) })
            tiles[shape.TileIndex(point)!.Value].Features |= StateConversion.EncodeMapFeatures(MapFeatures.Pollution);
        saved = saved with
        {
            Cities = new(cities),
            Civilizations = new(civilizations),
            Map = saved.Map with { Tiles = new(tiles) },
            Globals = saved.Globals with { PollutedTiles = 2 }
        };
        var initial = ClassicFiles.EncodeStoredSave(saved);
        Equal(LocalLoadResult.Loaded, game.LoadClassic(initial));
        EndLocalTurn(game, console, 2);
        var bytes = game.SaveClassic(); var result = ClassicFiles.DecodeStoredSave(bytes);
        city = StateConversion.ToLive(result.Cities[0]); civ = StateConversion.ToLive(result.Civilizations[1]);
        Equal(true, city.Flags.TerrainWorkGoal); Equal(false, city.Flags.Investigated || city.Flags.Airlifted);
        Equal(new CivilizationSlot(1), city.Founder);
        var goals = ((ReadOnlySpan<CivilizationGoal>)civ.Goals).ToArray().Where(g => g.Role == 21).ToArray();
        Equal(2, goals.Length); Equal(true, goals.All(g => g.Priority == -6)); // Real post-city A01 aging.
        Equal((short)(2 * civ.GovernmentScore[(int)Government.Despotism]), civ.GovernmentScore[(int)Government.Anarchy]);
        Equal(LocalLoadResult.Loaded, game.LoadClassic(bytes));
        EndLocalTurn(game, console, 3);
        return (initial, bytes, game.SaveClassic());
    }

    private static void LocalCityTerrainGoalsAndScores()
    {
        var baseline = PlayCityTerrainGoals(false, 0);
        foreach (var (console, score) in new[] { (false, (short)20000), (true, (short)0), (true, (short)20000) })
        {
            var result = PlayCityTerrainGoals(console, score);
            BytesEqual(baseline.Completed, result.Completed);
            BytesEqual(baseline.Continued, result.Continued);
        }
    }
}
