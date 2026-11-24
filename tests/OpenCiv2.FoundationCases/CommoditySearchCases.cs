using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Formats;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static void CommoditySearchGateAndLiveReports()
    {
        var f = CommodityFixture(); CargoNames(f);
        using var unavailable = new CommoditySearch(f.World, f.Rules, 1);
        Equal(null, unavailable.Resume()); Equal(true, unavailable.Completed);
        f.World.Civilizations[1].Advances = default(AdvanceSet).With(AdvanceIds.Trade);
        Equal(null, unavailable.Resume()); // A completed entry cannot be reopened by changing knowledge.
        using var search = new CommoditySearch(f.World, f.Rules, 1);
        var menu = (CommoditySearchMenu)search.Resume(3)!;
        Sequence(Enumerable.Range(0, 16), menu.Options.Select(o => o.Commodity)); Equal(menu, search.Resume());
        ref var city = ref f.World.Entities.Cities.Storage[0]; city.Supplies[0] = city.Demands[0] = 3;
        var report = (CommoditySearchReport)search.Resume(3)!; Equal(2, report.Cities.Count);
        Equal(report, search.Resume()); city.Supplies[0] = -3; city.Demands[0] = -3;
        f.World.Civilizations[1].Advances = default; f.Rules.Commodities[3] = new("cloth", "Renamed cloth");
        menu = (CommoditySearchMenu)search.Resume(0)!; Equal("Renamed cloth", menu.Options[3].Name);
        report = (CommoditySearchReport)search.Resume(3)!; Equal(0, report.Cities.Count); Equal("Renamed cloth", report.Name);
        Equal(null, search.Resume(-1)); Equal(true, search.Completed); Equal(null, search.Resume(3));
        Equal((sbyte)-3, city.Supplies[0]); Equal((sbyte)-3, city.Demands[0]);
    }

    private static void CommoditySearchStops()
    {
        var f = CommodityFixture(); CargoNames(f); f.World.Civilizations[1].Advances = default(AdvanceSet).With(AdvanceIds.Trade);
        using var invalid = new CommoditySearch(f.World, f.Rules, 1); invalid.Resume();
        Throws<InvalidOperationException>(() => invalid.Resume(16)); Equal(false, invalid.Completed);
        Throws<InvalidOperationException>(() => invalid.Resume(0));
        using var disposed = new CommoditySearch(f.World, f.Rules, 1); disposed.Resume(); disposed.Dispose();
        Throws<ObjectDisposedException>(() => disposed.Resume(0));
        foreach (var kind in new[] { OperationInputKind.Cancel, OperationInputKind.Timeout })
        {
            using var op = new CommoditySearchOperation(1, new(1)); var context = OutcomeContext(f);
            Equal(CommandStatus.AwaitingInteraction, op.Resume(context, new()).Status);
            var done = op.Resume(context, new(kind)); Equal(CommandStatus.Completed, done.Status);
            Equal(done, op.Resume(context, new(OperationInputKind.Choice, 3)));
        }
    }

    private static void CommoditySearchPrivateSession()
    {
        using var runtime = new SessionRuntime(new(), new(277));
        var f = CreationFixture(new(runtime.Process, runtime.World)); CargoNames(f);
        f.World.Map.Allocate(new(20, 12)); f.AddCity(0, 2, new(8, 6), 3); f.AddCity(1, 3, new(10, 6), 4);
        f.World.Civilizations[2].Advances = default(AdvanceSet).With(AdvanceIds.Trade);
        f.World.Entities.Cities.Storage[0].Name = "Owned"; f.World.Entities.Cities.Storage[0].Supplies[0] = 3;
        f.World.Entities.Cities.Storage[1].Name = "Hidden"; f.World.Entities.Cities.Storage[1].Supplies[0] = 3;
        ParticipantId recipient = default;
        runtime.RegisterCommand(new(new(1, "test.search", "Search", AccessClass.Player, new([]), new([])),
            (_, _, _) => new(), _ => new CommoditySearchOperation(2, recipient)));
        runtime.BeginClientSession(new()); var actor = SessionClient(runtime, 1); var viewer = SessionClient(runtime, 2); recipient = viewer.Identity.Participant;
        var command = actor.Submit(new("test.search", new([]))).Correlation.Command; runtime.AdvanceOne();
        var menu = RequiredInteraction(viewer, command); Equal(null, RequiredResult(actor, command).Interaction);
        Equal(false, actor.Reply(menu, 3).Accepted); Equal(true, viewer.Reply(menu, 3).Accepted); runtime.AdvanceOne();
        var result = RequiredResult(viewer, command); var report = RequiredInteraction(viewer, command);
        Equal(true, result.Interaction!.Spec.Prompt.Contains("Owned", StringComparison.Ordinal));
        Equal(false, result.Interaction.Spec.Prompt.Contains("Hidden", StringComparison.Ordinal));
        Equal(true, viewer.Reply(report, 0).Accepted); runtime.AdvanceOne();
        Equal(false, viewer.Reply(report, 0).Accepted); menu = RequiredInteraction(viewer, command);
        Equal(true, viewer.Reply(menu, -1).Accepted); runtime.AdvanceOne();
        Equal(CommandStatus.Completed, RequiredResult(actor, command).Status);
        Equal(0, RequiredResult(actor, command).Committed.Count);
    }

    private static byte[] LocalCommodityInput()
    {
        using var author = new LocalGame(); var pioneer = author.Player.OwnUnits().Records.Single(u => u.Type.Value == 0);
        RunLocal(author, false, "found commodity city", LocalPlayCommands.Found,
            new("unit", new CommandValue.Unit(new(pioneer.Slot, pioneer.Token))), new("name", new CommandValue.Text("Trade Port")));
        var saved = ClassicFiles.DecodeStoredSave(author.SaveClassic());
        var cities = saved.Cities.ToArray(); var city = StateConversion.ToLive(cities[0]);
        city.Supplies[0] = city.Demands[0] = 3; city.Supplies[1] = city.Demands[1] = -3;
        city.Supplies[2] = city.Demands[2] = 0; city.Flags.TradeDirty = false;
        cities[0] = StateConversion.ToStored(city);
        var civs = saved.Civilizations.ToArray(); var civ = StateConversion.ToLive(civs[1]);
        civ.Advances = civ.Advances.With(AdvanceIds.Trade); civ.NumAdvances = 1; civs[1] = StateConversion.ToStored(civ);
        return ClassicFiles.EncodeStoredSave(saved with { Cities = new(cities), Civilizations = new(civs) });
    }

    private static void SearchLocalCommodities(LocalGame game, bool console)
    {
        var menu = AwaitLocal(game, SubmitLocal(game, console, LocalPlayCommands.Commodities, LocalPlayCommands.Commodities));
        Equal("Search commodities", menu.Interaction!.Spec.Prompt); Equal(17, menu.Interaction.Spec.Options.Count);
        var report = ReplyLocal(game, console, menu, 3);
        Equal(true, report.Interaction!.Spec.Prompt.Contains("Trade Port (1): Demands Cloth", StringComparison.Ordinal));
        Equal(true, report.Interaction.Spec.Prompt.Contains("Trade Port (1): Supplies Cloth", StringComparison.Ordinal));
        Equal(false, game.Player.Reply(menu.Interaction.Id, 3).Accepted);
        menu = ReplyLocal(game, console, report, 0); report = ReplyLocal(game, console, menu, 15);
        Equal(true, report.Interaction!.Spec.Prompt.Contains("No known cities", StringComparison.Ordinal));
        menu = ReplyLocal(game, console, report, 0);
        Equal(CommandStatus.Completed, ReplyLocal(game, console, menu, -1).Status);
    }

    public static (byte[] Initial, byte[] Queried, byte[] Reloaded) LocalCommodityReference()
    {
        byte[] initial = LocalCommodityInput(); using var game = new LocalGame();
        Equal(LocalLoadResult.Loaded, game.LoadClassic(initial)); byte[] before = game.SaveClassic();
        SearchLocalCommodities(game, false); byte[] queried = game.SaveClassic(); BytesEqual(before, queried);
        Equal(LocalLoadResult.Loaded, game.LoadClassic(queried)); SearchLocalCommodities(game, false);
        return (initial, queried, game.SaveClassic());
    }

    private static void LocalCommoditySearchAndReplay()
    {
        foreach (bool console in new[] { false, true })
        {
            var recorder = new LocalReplayRecorder(new string('e', 64)); using var game = new LocalGame(recorder);
            var command = SubmitLocal(game, console, LocalPlayCommands.Commodities, LocalPlayCommands.Commodities);
            Equal(true, game.AdvanceOne()); var absent = game.Player.Result(command)!;
            Equal(CommandStatus.Completed, absent.Status); Equal(null, absent.Interaction);
            Equal(LocalLoadResult.Loaded, game.LoadClassic(LocalCommodityInput()));
            byte[] before = game.SaveClassic(); SearchLocalCommodities(game, console); BytesEqual(before, game.SaveClassic());
            Equal(LocalLoadResult.Loaded, game.LoadClassic(before)); SearchLocalCommodities(game, console);
            BytesEqual(game.SaveClassic(), LocalReplay.Run(recorder.Capture()));
        }
    }
}
