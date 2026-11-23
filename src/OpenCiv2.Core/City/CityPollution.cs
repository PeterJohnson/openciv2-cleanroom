using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.City;

public enum CityPollutionNoticeKind { Pollution, Meltdown }
public sealed record CityPollutionNotice(CitySlot City, CityPollutionNoticeKind Kind);
public interface ICityPollutionEffects
{
    void CenterPrimary(MapPoint point);
    void EnsureVisible(MapPoint point, int owner);
    void AnimateNuclear(MapPoint point);
    void Notice(CityPollutionNotice notice);
}

/// <summary>W05 ordinary city pollution and reactor meltdown. F04 owns human-city phase
/// admission and prior recalculation; this operation consumes the current C03 workspace.</summary>
public sealed class CityPollution(GameState world, GameplayRandom random, NuclearEffects nuclear, ICityPollutionEffects effects)
{
    public IReadOnlyList<Exception> Run(CitySlot slot, string call)
    {
        MapKnowledge.RequireLocal(world);
        ArgumentException.ThrowIfNullOrEmpty(call);
        List<Exception> failures = [];
        int owner = world.Entities.Cities.At(slot).Owner.Value;
        int risk = (world.Civilizations[owner].NumAdvances * (byte)world.Globals.Difficulty) >> 1;
        if (world.Globals.Status.Scenario && world.Scenario.Flags.NoPollution)
        { risk = 0; world.Calculation.Pollution = 0; }
        if (BuildingQueries.HasBuilding(world, slot, BuildingIds.SolarPlant))
        {
            while (risk > 255)
            {
                risk /= 2;
            }
        }
        int result = risk >= 255 ? 0 : Draw("risk", 256 - risk);
        if (result < unchecked(2 * world.Calculation.Pollution))
        {
            var offset = MapNeighborhoods.City[Draw("tile", 20)];
            var point = MapNeighborhoods.Offset(world.Map.Geometry, world.Entities.Cities.At(slot).Position, offset, world.Globals.Rules.FlatWorld);
            if (world.Map.Geometry.InBounds(point)
                && (world.Map.TileAt(point).Features & (MapFeatures.CitySite | MapFeatures.Pollution)) == 0
                && !world.Map.TileAt(point).Ocean)
            {
                PollutionPlacement.Place(world, point);
                if (world.Map.IsRevealedTo(point, world.Multiplayer.LocalHuman))
                {
                    Present(() => effects.CenterPrimary(point));
                    if (!world.Globals.MessageOptions.Pollution)
                    { Present(() => effects.Notice(new(slot, CityPollutionNoticeKind.Pollution))); }
                }
            }
        }
        if (world.Entities.Cities.At(slot).Flags.Disorder
            && BuildingQueries.HasBuilding(world, slot, BuildingIds.NuclearPlant)
            && !CivilizationKnowledge.KnowsAdvance(world.Civilizations, owner, AdvanceIds.FusionPower))
        {
            int difficulty = (byte)world.Globals.Difficulty;
            if (difficulty >= 5 || Draw("meltdown", 6 - difficulty) == 0)
            {
                var point = world.Entities.Cities.At(slot).Position;
                if (world.Map.IsRevealedTo(point, world.Multiplayer.LocalHuman))
                { Present(() => effects.EnsureVisible(world.Entities.Cities.At(slot).Position, world.Entities.Cities.At(slot).Owner.Value)); }
                Present(() => effects.AnimateNuclear(world.Entities.Cities.At(slot).Position));
                failures.AddRange(nuclear.Blast(world.Entities.Cities.At(slot).Position, call + "/meltdown"));
                BuildingQueries.SetBuilding(world, slot, BuildingIds.NuclearPlant, false);
                if (!world.Multiplayer.Saved.AutoplayActive)
                { Present(() => effects.Notice(new(slot, CityPollutionNoticeKind.Meltdown))); }
            }
        }
        return failures.AsReadOnly();

        int Draw(string site, int bound) => random.PrimaryModulo(new ChoiceKey("W05.city-" + site, call), bound);
        void Present(Action action)
        {
            try
            { action(); }
            catch (Exception error) { failures.Add(error); }
        }
    }
}
