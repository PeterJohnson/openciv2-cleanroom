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
    private sealed class WarNotices : IDiplomaticWarEffects
    {
        public List<DiplomaticWarNotice> Notices { get; } = [];
        public List<int> Redraws { get; } = [];
        public List<MapPoint> Cursors { get; } = [];
        public bool FailNotice;
        public void Notice(DiplomaticWarNotice notice)
        {
            Notices.Add(notice);
            if (FailNotice) { throw new InvalidOperationException("Presentation unavailable"); }
        }
        public void RedrawMaps(int localHuman) => Redraws.Add(localHuman);
        public void MoveCursor(MapPoint position) => Cursors.Add(position);
    }
    private static GameplayRandom WarRandom(params Choice[] choices) => new(new ControlledChoices(new(choices)), "diplomatic-war");
    private static DiplomaticWar War(RevelationFixture f, WarNotices notices, GameplayRandom? random = null) =>
        new(f.World, f.Rules, f.Stacks, new(f.World, f.Rules, f.Stacks), random ?? WarRandom(), notices);

    // V04 §7: retained summaries, contradictory treaties, signed credits and unsigned reputation.
    private static void DeclarationReputationAndDirection()
    {
        var f = new RevelationFixture(); var w = f.World; var notices = new WarNotices(); var war = War(f, notices);
        w.Globals.Difficulty = Difficulty.Prince; w.Globals.Turn = 123; w.Globals.PowerRank[1] = 7;
        w.Civilizations[1].CityCount = 5; w.Civilizations[1].Reputation = 254; w.Civilizations[1].ReputationCredit[2] = -128;
        w.Civilizations[1].Treaties[2] = new() { Peace = true, Ceasefire = true };
        war.Declare(1, 2);
        Equal((byte)2, w.Civilizations[1].Reputation); Equal((sbyte)126, w.Civilizations[1].ReputationCredit[2]);
        Equal(true, w.Civilizations[1].Treaties[2].War); Equal(true, w.Civilizations[2].Treaties[1].WarThisTurn);
        Equal(false, w.Civilizations[1].Treaties[2].Peace); Equal(false, w.Civilizations[2].Treaties[1].Ceasefire);
        Equal(true, w.Civilizations[2].Treaties[1].Vendetta); Equal(false, w.Civilizations[1].Treaties[2].Vendetta);
        Equal(true, w.Civilizations[1].Treaties[2].ExtendedAggressor); Equal(false, w.Civilizations[2].Treaties[1].Aggressor);
        Equal((short)123, w.Civilizations[1].LastContactTurn[2]); Equal(0, notices.Notices.Count);

        f = new(); w = f.World; war = War(f, notices); w.Globals.Difficulty = Difficulty.Prince; w.Globals.PowerRank[1] = 7;
        w.Civilizations[1].CityCount = 5; w.Civilizations[3].ReputationCredit[1] = 127; w.Civilizations[3].Attitude[1] = 80;
        w.Civilizations[1].Treaties[2] = new() { Peace = true, Ceasefire = true };
        war.Declare(1, 2, 3);
        Equal((sbyte)-128, w.Civilizations[3].ReputationCredit[1]); Equal((byte)65, w.Civilizations[3].Attitude[1]);
        Equal((byte)2, w.Civilizations[1].Reputation); Equal((sbyte)0, w.Civilizations[1].ReputationCredit[2]);
        war.Declare(1, 4, 3); Equal((byte)60, w.Civilizations[3].Attitude[1]); Equal((byte)2, w.Civilizations[1].Reputation);

        f = new(); w = f.World; war = War(f, new()); w.Globals.Difficulty = Difficulty.Prince; w.Globals.PowerRank[1] = 7;
        w.Civilizations[1].CityCount = 5; f.AddCity(0, 1, new(2, 2)); w.Globals.WonderCity[WonderIds.EiffelTower] = 0;
        w.Civilizations[1].Treaties[2].Peace = true;
        war.Declare(1, 2); Equal((byte)1, w.Civilizations[1].Reputation);
        w.Globals.Difficulty = Difficulty.Chieftain; w.Globals.WonderCity[WonderIds.EiffelTower] = -1;
        w.Civilizations[1].CityCount = 4; w.Civilizations[1].Treaties[3].Peace = true;
        war.Declare(1, 3); Equal((byte)1, w.Civilizations[1].Reputation); Equal((sbyte)-1, w.Civilizations[1].ReputationCredit[3]);
        w.Globals.HumanPlayers = default; w.Globals.Difficulty = Difficulty.Prince;
        w.Civilizations[2].Treaties[3].Peace = true; war.Declare(2, 3);
        Equal((byte)1, w.Civilizations[2].Reputation); Equal(false, w.Civilizations[2].Treaties[3].Aggressor);
        Equal(false, w.Civilizations[3].Treaties[2].Vendetta); Equal((short)0, w.Civilizations[2].LastContactTurn[3]);
    }

    // V04 §6 + actual C01/U01: land uses stored regional count and later nearest-city ties.
    private static void AllianceLandExpulsion()
    {
        var f = new RevelationFixture(); var w = f.World; var notices = new WarNotices(); var war = War(f, notices);
        f.AddCity(0, 1, new(2, 2)); f.AddCity(1, 1, new(2, 6)); f.AddCity(2, 2, new(8, 4));
        f.AddUnit(0, 1, 0, new(8, 4)); f.Unit(0).Orders = UnitOrder.Fortified;
        f.AddUnit(1, 1, 3, new(8, 4)); f.Unit(1).Orders = UnitOrder.Sleeping;
        w.MovementMode = MovementMode.MoveUnits; w.Globals.ActiveUnit = new(0);
        war.Expel(1, 2); Equal(new MapPoint(8, 4), f.Unit(0).Position); // No census repair.
        w.Civilizations[2].ContinentCities[0] = 1;
        war.Expel(1, 2); Equal(new MapPoint(2, 6), f.Unit(0).Position); Equal(UnitOrder.None, f.Unit(0).Orders);
        Equal(new MapPoint(8, 4), f.Unit(1).Position); Equal(UnitOrder.Sleeping, f.Unit(1).Orders);
        Equal(new MapPoint(2, 6), notices.Cursors.Single()); Equal(1, f.Stacks.Count(0)); Equal(1, f.Stacks.Count(1));

        f = new(); w = f.World; notices = new(); war = War(f, notices);
        f.AddCity(0, 2, new(8, 4)); f.AddUnit(0, 1, 0, new(8, 4)); f.Unit(0).Orders = UnitOrder.Fortified;
        w.Civilizations[2].ContinentCities[0] = 1; war.Expel(1, 2);
        Equal(new MapPoint(8, 4), f.Unit(0).Position); Equal(UnitOrder.Fortified, f.Unit(0).Orders); Equal(0, notices.Cursors.Count);
    }

    // V04 §6: ocean ignores trade-role/census gates, prefers the first tied port, and lifts real cargo.
    private static void AllianceOceanCargoExpulsion()
    {
        var f = new RevelationFixture(); var w = f.World; var notices = new WarNotices(); var war = War(f, notices);
        f.AddCity(0, 1, new(4, 4)); f.AddCity(1, 1, new(4, 8)); f.AddCity(2, 2, new(8, 6));
        foreach (var point in new[] { new MapPoint(5, 5), new(5, 7), new(8, 6) })
        { w.Map.TileAt(point).Terrain = Terrain.Ocean; w.Map.TileAt(point).Region = 7; }
        f.Rules.Units[1] = f.Rules.Units[1] with { Role = RuleRole.Trade, Capacity = 1 };
        f.AddUnit(0, 1, 1, new(8, 6)); f.Unit(0).Orders = UnitOrder.Fortified;
        f.AddUnit(1, 1, 0, new(8, 6)); f.Unit(1).Orders = UnitOrder.Sleeping; f.Unit(1).Carrier = new(0);
        // The destination also receives an earlier retained lifted member through actual stack semantics.
        f.AddUnit(2, 1, 0, UnitStaging.Lifted(1));
        war.Expel(1, 2);
        Equal(new MapPoint(4, 4), f.Unit(0).Position); Equal(new MapPoint(4, 4), f.Unit(1).Position);
        Equal(new MapPoint(4, 4), f.Unit(2).Position); Equal(3, f.Stacks.Count(0));
        Equal(UnitOrder.None, f.Unit(0).Orders); Equal(UnitOrder.Sleeping, f.Unit(1).Orders);
        Equal(false, w.UnitStacks.LiftActive); Equal(false, w.UnitStacks.CargoActive);
        Equal(false, w.Map.TileAt(new(8, 6)).Features.HasFlag(MapFeatures.Unit));

        f = new(); w = f.World; war = War(f, new());
        f.AddCity(0, 1, new(4, 4)); f.AddCity(1, 2, new(8, 6));
        w.Map.TileAt(new(8, 6)).Terrain = Terrain.Ocean; w.Map.TileAt(new(8, 6)).Region = 63;
        f.AddUnit(0, 1, 1, new(8, 6)); f.Unit(0).Orders = UnitOrder.Fortified;
        war.Expel(1, 2); Equal(new MapPoint(8, 6), f.Unit(0).Position); Equal(UnitOrder.Fortified, f.Unit(0).Orders);
    }

    private static void AllianceCancellationAndDeclaration()
    {
        var f = new RevelationFixture(); var w = f.World; var notices = new WarNotices(); var war = War(f, notices);
        f.AddCity(0, 1, new(2, 2)); f.AddCity(1, 2, new(8, 4));
        f.AddUnit(0, 1, 0, new(8, 4)); f.AddUnit(1, 2, 0, new(2, 2));
        w.Civilizations[1].ContinentCities[0] = w.Civilizations[2].ContinentCities[0] = 1;
        w.Names[1].Tribe = "Alders"; w.Names[2].Tribe = "Birches";
        DiplomaticRelations.Set(w, 1, 2, new() { Alliance = true });
        w.Globals.Difficulty = Difficulty.Prince; w.Civilizations[1].Reputation = 255; w.Civilizations[1].ReputationCredit[2] = -128;
        war.Declare(1, 2);
        Equal((byte)1, w.Civilizations[1].Reputation); Equal((sbyte)127, w.Civilizations[1].ReputationCredit[2]);
        Equal(false, w.Civilizations[1].Treaties[2].Alliance); Equal(true, w.Civilizations[1].Treaties[2].Peace);
        Equal(false, w.Civilizations[1].Treaties[2].War); Equal(true, w.Civilizations[2].Treaties[1].Vendetta);
        Equal(new MapPoint(2, 2), f.Unit(0).Position); Equal(new MapPoint(8, 4), f.Unit(1).Position);
        Equal(1, notices.Redraws.Single()); Equal(1, notices.Notices.Single().Participant);
        Equal("Birches", notices.Notices.Single().Second.Plural);

        notices.Notices.Clear(); w.Multiplayer.LocalHuman = 3;
        war.CancelAlliance(1, 2); Equal(0, notices.Notices.Count);
        w.Civilizations[3].Treaties[2].Embassy = true;
        war.CancelAlliance(1, 2); Equal(null, notices.Notices.Single().Participant);
        notices.Notices.Clear(); w.Multiplayer.Saved.AutoplayActive = true;
        war.CancelAlliance(1, 2); Equal(0, notices.Notices.Count);

        f = new(); w = f.World; notices = new(); war = War(f, notices); w.Globals.HumanPlayers = default;
        DiplomaticRelations.Set(w, 1, 2, new() { Alliance = true });
        war.Declare(1, 2); Equal((byte)1, w.Civilizations[1].Reputation); // Extra unmediated penalty applies to AI at Chieftain too.
        Equal(false, w.Civilizations[2].Treaties[1].Vendetta);
        DiplomaticRelations.Set(w, 1, 2, new() { Alliance = true });
        w.Civilizations[3].Attitude[1] = 80; war.Declare(1, 2, 3);
        Equal((byte)1, w.Civilizations[1].Reputation); Equal((byte)55, w.Civilizations[3].Attitude[1]); Equal((sbyte)1, w.Civilizations[3].ReputationCredit[1]);
    }

    private static void WarAllianceObligations()
    {
        var f = new RevelationFixture(); var w = f.World; var notices = new WarNotices(); var war = War(f, notices);
        w.Globals.Turn = 70; w.Globals.CivsAlive = default; // No alive gate on obligations.
        w.Civilizations[2].Treaties[1].Contact = true; w.Civilizations[2].Treaties[3].Alliance = true;
        w.Civilizations[3].Treaties[1].Contact = true;
        w.Multiplayer.Saved.AutoplayActive = true;
        war.Declare(1, 2);
        Equal(DiplomaticWarNoticeKind.AllyActivated, notices.Notices.Single().Kind);
        Equal(true, w.Civilizations[3].Treaties[1].War); Equal((byte)100, w.Civilizations[3].Attitude[1]);
        Equal(true, w.Civilizations[3].Treaties[1].ExtendedAggressor); Equal(true, w.Civilizations[2].Treaties[1].ExtendedAggressor);
        Equal((short)70, w.Civilizations[1].LastContactTurn[3]); Equal(false, w.Civilizations[1].Treaties[3].Aggressor);

        f = new(); w = f.World; notices = new(); war = War(f, notices);
        w.Civilizations[1].Treaties[2].Contact = true;
        foreach (int ally in new[] { 3, 4, 5 }) { w.Civilizations[1].Treaties[ally].Alliance = true; w.Civilizations[ally].Treaties[2].Contact = true; }
        w.Civilizations[4].Treaties[2].Alliance = true; w.Civilizations[5].Treaties[2].War = true;
        w.Civilizations[3].Attitude[2] = 23; war.ActivateAllies(1, 2);
        Equal(DiplomaticWarNoticeKind.AllyHelps, notices.Notices.Single().Kind);
        Equal(true, w.Civilizations[3].Treaties[2].War); Equal((byte)23, w.Civilizations[3].Attitude[2]);
        Equal(false, w.Civilizations[4].Treaties[2].War); Equal(false, w.Civilizations[1].Treaties[2].Aggressor);
        Equal(false, w.Civilizations[5].Treaties[2].Aggressor);
        f = new(); w = f.World; war = War(f, new()); w.Globals.HumanPlayers = default;
        w.Civilizations[1].Treaties[2].Contact = true; w.Civilizations[1].Treaties[3].Alliance = true; w.Civilizations[3].Treaties[2].Contact = true;
        war.ActivateAllies(1, 2); Equal(false, w.Civilizations[3].Treaties[2].War);
    }

    private static void ForcedWarAndJoinedWar()
    {
        var f = new RevelationFixture(); var w = f.World; var notices = new WarNotices(); var war = War(f, notices);
        w.Globals.HumanPlayers = default;
        w.Civilizations[1].Treaties[2] = new() { HostileIntent = true, BrokenTreaty = true, Ceasefire = true, Peace = true };
        Equal(false, war.Force(1, 2)); Equal(true, w.Civilizations[1].Treaties[2].Ceasefire);
        Equal(false, w.Civilizations[1].Treaties[2].Peace); Equal(false, w.Civilizations[1].Treaties[2].HostileIntent);
        w.Civilizations[1].Treaties[2].Contact = true;
        Equal(true, war.Force(1, 2)); Equal(false, war.Force(1, 2));
        Equal((byte)100, w.Civilizations[1].Attitude[2]); Equal((byte)100, w.Civilizations[2].Attitude[1]);
        Equal(true, w.Civilizations[1].Treaties[2].SneakAttack); Equal(false, w.Civilizations[2].Treaties[1].SneakAttack);
        w.Civilizations[1].Treaties[2].SneakAttack = false; w.Civilizations[2].Treaties[1].SneakAttack = true;
        Equal(false, war.Force(1, 2)); Equal(false, w.Civilizations[1].Treaties[2].SneakAttack);

        f = new(); w = f.World; war = War(f, new());
        DiplomaticRelations.Set(w, 1, 2, new() { Contact = true, Alliance = true });
        Equal(true, war.Force(1, 2)); Equal((byte)1, w.Civilizations[1].Reputation);
        Equal(true, w.Civilizations[1].Treaties[2].War); Equal(false, w.Civilizations[1].Treaties[2].Alliance);
        Equal(true, w.Civilizations[2].Treaties[1].Vendetta); Equal((short)0, w.Civilizations[1].LastContactTurn[2]);

        foreach (int roll in new[] { 1, 3 })
        {
            f = new(); w = f.World; notices = new();
            var random = WarRandom(Choice.Create(new("V04.join-ally-war", "join"), new SamplingLaw.PrimaryModulo(3), ChoiceCode.Primary(roll)));
            war = War(f, notices, random); w.Globals.Turn = 100;
            w.Civilizations[3].Treaties[1].War = true; w.Civilizations[1].LastContactTurn[2] = 106;
            Equal(roll == 3, war.JoinAllyWar(2, 1, 3, "join")); random.RequireChoicesConsumed();
            Equal(roll == 3, w.Civilizations[2].Treaties[1].War);
            Equal(roll == 3 ? (short)100 : (short)106, w.Civilizations[1].LastContactTurn[2]);
            Equal(roll == 3 ? 1 : 0, notices.Notices.Count); Equal((byte)0, w.Civilizations[2].Reputation);
        }
        f = new(); w = f.World; notices = new(); war = War(f, notices); w.Globals.Turn = 100;
        w.Civilizations[3].Treaties[1].War = true; w.Civilizations[1].LastContactTurn[2] = 99;
        Equal(false, war.JoinAllyWar(2, 1, 3, "past"));
        w.Civilizations[1].LastContactTurn[2] = 105; Equal(false, war.JoinAllyWar(2, 1, 3, "five"));
        w.Civilizations[1].LastContactTurn[2] = 106; w.Globals.PowerRank[1] = 7;
        Equal(true, war.JoinAllyWar(2, 1, 3, "top-rank")); Equal((short)100, w.Civilizations[1].LastContactTurn[3]);
        Equal(DiplomaticWarNoticeKind.JoinedWar, notices.Notices.Single().Kind);
        w.Civilizations[2].Treaties[1] = new() { Vendetta = true }; w.Civilizations[3].Treaties[1] = new() { Vendetta = true };
        Equal(false, war.JoinAllyWar(2, 1, 3, "mutual-vendetta"));
        Equal(true, w.Civilizations[2].Treaties[1].HostileIntent); Equal(true, w.Civilizations[3].Treaties[1].HostileIntent);
        w.Civilizations[3].Treaties[1].War = true;
        Equal(true, war.JoinAllyWar(2, 1, 3, "vendetta-skips-date"));
    }

    private static void WarFailurePrefixes()
    {
        var f = new RevelationFixture(); var w = f.World; var notices = new WarNotices { FailNotice = true }; var war = War(f, notices);
        f.AddCity(0, 1, new(2, 2)); f.AddCity(1, 2, new(8, 4)); f.AddUnit(0, 1, 0, new(8, 4));
        w.Civilizations[2].ContinentCities[0] = 1; DiplomaticRelations.Set(w, 1, 2, new() { Alliance = true });
        Throws<InvalidOperationException>(() => war.Declare(1, 2));
        Equal(false, w.Civilizations[1].Treaties[2].Alliance); Equal(true, w.Civilizations[1].Treaties[2].Peace);
        Equal(new MapPoint(2, 2), f.Unit(0).Position); Equal((byte)1, w.Civilizations[1].Reputation);
        Equal(false, w.Civilizations[2].Treaties[1].Vendetta); Equal(1, notices.Redraws.Count);
        w.Globals.Multiplayer = GameMode.Tcp; w.Civilizations[3].ReputationCredit[1] = 7;
        Throws<InvalidOperationException>(() => war.Declare(1, 2, 3)); Equal((sbyte)7, w.Civilizations[3].ReputationCredit[1]);
    }
}
