using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Formats;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // F06 §6 and F03 ordered reload. Authored semantic fixtures, no original-game trace claims.
    private static GameInput InstallationInput()
    {
        var input = RestorationInput.Game(ClassicFiles.DecodeStoredSave(SaveVector()));
        var civilizations = input.Civilizations.ToArray(); civilizations[1].UnitsActive[2] = 55; civilizations[1].UnitsInProduction[3] = 66; civilizations[1].UnitsLost[2] = 77;
        var units = input.Units.ToArray(); units[0].Owner = new(1); units[0].Type = new(2);
        var cities = input.Cities.ToArray(); cities[0].Owner = new(1); cities[0].Production = 3;
        var layout = input.Layout; layout.Rectangles[0] = new(0, 0, 640, 480);
        return input with { Globals = input.Globals with { Turn = 31, CivsAlive = StateConversion.DecodeCivilizations(7), PlayerCiv = 1, SavedCurrentCiv = 1, TurnCiv = 1, NumEnemies = 99, GameOptions = StateConversion.DecodeGameOptions(0x8000), RevealMap = 1 }, Civilizations = new(civilizations), Units = new(units), Cities = new(cities), Layout = layout };
    }
    private static SessionRuntime InstallationRuntime()
    {
        var runtime = new SessionRuntime(new(), new(71)); runtime.BeginClientSession(new(10, 20)); runtime.Process.Tokens.NextUnit = 100; runtime.Process.Tokens.NextCity = 200;
        var rules = runtime.Process.Rules; var id = new LeaderText(NameText("Label"));
        foreach (var leader in rules.Leaders) { Array.Fill(leader.Names, id); leader.Plural = leader.Adjective = id; foreach (var pair in leader.Titles) Array.Fill(pair, id); }
        runtime.World.Mood.City = new(3); runtime.World.Mood.Happy = 21; runtime.World.Mood.Unhappy = 22;
        return runtime;
    }
    private static void InstallInput(SessionRuntime runtime, GameInput input, RuleInput? rules = null, Rectangle? screen = null) =>
        GameInstallation.Install(input, runtime, rules ?? new(), screen ?? new(10, 20, 650, 500), "load/one");
    private static void GameInstallAdmission()
    {
        var input = InstallationInput(); GameInstallation.Validate(input);
        var badCivs = input.Civilizations.ToArray(); badCivs[0].Tribe = -1;
        var badNames = input.Names.ToArray(); badNames[2].Titles = new([]);
        var badLeaders = input.Leaders.ToArray(); badLeaders[0] = new(false, false, new([]));
        foreach (var bad in new[]
        {
            input with { Map = input.Map with { Width = 3 } }, input with { Civilizations = new([]) }, input with { Names = new([]) }, input with { Views = new([]) }, input with { Tribes = new([]) }, input with { Leaders = new([]) },
            input with { Units = new([]) }, input with { Cities = new([]) }, input with { Globals = input.Globals with { UnitEnd = -1 } }, input with { Globals = input.Globals with { CityEnd = -1 } },
            input with { Civilizations = new(badCivs) }, input with { Names = new(badNames) }, input with { Leaders = new(badLeaders) }, input with { Leaders = new(new LeaderCustomization?[21]) },
            input with { Globals = input.Globals with { PlayerCiv = -1 } }, input with { Globals = input.Globals with { PlayerCiv = 8 } }, input with { Scenario = new() }, input with { Globals = input.Globals with { Status = new GameStatus { Scenario = true } } },
            input with { Eliminations = input.Eliminations with { Names = new([]) } }, input with { Multiplayer = new() { Emails = new([]) } }, input with { Events = new([new(new(), new([]))]) }
        })
        {
            using var runtime = InstallationRuntime(); var random = runtime.GameplayRandom.Capture(); var rules = runtime.Process.Rules.Definitions.Snapshot(); var client = SessionClient(runtime); var identity = client.Identity;
            Throws<ArgumentException>(() => InstallInput(runtime, bad)); Equal(false, runtime.World.Map.Allocated); Equal((short)0, runtime.World.Globals.Turn); Equal(100U, runtime.Process.Tokens.NextUnit); Equal(200U, runtime.Process.Tokens.NextCity); Equal(random, runtime.GameplayRandom.Capture()); Equal(rules, runtime.Process.Rules.Definitions.Snapshot()); Equal(identity, client.Identity);
        }
        var scenarioNames = input.Names.ToArray(); scenarioNames[3].Tribe = "A\u0000";
        Throws<ArgumentException>(() => GameInstallation.Validate(input with { Globals = input.Globals with { Status = new GameStatus { Scenario = true } }, Scenario = new(), Names = new(scenarioNames) }));
        var huge = input with { Globals = input.Globals with { UnitEnd = 2049 }, Units = new(new Unit[2049]) }; Throws<ArgumentException>(() => GameInstallation.Validate(huge));
        Throws<ArgumentException>(() => GameInstallation.Validate(input with { Globals = input.Globals with { CityEnd = 257 }, Cities = new(new City[257]) }));
    }
    private static void GameInstallValuesAndLifetimes()
    {
        using var runtime = InstallationRuntime(); var input = InstallationInput(); var world = runtime.World; var lifetime = world.Lifetime; var client = SessionClient(runtime); var identity = client.Identity; var presentation = runtime.PresentationRandom.Capture();
        world.Multiplayer = IncomingMultiplayer(); runtime.Process.MultiplayerPreferences = new() { KillOnRetirement = true, ShowHumanStarts = true, ChatFromStart = true, Simultaneous = true };
        runtime.Process.Fallback.Value.Region = 61; world.Map.RememberedBarbarian = [MapFeatures.Unit | MapFeatures.CitySite, MapFeatures.Irrigation]; world.UnitStacks.Suppression = 13; world.UnitTotals[1] = 99;
        using (var activity = runtime.BeginActivity()) InstallInput(runtime, input);
        Equal(true, ReferenceEquals(world, runtime.World)); Equal(true, ReferenceEquals(lifetime, world.Lifetime)); Equal(identity, client.Identity); Equal(presentation, runtime.PresentationRandom.Capture()); Equal(255UL, runtime.GameplayRandom.Capture().Position.Draws);
        Equal((short)31, world.Globals.Turn); Equal((byte)1, world.Globals.NumEnemies); Equal(StateConversion.DecodeGameOptions(0x8000U), world.Globals.GameOptions); Equal(new UnitToken(100), world.Entities.Units.Storage[0].Token); Equal(new UnitToken(0), world.Entities.Units.Storage[1].Token); Equal(new CityToken(200), world.Entities.Cities.Storage[0].Token); Equal(101U, runtime.Process.Tokens.NextUnit); Equal(201U, runtime.Process.Tokens.NextCity);
        Equal(input.Units[1], world.Entities.Units.Storage[1]); Equal(1, client.OwnUnits().Records.Count); Equal((byte)1, world.Civilizations[1].UnitsActive[2]); Equal((byte)1, world.Civilizations[1].UnitsInProduction[3]); Equal((byte)77, world.Civilizations[1].UnitsLost[2]); Equal((short)10, world.Scenario.TechnologyParadigm);
        Equal(input.PowerHistory, world.PowerHistory); Equal(input.Views, new(world.Views)); Equal(input.Eliminations, world.Eliminations); Equal(true, world.Layout.Valid); Equal(false, world.Events.Loaded); Equal(true, world.Mood.City is null); Equal(21, world.Mood.Happy); Equal(22, world.Mood.Unhappy);
        Equal(13, world.UnitStacks.Suppression); Equal(99, world.UnitTotals[1]); Equal((byte)61, runtime.Process.Fallback.Value.Region); Equal(new OwnedList<MapFeatures>([MapFeatures.Unit | MapFeatures.CitySite, MapFeatures.Irrigation]), new(world.Map.RememberedBarbarian));
        Equal(input.Map.Tiles, new(world.Map.Tiles.ToArray())); Equal(input.Map.Regions, new(world.Map.Regions.ToArray())); Equal(input.Map.Remembered[0], new(world.Map.RememberedLayer(1).ToArray()));
        Equal(1, world.Multiplayer.LocalHuman); Equal(1, world.Multiplayer.ResumeCiv); Equal((byte)1, world.Multiplayer.Saved.ResumeCiv); Equal(true, world.Multiplayer.Saved.OpenGame && world.Multiplayer.Saved.KillOnRetirement && world.Multiplayer.Saved.ShowHumanStarts && world.Multiplayer.Saved.ChatFromStart);
        Equal(false, world.Multiplayer.Saved.DoubleMovement || world.Multiplayer.Saved.DoubleProduction || runtime.Process.MultiplayerPreferences.Simultaneous); Equal(true, world.Multiplayer.Saved.Simultaneous); Equal(123456, world.Multiplayer.Saved.TurnTimeLimit); Equal(true, world.Multiplayer.NetworkHost); Equal(new OptionVoteState(-3, -7, uint.MaxValue), world.Multiplayer.Vote);
        Equal((byte)13, runtime.Process.Rules.Leaders[0].TimesUsed); Equal((byte)29, runtime.Process.Rules.Leaders[0].CityNameIndex); Equal(true, runtime.Process.Rules.Leaders[0].Female);
        Equal(0x80000001U, input.Units[0].Token.Value); Equal((byte)55, input.Civilizations[1].UnitsActive[2]);
        InstallInput(runtime, input); Equal(new UnitToken(101), world.Entities.Units.Storage[0].Token); Equal(new CityToken(201), world.Entities.Cities.Storage[0].Token); Equal(510UL, runtime.GameplayRandom.Capture().Position.Draws);
    }
    private static void GameInstallMultiplayerAndScenario()
    {
        using var runtime = InstallationRuntime(); var input = InstallationInput(); var civs = input.Civilizations.ToArray(); civs[1].Flags = StateConversion.DecodeCivilizationStatus(0x200);
        var names = input.Names.ToArray(); names[1].Tribe = "Label";
        var saved = new MultiplayerValues { DoubleMovement = true, DoubleProduction = true, TurnTimeLimit = 123 }; saved.Passwords = new(Enumerable.Range(0, 8).Select(i => i == 1 ? "[" : ""));
        var eventInput = new EventInput(new() { Actions = StateConversion.DecodeEventActions(0x80000001) }, new(Enumerable.Range(0, 35).Select(i => i == 0 ? "Unit" : null)));
        input = input with { Globals = input.Globals with { Multiplayer = GameMode.Tcp, HumanPlayers = StateConversion.DecodeCivilizations(2), Status = new GameStatus { Cheated = true, ScenarioExport = true }, TurnCiv = 0 }, Civilizations = new(civs), Names = new(names), Multiplayer = saved, Scenario = new() { TechnologyParadigm = -11 }, Events = new([eventInput]) };
        runtime.Process.MultiplayerPreferences.Simultaneous = true; InstallInput(runtime, input);
        Equal(new GameStatus { Scenario = true }, runtime.World.Globals.Status); Equal(StateConversion.DecodeGameOptions(0U), runtime.World.Globals.GameOptions); Equal((byte)0, runtime.World.Globals.RevealMap); Equal((short)-11, runtime.World.Scenario.TechnologyParadigm); Equal(false, runtime.World.Layout.Valid);
        Equal(0UL, runtime.GameplayRandom.Capture().Position.Draws); Equal((int)GameMode.Tcp, (int)runtime.World.Globals.Multiplayer); Equal(false, runtime.World.Multiplayer.Saved.DoubleMovement || runtime.World.Multiplayer.Saved.DoubleProduction); Equal(1, runtime.World.Multiplayer.ResumeCiv); Equal(true, runtime.World.Multiplayer.Passwords.Present[1]); Equal(false, runtime.World.Multiplayer.Passwords.Present[2]); Equal(saved.Passwords, runtime.World.Multiplayer.Saved.Passwords);
        Equal((byte)21, runtime.World.Civilizations[1].LeaderPortrait); Equal((byte)0, runtime.World.Civilizations[2].LeaderPortrait); Equal(false, runtime.Process.Rules.Leaders[0].Female); Equal(true, runtime.Process.Rules.Leaders[0].Plural.Custom); Equal(RuleText.Nil, runtime.Process.Rules.Leaders[0].Titles[0][0].Text);
        Equal(true, runtime.World.Events.Loaded); Equal(1, runtime.World.Events.Collection.Count); Equal(eventInput.Values, runtime.World.Events.Collection.At(0).Values); Equal("Unit", runtime.World.Events.Collection.At(0).Strings[0].Value);
        var oldEvent = runtime.World.Events.Collection.At(0); InstallInput(runtime, input with { Events = new([]) }); Equal(true, runtime.World.Events.Loaded); Equal(0, runtime.World.Events.Collection.Count); Equal("Unit", oldEvent.Strings[0].Value);
    }
    private static void GameInstallNameMarkers()
    {
        using var runtime = InstallationRuntime(); var rules = runtime.Process.Rules; var input = InstallationInput(); var world = runtime.World;
        world.Globals = input.Globals; input.Civilizations.ToArray().CopyTo(world.Civilizations, 0); var leader = rules.Leaders[0]; leader.Names[0] = new(NameText("Male")); leader.Names[1] = new(NameText("Female")); leader.Plural = new(NameText("Plural"), true); leader.Adjective = new(RuleText.Nil, true); leader.Titles[3][1] = new(NameText("Title"), true);
        var selected = input.Leaders.ToArray(); selected[0] = new(true, true, new(Enumerable.Range(0, 7).Select(g => new OwnedList<bool>([false, g == 3])))); input = input with { Leaders = new(selected) };
        GameInstallation.RestoreLeaderNames(input, world, rules); Equal(new LeaderText(NameText("Male"), true), leader.LeaderName); Equal(new LeaderText(NameText("Plural"), true), leader.Plural); Equal(new LeaderText(RuleText.Nil, true), leader.Adjective); Equal(new LeaderText(NameText("Title"), true), leader.Titles[3][1]);
        world.Globals.Multiplayer = GameMode.Tcp; world.Globals.HumanPlayers = StateConversion.DecodeCivilizations(3); GameInstallation.RestoreLeaderNames(input, world, rules); Equal(false, leader.LeaderName.Custom || leader.Plural.Custom || leader.Adjective.Custom || leader.Titles[3][1].Custom);
        world.Globals.HumanPlayers = StateConversion.DecodeCivilizations(7); GameInstallation.RestoreLeaderNames(input, world, rules); Equal(true, leader.LeaderName.Custom && leader.Plural.Custom && leader.Adjective.Custom && leader.Titles[3][1].Custom);
        Equal((sbyte)1, world.Globals.PlayerCiv); Equal(NameText("Male"), leader.LeaderName.Text);
    }
    private static void GameInstallFailurePrefixes()
    {
        using var runtime = InstallationRuntime(); var input = InstallationInput(); runtime.World.Multiplayer = IncomingMultiplayer(); runtime.GameplayRandom.Restore(new GameplayRandom(PasswordChoices("load/one", 100), "session").Capture(), 1000);
        ChoiceFails(ChoiceFailure.Missing, () => InstallInput(runtime, input), PasswordKey("load/one", 100));
        Equal((short)31, runtime.World.Globals.Turn); Equal(new UnitToken(100), runtime.World.Entities.Units.Storage[0].Token); Equal(101U, runtime.Process.Tokens.NextUnit); Equal(input.PowerHistory, runtime.World.PowerHistory); Equal((byte)55, runtime.World.Civilizations[1].UnitsActive[2]); Equal(new CitySlot(3), runtime.World.Mood.City!.Value); Equal(false, runtime.World.Multiplayer.Saved.OpenGame); Equal(1, runtime.World.Multiplayer.LocalHuman); Equal(301, runtime.World.Multiplayer.ResumeCiv);
        for (int i = 0; i < 256; ++i) Equal(i < 100 ? unchecked((byte)PasswordCode(i)) : (byte)0xa5, PasswordConversion.Encode(runtime.World.Multiplayer.Saved.Passwords)[i]);
        using var invalidCensus = InstallationRuntime(); var units = input.Units.ToArray(); units[0].Type = new(62);
        Throws<ArgumentOutOfRangeException>(() => InstallInput(invalidCensus, input with { Units = new(units) })); Equal(255UL, invalidCensus.GameplayRandom.Capture().Position.Draws); Equal(new UnitToken(100), invalidCensus.World.Entities.Units.Storage[0].Token); Equal(true, invalidCensus.World.Multiplayer.Saved.OpenGame); Equal(new CitySlot(3), invalidCensus.World.Mood.City!.Value); Equal(default(UnitTypeArray<byte>), invalidCensus.World.Civilizations[0].UnitsActive); Equal(default(UnitTypeArray<byte>), invalidCensus.World.Civilizations[1].UnitsActive);
    }
    private static void GameInstallRuleReloadAndLayout()
    {
        var input = InstallationInput(); var ruleInput = new RulesInput().Load().Input;
        using var runtime = InstallationRuntime(); new RuleInstaller(ruleInput, runtime.Process.Rules).Load(true); var rules = runtime.Process.Rules;
        var oldIdentity = rules.Definitions.Units[0].Identity with { Name = "Retained" }; rules.Definitions.Units[0] = rules.Definitions.Units[0] with { Identity = oldIdentity, Attack = 99, Movement = 90 }; rules.DoubleMovementApplied = true;
        InstallInput(runtime, input, ruleInput); Equal(oldIdentity, rules.Definitions.Units[0].Identity); Equal((byte)1, rules.Definitions.Units[0].Attack); Equal((byte)6, rules.Definitions.Units[0].Movement); Equal(false, rules.DoubleMovementApplied); Equal(true, runtime.World.Layout.Valid);
        foreach (var screen in new[] { new Rectangle(10, 20, 650, 501), new Rectangle(10, 20, 651, 500) }) { InstallInput(runtime, input, ruleInput, screen); Equal(false, runtime.World.Layout.Valid); }
        var layout = input.Layout; layout.Rectangles[0] = new(int.MaxValue, int.MaxValue, int.MinValue, int.MinValue); InstallInput(runtime, input with { Layout = layout }, ruleInput, new(0, 0, 1, 1)); Equal(true, runtime.World.Layout.Valid);
        foreach (byte alive in new byte[] { 0, 1, 2, 3, 255 }) { InstallInput(runtime, input with { Globals = input.Globals with { CivsAlive = StateConversion.DecodeCivilizations(alive) } }, ruleInput); Equal(alive == 255 ? (byte)6 : alive <= 1 ? (byte)255 : (byte)0, runtime.World.Globals.NumEnemies); }
    }
    private static void GameInstallScenarioExportNames()
    {
        using var runtime = InstallationRuntime(); var world = runtime.World; var rules = runtime.Process.Rules;
        for (int civ = 0; civ < 8; ++civ) { world.Civilizations[civ].Tribe = (short)civ; world.Names[civ] = new() { Leader = "A", Tribe = "B", Adjective = "C" }; }
        var before = new OwnedList<CivilizationNames>(world.Names); rules.Leaders[1].LeaderName = rules.Leaders[1].Names[0]; rules.Leaders[1].Plural = rules.Leaders[1].Plural with { Custom = true }; rules.Leaders[2].Adjective = rules.Leaders[2].Adjective with { Custom = true };
        GameInstallation.PrepareScenarioNames(false, world.Globals, world.Civilizations, world.Names, rules); Equal(before, new(world.Names));
        GameInstallation.PrepareScenarioNames(true, world.Globals, world.Civilizations, world.Names, rules); Equal(before[0], world.Names[0]); Equal("Label", world.Names[1].Leader); Equal(before[1].Tribe, world.Names[1].Tribe); Equal("Label", world.Names[1].Adjective); Equal(before[2].Adjective, world.Names[2].Adjective);
        world.Names[1].Adjective = "D"; GameInstallation.PrepareScenarioNames(false, world.Globals with { Status = new GameStatus { Scenario = true } }, world.Civilizations, world.Names, rules); Equal("Label", world.Names[1].Adjective);
    }
}
