using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private sealed class StanceFixture
    {
        public RevelationFixture Data { get; } = new();
        public GameState World => Data.World;
        public DiplomaticStance Stance { get; }
        public List<GovernmentChangeNotice> GovernmentNotices { get; } = [];
        public StanceFixture(GameplayRandom? random = null)
        {
            var w = World; w.Globals.Difficulty = Difficulty.Prince; w.Globals.Turn = 100;
            w.Globals.PowerRank[1] = 3; w.Globals.PowerRank[2] = 4;
            w.Civilizations[1].Gold = 10000;
            w.Civilizations[1].MilitaryStrength = w.Civilizations[2].MilitaryStrength = 20;
            w.Civilizations[1].CityCount = w.Civilizations[2].CityCount = 2;
            w.Civilizations[2].Attitude[1] = 50;
            w.Civilizations[1].ContinentCities[1] = 1;
            w.Civilizations[1].ContinentMilitary[1] = w.Civilizations[2].ContinentMilitary[1] = 1;
            w.Civilizations[2].ContinentAttack[1] = 64;
            var changes = new GovernmentChanges(w, Data.Process.Rules, Data.Stacks,
                _ => throw new InvalidOperationException("Unexpected human government choice"),
                _ => throw new InvalidOperationException("Unexpected human rate choice"), GovernmentNotices.Add);
            Stance = new(w, Data.Process.Rules, new(w, Data.Process.Rules, changes, random ?? WarRandom()));
        }
        public void Evaluate() => Stance.Evaluate(1, 2, "stance");
    }

    private static void StanceRegionalPressureAndRetention()
    {
        var f = new StanceFixture(); var w = f.World; var e = w.Diplomacy;
        e.LastContact = -123; e.SelectedPair = (7, 6); w.Civilizations[1].LastContactTurn[2] = 80;
        f.Evaluate(); Equal(300, e.Demand); Equal(0, e.Fear); Equal(true, e.Aggressive); Equal(2, e.RequestMode);
        Equal(true, e.InitiallyAggressive); Equal(false, e.DeclareWar); Equal(50, e.Attitude);
        Equal(-123, e.LastContact); Equal(((int From, int To)?)(7, 6), e.SelectedPair);
        e.OpeningEvaluationActive = true; f.Evaluate(); Equal(80, e.LastContact);
        e.InitialEvaluationDone = true; e.DeclareWar = true; w.Civilizations[1].LastContactTurn[2] = 99;
        f.Evaluate(); Equal(80, e.LastContact); Equal(true, e.DeclareWar); Equal(30, e.Attitude); Equal(false, e.Aggressive);
        Equal(true, e.InitiallyAggressive); Equal(0, e.RequestMode); Equal(false, w.Civilizations[2].Treaties[1].War);
        w.Civilizations[1].Reputation = 7; f.Evaluate(); Equal(true, e.DeclareWar);
        w.Civilizations[2].Treaties[1].Alliance = true; f.Evaluate(); Equal(false, e.DeclareWar);

        f = new(); w = f.World; e = w.Diplomacy;
        w.Civilizations[2].ContinentCities[1] = 2; w.Civilizations[1].ContinentAttack[1] = 10;
        w.Civilizations[2].ContinentCities[63] = 2; w.Civilizations[1].ContinentAttack[63] = 10; w.Civilizations[2].ContinentMilitary[63] = 1;
        w.Civilizations[1].ContinentAttack[0] = 65535; w.Civilizations[2].ContinentCities[0] = 2;
        f.Evaluate(); Equal(20, e.Fear); Equal(0, e.Demand); Equal(0, e.RequestMode);
        w.Globals.Difficulty = Difficulty.Chieftain; f.Evaluate(); Equal(40, e.Fear);
        w.Civilizations[1].Reputation = 3; f.Evaluate(); Equal(20, e.Fear);
        Equal(false, e.Aggressive);
        Equal(0, new GameState(f.Data.Process).Diplomacy.Fear);
        w.Civilizations[1].Reputation = 0; w.Civilizations[1].ContinentAttack[1] = 65535; w.Civilizations[2].ContinentMilitary[1] = 0;
        w.Civilizations[1].ContinentAttack[63] = 0; w.Civilizations[2].ContinentCities[63] = 0;
        f.Evaluate(); Equal(262140, e.Fear); // Unsigned regional division does not narrow the contribution.
    }

    private static void StanceNuclearWonderAndNearbyUnits()
    {
        var f = new StanceFixture(); var w = f.World; var e = w.Diplomacy;
        w.Civilizations[1].UnitsActive[UnitTypeIds.NuclearMissile] = 1;
        f.Evaluate(); Equal(4, e.Fear); Equal(300, e.Demand); Equal(false, e.Aggressive); Equal(0, e.RequestMode);
        w.Civilizations[1].UnitsActive[UnitTypeIds.NuclearMissile] = 0; w.Civilizations[2].UnitsActive[UnitTypeIds.NuclearMissile] = 1;
        f.Evaluate(); Equal(450, e.Demand); Equal(true, w.Civilizations[2].Treaties[1].NuclearThreat);
        Equal(false, w.Civilizations[1].Treaties[2].NuclearThreat); Equal(true, e.Aggressive);
        w.Civilizations[1].UnitsActive[UnitTypeIds.NuclearMissile] = 2;
        f.Evaluate(); Equal(50, e.Demand); Equal(0, e.Fear); // 64/2, then 1/2, then the 50-point scale.

        f = new(); w = f.World; e = w.Diplomacy;
        w.Civilizations[1].Reputation = 3; w.Civilizations[2].ReputationCredit[1] = -2; w.Civilizations[1].Gold = 599;
        f.Evaluate(); Equal(70, e.Attitude); Equal(550, e.Demand);
        f = new(); w = f.World; e = w.Diplomacy;
        f.Data.AddCity(0, 1, new(8, 6)); w.Globals.WonderCity[WonderIds.GreatWall] = 0;
        w.Civilizations[1].Treaties[2].Incident = w.Civilizations[2].Treaties[1].Incident = true;
        f.Evaluate(); Equal(true, e.SuppressedAggression); Equal(false, e.Aggressive); Equal(300, e.Demand); Equal(40, e.Attitude);
        Equal(false, w.Civilizations[1].Treaties[2].Incident); Equal(false, w.Civilizations[2].Treaties[1].Incident);
        w.Globals.WonderCity[WonderIds.GreatWall] = -1;
        f.Data.AddUnit(0, 2, 0, new(9, 5)); f.Data.AddCity(1, 1, new(10, 4));
        f.Evaluate(); Equal(600, e.Demand); Equal(1, e.NearbyUnits);
        f.Data.AddUnit(1, 1, 0, new(8, 6)); f.Data.AddUnit(2, 1, 0, new(10, 4));
        f.Evaluate(); Equal(300, e.Demand); Equal(0, e.NearbyUnits);
        w.Civilizations[2].AggressionCounter[1] = 10; f.Evaluate(); Equal((byte)0, w.Civilizations[2].AggressionCounter[1]); Equal(false, e.InitiallyAggressive);
        f = new(); w = f.World; e = w.Diplomacy;
        f.Data.AddCity(0, 2, new(8, 6)); w.Globals.WonderCity[WonderIds.MarcoPolo] = 0;
        f.Evaluate(); Equal(350, e.Demand); // 64 + 64/4, then 3*80/32*50.
    }

    private static void StanceThirdPartiesAndDirection()
    {
        var f = new StanceFixture(); var w = f.World; var e = w.Diplomacy;
        w.Globals.Turn = 32; w.Civilizations[1].Treaties[2].Mercenary = w.Civilizations[2].Treaties[1].Mercenary = true;
        foreach (int k in new[] { 3, 4, 5 })
        {
            w.Civilizations[2].Treaties[k] = new() { War = true, Fighting = true };
            w.Civilizations[1].Treaties[k].Contact = true; w.Civilizations[k].MilitaryStrength = 30;
        }
        f.Evaluate(); Equal(5, e.WarTarget); Equal(3, e.StrongestEnemy); Equal(3, e.WeakestEnemy);
        Equal(6, e.PeaceInclination); Equal(false, e.Aggressive); Equal(true, e.InitiallyAggressive);
        Equal(2, e.RequestMode); Equal(0, e.Demand); Equal(false, w.Civilizations[1].Treaties[2].Mercenary);
        w.Civilizations[2].Treaties[3].Aggressor = true; w.Civilizations[1].Treaties[2].Mercenary = true;
        f.Evaluate(); Equal(true, w.Civilizations[1].Treaties[2].Mercenary);

        f = new(); w = f.World; e = w.Diplomacy;
        w.Civilizations[2].Attitude[1] = 255; f.Evaluate(); Equal(99, e.Attitude);
        w.Civilizations[1].Treaties[2].Alliance = true; f.Evaluate(); Equal(74, e.Attitude);
        w.Civilizations[1].Treaties[2] = new() { War = true }; w.Civilizations[2].Attitude[1] = 0;
        f.Evaluate(); Equal(26, e.Attitude); Equal((byte)0, w.Civilizations[2].Attitude[1]);
        w.Civilizations[2].Treaties[1].Alliance = true; w.Civilizations[1].Treaties[2] = default;
        f.Evaluate(); Equal(1, e.Attitude); // Signed negative evaluation is floored only after earlier stages.
        f = new(); w = f.World; e = w.Diplomacy; w.Civilizations[1].MilitaryStrength = 21;
        w.Civilizations[2].Treaties[1].War = true; f.Evaluate(); Equal(false, e.DeclareWar);
        for (int i = 0; i < 5; ++i)
        { f.Data.AddCity(i, 1, new(4 + 2 * i, 4)); w.Entities.Cities.Storage[i].Founder = new(2); }
        f.Evaluate(); Equal(5, e.CapturedCities); Equal(true, e.DeclareWar);
    }

    private static void StanceRaceAndScenarioOverrides()
    {
        var f = new StanceFixture(); var w = f.World; var e = w.Diplomacy;
        w.Globals.Difficulty = Difficulty.Emperor; w.Civilizations[1].SpaceshipFlags.Launched = true;
        w.Globals.CivsAlive = default; w.Globals.Rules.Bloodlust = true;
        f.Evaluate(); Equal(true, e.RaceThreat); Equal(true, e.OutranksTarget); Equal(false, e.DeclareWar);
        w.Civilizations[1].MilitaryStrength = 21; f.Evaluate(); Equal(true, e.DeclareWar); Equal(false, w.Civilizations[2].Treaties[1].War);
        w.Globals.Status.Scenario = true; w.Scenario.Flags.HistoricalAi = true;
        e.InitialEvaluationDone = true; e.DeclareWar = true;
        f.Stance.Evaluate(1, 3, "historical"); Equal(true, e.Aggressive); Equal(0, e.RequestMode); Equal(true, e.DeclareWar);
        f.Stance.Evaluate(1, 6, "historical-clear"); Equal(false, e.Aggressive);
        w.Globals.Status.Scenario = false; w.Globals.Status.ScenarioExport = true;
        f.Stance.Evaluate(1, 3, "export"); Equal(false, e.Aggressive);
        w.Globals.Multiplayer = GameMode.Tcp; e.Demand = 765;
        Throws<InvalidOperationException>(() => f.Evaluate()); Equal(765, e.Demand);
        f = new(); w = f.World; e = w.Diplomacy;
        w.Globals.PowerRank[2] = 8; w.Civilizations[1].ContinentMilitary[1] = 0;
        w.Civilizations[2].Treaties[1].Vendetta = true;
        Throws<DivideByZeroException>(() => f.Evaluate()); Equal(64, e.Demand); Equal(50, e.Attitude);
        Equal(false, w.Civilizations[2].Treaties[1].War);
    }

    private static void StanceGovernmentAndPatience()
    {
        var random = WarRandom(Choice.Create(new("A08.government-limit", "stance/government"), new SamplingLaw.PrimaryModulo(3), ChoiceCode.Primary(1)));
        var f = new StanceFixture(random); var w = f.World; var e = w.Diplomacy;
        e.OpeningEvaluationActive = true; w.Civilizations[2].Government = Government.Democracy;
        w.Civilizations[2].MilitaryStrength = 40; w.Civilizations[1].Treaties[2].Embassy = true;
        w.Civilizations[2].GovernmentScore[(int)Government.Despotism] = 20;
        f.Evaluate(); random.RequireChoicesConsumed(); Equal(Government.Despotism, w.Civilizations[2].Government);
        Equal(true, w.Civilizations[2].Flags.GovernmentChanged); Equal(1, f.GovernmentNotices.Count);
        Equal(2, e.RequestMode); Equal(true, e.InitiallyAggressive);
        f = new(); w = f.World; e = w.Diplomacy; e.OpeningEvaluationActive = true;
        w.Civilizations[2].Government = Government.Republic;
        f.Evaluate(); Equal(false, e.Aggressive); Equal(true, e.InitiallyAggressive); Equal(Government.Republic, w.Civilizations[2].Government);
        w.Civilizations[2].MilitaryStrength = 40; w.Globals.Status.Scenario = true; w.Scenario.Flags.RevolutionRestriction = true;
        f.Evaluate(); Equal(Government.Republic, w.Civilizations[2].Government); Equal(true, e.Aggressive);
        e.Attitude = 24; Equal(3, f.Stance.Patience(1, 2)); e.Attitude = 25; Equal(2, f.Stance.Patience(1, 2));
        e.Attitude = 60; Equal(2, f.Stance.Patience(1, 2)); e.Attitude = 61; Equal(1, f.Stance.Patience(1, 2));
        f.Data.AddCity(0, 1, new(4, 4)); w.Globals.WonderCity[WonderIds.EiffelTower] = 0;
        w.Civilizations[2].Treaties[1] = new() { Peace = true, Alliance = true };
        Equal(5, f.Stance.Patience(1, 2)); w.Civilizations[2].Treaties[1].War = true; Equal(2, f.Stance.Patience(1, 2));
    }

    private static void AllianceWillingnessOrderedOverrides()
    {
        var f = new StanceFixture(); var w = f.World; var e = w.Diplomacy; AllianceReasons reasons = AllianceReasons.None;
        f.Evaluate(); Equal(true, f.Stance.AllianceWilling(1, 2, ref reasons)); // Weak rank / late turn overrides the attitude-category gate.
        Equal(AllianceReasons.None, reasons); Equal(false, w.Civilizations[1].Treaties[2].Alliance);
        w.Civilizations[1].Reputation = 5; Equal(false, f.Stance.AllianceWilling(1, 2, ref reasons));
        w.Civilizations[1].Reputation = 0; w.Civilizations[2].DiploRequestCount = 5;
        Equal(false, f.Stance.AllianceWilling(1, 2, ref reasons));
        w.Globals.Difficulty = Difficulty.Chieftain; Equal(true, f.Stance.AllianceWilling(1, 2, ref reasons));
        e.Attitude = 76; Equal(false, f.Stance.AllianceWilling(1, 2, ref reasons));

        f = new(); w = f.World; e = w.Diplomacy; e.Attitude = 50; e.WarTarget = 3;
        w.Civilizations[1].Treaties[3] = new() { Contact = true, Peace = true };
        w.Civilizations[2].Treaties[3].Fighting = true; w.Civilizations[3].MilitaryStrength = 30;
        reasons = AllianceReasons.ExistingAlliedTarget;
        Equal(true, f.Stance.AllianceWilling(1, 2, ref reasons)); Equal(AllianceReasons.DangerousEnemy, reasons);
        w.Civilizations[2].Treaties[1].Vendetta = true;
        Equal(false, f.Stance.AllianceWilling(1, 2, ref reasons)); Equal(AllianceReasons.DangerousEnemy, reasons);
        w.Civilizations[1].Treaties[3].Alliance = true;
        Equal(true, f.Stance.AllianceWilling(1, 2, ref reasons));
        Equal(AllianceReasons.DangerousEnemy | AllianceReasons.ExistingAlliedTarget, reasons);
        e.Attitude = 76; Equal(false, f.Stance.AllianceWilling(1, 2, ref reasons));
        Equal(AllianceReasons.DangerousEnemy | AllianceReasons.ExistingAlliedTarget, reasons);
    }

    private static void AllianceCommonTargetSelection()
    {
        var f = new StanceFixture(); var w = f.World; var e = w.Diplomacy; AllianceReasons reasons = AllianceReasons.None;
        e.Attitude = 38; w.Globals.CivsAlive = default;
        w.Globals.PowerRank[3] = w.Globals.PowerRank[4] = 5;
        w.Civilizations[1].Treaties[3].Contact = true; w.Civilizations[1].Treaties[4].Contact = true;
        Equal(true, f.Stance.AllianceWilling(1, 2, ref reasons)); Equal(3, e.WarTarget); Equal(AllianceReasons.NewCommonTarget, reasons);
        e.WarTarget = null; reasons = AllianceReasons.None;
        w.Civilizations[1].Treaties[3].Contact = false; w.Civilizations[2].Treaties[3].Contact = true;
        Equal(true, f.Stance.AllianceWilling(1, 2, ref reasons)); Equal(4, e.WarTarget);
        // An existing non-top target replaces the search result without rerunning the candidate gates.
        e.WarTarget = 5; reasons = AllianceReasons.None; w.Globals.PowerRank[5] = 1;
        w.Civilizations[1].Treaties[5].War = true; w.Civilizations[2].Treaties[5].Alliance = true;
        Equal(true, f.Stance.AllianceWilling(1, 2, ref reasons)); Equal(5, e.WarTarget);
        w.Globals.PowerRank[5] = 7; reasons = AllianceReasons.None;
        Equal(true, f.Stance.AllianceWilling(1, 2, ref reasons)); Equal(4, e.WarTarget);
        e.WarTarget = 0; e.Attitude = 62; w.Civilizations[2].Treaties[1].Vendetta = true;
        w.Civilizations[1].Treaties[0].Alliance = true;
        Equal(true, f.Stance.AllianceWilling(1, 2, ref reasons)); Equal(0, e.WarTarget);
        Equal(true, reasons.HasFlag(AllianceReasons.ExistingAlliedTarget));
    }
}
