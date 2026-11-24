using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.Units;
using OpenCiv2.Formats;
using OpenCiv2.Formats.Classic;
using OpenCiv2.Formats.Content;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private const string GameplayOptions = "@SPYOPTIONS\nEmbassy selected\nInvestigate selected\nSteal selected\nSabotage selected\nPoison selected\nDevice selected\nIncite selected\n"
        + "@DISSIDENTOPTIONS\nStay home\nPay normally\nRisk incident\nPay double\n";

    private static SinglePlayerGameplayContent BindClassicGameplay(GameplayFixture f, ClassicTextInput input,
        CityPreferences preferences, string filename = "GAME") =>
        new ClassicGameplayContent(input.Workspace, input.Directories, input.Services(), TextCase.Ascii).Load(
            f.Data.Process.Rules, preferences, new(), GameplayContent.Builtin.Values, new(f.World.Map),
            f.F.Life.Diplomacy.Events.Media, lines => lines, "Selected food", " (Selected spy)", filename);

    // F03/C01: selected sources retain layered naming and the supplied per-game preference state.
    private static void ClassicGameplayContentNamesAndPreferences()
    {
        var input = new ClassicTextInput(); var preferences = new CityPreferences { NoDefend = 7, Count = 2 };
        preferences.BuildOrder[0] = 5; preferences.BuildOrder[1] = 6;
        input.Files["scenario/CITY.TXT"] = "@River Folk\nRiver City\n@STOP\n@EXTRA\nRiver Extra\n@STOP\n";
        input.Files["home/CITY.TXT"] = "@River Folk\nHome River\n@STOP\n@Hill Folk\nHill City\n@STOP\n";
        input.Files["scenario/CITYPREF.TXT"] = "@AUTOBUILD\nSelected Library\nUnknown building\n";
        SinglePlayerGameplayContent? content = null;
        var f = new GameplayFixture(content: g =>
        {
            g.Data.Rules.Improvements[2] = new() { Identity = new("selected-library", "Selected Library") };
            return content = BindClassicGameplay(g, input, preferences);
        });
        var c = content!; Equal(true, ReferenceEquals(preferences, c.CityPreferences));
        Equal(7, preferences.NoDefend); Equal(1, preferences.Count); Equal(2, preferences.BuildOrder[0]); Equal(6, preferences.BuildOrder[1]);
        Equal("River City", c.Names.Select("River Folk", 1).Text); Equal("River Extra", c.Names.Select("River Folk", 2).Text);
        Equal("Hill City", c.Names.Select("Hill Folk", 1).Text); Equal("Selected food", c.FoodName);
        Equal(true, c.MapCompatibility.BelongsTo(f.World.Map)); Equal(true, ReferenceEquals(GameplayContent.Builtin.Values, c.Values));
        Equal("scenario", input.Directories.Search); Equal("home", input.Directories.Working);
        Equal(false, input.Opens.Any(p => p.Contains("GAME", StringComparison.Ordinal)));
        input.Files.Remove("scenario/CITYPREF.TXT"); BindClassicGameplay(f, input, preferences);
        Equal(0, preferences.Count); Equal(7, preferences.NoDefend); Equal(2, preferences.BuildOrder[0]);
        input.Files["scenario/CITYPREF.TXT"] = "@NODEFEND\n@AUTOBUILD\nSelected Library\n";
        BindClassicGameplay(f, input, preferences); Equal(1, preferences.NoDefend); Equal(1, preferences.Count);
        f.F.Consumed();
    }

    // V06/F03: option reads occur on demand and obey selected filename/language and section fallback.
    private static void ClassicGameplayContentLazyOptions()
    {
        var input = new ClassicTextInput(); input.Directories.Extension = "FRE";
        SinglePlayerGameplayContent? content = null;
        var f = new GameplayFixture(content: g => content = BindClassicGameplay(g, input, new(), "OPTIONS"));
        Equal(false, input.Opens.Any(p => p.Contains("OPTIONS", StringComparison.Ordinal)));
        input.Files["home/OPTIONS.FRE"] = GameplayOptions;
        Equal("Embassy selected", content!.MissionOptions()!.Embassy); Equal("Stay home", content.RevoltOptions()!.Cancel);
        var previous = content.MissionOptions()!;
        input.Files["scenario/OPTIONS.FRE"] = GameplayOptions.Replace("Embassy selected", "Changed embassy", StringComparison.Ordinal);
        Equal("Changed embassy", content.MissionOptions()!.Embassy); Equal("Embassy selected", previous.Embassy);
        Equal(" (Selected spy)", previous.SpyAnnotation);
        input.Files["scenario/OPTIONS.FRE"] = "@UNRELATED\nOther text\n";
        Equal("Embassy selected", content.MissionOptions()!.Embassy);
        input.Files["scenario/OPTIONS.FRE"] = "@SPYOPTIONS\nOnly one line\n";
        int closes = input.Traces["scenario/OPTIONS.FRE"].Closes;
        Throws<InvalidDataException>(() => content.MissionOptions()); Equal(closes + 1, input.Traces["scenario/OPTIONS.FRE"].Closes);
        input.Files["scenario/OPTIONS.FRE"] = "@DISSIDENTOPTIONS\nOnly one line\n";
        closes = input.Traces["scenario/OPTIONS.FRE"].Closes;
        Throws<InvalidDataException>(() => content.RevoltOptions()); Equal(closes + 1, input.Traces["scenario/OPTIONS.FRE"].Closes);
        input.Files.Clear(); Equal(null, content.MissionOptions()); Equal(null, content.RevoltOptions()); f.F.Consumed();
    }

    // V06: the actual unified graph retains selected options; malformed late content preserves Contact.
    private static void ClassicGameplayContentMissionGraph()
    {
        foreach (bool malformed in new[] { false, true })
        {
            var input = new ClassicTextInput();
            var f = new GameplayFixture(content: g => BindClassicGameplay(g, input, new()));
            f.Data.Unit(0).Type = new(47); f.Data.Unit(0).MovesUsed = 0;
            f.Data.Rules.Units[47] = f.Data.Rules.Units[47] with { Role = RuleRole.Diplomacy };
            BuildingQueries.SetBuilding(f.World, new(0), BuildingIds.Palace, false); f.World.Civilizations[1].Gold = 30000;
            input.Files["scenario/GAME.TXT"] = malformed ? "@SPYOPTIONS\nOnly one line\n" : GameplayOptions;
            using var operation = f.Services.Missions.Cities.Begin(0, new(0), 77, "content-mission");
            if (malformed)
            {
                Throws<InvalidDataException>(() => operation.Resume()); Equal(null, f.World.Espionage.LastMission);
                Equal(1, input.Traces["scenario/GAME.TXT"].Closes);
            }
            else
            {
                var menu = ((CityMissionDecision.Menu)operation.Resume()!).Request;
                Equal("Embassy selected (Selected spy)", menu.Options.Single(o => o.Mission == CityMission.Embassy).Text);
                Equal("Incite selected", menu.Options.Single(o => o.Mission == CityMission.Incite).Text);
                int opens = input.Opens.Count; Equal(menu, ((CityMissionDecision.Menu)operation.Resume()!).Request); Equal(opens, input.Opens.Count);
                input.Files["scenario/GAME.TXT"] = GameplayOptions.Replace("Pay normally", "New payment text", StringComparison.Ordinal);
                var revolt = (CityMissionDecision.Revolt)operation.Resume(new((int)CityMission.Incite))!;
                var offer = ((RevoltDecision.Offer)revolt.Decision).Request;
                Equal("New payment text", offer.Options.Single(o => o.Value == 1).Text);
                Equal(CityMission.Incite, f.World.Espionage.LastMission); operation.Resume(new(0));
                for (int i = 0; i < 50 && !operation.Completed; ++i) Equal(null, operation.Resume());
                Equal(true, operation.Completed); Equal(new UnitType(47), f.Data.Unit(0).Type); Equal(30000, f.World.Civilizations[1].Gold);
            }
            Equal(true, f.World.Civilizations[1].Treaties[2].Contact); Equal(true, f.World.Civilizations[2].Treaties[1].Contact);
            f.F.Consumed();
        }
    }
}
