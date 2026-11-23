using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;
using OpenCiv2.Formats.Classic;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static List<Choice> TacticsRolls(string site = "ground-base", int hi = 4, params int[] directions) =>
        (directions.Length == 0 ? Enumerable.Range(0, 8) : directions).Select(d =>
            Choice.Create(new("A04." + site, "tactics/plan", d), new SamplingLaw.SecondaryRange(0, hi), ChoiceCode.Secondary(0))).ToList();
    private sealed class TacticsFixture : IBarbarianOrderEffects, IBarbarianRansomEffects, ISettlerPopulationEffects
    {
        public RevelationFixture Data { get; }
        public GameState World => Data.World;
        public RuleTables Rules => Data.Rules;
        public UnitMissionFixture Missions { get; }
        public ParadropFixture Para { get; }
        public FoundingFixture Found { get; }
        public EliminationFixture Life => Found.Life;
        public GameplayRandom Random => Life.Random;
        public SharedGoals Goals { get; }
        public UnitTactics Tactics { get; }
        public StartingPositions Starts = new();
        public List<string> Effects { get; } = [];
        public bool FailCosmetic;
        public MapPoint Point => Data.Unit(0).Position;
        public TacticsFixture(List<Choice>? choices = null, RevelationFixture? data = null, List<Choice>? paraChoices = null)
        {
            Data = data ?? new();
            Missions = new(data: Data); Para = new(paraChoices, Data); Found = new(choices: choices, data: Data);
            var incidents = new IncidentFixture(Random, Data);
            // Build the real child graph first, then author a coherent, empty world for this group.
            World.Entities.Units.SetEnd(0); World.Entities.Cities.SetEnd(0);
            for (int i = 0; i < EntityLimits.Civilizations; ++i)
            { World.Civilizations[i] = default; SharedGoals.ResetStanding(ref World.Civilizations[i]); }
            foreach (ref var tile in World.Map.Tiles)
            { tile = default; tile.Terrain = Terrain.Grassland; tile.Region = 1; tile.Owner = 0; tile.RevealedTo = CivilizationSet.Only(1); }
            World.Map.Regions[1] = new(480, 64, 0, default);
            World.Map.CoarseGrid(0).Fill(0); World.Map.CoarseGrid(1).Fill(0);
            World.Paths.Owner = 1; World.Paths.SelectedUnitType = 4;
            for (int i = 0; i < RuleLimits.UnitTypes; ++i)
                Rules.Units[i] = new()
                {
                    Role = RuleRole.Attack,
                    Domain = RuleDomain.Land,
                    Movement = 6,
                    Attack = 1,
                    Defense = 1,
                    HitPoints = 10,
                    Firepower = 1,
                    CostRows = 2,
                    Prerequisite = AdvanceReference.None,
                    ObsoleteBy = AdvanceReference.Never
                };
            Rules.Units[0] = Rules.Units[0] with { Role = RuleRole.Settle };
            Rules.Units[1] = Rules.Units[1] with { Role = RuleRole.SeaTransport, Domain = RuleDomain.Sea, Capacity = 4 };
            Rules.Units[3] = Rules.Units[3] with { Role = RuleRole.Defend, Defense = 2 };
            Rules.Units[46] = Rules.Units[46] with { Role = RuleRole.Diplomacy };
            Rules.Units[47] = Rules.Units[46];
            Rules.Units[48] = Rules.Units[48] with { Role = RuleRole.Trade };
            Rules.Terrain[(int)Terrain.Grassland] = new()
            {
                MovementCost = 1,
                DefenseFactor = 2,
                Food = 2,
                Work = new(new(TerrainResult.ImproveInPlace, 1, 4), new(TerrainResult.ImproveInPlace, 1, 4), (TerrainResult)Terrain.Plains)
            };
            Rules.Terrain[(int)Terrain.Ocean] = Rules.Terrain[(int)Terrain.Grassland];
            World.Globals.Turn = 2; World.Globals.HumanPlayers = CivilizationSet.Only(2); World.Multiplayer.LocalHuman = 2;
            World.Globals.CivsAlive = CivilizationSet.Only(1).With(2).With(3); World.Globals.Status.Scenario = false;
            World.Globals.Difficulty = Difficulty.Prince;
            Data.AddUnit(0, 1, 4, new(10, 10)); Data.Unit(0).Visibility = CivilizationSet.Only(1); Data.Unit(0).LastDirection = 255;
            World.Civilizations[1].UnitsActive[4] = 1; World.UnitTotals[1] = 1;
            World.Civilizations[1].Government = Government.Monarchy;
            Goals = new(World, Rules, new MovementGoalQueries(World, Rules));
            var orders = new GroundOrders(World, Rules, Data.Stacks, Goals);
            var recovery = new UnitRecovery(World, Rules);
            var strength = new CombatStrength(World, Rules, Data.Stacks, new());
            var pillage = new Pillage(World, Rules, new(World, Rules, _ => { }), Goals, (_, _) => Cosmetic("pillage"));
            var text = new ClassicTextInput(); text.Files["scenario/CITY.TXT"] = "@BARBARIANS\n1\nPay the raiders\n";
            var ransom = new BarbarianRansom(World, Data.Stacks, Life.Loss, Life.Elimination, incidents.Incidents, new ClassicRansomText(text.Reader()), this);
            var barbarians = new BarbarianOrders(World, Rules, Random, Data.Stacks, new(World, Rules, Data.Stacks,
                new(World, Rules, Data.Stacks)), pillage, ransom, Life.Loss, Life.Elimination, incidents.Incidents, this);
            var navalTargets = new NavalTargets(World);
            var naval = new NavalSeaTactics(World, new(World, Rules, Data.Stacks),
                new(World, Rules, Data.Stacks, new(World, Rules, Data.Stacks, Goals, Random), new(World, Rules, Data.Stacks, Goals, navalTargets)),
                new(World, Rules, Data.Stacks, navalTargets, Random), Goals);
            Tactics = new(World, Rules, new(barbarians,
                new(World, Rules, new(World, Rules, Data.Stacks, recovery), orders, Random), orders,
                new(World, Rules, Data.Stacks, strength, Goals), new(World, Rules, Data.Stacks, recovery),
                new(World, Rules, Data.Stacks), new(World, Rules, Life.Loss, Life.Elimination, incidents.Incidents),
                new(World, Rules, Goals, Found.Founding), new(World, Rules, Data.Stacks, Para.Drop), new(World), naval,
                new(World, Rules, pillage), new(World, Data.Process.Rules, Data.Stacks, Goals),
                new(World, Life.Loss, Life.Elimination, incidents.Incidents, this),
                new(World, Rules, Data.Stacks, strength, new(World, Rules, Data.Stacks, new()), Missions.Missions, Random)));
        }
        public MapPoint At(int direction) => MapNeighborhoods.Offset(World.Map.Geometry, Point, MapNeighborhoods.Directions[direction], World.Globals.Rules.FlatWorld);
        public void City(MapPoint point, int owner = 1, int slot = 0, int size = 4)
        {
            Data.AddCity(slot, owner, point, size); World.Entities.Cities.Storage[slot].Name = "Haven" + slot;
            World.Civilizations[owner].CityCount++; World.Civilizations[owner].ContinentCities[World.Map.TileAt(point).Region]++;
            World.Civilizations[owner].TotalCitySize += (short)size;
        }
        public UnitTactics.Operation Begin() => Tactics.Begin(new(0), Starts, "tactics");
        public void Complete(UnitTactics.Operation op, UnitTacticsReply? reply = null)
        {
            for (int i = 0; i < 200 && op.Result is null; ++i)
            { var pending = op.Resume(reply); if (reply is null) Equal(null, pending); }
            Equal(true, op.Result is not null); Random.RequireChoicesConsumed();
            Para.Capture.Life.Random.RequireChoicesConsumed(); Missions.Lifecycle.Random.RequireChoicesConsumed();
        }
        public void EnsureVisible(MapPoint point, int observer) => Cosmetic("visible:" + observer);
        public void Redraw(MapPoint point) => Cosmetic("draw");
        public void RefreshStatus() => Cosmetic("status");
        public void Landing(BarbarianLandingNotice notice) => Cosmetic("landing");
        public void RefreshCity(CitySlot city) => Cosmetic("city");
        private void Cosmetic(string name) { Effects.Add(name); if (FailCosmetic) throw new InvalidOperationException("Presentation unavailable"); }
    }

    private static void UnitTacticsEarlyEntry()
    {
        foreach (var order in new[] { UnitOrder.Fortress, UnitOrder.Road, UnitOrder.Irrigate, UnitOrder.Mine, UnitOrder.Transform, UnitOrder.Clean })
        {
            var f = new TacticsFixture(); f.Data.Unit(0).Orders = order; f.Data.Stacks.Relocate(0, new(-2, 0), false);
            using var op = f.Begin(); f.Complete(op); Equal(0, op.Result); Equal(null, op.Context);
            Equal(order, f.Data.Unit(0).Orders); Equal((byte)0, f.Data.Unit(0).MovesUsed);
        }
        var offmap = new TacticsFixture(); offmap.Data.Stacks.Relocate(0, new(-2, 0), false);
        using var asleep = offmap.Begin(); offmap.Complete(asleep); Equal(null, asleep.Context); Equal(UnitOrder.Sleeping, offmap.Data.Unit(0).Orders);
        var barb = new TacticsFixture(); barb.Data.Unit(0).Owner = new(0); barb.Data.Stacks.Relocate(0, new(10, 0), false);
        barb.Data.Unit(0).Orders = UnitOrder.Clean; using var gone = barb.Begin(); barb.Complete(gone);
        Equal(1, gone.Result); Equal(null, gone.Context); Equal(false, barb.Data.Unit(0).Token.Occupied);
    }

    private static void UnitTacticsRecoveryAndScenario()
    {
        var f = new TacticsFixture(); f.Data.Unit(0).Damage = 6;
        using var heal = f.Begin(); f.Complete(heal); Equal(AiTaskIds.Recover, f.Data.Unit(0).AiTask);
        Equal(UnitOrder.Fortifying, f.Data.Unit(0).Orders); Equal((byte)3, f.Data.Unit(0).MovesUsed);
        Equal(false, heal.Context!.SettlerType);
        var nearby = new TacticsFixture(); nearby.Data.Unit(0).Damage = 1; nearby.City(nearby.At(1));
        using var home = nearby.Begin(); nearby.Complete(home); Equal(UnitOrder.GoTo, nearby.Data.Unit(0).Orders);
        Equal(nearby.At(1), nearby.Data.Unit(0).Destination); Equal((byte)0, nearby.Data.Unit(0).MovesUsed);
        var historical = new TacticsFixture(); historical.Data.Unit(0).Type = new(0);
        historical.World.Globals.Status.Scenario = true; historical.World.Scenario.Flags.HistoricalAi = true;
        historical.World.Map.TileAt(historical.Point).Fertility = 15;
        using var hold = historical.Begin(); historical.Complete(hold);
        Equal(false, hold.Context!.SettlerType); Equal(0, historical.World.Entities.Cities.End);
        Equal(UnitOrder.None, historical.Data.Unit(0).Orders); Equal((byte)6, historical.Data.Unit(0).MovesUsed);
    }

    private static void UnitTacticsOrdinaryPlannerAndRetainedGoto()
    {
        var f = new TacticsFixture(TacticsRolls()); var origin = f.Point;
        using var op = f.Begin(); f.Complete(op);
        Equal(UnitOrder.AiGoTo, f.Data.Unit(0).Orders); Equal(f.At(0), f.Data.Unit(0).Destination);
        Equal(origin, f.Point); Equal((byte)0, f.Data.Unit(0).MovesUsed); Equal(AiTaskIds.ChooseMove, f.Data.Unit(0).AiTask);
        Equal(false, op.Context!.SettlerType); Equal(null, op.Context.CitySelector);
        var retained = new TacticsFixture(); retained.Data.Unit(0).Orders = UnitOrder.GoTo; retained.Data.Unit(0).Destination = new(20, 10);
        using var keep = retained.Begin(); retained.Complete(keep);
        Equal(UnitOrder.GoTo, retained.Data.Unit(0).Orders); Equal((byte)0, retained.Data.Unit(0).MovesUsed);
        var arrived = new TacticsFixture(); arrived.Data.Unit(0).Orders = UnitOrder.GoTo; arrived.Data.Unit(0).Destination = arrived.Point;
        using var finish = arrived.Begin(); arrived.Complete(finish);
        Equal(UnitOrder.None, arrived.Data.Unit(0).Orders); Equal(true, arrived.Data.Unit(0).Flags.GoalReached);
    }

    private static void UnitTacticsActualOrderExecution()
    {
        var execution = new OrderFixture();
        var f = new TacticsFixture(TacticsRolls(), execution.Data);
        f.Data.Stacks.Relocate(0, new(18, 12), false); f.Data.Unit(0).LastDirection = 1;
        using var chosen = f.Begin(); f.Complete(chosen);
        Equal(new MapPoint(20, 12), f.Data.Unit(0).Destination); Equal(new MapPoint(18, 12), f.Point);
        using var moved = execution.Begin(); execution.Complete(moved);
        Equal(new MapPoint(20, 12), f.Point); Equal((byte)3, f.Data.Unit(0).MovesUsed);
        Equal(false, f.World.UnitMovementActive);
    }

    private static void UnitTacticsCityGuardAndFerry()
    {
        var defender = new TacticsFixture(); defender.Data.Unit(0).Type = new(3); defender.City(defender.Point);
        using var guard = defender.Begin(); defender.Complete(guard);
        Equal(true, guard.Context!.Guard); Equal((CitySlot?)new CitySlot(0), defender.Data.Unit(0).Home);
        Equal(AiTaskIds.Fortify, defender.Data.Unit(0).AiTask); Equal(UnitOrder.Fortifying, defender.Data.Unit(0).Orders);
        var ferry = new TacticsFixture(TacticsRolls()); ferry.City(ferry.Point);
        ferry.World.Entities.Cities.Storage[0].Flags.FerryPort = true; ferry.World.Civilizations[1].ContinentPolicy[1] = 5;
        ferry.Data.Unit(0).Flags.Colonist = true;
        using var waiting = ferry.Begin(); ferry.Complete(waiting);
        Equal(true, waiting.Context!.StayHome); Equal(true, waiting.Context.Hold); Equal(false, waiting.Context.SettlerType);
        Equal(UnitOrder.None, ferry.Data.Unit(0).Orders); Equal((byte)6, ferry.Data.Unit(0).MovesUsed);
    }

    private static void UnitTacticsFoundingAndWork()
    {
        foreach (bool fail in new[] { false, true })
        {
            var f = new TacticsFixture(); f.Data.Unit(0).Type = new(0); f.World.Globals.Turn = 1;
            f.Starts.Positions[0] = f.Point; f.World.Map.TileAt(f.Point).Fertility = 9; f.Found.FailCosmetic = fail;
            using var op = f.Begin(); f.Complete(op);
            Equal(1, op.Result); Equal(false, f.Data.Unit(0).Token.Occupied); Equal((short)1, f.World.Civilizations[1].CityCount);
            Equal("First", f.World.Entities.Cities.Storage[0].Name); Equal(true, op.Context!.SettlerType);
            Equal(fail, op.PresentationFailures.Count > 0);
        }
        var work = new TacticsFixture(); work.Data.Unit(0).Type = new(0);
        work.World.Map.TileAt(work.Point).Features |= MapFeatures.Pollution;
        using var assigned = work.Begin(); work.Complete(assigned);
        Equal(UnitOrder.Clean, work.Data.Unit(0).Orders); Equal(AiTaskIds.LocalCleanup, work.Data.Unit(0).AiTask);
        Equal((byte)0, work.Data.Unit(0).MovesUsed); Equal(true, work.World.Map.TileAt(work.Point).Features.HasFlag(MapFeatures.Pollution));
    }

    private static void UnitTacticsPopulationReturn()
    {
        var f = new TacticsFixture(); f.Data.Unit(0).Type = new(0); f.City(f.Point, size: 9);
        f.FailCosmetic = true; f.Data.Unit(0).Home = new(0); f.World.Map.Regions[1] = new(480, 0, 0, default);
        using var absorb = f.Begin(); f.Complete(absorb);
        Equal(1, absorb.Result); Equal((sbyte)10, f.World.Entities.Cities.Storage[0].Size);
        Equal(false, f.Data.Unit(0).Token.Occupied); Sequence(new[] { "city", "draw" }, f.Effects);
    }

    private static void UnitTacticsPillageAndPartisan()
    {
        var f = new TacticsFixture(); f.Rules.Units[4] = f.Rules.Units[4] with { Movement = 3 };
        f.City(new(14, 10), 2); f.World.Civilizations[1].Treaties[2].War = true;
        f.World.Map.TileAt(f.Point).Features |= MapFeatures.Irrigation | MapFeatures.Mine;
        using var pillage = f.Begin(); f.Complete(pillage);
        Equal(false, f.World.Map.TileAt(f.Point).Features.HasFlag(MapFeatures.Mine));
        Equal(true, f.World.Map.TileAt(f.Point).Features.HasFlag(MapFeatures.Irrigation));
        Equal((byte)3, f.Data.Unit(0).MovesUsed); Equal(UnitOrder.Fortifying, f.Data.Unit(0).Orders);
        var partisan = new TacticsFixture(); partisan.Data.Unit(0).Type = new(UnitTypeIds.Partisans);
        partisan.City(new(14, 10), 2); partisan.World.Civilizations[1].Treaties[2].War = true;
        partisan.World.Map.TileAt(partisan.At(1)).Features |= MapFeatures.Road;
        using var road = partisan.Begin(); partisan.Complete(road);
        Equal(partisan.At(1), partisan.Data.Unit(0).Destination); Equal(AiTaskIds.PartisanPillage, partisan.Data.Unit(0).AiTask);
        Equal((byte)1, partisan.Data.Unit(0).Counter); Equal((byte)0, partisan.Data.Unit(0).MovesUsed);
    }

    private static void UnitTacticsSpecialDestinations()
    {
        foreach (byte type in new byte[] { 46, 48 })
        {
            var f = new TacticsFixture(); f.Data.Unit(0).Type = new(type);
            using var gone = f.Begin(); f.Complete(gone);
            Equal(1, gone.Result); Equal(false, f.Data.Unit(0).Token.Occupied);
            Equal(false, gone.Context!.SettlerType);
            var retained = new TacticsFixture(); retained.Data.Unit(0).Type = new(type);
            retained.Data.Unit(0).Orders = UnitOrder.GoTo; retained.Data.Unit(0).Destination = new(20, 10);
            using var keep = retained.Begin(); retained.Complete(keep);
            Equal(UnitOrder.GoTo, retained.Data.Unit(0).Orders); Equal((byte)0, retained.Data.Unit(0).MovesUsed);
        }
    }

    private static void UnitTacticsAirAndNuclear()
    {
        var air = new TacticsFixture(); air.City(new(14, 10));
        air.Rules.Units[4] = air.Rules.Units[4] with { Domain = RuleDomain.Air };
        using var home = air.Begin(); air.Complete(home);
        Equal(UnitOrder.GoTo, air.Data.Unit(0).Orders); Equal(new MapPoint(14, 10), air.Data.Unit(0).Destination);
        Equal(false, home.Context!.SettlerType);
        var nuclear = new TacticsFixture(); nuclear.City(nuclear.Point);
        nuclear.Rules.Units[4] = nuclear.Rules.Units[4] with { Domain = RuleDomain.Air, Attack = 99 };
        using var hold = nuclear.Begin(); nuclear.Complete(hold);
        Equal(1, hold.Result); Equal((byte)3, nuclear.Data.Unit(0).MovesUsed); Equal(UnitOrder.None, nuclear.Data.Unit(0).Orders);
        Equal(false, hold.Context!.SettlerType);
    }

    private static void UnitTacticsParatrooperHome()
    {
        var f = new TacticsFixture(); f.City(new(14, 10));
        f.Rules.Units[4] = f.Rules.Units[4] with { Abilities = new() { Paradrop = true } };
        using var op = f.Begin(); f.Complete(op);
        Equal((CitySlot?)new CitySlot(0), op.Context!.CitySelector);
        Equal(UnitOrder.GoTo, f.Data.Unit(0).Orders); Equal(new MapPoint(14, 10), f.Data.Unit(0).Destination);
        Equal(AiTaskIds.CityTarget, f.Data.Unit(0).AiTask); Equal((byte)0, f.Data.Unit(0).MovesUsed);
    }

    private static void UnitTacticsSeaNoMoves()
    {
        var f = new TacticsFixture(); f.Data.Unit(0).Type = new(1); f.Data.Unit(0).MovesUsed = 6;
        foreach (ref var tile in f.World.Map.Tiles) { tile.Terrain = Terrain.Ocean; tile.Region = 2; }
        f.Data.AddUnit(1, 1, 3, f.Point); f.Data.Unit(1).Orders = UnitOrder.Sleeping;
        using var op = f.Begin(); f.Complete(op);
        Equal(1, op.Result); Equal(true, op.Context!.NoMoves); Equal(false, op.Context.SettlerType);
        Equal((byte)6, f.Data.Unit(0).MovesUsed); Equal(f.Point, f.Data.Unit(1).Position);
        Equal(0, op.Context.DesiredDefenders); Equal(UnitOrder.None, f.Data.Unit(1).Orders);
    }

    private static void UnitTacticsTransportRetry()
    {
        var f = new TacticsFixture(TacticsRolls()); f.Data.Unit(0).Type = new(1);
        foreach (ref var tile in f.World.Map.Tiles) { tile.Terrain = Terrain.Ocean; tile.Region = 2; }
        f.Data.AddUnit(1, 1, 3, f.Point); f.Data.Unit(1).Orders = UnitOrder.Sleeping;
        f.Data.AddUnit(2, 2, 1, f.At(1)); f.World.Civilizations[1].Treaties[2].Peace = true;
        f.City(new(30, 10)); f.World.Map.TileAt(new(30, 10)).Terrain = Terrain.Grassland;
        f.World.Map.TileAt(new(30, 10)).Region = 1; f.World.Map.Regions[1] = new(480, 64, 0, new RegionSet().With(2));
        f.World.Entities.Cities.Storage[0].Flags.Coastal = true; f.World.Civilizations[1].ContinentPolicy[1] = 1;
        f.Data.Unit(0).Orders = UnitOrder.GoTo; f.Data.Unit(0).Destination = f.At(1);
        using var op = f.Begin(); f.Complete(op);
        Equal(true, op.Context!.RetryTransport); Equal(true, op.Context.HasOrders);
        Equal(UnitOrder.GoTo, f.Data.Unit(0).Orders); Equal(new MapPoint(30, 10), f.Data.Unit(0).Destination);
        Equal((byte)0, f.Data.Unit(0).MovesUsed); Equal(UnitOrder.None, f.Data.Unit(1).Orders);
        Equal((CitySlot?)new CitySlot(0), op.Context.HomeCity); Equal(0, op.Context.DesiredDefenders);
    }

    private static void UnitTacticsNavalInitialization()
    {
        var f = new TacticsFixture(); f.Data.Unit(0).Type = new(1); f.City(f.Point);
        f.Data.AddUnit(1, 1, 0, f.Point); f.Data.Unit(1).Orders = UnitOrder.Sleeping;
        f.World.Civilizations[1].ContinentPolicy[1] = 4; f.World.Globals.Turn = 1; f.Data.Unit(0).Counter = 1;
        f.Data.Unit(0).Destination = f.Point;
        using var recruitment = f.Begin(); f.Complete(recruitment);
        Equal(2, recruitment.Context!.DesiredDefenders); // Reached city-guard quota replaces zero.
        Equal(false, f.World.Entities.Cities.Storage[0].Flags.FerryPort); Equal(true, recruitment.Context.Guard);
        Equal((byte)6, f.Data.Unit(0).MovesUsed);
        foreach (bool demand in new[] { false, true })
        {
            var port = new NavalPortFixture(); var point = new MapPoint(10, 10);
            port.Port(0, point, 1, demand ? (byte)4 : (byte)0, coastal: false);
            port.Data.World.Civilizations[1].ContinentCities[1] = 1;
            port.Data.World.Globals.Turn = 1; port.Data.Unit(0).Counter = 1; port.Data.Unit(0).Destination = point;
            var initial = NavalTransportContext.ForChooser(new(port.Data.World, port.Data.Rules, new(0)));
            initial.ResetCargo(ScanNavalCargo(port.Data, initial.Tactics));
            Equal(demand ? NavalPortResult.Hold : NavalPortResult.Continue, port.Select(initial));
            Equal(demand, port.Data.World.Entities.Cities.Storage[0].Flags.FerryPort);
            Equal(demand ? 2 : 0, port.Goal(point, (int)RuleRole.Settle));
            Equal(demand ? 2 : 0, port.Goal(point, (int)RuleRole.Defend));
            Equal(demand, initial.CargoNeeded);
        }
        // An unmatched landing starts false. A real match survives a later cargo scan/retry.
        var data = UnloadFixture(1, 0); UnloadLand(data, new(11, 9), fertile: 0);
        data.World.Civilizations[1].ContinentMilitary[1] = 1; data.Unit(0).Counter = 9;
        var context = new UnitTacticsContext(data.World, data.Rules, new(0));
        var state = NavalTransportContext.ForChooser(context); state.ResetCargo(ScanNavalCargo(data, context));
        Equal(NavalRoute.Continue, Unloader(data).Choose(state, 0).Route); Equal((byte)9, data.Unit(0).Counter);
        Equal(false, state.GotoLandingRegion); Equal(false, state.CargoNeeded);
        data.Unit(0).Orders = UnitOrder.GoTo; data.Unit(0).Destination = new(11, 9);
        Equal(NavalRoute.Continue, Unloader(data).Choose(state, 0).Route); Equal(true, state.GotoLandingRegion);
        data.Unit(0).Orders = UnitOrder.None; data.Unit(0).Counter = 9; context.RetryTransport = true;
        state.ResetCargo(ScanNavalCargo(data, context)); Unloader(data).Choose(state, 0);
        Equal(true, state.GotoLandingRegion); Equal((byte)(data.World.Globals.Turn & 7), data.Unit(0).Counter);
        var fresh = NavalTransportContext.ForChooser(new(data.World, data.Rules, new(0)));
        Equal(false, fresh.GotoLandingRegion); Equal(false, fresh.CargoNeeded); Equal(0, fresh.SelectedPortPolicy);
    }

    private static void UnitTacticsPrivateRansom()
    {
        foreach (bool pay in new[] { false, true })
        {
            using var runtime = new SessionRuntime(new(), new(729));
            var f = new TacticsFixture(data: new(runtime.Process, runtime.World)); ParticipantId recipientSeat = default;
            f.Data.Unit(0).Owner = new(0); f.Data.Stacks.RelocateInPlace(0, false); f.World.Globals.Turn = 3;
            f.City(f.At(0), 2); f.World.Civilizations[2].Gold = 150;
            runtime.RegisterCommand(new(new(1, "test.tactics", "Choose orders", AccessClass.Player, new([]), new([new("result", ValueKind.Integer)])),
                (_, _, _) => new(), _ => new UnitTacticsOperation(f.Begin(), _ => recipientSeat,
                    _ => throw new InvalidOperationException("No city visit in this case"))));
            runtime.BeginClientSession(new()); var caller = SessionClient(runtime); var recipient = SessionClient(runtime, 2);
            recipientSeat = recipient.Identity.Participant;
            var command = caller.Submit(new("test.tactics", new([]))).Correlation.Command; runtime.AdvanceOne();
            Equal(null, RequiredResult(caller, command).Interaction); var prompt = RequiredInteraction(recipient, command);
            Equal(false, caller.Reply(prompt, 1).Accepted); Equal(false, recipient.Reply(prompt, 9).Accepted);
            if (pay) Equal(true, recipient.Reply(prompt, 1).Accepted); else Equal(true, recipient.Cancel(command).Accepted);
            runtime.AdvanceOne(); Equal(CommandStatus.Completed, RequiredResult(caller, command).Status);
            Equal(new CommandValue.Integer(pay ? 1 : 0), RequiredResult(caller, command).Result.Single().Value);
            Equal(pay ? 50 : 150, f.World.Civilizations[2].Gold); Equal(!pay, f.Data.Unit(0).Token.Occupied);
            Equal(false, recipient.Reply(prompt, 1).Accepted); f.Random.RequireChoicesConsumed();
        }
    }

    private static void UnitTacticsNuclearReload()
    {
        var f = new TacticsFixture(); f.City(f.Point); f.City(new(18, 10), 2, 1, 20);
        f.Rules.Units[4] = f.Rules.Units[4] with { Domain = RuleDomain.Air, Attack = 99, Movement = 12, Range = 1 };
        f.World.Civilizations[1].UnitsActive[4] = 2; f.World.Civilizations[1].CityCount = 5;
        f.World.Civilizations[1].MilitaryStrength = 100; f.World.Civilizations[2].MilitaryStrength = 200;
        f.World.Civilizations[1].Treaties[2].NuclearThreat = true;
        using var op = f.Begin(); f.Complete(op);
        Equal(new MapPoint(19, 9), f.Point); Equal(new MapPoint(18, 10), f.Data.Unit(0).Destination);
        Equal(new MapPoint(18, 10), op.Context!.Position); Equal(UnitOrder.AiGoTo, f.Data.Unit(0).Orders);
        Equal(true, f.World.Civilizations[1].Treaties[2].Nuked);
        Equal((byte)0, f.Data.Unit(0).MovesUsed);
    }

    private static void UnitTacticsPrivateBribe()
    {
        foreach (bool pay in new[] { false, true })
        {
            using var runtime = new SessionRuntime(new(), new(730)); ParticipantId actorSeat = default;
            var choices = TacticsRolls(); choices.AddRange(TacticsRolls("ground-opportunistic-bribe", 7, 0));
            var f = new TacticsFixture(choices, new(runtime.Process, runtime.World));
            f.World.Globals.HumanPlayers = CivilizationSet.Only(1).With(2); f.World.Multiplayer.LocalHuman = 1;
            f.World.Globals.Difficulty = Difficulty.King; f.World.Civilizations[1].Treaties[2].Peace = true;
            f.World.Civilizations[1].Gold = 1000; f.World.Civilizations[2].Gold = 150;
            f.World.Map.TileAt(new(30, 10)).Region = 3; f.City(new(30, 10));
            f.Rules.Units[4] = f.Rules.Units[4] with { Abilities = new() { IgnoreZonesOfControl = true } };
            f.Data.AddUnit(1, 2, 4, f.At(0));
            runtime.RegisterCommand(new(new(1, "test.tactics", "Choose orders", AccessClass.Player, new([]), new([new("result", ValueKind.Integer)])),
                (_, _, _) => new(), _ => new UnitTacticsOperation(f.Begin(), _ => actorSeat,
                    _ => throw new InvalidOperationException("No city visit in this case"))));
            runtime.BeginClientSession(new()); var caller = SessionClient(runtime, 2); var actor = SessionClient(runtime);
            actorSeat = actor.Identity.Participant;
            var command = caller.Submit(new("test.tactics", new([]))).Correlation.Command; runtime.AdvanceOne();
            Equal(null, RequiredResult(caller, command).Interaction); var prompt = RequiredInteraction(actor, command);
            Equal(false, caller.Reply(prompt, 1).Accepted); Equal(1000, f.World.Civilizations[1].Gold);
            if (pay) actor.Reply(prompt, 1); else actor.Cancel(command);
            runtime.AdvanceOne(); Equal(CommandStatus.Completed, RequiredResult(caller, command).Status);
            Equal(new CommandValue.Integer(0), RequiredResult(caller, command).Result.Single().Value);
            Equal(pay ? 950 : 1000, f.World.Civilizations[1].Gold);
            Equal(new CivilizationSlot((byte)(pay ? 1 : 2)), f.Data.Unit(1).Owner);
            Equal(UnitOrder.AiGoTo, f.Data.Unit(0).Orders); Equal(f.At(1), f.Data.Unit(0).Destination);
            Equal((byte)0, f.Data.Unit(0).MovesUsed); Equal(false, actor.Reply(prompt, 1).Accepted);
            f.Random.RequireChoicesConsumed(); f.Missions.Lifecycle.Random.RequireChoicesConsumed();
        }
    }

    private static void UnitTacticsActualParadrop()
    {
        var rolls = Enumerable.Range(0, 8).Select(i => Choice.Create(new("U05.paradrop-approach", "tactics/paratrooper/paradrop", i),
            new SamplingLaw.PrimaryModulo(6), ChoiceCode.Primary(0))).ToList();
        rolls.Add(Choice.Create(new("C09.capture.building-parity", "tactics/paratrooper/paradrop/capture"), new SamplingLaw.PrimaryModulo(2), ChoiceCode.Primary(0)));
        var f = new TacticsFixture(paraChoices: rolls); f.City(f.Point); f.City(new(14, 10), 2, 1, 5); f.City(new(30, 10), 2, 2);
        f.Rules.Units[4] = f.Rules.Units[4] with { Abilities = new() { Paradrop = true } };
        f.World.Civilizations[1].Treaties[2].War = true;
        using var op = f.Begin(); f.Complete(op);
        Equal(0, op.Result); Equal(new MapPoint(14, 10), f.Point); Equal(true, f.Data.Unit(0).Flags.Paradropped);
        Equal(new CivilizationSlot(1), f.World.Entities.Cities.Storage[1].Owner);
        Equal(true, f.Para.Animations.Count > 0); Equal((CitySlot?)new CitySlot(0), op.Context!.CitySelector);
    }

    private static void UnitTacticsFailureAndDisposal()
    {
        var f = new TacticsFixture(); f.Data.Unit(0).Flags.Colonist = true;
        using var failure = f.Begin(); Throws<ChoiceException>(() => failure.Resume());
        Equal(false, f.Data.Unit(0).Flags.Colonist); Equal((byte)0, f.Data.Unit(0).MovesUsed);
        Throws<InvalidOperationException>(() => failure.Resume());
        var remote = new TacticsFixture(); remote.World.Globals.Multiplayer = GameMode.Tcp;
        remote.Data.Unit(0).Flags.Colonist = true; using var unsupported = remote.Begin();
        Throws<InvalidOperationException>(() => unsupported.Resume()); Equal(true, remote.Data.Unit(0).Flags.Colonist);
        var pending = new TacticsFixture(); pending.Data.Unit(0).Owner = new(0); pending.World.Globals.Turn = 3;
        pending.City(pending.At(0), 2); pending.World.Civilizations[2].Gold = 150;
        using var ransom = pending.Begin(); Equal(true, ransom.Resume() is UnitTacticsDecision.Barbarian);
        ransom.Dispose(); Equal(150, pending.World.Civilizations[2].Gold); Equal(true, pending.Data.Unit(0).Token.Occupied);
        Throws<ObjectDisposedException>(() => ransom.Resume());
    }
}
