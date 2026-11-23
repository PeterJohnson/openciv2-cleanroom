using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private sealed class CityPollutionFixture : ICityPollutionEffects
    {
        public NuclearFixture Nuclear { get; }
        public RevelationFixture Data => Nuclear.Data;
        public GameState World => Data.World;
        public CityPollution Pollution { get; }
        public List<CityPollutionNotice> Notices { get; } = [];
        public List<MapPoint> Centers { get; } = [];
        public List<(MapPoint, int)> Visible { get; } = [];
        public List<MapPoint> Animations { get; } = [];
        public List<string> Events { get; } = [];
        public bool FailPresentation;
        public CityPollutionFixture(List<Choice> choices, RevelationFixture? data = null)
        {
            Nuclear = new(choices, data); Data.AddCity(0, 1, new(10, 10), 9);
            World.Globals.Difficulty = 0; World.Civilizations[1].NumAdvances = 0;
            Nuclear.OnRedraw = _ => Events.Add("blast");
            Pollution = new(World, Nuclear.Lifecycle.Random, Nuclear.Nuclear, this);
        }
        public IReadOnlyList<Exception> Run() { var result = Pollution.Run(new(0), "pollution"); Nuclear.Lifecycle.Random.RequireChoicesConsumed(); return result; }
        public void CenterPrimary(MapPoint p) { Centers.Add(p); Event("center"); }
        public void EnsureVisible(MapPoint p, int owner) { Visible.Add((p, owner)); Event("visible"); }
        public void AnimateNuclear(MapPoint p) { Animations.Add(p); Event("animation"); }
        public void Notice(CityPollutionNotice notice) { Notices.Add(notice); Event(notice.Kind.ToString()); }
        private void Event(string name) { Events.Add(name); if (FailPresentation) throw new InvalidOperationException("Presentation unavailable"); }
        public void Reactor(bool disorder = true)
        { World.Entities.Cities.Storage[0].Flags.Disorder = disorder; BuildingQueries.SetBuilding(World, new(0), BuildingIds.NuclearPlant, true); }
    }
    private static Choice CityPollutionChoice(string site, int bound, int value = 0) => Choice.Create(new("W05.city-" + site, "pollution"), new SamplingLaw.PrimaryModulo(bound), ChoiceCode.Primary(value));
    private static Choice MeltdownBlastChoice(string site, int index, int value) => Choice.Create(new("U07.blast." + site, "pollution/meltdown", index), new SamplingLaw.PrimaryModulo(site == "pollution" ? 3 : 2), ChoiceCode.Primary(value));

    private static void CityPollutionRiskLaws()
    {
        foreach (var (advances, difficulty, solar, denominator) in new[] { (0, 0, false, 256), (2, 3, false, 253),
            (102, 5, false, 0), (204, 5, true, 0), (128, 4, true, 128), (255, 255, true, 2), (255, 255, false, 0) })
        {
            List<Choice> choices = denominator == 0 ? [] : [CityPollutionChoice("risk", denominator)];
            var f = new CityPollutionFixture(choices); f.World.Civilizations[1].NumAdvances = (byte)advances;
            f.World.Globals.Difficulty = (Difficulty)difficulty; BuildingQueries.SetBuilding(f.World, new(0), BuildingIds.SolarPlant, solar);
            f.Run(); Equal((short)0, f.World.Globals.PollutedTiles); Equal(0, f.Animations.Count);
        }
    }
    private static void CityPollutionScenarioAndThreshold()
    {
        foreach (bool scenario in new[] { false, true }) foreach (bool suppress in new[] { false, true })
        {
            bool disabled = scenario && suppress;
            var f = new CityPollutionFixture([CityPollutionChoice("risk", disabled ? 256 : 251, 10)]);
            f.World.Civilizations[1].NumAdvances = 10; f.World.Globals.Difficulty = (Difficulty)1;
            f.World.Globals.Status.Scenario = scenario; f.World.Scenario.Flags.NoPollution = suppress; f.World.Calculation.Pollution = 5;
            f.Run(); Equal(disabled ? 0 : 5, f.World.Calculation.Pollution); Equal((short)0, f.World.Globals.PollutedTiles);
        }
        foreach (int intensity in new[] { 0, -1, int.MinValue, int.MaxValue })
        { var f = new CityPollutionFixture([CityPollutionChoice("risk", 256)]); f.World.Calculation.Pollution = intensity; f.Run(); Equal((short)0, f.World.Globals.PollutedTiles); }
    }
    private static void CityPollutionRadiusSelection()
    {
        MapPoint[] offsets = [new(1,-1),new(2,0),new(1,1),new(0,2),new(-1,1),new(-2,0),new(-1,-1),new(0,-2),
            new(2,-2),new(2,2),new(-2,2),new(-2,-2),new(1,-3),new(3,-1),new(3,1),new(1,3),new(-1,3),new(-3,1),new(-3,-1),new(-1,-3)];
        // W01 section 3's B[0..19], excluding its center at 20.
        for (int i = 0; i < offsets.Length; ++i)
        {
            var f = new CityPollutionFixture([CityPollutionChoice("risk", 256, 1), CityPollutionChoice("tile", 20, i)]);
            var p = new MapPoint(10 + offsets[i].X, 10 + offsets[i].Y); f.World.Calculation.Pollution = 1;
            f.World.Map.TileAt(p).Terrain = Terrain.Grassland; f.World.Map.TileAt(p).Features = MapFeatures.Unit | MapFeatures.Road;
            f.World.Map.TileAt(p).Owner = 7; f.Run(); Equal((short)1, f.World.Globals.PollutedTiles);
            Equal(MapFeatures.Unit | MapFeatures.Road | MapFeatures.Pollution, f.World.Map.TileAt(p).Features);
            for (int c = 1; c < 8; ++c) Equal(f.World.Map.TileAt(p).Features, f.World.Map.Remembered(c, p));
            Equal(0, f.Centers.Count); Equal(0, f.Notices.Count);
        }
    }
    private static void CityPollutionRejectedCandidate()
    {
        foreach (int reject in new[] { 0, 1, 2, 3, 4 })
        {
            var f = new CityPollutionFixture([CityPollutionChoice("risk", 256), CityPollutionChoice("tile", 20)]);
            f.World.Calculation.Pollution = 10; var p = new MapPoint(11, 9);
            if (reject != 0) f.World.Map.TileAt(p).Terrain = Terrain.Grassland;
            if (reject == 1) f.World.Map.TileAt(p).Features = MapFeatures.CitySite;
            if (reject == 2) f.World.Map.TileAt(p).Features = MapFeatures.CitySite | MapFeatures.Fortification;
            if (reject == 3) f.World.Map.TileAt(p).Features = MapFeatures.Pollution;
            if (reject == 4) f.World.Entities.Cities.Storage[0].Position = new(0, 0);
            f.Run(); Equal((short)0, f.World.Globals.PollutedTiles); Equal(0, f.Centers.Count);
        }
    }
    private static void CityPollutionWrappingAndNotices()
    {
        foreach (bool suppress in new[] { false, true }) foreach (bool autoplay in new[] { false, true })
        {
            var f = new CityPollutionFixture([CityPollutionChoice("risk", 256), CityPollutionChoice("tile", 20, 5)]);
            f.World.Entities.Cities.Storage[0].Position = new(0, 10); f.World.Globals.Rules.FlatWorld = false;
            var p = new MapPoint(38, 10); f.World.Map.TileAt(p).Terrain = Terrain.Plains; f.World.Map.TileAt(p).RevealedTo = CivilizationSet.Only(1);
            f.World.Globals.MessageOptions.Pollution = suppress; f.World.Multiplayer.Saved.AutoplayActive = autoplay;
            f.World.Calculation.Pollution = 1; f.World.Globals.PollutedTiles = short.MaxValue;
            f.Run(); Equal(short.MinValue, f.World.Globals.PollutedTiles); Equal(p, f.Centers.Single()); Equal(suppress ? 0 : 1, f.Notices.Count);
        }
    }
    private static void CityMeltdownGates()
    {
        foreach (int gate in new[] { 0, 1, 2 })
        {
            var f = new CityPollutionFixture([CityPollutionChoice("risk", 256)]); f.Reactor(gate != 0);
            if (gate == 1) BuildingQueries.SetBuilding(f.World, new(0), BuildingIds.NuclearPlant, false);
            if (gate == 2) f.World.Civilizations[1].Advances = default(AdvanceSet).With(AdvanceIds.FusionPower);
            f.Run(); Equal(0, f.Animations.Count); Equal((sbyte)9, f.World.Entities.Cities.Storage[0].Size);
        }
    }
    private static void CityMeltdownSampling()
    {
        foreach (int difficulty in new[] { 0, 1, 2, 3, 4, 5, 255 }) foreach (bool melt in new[] { false, true })
        {
            if (difficulty >= 5 && !melt) continue;
            List<Choice> choices = [CityPollutionChoice("risk", 256)];
            if (difficulty < 5) choices.Add(CityPollutionChoice("meltdown", 6 - difficulty, melt ? 0 : 1));
            var f = new CityPollutionFixture(choices); f.Reactor(); f.World.Globals.Difficulty = (Difficulty)difficulty;
            f.Run(); Equal((sbyte)(melt ? 5 : 9), f.World.Entities.Cities.Storage[0].Size);
            Equal(!melt, BuildingQueries.HasBuilding(f.World, new(0), BuildingIds.NuclearPlant)); Equal(melt ? 1 : 0, f.Animations.Count);
            Equal(0, f.Visible.Count); // Invisible cities still melt down.
        }
    }
    private static void CityMeltdownActualBlast()
    {
        List<Choice> choices = [CityPollutionChoice("risk", 256), CityPollutionChoice("tile", 20, 8),
            MeltdownBlastChoice("railroad", 0, 1), MeltdownBlastChoice("mine", 0, 0), MeltdownBlastChoice("irrigation", 0, 1), MeltdownBlastChoice("pollution", 0, 1)];
        var f = new CityPollutionFixture(choices); f.Reactor(); f.World.Globals.Difficulty = (Difficulty)5; f.World.Calculation.Pollution = 1;
        var ordinary = new MapPoint(12, 8); var fallout = new MapPoint(11, 9); var center = new MapPoint(10, 10);
        foreach (var p in new[] { ordinary, fallout }) { f.World.Map.TileAt(p).Terrain = Terrain.Grassland; f.World.Map.TileAt(p).RevealedTo = CivilizationSet.Only(1); }
        f.World.Map.TileAt(fallout).Features = MapFeatures.Railroad | MapFeatures.Irrigation;
        f.World.Map.TileAt(center).RevealedTo = CivilizationSet.Only(1); f.Data.AddUnit(0, 2, 4, fallout); f.Data.AddUnit(1, 1, 4, center);
        BuildingQueries.SetBuilding(f.World, new(0), BuildingIds.SdiDefense, true); f.World.Civilizations[1].TotalCitySize = 9;
        f.Run(); Equal((short)2, f.World.Globals.PollutedTiles); Equal((sbyte)5, f.World.Entities.Cities.Storage[0].Size);
        Equal(true, f.Data.Unit(0).Token.Occupied && f.Data.Unit(1).Token.Occupied); Equal((short)9, f.World.Civilizations[1].TotalCitySize);
        Equal(false, f.World.Civilizations[1].Treaties[2].Nuked || f.World.Civilizations[2].Treaties[1].Vendetta);
        Equal(MapFeatures.Unit | MapFeatures.Pollution, f.World.Map.TileAt(fallout).Features);
        Sequence(new[] { "center", "Pollution", "visible", "animation", "blast", "blast", "Meltdown" }, f.Events);
        Equal(0, f.Nuclear.Animations); Equal(0, f.Nuclear.Notices.Count); Equal((center, 1), f.Visible.Single());
    }
    private static void CityMeltdownScenarioAutoplay()
    {
        foreach (bool autoplay in new[] { false, true })
        {
            var f = new CityPollutionFixture([CityPollutionChoice("risk", 256)]); f.Reactor(); f.World.Globals.Difficulty = (Difficulty)5;
            f.World.Globals.Status.Scenario = true; f.World.Scenario.Flags.NoPollution = true; f.World.Calculation.Pollution = 100;
            f.World.Multiplayer.Saved.AutoplayActive = autoplay; f.World.Globals.MessageOptions.Pollution = true;
            f.Run(); Equal((sbyte)5, f.World.Entities.Cities.Storage[0].Size); Equal(0, f.World.Calculation.Pollution);
            Equal(autoplay ? 0 : 1, f.Notices.Count); Equal(1, f.Animations.Count);
        }
    }
    private static void CityMeltdownRestoredMood()
    {
        var mood = new GovernmentMoodFixture(); var f = new CityPollutionFixture([CityPollutionChoice("risk", 256)], mood.Data);
        f.Reactor(); f.World.Globals.Difficulty = (Difficulty)5; f.World.Mood.Happy = 1; f.World.Mood.Unhappy = 0;
        mood.Mood.Run(new(0), 0); Equal(false, f.World.Entities.Cities.Storage[0].Flags.Disorder);
        f.Run(); Equal(0, f.Animations.Count); Equal(true, BuildingQueries.HasBuilding(f.World, new(0), BuildingIds.NuclearPlant));
    }
    private static void CityPollutionCosmeticFailures()
    {
        var f = new CityPollutionFixture([CityPollutionChoice("risk", 256)]); f.Reactor(); f.World.Globals.Difficulty = (Difficulty)5;
        f.World.Map.TileAt(new(10, 10)).RevealedTo = CivilizationSet.Only(1); f.FailPresentation = true; f.Nuclear.FailRedraw = true;
        Equal(4, f.Run().Count); Equal((sbyte)5, f.World.Entities.Cities.Storage[0].Size);
        Equal(false, BuildingQueries.HasBuilding(f.World, new(0), BuildingIds.NuclearPlant));
        var ordinary = new CityPollutionFixture([CityPollutionChoice("risk", 256), CityPollutionChoice("tile", 20)]);
        ordinary.World.Calculation.Pollution = 1; ordinary.World.Map.TileAt(new(11, 9)).Terrain = Terrain.Grassland;
        ordinary.World.Map.TileAt(new(11, 9)).RevealedTo = CivilizationSet.Only(1); ordinary.FailPresentation = true;
        Equal(2, ordinary.Run().Count); Equal((short)1, ordinary.World.Globals.PollutedTiles);
    }
    private static void CityPollutionRequiredFailures()
    {
        var f = new CityPollutionFixture([CityPollutionChoice("risk", 256), CityPollutionChoice("tile", 20, 8)]);
        f.Reactor(); f.World.Globals.Difficulty = (Difficulty)5; f.World.Calculation.Pollution = 1;
        f.World.Map.TileAt(new(12, 8)).Terrain = f.World.Map.TileAt(new(11, 9)).Terrain = Terrain.Grassland;
        f.World.Map.TileAt(new(11, 9)).Features = MapFeatures.Fortification;
        Throws<ChoiceException>(() => f.Pollution.Run(new(0), "pollution")); Equal((short)1, f.World.Globals.PollutedTiles);
        Equal(false, f.World.Map.TileAt(new(11, 9)).Features.HasFlag(MapFeatures.Fortification));
        Equal((sbyte)9, f.World.Entities.Cities.Storage[0].Size); Equal(true, BuildingQueries.HasBuilding(f.World, new(0), BuildingIds.NuclearPlant));
        var remote = new CityPollutionFixture([]); remote.World.Globals.Multiplayer = GameMode.Tcp; remote.World.Calculation.Pollution = 8;
        Throws<InvalidOperationException>(() => remote.Pollution.Run(new(0), "pollution")); Equal(8, remote.World.Calculation.Pollution);
    }
}
