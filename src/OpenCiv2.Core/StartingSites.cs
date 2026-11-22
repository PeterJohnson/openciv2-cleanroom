using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

/// <summary>The F05 search result before V01's hint override, polar admission and placement.</summary>
public readonly record struct StartingSite(MapPoint Position, int Attempts);

/// <summary>F05 §7 resumable search. A human turn-zero search has no invented retry limit.
/// The owning session must retain its world and prevent interleaved gameplay while this search is pending.</summary>
public sealed class StartingSiteSearch
{
    private readonly GameState world;
    private readonly int civilization;
    private readonly int[] capturedCityCounts;
    private readonly GameplayRandom random;
    private readonly string call;
    private int attempts;
    private uint choiceEpoch;

    public StartingSite? Result { get; private set; }

    /// <summary>Copies V01's retained regional counts; subsequent region-summary changes do not replace them.</summary>
    public StartingSiteSearch(
        GameState world, int civilization, ReadOnlySpan<int> capturedCityCounts, GameplayRandom random, string call)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentException.ThrowIfNullOrEmpty(call);
        ArgumentOutOfRangeException.ThrowIfNegative(civilization);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(civilization, EntityLimits.Civilizations);
        if (!world.Map.Allocated || capturedCityCounts.Length != MapLimits.Regions)
        {
            throw new ArgumentException("Starting-site search requires a map and 64 captured region counts.");
        }

        this.world = world;
        this.civilization = civilization;
        this.capturedCityCounts = capturedCityCounts.ToArray();
        this.random = random;
        this.call = call;
    }

    /// <summary>Evaluates up to the requested number of complete candidates, returning null while pending.
    /// A completed result is stable and repeated calls consume no choices.</summary>
    public StartingSite? Advance(int candidateBudget)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(candidateBudget);
        for (int i = 0; i < candidateBudget && Result is null; ++i)
        {
            EvaluateCandidate();
        }

        return Result;
    }

    private int Draw(string coordinate, int bound)
    {
        // F01 choices have nonnegative ordinal keys. Epochs distinguish a full signed-attempt wrap
        // without changing F05's wrapped acceptance counter or imposing a gameplay retry ceiling.
        string identity = choiceEpoch == 0 ? call : call + "/epoch/" + choiceEpoch;
        return bound <= 1 ? 0 : random.PrimaryModulo(
            new ChoiceKey("F05.start-site." + coordinate, identity, attempts & int.MaxValue), bound);
    }

    private void EvaluateCandidate()
    {
        var map = world.Map;
        var geometry = map.Geometry;
        bool flat = world.Globals.Rules.FlatWorld;
        bool insetX = flat && geometry.Width >= 81 && attempts < 1500;
        bool insetY = flat && geometry.Height >= 40 && attempts < 1500;
        int x = insetX ? 8 + Draw("x", geometry.Width - 16) : Draw("x", geometry.Width);
        int y = insetY ? 8 + Draw("y", geometry.Height - 16) : Draw("y", geometry.Height);
        MapPoint point = new(x - x % 2 + y % 2, y);

        var city = CityQueries.Nearest(world, point);
        int distance = city.Slot is null ? 30 : city.Distance;
        if (world.Globals.Turn == 0)
        {
            var units = world.Entities.Units;
            for (int i = 0; i < units.End; ++i)
            {
                if (units.Storage[i].Token.Occupied)
                {
                    distance = Math.Min(distance,
                        geometry.WeightedDistance(point, units.Storage[i].Position, flat));
                }
            }
        }

        int fertility = map.ClaimantOrFertility(point);
        int region = map.TileAt(point).Region;
        int fertileTiles = map.Regions[region].Fertile;
        if ((attempts & int.MaxValue) == int.MaxValue)
        {
            choiceEpoch = unchecked(choiceEpoch + 1);
        }

        attempts = unchecked(attempts + 1);
        if (attempts > 2000 && !(world.Globals.HumanPlayers.Contains(civilization) && world.Globals.Turn == 0))
        {
            Result = new(point, attempts);
            return;
        }

        if (map.TileAt(point).Ocean || fertility < 12 - (attempts >> 5)
            || distance < 16 - (attempts >> 7) || distance < 2 && attempts <= 2000 || distance < 1
            || fertileTiles < 32 - (world.Globals.Turn >> 4) - (attempts >> 6)
            || world.Globals.Turn > 200 && capturedCityCounts[region] != 0
            || map.Hut(point) && attempts <= 2000 || map.CityOrUnitOwner(point) is not null)
        {
            return;
        }

        Result = new(point, attempts);
    }
}

/// <summary>F05 §8 captured start selections and scores, before any compensation mutates the world.</summary>
public sealed record StartingSiteScores(OwnedList<UnitSlot?> Units, OwnedList<int> Scores, int Maximum);

public static class StartingSites
{
    /// <summary>F05 §8 local turn-zero compensation, before accelerated city creation. Scores and
    /// selected units are captured first; real creation/research failures do not invent compensation.</summary>
    public static void Compensate(GameState world, ProcessContext process, UnitCreation creation,
        GameplayRandom random, string call)
    {
        if (world.Globals.Turn != 0 || world.Globals.Multiplayer != GameMode.SinglePlayer
            || world.Entities.Cities.Scanned.ToArray().Any(city => city.Token.Occupied))
        {
            throw new InvalidOperationException("Local start balancing requires turn zero before city creation");
        }
        var captured = Assess(world);
        int[] scores = captured.Scores.ToArray();
        for (int c = 1; c < EntityLimits.Civilizations; ++c)
        {
            if (captured.Units[c] is not { } selected)
            {
                continue;
            }
            int index = c;
            if (captured.Maximum - scores[index] > 3
                || world.Globals.HumanPlayers.Contains(c) && world.Globals.Difficulty == Difficulty.Deity)
            {
                creation.Create(new(0), new((byte)c), world.Entities.Units.At(selected).Position);
                scores[index] += world.Globals.HumanPlayers.Contains(c) && world.Globals.Difficulty == Difficulty.Deity ? 1 : 3;
            }
            for (int i = 0; i < captured.Maximum - scores[index]; ++i)
            {
                Research.CompleteStarting(world, process.Rules, c, random, call + "/civ/" + c + "/advance/" + i);
            }
        }
    }

    /// <summary>V01 §2: a rectangular turn-zero hint replaces the completed search, even if it has
    /// wrong parity, ocean, occupation or polar coordinates. Admission remains the caller's operation.</summary>
    public static StartingSite ApplyHint(GameState world, StartingSite searched, MapPoint hint) =>
        world.Globals.Turn == 0 && world.Map.Geometry.InBounds(hint) ? new(hint, 0) : searched;

    /// <summary>F05 §8: capture one unit per normal civilization, then all scores, with no human/alive filter.
    /// This query neither grants research nor creates bonus Settlers.</summary>
    public static StartingSiteScores Assess(GameState world)
    {
        var map = world.Map;
        var geometry = map.Geometry;
        var selected = new UnitSlot?[EntityLimits.Civilizations];
        var scores = new int[EntityLimits.Civilizations];
        MapPoint center = new(geometry.Width >> 1, geometry.Height >> 1);
        for (int c = 1; c < selected.Length; ++c)
        {
            selected[c] = UnitQueries.Nearest(world, center, c).Slot;
        }

        int maximum = 0;
        for (int c = 1; c < selected.Length; ++c)
        {
            if (selected[c] is not { } slot)
            {
                continue;
            }

            var point = world.Entities.Units.At(slot).Position;
            int region = Math.Clamp((int?)map.LandRegion(point) ?? -1, 0, MapLimits.Regions - 1);
            int tiles = map.Regions[region].Land;
            int distance = 999;
            for (int other = 1; other < selected.Length; ++other)
            {
                if (other != c && selected[other] is { } otherSlot)
                {
                    var otherPoint = world.Entities.Units.At(otherSlot).Position;
                    if (map.LandRegion(otherPoint) == region)
                    {
                        distance = Math.Min(distance,
                            geometry.WeightedDistance(point, otherPoint, world.Globals.Rules.FlatWorld));
                    }
                }
            }

            int score = distance == 999 ? Math.Clamp(tiles / 50, 3, 6)
                : distance <= 9 ? 0 : distance < 20 ? 1 : 2;
            int rivers = 0, grassland = 0;
            for (int i = 0; i <= 20; ++i)
            {
                var neighbor = MapNeighborhoods.Offset(geometry, point, MapNeighborhoods.Radius[i],
                    world.Globals.Rules.FlatWorld);
                if (geometry.InBounds(neighbor))
                {
                    ref readonly var tile = ref map.TileAt(neighbor);
                    rivers += tile.River ? 1 : 0;
                    grassland += tile.Terrain == Terrain.Grassland ? 1 : 0;
                }
            }

            score += rivers >= 4 ? 4 : grassland >= 4 ? 2 : 0;
            int size = tiles / 50;
            score += size is >= 0 and <= 2 ? size : 3;
            scores[c] = score;
            maximum = Math.Max(maximum, score);
        }

        return new(new(selected), new(scores), maximum);
    }
}
