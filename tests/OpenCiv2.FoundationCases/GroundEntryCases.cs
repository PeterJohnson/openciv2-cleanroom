using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.Units;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static GroundTacticsEntry TacticEntry(RevelationFixture data, GameplayRandom random) => new(data.World, data.Rules,
        new(data.World, data.Rules, data.Stacks, new(data.World, data.Rules)), TacticOrders(data), random);
    private static GameplayRandom EntryRandom(params Choice[] choices) => new(new ControlledChoices(new(choices)), "ground-entry");
    private static Choice TrespassChoice(uint code = 0) => Choice.Create(new("A04.trespass", "entry"), new SamplingLaw.SecondaryRange(0, 3), ChoiceCode.Secondary(code));

    private static void GroundEntryWorkBypass()
    {
        foreach (var order in new[] { UnitOrder.Fortress, UnitOrder.Road, UnitOrder.Irrigate, UnitOrder.Mine, UnitOrder.Transform, UnitOrder.Clean })
        {
            var f = new RecoveryFixture(); f.Data.Unit(0).Orders = order;
            f.Data.Unit(0).Type = new(255); f.Data.Unit(0).Position = new(-1, -1); f.Data.Unit(0).Flags.Colonist = true;
            var random = EntryRandom(); var result = TacticEntry(f.Data, random).EnterOrdinary(new(0), "entry");
            Equal(0, result.Result); Equal(null, result.Context); Equal(null, result.Recovery);
            Equal(order, f.Data.Unit(0).Orders); Equal((byte)0, f.Data.Unit(0).MovesUsed); Equal(true, f.Data.Unit(0).Flags.Colonist);
            random.RequireChoicesConsumed();
        }
        var airbase = new RecoveryFixture(); airbase.Data.Unit(0).Orders = UnitOrder.Airbase;
        var normal = TacticEntry(airbase.Data, EntryRandom()).EnterOrdinary(new(0), "entry");
        Equal(null, normal.Result); Equal(true, normal.Context is not null); Equal(GroundRecoveryRoute.ScenarioGuard, normal.Recovery!.Value.Route);
        Equal(UnitOrder.Airbase, airbase.Data.Unit(0).Orders);
        var barbarian = new RecoveryFixture(); barbarian.Data.Unit(0).Owner = new(0); barbarian.Data.Unit(0).Orders = UnitOrder.Road;
        Throws<ArgumentException>(() => TacticEntry(barbarian.Data, EntryRandom()).EnterOrdinary(new(0), "entry"));
        Equal(UnitOrder.Road, barbarian.Data.Unit(0).Orders); // This entry cannot substitute ordinary logic for the required W06 dispatch.
    }

    private static void GroundEntryOffMapSleep()
    {
        foreach (var point in new[] { new MapPoint(-1, 10), new MapPoint(10, -1), new MapPoint(40, 10), new MapPoint(10, 24) })
        {
            var f = new RecoveryFixture(); f.Data.Unit(0).Orders = UnitOrder.GoTo; f.Data.Unit(0).Position = point;
            f.Data.Unit(0).Type = new(255); f.Data.Unit(0).Carrier = new(5); f.Data.Unit(0).MovesUsed = 2; f.Data.Unit(0).LastDirection = 4;
            var result = TacticEntry(f.Data, EntryRandom()).EnterOrdinary(new(0), "entry");
            Equal(0, result.Result); Equal(null, result.Context); Equal(UnitOrder.Sleeping, f.Data.Unit(0).Orders);
            Equal(new MapPoint(-1, 8), f.Data.Unit(0).Destination); Equal(null, f.Data.Unit(0).Carrier);
            Equal((byte)2, f.Data.Unit(0).MovesUsed); Equal((byte)4, f.Data.Unit(0).LastDirection);
        }
        var sleeping = new RecoveryFixture(); sleeping.Data.Unit(0).Orders = UnitOrder.Sleeping;
        sleeping.Data.Unit(0).Position = new(-1, 10); sleeping.Data.Unit(0).Carrier = new(5);
        TacticEntry(sleeping.Data, EntryRandom()).EnterOrdinary(new(0), "entry");
        Equal(new UnitSlot(5), sleeping.Data.Unit(0).Carrier); Equal(new MapPoint(8, 8), sleeping.Data.Unit(0).Destination);
    }

    private static void GroundEntryTrespassGates()
    {
        foreach (int variant in Enumerable.Range(0, 8))
        {
            var f = new RecoveryFixture(); f.Ai(); f.Data.AddCity(0, 1, new(10, 8));
            int foreign = variant == 7 ? 0 : 2; f.Data.AddUnit(1, foreign, 0, new(12, 10));
            var w = f.Data.World; w.Civilizations[1].Treaties[foreign].Peace = variant != 5;
            w.Globals.Difficulty = variant == 1 ? Difficulty.Prince : Difficulty.King;
            if (variant == 2) w.Globals.HumanPlayers = CivilizationSet.Only(3);
            w.Civilizations[1].Attitude[foreign] = (byte)(variant == 3 ? 59 : 60);
            w.Globals.PowerRank[1] = 2; w.Globals.PowerRank[foreign] = (byte)(variant == 4 ? 2 : 5);
            w.Diplomacy.SelectedPair = (1, foreign); w.Diplomacy.Attitude = 9;
            if (variant == 6) f.Data.AddCity(1, 2, new(11, 9));
            var random = variant == 0 ? EntryRandom(TrespassChoice()) : EntryRandom();
            var result = TacticEntry(f.Data, random).EnterOrdinary(new(0), "entry");
            Equal(foreign, result.Context!.AdjacentForeignCivilization); Equal(true, result.Context.EnemyZone);
            bool adjusted = variant is not (5 or 6 or 7);
            Equal((byte)((variant == 3 ? 59 : 60) + (adjusted ? 1 : 0)), w.Civilizations[1].Attitude[foreign]);
            Equal(adjusted ? 10 : 9, w.Diplomacy.Attitude);
            Equal(variant == 0, w.Civilizations[1].Treaties[foreign].HostileIntent);
            Equal(false, w.Civilizations[foreign].Treaties[1].HostileIntent); Equal(false, w.Civilizations[1].Treaties[foreign].War);
            random.RequireChoicesConsumed();
        }
        var absent = new RecoveryFixture(); absent.Ai(); absent.Data.AddUnit(1, 2, 0, new(12, 10));
        absent.Data.World.Civilizations[1].Treaties[2].Peace = true;
        absent.Data.World.Civilizations[1].Attitude[2] = 60; absent.Data.World.Globals.Difficulty = Difficulty.King;
        absent.Data.World.Globals.PowerRank[1] = 1; absent.Data.World.Globals.PowerRank[2] = 2;
        var nonzero = EntryRandom(TrespassChoice(0x80000000)); var entry = TacticEntry(absent.Data, nonzero).EnterOrdinary(new(0), "entry");
        Equal(null, entry.Context!.HomeCity); Equal(null, entry.Context.NearestCity);
        Equal((byte)61, absent.Data.World.Civilizations[1].Attitude[2]); Equal(false, absent.Data.World.Civilizations[1].Treaties[2].HostileIntent);
        nonzero.RequireChoicesConsumed();
    }

    private static void GroundEntryForcedCity()
    {
        foreach (int variant in Enumerable.Range(0, 8))
        {
            var f = new RecoveryFixture(); f.Ai(); f.Data.AddCity(0, 1, new(20, 10)); f.Data.AddCity(1, 2, new(11, 9));
            f.Data.Unit(0).Damage = 8; f.Data.Unit(0).Orders = UnitOrder.GoTo;
            switch (variant)
            {
                case 1: f.Data.World.Civilizations[1].Treaties[2].Peace = true; f.Data.World.Civilizations[1].Treaties[2].War = true; break;
                case 2: f.Data.World.Civilizations[1].Treaties[2].Alliance = true; break;
                case 3: f.Data.World.Civilizations[1].Treaties[2].Ceasefire = true; break;
                case 4: f.Data.AddUnit(1, 2, 0, new(11, 9)); break;
                case 5: f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Role = RuleRole.Diplomacy }; break;
                case 6: f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Domain = RuleDomain.Sea }; break;
                case 7: f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Role = (RuleRole)255 }; break;
            }
            var result = TacticEntry(f.Data, EntryRandom()).EnterOrdinary(new(0), "entry");
            bool forced = variant is 0 or 7;
            Equal(forced, result.Context!.ForceAttack); Equal(forced ? 0 : 3, result.Recovery!.Value.DamageClass);
            if (forced)
            {
                Equal(true, result.Context.EnemyZone); Equal(2, result.Context.AdjacentForeignCivilization);
                Equal(UnitOrder.None, f.Data.Unit(0).Orders); Equal((byte)0, f.Data.Unit(0).MovesUsed);
                Equal(GroundRecoveryRoute.ScenarioGuard, result.Recovery.Value.Route); Equal(null, result.Result);
            }
        }
    }

    private static void GroundEntryRecoveryCompletion()
    {
        var land = new RecoveryFixture(); land.Ai(); land.Data.Unit(0).Damage = 1;
        land.Data.World.Civilizations[1].ContinentPolicy[0] = (byte)RuleRole.Defend;
        var held = TacticEntry(land.Data, EntryRandom()).EnterOrdinary(new(0), "entry");
        Equal(0, held.Result); Equal(GroundRecoveryRoute.Hold, held.Recovery!.Value.Route);
        Equal(UnitOrder.Fortifying, land.Data.Unit(0).Orders); Equal((byte)6, land.Data.Unit(0).MovesUsed); Equal((byte)100, land.Data.Unit(0).AiTask);
        var air = new RecoveryFixture(2); air.Ai(); air.Data.Unit(0).Damage = 4;
        var waiting = TacticEntry(air.Data, EntryRandom()).EnterOrdinary(new(0), "entry");
        Equal(1, waiting.Result); Equal(GroundRecoveryRoute.Hold, waiting.Recovery!.Value.Route);
        Equal(UnitOrder.None, air.Data.Unit(0).Orders); Equal((byte)3, air.Data.Unit(0).MovesUsed); Equal(true, air.Data.Unit(0).Flags.Retreating);
        var transport = new RecoveryFixture(1); transport.Ai(); transport.Data.Unit(0).Damage = 1;
        transport.Data.Rules.Units[1] = transport.Data.Rules.Units[1] with { Capacity = 1 };
        transport.Data.Unit(0).MovesUsed = 6; transport.Data.Unit(0).Flags.Retreating = true;
        var cargo = TacticEntry(transport.Data, EntryRandom()).EnterOrdinary(new(0), "entry");
        Equal(null, cargo.Result); Equal(GroundRecoveryRoute.TransportWork, cargo.Recovery!.Value.Route);
        Equal(true, cargo.Context!.NoMoves); Equal(true, transport.Data.Unit(0).Flags.Retreating);
    }

    private static void GroundEntryActualReturnMovement()
    {
        var f = new OrderFixture(); var d = f.Data;
        d.World.Globals.HumanPlayers = CivilizationSet.Only(2); d.World.Multiplayer.LocalHuman = 2;
        d.Rules.Units[0] = d.Rules.Units[0] with { HitPoints = 12 }; d.Unit(0).Damage = 4;
        d.AddCity(d.World.Entities.Cities.End, 1, new(20, 12)); d.Unit(0).Orders = UnitOrder.None;
        var random = EntryRandom(); var entered = TacticEntry(d, random).EnterOrdinary(new(0), "entry");
        Equal(0, entered.Result); Equal(GroundRecoveryRoute.Finish, entered.Recovery!.Value.Route);
        Equal(UnitOrder.GoTo, d.Unit(0).Orders); Equal(new MapPoint(20, 12), d.Unit(0).Destination); Equal((byte)100, d.Unit(0).AiTask);
        using var move = f.Begin(); f.Complete(move);
        Equal(1, move.MovementResult); Equal(new MapPoint(20, 12), d.Unit(0).Position);
        Equal(true, d.Unit(0).Flags.GoalReached); Equal(UnitOrder.None, d.Unit(0).Orders); random.RequireChoicesConsumed();
    }
}
