using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.State;
using OpenCiv2.Formats;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static void LocalGlobalThroneHistoryAndWarming()
    {
        foreach (bool console in new[] { false, true })
            foreach (bool cancel in new[] { false, true })
            {
                using var game = new LocalGame(); FoundCouncil(game);
                var source = ClassicFiles.DecodeStoredSave(game.SaveClassic());
                var globals = StateConversion.ToLive(source.Globals);
                globals.Turn = 2; globals.GameOptions.Throne = true;
                globals.WarmingCounter = 11; globals.PollutedTiles = 10; globals.WarmingPollution = 2; globals.NextHistoryTurn = 0;
                var civs = source.Civilizations.ToArray(); civs[1] = civs[1] with { FutureTechs = 100, ThroneRoomUpgrades = 0 };
                source = source with { Globals = StateConversion.PreserveOpaque(StateConversion.ToStored(globals), source.Globals), Civilizations = new(civs) };
                Equal(LocalLoadResult.Loaded, game.LoadClassic(ClassicFiles.EncodeStoredSave(source)));
                var step = AwaitLocal(game, SubmitLocal(game, console, LocalPlayCommands.EndTurn, LocalPlayCommands.EndTurn));
                step = ReplyLocal(game, console, step, 1);
                Equal(true, step.Interaction!.Spec.Prompt.StartsWith("Throne room", StringComparison.Ordinal));
                var throne = step.Interaction.Id; int selected = step.Interaction.Spec.Choices[0];
                Equal(2, game.Player.ObserveLocalPlay()!.Turn); Equal(false, game.Player.ObserveLocalPlay()!.PlayerPhase);
                Equal(1, step.Committed.Count(e => e.Name == "world.warming")); Equal(1, step.Committed.Count(e => e.Name == "throne.progress"));
                Equal(false, step.Committed.Any(e => e.Name == "turn.global.advanced"));
                step = GovernmentReply(game, console, step, cancel ? null : selected);
                Equal(true, step.Interaction!.Spec.Prompt.StartsWith("Historian report", StringComparison.Ordinal));
                Equal(3, game.Player.ObserveLocalPlay()!.Turn); Equal(false, game.Player.Reply(throne, selected).Accepted);
                step = ReplyLocal(game, console, step, 0);
                for (int n = 0; n < 20 && step.Interaction is { } pending; ++n)
                    step = ReplyLocal(game, console, step, pending.Spec.Choices[0]);
                Equal(CommandStatus.Completed, step.Status); Equal(1, step.Committed.Count(e => e.Name == "turn.global.advanced"));
                Equal(cancel ? 0 : 1, step.Committed.Count(e => e.Name == "throne.upgraded"));
                var saved = ClassicFiles.DecodeStoredSave(game.SaveClassic());
                Equal((sbyte)12, saved.Globals.WarmingCounter); Equal((short)0, saved.Globals.WarmingPollution);
                Equal((short)1, saved.Civilizations[1].ThroneRoomUpgrades);
                Equal((byte)(source.Civilizations[1].ThroneRoom[selected] + (cancel ? 0 : 1)), saved.Civilizations[1].ThroneRoom[selected]);
                Equal(true, saved.Globals.NextHistoryTurn is >= 23 and <= 62);
                Equal(LocalLoadResult.Loaded, game.LoadClassic(game.SaveClassic()));
                Equal((short)1, ClassicFiles.DecodeStoredSave(game.SaveClassic()).Civilizations[1].ThroneRoomUpgrades);
            }
    }

    private static void LocalGlobalSingleArrival()
    {
        foreach (bool console in new[] { false, true })
        {
            using var game = new LocalGame(); FoundCouncil(game);
            var source = ClassicFiles.DecodeStoredSave(game.SaveClassic());
            var globals = StateConversion.ToLive(source.Globals); globals.Status.Scored = true;
            var civs = source.Civilizations.ToArray();
            foreach (int c in new[] { 1, 2 })
            {
                var civ = StateConversion.ToLive(civs[c]); civ.SpaceshipFlags.Launched = true;
                civ.SpaceshipArrivalYear = short.MinValue;
                civs[c] = StateConversion.PreserveOpaque(StateConversion.ToStored(civ), civs[c]);
            }
            source = source with { Globals = StateConversion.PreserveOpaque(StateConversion.ToStored(globals), source.Globals), Civilizations = new(civs) };
            Equal(LocalLoadResult.Loaded, game.LoadClassic(ClassicFiles.EncodeStoredSave(source)));
            var step = AwaitLocal(game, SubmitLocal(game, console, LocalPlayCommands.EndTurn, LocalPlayCommands.EndTurn));
            step = ReplyLocal(game, console, step, 1);
            Equal(true, step.Interaction!.Spec.Prompt.StartsWith("Arrival at Alpha Centauri", StringComparison.Ordinal));
            for (int n = 0; n < 20 && step.Interaction is { } pending; ++n)
            {
                Equal(false, pending.Spec.Prompt.StartsWith("Spaceship landing", StringComparison.Ordinal));
                step = ReplyLocal(game, console, step, pending.Spec.Prompt == "Keep playing?" ? 1 : pending.Spec.Choices[0]);
            }
            Equal(CommandStatus.Completed, step.Status); Equal(1, step.Committed.Count(e => e.Name == "spaceship.arrived"));
            var saved = ClassicFiles.DecodeStoredSave(game.SaveClassic());
            Equal(true, StateConversion.ToLive(saved.Civilizations[1]).SpaceshipFlags.Arrived);
            Equal(false, StateConversion.ToLive(saved.Civilizations[2]).SpaceshipFlags.Arrived);
            Equal(true, game.Player.ObserveLocalPlay()!.PlayerPhase); Equal((short)2, saved.Globals.Turn);
        }
    }
}
