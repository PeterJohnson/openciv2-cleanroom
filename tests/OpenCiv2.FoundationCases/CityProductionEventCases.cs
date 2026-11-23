using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Rules;
using OpenCiv2.Formats;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static void LocalCityProductionEvents()
    {
        foreach (int completedItem in new[] { -BuildingIds.Temple, 2 })
        {
            BytesEqual(PlayProductionEvent(false, completedItem), PlayProductionEvent(true, completedItem));
        }
    }

    private static byte[] PlayProductionEvent(bool automationChooses, int completedItem)
    {
        using var game = new LocalGame();
        var pioneer = game.Player.OwnUnits().Records.Single(u => u.Type.Value == 0);
        RunLocal(game, false, "found production event city", LocalPlayCommands.Found,
            new("unit", new CommandValue.Unit(new(pioneer.Slot, pioneer.Token))),
            new("name", new CommandValue.Text("Workshop")));
        var saved = ClassicFiles.DecodeStoredSave(game.SaveClassic());
        var civilizations = saved.Civilizations.ToArray();
        var civ = StateConversion.ToLive(civilizations[1]);
        civ.Advances = default(AdvanceSet).With(0);
        civ.NumAdvances = 1;
        civilizations[1] = StateConversion.ToStored(civ);
        var initial = saved with
        {
            Civilizations = new(civilizations),
            Cities = new([saved.Cities[0] with { ShieldBox = 40 }])
        };
        Equal(LocalLoadResult.Loaded, game.LoadClassic(ClassicFiles.EncodeStoredSave(initial)));
        var automation = game.Bind(ControllerKind.Automation);
        var secondAutomation = game.Bind(ControllerKind.Automation);
        var city = game.Player.OwnCities().Records.Single();
        CityTarget target = new(city.Slot, city.Token);
        var buildRequest = new CommandRequest(LocalPlayCommands.Production,
            new([new("city", new CommandValue.City(target)), new("type", new CommandValue.Integer(completedItem))]));
        Equal(CommandStatus.Pending, game.Player.Submit(buildRequest).Status);
        Equal(true, game.AdvanceOne());
        Equal(0, automation.TakeCityProductionEvents().Events.Count);

        var turn = EndLocalTurn(game, false, 2);
        Equal(true, turn.Committed.Any(e => e.Name == (completedItem < 0 ? "city.improvement_built" : "unit.produced")));
        var events = automation.TakeCityProductionEvents();
        var completion = events.Events.Single();
        Equal(0, events.Dropped);
        Equal(target, completion.City);
        Equal(completedItem, completion.Item);
        Equal(turn.Correlation, completion.Cause);
        Equal(true, completion.Revision > 0 && completion.Revision <= game.Player.OwnCities().Observation.Revision);
        Equal(completion, secondAutomation.TakeCityProductionEvents().Events.Single());
        Equal(0, automation.TakeCityProductionEvents().Events.Count);
        Equal(0, game.Player.TakeCityProductionEvents().Events.Count);
        Equal(0, game.Bind(ControllerKind.Automation).TakeCityProductionEvents().Events.Count);

        var next = automation.ObserveLocalPlay()!.City!.Production.Single(option => option.Id == 4);
        var actor = automationChooses ? automation : game.Player;
        var choice = actor.Submit(new(LocalPlayCommands.Production,
            new([new("city", new CommandValue.City(completion.City)), new("type", new CommandValue.Integer(next.Id))])));
        Equal(CommandStatus.Pending, choice.Status);
        Equal(true, game.AdvanceOne());
        Equal(CommandStatus.Completed, actor.Result(choice.Correlation.Command)!.Status);
        Equal(0, automation.TakeCityProductionEvents().Events.Count);
        var continued = game.SaveClassic();
        Equal(LocalLoadResult.Loaded, game.LoadClassic(continued));
        Throws<AccessException>(() => automation.TakeCityProductionEvents());
        Equal(0, game.Bind(ControllerKind.Automation).TakeCityProductionEvents().Events.Count);
        return continued;
    }
}
