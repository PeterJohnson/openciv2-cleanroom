using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private sealed class FoundingFixture : ICityFoundingEffects
    {
        public EliminationFixture Life { get; }
        public RevelationFixture Data => Life.Data;
        public GameState World => Data.World;
        public CityFounding Founding { get; }
        public List<string> Effects { get; } = [];
        public List<FoundingProblem> Rejections { get; } = [];
        public List<FoundedCity> Notices { get; } = [];
        public List<CityTarget> Removed { get; } = [];
        public bool FailCosmetic, FailFounded, FailRemoval;
        public int CapacityNotices;
        public FoundingFixture(NameCatalog? names = null, List<Choice>? choices = null, RevelationFixture? data = null, UnitCreation? creation = null)
        {
            Life = new(choices, data); World.Civilizations[1].Tribe = 0;
            World.Globals.FirstLaunchTurn = -1; World.Globals.Turn = 1;
            Data.Rules.Cosmic = Data.Rules.Cosmic with { AqueductSize = 8 };
            Data.AddUnit(0, 1, 0, new(10, 10)); World.Civilizations[1].UnitsActive[0] = 1; World.UnitTotals[1] = 1;
            var creator = CityCreator(Data, () => ++CapacityNotices, names, creation);
            var removal = new CityRemoval(World, Data.Rules, Data.Stacks, Life.Loss, city =>
            { Removed.Add(city); if (FailRemoval) throw new InvalidOperationException("Required city removal publication failed"); });
            var incidents = new IncidentFixture(Life.Random, Data);
            World.Globals.Turn = 1;
            Founding = new(World, Data.Process, creator, removal, Life.Elimination, incidents.Incidents, ClassicCalendar, this);
        }
        private void Cosmetic(string value)
        { Effects.Add(value); if (FailCosmetic) throw new InvalidOperationException("Cosmetic founding failure"); }
        public void StopBlinking() => Cosmetic("blink");
        public void EnsureVisible(MapPoint position, int owner) => Cosmetic("visible:" + owner);
        public void Redraw(MapPoint position, int observer) => Cosmetic("redraw:" + observer);
        public void RefreshCity(CitySlot city) => Cosmetic("refresh:" + city.Value);
        public void Rejected(FoundingProblem problem) => Rejections.Add(problem);
        public void BuildSound() => Cosmetic("sound");
        public void Founded(FoundedCity city)
        { Notices.Add(city); if (FailFounded) throw new InvalidOperationException("Required founding notice failed"); }
        public void FirstProduction(int owner, CitySlot city) => Effects.Add("tutorial:" + owner);
        public CityFounding.Operation Player() => Founding.BeginPlayer(new(0), "player");
    }

    // C01 founding: actual creation precedes basic deletion; stack companions survive and memory is not exploration.
    private static void FoundingCreationAndMemory()
    {
        var f = new FoundingFixture(); var p = new MapPoint(10, 10);
        f.Data.AddUnit(1, 1, 4, p); f.Data.Unit(1).Orders = UnitOrder.Sleeping; f.Data.Unit(1).Carrier = new(0);
        f.World.Map.TileAt(p).Features |= MapFeatures.Irrigation | MapFeatures.Mine | MapFeatures.Road | MapFeatures.Railroad | MapFeatures.Fortification | MapFeatures.Pollution;
        f.World.Map.TileAt(p).RevealedTo = CivilizationSet.Only(3);
        using var op = f.Founding.Begin(new(0), "found"); Equal(null, op.Resume());
        Equal(FoundingOutcome.Founded, op.Result!.Value); Equal(new CitySlot(0), op.City!.Value);
        Equal("First", f.World.Entities.Cities.At(new(0)).Name); Equal((short)1, f.World.Civilizations[1].CityCount);
        Equal(false, f.Data.Unit(0).Token.Occupied); Equal(true, f.Data.Unit(1).Token.Occupied); Equal(UnitOrder.None, f.Data.Unit(1).Orders);
        Equal(MapFeatures.CitySite | MapFeatures.Unit | MapFeatures.Pollution, f.World.Map.TileAt(p).Features);
        for (int c = 1; c < 8; ++c) Equal(f.World.Map.TileAt(p).Features, f.World.Map.Remembered(c, p));
        Equal(CivilizationSet.Only(3), f.World.Map.TileAt(p).RevealedTo);
        Equal(0, f.Life.Destructions.Count); Equal(false, f.World.UnitDeletion.Active); f.Life.Random.RequireChoicesConsumed();
    }

    private static void FoundingSuppliedCity()
    {
        foreach (bool occupied in new[] { false, true })
        {
            var f = new FoundingFixture(); f.World.Civilizations[1].CityCount = 1;
            if (occupied) f.Data.AddCity(7, 2, new(20, 10), 9);
            else { f.World.Entities.Cities.At(new(7)).Owner = new(2); f.World.Entities.Cities.At(new(7)).Position = new(20, 10); }
            f.World.Map.TileAt(new(10, 10)).Features |= MapFeatures.Road;
            using var op = f.Founding.Begin(new(0), "found", new(7), "Borrowed"); Equal(null, op.Resume());
            Equal(FoundingOutcome.Founded, op.Result!.Value); Equal("Borrowed", f.World.Entities.Cities.At(new(7)).Name);
            Equal(new MapPoint(20, 10), f.World.Entities.Cities.At(new(7)).Position); Equal(new CivilizationSlot(2), f.World.Entities.Cities.At(new(7)).Owner);
            Equal(occupied, f.World.Entities.Cities.At(new(7)).Token.Occupied); Equal((byte)0, f.Data.Process.Rules.Leaders[0].CityNameIndex);
            Equal(false, f.World.Map.TileAt(new(10, 10)).Features.HasFlag(MapFeatures.Road)); Equal(false, f.Data.Unit(0).Token.Occupied);
        }
    }

    private static void FoundingDirectCustomAndNameBounds()
    {
        var f = new FoundingFixture(); f.Data.Unit(0).Type = new(4); f.World.Map.TileAt(new(10, 10)).Terrain = Terrain.Ocean;
        string name = string.Concat(Enumerable.Repeat("🌲", 15));
        using var allowed = f.Founding.Begin(new(0), "found", name: name); Equal(null, allowed.Resume());
        Equal(FoundingOutcome.Founded, allowed.Result!.Value); Equal(name, f.World.Entities.Cities.At(new(0)).Name); // No player role/ocean gate in the helper.
        var longName = new FoundingFixture(); longName.World.Map.TileAt(new(10, 10)).Features |= MapFeatures.Road;
        using var rejected = longName.Founding.Begin(new(0), "found", name: new string('x', 16));
        Throws<ArgumentOutOfRangeException>(() => rejected.Resume()); Equal((short)1, longName.World.Civilizations[1].CityCount);
        Equal("First", longName.World.Entities.Cities.At(new(0)).Name); Equal(true, longName.Data.Unit(0).Token.Occupied);
        Equal(true, longName.World.Map.TileAt(new(10, 10)).Features.HasFlag(MapFeatures.Road));
        Throws<InvalidOperationException>(() => rejected.Resume());
    }

    private static void FoundingVisibilityAndCosmeticFailure()
    {
        foreach (int reveal in new[] { 0, 1 })
        {
            var f = new FoundingFixture(); f.World.Multiplayer.LocalHuman = 2; f.World.Globals.RevealMap = (byte)reveal;
            using var op = f.Founding.Begin(new(0), "found"); op.Resume();
            Equal(reveal != 0, f.Effects.Contains("visible:1")); Equal(true, f.Effects.Contains("redraw:2"));
        }
        var broken = new FoundingFixture { FailCosmetic = true };
        using var player = broken.Player(); Equal(true, player.Resume() is FoundingDecision.Name);
        Equal(true, player.Resume(new(0, "Kept")) is FoundingDecision.Window);
        Equal(false, broken.Data.Unit(0).Token.Occupied); Equal(4, player.PresentationFailures.Count);
        player.Resume(new(-1)); Equal(FoundingOutcome.Founded, player.Result!.Value); Equal("Kept", broken.World.Entities.Cities.At(new(0)).Name);
    }

    private static void FoundingPlayerAdmission()
    {
        foreach (int variant in Enumerable.Range(0, 3))
        {
            var f = new FoundingFixture();
            if (variant == 0) f.Data.Unit(0).Type = new(4);
            if (variant == 1) f.World.Map.TileAt(new(10, 10)).Terrain = Terrain.Ocean;
            if (variant == 2) { f.Data.AddCity(0, 2, new(11, 9)); f.Data.AddCity(1, 1, new(12, 10)); }
            using var op = f.Player(); Equal(null, op.Resume()); Equal(FoundingOutcome.Unavailable, op.Result!.Value);
            Equal((FoundingRejection)variant, f.Rejections.Single().Reason); Equal(true, f.Data.Unit(0).Token.Occupied);
            if (variant == 2) Equal(new CitySlot(0), f.Rejections[0].City!.Value);
        }
        var airbase = new FoundingFixture(); airbase.World.Map.TileAt(new(11, 9)).Features |= MapFeatures.CitySite | MapFeatures.Fortification;
        airbase.World.Map.TileAt(new(11, 9)).Owner = 2;
        airbase.Data.Rules.Units[20] = airbase.Data.Rules.Units[0]; airbase.Data.Unit(0).Type = new(20); airbase.Data.Unit(0).Orders = UnitOrder.Fortified;
        using var admitted = airbase.Player(); Equal(true, admitted.Resume() is FoundingDecision.Name); Equal(0, airbase.Rejections.Count);
    }

    private static void FoundingPlayerJoin()
    {
        foreach (var (size, limit, accepted, final) in new (sbyte, byte, bool, sbyte)[] { (7, 8, true, 8), (8, 8, false, 8), (127, 128, true, -128), (-1, 0, true, 0) })
        {
            var f = new FoundingFixture(); f.Data.AddCity(0, 2, new(10, 10), size); f.World.Civilizations[1].CityCount = 1;
            f.Data.Rules.Cosmic = f.Data.Rules.Cosmic with { AqueductSize = limit }; f.World.Entities.Cities.At(new(0)).BaseTrade = 777;
            f.World.Map.TileAt(new(10, 10)).Features |= MapFeatures.Road;
            using var op = f.Player(); Equal(null, op.Resume()); Equal(accepted ? FoundingOutcome.Joined : FoundingOutcome.Unavailable, op.Result!.Value);
            Equal(final, f.World.Entities.Cities.At(new(0)).Size); Equal(!accepted, f.Data.Unit(0).Token.Occupied);
            Equal((short)777, f.World.Entities.Cities.At(new(0)).BaseTrade); Equal(true, f.World.Map.TileAt(new(10, 10)).Features.HasFlag(MapFeatures.Road));
            Equal(0, f.Notices.Count); Equal(accepted, f.Effects.Contains("refresh:0"));
            if (!accepted) { Equal(FoundingRejection.PopulationLimit, f.Rejections.Single().Reason); Equal((int)limit, f.Rejections[0].Limit); }
        }
    }

    private static void FoundingNamingAndCityVisit()
    {
        foreach (string name in new[] { "Custom name", "" })
        {
            var f = new FoundingFixture(); f.World.Globals.GameOptions.Tutorial = true; f.World.Calculation.Processing = true;
            using var op = f.Player(); var prompt = (FoundingDecision.Name)op.Resume()!;
            Equal("First", prompt.InitialName); Equal(15, prompt.MaximumLength); Equal(1, prompt.Owner);
            Equal((short)1, f.World.Civilizations[1].CityCount); Equal(true, f.Data.Unit(0).Token.Occupied);
            Equal(prompt, op.Resume()); Equal((byte)1, f.Data.Process.Rules.Leaders[0].CityNameIndex);
            var visit = (FoundingDecision.Window)op.Resume(new(0, name))!;
            Equal(name, visit.Request.Name); Equal(new CitySlot(0), visit.Request.City.Slot); Equal(true, f.World.Calculation.Processing);
            Equal(false, f.Data.Unit(0).Token.Occupied); Equal(-4000, f.Notices.Single().Year); Equal(name, f.Notices[0].Name);
            Equal(true, f.Effects.Contains("tutorial:1")); Equal(visit, op.Resume()); Equal(null, op.Result);
            op.Resume(new(-1)); Equal(false, f.World.Calculation.Processing); Equal(FoundingOutcome.Founded, op.Result!.Value);
            int count = f.Effects.Count; op.Resume(new(0)); Equal(count, f.Effects.Count);
        }
    }

    private static void FoundingCancellationEffects()
    {
        foreach (bool exhaustedNames in new[] { false, true })
        {
            var f = new FoundingFixture(exhaustedNames ? NamingCatalog([]) : null);
            if (exhaustedNames) f.Data.Process.Rules.Leaders[0].CityNameIndex = 5;
            using var op = f.Player(); var prompt = (FoundingDecision.Name)op.Resume()!;
            uint nextToken = f.Data.Process.Tokens.NextCity;
            Equal(null, op.Resume(new(1))); Equal(FoundingOutcome.Cancelled, op.Result!.Value);
            Equal(false, f.World.Entities.Cities.At(prompt.City.Slot).Token.Occupied); Equal(true, f.Data.Unit(0).Token.Occupied);
            Equal((short)0, f.World.Civilizations[1].CityCount); Equal(1, f.Removed.Count); Equal(nextToken, f.Data.Process.Tokens.NextCity);
            Equal((short)10, f.World.Civilizations[1].CapitalX); // Creation's committed capital summary is not rolled back.
            Equal(exhaustedNames ? (byte)255 : (byte)0, f.Data.Process.Rules.Leaders[0].CityNameIndex);
            Equal(false, f.World.CityDeletionActive); Equal(0, f.Notices.Count);
        }
    }

    private static void FoundingCancellationSupportedUnits()
    {
        var f = new FoundingFixture(); f.Data.Unit(0).Home = new(0); f.Data.AddUnit(1, 1, 0, new(20, 10));
        f.World.Civilizations[1].UnitsActive[0] = 2;
        using var op = f.Player(); Equal(true, op.Resume() is FoundingDecision.Name); op.Resume(new(-1));
        Equal(FoundingOutcome.Cancelled, op.Result!.Value); Equal(false, f.Data.Unit(0).Token.Occupied); Equal(true, f.Data.Unit(1).Token.Occupied);
        Equal((byte)1, f.World.Civilizations[1].UnitsActive[0]); Equal(0, f.Life.Destructions.Count); Equal(1, f.Removed.Count);
    }

    private static void FoundingNestedLifecycle()
    {
        var f = new FoundingFixture(choices: [EscapeChoice("found/delete/unit/0/check/spy/1", 2, 0)]);
        EliminationSpySetup(f.Life); f.Data.Unit(0).Type = new(0); f.World.Civilizations[2].UnitsActive[0] = 1;
        f.Data.AddCity(0, 3, new(10, 10)); var origin = f.Data.Unit(0).Position;
        f.World.Map.TileAt(origin).Features |= MapFeatures.Road;
        using var op = f.Founding.Begin(new(0), "found", new(0), "Retained");
        var pending = (FoundingDecision.Lifecycle)op.Resume()!;
        Equal(2, ((EspionagePretext)pending.Decision).Victim.Civilization); Equal(false, f.Data.Unit(0).Token.Occupied);
        Equal(true, f.World.Map.TileAt(origin).Features.HasFlag(MapFeatures.Road)); Equal("Retained", f.World.Entities.Cities.At(new(0)).Name);
        Equal(pending, op.Resume()); Equal(0, f.Life.Destructions.Count); // Killer -1 resets instead of recording conquest.
        Equal(false, f.Data.Unit(1).Token.Occupied); Equal(false, f.World.Civilizations[2].SpaceshipFlags.Exists);
        Equal(null, op.Resume(new(0)));
        Equal(FoundingOutcome.Founded, op.Result!.Value); Equal(false, f.World.Map.TileAt(origin).Features.HasFlag(MapFeatures.Road));
        Equal(false, f.World.UnitDeletion.Active); f.Life.Random.RequireChoicesConsumed();
    }

    private static void FoundingFailureAndDisposal()
    {
        var f = new FoundingFixture { FailFounded = true }; using var op = f.Player(); op.Resume();
        Throws<InvalidOperationException>(() => op.Resume(new(0, "Committed"))); Equal(false, f.Data.Unit(0).Token.Occupied);
        Equal("Committed", f.World.Entities.Cities.At(new(0)).Name); Equal(false, f.World.Calculation.Processing);
        Throws<InvalidOperationException>(() => op.Resume(new(0))); Equal(1, f.Notices.Count);
        var removal = new FoundingFixture { FailRemoval = true }; using var cancel = removal.Player(); cancel.Resume();
        Throws<InvalidOperationException>(() => cancel.Resume(new(1))); Equal(false, removal.World.Entities.Cities.At(new(0)).Token.Occupied);
        Equal((byte)1, removal.Data.Process.Rules.Leaders[0].CityNameIndex); // Decrement is after successful removal, not a finally action.
        var disposed = new FoundingFixture(); var pending = disposed.Player(); pending.Resume(); pending.Dispose();
        Equal(true, disposed.World.Entities.Cities.At(new(0)).Token.Occupied); Equal(true, disposed.Data.Unit(0).Token.Occupied);
        Throws<ObjectDisposedException>(() => pending.Resume(new(1)));
    }

    private static void FoundingCapacityAndAuthority()
    {
        foreach (bool player in new[] { false, true })
        {
            var f = new FoundingFixture(); f.World.Entities.Cities.SetEnd(EntityLimits.CreatableCities);
            for (int i = 0; i < EntityLimits.CreatableCities; ++i) f.World.Entities.Cities.Storage[i].Token = new((uint)i + 1);
            using var op = player ? f.Player() : f.Founding.Begin(new(0), "found"); op.Resume();
            Equal(FoundingOutcome.Unavailable, op.Result!.Value); Equal(1, f.CapacityNotices); Equal(true, f.Data.Unit(0).Token.Occupied);
        }
        var remote = new FoundingFixture(); remote.World.Globals.Multiplayer = GameMode.Tcp;
        using var denied = remote.Player(); Throws<InvalidOperationException>(() => denied.Resume());
        Equal(0, remote.Effects.Count); Equal((short)0, remote.World.Civilizations[1].CityCount);
    }
}
