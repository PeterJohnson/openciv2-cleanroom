using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.State;
using OpenCiv2.Formats;
using StateConversion = OpenCiv2.Formats.StateConversion;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // F02 ownership/construction, F03 §8 defaults, S01 §2 parameter reset.
    private static void WorldOwnership()
    {
        var process = new ProcessContext(); var peer = new ProcessContext(); process.Tokens.AssignUnit(); process.Fallback.Value.Fertility = 7;
        process.OrdinaryPreferences.Science = 6; process.MultiplayerPreferences.Timer = 3; process.TextParameters.Numbers[5] = 17; process.Rules.Leaders[0].Plural = new(NameText("Kept"));
        var first = new GameState(process); var second = new GameState(process); var otherPeer = new GameState(peer);
        Equal(false, ReferenceEquals(first.Lifetime, second.Lifetime)); Equal(false, ReferenceEquals(process.Rules, peer.Rules));
        first.Map.Allocate(new(4, 2)); second.Map.Allocate(new(2, 2)); otherPeer.Map.Allocate(new(2, 2));
        Equal((byte)7, first.Map.TileAt(new(-1, 0)).Fertility); second.Map.TileAt(new(-1, 0)).Fertility = 9; Equal((byte)9, first.Map.TileAt(new(-1, 0)).Fertility); Equal((byte)0, otherPeer.Map.TileAt(new(-1, 0)).Fertility);
        first.Entities.Units.Storage[0].Damage = 99; first.Civilizations[1].UnitsActive[61] = 7; first.Events.Collection.Append(); first.Mood.Happy = 3; first.PowerHistory[149][7] = 255;
        Equal((byte)0, second.Entities.Units.Storage[0].Damage); Equal((byte)0, second.Civilizations[1].UnitsActive[61]); Equal(0, second.Events.Collection.Count); Equal(0, second.Mood.Happy); Equal((byte)0, second.PowerHistory[149][7]);
        Equal(true, second.PendingLeaderOverrides.All(v => v == -1)); Equal(2U, process.Tokens.NextUnit); Equal(1U, peer.Tokens.NextUnit); Equal((short)6, process.OrdinaryPreferences.Science); Equal((short)3, process.MultiplayerPreferences.Timer); Equal(17, process.TextParameters.Numbers[5]); Equal(NameText("Kept"), process.Rules.Leaders[0].Plural.Text);
        Equal(0, peer.TextParameters.Numbers[5]); Equal(0, peer.TextParameters.Strings[10].Length); Equal(0, second.Names[7].Titles[6].Length); Equal(0, second.Eliminations.Names[11].Length);
        first.Map.Release(); Equal(true, second.Map.Allocated); Equal((byte)9, process.Fallback.Value.Fertility);
    }
    private static void WorldValueCopies()
    {
        var starts = new StartingPositions(); for (int i = 0; i < 21; ++i) { Equal((short)-1, starts.Positions[i].X); Equal((short)-1, starts.Positions[i].Y); }
        var copy = starts; copy.Positions[20] = copy.Positions[20] with { X = short.MinValue }; Equal((short)-1, starts.Positions[20].X);
        var world = new GameState(new()); world.Scenario = new() { Flags = StateConversion.DecodeScenarioOptions(0xffff), Name = "\u00e9", TechnologyParadigm = -1, YearIncrement = 2, StartYear = 3, MaximumTurns = 4, ObjectiveCiv = 5 }; world.Scenario.VictoryThresholds[3] = 6;
        world.Events.Loaded = true; world.Globals.Status = new GameStatus { Cheated = true }; world.Scenario.Reset(); Equal(new OpenCiv2.Core.State.ScenarioParameters { TechnologyParadigm = 10 }, world.Scenario); Equal(true, world.Events.Loaded); Equal(new GameStatus { Cheated = true }, world.Globals.Status);
        var history = new OpenCiv2.Core.State.EliminationHistory { Count = -1 }; history.Turns[11] = short.MinValue; history.Killers[11] = 255;
        var historyCopy = history; historyCopy.Turns[11] = 17; Equal(short.MinValue, history.Turns[11]); Equal((short)-1, history.Count);
        var layout = new Layout { Valid = true }; layout.Rectangles[12] = new(int.MinValue, int.MaxValue, -1, 7); var layoutCopy = layout; layoutCopy.Rectangles[12] = new(); Equal(int.MinValue, layout.Rectangles[12].Left);
        var setup = new WorldSetup { Width = -1, Height = 2 }; setup.Parameters[4] = int.MinValue; var setupCopy = setup; setupCopy.Parameters[4] = 0; Equal(int.MinValue, setup.Parameters[4]);
    }
    private static void PreferenceDefaults()
    {
        foreach (int width in new[] { -1, 999, 1000 }) foreach (bool animations in new[] { false, true })
        {
            var preferences = new OrdinaryPreferences { Rules = StateConversion.DecodeGameRules(0xffff), Difficulty = Difficulty.Deity, Gender = Gender.Female, Tribe = 19, Text = "\u00e9", AcceleratedStart = 7, RulesChoice = RuleSelection.Advanced };
            preferences.WorldParameters[4] = -1; preferences.SetDefaults(width, animations);
            var expected = new OrdinaryPreferences { Rules = StateConversion.DecodeGameRules(0x3f), DefaultLevel = width > 999 ? (short)2 : (short)0, Science = 6, Tax = 4, GameOptions = StateConversion.DecodeGameOptions(animations ? 0x3f3258U : 0x1f3258U), MapSize = WorldSize.Normal, Barbarians = 2 };
            Equal(expected, preferences); preferences.SetDefaults(width, animations); Equal(expected, preferences);
        }
        var multiplayer = new MultiplayerPreferences { OpenGame = true, KillOnRetirement = true, Simultaneous = true, DoubleMovement = true, Protocol = NetworkProtocol.Serial, GameName = "\u0001", CustomTimer = "\u0002", Parley = new(1, 2, 3, 4) };
        multiplayer.SetDefaults(); Equal(new MultiplayerPreferences { CustomTimer = "60" }, multiplayer);
        Equal(0, new MultiplayerPreferences().CustomTimer.Length); Equal(StateConversion.DecodeGameOptions(0U), new OrdinaryPreferences().GameOptions);
    }
}
