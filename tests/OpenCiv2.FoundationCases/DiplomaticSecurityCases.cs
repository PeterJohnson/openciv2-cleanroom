using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static StanceFixture SecurityFixture()
    {
        var f = new StanceFixture(); var w = f.World;
        w.Globals.HumanPlayers = default;
        w.Civilizations[1].ContinentCities = default;
        w.Civilizations[1].ContinentMilitary = default;
        w.Civilizations[2].ContinentMilitary = default;
        w.Civilizations[2].ContinentAttack = default;
        w.Civilizations[1].Tribe = 1;
        f.Data.Process.Rules.Leaders[1].Attack = 0;
        return f;
    }

    private static void SecurityShortcutsAndHumanDirection()
    {
        var f = SecurityFixture(); var w = f.World;
        w.Civilizations[2].UnitsActive[UnitTypeIds.NuclearMissile] = 1;
        Equal(false, f.Stance.FeelsSecure(1, 2));
        w.Civilizations[2].Treaties[1].Aggressor = true;
        Equal(false, f.Stance.FeelsSecure(1, 2));
        w.Civilizations[1].Treaties[2].Aggressor = true;
        Equal(true, f.Stance.FeelsSecure(1, 2));
        w.Civilizations[1].Treaties[2] = new() { Vendetta = true };
        Equal(true, f.Stance.FeelsSecure(1, 2));
        w.Globals.Multiplayer = GameMode.Tcp;
        Throws<InvalidOperationException>(() => f.Stance.FeelsSecure(1, 2));

        foreach (bool reverse in new[] { false, true })
        {
            f = SecurityFixture(); w = f.World;
            int hostile = reverse ? 2 : 1, protectedCiv = reverse ? 1 : 2;
            w.Globals.HumanPlayers = CivilizationSet.Only(3); // Dead human slots still count.
            w.Civilizations[3].MilitaryStrength = 21;
            w.Civilizations[hostile].Treaties[3].War = true;
            Equal(false, f.Stance.FeelsSecure(1, 2));
            w.Civilizations[protectedCiv].Treaties[3].Alliance = true;
            Equal(false, f.Stance.FeelsSecure(1, 2)); // Alliance direction matters.
            w.Civilizations[3].Treaties[protectedCiv].Alliance = true;
            Equal(true, f.Stance.FeelsSecure(1, 2));
            w.Civilizations[3].Treaties[protectedCiv].Alliance = false;
            w.Civilizations[3].MilitaryStrength = 20;
            Equal(true, f.Stance.FeelsSecure(1, 2));
        }
        f = SecurityFixture(); w = f.World;
        w.Globals.HumanPlayers = CivilizationSet.Only(1);
        w.Civilizations[1].MilitaryStrength = 21;
        w.Civilizations[1].Treaties[1].War = true;
        Equal(false, f.Stance.FeelsSecure(1, 2)); // The human scan does not exclude C.
    }

    private static void SecurityRegionalBoundariesAndRatios()
    {
        foreach (int region in new[] { 0, 1, 62, 63 })
        {
            var f = SecurityFixture(); var w = f.World;
            w.Civilizations[1].ContinentCities[region] = 1;
            w.Civilizations[2].ContinentAttack[region] = 2;
            w.Civilizations[1].ContinentMilitary[region] = 1;
            Equal(region is 0 or 63, f.Stance.FeelsSecure(1, 2));
            w.Civilizations[1].ContinentAttack[region] = 1;
            Equal(true, f.Stance.FeelsSecure(1, 2)); // Equality passes the local defense gate.
        }
        var sample = SecurityFixture(); var world = sample.World;
        world.Civilizations[1].ContinentCities[1] = 1;
        world.Civilizations[1].ContinentAttack[1] = 5;
        world.Civilizations[2].ContinentMilitary[1] = 3;
        Equal(true, sample.Stance.FeelsSecure(1, 2)); // 20 / (1+3) = 5.
        world.Civilizations[1].ContinentAttack[1] = 4;
        Equal(false, sample.Stance.FeelsSecure(1, 2));
        world.Civilizations[1].ContinentCities[1] = 0;
        Equal(true, sample.Stance.FeelsSecure(1, 2));
        world.Civilizations[1].ContinentCities[1] = 1;
        world.Civilizations[1].ContinentAttack[1] = 5;
        world.Civilizations[2].ContinentAttack[1] = 3;
        Equal(false, sample.Stance.FeelsSecure(1, 2)); // 20 / (1+(3>>1)+3) = 4.
        world.Civilizations[1].ContinentAttack[62] = 2;
        world.Civilizations[2].ContinentMilitary[62] = 1;
        Equal(false, sample.Stance.FeelsSecure(1, 2)); // 28/6 truncates to 4.
        world.Civilizations[1].ContinentAttack[62] = 3;
        Equal(true, sample.Stance.FeelsSecure(1, 2)); // 32/6 = 5, even with no cities in region 62.
    }

    private static void SecurityPoliticalThresholdsAndPurity()
    {
        var f = SecurityFixture(); var w = f.World;
        w.Civilizations[1].ContinentCities[1] = 1;
        w.Civilizations[1].ContinentAttack[1] = 5;
        w.Civilizations[2].ContinentMilitary[1] = 3;
        w.Diplomacy.Attitude = -77;
        w.Civilizations[1].Treaties[2].Alliance = true;
        Equal(false, f.Stance.FeelsSecure(1, 2)); // Alliance raises the threshold to 6.
        f.Data.Process.Rules.Leaders[1].Attack = 1;
        Equal(true, f.Stance.FeelsSecure(1, 2));
        Equal(-77, w.Diplomacy.Attitude); Equal(true, w.Civilizations[1].Treaties[2].Alliance);
        w.Civilizations[1].Treaties[2] = default;
        f.Data.Process.Rules.Leaders[1].Attack = 0;
        w.Civilizations[1].Treaties[3].War = true;
        Equal(false, f.Stance.FeelsSecure(1, 2));
        w.Civilizations[1].Treaties[2].War = true;
        Equal(true, f.Stance.FeelsSecure(1, 2)); // Hostility to D removes its non-hostile bonus.
        w.Civilizations[1].Treaties[2] = default;
        w.Civilizations[1].Treaties[3] = new() { Alliance = true };
        w.Civilizations[2].Treaties[3].Alliance = true;
        w.Globals.HumanPlayers = CivilizationSet.Only(3);
        Equal(false, f.Stance.FeelsSecure(1, 2));
        w.Civilizations[1].ContinentAttack[1] = 6;
        Equal(true, f.Stance.FeelsSecure(1, 2));
        w.Globals.PowerRank[3] = 7;
        Equal(false, f.Stance.FeelsSecure(1, 2)); // A shared human ally outranks both.
        w.Civilizations[2].Treaties[3].War = true;
        Equal(true, f.Stance.FeelsSecure(1, 2)); // Alliance and War terms are independent.
        w.Civilizations[1].ContinentAttack[1] = 5;
        w.Globals.Turn = 199;
        Equal(false, f.Stance.FeelsSecure(1, 2));
        w.Globals.Turn = 200;
        Equal(true, f.Stance.FeelsSecure(1, 2)); // The leader-war subtraction is independently applied.
        Equal(-77, w.Diplomacy.Attitude); Equal(0, f.GovernmentNotices.Count);
    }

    private static void MilitaryAidContradictoryBounds()
    {
        foreach (bool flat in new[] { false, true })
        {
            var f = new RevelationFixture(); var w = f.World;
            w.Globals.Rules.FlatWorld = flat;
            w.Civilizations[1].Treaties[2].Alliance = true;
            w.Civilizations[2].Treaties[3] = new() { War = true, Fighting = true };
            w.Civilizations[1].MilitaryStrength = w.Civilizations[2].MilitaryStrength = w.Civilizations[3].MilitaryStrength = 20;
            f.AddCity(0, 1, new(4, 4));
            f.AddUnit(0, 1, 0, new(4, 4));
            f.AddUnit(1, 1, 1, new(6, 4));
            f.AddUnit(2, 1, 3, new(8, 4));
            f.Rules.Units[4] = f.Rules.Units[0] with { Role = (RuleRole)255 };
            f.AddUnit(3, 1, 4, new(10, 4));
            f.Unit(3).Position = new(-2, 4);
            w.Map.TileAt(new(-2, 4)).Features |= MapFeatures.CitySite;
            w.Map.TileAt(new(-2, 4)).Owner = 1; // Even a city-marked fallback cannot admit aid.
            Equal((byte?)null, w.Map.CityOwner(new(-2, 4)));
            var before = w.Entities.Units.Scanned.ToArray();
            MilitaryAid.Run(w, f.Rules, 1);
            Equal(true, before.SequenceEqual(w.Entities.Units.Scanned.ToArray()));
            Equal(true, w.Civilizations[1].Treaties[2].Alliance);
            w.Globals.Multiplayer = GameMode.Tcp;
            Throws<InvalidOperationException>(() => MilitaryAid.Run(w, f.Rules, 1));
        }
    }
}
