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
    private sealed class ThroneFixture
    {
        public RevelationFixture Data { get; }
        public GameState World => Data.World;
        public ThroneProgress Throne { get; }
        public ThroneFixture(RevelationFixture? data = null)
        {
            Data = data ?? new(); Data.AddCity(0, 1, new(10, 10), 4);
            World.Globals.CivsAlive = CivilizationSet.Only(1).With(2); World.Globals.BarbarianActivity = 2;
            World.Globals.Turn = 2; World.Globals.Difficulty = Difficulty.Chieftain; World.Globals.GameOptions.Throne = true;
            World.Multiplayer.LocalHuman = 1;
            BuildingQueries.SetBuilding(World, new(0), BuildingIds.Palace, true);
            Throne = new(World, ScoreFor(Data));
        }
        public void CompletePieces()
        { for (int i = 0; i < 8; ++i) World.Civilizations[1].ThroneRoom[i] = 4; }
    }
    private static void ThroneThresholdArithmetic()
    {
        foreach (var (difficulty, stage, expected) in new[] { (0,-1,1),(0,0,1),(0,1,4),(0,2,11),(5,3,7),
            (0,32768,-268378111),(0,65535,1073627137),(5,32768,536887297),(255,32768,-2031615),(0,int.MaxValue-1,-536870907) })
        {
            var f = new ThroneFixture(); f.World.Globals.Difficulty = (Difficulty)difficulty;
            Equal(expected, f.Throne.Threshold(stage));
        }
        var normal = new ThroneFixture(); Throws<ArgumentOutOfRangeException>(() => normal.Throne.Threshold(int.MaxValue));
    }
    private static void ThroneProgressAdmission()
    {
        foreach (int gate in new[] { 0, 1, 2, 3, 4, 5 })
        {
            var f = new ThroneFixture(); f.World.Score.Total = 12345;
            if (gate == 0) f.World.Globals.Status.Scenario = true;
            if (gate == 1) f.World.Globals.Multiplayer = GameMode.Hotseat;
            if (gate == 2) BuildingQueries.SetBuilding(f.World, new(0), BuildingIds.Palace, false);
            if (gate == 3) f.World.Entities.Cities.Storage[0].Owner = new(2);
            if (gate == 4) f.World.Entities.Cities.Storage[0].Token = default;
            if (gate == 5) f.World.Globals.Turn = 1;
            var result = f.Throne.Check(0); Equal(false, result.Awarded); Equal(12345, f.World.Score.Total);
            Equal((short)0, f.World.Civilizations[1].ThroneRoomUpgrades);
        }
        var email = new ThroneFixture(); email.World.Globals.Multiplayer = GameMode.Email; email.World.Globals.HumanPlayers = default;
        Equal(true, email.Throne.Check(0).Awarded); // Only exact mode 1 is excluded among local modes.
        var acceleration = new ThroneFixture(); acceleration.World.Globals.Turn = 41;
        Equal(false, acceleration.Throne.Check(2).Awarded); acceleration.World.Globals.Turn = 42;
        Equal(true, acceleration.Throne.Check(2).Awarded);
    }
    private static void ThroneActualScoreAndOneAward()
    {
        foreach (int size in new[] { 3, 4, 127 })
        {
            var f = new ThroneFixture(); f.World.Entities.Cities.Storage[0].Size = (sbyte)size;
            var result = f.Throne.Check(0); Equal(size >= 4, result.Awarded); Equal(size, f.World.Score.Citizens);
            Equal((short)(size >= 4 ? 1 : 0), result.Progress); Equal(size >= 4, result.Upgrade is not null);
            result.Upgrade?.Dispose();
        }
        var hidden = new ThroneFixture(); hidden.World.Globals.GameOptions.Throne = false; hidden.World.Entities.Cities.Storage[0].Size = 127;
        var progress = hidden.Throne.Check(0); Equal(true, progress.Awarded); Equal((short)1, progress.Progress); Equal(null, progress.Upgrade);
        Equal((short)2, hidden.Throne.Check(0).Progress); // One increment, not catch-up to the highest affordable stage.
    }
    private static void ThroneProgressCountLimits()
    {
        foreach (var (before, after, display) in new[] { (36, 37, true), (37, 38, false), (-2, -1, true), (32767, -32768, true) })
        {
            var f = new ThroneFixture(); f.World.Civilizations[1].ThroneRoomUpgrades = (short)before;
            if (before is 36 or 37) { f.World.Globals.Difficulty = Difficulty.Deity; f.World.Civilizations[1].FutureTechs = 255; }
            var result = f.Throne.Check(0); Equal(true, result.Awarded); Equal((short)after, result.Progress);
            Equal(display, result.Upgrade is not null); result.Upgrade?.Dispose();
        }
    }
    private static void ThroneWrapperAndMode()
    {
        var f = new ThroneFixture(); f.CompletePieces(); f.World.Civilizations[1].ThroneRoom[7] = 3; f.World.Civilizations[1].ThroneRoom[8] = 127;
        Equal(null, f.Throne.BeginUpgrade(1)); // The specified availability wrapper omits piece 7.
        f.World.Civilizations[1].ThroneRoom[8] = 0;
        using var piece = f.Throne.BeginUpgrade(1)!; Equal(false, piece.Overlays);
        Equal(new ThroneSelection(ThroneUpgradeKind.Piece, 7), piece.Choices().Single());
        Equal(true, piece.Select(new(ThroneUpgradeKind.Piece, 7))); Equal((byte)4, f.World.Civilizations[1].ThroneRoom[7]);
        using var overlay = f.Throne.BeginUpgrade(1)!; Equal(true, overlay.Overlays); Equal(7, overlay.Choices().Count);
    }
    private static void ThronePieceSelectionAndCancellation()
    {
        var f = new ThroneFixture(); f.World.Civilizations[1].ThroneRoom[0] = 3; f.World.Civilizations[1].ThroneRoom[1] = 4;
        f.World.Civilizations[1].ThroneRoom[9] = 201;
        using var choice = f.Throne.BeginUpgrade(1)!;
        foreach (var invalid in new[] { new ThroneSelection(ThroneUpgradeKind.Piece, -1), new(ThroneUpgradeKind.Piece, 8), new(ThroneUpgradeKind.Piece, 1), new(ThroneUpgradeKind.Overlay, 0) })
        { Equal(false, choice.Select(invalid)); Equal(false, choice.Completed); }
        Equal(true, choice.Select(new(ThroneUpgradeKind.Piece, 0))); Equal(false, choice.Select(new(ThroneUpgradeKind.Piece, 2)));
        Equal((byte)4, f.World.Civilizations[1].ThroneRoom[0]); Equal((byte)0, f.World.Civilizations[1].ThroneRoom[2]);
        Equal((byte)201, f.World.Civilizations[1].ThroneRoom[9]);
        using var cancelled = f.Throne.BeginUpgrade(1)!; cancelled.Cancel(); Equal(false, cancelled.Select(new(ThroneUpgradeKind.Piece, 2)));
        var disposed = f.Throne.BeginUpgrade(1)!; disposed.Dispose(); Throws<ObjectDisposedException>(() => disposed.Select(new(ThroneUpgradeKind.Piece, 2)));
    }
    private static void ThroneOverlaySelection()
    {
        var f = new ThroneFixture(); f.CompletePieces(); f.World.Civilizations[1].ThroneRoom[8] = 128 | 16;
        using var choice = f.Throne.BeginUpgrade(1)!;
        Equal(false, choice.Select(new(ThroneUpgradeKind.Overlay, 4))); // Explicit semantic selections do not use paired pixel fallback.
        Equal(false, choice.Select(new(ThroneUpgradeKind.Overlay, 7))); Equal(false, choice.Select(new(ThroneUpgradeKind.Piece, 0)));
        Equal(true, choice.Select(new(ThroneUpgradeKind.Overlay, 5))); Equal((byte)(128 | 16 | 32), f.World.Civilizations[1].ThroneRoom[8]);
        Equal(false, choice.Select(new(ThroneUpgradeKind.Overlay, 6)));
        f.World.Civilizations[1].ThroneRoom[8] = 255; Equal(null, f.Throne.BeginUpgrade(1));
    }
    private static void ThroneViewAndClassicRoundTrip()
    {
        var f = new ThroneFixture(); f.World.Civilizations[1].ThroneRoom[9] = 213;
        var progress = f.Throne.Check(0); var before = f.Throne.View(1); Equal((short)1, before.Progress);
        Equal(true, progress.Upgrade!.Select(new(ThroneUpgradeKind.Piece, 3))); progress.Upgrade.Dispose();
        Equal((byte)0, before.Pieces[3]); Equal((byte)1, f.Throne.View(1).Pieces[3]);
        f.World.Score.Total = 998; f.Throne.View(1); Equal(998, f.World.Score.Total);
        var encoded = ClassicRecords.Encode(StateConversion.ToStored(f.World.Civilizations[1]));
        var restored = StateConversion.ToLive(ClassicRecords.DecodeStoredCivilization(encoded));
        Equal((short)1, restored.ThroneRoomUpgrades); Equal((byte)1, restored.ThroneRoom[3]); Equal((byte)213, restored.ThroneRoom[9]);
    }
    private static void ThroneFailuresAndActorChange()
    {
        var f = new ThroneFixture(); f.World.Globals.WonderCity[0] = short.MaxValue;
        Throws<IndexOutOfRangeException>(() => f.Throne.Check(0)); Equal((short)0, f.World.Civilizations[1].ThroneRoomUpgrades);
        Equal(4, f.World.Score.Citizens);
        f.World.Globals.WonderCity[0] = -1; var result = f.Throne.Check(0); f.World.Multiplayer.LocalHuman = 2;
        Equal(false, result.Upgrade!.ActorCurrent); Equal(false, result.Upgrade.Select(new(ThroneUpgradeKind.Piece, 0)));
        Equal((short)1, f.World.Civilizations[1].ThroneRoomUpgrades); result.Upgrade.Dispose();
        f.World.Globals.Multiplayer = GameMode.Tcp;
        Throws<InvalidOperationException>(() => f.Throne.Check(0)); Throws<InvalidOperationException>(() => f.Throne.BeginUpgrade(1));
        Throws<InvalidOperationException>(() => f.Throne.View(1));
    }
    private static void ThronePrivateSession()
    {
        foreach (bool cancel in new[] { false, true })
        {
            using var runtime = new SessionRuntime(new(), new(741)); var f = new ThroneFixture(new(runtime.Process, runtime.World));
            ParticipantId ownerSeat = default;
            runtime.RegisterCommand(new(new(1, "test.throne", "Advance throne progress", AccessClass.Player, new([]), new([])), (_, _, _) => new(),
                _ => new ThroneProgressOperation(_ => ownerSeat)));
            runtime.BeginClientSession(new()); var actor = SessionClient(runtime, 2); var owner = SessionClient(runtime, 1); ownerSeat = owner.Identity.Participant;
            var command = actor.Submit(new("test.throne", new([]))).Correlation.Command; runtime.AdvanceOne();
            var prompt = RequiredInteraction(owner, command); Equal(null, RequiredResult(actor, command).Interaction);
            Equal((short)1, f.World.Civilizations[1].ThroneRoomUpgrades); Equal(false, actor.Reply(prompt, 0).Accepted);
            Equal(false, owner.Reply(prompt, 100).Accepted);
            if (cancel) owner.Cancel(command); else owner.Reply(prompt, 0);
            runtime.AdvanceOne(); Equal(cancel ? CommandStatus.Cancelled : CommandStatus.Completed, RequiredResult(actor, command).Status);
            Equal((byte)(cancel ? 0 : 1), f.World.Civilizations[1].ThroneRoom[0]); Equal((short)1, f.World.Civilizations[1].ThroneRoomUpgrades);
            Equal(false, owner.Reply(prompt, 0).Accepted);
        }
    }
}
