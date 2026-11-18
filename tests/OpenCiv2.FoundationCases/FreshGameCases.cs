using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Formats;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static void PoisonFreshWorld(GameState world, ProcessContext process)
    {
        world.Entities.Units.Storage.Fill(new Unit { Token = new(123), Owner = new(7), X = 9, Flags = 0xffff });
        world.Entities.Cities.Storage.Fill(new City { Token = new(234), Owner = new(6), Name = new([99]), Flags = uint.MaxValue });
        world.Entities.Units.SetEnd(2); world.Entities.Cities.SetEnd(1);
        world.UnitStacks.Suppression = 9; world.UnitStacks.AnimationActive = world.UnitStacks.DetachActive = world.UnitStacks.AttachActive = world.UnitStacks.RelocateActive = world.UnitStacks.BottomActive = true;
        world.UnitStacks.DetachReply = world.UnitStacks.AttachReply = world.UnitStacks.RelocateReply = world.UnitStacks.BottomReply = 11;
        world.UnitDeletion.Active = true; world.UnitDeletion.Reply = -2; world.UnitTotals[1] = 123; world.TurnLoopActive = true; world.MovementMode = (MovementMode)99; world.CivilizationColorFlags[1] = uint.MaxValue;
        world.Globals.Turn = 123; world.Globals.HumanPlayers = 255; world.Globals.ActiveUnit = new(99);
        for (int i = 0; i < 8; ++i)
        {
            world.Civilizations[i].Gold = 555; world.Civilizations[i].Goals[1] = new(9, 8, 7, 6); world.Civilizations[i].StandingGoals[2] = new(9, 8, 7, 6);
            world.Names[i] = new() { Leader = new([88]), Tribe = new([89]), Adjective = new([90]) }; world.PendingLeaderOverrides[i] = 7;
        }
        world.Multiplayer.LocalHuman = world.Multiplayer.ResumeCiv = 7; world.Multiplayer.NetworkHost = true; world.Multiplayer.NetworkTurnCiv = 6; world.Multiplayer.HotseatParticipants = 254;
        world.Events.Loaded = true; world.Events.Collection.Append().SetString(0, new EventString("prior"u8.ToArray())); world.PowerHistory[2][3] = 99;
        world.Layout.Valid = true; world.Views[1] = new(true, true, 3, 4, 5); world.Mood.City = new(2); world.Mood.Happy = 40; world.Mood.Unhappy = -20;
        world.Map.Allocate(new(8, 6)); world.Map.Tiles.Fill(new Tile { Features = 255, Region = 11, Revelation = 254 });
        for (int civ = 1; civ < 8; ++civ) world.Map.RememberedLayer(civ).Fill(77);
        for (int grid = 0; grid < 4; ++grid) world.Map.CoarseGrid(grid).Fill(98);
        world.Map.PatternSeed = 112; world.Map.StoredTopology = 1; world.Map.Regions.Fill(new(1, 2, 3, 4, new([5, 6]))); world.Map.RememberedBarbarian = [1, 2, 3];
        process.Fallback.Value.Region = 62; process.Fallback.Value.Features = 255;
        process.TextParameters.Numbers[0] = -123; Array.Fill(process.TextParameters.Strings, new OwnedList<byte>([65])); process.TextParameters.Line = new([66]);
        var rules = process.Rules; rules.Text.Initialize(); var name = rules.Text.Store("old"u8); Array.Fill(rules.Names.Units, name); rules.Labels.Entries.Add(name); Array.Fill(rules.Labels.Popups, name); rules.Labels.DeclaredCount = 1; rules.Labels.StatusLanguage = 3;
        rules.Definitions.Units[0] = rules.Definitions.Units[0] with { Attack = 123 };
        foreach (var leader in rules.Leaders) { leader.Attack = -91; leader.TimesUsed = leader.CityNameIndex = 165; leader.LeaderName = leader.Plural = leader.Adjective = new(70000, true); Array.Fill(leader.Names, new(80000, true)); foreach (var titles in leader.Titles) Array.Fill(titles, new(90000, true)); }
        rules.DoubleMovementApplied = true;
    }
    private static void FreshGameWorldIsolation()
    {
        var process = new ProcessContext(); using var runtime = new SessionRuntime(process, new(18)); runtime.BeginClientSession(new(13, 17)); var client = SessionClient(runtime); var identity = client.Identity;
        var oldWorld = runtime.World; PoisonFreshWorld(oldWorld, process); var compatibility = new RecordCompatibility(oldWorld.Lifetime);
        runtime.PrepareNewGame(); Equal(false, runtime.Active); CheckRevoked(client, AccessFailure.SessionEnded); var world = runtime.World; var cold = new GameState(new());
        Equal(false, ReferenceEquals(oldWorld, world)); Equal(false, ReferenceEquals(oldWorld.Lifetime, world.Lifetime)); Equal(true, compatibility.For(world.Lifetime) is null);
        Equal(0, world.Entities.Units.End); Equal(0, world.Entities.Cities.End); Equal(new OwnedList<Unit>(cold.Entities.Units.Storage.ToArray()), new(world.Entities.Units.Storage.ToArray())); Equal(new OwnedList<City>(cold.Entities.Cities.Storage.ToArray()), new(world.Entities.Cities.Storage.ToArray()));
        Equal(0, world.UnitStacks.Suppression); Equal(false, world.UnitStacks.AnimationActive || world.UnitStacks.DetachActive || world.UnitStacks.AttachActive || world.UnitStacks.RelocateActive || world.UnitStacks.BottomActive);
        Equal(0, world.UnitStacks.DetachReply | world.UnitStacks.AttachReply | world.UnitStacks.RelocateReply | world.UnitStacks.BottomReply); Equal(false, world.UnitDeletion.Active); Equal(0, world.UnitDeletion.Reply);
        Equal(false, world.TurnLoopActive); Equal((int)MovementMode.View, (int)world.MovementMode); Equal(cold.UnitTotals, world.UnitTotals); Equal(cold.CivilizationColorFlags, world.CivilizationColorFlags); Equal(cold.Globals, world.Globals); Equal(cold.WorldSetup, world.WorldSetup);
        foreach (ref var civ in cold.Civilizations.AsSpan()) { foreach (ref var goal in (Span<CivilizationGoal>)civ.Goals) goal = new(0, 0, -1, 0); foreach (ref var goal in (Span<CivilizationGoal>)civ.StandingGoals) goal = new(0, 0, -1, 0); }
        Equal(new OwnedList<Civilization>(cold.Civilizations), new(world.Civilizations)); Equal(new OwnedList<CivilizationNames>(cold.Names), new(world.Names)); Equal(new OwnedList<sbyte>(cold.PendingLeaderOverrides), new(world.PendingLeaderOverrides)); Equal(cold.Multiplayer, world.Multiplayer);
        Equal(false, world.Events.Loaded); Equal(0, world.Events.Collection.Count); Equal(cold.PowerHistory, world.PowerHistory); Equal(cold.Eliminations, world.Eliminations); Equal(cold.Scenario, world.Scenario); Equal(cold.Layout, world.Layout); Equal(new OwnedList<MapView>(cold.Views), new(world.Views));
        Equal(true, world.Mood.City is null); Equal(0, world.Mood.Happy); Equal(0, world.Mood.Unhappy); Equal(false, world.Map.Allocated); Throws<InvalidOperationException>(() => _ = world.Map.Geometry); Throws<InvalidOperationException>(() => _ = world.Map.Tiles.Length);
        Equal((ushort)0, world.Map.PatternSeed); Equal((ushort)0, world.Map.StoredTopology); Equal(new OwnedList<RegionSummary>(cold.Map.Regions.ToArray()), new(world.Map.Regions.ToArray())); Equal(0, world.Map.RememberedBarbarian.Length);
        runtime.BeginClientSession(new(13, 17)); var next = SessionClient(runtime); Equal(false, next.Identity.Session == identity.Session); Equal(false, next.Identity.Participant == identity.Participant); Equal(false, next.Identity.Controller == identity.Controller); CheckRevoked(client, AccessFailure.Revoked); Equal(0, next.OwnUnits().Records.Count);
        oldWorld.Entities.Units.Storage[0].Token = new(999); Equal(0, next.OwnUnits().Records.Count);
    }
    private static void FreshGameProcessAllowlist()
    {
        var process = new ProcessContext(); using var runtime = new SessionRuntime(process, new(1)); runtime.BeginClientSession(new(13, 17)); PoisonFreshWorld(runtime.World, process);
        process.Tokens.NextUnit = uint.MaxValue; process.Tokens.NextCity = 0; process.OrdinaryPreferences.GameOptions = 0x12345678; process.MultiplayerPreferences.KillOnRetirement = true;
        var ordinary = process.OrdinaryPreferences; var multiplayer = process.MultiplayerPreferences; var rules = process.Rules; var oldDefinitions = rules.Definitions; var oldText = rules.Text; var oldNames = rules.Names; var oldLeader = rules.Leaders[0]; var parameters = process.TextParameters;
        var gameplay = runtime.GameplayRandom; var presentation = runtime.PresentationRandom; gameplay.PrimaryModulo("prior.game", 5); presentation.PrimaryModulo("prior.content", 3); var random = gameplay.Capture(); var content = presentation.Capture();
        runtime.PrepareNewGame(); Equal(uint.MaxValue, process.Tokens.NextUnit); Equal(0U, process.Tokens.NextCity); Equal(ordinary, process.OrdinaryPreferences); Equal(multiplayer, process.MultiplayerPreferences); Equal(random, gameplay.Capture()); Equal(content, presentation.Capture());
        Equal(true, ReferenceEquals(rules, process.Rules)); var cold = new RuleState(); Equal(cold.Definitions.Snapshot(), rules.Definitions.Snapshot()); Equal(false, rules.DoubleMovementApplied); Equal(1, rules.Text.Count); Equal(new OwnedList<byte>("<nil>"u8.ToArray()), rules.Text.Text(0));
        Equal(0, rules.Labels.Entries.Count); Equal(true, rules.Labels.Popups.All(p => p is null)); Equal(0, rules.Labels.DeclaredCount); Equal(0, rules.Labels.StatusLanguage);
        Equal(new OwnedList<LeaderSnapshot>(cold.Leaders.Select(l => l.Snapshot())), new(rules.Leaders.Select(l => l.Snapshot()))); Equal(true, rules.Names.Units.All(v => v == 0)); Equal(true, rules.Names.GovernmentTitles.All(row => row.All(v => v == 0)));
        Equal(true, ReferenceEquals(parameters, process.TextParameters)); Equal(default(Fixed6<int>), parameters.Numbers); Equal(true, parameters.Strings.All(s => s.Count == 0)); Equal(0, parameters.Line.Count); Equal(new Tile(), process.Fallback.Value);
        oldDefinitions.Units[0] = oldDefinitions.Units[0] with { Attack = 255 }; oldNames.Units[0] = 999; oldText.Store("stale"u8); oldLeader.CityNameIndex = 99;
        Equal((byte)0, rules.Definitions.Units[0].Attack); Equal(0U, rules.Names.Units[0]); Equal(1, rules.Text.Count); Equal((byte)0, rules.Leaders[0].CityNameIndex);
        runtime.World.Map.Allocate(new(8, 6)); runtime.World.Map.TileAt(new(-1, -1)).Region = 33; Equal((byte)33, process.Fallback.Value.Region);
    }
    private static void FreshGameRejectsPendingWork()
    {
        using var runtime = new SessionRuntime(new(), new(18)); runtime.RegisterCommand(CommandDefinitionFor(_ => DecisionContinuation())); runtime.BeginClientSession(new()); var seat = runtime.AddParticipant(new(1), new(9)); var client = runtime.Bind(seat, ControllerKind.Scripted);
        var origin = new CommandCorrelation(new(29), new(7), new(41)); var command = runtime.ReceiveRequest(new(9), seat, origin, CommandRequestFor()).Correlation.Command; runtime.AdvanceOne(); var before = RequiredResult(client, command); var world = runtime.World;
        Throws<InvalidOperationException>(() => runtime.PrepareNewGame()); Equal(true, ReferenceEquals(world, runtime.World)); Equal(before, RequiredResult(client, command)); Equal(-5, runtime.World.Civilizations[1].Gold);
        runtime.CancelCommand(command); runtime.AdvanceOne(); Throws<InvalidOperationException>(() => runtime.PrepareNewGame()); Equal(-5, runtime.World.Civilizations[1].Gold);
        var reply = runtime.TakeReplies().Single(); Equal(origin, reply.Correlation); Equal(new ConnectionId(9), reply.Destination); runtime.PrepareNewGame(); Equal(0, runtime.World.Civilizations[1].Gold); CheckRevoked(client, AccessFailure.SessionEnded);
    }
    private static void FreshGameRejectsPinsAndResetsAgain()
    {
        using var runtime = new SessionRuntime(new(), new(1)); var original = runtime.World;
        using (var pin = runtime.PinWorld()) { Throws<InvalidOperationException>(() => runtime.PrepareNewGame()); Equal(true, ReferenceEquals(original, runtime.World)); }
        runtime.PrepareNewGame(); var first = runtime.World; runtime.World.Civilizations[1].Gold = 11; runtime.Process.Rules.Leaders[0].CityNameIndex = 9;
        runtime.PrepareNewGame(); Equal(false, ReferenceEquals(first, runtime.World)); Equal(0, runtime.World.Civilizations[1].Gold); Equal((byte)0, runtime.Process.Rules.Leaders[0].CityNameIndex);
        runtime.BeginClientSession(new()); var client = SessionClient(runtime); using (var activity = runtime.BeginActivity()) { Throws<InvalidOperationException>(() => runtime.PrepareNewGame()); Equal(new CivilizationSlot(1), client.Identity.Observer); }
        runtime.PrepareNewGame(); CheckRevoked(client, AccessFailure.SessionEnded);
    }
    private static void RuleStateContentTransfer()
    {
        var a = new RuleState(); var b = new RuleState(); a.Text.Initialize(); a.Text.Store("a"u8); a.Leaders[0].CityNameIndex = 9; a.DoubleMovementApplied = true;
        b.Text.Initialize(); b.Leaders[0].CityNameIndex = 3; var original = a.Text;
        a.SwapContents(b); Equal(1, a.Text.Count); Equal(2, b.Text.Count); Equal(true, ReferenceEquals(original, b.Text)); Equal((byte)3, a.Leaders[0].CityNameIndex); Equal((byte)9, b.Leaders[0].CityNameIndex); Equal(false, a.DoubleMovementApplied); Equal(true, b.DoubleMovementApplied);
        a.SwapContents(a); Equal(1, a.Text.Count); Throws<ArgumentNullException>(() => a.SwapContents(null!)); Equal(1, a.Text.Count);
    }
}
