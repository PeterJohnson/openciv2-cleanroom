using OpenCiv2.Base;
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
    private sealed class EliminationFixture : IUnitDeletionEffects, IUnitLossEffects, ISpyEscapeEffects,
        ICivilizationEliminationEffects, ICityFoodContinuations
    {
        public RevelationFixture Data { get; }
        public GameState World => Data.World;
        public GameplayRandom Random { get; }
        public UnitDeletion Deletion { get; }
        public UnitLoss Loss { get; }
        public SpyEscape Escape { get; }
        public CivilizationElimination Elimination { get; }
        public Func<int, CivilizationCreation>? Recreate;
        public List<string> Order { get; } = [];
        public List<SpyNotice> SpyNotices { get; } = [];
        public List<SpaceshipLossNotice> ShipNotices { get; } = [];
        public List<CivilizationDestroyedNotice> Destructions { get; } = [];
        public List<(int Owner, int Victim)> Incidents { get; } = [];
        public List<MapPoint> Redraws { get; } = [];
        public int Restarts, Checks, MapRefreshes;

        public EliminationFixture(List<Choice>? choices = null, RevelationFixture? data = null)
        {
            Data = data ?? CreationFixture();
            Random = new(new ControlledChoices(new(choices ?? [])), "elimination");
            World.Globals.Turn = 20; World.Globals.CivsAlive = CivilizationSet.Only(1);
            World.Names[1] = new() { Tribe = "Alderfolk", Adjective = "Alder" };
            World.Names[2] = new() { Tribe = "Birchfolk", Adjective = "Birch" };
            Data.Rules.Units[4] = Data.Rules.Units[0] with { Role = RuleRole.Attack };
            Data.Rules.Units[47] = Data.Rules.Units[0] with { Role = RuleRole.Diplomacy, Identity = new("agent", "Renamed agent") };
            Deletion = new(World, Data.Rules, new(5_000_000, 64), this);
            Loss = new(World, Data.Rules, Data.Stacks, new(World, Data.Rules, Data.Stacks), Deletion, this);
            Escape = new(World, Data.Rules, Data.Stacks, Loss, Random, (a, b) =>
            {
                Order.Add("incident"); Incidents.Add((a, b));
            }, this);
            Elimination = new(World, Deletion, Escape, owner =>
            {
                ++Restarts;
                return (Recreate ?? throw new InvalidOperationException("Unexpected restart"))(owner);
            }, this);
        }

        public void CheckCivilization(int owner, int killer) => Elimination.Check(owner, killer, "nested/" + Checks++);
        public void ReconsiderProduction(CitySlot city) => throw new InvalidOperationException("No production choice in this case");
        public void UnitRemoved(MapPoint position) => Order.Add("unit:" + position.X);
        public void RefreshHomeCity(int city) => Order.Add("home:" + city);
        public void SynchronizeDeletion(MapPoint position, CitySlot? home) => throw new InvalidOperationException("No transport");
        public void SynchronizeLoss(MapPoint? position) => throw new InvalidOperationException("No transport");
        public void UnitsChangedAt(MapPoint position) => Order.Add("units");
        public void RedrawTile(MapPoint position) { Order.Add("draw"); Redraws.Add(position); }
        public void Redraw(MapPoint position) => RedrawTile(position);
        public void Notice(SpyNotice notice) { SpyNotices.Add(notice); Order.Add("spy:" + notice.Fate); }
        public void Destroyed(CivilizationDestroyedNotice notice) { Destructions.Add(notice); Order.Add("destroyed"); }
        public void SpaceshipLost(SpaceshipLossNotice notice) { ShipNotices.Add(notice); Order.Add("ship"); }
        public void RedrawMaps() { ++MapRefreshes; Order.Add("maps"); }
    }

    private static Choice EscapeChoice(string call, int bound, int value, bool adjust = false) =>
        Choice.Create(new(adjust ? "V06.escape.adjust" : "V06.escape.survive", call, 0),
            new SamplingLaw.PrimaryModulo(bound), ChoiceCode.Primary(value));

    private static void SpyEscapeOddsAndPromotion()
    {
        foreach (var (mode, veteran, bound, roll, adjust, survives) in new[]
        {
            (-1, false, 3, 2, -1, true), (-1, true, 6, 5, -1, true),
            (0, false, 2, 1, -1, true), (0, true, 4, 3, -1, true),
            (1, false, 2, 1, 0, true), (1, false, 1, 0, 1, false),
            (1, true, 2, 1, -1, true), (1, true, 2, 0, -1, false),
            (0, false, 2, 0, -1, false), (-1, false, 3, 0, -1, false)
        })
        {
            List<Choice> choices = [];
            if (adjust >= 0) choices.Add(EscapeChoice("escape", 2, adjust, true));
            if (bound > 1) choices.Add(EscapeChoice("escape", bound, roll));
            var f = new EliminationFixture(choices);
            f.Data.AddUnit(0, 1, 47, new(10, 10)); f.Data.Unit(0).MovesUsed = 254; f.Data.Unit(0).Flags.Veteran = veteran;
            Equal(!survives, f.Escape.Try(0, mode, 2, "escape")); f.Random.RequireChoicesConsumed();
            Equal(survives, f.Data.Unit(0).Token.Occupied);
            Equal(mode < 0 ? (byte)254 : (byte)1, f.Data.Unit(0).MovesUsed); // No city means no relocation or exhaustion.
            Equal(veteran || survives && mode >= 0, f.Data.Unit(0).Flags.Veteran);
            Equal(!survives && mode >= 0 ? 1 : 0, f.Incidents.Count);
            if (survives && mode >= 0)
            {
                Equal(veteran ? SpyFate.VeteranSurvived : SpyFate.Survived, f.SpyNotices.Single().Fate);
                Equal((string?)null, f.SpyNotices.Single().Destination);
            }
            else if (!survives && mode >= 0)
            {
                Sequence(new[] { "spy:Died", "draw", "incident" }, f.Order);
            }
            else Equal(0, f.SpyNotices.Count);
        }
    }

    private static void SpyEscapeRelocationAndGates()
    {
        var f = new EliminationFixture([EscapeChoice("escape", 2, 1)]);
        f.Data.AddCity(0, 1, new(6, 10)); f.Data.AddCity(1, 1, new(14, 10));
        f.World.Entities.Cities.Storage[1].Name = "Later equal city";
        f.Data.AddUnit(0, 1, 47, new(10, 10)); f.Data.AddUnit(1, 1, 4, new(10, 10));
        f.Data.Unit(0).Home = new(0); f.Data.Unit(0).Orders = UnitOrder.Fortified;
        Equal(false, f.Escape.Try(0, 0, 2, "escape"));
        Equal(new MapPoint(14, 10), f.Data.Unit(0).Position); Equal(new MapPoint(10, 10), f.Data.Unit(1).Position);
        Equal((byte)6, f.Data.Unit(0).MovesUsed); Equal((CitySlot?)new CitySlot(0), f.Data.Unit(0).Home);
        Equal(UnitOrder.Fortified, f.Data.Unit(0).Orders); Equal("Later equal city", f.SpyNotices.Single().Destination);
        Sequence(new[] { new MapPoint(10, 10), new MapPoint(14, 10) }, f.Redraws);
        Equal(true, f.World.Map.TileAt(new(10, 10)).Features.HasFlag(MapFeatures.Unit));
        f.Random.RequireChoicesConsumed();
        foreach (var (human, autoplay, local) in new[] { (false, false, 1), (true, true, 1), (true, false, 2) })
        {
            f = new([EscapeChoice("escape", 2, 1)]); f.Data.AddUnit(0, 1, 47, new(10, 10));
            f.World.Globals.HumanPlayers = human ? CivilizationSet.Only(1) : default;
            f.World.Multiplayer.Saved.AutoplayActive = autoplay; f.World.Multiplayer.LocalHuman = local;
            Equal(false, f.Escape.Try(0, 0, 0, "escape"));
            Equal(true, f.Data.Unit(0).Flags.Veteran); Equal(0, f.SpyNotices.Count); f.Random.RequireChoicesConsumed();
        }
    }

    private static void SpyCaptureOrdinaryAndFailure()
    {
        var f = new EliminationFixture(); f.Data.AddUnit(0, 1, 4, new(10, 10));
        f.Data.Rules.Units[4] = f.Data.Rules.Units[4] with { Identity = new("named-spy", "Spy") };
        Equal(true, f.Escape.Try(0, 0, -1, "no-draw"));
        Equal(0, f.Incidents.Count); Equal(0, f.SpyNotices.Count); f.Random.RequireChoicesConsumed();
        f = new([EscapeChoice("capture", 3, 0)]); f.Data.AddUnit(0, 2, 47, new(10, 10));
        f.World.Globals.HumanPlayers = CivilizationSet.Only(2); f.Data.Unit(0).MovesUsed = 9;
        Equal(true, f.Escape.Caught(0, 1, "capture"));
        Equal((byte)9, f.Data.Unit(0).MovesUsed); Equal(0, f.Incidents.Count);
        Equal(SpyFate.Caught, f.SpyNotices.Single().Fate); Equal(new UnitType(47), f.SpyNotices.Single().Type);
        Equal(2, f.SpyNotices.Single().Owner); f.Random.RequireChoicesConsumed(); // No local-owner gate for capture notice.
        f = new([EscapeChoice("capture", 3, 1)]); f.Data.AddUnit(0, 1, 47, new(10, 10));
        f.Data.AddCity(0, 1, new(14, 10)); f.Data.Unit(0).MovesUsed = 9;
        Equal(false, f.Escape.Caught(0, 2, "capture")); Equal((byte)6, f.Data.Unit(0).MovesUsed);
        Equal(false, f.Data.Unit(0).Flags.Veteran); Equal(0, f.SpyNotices.Count); f.Random.RequireChoicesConsumed();
        f = new(); f.Data.AddUnit(0, 1, 47, new(10, 10)); f.World.Globals.Multiplayer = GameMode.Tcp;
        Throws<InvalidOperationException>(() => f.Escape.Try(0, 0, 2, "network"));
        Equal((byte)0, f.Data.Unit(0).MovesUsed); Equal(true, f.Data.Unit(0).Token.Occupied);
    }

    private static void EliminationLastHumanAndRecursion()
    {
        var f = new EliminationFixture(); var world = f.World;
        world.Globals.CivsAlive = CivilizationSet.Only(1).With(2); world.Globals.FirstLaunchTurn = 17;
        ref var civ = ref world.Civilizations[1];
        civ.SpaceshipFlags = new() { Exists = true, Launched = true, Arrived = true, FusionAtLaunch = true, FirstArrivalVictory = true };
        civ.SpaceshipPopulation = 321; civ.SpaceshipLaunchYear = 10; civ.SpaceshipArrivalYear = 40;
        for (int i = 0; i < 6; ++i) civ.SpaceshipParts[i] = (short)(i + 1);
        var passwords = world.Multiplayer.Saved.Passwords.ToArray(); passwords[1] = "secret"; passwords[2] = "other";
        world.Multiplayer.Saved.Passwords = new(passwords); world.Multiplayer.Passwords.Present[1] = true;
        foreach (ref var tile in world.Map.Tiles) tile.RevealedTo = CivilizationSet.Only(1).With(2);
        world.Map.RememberedLayer(1).Fill(MapFeatures.Road);
        f.Data.AddUnit(0, 1, 4, new(10, 10)); f.Data.AddUnit(1, 1, 0, new(12, 10));
        f.Data.AddUnit(2, 2, 4, new(14, 10)); f.Data.Unit(2).Visibility = CivilizationSet.Only(1);
        civ.UnitsActive[0] = 1; civ.UnitsActive[4] = 1; civ.MilitaryCount = 1; world.UnitTotals[1] = 2;
        Equal(0, f.Elimination.Check(1, -1, "loss"));
        Equal(1, f.Checks); Equal(false, f.Data.Unit(0).Token.Occupied || f.Data.Unit(1).Token.Occupied);
        Equal(true, f.Data.Unit(2).Token.Occupied); Equal(3, world.Entities.Units.End); // Foreign tail prevents trimming holes.
        Equal(EndReason.Defeat, world.PendingEndReason); Equal(CivilizationSet.Only(1), world.Globals.HumanPlayers);
        Equal(CivilizationSet.Only(1).With(2), world.Globals.CivsAlive); Equal(0, f.Restarts); Equal(0, f.MapRefreshes);
        Equal("", world.Multiplayer.Saved.Passwords[1]); Equal("other", world.Multiplayer.Saved.Passwords[2]);
        Equal(true, world.Multiplayer.Passwords.Present[1]); // Derived password flags are not silently rebuilt.
        Equal(true, world.Map.Tiles.ToArray().All(t => t.RevealedTo.Contains(1)));
        Equal(MapFeatures.Road, world.Map.Remembered(1, new(10, 10))); Equal(CivilizationSet.Only(1), f.Data.Unit(2).Visibility);
        Equal(default(SpaceshipStatus), civ.SpaceshipFlags); Equal((short)0, civ.SpaceshipPopulation);
        Equal((short)0, civ.SpaceshipLaunchYear); Equal((short)0, civ.SpaceshipArrivalYear);
        Equal(default(Fixed6<short>), civ.SpaceshipParts); Equal((short)17, world.Globals.FirstLaunchTurn);
        Equal((short)0, world.Eliminations.Count); Equal(0, f.ShipNotices.Count); Equal(0, f.Destructions.Count);
        Equal((short)0, civ.MilitaryCount); Equal(0, world.UnitTotals[1]); f.Random.RequireChoicesConsumed();
    }

    private static void EliminationHistoryAndShipLoss()
    {
        foreach (var (launched, exists, autoplay) in new[] { (true, false, false), (false, true, true), (false, false, false) })
        {
            var f = new EliminationFixture(); var world = f.World;
            world.Globals.Turn = -20; world.Civilizations[1].LeaderPortrait = 37;
            world.Civilizations[1].SpaceshipFlags = new() { Launched = launched, Exists = exists };
            world.Civilizations[1].SpaceshipParts[5] = 9; world.Multiplayer.Saved.AutoplayActive = autoplay;
            world.Eliminations.Count = 11; world.Eliminations.Turns[0] = 77;
            Equal(0, f.Elimination.Check(1, 2, "history"));
            Equal((short)12, world.Eliminations.Count); Equal((short)-20, world.Eliminations.Turns[11]);
            Equal((byte)2, world.Eliminations.Killers[11]); Equal((byte)37, world.Eliminations.Portraits[11]);
            Equal("Alderfolk", world.Eliminations.Names[11]); Equal((short)77, world.Eliminations.Turns[0]);
            Equal(autoplay ? 0 : 1, f.Destructions.Count); Equal(launched || exists ? 1 : 0, f.ShipNotices.Count);
            if (f.ShipNotices.Count != 0) Equal(new SpaceshipLossNotice(1, "Alder", launched), f.ShipNotices.Single());
            Equal((short)0, world.Civilizations[1].SpaceshipParts[5]);
            var history = world.Eliminations; world.Globals.Turn = 300;
            f.Elimination.Check(1, 2, "full-history"); Equal(history, world.Eliminations);
        }
        var failed = new EliminationFixture(); failed.World.Eliminations.Count = -1;
        failed.World.Civilizations[1].SpaceshipFlags.Exists = true;
        Throws<IndexOutOfRangeException>(() => failed.Elimination.Check(1, 2, "invalid-history"));
        Equal((short)0, failed.World.Eliminations.Count); Equal(1, failed.Destructions.Count);
        Equal(true, failed.World.Civilizations[1].SpaceshipFlags.Exists); Equal(EndReason.None, failed.World.PendingEndReason);
    }

    private static void EliminationSurvivorAndSpyMission()
    {
        var f = new EliminationFixture();
        f.Data.AddCity(0, 1, new(10, 10)); f.World.Civilizations[1].CityCount = 0;
        f.World.Civilizations[1].SpaceshipFlags.Exists = true;
        Equal(0, f.Elimination.Check(1, 99, "live-city")); // City scan precedes killer reads and does not trust the summary.
        Equal(true, f.World.Civilizations[1].SpaceshipFlags.Exists); Equal(0, f.Order.Count);
        f.World.Globals.Multiplayer = GameMode.Tcp; Equal(0, f.Elimination.Check(0, 99, ""));
        Throws<InvalidOperationException>(() => f.Elimination.Check(1, -1, "network"));
        f = new([EscapeChoice("loss/spy/1", 2, 1)]);
        f.Data.AddCity(0, 2, new(18, 10)); f.Data.AddUnit(0, 1, 4, new(8, 8)); f.Data.AddUnit(1, 2, 47, new(10, 10));
        f.Data.Unit(1).Flags.SpyMission = true; f.Data.Unit(1).Counter = 1;
        f.World.Civilizations[1].Treaties[1].SpyChat = f.World.Civilizations[1].Treaties[2].SpyChat = true;
        Equal(0, f.Elimination.Check(1, -1, "loss"));
        Equal(false, f.Data.Unit(1).Flags.SpyMission); Equal(false, f.World.Civilizations[1].Treaties[1].SpyChat);
        Equal(true, f.World.Civilizations[1].Treaties[2].SpyChat); Equal(true, f.Data.Unit(1).Flags.Veteran);
        Equal(new MapPoint(18, 10), f.Data.Unit(1).Position); Equal((byte)1, f.Data.Unit(1).Counter);
        Equal(0, f.Incidents.Count); Equal(0, f.SpyNotices.Count); f.Random.RequireChoicesConsumed();
    }

    private static void EliminationRealRestartAndExploration()
    {
        foreach (var (restart, enoughSurvivors) in new[] { (true, true), (false, true), (false, false) })
        {
            var choices = CreationAttitudes(); CreationChoice(choices, "gender", 3, 1); CreationSiteChoices(choices);
            var f = new EliminationFixture(); var world = f.World;
            world.Globals.CivsAlive = CivilizationSet.Only(1).With(2).With(3, enoughSurvivors);
            world.Globals.Rules.NoRestart = !restart; f.Data.Process.Rules.Leaders[2].Color = 2;
            foreach (ref var tile in world.Map.Tiles) tile.RevealedTo = CivilizationSet.Only(2).With(3);
            world.Map.RememberedLayer(2).Fill(MapFeatures.Railroad);
            f.Data.AddUnit(0, 2, 4, new(20, 10)); f.Data.AddUnit(1, 3, 4, new(26, 10));
            f.Data.Unit(1).Visibility = CivilizationSet.Only(2);
            var random = CreationRandom(choices);
            f.Recreate = owner => Creation(f.Data, owner, random);
            Equal(1, f.Elimination.Check(2, -1, "restart")); random.RequireChoicesConsumed();
            Equal(1, f.Restarts); Equal(1, f.MapRefreshes); Equal(restart, world.Globals.CivsAlive.Contains(2));
            Equal(!restart && !enoughSurvivors, world.Globals.Status.Won);
            Equal(!restart && !enoughSurvivors ? EndReason.Conquest : EndReason.None, world.PendingEndReason);
            Equal(false, world.Map.TileAt(new(30, 20)).RevealedTo.Contains(2));
            Equal(true, world.Map.TileAt(new(30, 20)).RevealedTo.Contains(3));
            Equal(MapFeatures.Railroad, world.Map.Remembered(2, new(30, 20))); Equal(CivilizationSet.Only(2), f.Data.Unit(1).Visibility);
            if (restart)
            {
                Equal(new UnitType(0), f.Data.Unit(0).Type); Equal(new MapPoint(10, 8), f.Data.Unit(0).Position);
                Equal(true, f.Data.Unit(0).Token.Occupied); Equal((byte)1, world.Civilizations[2].UnitsActive[0]);
                Equal(true, world.Map.TileAt(new(10, 8)).RevealedTo.Contains(2));
            }
            else Equal(false, f.Data.Unit(0).Token.Occupied);
        }
    }

    private static void EliminationHotseatAndHumanRetention()
    {
        foreach (bool hotseat in new[] { true, false })
        {
            var f = new EliminationFixture(); var world = f.World;
            world.Globals.Multiplayer = hotseat ? GameMode.Hotseat : GameMode.SinglePlayer;
            world.Globals.HumanPlayers = world.Globals.CivsAlive = CivilizationSet.Only(1).With(2);
            foreach (ref var tile in world.Map.Tiles) tile.RevealedTo = CivilizationSet.Only(1).With(2);
            var random = CreationRandom(hotseat ? CreationAttitudes() : []); // No eligible AI leader after Hotseat clears human control.
            f.Recreate = owner => Creation(f.Data, owner, random);
            Equal(1, f.Elimination.Check(1, -1, "human")); random.RequireChoicesConsumed();
            Equal(!hotseat, world.Globals.HumanPlayers.Contains(1)); Equal(!hotseat, world.Globals.CivsAlive.Contains(1));
            Equal(EndReason.Defeat, world.PendingEndReason); Equal(1, f.Restarts); Equal(hotseat ? 0 : 1, f.MapRefreshes);
            Equal(false, world.Map.TileAt(new(8, 8)).RevealedTo.Contains(1)); Equal(true, world.Map.TileAt(new(8, 8)).RevealedTo.Contains(2));
        }
    }

    private static void FamineActualEliminationIntegration()
    {
        var f = new EliminationFixture(); f.Data.AddCity(0, 1, new(8, 8), 1);
        f.World.Civilizations[1].CityCount = 1; f.World.Entities.Cities.Storage[0].FoodBox = -1;
        f.Data.AddUnit(0, 1, 4, new(10, 10)); f.Data.Unit(0).Home = new(0);
        f.Data.AddUnit(1, 1, 0, new(12, 10)); // A surviving unhomed settler is removed by the actual V01 check.
        List<CityTarget> removed = []; var effects = new FoodEffects();
        var removal = new CityRemoval(f.World, f.Data.Rules, f.Data.Stacks, f.Loss, removed.Add);
        var food = new CityFood(f.World, f.Data.Rules, removal, f.Loss, f, effects);
        Equal(true, food.Run(new(0))); Equal(EndReason.Defeat, f.World.PendingEndReason);
        Equal(false, f.World.Entities.Cities.Storage[0].Token.Occupied);
        Equal(false, f.Data.Unit(0).Token.Occupied || f.Data.Unit(1).Token.Occupied);
        Equal((short)0, f.World.Civilizations[1].CityCount); Equal((short)-1, f.World.Entities.Cities.Storage[0].FoodBox);
        Equal(1, removed.Count); Equal(0, effects.Redraws); Equal(0, f.Restarts); Equal(2, f.Checks);
    }
}
