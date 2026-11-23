using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.State;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private sealed class PopulationFixture : ISettlerPopulationEffects
    {
        public SettlementFixture Setup { get; } = new();
        public RevelationFixture Data => Setup.Data;
        public GameState World => Data.World;
        public SettlerPopulationReturn Policy { get; }
        public List<string> Effects = [];
        public bool FailCosmetic;
        public PopulationFixture(bool away = false)
        {
            Setup.Home(away ? new(14, 10) : Setup.Position); World.Entities.Cities.At(new(0)).Size = 9;
            Data.AddCity(7, 1, new(30, 20)); Data.Unit(0).Home = new(7);
            World.Civilizations[1].CityCount = 2; World.Civilizations[1].ContinentCities[1] = 2;
            World.Map.Regions[1] = new(480, 8, 0, default);
            var life = Setup.Founding.Life;
            var incidents = new IncidentFixture(life.Random, Data);
            Policy = new(World, life.Loss, life.Elimination, incidents.Incidents, this);
        }
        public UnitTacticsContext Context() => new(World, Data.Rules, new(0)) { SettlerKind = true, SettlerType = true };
        public SettlerPopulationReturn.Operation Begin(UnitTacticsContext? c = null) => Policy.Begin(c ?? Context(), "population");
        public void RefreshCity(CitySlot city)
        { Equal(false, Data.Unit(0).Token.Occupied); Effects.Add("city:" + city.Value); if (FailCosmetic) throw new InvalidOperationException("Unavailable city window"); }
        public void Redraw(MapPoint p)
        { Effects.Add("map:" + p.X + "," + p.Y); if (FailCosmetic) throw new InvalidOperationException("Unavailable map"); }
    }

    private static void SettlerPopulationActualAbsorption()
    {
        var f = new PopulationFixture(); f.World.Entities.Cities.At(new(0)).Tax = 77;
        f.Data.AddUnit(1, 1, 4, f.Setup.Position); f.Data.Unit(0).MovesUsed = 1;
        using var op = f.Begin(); Equal(null, op.Resume()); Equal(SpecialUnitRoute.ReturnOne, op.Result!.Value.Route);
        Equal((sbyte)10, f.World.Entities.Cities.At(new(0)).Size); Equal((short)77, f.World.Entities.Cities.At(new(0)).Tax);
        Equal(false, f.Data.Unit(0).Token.Occupied); Equal(true, f.Data.Unit(1).Token.Occupied);
        Sequence(new[] { "city:0", "map:10,10" }, f.Effects); Equal(1, f.Setup.Founding.Life.Redraws.Count);
        Equal(false, f.World.UnitDeletion.DestroyActive); Equal(false, f.World.UnitDeletion.CargoActive);
        f.Setup.Founding.Life.Random.RequireChoicesConsumed(); Equal(null, op.Resume()); Equal(2, f.Effects.Count);
    }

    private static void SettlerPopulationGatesAndSignedCounts()
    {
        foreach (int variant in Enumerable.Range(0, 5))
        {
            var f = new PopulationFixture(); var c = f.Context();
            if (variant == 0) c.SettlerKind = false;
            if (variant == 1) f.Data.Unit(0).Home = null;
            if (variant == 2) f.World.Map.Regions[1] = new(480, 16, 0, default);
            if (variant == 3) f.World.Entities.Cities.At(new(0)).Size = 10;
            if (variant == 4) { f.World.Map.Regions[1] = new(480, -1, 0, default); f.World.Civilizations[1].ContinentCities[1] = 0; }
            using var op = f.Begin(c); Equal(null, op.Resume()); Equal(variant == 4 ? SpecialUnitRoute.ReturnOne : SpecialUnitRoute.Continue, op.Result!.Value.Route);
            Equal(variant != 4, f.Data.Unit(0).Token.Occupied);
        }
        var signed = new PopulationFixture(); signed.World.Entities.Cities.At(new(0)).Size = -128;
        using var wrap = signed.Begin(); Equal(null, wrap.Resume()); Equal((sbyte)-127, signed.World.Entities.Cities.At(new(0)).Size);
    }

    private static void SettlerPopulationReturnDestination()
    {
        var f = new PopulationFixture(true); f.Data.Unit(0).MovesUsed = 1;
        using var op = f.Begin(); Equal(null, op.Resume()); Equal(SpecialUnitRoute.Finish, op.Result!.Value.Route);
        Equal(UnitOrder.GoTo, f.Data.Unit(0).Orders); Equal(new MapPoint(14, 10), f.Data.Unit(0).Destination);
        Equal(AiTaskIds.ReturnForPopulation, f.Data.Unit(0).AiTask); Equal((byte)1, f.Data.Unit(0).MovesUsed);
        Equal(new CitySlot(7), f.Data.Unit(0).Home!.Value); Equal(0, f.Effects.Count);
        var otherRegion = new PopulationFixture(true); otherRegion.World.Map.TileAt(new(14, 10)).Region = 2;
        using var unchanged = otherRegion.Begin(); Equal(null, unchanged.Resume()); Equal(SpecialUnitRoute.Continue, unchanged.Result!.Value.Route);
        Equal(UnitOrder.None, otherRegion.Data.Unit(0).Orders);
    }

    private static void SettlerPopulationCosmeticFailures()
    {
        var f = new PopulationFixture { FailCosmetic = true };
        using var op = f.Begin(); Equal(null, op.Resume()); Equal(SpecialUnitRoute.ReturnOne, op.Result!.Value.Route);
        Equal(2, op.PresentationFailures.Count); Equal(false, f.Data.Unit(0).Token.Occupied);
        Equal((sbyte)10, f.World.Entities.Cities.At(new(0)).Size); Equal(2, f.Effects.Count);
    }

    private static void SettlerPopulationFailurePrefixAndDisposal()
    {
        var f = new PopulationFixture(); var c = f.Context(); f.Data.Unit(0).Type = new(255);
        using var op = f.Begin(c); Throws<IndexOutOfRangeException>(() => op.Resume());
        Equal((sbyte)10, f.World.Entities.Cities.At(new(0)).Size); Equal(true, f.Data.Unit(0).Token.Occupied); Equal(0, f.Effects.Count);
        f.Data.Unit(0).Type = new(0); Throws<InvalidOperationException>(() => op.Resume()); Equal((sbyte)10, f.World.Entities.Cities.At(new(0)).Size);
        var clean = new PopulationFixture(); var disposed = clean.Begin(); disposed.Dispose(); Throws<ObjectDisposedException>(() => disposed.Resume());
        Equal((sbyte)9, clean.World.Entities.Cities.At(new(0)).Size); Equal(true, clean.Data.Unit(0).Token.Occupied);
    }
}
