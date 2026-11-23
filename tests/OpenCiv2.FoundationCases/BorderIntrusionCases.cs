using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Formats;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static void BorderFirstStackAndRetainedWarnings()
    {
        var f = new RevelationFixture(); var w = f.World;
        f.AddCity(0, 1, new(8, 6)); f.AddCity(1, 1, new(8, 4)); f.AddCity(2, 3, new(10, 6));
        f.AddUnit(0, 2, 3, new(9, 5)); // First occupied tile has only Trade: still stop this city's tile loop.
        f.AddUnit(1, 2, 0, new(10, 6)); // A city tile is never an intrusion tile.
        f.AddUnit(2, 2, 0, new(9, 7));
        var count = new BorderIntrusions(w, f.Rules, f.Stacks);
        Equal(0, count.Count(1, 2)); Equal(default(BorderIntrusionSummary), w.Diplomacy.Intrusions);
        Equal(false, f.Unit(2).Flags.BorderWarned);
        f.Unit(0).Type = new(0);
        w.Map.TileAt(new(9, 5)).Features |= MapFeatures.Road | MapFeatures.Railroad | MapFeatures.Mine
            | MapFeatures.Irrigation | MapFeatures.Fortification;
        Equal(7, count.Count(1, 2)); Equal(new BorderIntrusionSummary(new(0), 1, 1, 7, 0), w.Diplomacy.Intrusions);
        Equal(true, f.Unit(0).Flags.BorderIntruder); Equal(true, f.Unit(0).Flags.BorderWarned);
        Equal(0, count.Count(1, 2)); Equal(default(BorderIntrusionSummary), w.Diplomacy.Intrusions);
        f.Unit(0).Flags.BorderIntruder = false;
        Equal(7, count.Count(1, 2)); Equal(1, w.Diplomacy.Intrusions.RepeatOffenses);
        w.Civilizations[1].Treaties[2].Alliance = true;
        Equal(0, count.Count(1, 2)); Equal(default(BorderIntrusionSummary), w.Diplomacy.Intrusions);
        Equal(true, f.Unit(0).Flags.BorderWarned); Equal(true, f.Unit(0).Flags.BorderIntruder);
    }

    private static void BorderStackOwnershipAndCrowding()
    {
        var f = new RevelationFixture(); var w = f.World;
        f.AddCity(0, 1, new(8, 6));
        for (int i = 0; i < 4; ++i) { f.AddUnit(i, 2, 0, new(9, 5)); }
        f.Unit(0).Owner = new(3); // Ownership changes need not rebuild the physical chain.
        // Tile owner qualifies the entire physical stack; the foreign member is still counted.
        w.Map.TileAt(new(9, 5)).Owner = 2;
        w.Map.TileAt(new(9, 5)).Features |= MapFeatures.Road | MapFeatures.Railroad | MapFeatures.Mine
            | MapFeatures.Irrigation | MapFeatures.Fortification;
        f.Unit(1).Flags.BorderWarned = true;
        f.Rules.Units[9] = f.Rules.Units[0] with { Role = (RuleRole)255 };
        f.Unit(0).Type = new(9); // Signed role -1 is below five.
        var count = new BorderIntrusions(w, f.Rules, f.Stacks);
        Equal(28, count.Count(1, 2)); Equal(new BorderIntrusionSummary(new(0), 1, 4, 28, 2), w.Diplomacy.Intrusions);
        for (int i = 0; i < 4; ++i) { Equal(true, f.Unit(i).Flags.BorderIntruder); }
        var loaded = StateConversion.ToLive(StateConversion.ToStored(f.Unit(0)));
        Equal(true, loaded.Flags.BorderIntruder); Equal(true, loaded.Flags.BorderWarned);
        Equal(default(BorderIntrusionSummary), new GameState(f.Process).Diplomacy.Intrusions);
    }

    private static void BorderTopologyAndSkips()
    {
        var f = new RevelationFixture(); var w = f.World; f.AddCity(0, 1, new(0, 2));
        f.AddUnit(0, 2, 0, new(39, 1));
        var count = new BorderIntrusions(w, f.Rules, f.Stacks);
        Equal(0, count.Count(1, 2));
        w.Globals.Rules.FlatWorld = false; Equal(1, count.Count(1, 2));
        f.Unit(0).Flags.BorderIntruder = false; w.Map.TileAt(new(39, 1)).Terrain = Terrain.Ocean;
        Equal(0, count.Count(1, 2));
        w.Map.TileAt(new(39, 1)).Terrain = Terrain.Grassland; w.Map.TileAt(new(39, 1)).Features |= MapFeatures.CitySite;
        Equal(0, count.Count(1, 2));
        w.Map.TileAt(new(39, 1)).Features &= ~MapFeatures.CitySite; w.Map.TileAt(new(39, 1)).Owner = 3;
        Equal(0, count.Count(1, 2));
        w.Civilizations[2].Treaties[1].Alliance = true; w.Map.TileAt(new(39, 1)).Owner = 2;
        Equal(1, count.Count(1, 2)); // Reverse-only alliance does not suppress the measurement.
    }
}
