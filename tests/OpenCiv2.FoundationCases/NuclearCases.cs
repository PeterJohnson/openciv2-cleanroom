using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private sealed class NuclearFixture : INuclearEffects
    {
        public EliminationFixture Lifecycle { get; }
        public RevelationFixture Data => Lifecycle.Data;
        public GameState World => Data.World;
        public IncidentFixture Incidents { get; }
        public NuclearEffects Nuclear { get; }
        public List<NuclearNotice> Notices { get; } = [];
        public List<MapPoint> Redraws { get; } = [];
        public int Animations;
        public bool FailAnimation, FailRedraw, FailNotice;
        public Action? OnAnimation;
        public Action<MapPoint>? OnRedraw;
        public NuclearFixture(List<Choice>? choices = null, RevelationFixture? data = null)
        {
            Lifecycle = new(choices, data); Incidents = new(Lifecycle.Random, Data);
            foreach (ref var tile in World.Map.Tiles) tile.Terrain = Terrain.Ocean;
            Nuclear = new(World, Lifecycle.Random, Data.Stacks, Lifecycle.Loss, Lifecycle.Elimination, Incidents.Incidents, this);
        }
        public NuclearEffects.Operation Begin(MapPoint? point = null, bool sdi = true) => Nuclear.Begin(1, point ?? new(10, 10), sdi, "nuke");
        public void Notice(NuclearNotice notice) { Notices.Add(notice); if (FailNotice) throw new InvalidOperationException("Notice failed"); }
        public void Animate(MapPoint point) { ++Animations; if (FailAnimation) throw new InvalidOperationException("Animation failed"); OnAnimation?.Invoke(); }
        public void Redraw(MapPoint point) { Redraws.Add(point); if (FailRedraw) throw new InvalidOperationException("Redraw failed"); OnRedraw?.Invoke(point); }
    }
    private static Choice BlastChoice(string site, int index, int value) => Choice.Create(new("U07.blast." + site, "nuke", index),
        new SamplingLaw.PrimaryModulo(site == "pollution" ? 3 : 2), ChoiceCode.Primary(value));

    // W05 shared placement has no terrain/city restriction and does not explore, redraw or draw.
    private static void PollutionPlacementMemoryAndWrap()
    {
        var f = new NuclearFixture(); var w = f.World; MapPoint p = new(10, 10);
        w.Globals.PollutedTiles = short.MaxValue; w.Map.TileAt(p).Features = MapFeatures.Road;
        PollutionPlacement.Place(w, p); Equal(short.MinValue, w.Globals.PollutedTiles);
        for (int observer = 1; observer < 8; ++observer)
        { Equal(MapFeatures.Road | MapFeatures.Pollution, w.Map.Remembered(observer, p)); Equal(false, w.Map.TileAt(p).RevealedTo.Contains(observer)); }
        w.Map.Remembered(1, p) = MapFeatures.None; PollutionPlacement.Place(w, p);
        Equal(MapFeatures.None, w.Map.Remembered(1, p)); Equal(short.MinValue, w.Globals.PollutedTiles);
        PollutionPlacement.Place(w, new(-1, -1)); Equal(short.MinValue, w.Globals.PollutedTiles);
        f.Data.AddCity(0, 1, new(12, 10)); PollutionPlacement.Place(w, new(12, 10)); Equal((short)-32767, w.Globals.PollutedTiles);
        Equal(0, f.Redraws.Count); f.Lifecycle.Random.RequireChoicesConsumed();
    }

    // U07 city-first signed population, farmland draw branch, airbase preservation and memory after pollution.
    private static void NuclearBlastFeaturesAndPopulation()
    {
        List<Choice> choices = [BlastChoice("railroad", 0, 1), BlastChoice("mine", 0, 1), BlastChoice("pollution", 0, 1),
            BlastChoice("railroad", 1, 0), BlastChoice("mine", 1, 1), BlastChoice("irrigation", 1, 1), BlastChoice("pollution", 1, 0),
            BlastChoice("railroad", 2, 1), BlastChoice("mine", 2, 0), BlastChoice("irrigation", 2, 0), BlastChoice("pollution", 2, 2)];
        var f = new NuclearFixture(choices); var w = f.World;
        MapPoint farm = new(11, 9), airbase = new(12, 10), empty = new(11, 11), center = new(10, 10);
        foreach (var p in new[] { farm, airbase, empty }) w.Map.TileAt(p).Terrain = Terrain.Grassland;
        w.Map.TileAt(farm).Features = MapFeatures.Road | MapFeatures.Railroad | MapFeatures.Mine | MapFeatures.Irrigation | MapFeatures.Fortification;
        w.Map.TileAt(airbase).Features = MapFeatures.CitySite | MapFeatures.Fortification | MapFeatures.Railroad;
        w.Map.TileAt(empty).Features = MapFeatures.Pollution; // Still consumes all four draws; does not increment L.
        f.Data.AddCity(0, 2, center, 5); f.Data.AddUnit(0, 2, 4, center);
        w.Civilizations[2].TotalCitySize = 5; w.Entities.Cities.Storage[0].RememberedSize[1] = 5;
        f.Nuclear.Blast(center, "nuke"); f.Lifecycle.Random.RequireChoicesConsumed();
        Equal(MapFeatures.Road | MapFeatures.Irrigation | MapFeatures.Pollution, w.Map.TileAt(farm).Features);
        Equal(MapFeatures.CitySite | MapFeatures.Fortification | MapFeatures.Railroad, w.Map.TileAt(airbase).Features);
        Equal((short)1, w.Globals.PollutedTiles); Equal((sbyte)3, w.Entities.Cities.Storage[0].Size);
        Equal((short)5, w.Civilizations[2].TotalCitySize); Equal((byte)5, w.Entities.Cities.Storage[0].RememberedSize[1]);
        Equal(true, f.Data.Unit(0).Token.Occupied); Equal(false, w.Civilizations[1].Treaties[2].Nuked);
        Sequence(new[] { farm, airbase, empty, center }, f.Redraws);
        for (int observer = 1; observer < 8; ++observer) Equal(w.Map.TileAt(farm).Features, w.Map.Remembered(observer, farm));
        foreach (var (size, expected) in new[] { (0, 0), (1, 1), (2, 1), (5, 3), (127, 64), (128, -64), (255, 0) })
        {
            var city = new NuclearFixture(); city.Data.AddCity(0, 1, center, unchecked((sbyte)size));
            city.Nuclear.Blast(center, "nuke");
            Equal((sbyte)expected, city.World.Entities.Cities.Storage[0].Size);
            city.Lifecycle.Random.RequireChoicesConsumed();
        }
    }

    // Wrapped aliases are separate visits. P10's semantic completion precedes the live first-pass lookup.
    private static void NuclearBlastAliasesAndLiveNeighbors()
    {
        var f = new NuclearFixture(); f.World.Map.Allocate(new(2, 6)); f.World.Globals.Rules.FlatWorld = false;
        foreach (ref var tile in f.World.Map.Tiles) tile.Terrain = Terrain.Ocean;
        f.Data.AddCity(0, 1, new(0, 2), 8); f.Nuclear.Blast(new(0, 2), "nuke");
        Equal((sbyte)1, f.World.Entities.Cities.Storage[0].Size); Equal(3, f.Redraws.Count);
        f.Nuclear.Blast(new(0, -1), "nuke"); Equal(3, f.Redraws.Count); f.Lifecycle.Random.RequireChoicesConsumed();
        var live = new NuclearFixture();
        live.Data.AddCity(0, 2, new(10, 10), 9); live.Data.AddUnit(0, 2, 4, new(11, 9));
        live.OnAnimation = () => { Equal((sbyte)9, live.World.Entities.Cities.Storage[0].Size); Equal(true, live.Data.Unit(0).Token.Occupied); };
        using var action = live.Begin(); action.Resume(); Equal(1, action.Result);
        Equal((sbyte)5, live.World.Entities.Cities.Storage[0].Size); Equal(false, live.Data.Unit(0).Token.Occupied);
        live.Lifecycle.Random.RequireChoicesConsumed();
    }

    // First qualifying SDI (including slot zero/third party), strict distance and no autoplay gate.
    private static void NuclearInterceptionAndNotices()
    {
        foreach (bool autoplay in new[] { false, true })
        {
            var f = new NuclearFixture(); var w = f.World; w.Globals.HumanPlayers = CivilizationSet.Only(1).With(2).With(3);
            w.Multiplayer.Saved.AutoplayActive = autoplay;
            f.Data.AddCity(0, 3, new(16, 10)); w.Entities.Cities.Storage[0].Name = "First shield";
            f.Data.AddCity(1, 2, new(10, 10), 9); w.Entities.Cities.Storage[1].Name = "Closer shield";
            BuildingQueries.SetBuilding(w, new(0), BuildingIds.SdiDefense, true); BuildingQueries.SetBuilding(w, new(1), BuildingIds.SdiDefense, true);
            f.Data.AddUnit(0, 2, 4, new(10, 10)); using var op = f.Begin(); Equal(null, op.Resume()); Equal(0, op.Result);
            Sequence(new[] { NuclearNoticeKind.UseWeapons, NuclearNoticeKind.Intercepted, NuclearNoticeKind.Intercepted }, f.Notices.Select(n => n.Kind));
            Sequence(new[] { 2, 3, 1 }, f.Notices.Select(n => n.Recipient)); Equal("First shield", f.Notices[2].Name);
            Equal(true, f.Data.Unit(0).Token.Occupied); Equal((sbyte)9, w.Entities.Cities.Storage[1].Size);
            Equal(0, f.Animations); Equal(0, f.Redraws.Count); op.Resume(); Equal(3, f.Notices.Count); f.Lifecycle.Random.RequireChoicesConsumed();
        }
        foreach (var (owner, distance, check) in new[] { (1, 0, true), (3, 4, true), (3, 0, false) })
        {
            var f = new NuclearFixture(); f.Data.AddCity(0, owner, new(10 + 2 * distance, 10), 6);
            BuildingQueries.SetBuilding(f.World, new(0), BuildingIds.SdiDefense, true);
            using var op = f.Begin(sdi: check); op.Resume(); Equal(1, op.Result); Equal(1, f.Animations);
            f.Lifecycle.Random.RequireChoicesConsumed();
        }
    }

    // All stacks are erased before any population damage. Only first member's owner receives nuclear diplomacy.
    private static void NuclearStacksAndDiplomacy()
    {
        var f = new NuclearFixture(); var w = f.World;
        f.Data.AddCity(0, 2, new(10, 10), 9); f.Data.AddCity(1, 3, new(11, 9), 7);
        f.Data.AddUnit(0, 3, 4, new(11, 9)); f.Data.AddUnit(1, 2, 4, new(11, 9));
        // Imported mixed-owner physical chain; ordinary placement only joins matching owners.
        f.Data.Unit(1).Next = new(0); f.Data.Unit(0).Previous = new(1);
        f.Data.AddUnit(2, 1, 4, new(12, 10)); f.Data.AddUnit(3, 2, 4, new(10, 10));
        f.Data.AddUnit(4, 1, 4, new(22, 10)); // A distant delivering unit belongs to the caller.
        w.Diplomacy.SelectedPair = (2, 1); w.Diplomacy.Attitude = 25; w.Civilizations[2].Treaties[1].Peace = true;
        f.OnAnimation = () => { Equal(true, f.Data.Unit(0).Token.Occupied); Equal((sbyte)9, w.Entities.Cities.Storage[0].Size); };
        f.OnRedraw = _ => { for (int i = 0; i < 4; ++i) Equal(false, f.Data.Unit(i).Token.Occupied); };
        using var op = f.Begin(); op.Resume(); Equal(1, op.Result);
        Equal(true, f.Data.Unit(4).Token.Occupied); Equal(225, w.Diplomacy.Attitude); Equal((byte)100, w.Civilizations[2].Attitude[1]);
        Equal(true, w.Civilizations[2].Treaties[1].Vendetta && w.Civilizations[2].Treaties[1].NuclearThreat && w.Civilizations[1].Treaties[2].Nuked);
        Equal(true, w.Civilizations[2].Treaties[1].Peace); Equal(false, w.Civilizations[2].Treaties[1].War);
        Equal(false, w.Civilizations[3].Treaties[1].Vendetta || w.Civilizations[1].Treaties[1].Nuked);
        Equal((sbyte)5, w.Entities.Cities.Storage[0].Size); Equal((sbyte)4, w.Entities.Cities.Storage[1].Size);
        f.Lifecycle.Random.RequireChoicesConsumed();
        // The action has no up-front bounds rejection: an invalid center can destroy an in-bounds neighbor.
        var edge = new NuclearFixture(); edge.Data.AddUnit(0, 2, 4, new(0, 0));
        using var outside = edge.Begin(new(-2, 0)); outside.Resume(); Equal(1, outside.Result); Equal(false, edge.Data.Unit(0).Token.Occupied);
        Equal(0, edge.Redraws.Count); edge.Lifecycle.Random.RequireChoicesConsumed();
        var hotseat = new NuclearFixture(); hotseat.Data.AddUnit(0, 2, 4, new(10, 10));
        hotseat.World.Globals.Multiplayer = GameMode.Hotseat; hotseat.World.Multiplayer.HotseatParticipants = CivilizationSet.Only(2);
        hotseat.World.Diplomacy.SelectedPair = (2, 1); hotseat.World.Diplomacy.Attitude = 50;
        using var gated = hotseat.Begin(); gated.Resume(); Equal(1, gated.Result);
        Equal((byte)0, hotseat.World.Civilizations[2].Attitude[1]); Equal(150, hotseat.World.Diplomacy.Attitude);
        hotseat.Lifecycle.Random.RequireChoicesConsumed();
    }

    private static NuclearFixture NuclearNestedFixture(RevelationFixture? data = null)
    {
        var f = new NuclearFixture(data: data); var w = f.World;
        f.Data.Rules.Units[46] = f.Data.Rules.Units[4] with { Role = RuleRole.Settle };
        f.Data.AddUnit(0, 1, 46, new(11, 9)); w.Civilizations[1].UnitsActive[46] = 1;
        f.Data.AddCity(0, 2, new(10, 10), 9); w.Globals.HumanPlayers = CivilizationSet.Only(1).With(2);
        f.Data.AddUnit(1, 3, 4, new(24, 10)); f.Data.Unit(1).Flags.SpyMission = true; f.Data.Unit(1).Counter = 1;
        w.Civilizations[1].Treaties[3].Peace = true;
        f.Lifecycle.Recreate = owner => Creation(f.Data, owner, f.Lifecycle.Random);
        return f;
    }

    // Actual last-settler V01 deletion interrupts a foreign mission and waits before the entire blast pass.
    private static void NuclearNestedLifecycleAndFailures()
    {
        var nested = NuclearNestedFixture(); using var op = nested.Begin();
        var pending = op.Resume()!; Equal(1, ((EspionagePretext)pending).Victim.Civilization); Equal(null, op.Result);
        Equal(false, nested.Data.Unit(0).Token.Occupied || nested.Data.Unit(1).Token.Occupied);
        Equal((sbyte)9, nested.World.Entities.Cities.Storage[0].Size); Equal(0, nested.Redraws.Count);
        Equal(pending, op.Resume()); op.Resume(0); Equal(1, op.Result); Equal((sbyte)5, nested.World.Entities.Cities.Storage[0].Size);
        Equal(0, nested.Lifecycle.Checks); Equal(EndReason.Defeat, nested.World.PendingEndReason); nested.Lifecycle.Random.RequireChoicesConsumed();
        foreach (int failure in new[] { 0, 1, 2 })
        {
            var f = new NuclearFixture(); f.Data.AddCity(0, 1, new(10, 10), 9); f.Data.AddUnit(0, 1, 4, new(10, 10));
            f.FailNotice = failure == 0; f.FailAnimation = failure == 1; f.FailRedraw = failure == 2;
            using var failed = f.Begin(); failed.Resume(); Equal(1, failed.Result); Equal(1, failed.PresentationFailures.Count);
            failed.Resume(); Equal(1, failed.PresentationFailures.Count);
            Equal(false, f.Data.Unit(0).Token.Occupied); Equal((sbyte)5, f.World.Entities.Cities.Storage[0].Size);
            Equal(1, f.Animations); f.Lifecycle.Random.RequireChoicesConsumed();
        }
        var denied = new NuclearFixture(); denied.World.Globals.Multiplayer = GameMode.Tcp;
        using var network = denied.Begin(); Throws<InvalidOperationException>(() => network.Resume()); Equal(0, denied.Animations);
        Throws<InvalidOperationException>(() => denied.Nuclear.Blast(new(10, 10), "nuke"));
        Throws<InvalidOperationException>(() => PollutionPlacement.Place(denied.World, new(10, 10)));
        var disposed = NuclearNestedFixture(); var stopped = disposed.Begin(); stopped.Resume(); stopped.Dispose();
        Throws<ObjectDisposedException>(() => stopped.Resume()); Equal((sbyte)9, disposed.World.Entities.Cities.Storage[0].Size);
    }

    private static void NuclearSessionIncidentPrivacy()
    {
        using var runtime = new SessionRuntime(new(), new(196));
        var f = NuclearNestedFixture(CreationFixture(new(runtime.Process, runtime.World)));
        ParticipantId victimSeat = default;
        runtime.RegisterCommand(new(new(1, "test.nuclear", "Authored nuclear delivery", AccessClass.Player, new([]), new([new("result")])),
            (_, _, _) => new(), _ => new NuclearActionOperation(f.Begin(), _ => victimSeat)));
        runtime.BeginClientSession(new()); var actor = SessionClient(runtime, 2); var victim = SessionClient(runtime, 1); victimSeat = victim.Identity.Participant;
        var command = actor.Submit(new("test.nuclear", new([]))).Correlation.Command; runtime.AdvanceOne();
        var interaction = RequiredInteraction(victim, command); Equal(null, RequiredResult(actor, command).Interaction);
        Equal(false, actor.Reply(interaction, 0).Accepted); Equal(true, victim.Reply(interaction, 0).Accepted);
        runtime.AdvanceOne(); Equal(CommandStatus.Completed, RequiredResult(actor, command).Status);
        Equal((sbyte)5, f.World.Entities.Cities.Storage[0].Size); f.Lifecycle.Random.RequireChoicesConsumed();
    }
}
