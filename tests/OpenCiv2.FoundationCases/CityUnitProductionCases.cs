using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private sealed class UnitProductionFixture : ICityUnitProductionContinuations, ICityUnitProductionEffects
    {
        public EliminationFixture Lifecycle { get; } = new();
        public RevelationFixture Data => Lifecycle.Data;
        public GameState World => Data.World;
        public CityUnitProduction Production { get; }
        public List<(UnitType Old, UnitType New)> Upgrades { get; } = [];
        public List<CityTarget> Removed { get; } = [];
        public List<UnitType> BuiltTypes { get; } = [];
        public List<string> Order { get; } = [];
        public bool Abandon;
        public int AbandonRequests, Checks, Choices, CargoSelections;
        public Action<CitySlot>? Choose;
        public Action<UnitSlot>? Cargo;
        public List<AlmostWonderNotice> AlmostWonders { get; } = [];

        public UnitProductionFixture()
        {
            Data.AddCity(0, 1, new(10, 10), 3);
            World.Civilizations[1].CityCount = 1;
            World.Civilizations[1].Government = Government.Monarchy;
            World.Globals.Difficulty = Difficulty.Prince;
            Data.Rules.Units[4] = Data.Rules.Units[4] with { CostRows = 2, Prerequisite = AdvanceReference.None };
            World.Entities.Cities.Storage[0].Production = 4;
            World.Entities.Cities.Storage[0].Flags.DefenderLost = true;
            World.Calculation.Shields = 5; World.Calculation.ShieldSupport = 1; World.Calculation.ShieldRows = 10;
            var creator = new UnitCreation(World, Data.Process, Data.Stacks, Data);
            var removal = new CityRemoval(World, Data.Rules, Data.Stacks, Lifecycle.Loss, Removed.Add);
            Production = new(World, Data.Rules, creator, Lifecycle.Deletion, removal, Lifecycle.Random, this, this);
        }
        public ref City City => ref World.Entities.Cities.Storage[0];
        public UnitProductionResult Run() => Production.Run(new(0), "production");
        public void ReconsiderProduction(CitySlot city) { ++Choices; Order.Add("choose"); Choose?.Invoke(city); }
        public void ChooseTradeCargo(CitySlot city, int owner, UnitSlot unit) { ++CargoSelections; Order.Add("cargo"); Cargo?.Invoke(unit); }
        public int CheckCivilization(int owner, int killer) { ++Checks; Order.Add("eliminate"); return Lifecycle.Elimination.Check(owner, killer, "production-elimination"); }
        public bool AbandonCity(CitySlot city, UnitType type) { ++AbandonRequests; return Abandon; }
        public void Upgraded(CitySlot city, UnitType oldType, UnitType newType) => Upgrades.Add((oldType, newType));
        public void Built(CityTarget city, UnitType type) { BuiltTypes.Add(type); Order.Add("built"); }
        public void Redraw(MapPoint position) => Order.Add("draw");
        public void AlmostWonder(AlmostWonderNotice notice) => AlmostWonders.Add(notice);
    }

    private static void UnitProductionStocksAndCapacity()
    {
        var f = new UnitProductionFixture(); f.City.ShieldBox = 15;
        Equal(UnitProductionOutcome.NotReady, f.Run().Outcome); Equal((short)19, f.City.ShieldBox);
        Equal(false, f.City.Flags.DefenderLost); Equal(0, f.World.Entities.Units.End);
        f.City.ShieldBox = 29;
        var result = f.Run(); Equal(UnitProductionOutcome.Completed, result.Outcome);
        Equal((short)0, f.City.ShieldBox); Equal(new UnitSlot(0), result.Unit!.Value.Slot);
        Equal((CitySlot?)new CitySlot(0), f.Data.Unit(0).Home); Equal(f.City.Position, f.Data.Unit(0).Position);
        Equal((byte)1, f.World.Civilizations[1].UnitsActive[4]); Equal(0, f.BuiltTypes.Count);
        Equal(true, f.World.Map.IsRevealedTo(new(12, 10), 1));
        f = new(); f.World.Civilizations[1].MilitaryCount = 1949; f.City.ShieldBox = 29;
        Equal(UnitProductionOutcome.CreationFailed, f.Run().Outcome); Equal((short)13, f.City.ShieldBox);
        Equal(UnitCreationNotice.TooManyUnits, f.Data.Notices.Single()); Equal(0, f.World.Entities.Units.End);
        f.Data.Rules.Units[4] = f.Data.Rules.Units[4] with { CostRows = 255 };
        f.World.Calculation.Shields = 0; f.World.Calculation.ShieldSupport = 0; f.City.ShieldBox = -1;
        Equal(UnitProductionOutcome.CreationFailed, f.Run().Outcome); Equal((short)9, f.City.ShieldBox); // Signed cost -1 *10.
        f.City.ShieldBox = short.MaxValue; f.World.Calculation.Shields = 1;
        Equal(UnitProductionOutcome.NotReady, f.Run().Outcome); Equal(short.MinValue, f.City.ShieldBox);
        f.Lifecycle.Random.RequireChoicesConsumed();
    }

    private static void UnitProductionDisorderAndBarbarianStocks()
    {
        var f = new UnitProductionFixture(); f.City.Flags.Disorder = true; f.City.ShieldBox = 19;
        Equal(UnitProductionOutcome.NotReady, f.Run().Outcome); Equal((short)19, f.City.ShieldBox);
        f.City.ShieldBox = 20; Equal(UnitProductionOutcome.Completed, f.Run().Outcome); // Disorder does not block completion of existing stock.
        f = new(); f.City.Owner = new(0); f.World.Civilizations[0].ContinentMilitary[1] = 17;
        f.City.ShieldBox = 19; f.Data.AddUnit(0, 2, 4, f.City.Position);
        Equal(UnitProductionOutcome.NotReady, f.Run().Outcome); Equal((short)0, f.City.ShieldBox);
        Equal(1, f.Choices); Equal(0, f.World.Civilizations[0].Gold); // Any occupying unit owner activates the barbarian reset.
        f.World.Civilizations[0].ContinentMilitary[1] = 16; f.City.ShieldBox = 19;
        Equal(UnitProductionOutcome.Completed, f.Run().Outcome); Equal((short)3, f.City.ShieldBox);
        Equal((byte)0, f.World.Entities.Units.Storage[1].Owner.Value);
        f = new(); f.City.Production = -BuildingIds.Temple; f.City.ShieldBox = 8;
        Throws<ArgumentOutOfRangeException>(() => f.Run()); Equal((short)8, f.City.ShieldBox);
        f.City.Production = 4; f.World.Globals.Multiplayer = GameMode.Tcp;
        Throws<InvalidOperationException>(() => f.Run()); Equal((short)8, f.City.ShieldBox);
    }

    private static void UnitProductionFirstUpgradeAndNoChain()
    {
        var f = new UnitProductionFixture(); f.City.ShieldBox = 36;
        f.World.Civilizations[1].Advances = default(AdvanceSet).With(0).With(1);
        f.World.Civilizations[1].UnitsInProduction[4] = 2;
        f.Data.Rules.Units[4] = f.Data.Rules.Units[4] with { ObsoleteBy = (AdvanceReference)0 };
        f.Data.Rules.Units[1] = f.Data.Rules.Units[4] with { Domain = RuleDomain.Sea, Prerequisite = (AdvanceReference)0, ObsoleteBy = (AdvanceReference)1, CostRows = 4 };
        f.Data.Rules.Units[2] = f.Data.Rules.Units[1] with { CostRows = 1 };
        f.Data.Rules.Units[5] = f.Data.Rules.Units[1] with { Prerequisite = (AdvanceReference)1 };
        Equal(false, UnitAvailability.CanBuild(f.World, f.Data.Rules, 1, new(0), new(1)));
        var result = f.Run(); Equal(UnitProductionOutcome.Completed, result.Outcome);
        Equal((sbyte)1, f.City.Production); Equal(new UnitType(1), f.Data.Unit(result.Unit!.Value.Slot.Value).Type);
        Equal((new UnitType(4), new UnitType(1)), f.Upgrades.Single());
        Equal((byte)2, f.World.Civilizations[1].UnitsInProduction[4]); Equal((byte)0, f.World.Civilizations[1].UnitsInProduction[1]);
        Equal((short)0, f.City.ShieldBox); // Direct upgrade ignores availability and production-switch census semantics.
    }

    private static void UnitProductionVeteransAndTransport()
    {
        foreach (var (domain, role, government, normal, building, wonder, disposable, expected) in new[]
        {
            (RuleDomain.Land, RuleRole.Attack, Government.Monarchy, true, 2, -1, false, true),
            (RuleDomain.Land, RuleRole.Attack, Government.Monarchy, true, 0, 7, false, true),
            (RuleDomain.Land, RuleRole.Attack, Government.Monarchy, true, 2, 7, true, false),
            (RuleDomain.Land, RuleRole.Diplomacy, Government.Monarchy, true, 2, 7, false, false),
            (RuleDomain.Land, RuleRole.Diplomacy, Government.Communism, true, 0, -1, true, true),
            (RuleDomain.Sea, RuleRole.SeaTransport, Government.Monarchy, true, 34, -1, false, true),
            (RuleDomain.Sea, RuleRole.NavalSuperiority, Government.Monarchy, true, 0, 3, false, true),
            (RuleDomain.Sea, RuleRole.NavalSuperiority, Government.Monarchy, false, 34, 3, false, false),
            (RuleDomain.Sea, RuleRole.NavalSuperiority, Government.Monarchy, false, 2, -1, false, true),
            (RuleDomain.Air, RuleRole.AirSuperiority, Government.Monarchy, true, 32, -1, false, true),
            (RuleDomain.Air, RuleRole.AirSuperiority, Government.Monarchy, true, 32, -1, true, false),
            (RuleDomain.Air, RuleRole.AirSuperiority, Government.Monarchy, false, 2, -1, false, true)
        })
        {
            var f = new UnitProductionFixture(); f.City.ShieldBox = 16;
            f.Data.Rules.Units[4] = f.Data.Rules.Units[4] with { Domain = domain, Role = role, Abilities = new() { DestroyedAfterAttack = disposable } };
            f.World.Civilizations[1].Government = government; f.World.Globals.Rules.NormalCombat = normal;
            if (building != 0) BuildingQueries.SetBuilding(f.World, new(0), building, true);
            if (wonder >= 0) f.World.Globals.WonderCity[wonder] = 0;
            var result = f.Run(); Equal(UnitProductionOutcome.Completed, result.Outcome);
            Equal(expected, f.Data.Unit(result.Unit!.Value.Slot.Value).Flags.Veteran);
            Equal(domain == RuleDomain.Sea && role == RuleRole.SeaTransport ? (byte)255 : (byte)0, f.Data.Unit(0).Counter);
        }
        var transport = new UnitProductionFixture();
        transport.Data.AddCity(130, 1, new(14, 10));
        transport.World.Entities.Cities.Storage[130].Production = 4; transport.World.Entities.Cities.Storage[130].ShieldBox = 16;
        transport.Data.Rules.Units[4] = transport.Data.Rules.Units[4] with { Domain = RuleDomain.Sea, Role = RuleRole.SeaTransport };
        transport.Production.Run(new(130), "transport"); Equal((byte)253, transport.Data.Unit(0).Counter);
    }

    private static void UnitProductionSettlerRefusalAndPopulation()
    {
        var f = new UnitProductionFixture(); f.Data.Rules.Units[4] = f.Data.Rules.Units[4] with { Role = RuleRole.Settle };
        f.City.Size = 1; f.City.ShieldBox = 23; f.City.Flags.WonderBuilt = true;
        f.World.Globals.Difficulty = Difficulty.Chieftain;
        Equal(UnitProductionOutcome.ChieftainRefusal, f.Run().Outcome); Equal((short)27, f.City.ShieldBox);
        Equal(0, f.World.Entities.Units.End); Equal(false, f.City.Flags.WonderBuilt); Equal(true, f.City.Flags.DefenderLost);
        f.World.Globals.Difficulty = Difficulty.Prince;
        var last = f.Run(); Equal(UnitProductionOutcome.LastCitySettler, last.Outcome);
        Equal((short)11, f.City.ShieldBox); Equal((sbyte)1, f.City.Size); Equal(true, f.Data.Unit(0).Token.Occupied);
        Equal(0, f.AbandonRequests); Equal(0, f.BuiltTypes.Count); Equal(true, f.City.Flags.DefenderLost);
        f.City.Size = 2; f.City.ShieldBox = 16;
        Equal(UnitProductionOutcome.Completed, f.Run().Outcome); Equal((sbyte)1, f.City.Size); Equal((short)0, f.City.ShieldBox);
        Equal(new UnitType(4), f.BuiltTypes.Single()); Equal(false, f.City.Flags.DefenderLost);
        f.City.Size = sbyte.MinValue; f.City.ShieldBox = 16;
        Equal(UnitProductionOutcome.Completed, f.Run().Outcome); Equal(sbyte.MaxValue, f.City.Size);
        Equal(0, f.Removed.Count);
    }

    private static void UnitProductionSettlerCancellationAndRemoval()
    {
        foreach (bool autoplay in new[] { false, true })
        {
            var f = new UnitProductionFixture(); f.Data.AddCity(1, 1, new(20, 10));
            f.World.Civilizations[1].CityCount = 2; f.World.Multiplayer.Saved.AutoplayActive = autoplay;
            f.Data.Rules.Units[4] = f.Data.Rules.Units[4] with { Role = RuleRole.Settle };
            f.City.Size = 1; f.City.ShieldBox = 23;
            Equal(UnitProductionOutcome.SettlerCancelled, f.Run().Outcome); Equal((short)7, f.City.ShieldBox);
            Equal(true, f.City.Token.Occupied); Equal((sbyte)1, f.City.Size); Equal(0, f.World.Entities.Units.End);
            Equal(autoplay ? 0 : 1, f.AbandonRequests); Equal(true, f.City.Flags.DefenderLost);
            Equal(0, f.Removed.Count); Equal((uint)2, f.Data.Process.Tokens.NextUnit);
        }
        var accepted = new UnitProductionFixture(); accepted.Data.AddCity(1, 1, new(20, 10));
        accepted.World.Civilizations[1].CityCount = 2; accepted.Abandon = true;
        accepted.Data.Rules.Units[4] = accepted.Data.Rules.Units[4] with { Role = RuleRole.Settle };
        accepted.City.Size = 1; accepted.City.ShieldBox = 16;
        Equal(UnitProductionOutcome.CityDisbanded, accepted.Run().Outcome);
        Equal(false, accepted.City.Token.Occupied); Equal((short)1, accepted.World.Civilizations[1].CityCount);
        Equal(1, accepted.Removed.Count); Equal(1, accepted.Checks);
        Equal((uint)3, accepted.Data.Process.Tokens.NextUnit); Equal(1, accepted.World.Entities.Units.End);
        Equal((uint)2, accepted.Data.Unit(0).Token.Value); Equal((CitySlot?)new CitySlot(1), accepted.Data.Unit(0).Home);
        Equal(new MapPoint(10, 10), accepted.Data.Unit(0).Position); Equal(false, accepted.World.Map.TileAt(new(10, 10)).City);
        Equal(EndReason.None, accepted.World.PendingEndReason); Sequence(new[] { "eliminate", "built", "draw" }, accepted.Order);
        accepted.Lifecycle.Random.RequireChoicesConsumed();
    }

    private static void UnitProductionAiDiplomatsAndNuclearContact()
    {
        foreach (bool failure in new[] { false, true })
        {
            var f = new UnitProductionFixture(); f.World.Globals.HumanPlayers = CivilizationSet.Only(2);
            f.World.Civilizations[1].Government = Government.Communism;
            f.Data.Rules.Units[4] = f.Data.Rules.Units[4] with { Role = RuleRole.Diplomacy };
            f.City.ShieldBox = 23; f.Data.AddCity(1, 3, new(16, 10));
            f.Data.AddUnit(0, 1, 47, new(14, 10));
            if (failure) f.World.Civilizations[1].MilitaryCount = 1949;
            var result = f.Run(); Equal(UnitProductionOutcome.Completed, result.Outcome);
            Equal(failure, result.Unit is null); Equal((short)7, f.City.ShieldBox);
            if (!failure)
            {
                Equal(f.City.Position, f.Data.Unit(result.Unit!.Value.Slot.Value).Position);
                Equal(true, f.Data.Unit(result.Unit.Value.Slot.Value).Flags.Veteran);
            }
            Equal(2, f.Choices); Equal(0, f.World.Civilizations[1].Gold); Equal(false, f.City.Flags.DefenderLost);
            Equal(false, f.Data.Unit(0).Flags.Veteran); // Failed creation never touches a neighboring unit.
            f.Lifecycle.Random.RequireChoicesConsumed(); // The unusual distance comparison does not create a placement draw.
        }
        var nuclear = new UnitProductionFixture(); nuclear.World.Globals.HumanPlayers = CivilizationSet.Only(2);
        nuclear.Data.Rules.Units[4] = nuclear.Data.Rules.Units[4] with { Attack = 99 };
        nuclear.City.ShieldBox = 16;
        foreach (ref var civ in nuclear.World.Civilizations.AsSpan()) civ.LastContactTurn[1] = 77;
        nuclear.Run(); Equal((short)77, nuclear.World.Civilizations[0].LastContactTurn[1]);
        for (int c = 1; c < 8; ++c) Equal((short)-1, nuclear.World.Civilizations[c].LastContactTurn[1]);
        nuclear.World.Civilizations[2].LastContactTurn[1] = 31; nuclear.City.ShieldBox = 16;
        nuclear.Run(); Equal((short)31, nuclear.World.Civilizations[2].LastContactTurn[1]);
    }

    private static void UnitProductionPolicyContinuationsAndFailures()
    {
        var f = new UnitProductionFixture(); f.City.ShieldBox = 23;
        f.Data.Rules.Units[4] = f.Data.Rules.Units[4] with { Role = RuleRole.Trade };
        f.Cargo = _ => throw new InvalidOperationException("Cargo choice failed");
        Throws<InvalidOperationException>(() => f.Run());
        Equal((short)7, f.City.ShieldBox); Equal(true, f.Data.Unit(0).Token.Occupied);
        Equal(true, f.City.Flags.DefenderLost); Equal(1, f.CargoSelections);
        f = new(); f.City.ShieldBox = 16;
        f.City.Flags.Autobuild = f.City.Flags.UnitsOnlyAutomation = f.City.Flags.ImprovementsOnlyAutomation = true;
        f.Choose = city => f.World.Entities.Cities.At(city).Production = 99;
        f.Run(); Equal(2, f.Choices); // Success branch and common tail each request reconsideration.
        Equal(false, f.City.Flags.Autobuild || f.City.Flags.UnitsOnlyAutomation || f.City.Flags.ImprovementsOnlyAutomation);
        f = new(); f.World.Globals.HumanPlayers = CivilizationSet.Only(2); f.City.ShieldBox = 16;
        f.World.Civilizations[1].Gold = 512; f.World.Globals.PowerRank[f.World.Globals.BestHumanRank] = 8;
        Throws<DivideByZeroException>(() => f.Run());
        Equal((short)8, f.City.ShieldBox); Equal(512, f.World.Civilizations[1].Gold);
        Equal(true, f.Data.Unit(0).Token.Occupied);
        Equal(true, f.City.Flags.DefenderLost); Sequence(new[] { "choose", "choose" }, f.Order);
    }
}
