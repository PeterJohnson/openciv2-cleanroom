using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Console;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;
using OpenCiv2.Formats;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    /// <summary>Plays the direct-client reference used by the isolated native input scenario.</summary>
    public static (byte[] Saved, byte[] Continued) LocalPlayReference()
    {
        using var game = new LocalGame();
        byte[] saved = PlayAlderValley(game, false);
        ContinueAlderValley(game, saved, false);
        return (saved, game.SaveClassic());
    }

    private static void LocalPlayRoundTrip()
    {
        using var direct = new LocalGame();
        using var console = new LocalGame();
        var directSave = PlayAlderValley(direct, false);
        var consoleSave = PlayAlderValley(console, true);
        BytesEqual(directSave, consoleSave);

        ContinueAlderValley(direct, directSave, false);
        ContinueAlderValley(console, consoleSave, true);
        BytesEqual(direct.SaveClassic(), console.SaveClassic());
    }

    private static byte[] PlayAlderValley(LocalGame game, bool console)
    {
        var player = game.Player;
        var second = game.Bind(ControllerKind.Scripted);
        var settler = player.OwnUnits().Records.Single(u => u.Type.Value == 0);
        var rider = player.OwnUnits().Records.Single(u => u.Type.Value == 4);
        NamedValue settlerValue = new("unit", new CommandValue.Unit(new(settler.Slot, settler.Token)));
        NamedValue riderValue = new("unit", new CommandValue.Unit(new(rider.Slot, rider.Token)));
        string st = $"{settler.Slot.Value}:{settler.Token.Value}";
        string rt = $"{rider.Slot.Value}:{rider.Token.Value}";
        var initial = player.ObserveMap()!;
        var queued = player.Submit(new(LocalPlayCommands.Move, new([
            riderValue, new("direction", new CommandValue.Integer(1))])));
        Equal(LocalLoadResult.Busy, game.LoadClassic(ReadOnlyMemory<byte>.Empty));
        Equal(true, player.Cancel(queued.Correlation.Command).Accepted);
        Equal(true, game.AdvanceOne());
        Equal(CommandStatus.Cancelled, player.Result(queued.Correlation.Command)!.Status);
        Equal(rider, player.OwnUnits().Records.Single(u => u.Slot == rider.Slot));
        Equal(true, initial.Tiles.Any(t => t.Terrain is null));
        RunLocal(game, console, $"unit.select unit={rt}", LocalPlayCommands.Select, riderValue);
        RunLocal(game, console, $"unit.move unit={rt} direction=1", LocalPlayCommands.Move, riderValue,
            new("direction", new CommandValue.Integer(1)));
        var moved = player.OwnUnits().Records.Single(u => u.Slot == rider.Slot);
        Equal(new MapPoint(12, 8), moved.Position);
        Equal((byte)1, moved.MovesUsed);
        Equal(true, player.ObserveMap()!.Tiles.Count(t => t.Terrain is not null) >
            initial.Tiles.Count(t => t.Terrain is not null));
        Equal(null, player.ObserveMap()!.Tiles[0].Terrain);

        RunLocal(game, console, $"city.found unit={st} name=\"Alder Crossing\"", LocalPlayCommands.Found, settlerValue,
            new("name", new CommandValue.Text("Alder Crossing")));
        Equal("unit_not_available", player.Submit(new(LocalPlayCommands.Select, new([settlerValue]))).Code);
        var city = player.OwnCities().Records.Single();
        var target = new CityTarget(city.Slot, city.Token);
        var cityValue = new NamedValue("city", new CommandValue.City(target));
        string ct = $"{city.Slot.Value}:{city.Token.Value}";
        Equal(new MapPoint(8, 8), city.Position);
        Equal((sbyte)1, city.Size);
        int worked = player.ObserveLocalPlay()!.City!.Tiles.Single(t => t.Worked && t.Index != 20).Index;
        RunLocal(game, console, $"city.worker city={ct} tile={worked}", LocalPlayCommands.Worker, cityValue,
            new("tile", new CommandValue.Integer(worked)));
        Equal(1, player.ObserveLocalPlay()!.City!.Specialists);
        RunLocal(game, console, $"city.worker city={ct} tile=1", LocalPlayCommands.Worker, cityValue,
            new("tile", new CommandValue.Integer(1)));
        Equal(0, player.ObserveLocalPlay()!.City!.Specialists);
        Equal(true, player.ObserveLocalPlay()!.City!.Tiles.Single(t => t.Index == 1).Worked);
        RunLocal(game, console, $"city.production city={ct} type=4", LocalPlayCommands.Production, cityValue,
            new("type", new CommandValue.Integer(4)));

        var pending = SubmitLocal(game, console, "turn.end", LocalPlayCommands.EndTurn);
        var confirm = AwaitLocal(game, pending);
        Equal(LocalLoadResult.Busy, game.LoadClassic(ReadOnlyMemory<byte>.Empty));
        Equal("decision_or_command_pending", second.Submit(new(LocalPlayCommands.Move,
            new([riderValue, new("direction", new CommandValue.Integer(1))]))).Code);
        Decide(player, console, confirm, null);
        game.AdvanceOne();
        Equal(CommandStatus.Cancelled, player.Result(pending)!.Status);
        Equal(1, player.ObserveLocalPlay()!.Turn);

        bool produced = false, discovered = false;
        for (int turn = 2; turn <= 7; turn++)
        {
            var update = EndLocalTurn(game, console, turn);
            produced |= update.Committed.Any(e => e.Name == "unit.produced");
            discovered |= update.Committed.Any(e => e.Name == "research.completed");
        }
        Equal(true, produced);
        Equal(true, discovered);
        Equal(true, player.OwnUnits().Records.Count >= 2);
        Equal((sbyte)4, player.OwnCities().Records.Single().Production);
        Equal(true, player.ObserveLocalPlay()!.Discovered.Any(a => a.Id == 0));

        RunLocal(game, console, $"unit.fortify unit={rt}", LocalPlayCommands.Fortify, riderValue);
        Equal(UnitOrder.Fortifying, player.OwnUnits().Records.Single(u => u.Slot == rider.Slot).Orders);
        var save = game.SaveClassic();
        var snapshot = LocalPlayerSnapshot.Capture(player);
        Equal(snapshot, LocalPlayerSnapshot.Capture(player));
        BytesEqual(save, game.SaveClassic());
        var stored = ClassicFiles.DecodeStoredSave(save);
        Equal((short)7, stored.Globals.Turn);
        Equal(1, stored.Cities.Count);
        return save;
    }

    private static void ContinueAlderValley(LocalGame game, byte[] save, bool console)
    {
        var retired = game.Player;
        var before = retired.OwnUnits();
        var beforeCity = retired.OwnCities().Records.Single();
        var beforePlay = retired.ObserveLocalPlay()!;
        Equal(LocalLoadResult.InvalidFile, game.LoadClassic(save.AsMemory(0, 20)));
        var stored = ClassicFiles.DecodeStoredSave(save);
        var harder = stored with { Globals = stored.Globals with { Difficulty = 1 } };
        Equal(LocalLoadResult.UnsupportedGame, game.LoadClassic(ClassicFiles.EncodeStoredSave(harder)));
        Equal(retired.Identity, game.Player.Identity);
        Equal(LocalLoadResult.Loaded, game.LoadClassic(save));
        var player = game.Player;
        Equal(true, before.Observation.Runtime != player.Identity.Runtime);
        try
        {
            retired.ObserveMap();
            throw new InvalidOperationException("The outgoing observer remained attached.");
        }
        catch (AccessException error)
        {
            Equal(AccessFailure.SessionEnded, error.Failure);
        }

        Equal(beforePlay.Turn, player.ObserveLocalPlay()!.Turn);
        Equal(beforePlay.ResearchProgress, player.ObserveLocalPlay()!.ResearchProgress);
        Equal(beforePlay.Gold, player.ObserveLocalPlay()!.Gold);
        var city = player.OwnCities().Records.Single();
        Equal(beforeCity with { Token = city.Token }, city);
        Equal(true, beforeCity.Token != city.Token);
        var units = player.OwnUnits().Records;
        Equal(before.Records.Count, units.Count);
        for (int i = 0; i < units.Count; i++)
        {
            Equal(before.Records[i] with { Token = units[i].Token }, units[i]);
            Equal(true, before.Records[i].Token != units[i].Token);
        }

        var oldCity = new CityTarget(beforeCity.Slot, beforeCity.Token);
        Equal("city_not_available", player.Submit(new(LocalPlayCommands.Worker, new([
            new("city", new CommandValue.City(oldCity)), new("tile", new CommandValue.Integer(20))]))).Code);
        Equal(true, CommandText.Complete("city.worker city=", player.Commands(), player.OwnUnits(), player.OwnCities())
            .Single().EndsWith($"{city.Slot.Value}:{city.Token.Value}", StringComparison.Ordinal));
        EndLocalTurn(game, console, 8);
        Equal(UnitOrder.Fortified, player.OwnUnits().Records.Single(u => u.Slot.Value == 1).Orders);
    }

    private static CommandUpdate EndLocalTurn(LocalGame game, bool console, int turn)
    {
        var player = game.Player;
        var id = SubmitLocal(game, console, "turn.end", LocalPlayCommands.EndTurn);
        var confirmation = AwaitLocal(game, id);
        Decide(player, console, confirmation, 1);
        Equal(false, player.Reply(confirmation.Interaction!.Id, 1).Accepted);
        Equal(false, player.Cancel(id).Accepted);
        Equal(true, game.AdvanceOne());
        var update = player.Result(id)!;
        if (update.Interaction is { } research)
        {
            Equal(true, update.Committed.Any(e => e.Name == "turn.advanced"));
            Equal(turn, player.ObserveLocalPlay()!.Turn);
            Equal(false, player.ObserveLocalPlay()!.PlayerPhase);
            Equal(false, player.Cancel(id).Accepted);
            Equal(false, player.Reply(research.Id, -1).Accepted);
            Equal(LocalLoadResult.Busy, game.LoadClassic(ReadOnlyMemory<byte>.Empty));
            Decide(player, console, update, research.Spec.Choices[0]);
            Equal(true, game.AdvanceOne());
            update = player.Result(id)!;
        }

        Equal(CommandStatus.Completed, update.Status);
        Equal(turn, player.ObserveLocalPlay()!.Turn);
        Equal(true, player.ObserveLocalPlay()!.PlayerPhase);
        var delivered = player.Updates().Where(u => u.Correlation.Command == id)
            .SelectMany(u => u.Committed).ToArray();
        Equal(true, delivered.SequenceEqual(update.Committed));
        Equal(0, player.Updates().Count);
        return update;
    }

    private static CommandUpdate AwaitLocal(LocalGame game, CommandId id)
    {
        Equal(true, game.AdvanceOne());
        var update = game.Player.Result(id)!;
        Equal(CommandStatus.AwaitingInteraction, update.Status);
        return update;
    }

    private static void Decide(IPlayerClient player, bool console, CommandUpdate update, int? choice)
    {
        if (console)
        {
            choice = CommandText.Decision(choice is int value ? $"reply {value}" : "cancel");
        }

        var receipt = choice is int selected ? player.Reply(update.Interaction!.Id, selected)
            : player.Cancel(update.Correlation.Command);
        Equal(true, receipt.Accepted);
    }

    private static void RunLocal(LocalGame game, bool console, string line, string name, params NamedValue[] args)
    {
        var id = SubmitLocal(game, console, line, name, args);
        Equal(true, game.AdvanceOne());
        var result = game.Player.Result(id)!;
        if (result.Status != CommandStatus.Completed)
        {
            throw new InvalidOperationException($"{line}: {result.Status} {result.Code}");
        }
    }

    private static CommandId SubmitLocal(LocalGame game, bool console, string line, string name, params NamedValue[] args)
    {
        var request = console ? CommandText.Parse(line, game.Player.Commands()) : new(name, new(args));
        var result = game.Player.Submit(request);
        Equal(CommandStatus.Pending, result.Status);
        return result.Correlation.Command;
    }

    private static void LocalStepZones()
    {
        var world = new GameState(new());
        var rules = new RuleTables { Cosmic = new() { RoadMovementMultiplier = 3 } };
        rules.Units[0] = new() { Movement = 6, Attack = 1 };
        rules.Terrain[(int)Terrain.Grassland] = new() { MovementCost = 1 };
        world.Globals.Rules.FlatWorld = true;
        world.Map.Allocate(new(20, 16));
        for (int i = 0; i < world.Map.Geometry.Area; i++)
        {
            world.Map.Tiles[i] = new() { Terrain = Terrain.Grassland };
        }
        world.Entities.Units.SetEnd(2);
        var stacks = new StackOperations(world, new(500, 64));
        world.Entities.Units.Storage[0] = new() { Token = new(1), Owner = new(1), Position = new(-1, -1) };
        world.Entities.Units.Storage[1] = new() { Token = new(2), Owner = new(2), Position = new(-1, -1) };
        stacks.AttachAtFront(0, new(8, 8), false);
        stacks.AttachAtFront(1, new(9, 7), false);
        var random = new GameplayRandom(1, "local-step");
        var result = LandMovement.Step(world, rules, new(0), 1, random);
        Equal("zone_of_control", result.Code);
        Equal(true, result.Changed);
        Equal(new MapPoint(8, 8), world.Entities.Units.Storage[0].Position);
        Equal((byte)0, world.Entities.Units.Storage[0].MovesUsed);
        Equal(true, world.Entities.Units.Storage[1].Visibility.Contains(1));
        Equal(true, world.Map.TileAt(new(9, 7)).RevealedTo.Contains(1));
        world.Civilizations[1].Treaties[2].Alliance = true;
        Equal("moved", LandMovement.Step(world, rules, new(0), 1, random).Code);
        Equal((byte)3, world.Entities.Units.Storage[0].MovesUsed);
    }
}
