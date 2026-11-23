using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private sealed class ResolutionFixture : ICombatResolutionEffects, ICombatHistoryEffects
    {
        public CombatFixture Combat { get; } = new();
        public RevelationFixture Data => Combat.Data;
        public GameState World => Data.World;
        public CombatWorkspace State => Combat.State;
        public EliminationFixture Life { get; }
        public CombatResolution Resolution { get; }
        public List<string> Calls { get; } = [];
        public Action<string>? Observe;
        public string? Fail;
        public int ProductionChoices;
        public ResolutionFixture(List<Choice>? extra = null)
        {
            List<Choice> choices = [Choice.Create(new("U06.promotion", "resolution/promotion"), new SamplingLaw.PrimaryModulo(12), ChoiceCode.Primary(0))];
            if (extra is not null) choices.AddRange(extra);
            Life = new(choices, Data); var incidents = new IncidentFixture(Life.Random, Data);
            World.Globals.Rules.NoRestart = true; World.Globals.CivsAlive = CivilizationSet.Only(1).With(2).With(3);
            Life.Recreate = owner => Creation(Data, owner, Life.Random);
            var removal = new CityRemoval(World, Data.Rules, Data.Stacks, Life.Loss, _ => Emit("city-removed"));
            var airlift = new AiProductionAirlift(World, Data.Rules, Data.Stacks, Life.Deletion, Life.Random, _ => Emit("airlift"));
            var policy = new CityProductionChoice(World, Data.Process.Rules, new(), new(World, Data.Process.Rules, Data.Stacks), Data.Stacks,
                new(World, Data.Rules, ClassicShip, ClassicCalendar), airlift);
            var production = new ProductionOrders(World, Data.Rules, city => { ++ProductionChoices; return policy.Choose(city, "resolution/production"); }, _ => Emit("production-notice"));
            var promotion = new CombatPromotion(World, Data.Rules, Life.Random, _ => Emit("promotion"));
            var casualties = new CombatCasualties(World, Data.Stacks, State, new(World, this), Life.Elimination, incidents.Incidents, _ => Emit("event"));
            Resolution = new(World, Data.Rules, State, new(promotion, casualties, new(World, Data.Rules, Data.Stacks), removal,
                Life.Elimination, incidents.Incidents, production, new(World, Data.Rules, Data.Stacks)), this);
        }
        public PreparedCombat Prepare() => Combat.Prepare(true) with { Attack = 8, Defense = 4 };
        public CombatResolution.Operation Begin(bool won = true)
        {
            // These cases supply the already-selected pre-round presentation kind.
            int explosion = State.ExplosionKind; var prepared = Prepare(); State.ExplosionKind = explosion;
            return Resolution.Begin(prepared, won, "resolution");
        }
        public void Complete(CombatResolution.Operation op)
        {
            int budget = 100;
            while (!op.Completed && --budget > 0) Equal(null, op.Resume());
            Equal(true, op.Completed); Life.Random.RequireChoicesConsumed();
        }
        private void Emit(string value) { Calls.Add(value); Observe?.Invoke(value); if (Fail == value) throw new InvalidOperationException("presentation failed"); }
        public void Sound(CombatSound sound, int argument) => Emit("sound:" + sound + ":" + argument);
        public void Synchronize() => Emit("sync");
        public void Redraw(MapPoint origin, MapPoint target) => Emit("redraw");
        public void Explosion(MapPoint point, int kind) => Emit($"explosion:{point.X},{point.Y}:{kind}");
        public void RedrawNeighbors(MapPoint point) => Emit("neighbors");
        public int SecondaryColor(int civilization) => civilization;
        public string PluralName(int civilization) => World.Names[civilization].Tribe;
        public void RefreshHistory(int civilization) => Emit("history:" + civilization);
    }

    // U06 result presentation precedes promotion and casualties; normal win retains active-loss state.
    private static void CombatResultPresentationOrder()
    {
        var f = new ResolutionFixture(); f.State.AnyViewer = f.State.Seen[1] = true; f.State.ExplosionKind = 46;
        f.State.AnimationDefender = new(2); f.State.KillCount = 19; f.State.ActiveUnitLost = true; f.World.Events.Loaded = true;
        f.Observe = value =>
        {
            if (value == "redraw")
            { Equal(true, f.Data.Unit(2).Token.Occupied); Equal(false, f.Data.Unit(3).Flags.Veteran); Equal(new UnitSlot(2), f.State.AnimationDefender); Equal(19, f.State.KillCount); }
            if (value == "promotion") { Equal((UnitSlot?)null, f.State.AnimationDefender); Equal(0, f.State.KillCount); Equal(true, f.Data.Unit(2).Token.Occupied); }
            if (value == "event") Equal(true, f.Data.Unit(3).Flags.Veteran);
        };
        using var op = f.Begin(); f.Complete(op);
        Sequence(new[] { "sound:LargeExplosion:1", "sync", "redraw", "explosion:10,10:0", "promotion" }, f.Calls.Take(5));
        Equal(true, f.State.ActiveUnitLost); Equal(0, f.State.ExplosionKind); Equal(3, f.State.KillCount);
        Equal(new ResolvedCombat(1, 2, false, null), op.Result); Equal(3, f.World.Casualties[2].Count);
        Equal(true, f.Data.Unit(3).Token.Occupied); int count = f.Calls.Count; op.Resume(); Equal(count, f.Calls.Count);
        var noViewer = new ResolutionFixture(); noViewer.State.LastSound = CombatSound.JetCrash; noViewer.State.SoundArgument = 7;
        using var loss = noViewer.Begin(false); noViewer.Complete(loss); Equal(true, noViewer.State.ActiveUnitLost);
        Equal(CombatSound.JetCrash, noViewer.State.LastSound); Equal(7, noViewer.State.SoundArgument); Equal("sync", noViewer.Calls[0]);
    }

    private static void CombatResultSoundAndViewers()
    {
        foreach (int variant in Enumerable.Range(0, 6))
        {
            var f = new ResolutionFixture(); f.State.AnyViewer = f.State.Seen[1] = true; f.State.ExplosionShown = true; f.State.SoundArgument = 7;
            bool won = variant < 3;
            if (won)
            {
                int type = variant == 0 ? 11 : 30;
                f.Data.Rules.Units[type] = f.Data.Rules.Units[0] with { Domain = RuleDomain.Air };
                for (int i = 0; i < 3; ++i) f.Data.Unit(i).Type = new((byte)type);
                if (variant == 2) f.Combat.City();
            }
            else f.Data.Rules.Units[10] = f.Data.Rules.Units[10] with { Domain = RuleDomain.Air, Abilities = new() { DestroyedAfterAttack = variant == 4 } };
            if (variant == 5) { f.Data.Rules.Units[30] = f.Data.Rules.Units[10]; f.Data.Unit(3).Type = new(30); }
            using var op = f.Begin(won); f.Complete(op);
            CombatSound? sound = variant is 0 or 3 ? CombatSound.DiveCrash : variant is 1 or 5 ? CombatSound.JetCrash : null;
            Equal(sound, f.State.LastSound); Equal(sound is null ? 7 : 1, f.State.SoundArgument);
            Equal(false, f.Calls.Any(x => x.StartsWith("explosion:"))); Equal(true, f.Calls.Contains("redraw"));
        }
        var hidden = new ResolutionFixture(); hidden.State.AnyViewer = true; hidden.State.Seen[0] = hidden.State.Seen[2] = true;
        using var result = hidden.Begin(); hidden.Complete(result); Equal(false, hidden.Calls.Contains("redraw"));
    }

    private static void CombatLoserGroupingAndProtection()
    {
        foreach (int protection in Enumerable.Range(0, 4))
        {
            var f = new ResolutionFixture();
            if (protection == 1) f.World.Map.TileAt(CargoPoint).Features |= MapFeatures.Fortification;
            if (protection == 2) f.World.Map.TileAt(CargoPoint).Features |= MapFeatures.Fortification | MapFeatures.CitySite;
            if (protection == 3) { var city = f.Combat.City(); BuildingQueries.SetBuilding(f.World, city, BuildingIds.CityWalls, true); }
            using var op = f.Begin(); f.Complete(op);
            Equal(protection == 0 ? 3 : 1, f.State.KillCount); Equal(protection != 0, f.Data.Unit(0).Token.Occupied);
        }
        var land = new ResolutionFixture(); land.Data.AddUnit(4, 1, 10, new(8, 10));
        land.Observe = value => { if (value == "history:1") Equal(UnitStaging.Lifted(1), land.Data.Unit(3).Position); };
        using var loss = land.Begin(false); land.Complete(loss); Equal(false, land.Data.Unit(3).Token.Occupied); Equal(true, land.Data.Unit(4).Token.Occupied);
        Equal(CargoPoint, land.World.Casualties[1].At(0).Position); Equal(new MapPoint(-200, -200), land.Data.Unit(3).Position);
        var sea = new ResolutionFixture(); sea.Data.Rules.Units[10] = sea.Data.Rules.Units[10] with { Domain = RuleDomain.Sea, Capacity = 1 };
        sea.Data.AddUnit(4, 1, 0, new(8, 10)); sea.Data.Unit(4).Orders = UnitOrder.Sleeping; sea.Data.Unit(4).Carrier = new(3);
        using var sunk = sea.Begin(false); sea.Complete(sunk); Equal(2, sea.State.KillCount);
        Equal(false, sea.Data.Unit(3).Token.Occupied || sea.Data.Unit(4).Token.Occupied);
        var ransom = new ResolutionFixture(); for (int i = 0; i < 3; ++i) ransom.Data.Unit(i).Owner = new(0);
        ransom.Data.Rules.Units[0] = ransom.Data.Rules.Units[0] with { Role = RuleRole.Diplomacy };
        using var leader = ransom.Begin(); ransom.Complete(leader); Equal(true, leader.Result!.Ransom); Equal(0, leader.Result.Loser);
    }

    private static void CombatCityPopulationGates()
    {
        foreach (int gate in Enumerable.Range(0, 5))
        {
            var f = new ResolutionFixture(); var city = f.Combat.City(); f.World.Entities.Cities.At(city).Size = 4;
            if (gate == 1) f.World.Map.TileAt(new(8, 10)).Terrain = Terrain.Ocean;
            if (gate == 2) BuildingQueries.SetBuilding(f.World, city, BuildingIds.CityWalls, true);
            if (gate == 3) f.World.Globals.WonderCity[WonderIds.GreatWall] = 0;
            if (gate == 4) { f.World.Globals.Difficulty = Difficulty.Chieftain; f.World.Globals.HumanPlayers = f.World.Globals.HumanPlayers.With(2); }
            using var op = f.Begin(); f.Complete(op); ref var record = ref f.World.Entities.Cities.At(city);
            Equal((sbyte)(gate == 0 ? 3 : 4), record.Size); Equal(true, record.Flags.DefenderLost);
            Equal(gate == 0, record.KnownTo.Contains(1)); Equal(0, f.ProductionChoices); // Remaining garrison suppresses reset.
        }
        var wrap = new ResolutionFixture(); var zero = wrap.Combat.City(); wrap.World.Entities.Cities.At(zero).Size = 0;
        using var retained = wrap.Begin(); wrap.Complete(retained); Equal((sbyte)-1, wrap.World.Entities.Cities.At(zero).Size);
        Equal(true, wrap.World.Entities.Cities.At(zero).Token.Occupied); Equal((byte)255, wrap.World.Entities.Cities.At(zero).RememberedSize[1]);
    }

    private static void CombatCityDeletionAndRetasking()
    {
        var f = new ResolutionFixture(CreationAttitudes()); var city = f.Combat.City(); f.World.Entities.Cities.At(city).Size = 1;
        using var op = f.Begin();
        f.State.AggregateTreatyNotice = true; f.State.TreatyNotices[0] = f.State.TreatyNotices[1] = f.State.TreatyNotices[7] = true; f.State.AllyWarnings[1] = 2;
        f.World.Map.RememberedLayer(7).Fill(MapFeatures.CitySite);
        f.Complete(op);
        Equal(false, f.World.Entities.Cities.At(city).Token.Occupied); Equal(false, f.World.Map.TileAt(CargoPoint).City);
        Equal(false, f.World.Map.Remembered(7, CargoPoint).HasFlag(MapFeatures.CitySite)); Equal(true, f.Calls.Contains("neighbors"));
        Equal(false, f.State.AggregateTreatyNotice || f.State.TreatyNotices[1] || f.State.TreatyNotices[7]); Equal(true, f.State.TreatyNotices[0]); Equal(2, f.State.AllyWarnings[1]);
        Equal(1, f.Life.Restarts); Equal(city, op.Result!.DefendingCity); // Retained slot, not an invented absent-city repair.
        var human = new ResolutionFixture(); var defended = human.Combat.City(); human.World.Globals.HumanPlayers = CivilizationSet.Only(2);
        human.World.Multiplayer.LocalHuman = 2; human.World.Entities.Cities.At(defended).Size = 1;
        // An AI capacity unit is eligible even with Land domain and an ordinary attack role.
        human.Data.AddUnit(4, 3, 10, new(12, 10)); human.Data.AddUnit(5, 3, 0, new(12, 10));
        human.Data.Rules.Units[10] = human.Data.Rules.Units[10] with { Capacity = 255 };
        human.World.Map.TileAt(new(12, 10)).Terrain = Terrain.Ocean;
        using var lostCity = human.Begin();
        human.State.AggregateTreatyNotice = true; human.State.TreatyNotices[1] = true;
        human.Complete(lostCity);
        Equal(false, human.World.Entities.Cities.At(defended).Token.Occupied); Equal(UnitOrder.GoTo, human.Data.Unit(4).Orders);
        Equal(CargoPoint, human.Data.Unit(4).Destination); Equal(true, human.State.AggregateTreatyNotice && human.State.TreatyNotices[1]);
    }

    private static void ExposedTransportRetaskingGates()
    {
        foreach (int gate in Enumerable.Range(0, 9))
        {
            var f = new CombatFixture(); var city = f.City();
            f.Attacker.Owner = new(3); f.Data.AddUnit(4, 3, 0, new(8, 10)); f.World.Map.TileAt(new(8, 10)).Terrain = Terrain.Ocean;
            f.Data.Rules.Units[10] = f.Data.Rules.Units[10] with { Capacity = 255, Movement = 6 };
            f.Attacker.MovesUsed = 99; f.Attacker.AiTask = 17; f.Attacker.Counter = 18;
            if (gate == 1) f.Attacker.Owner = new(0);
            if (gate == 2) f.World.Globals.HumanPlayers = f.World.Globals.HumanPlayers.With(3);
            if (gate == 3) f.Attacker.Owner = new(2);
            if (gate == 4) f.World.Civilizations[3].Treaties[2].Peace = true;
            if (gate == 5) f.Data.Rules.Units[10] = f.Data.Rules.Units[10] with { Capacity = 0 };
            if (gate == 6) f.Data.Stacks.Relocate(4, new(30, 10), false);
            if (gate == 7) f.World.Map.TileAt(new(8, 10)).Terrain = Terrain.Grassland;
            if (gate == 8) f.Data.Rules.Units[10] = f.Data.Rules.Units[10] with { Movement = (byte)f.World.Map.Geometry.WeightedDistance(CargoPoint, f.Attacker.Position, true) };
            f.World.Civilizations[3].Treaties[2].Alliance = true;
            new ExposedCityTransports(f.World, f.Data.Rules, f.Data.Stacks).Retask(city);
            Equal(gate == 0 ? UnitOrder.GoTo : UnitOrder.None, f.Attacker.Orders);
            Equal((byte)17, f.Attacker.AiTask); Equal((byte)18, f.Attacker.Counter); Equal((byte)99, f.Attacker.MovesUsed);
        }
    }

    private static void ExposedTransportAllowanceAndTopology()
    {
        foreach (byte damage in new byte[] { 0, 9 })
        {
            var f = new CombatFixture(); var city = f.City(); f.Attacker.Owner = new(3); f.Attacker.Damage = damage;
            f.Data.Rules.Units[10] = f.Data.Rules.Units[10] with { Capacity = 1, Movement = 6 };
            f.Data.Stacks.Relocate(3, new(4, 8), false); f.Data.AddUnit(4, 3, 0, new(4, 8)); f.World.Map.TileAt(new(4, 8)).Terrain = Terrain.Ocean; // W01: deltas (6,2) have weighted distance 5.
            new ExposedCityTransports(f.World, f.Data.Rules, f.Data.Stacks).Retask(city);
            Equal(damage == 0 ? UnitOrder.GoTo : UnitOrder.None, f.Attacker.Orders);
        }
        foreach (bool flat in new[] { false, true })
        {
            var f = new CombatFixture(); var city = f.City(); f.World.Entities.Cities.At(city).Position = new(0, 10);
            f.Attacker.Owner = new(3); f.Data.Rules.Units[10] = f.Data.Rules.Units[10] with { Capacity = 1 };
            f.Data.Stacks.Relocate(3, new(38, 10), false); f.Data.AddUnit(4, 3, 0, new(38, 10)); f.World.Map.TileAt(new(38, 10)).Terrain = Terrain.Ocean;
            f.World.Globals.Rules.FlatWorld = flat;
            new ExposedCityTransports(f.World, f.Data.Rules, f.Data.Stacks).Retask(city);
            Equal(flat ? UnitOrder.None : UnitOrder.GoTo, f.Attacker.Orders);
        }
    }

    private static void CombatResolutionRoundsAndProduction()
    {
        var f = new ResolutionFixture([CombatChoice("U06.round.attack", 8, 7), CombatChoice("U06.round.defense", 4, 0)]);
        var prepared = f.Prepare() with { AttackFirepower = 10 };
        using var rounds = RoundService(f.Combat, f.Life.Random, new()).Begin(prepared, "fight");
        Equal(10, rounds.Advance()); Equal((byte)10, f.Data.Unit(2).Damage);
        using var resolution = f.Resolution.Begin(prepared, rounds.Result != 0, "resolution"); f.Complete(resolution);
        Equal(true, f.Data.Unit(3).Flags.Veteran); Equal(false, f.Data.Unit(2).Token.Occupied); Equal(3, f.World.Casualties[2].Count);
        var ai = new ResolutionFixture(); var city = ai.Combat.City();
        ai.Life.Deletion.Erase(0, false); ai.Life.Deletion.Erase(1, false);
        ai.World.Entities.Cities.At(city).Production = 99;
        ai.World.Civilizations[2].Government = Government.Despotism;
        ai.Data.Rules.Units[60] = ai.Data.Rules.Units[0] with { Role = RuleRole.Defend, Prerequisite = AdvanceReference.None };
        ai.Data.AddCity(1, 2, new(30, 10)); ai.World.Map.TileAt(new(30, 10)).Region = 1;
        ai.World.Entities.Cities.Storage[1].Production = 99;
        ai.World.Civilizations[2].ContinentPolicy[0] = 4;
        ai.Data.Rules.Cosmic = ai.Data.Rules.Cosmic with { ShieldBoxRows = 10, FoodPerCitizen = 2 };
        using var exposed = ai.Begin(); ai.Complete(exposed);
        Equal(1, ai.ProductionChoices); Equal((sbyte)60, ai.World.Entities.Cities.At(city).Production); Equal((sbyte)99, ai.World.Entities.Cities.Storage[1].Production);
        Equal(true, ai.World.Entities.Cities.At(city).KnownTo.Contains(1));
    }

    private static void CombatResolutionNestedLoss()
    {
        var f = new ResolutionFixture(); f.Data.Rules.Units[10] = f.Data.Rules.Units[10] with { Role = RuleRole.Settle };
        f.World.Civilizations[1].CityCount = 0; f.World.Civilizations[1].UnitsActive[10] = 1;
        f.Data.AddUnit(4, 3, 4, new(24, 10)); f.Data.Unit(4).Flags.SpyMission = true; f.Data.Unit(4).Counter = 1;
        f.World.Civilizations[1].Treaties[3].Peace = true;
        using var op = f.Begin(false); var pending = op.Resume()!;
        Equal(1, pending.Victim.Civilization); Equal(3, pending.Actor.Civilization); Equal(pending, op.Resume());
        Equal(false, op.Completed); Equal(true, f.State.ActiveUnitLost); Equal(1, f.State.KillCount);
        Equal(false, f.Data.Unit(3).Token.Occupied); Equal(1, f.World.Casualties[1].Count);
        Equal(true, f.Data.Unit(2).Flags.Veteran); int calls = f.Calls.Count;
        op.Resume(0); f.Complete(op); Equal(calls, f.Calls.Count); Equal(new ResolvedCombat(2, 1, false, null), op.Result);
    }

    private static void CombatResolutionFailures()
    {
        foreach (string phase in new[] { "sound:LargeExplosion:1", "sync", "redraw", "explosion:10,10:0", "promotion", "history:2" })
        {
            var f = new ResolutionFixture(); f.State.AnyViewer = f.State.Seen[1] = true; f.State.ExplosionKind = 6; f.State.KillCount = 19;
            f.Fail = phase; using var op = f.Begin(); Throws<InvalidOperationException>(() => op.Resume());
            Equal(true, f.Data.Unit(2).Token.Occupied); Equal(false, op.Completed);
            Equal(phase is "promotion" or "history:2", f.Data.Unit(3).Flags.Veteran);
            Equal(phase is "promotion" or "history:2" ? 0 : 19, f.State.KillCount);
            int calls = f.Calls.Count; Throws<InvalidOperationException>(() => op.Resume()); Equal(calls, f.Calls.Count);
        }
        var partial = new ResolutionFixture(); var operation = partial.Begin(); operation.Resume(); operation.Dispose();
        Throws<ObjectDisposedException>(() => operation.Resume()); Equal(1, partial.State.KillCount); Equal(false, partial.Data.Unit(0).Token.Occupied);
        var remote = new ResolutionFixture(); var prepared = remote.Prepare(); remote.World.Globals.Multiplayer = GameMode.Tcp;
        using var blocked = remote.Resolution.Begin(prepared, false, "resolution"); Throws<InvalidOperationException>(() => blocked.Resume());
        Equal(false, remote.State.ActiveUnitLost); Equal(0, remote.Calls.Count);
    }
}
