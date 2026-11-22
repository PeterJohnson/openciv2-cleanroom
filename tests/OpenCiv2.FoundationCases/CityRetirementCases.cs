using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private sealed class RetirementNotices : ICityRetirementEffects
    {
        public List<int> Shortages { get; } = [];
        public List<(int Slot, bool Shortage)> Removed { get; } = [];
        public bool FailNotice;
        public void SupportShortage(CitySlot city, UnitTarget selected, UnitType type)
        {
            Shortages.Add(selected.Slot.Value);
            if (FailNotice) throw new IOException("notice delivery failed");
        }
        public void UnitRemoved(CitySlot city, UnitTarget selected, bool shortage) => Removed.Add((selected.Slot.Value, shortage));
    }
    private static CityRetirement Retirement(RevelationFixture f, RetirementNotices notices, LossPresentation? presentation = null) =>
        new(f.World, f.Rules, new(f.World, f.Rules, new(100000, 64), f), Loss(f, presentation ?? new()), notices);

    private static void RetirementForeignRestartAndTies()
    {
        var f = WorkingFixture(); var notices = new RetirementNotices();
        f.World.Civilizations[1].Government = Government.Republic;
        f.AddUnit(0, 1, 0, new(10, 6)); f.AddUnit(1, 1, 0, new(14, 6)); f.AddUnit(2, 2, 0, new(18, 6));
        for (int i = 0; i < 3; ++i) f.Unit(i).Home = new(0);
        var work = f.World.Calculation; work.Shields = 1; work.SupportedSettlers = 17; work.AggressiveUnits = 19;
        f.Unit(0).Flags.Aggressive = true;
        Retirement(f, notices).Run(new(0));
        Sequence(new[] { 2, 1 }, notices.Shortages); Equal(2, notices.Removed.Count);
        Equal(true, f.Unit(0).Token.Occupied && f.Unit(0).Flags.Aggressive);
        Equal(false, f.Unit(1).Token.Occupied || f.Unit(2).Token.Occupied);
        Equal(1, work.ShieldSupport); Equal(1, work.SupportedUnits); Equal(17, work.SupportedSettlers); Equal(19, work.AggressiveUnits);
        f = WorkingFixture(); notices = new(); f.World.Civilizations[1].Government = Government.Republic;
        f.AddUnit(0, 1, 0, new(14, 6)); f.AddUnit(1, 1, 0, new(8, 0));
        f.Unit(0).Home = f.Unit(1).Home = new(0); f.World.Calculation.Shields = 1;
        Retirement(f, notices).Run(new(0));
        Sequence(new[] { 0 }, notices.Shortages); Equal(true, f.Unit(1).Token.Occupied); // First weighted-distance tie.
    }
    private static void RetirementFreeUnitsAndCargo()
    {
        var f = WorkingFixture(); var notices = new RetirementNotices();
        f.World.Civilizations[1].Government = Government.Fundamentalism;
        f.Rules.Units[4] = f.Rules.Units[0] with { Abilities = new() { FundamentalismFreeSupport = true } };
        f.AddUnit(0, 1, 4, new(18, 6)); f.AddUnit(1, 1, 0, new(10, 6));
        f.Unit(0).Home = f.Unit(1).Home = new(0); f.World.Calculation.Shields = 0;
        Retirement(f, notices).Run(new(0));
        Sequence(new[] { 0, 1 }, notices.Shortages); Equal(0, f.World.Calculation.ShieldSupport);
        f = WorkingFixture(); notices = new(); var presentation = new LossPresentation();
        f.World.Civilizations[1].Government = Government.Republic;
        f.AddUnit(0, 1, 0, new(10, 6)); f.AddUnit(1, 1, 1, new(18, 6)); f.AddUnit(2, 2, 0, new(18, 6));
        // Supply an ordinary same-position physical stack with cargo of a different owner.
        f.Unit(1).Next = new(2); f.Unit(2).Previous = new(1); f.Unit(1).Previous = f.Unit(2).Next = null;
        f.Unit(0).Home = f.Unit(1).Home = new(0);
        f.World.Map.TileAt(new(18, 6)).Terrain = Terrain.Ocean; f.World.Calculation.Shields = 1;
        Retirement(f, notices, presentation).Run(new(0));
        Sequence(new[] { 1 }, notices.Shortages); Equal(false, f.Unit(1).Token.Occupied || f.Unit(2).Token.Occupied);
        Equal("changed:18,6;draw:18,6", string.Join(';', presentation.Calls)); Equal(1, f.World.Calculation.ShieldSupport);
    }
    private static void RetirementAffordableAndCoordination()
    {
        var f = WorkingFixture(); var notices = new RetirementNotices();
        f.World.Globals.HumanPlayers = default; f.World.Civilizations[1].Government = Government.Republic;
        ref var city = ref f.World.Entities.Cities.Storage[0];
        city.Flags.Disorder = city.Flags.SecondDisorderTurn = city.Flags.LongDisorder = city.Flags.MoodOnEdge = city.Flags.Celebrating = true;
        f.AddUnit(0, 1, 0, new(10, 6)); f.AddUnit(1, 1, 0, new(18, 6));
        f.Unit(0).Home = f.Unit(1).Home = new(0); f.World.Calculation.Shields = 9;
        Retirement(f, notices).Run(new(0));
        Equal(0, notices.Shortages.Count); Equal((1, false), notices.Removed.Single());
        Equal(false, city.Flags.Disorder || city.Flags.SecondDisorderTurn || city.Flags.LongDisorder);
        Equal(true, city.Flags.MoodOnEdge && city.Flags.Celebrating); Equal(1, f.Removed.Count); // Bare deletion sent its ordinary notification.
        f = WorkingFixture(); notices = new(); f.World.Globals.HumanPlayers = default;
        f.World.Civilizations[1].Government = Government.Republic; f.World.Entities.Cities.Storage[0].Flags.Disorder = true;
        f.AddUnit(0, 1, 0, new(10, 6)); f.AddUnit(1, 1, 2, new(18, 6));
        f.Unit(0).Home = f.Unit(1).Home = new(0); f.World.Calculation.Shields = 9;
        Retirement(f, notices).Run(new(0)); Equal(0, notices.Removed.Count); // Air wins; no land fallback.
        f.World.Globals.Status.AiCoordination = true; f.World.Globals.BestHumanRank = 3;
        f.World.Globals.CivByRank[7] = 4; f.World.Civilizations[3].NumAdvances = 4;
        Retirement(f, notices).Run(new(0));
        Equal((short)-999, f.World.Civilizations[1].GovernmentScore[(int)Government.Republic]);
        Equal((short)-999, f.World.Civilizations[1].GovernmentScore[(int)Government.Democracy]);
        Equal(0, notices.Removed.Count); Equal(true, f.World.Entities.Cities.Storage[0].Flags.Disorder);
        f.World.Civilizations[1].NumAdvances = 4; f.Unit(1).Type = new(0);
        Retirement(f, notices).Run(new(0)); Equal((1, false), notices.Removed.Single()); // Equality does not suppress retirement.
    }
    private static void RetirementProtectionAndHomeDomains()
    {
        var f = WorkingFixture(); var notices = new RetirementNotices();
        f.World.Globals.HumanPlayers = default; f.World.Civilizations[1].Government = Government.Republic;
        f.World.Entities.Cities.Storage[0].Flags.Disorder = true;
        f.AddCity(1, 2, new(18, 6));
        f.Rules.Units[5] = f.Rules.Units[0] with { Role = RuleRole.Settle };
        f.AddUnit(0, 1, 0, new(10, 6)); f.AddUnit(1, 1, 0, new(12, 6));
        f.AddUnit(2, 1, 0, new(18, 6)); f.AddUnit(3, 1, 5, new(16, 6));
        for (int i = 0; i < 4; ++i) f.Unit(i).Home = new(0);
        f.World.Map.TileAt(new(12, 6)).Features |= MapFeatures.Fortification;
        f.World.Calculation.Shields = 10;
        Retirement(f, notices).Run(new(0));
        Equal((0, false), notices.Removed.Single()); // Fortress, foreign city and settler are all protected.
        f = WorkingFixture(); notices = new(); f.AddCity(255, 1, new(12, 6));
        f.World.Civilizations[1].Government = Government.Republic;
        f.AddUnit(0, 1, 0, new(10, 6)); f.AddUnit(1, 1, 3, new(12, 6));
        f.Unit(0).Home = f.Unit(1).Home = null; f.Rules.Units[0] = f.Rules.Units[0] with { Role = (RuleRole)255 };
        f.World.Calculation.Shields = 0; Retirement(f, notices).Run(new(255));
        Equal(false, f.Unit(0).Token.Occupied); Equal(true, f.Unit(1).Token.Occupied); // Raw home255; signed role-1 qualifies, role6 does not.
    }
    private static void RetirementFailurePrefixes()
    {
        var f = WorkingFixture(); var notices = new RetirementNotices { FailNotice = true };
        f.World.Civilizations[1].Government = Government.Republic; f.AddUnit(0, 1, 0, new(10, 6)); f.Unit(0).Home = new(0);
        Throws<IOException>(() => Retirement(f, notices).Run(new(0)));
        Equal(true, f.Unit(0).Token.Occupied); Equal(1, f.World.Calculation.ShieldSupport);
        notices.FailNotice = false;
        Throws<IOException>(() => Retirement(f, notices, new() { FailRedraw = true }).Run(new(0)));
        Equal(false, f.Unit(0).Token.Occupied); Equal(1, f.World.Calculation.ShieldSupport); Equal(0, notices.Removed.Count);
        Equal(true, f.World.UnitDeletion.DestroyActive); // Loss was committed before presentation failed; restart did not run.
    }
}
