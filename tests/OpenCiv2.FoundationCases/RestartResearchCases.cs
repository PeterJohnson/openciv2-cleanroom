using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // V01 §3 and V03 §10: inactive restart grants still propagate knowledge and complete real C08 children.
    private static void RestartResearchLibraryAndUpgrade()
    {
        var f = CreationFixture(); var w = f.World;
        w.Globals.Turn = 25; w.Globals.Difficulty = Difficulty.Prince;
        w.Globals.HumanPlayers = CivilizationSet.Only(1).With(4); w.Globals.CivsAlive = CivilizationSet.Only(1).With(2);
        w.Globals.RevealMap = 1; // Observer visibility does not override the temporarily inactive recipient.
        f.Process.Rules.Leaders[2].Color = 2; f.AddCity(0, 1, new(30, 20));
        w.Globals.WonderCity[WonderIds.GreatLibrary] = w.Globals.WonderCity[WonderIds.Leonardo] = 0;
        f.Rules.Units[4] = f.Rules.Units[0] with { Role = RuleRole.Attack, ObsoleteBy = (AdvanceReference)5 };
        f.Rules.Units[61] = f.Rules.Units[4] with { Prerequisite = (AdvanceReference)5, ObsoleteBy = AdvanceReference.Never };
        f.AddUnit(0, 1, 4, new(30, 20)); f.Unit(0).Flags.Veteran = true;
        w.Civilizations[2].UnitsActive[4] = 7; w.Civilizations[2].UnitsLost[4] = 9;
        w.Civilizations[2].Researching = 5;
        foreach (int a in new[] { 5, 6 })
        {
            w.Civilizations[4].Advances = w.Civilizations[4].Advances.With(a);
            w.Globals.AdvanceKnownBy[a] = CivilizationSet.Only(4); w.Globals.FirstDiscoverer[a] = 7;
        }
        var choices = CreationAttitudes(); CreationChoice(choices, "catch-up", 3, 1, 5);
        CreationChoice(choices, "catch-up", 3, 2, 6); CreationChoice(choices, "gender", 3, 1); CreationSiteChoices(choices);
        var random = CreationRandom(choices); var research = new GrantFixture(f, random);
        research.OnAcquired = n =>
        {
            Equal(1, n.Civilization); Equal(-2, n.Source); Equal(false, w.Globals.CivsAlive.Contains(2));
            Equal((byte)7, w.Civilizations[2].UnitsActive[4]); Equal((byte)9, w.Civilizations[2].UnitsLost[4]);
            Equal((byte)(n.Advance == 5 ? 1 : 2), w.Civilizations[2].NumAdvances);
            if (n.Advance == 6) { Equal(new UnitType(61), f.Unit(0).Type); }
        };
        var creation = Creation(f, 2, random, grants: research.Grants);
        Equal(1, creation.Advance(1)); random.RequireChoicesConsumed();
        Equal(new MapPoint(10, 8), creation.Site!.Value.Position); Equal(true, w.Globals.CivsAlive.Contains(2));
        Equal((short)-1, w.Civilizations[2].Researching);
        Equal(new UnitType(61), f.Unit(0).Type); Equal(false, f.Unit(0).Flags.Veteran);
        Equal(1, research.Upgrades.Count); Equal(2, research.Acquisitions.Count); Equal(2, research.Prompts.Count);
        Equal((byte)1, w.Civilizations[2].NumAdvances); // Placement overwrites the grant count, not knowledge.
        Equal((byte)2, w.Civilizations[1].NumAdvances); Equal((byte)0, w.Civilizations[2].UnitsActive[4]);
        Equal((byte)0, w.Civilizations[2].UnitsLost[4]);
        foreach (int a in new[] { 5, 6 })
        {
            Equal(true, w.Civilizations[2].Advances.Contains(a)); Equal((byte)2, w.Civilizations[2].AdvanceSource[a]);
            Equal(true, w.Civilizations[1].Advances.Contains(a)); Equal((byte)254, w.Civilizations[1].AdvanceSource[a]);
            Equal(CivilizationSet.Only(1).With(2).With(4), w.Globals.AdvanceKnownBy[a]); Equal((byte)7, w.Globals.FirstDiscoverer[a]);
        }
        Sequence(new[] { "acquired:1", "prompts:1", "upgrade:0", "redraw", "acquired:1", "prompts:1" }, research.Order);
    }

    private static void RestartResearchGovernmentAndFailurePrefix()
    {
        foreach (bool fail in new[] { false, true })
        {
            var f = CreationFixture(); var w = f.World; int a = AdvanceIds.Monarchy;
            for (int i = 0; i < f.Rules.Terrain.Length; ++i)
            {
                f.Rules.Terrain[i] = new()
                {
                    Food = 2,
                    Shields = 1,
                    Trade = 1,
                    Work = new(new(TerrainResult.ImproveInPlace, 1), new(TerrainResult.ImproveInPlace, 2))
                };
            }
            w.Globals.Turn = 25; w.Globals.Difficulty = Difficulty.Prince;
            w.Globals.HumanPlayers = CivilizationSet.Only(1).With(4); w.Globals.CivsAlive = CivilizationSet.Only(1).With(2);
            f.AddCity(0, 1, new(30, 20)); w.Globals.WonderCity[WonderIds.GreatLibrary] = 0;
            w.Civilizations[1].Government = Government.Despotism; w.Civilizations[1].CityCount = 1;
            w.Entities.Cities.Storage[0].Production = UnitTypeIds.Fanatics;
            w.Civilizations[2].UnitsActive[4] = 7;
            w.Civilizations[4].Advances = w.Civilizations[4].Advances.With(a);
            w.Globals.AdvanceKnownBy[a] = CivilizationSet.Only(4); w.Globals.FirstDiscoverer[a] = 7;
            var choices = CreationAttitudes(); CreationChoice(choices, "catch-up", 3, 1, a);
            var random = CreationRandom(choices); var research = new GrantFixture(f, random) { RevolutionReply = 1 };
            if (fail) { research.OnAcquired = _ => throw new InvalidOperationException("Nested library notice failed"); }
            var creation = Creation(f, 2, random, grants: research.Grants);
            if (fail)
            {
                Throws<InvalidOperationException>(() => creation.Advance(1));
                Throws<InvalidOperationException>(() => creation.Advance(1));
                Equal((byte)7, w.Civilizations[2].UnitsActive[4]);
                Equal(Government.Despotism, w.Civilizations[1].Government);
                Equal((sbyte)UnitTypeIds.Fanatics, w.Entities.Cities.Storage[0].Production);
            }
            else
            {
                Equal(1, creation.Advance(1)); // No eligible leader; the completed grant survives.
                Equal((byte)0, w.Civilizations[2].UnitsActive[4]);
                Equal(Government.Anarchy, w.Civilizations[1].Government);
                Equal((sbyte)UnitTypeIds.Riflemen, w.Entities.Cities.Storage[0].Production);
                Equal(1, research.GovernmentChoices.Count);
            }
            Equal(false, w.Globals.CivsAlive.Contains(2)); random.RequireChoicesConsumed();
            Equal((byte)1, w.Civilizations[2].NumAdvances); Equal((byte)1, w.Civilizations[1].NumAdvances);
            Equal(true, w.Civilizations[2].Advances.Contains(a)); Equal(true, w.Civilizations[1].Advances.Contains(a));
            Equal((byte)2, w.Civilizations[2].AdvanceSource[a]); Equal((byte)254, w.Civilizations[1].AdvanceSource[a]);
        }
    }

    // V01 §6 -> §§2–5 -> V03 §10, including grant effects before a later restart denial.
    private static void EliminationRestartActualResearch()
    {
        foreach (bool restart in new[] { false, true })
        {
            var f = new EliminationFixture(); var w = f.World;
            w.Globals.CivsAlive = CivilizationSet.Only(1).With(2).With(3); w.Globals.Difficulty = Difficulty.Prince;
            w.Globals.Rules.NoRestart = !restart; f.Data.Process.Rules.Leaders[2].Color = 2;
            f.Data.AddUnit(0, 2, 4, new(20, 10));
            foreach (int a in new[] { 5, 6 })
            {
                w.Civilizations[1].Advances = w.Civilizations[1].Advances.With(a);
                w.Globals.AdvanceKnownBy[a] = CivilizationSet.Only(1); w.Globals.FirstDiscoverer[a] = 3;
            }
            var choices = CreationAttitudes(); CreationChoice(choices, "catch-up", 3, 1, 5);
            CreationChoice(choices, "catch-up", 3, 1, 6); CreationChoice(choices, "gender", 3, 1); CreationSiteChoices(choices);
            var random = CreationRandom(choices);
            f.Recreate = owner => Creation(f.Data, owner, random);
            Equal(1, f.Elimination.Check(2, -1, "restart")); random.RequireChoicesConsumed(); f.Random.RequireChoicesConsumed();
            Equal(restart, w.Globals.CivsAlive.Contains(2)); Equal(false, w.Globals.Status.Won);
            Equal((byte)(restart ? 1 : 2), w.Civilizations[2].NumAdvances);
            Equal(restart, f.Data.Unit(0).Token.Occupied); Equal(1, f.Restarts); Equal(1, f.MapRefreshes);
            if (restart) { Equal(new UnitType(0), f.Data.Unit(0).Type); Equal(new MapPoint(10, 8), f.Data.Unit(0).Position); }
            foreach (int a in new[] { 5, 6 })
            {
                Equal(true, w.Civilizations[2].Advances.Contains(a)); Equal((byte)2, w.Civilizations[2].AdvanceSource[a]);
                Equal(CivilizationSet.Only(1).With(2), w.Globals.AdvanceKnownBy[a]);
            }
        }
    }
}
