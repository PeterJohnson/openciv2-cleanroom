using System.Buffers.Binary;
using System.Text.Json.Nodes;
using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.State;
using OpenCiv2.Formats;
using OpenCiv2.Formats.Content;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static readonly string SessionCheckpointContent = ContentIdentity.Sha256("test immutable constructor-default content, version 1"u8);
    private static void CheckpointAdmit(bool value) { if (!value) throw new ArgumentException("Outside authored checkpoint profile"); }
    private static void CheckpointFresh(GameState game, ProcessContext process)
    {
        var coldProcess = new ProcessContext(); var cold = new GameState(coldProcess);
        CheckpointAdmit(game.Entities.Units.End == 0 && game.Entities.Cities.End == 0 && game.Entities.Units.Storage.SequenceEqual(cold.Entities.Units.Storage) && game.Entities.Cities.Storage.SequenceEqual(cold.Entities.Cities.Storage));
        var s = game.UnitStacks;
        CheckpointAdmit(s.Suppression == 0 && !s.AnimationActive && !s.DetachActive && !s.AttachActive && !s.RelocateActive && !s.BottomActive && s.DetachReply == 0 && s.AttachReply == 0 && s.RelocateReply == 0 && s.BottomReply == 0);
        CheckpointAdmit(!game.UnitDeletion.Active && game.UnitDeletion.Reply == 0 && game.UnitTotals.Equals(cold.UnitTotals) && !game.TurnLoopActive && game.MovementMode == MovementMode.View);
        CheckpointAdmit(game.Globals == cold.Globals && game.WorldSetup == cold.WorldSetup && game.Names.SequenceEqual(cold.Names) && game.PendingLeaderOverrides.SequenceEqual(cold.PendingLeaderOverrides));
        cold.Civilizations[1].Gold = game.Civilizations[1].Gold;
        CheckpointAdmit(game.Civilizations.SequenceEqual(cold.Civilizations) && game.Multiplayer == cold.Multiplayer && !game.Events.Loaded && game.Events.Collection.Pristine && game.PowerHistory.Equals(cold.PowerHistory) && game.Eliminations == cold.Eliminations && game.Scenario == cold.Scenario && game.Layout == cold.Layout && game.Views.SequenceEqual(cold.Views));
        CheckpointAdmit(game.Mood.City is null && game.Mood.Happy == 0 && game.Mood.Unhappy == 0 && !game.Map.Allocated && game.Map.PatternSeed == 0 && game.Map.StoredTopology == 0 && game.Map.Regions.SequenceEqual(cold.Map.Regions) && game.Map.RememberedBarbarian.Length == 0);
        CheckpointAdmit(process.Fallback.Value == coldProcess.Fallback.Value && process.TextParameters.Numbers.Equals(coldProcess.TextParameters.Numbers) && process.TextParameters.Strings.SequenceEqual(coldProcess.TextParameters.Strings) && process.TextParameters.Line.Length == 0);
        var rules = process.Rules;
        CheckpointAdmit(rules.Definitions.Snapshot() == coldProcess.Rules.Definitions.Snapshot() && rules.Leaders.Select(l => l.Snapshot()).SequenceEqual(coldProcess.Rules.Leaders.Select(l => l.Snapshot())) && !rules.DoubleMovementApplied && !rules.IsLoaded);
        CheckpointAdmit(rules.Labels.StatusLanguage == 0 && rules.Labels.DeclaredCount == 0 && rules.Labels.Entries.Count == 0 && rules.Labels.Popups.All(p => p is null));
    }
    private static CheckpointProfile SessionCheckpointProfile() => new("test.local-foundation/1", (world, process) =>
    {
        CheckpointFresh(world, process); var bytes = new byte[16 + 72 + 444]; "FND\x01"u8.CopyTo(bytes); BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), world.Civilizations[1].Gold);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), process.Tokens.NextUnit); BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), process.Tokens.NextCity);
        ClassicPreferences.EncodeOrdinary(process.OrdinaryPreferences).CopyTo(bytes, 16); ClassicPreferences.EncodeMultiplayer(process.MultiplayerPreferences).CopyTo(bytes, 88); return new(bytes);
    }, (bytes, world, process) =>
    {
        CheckpointAdmit(bytes.Length == 532 && bytes[..4].SequenceEqual("FND\x01"u8)); world.Civilizations[1].Gold = BinaryPrimitives.ReadInt32LittleEndian(bytes[4..]);
        process.Tokens.NextUnit = BinaryPrimitives.ReadUInt32LittleEndian(bytes[8..]); process.Tokens.NextCity = BinaryPrimitives.ReadUInt32LittleEndian(bytes[12..]);
        process.OrdinaryPreferences = StateConversion.ToLive(ClassicPreferences.DecodeOrdinary(bytes.Slice(16, 72))); process.MultiplayerPreferences = StateConversion.ToLive(ClassicPreferences.DecodeMultiplayer(bytes[88..]));
    });
    private static ChoiceKey SessionCheckpointPoint(string call) => new("test.checkpoint", call);
    private sealed class CheckpointDecision(int amount) : IContinuation
    {
        public int Amount = amount, Selected, Phase;
        public Action? Release;
        public OperationStep Resume(OperationContext context, OperationInput input)
        {
            ref int treasury = ref context.World.Civilizations[1].Gold;
            if (Phase == 0)
            {
                ++Phase; Selected = context.Gameplay.PrimaryModulo(SessionCheckpointPoint("prefix"), 7); context.Presentation.SecondaryUnit(SessionCheckpointPoint("presentation-prefix"));
                treasury = unchecked(treasury - Amount); context.Effect("test.debit", new([new("amount", new CommandValue.Integer(Amount)), new("selected", new CommandValue.Integer(Selected))]));
                return new(CommandStatus.AwaitingInteraction, Interaction: new("Authored checkpoint decision", null, new CommandValue.Text("owned target"), new([2, 4]), 50, 2));
            }
            if (input.Kind == OperationInputKind.Cancel) return new(CommandStatus.Cancelled, Code: "cancelled");
            CheckpointAdmit(input.Choice is not null && Phase == 1); int suffix = context.Gameplay.PrimaryModulo(SessionCheckpointPoint("suffix"), 5); context.Presentation.PrimaryCode(SessionCheckpointPoint("presentation-suffix"));
            treasury = unchecked(treasury + Selected + input.Choice!.Value + suffix); context.Effect("test.credit", new([new("treasury", new CommandValue.Integer(treasury))])); Phase = 2;
            return new(CommandStatus.Completed, new([new("treasury", new CommandValue.Integer(treasury))]));
        }
        public void Dispose() { var release = Release; Release = null; release?.Invoke(); }
    }
    private static CommandDefinition SessionCheckpointDefinition() => new(new(1, "test.checkpoint", "Authored checkpoint proof", AccessClass.Player, new([new("amount")]), new([new("treasury")])),
        (_, _, _) => new(), request => new CheckpointDecision(((CommandValue.Integer)CommandDescriptor.Value(request.Arguments, "amount")).Value), Checkpoint: new("test.decision", 1, continuation =>
        {
            var decision = (CheckpointDecision)continuation; CheckpointAdmit(decision.Phase == 1); var bytes = new byte[12]; BinaryPrimitives.WriteInt32LittleEndian(bytes, 1);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), decision.Amount); BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), decision.Selected); return new(bytes);
        }, (bytes, request, update) =>
        {
            CheckpointAdmit(bytes.Length == 12 && BinaryPrimitives.ReadUInt32LittleEndian(bytes) == 1 && BinaryPrimitives.ReadUInt32LittleEndian(bytes[8..]) < 7 && update.Committed.Count == 1);
            int amount = BinaryPrimitives.ReadInt32LittleEndian(bytes[4..]); CheckpointAdmit(amount == ((CommandValue.Integer)CommandDescriptor.Value(request.Arguments, "amount")).Value);
            return new CheckpointDecision(amount) { Phase = 1, Selected = BinaryPrimitives.ReadInt32LittleEndian(bytes[8..]) };
        }));
    private sealed class CheckpointFixture : IDisposable
    {
        public ProcessContext Process = new(); public SessionRuntime Runtime; public ParticipantId Participant;
        public CheckpointFixture(ulong identity = 1, bool start = true, CommandDefinition? definition = null)
        {
            Runtime = new(Process, new(identity)); Runtime.RegisterCommand(definition ?? SessionCheckpointDefinition()); if (start) { Runtime.BeginClientSession(new(11, 13)); Participant = Runtime.AddParticipant(new(1)); }
        }
        public IPlayerClient Client() => Runtime.Bind(Participant, ControllerKind.Scripted);
        public byte[] Save() => Runtime.Checkpoint(SessionCheckpointProfile(), SessionCheckpointContent);
        public void Restore(byte[] bytes) { Runtime.RestoreCheckpoint(bytes, SessionCheckpointProfile(), SessionCheckpointContent); Participant = LocalCheckpointCodec.Decode(bytes).Participant; }
        public void Controlled()
        {
            Runtime.GameplayRandom.Restore(new GameplayRandom(new ControlledChoices(new([Choice.Create(SessionCheckpointPoint("prefix"), new SamplingLaw.PrimaryModulo(7), ChoiceCode.Primary(9)), Choice.Create(SessionCheckpointPoint("suffix"), new SamplingLaw.PrimaryModulo(5), ChoiceCode.Primary(8))])), "session").Capture(), 0);
            Runtime.PresentationRandom.Restore(new PresentationRandom(new ControlledChoices(new([Choice.Create(SessionCheckpointPoint("presentation-prefix"), new SamplingLaw.SecondaryUnit(), ChoiceCode.Secondary(uint.MaxValue)), Choice.Create(SessionCheckpointPoint("presentation-suffix"), new SamplingLaw.PrimaryCode(), ChoiceCode.Primary(7))])), "session").Capture(), 0);
        }
        public void Dispose() => Runtime.Dispose();
    }
    private static CommandRequest SessionCheckpointRequest(int amount = 5) => new("test.checkpoint", new([new("amount", new CommandValue.Integer(amount))]));
    private static CommandUpdate SuspendCheckpoint(CheckpointFixture fixture, IPlayerClient client)
    {
        fixture.Runtime.World.Civilizations[1].Gold = 100; var submitted = client.Submit(SessionCheckpointRequest()); Equal(true, fixture.Runtime.AdvanceOne()); return RequiredResult(client, submitted.Correlation.Command);
    }
    private static void SessionCheckpointIdle()
    {
        using var source = new CheckpointFixture(start: false); source.Runtime.BeginClientSession(new GameplayRandom(11, "test/custom-gameplay"), new PresentationRandom(13, "test/custom-presentation")); source.Participant = source.Runtime.AddParticipant(new(1));
        using var destination = new CheckpointFixture(2, false); source.Process.Tokens.NextUnit = uint.MaxValue; source.Process.Tokens.NextCity = 0;
        source.Process.OrdinaryPreferences = StateConversion.ToLive(ClassicPreferences.DecodeOrdinary(Enumerable.Repeat((byte)0xa5, 72).ToArray())); source.Process.MultiplayerPreferences = StateConversion.ToLive(ClassicPreferences.DecodeMultiplayer(Enumerable.Repeat((byte)0x5a, 444).ToArray()));
        source.Runtime.World.Civilizations[1].Gold = int.MinValue; source.Runtime.GameplayRandom.PrimaryCode("test.advance"); byte[] bytes = source.Save();
        var rules = destination.Process.Rules; var tokens = destination.Process.Tokens; var world = destination.Runtime.World; destination.Restore(bytes);
        Equal(true, ReferenceEquals(destination.Process, destination.Runtime.Process)); Equal(true, ReferenceEquals(rules, destination.Process.Rules)); Equal(true, ReferenceEquals(tokens, destination.Process.Tokens)); Equal(false, ReferenceEquals(world, destination.Runtime.World));
        Equal(uint.MaxValue, tokens.NextUnit); Equal(0U, tokens.NextCity); Equal(source.Process.OrdinaryPreferences, destination.Process.OrdinaryPreferences); Equal(source.Process.MultiplayerPreferences, destination.Process.MultiplayerPreferences);
        Equal(int.MinValue, destination.Runtime.World.Civilizations[1].Gold); Equal(source.Runtime.GameplayRandom.Capture(), destination.Runtime.GameplayRandom.Capture()); Equal(source.Runtime.PresentationRandom.Capture(), destination.Runtime.PresentationRandom.Capture());
        var client = destination.Client(); Equal(0, client.OwnUnits().Records.Count); Equal(false, destination.Runtime.HasPendingCommands); Equal(false, destination.Runtime.AdvanceOne());
        foreach (bool activity in new[] { false, true })
        {
            using var pin = activity ? destination.Runtime.BeginActivity() : destination.Runtime.PinWorld(); Throws<ArgumentException>(() => destination.Save()); Throws<ArgumentException>(() => destination.Restore(bytes)); Equal(int.MinValue, destination.Runtime.World.Civilizations[1].Gold); Equal(0, client.OwnCities().Records.Count);
        }
        destination.Save(); destination.Runtime.World.Map.Allocate(new(8, 6)); destination.Runtime.World.Map.TileAt(new(-1, -1)).Region = 33; Equal((byte)33, destination.Process.Fallback.Value.Region);
    }
    private static void SessionCheckpointResumesOnce()
    {
        foreach (bool controlled in new[] { false, true })
        {
            using var source = new CheckpointFixture(); using var destination = new CheckpointFixture(2);
            if (controlled) source.Controlled(); else { source.Runtime.GameplayRandom.RecordChoices(2); source.Runtime.PresentationRandom.RecordChoices(2); }
            var client = source.Client(); var stale = destination.Client(); var pending = SuspendCheckpoint(source, client); destination.Restore(source.Save()); Throws<AccessException>(() => stale.OwnUnits());
            var restored = destination.Client(); Equal(pending, RequiredResult(restored, pending.Correlation.Command)); Equal(pending, restored.Updates().Single()); Equal(95, destination.Runtime.World.Civilizations[1].Gold);
            ChoiceFails(ChoiceFailure.AlreadyConsumed, () => destination.Runtime.GameplayRandom.PrimaryModulo(SessionCheckpointPoint("prefix"), 7), SessionCheckpointPoint("prefix"));
            Equal(true, client.Reply(pending.Interaction!.Id, 4).Accepted); Equal(true, restored.Reply(pending.Interaction.Id, 4).Accepted); Equal(false, restored.Reply(pending.Interaction.Id, 4).Accepted);
            Equal(true, source.Runtime.AdvanceOne()); Equal(true, destination.Runtime.AdvanceOne()); var result = RequiredResult(restored, pending.Correlation.Command); Equal(RequiredResult(client, pending.Correlation.Command), result); Equal(2, result.Committed.Count); Equal(2UL, result.Committed[1].Index);
            Equal(source.Runtime.World.Civilizations[1].Gold, destination.Runtime.World.Civilizations[1].Gold); Equal(source.Runtime.GameplayRandom.Capture(), destination.Runtime.GameplayRandom.Capture()); Equal(source.Runtime.PresentationRandom.Capture(), destination.Runtime.PresentationRandom.Capture());
            destination.Runtime.GameplayRandom.RequireChoicesConsumed(); destination.Runtime.PresentationRandom.RequireChoicesConsumed(); Equal(false, restored.Reply(pending.Interaction.Id, 4).Accepted);
            Equal(1, restored.Updates().Sum(u => u.Committed.Count)); // Rebound clients receive the prefix exactly once, then only the new suffix.
        }
    }
    private static void SessionCheckpointCancelTimeout()
    {
        foreach (bool cancel in new[] { false, true })
        {
            using var source = new CheckpointFixture(); using var destination = new CheckpointFixture(2, false); source.Controlled(); var client = source.Client(); var pending = SuspendCheckpoint(source, client); destination.Restore(source.Save()); var restored = destination.Client();
            if (cancel) { Equal(true, client.Cancel(pending.Correlation.Command).Accepted); Equal(true, restored.Cancel(pending.Correlation.Command).Accepted); }
            else { Equal(0, destination.Runtime.ExpireInteractions(49)); Equal(1, source.Runtime.ExpireInteractions(50)); Equal(1, destination.Runtime.ExpireInteractions(50)); }
            Equal(true, source.Runtime.AdvanceOne()); Equal(true, destination.Runtime.AdvanceOne()); Equal(RequiredResult(client, pending.Correlation.Command), RequiredResult(restored, pending.Correlation.Command));
            Equal(source.Runtime.World.Civilizations[1].Gold, destination.Runtime.World.Civilizations[1].Gold); Equal(source.Runtime.GameplayRandom.Capture(), destination.Runtime.GameplayRandom.Capture()); Equal(source.Runtime.PresentationRandom.Capture(), destination.Runtime.PresentationRandom.Capture());
            if (cancel) { Equal(95, destination.Runtime.World.Civilizations[1].Gold); Equal(1, RequiredResult(restored, pending.Correlation.Command).Committed.Count); }
        }
    }
    private static void SessionCheckpointBadAdmission()
    {
        using var source = new CheckpointFixture(); var client = source.Client(); SuspendCheckpoint(source, client); byte[] bytes = source.Save();
        using var destination = new CheckpointFixture(2); var existing = destination.Client(); destination.Runtime.World.Civilizations[1].Gold = 271; destination.Process.Tokens.NextUnit = 77; destination.Process.Tokens.NextCity = 88; destination.Process.OrdinaryPreferences.GameOptions = StateConversion.DecodeGameOptions(0x12345678);
        byte[] before = destination.Save(); var world = destination.Runtime.World; var fallback = destination.Process.Fallback;
        Action<JsonNode>[] changes = [n => n["content"] = new string('b', 64), n => n["profile"] = "0000", n => n["command"]!["codecVersion"] = "999", n => n["command"]!["descriptorVersion"] = "999", n => n["command"]!["codec"] = "0000", n => n["command"]!["name"] = "0000", n => n["command"]!["locals"] = "00", n => n["world"] = "00", n => n["command"]!["arguments"] = new JsonArray(), n => n["counters"]!["command"] = "0", n => n["command"]!["interaction"]!["recipient"] = "2", n => n["gameplay"] = n["gameplay"]!.GetValue<string>().Replace("oc2-pcg32", "old-policy", StringComparison.Ordinal)];
        foreach (var change in changes)
        {
            var payload = LocalCodecPayload(bytes); change(payload); bool rejected = false; try { destination.Restore(LocalCodecEnvelope(payload)); } catch (Exception error) when (error is ArgumentException or ContentException or ChoiceException) { rejected = true; }
            Equal(true, rejected); BytesEqual(before, destination.Save()); Equal(true, ReferenceEquals(world, destination.Runtime.World)); Equal(0, existing.OwnUnits().Records.Count);
        }
        var badProfile = SessionCheckpointProfile() with { Decode = (_, stagedWorld, stagedProcess) => { stagedWorld.Civilizations[1].Gold = -99; stagedProcess.Tokens.NextUnit = 2; stagedProcess.Fallback.Value.Region = 63; stagedProcess.Rules.IsLoaded = true; throw new InvalidOperationException("owner decode failure"); } };
        Throws<InvalidOperationException>(() => destination.Runtime.RestoreCheckpoint(bytes, badProfile, SessionCheckpointContent)); BytesEqual(before, destination.Save()); Equal(true, ReferenceEquals(fallback, destination.Process.Fallback)); Equal((byte)0, fallback.Value.Region);
        var corrupt = bytes.ToArray(); corrupt[corrupt.Length / 2] ^= 1; bool failed = false; try { destination.Restore(corrupt); } catch (Exception error) when (error is ArgumentException or ContentException) { failed = true; }
        Equal(true, failed); BytesEqual(before, destination.Save());
        Throws<ContentException>(() => destination.Restore(bytes[..12])); Equal(0, existing.OwnCities().Records.Count);
    }
    private static void SessionCheckpointUnsupportedState()
    {
        Action<CheckpointFixture>[] changes =
        [
            f => f.Runtime.World.Civilizations[2].Gold = 1, f => f.Runtime.World.Entities.Units.Storage[2047].Damage = 3, f => f.Runtime.World.Entities.Cities.SetEnd(1), f => f.Runtime.World.Globals.Turn = 1,
            f => f.Runtime.World.Map.Allocate(new(8, 8)), f => f.Runtime.World.Events.Loaded = true, f => { f.Runtime.World.Events.Collection.Append(); f.Runtime.World.Events.Collection.Reset(); },
            f => f.Runtime.World.UnitStacks.AttachActive = true, f => f.Runtime.World.UnitDeletion.Reply = -2, f => f.Runtime.World.UnitTotals[2] = 1, f => f.Runtime.World.TurnLoopActive = true, f => f.Runtime.World.MovementMode = MovementMode.MoveUnits,
            f => f.Runtime.World.PendingLeaderOverrides[2] = 0, f => f.Runtime.World.Mood.Happy = 1, f => f.Runtime.World.Map.PatternSeed = 1,
            f => f.Runtime.World.Map.Regions[2] = new(1, 0, 0, default), f => f.Runtime.World.Map.RememberedBarbarian = [MapFeatures.Unit], f => f.Process.Rules.DoubleMovementApplied = true,
            f => f.Process.Rules.Labels.Entries.Add(NameText("unexpected")), f => f.Process.Rules.IsLoaded = true, f => f.Process.Fallback.Value.Region = 1, f => f.Process.TextParameters.Line = "\u0001",
            f => f.Process.Rules.Definitions.Advances[1] = new() { Identity = new("advance.1", NameText("unexpected")) }, f => f.Process.Rules.Leaders[0].CityNameIndex = 1, f => f.Runtime.World.Views[0] = new(true, false, new(0, 0), 0), f => f.Runtime.World.Scenario.Flags = new() { RevolutionRestriction = true },
            f => f.Runtime.World.Names[0].Leader = "\u0003", f => f.Runtime.World.WorldSetup.Width = 1, f => f.Runtime.World.Eliminations.Count = 1, f => f.Runtime.World.PowerHistory[0][0] = 1,
            f => f.Runtime.AddParticipant(new(1)), f => f.Runtime.ReassignParticipant(f.Participant, new(2)), f => f.Runtime.ReassignParticipant(f.Participant, new(1), new(9)),
            f => f.Client().Submit(SessionCheckpointRequest(1)), f => { var c = f.Client(); var p = SuspendCheckpoint(f, c); c.Reply(p.Interaction!.Id, 2); },
            f => { var c = f.Client(); SuspendCheckpoint(f, c); c.Submit(SessionCheckpointRequest(1)); }, f => { var c = f.Client(); var p = SuspendCheckpoint(f, c); c.Cancel(p.Correlation.Command); f.Runtime.AdvanceOne(); }
        ];
        foreach (var change in changes)
        {
            using var fixture = new CheckpointFixture(); change(fixture); var game = fixture.Runtime.GameplayRandom.Capture(); var presentation = fixture.Runtime.PresentationRandom.Capture(); int gold = fixture.Runtime.World.Civilizations[1].Gold;
            Throws<ArgumentException>(() => fixture.Save()); Equal(game, fixture.Runtime.GameplayRandom.Capture()); Equal(presentation, fixture.Runtime.PresentationRandom.Capture()); Equal(gold, fixture.Runtime.World.Civilizations[1].Gold);
        }
    }
    private static void SessionCheckpointBindings()
    {
        using var source = new CheckpointFixture(); var original = source.Client(); var pending = SuspendCheckpoint(source, original); byte[] snapshot = source.Save();
        using var destination = new CheckpointFixture(2); var old = destination.Client();
        for (int i = 0; i < 3; ++i) { destination.Runtime.BeginClientSession(new()); destination.Participant = destination.Runtime.AddParticipant(new(1)); old = destination.Client(); }
        var highWater = LocalCheckpointCodec.Decode(destination.Save()).Counters;
        for (int i = 0; i < 5; ++i) old = destination.Client(); var oldIdentity = old.Identity; destination.Restore(snapshot); var first = destination.Client();
        Equal(true, first.Identity.Controller.Value > oldIdentity.Controller.Value); Equal(new RuntimeId(2), first.Identity.Runtime); Equal(pending.Correlation.Runtime, RequiredResult(first, pending.Correlation.Command).Correlation.Runtime); Throws<AccessException>(() => old.Reply(pending.Interaction!.Id, 2));
        byte[] repeated = destination.Save(); var retainedCounters = LocalCheckpointCodec.Decode(repeated).Counters; Equal(highWater.Session, retainedCounters.Session); Equal(highWater.Participant, retainedCounters.Participant); Equal(LocalCheckpointCodec.Decode(snapshot).Counters.Revision, retainedCounters.Revision);
        var firstIdentity = first.Identity; destination.Restore(repeated); Throws<AccessException>(() => first.Cancel(pending.Correlation.Command)); var final = destination.Client(); Equal(true, final.Identity.Controller.Value > firstIdentity.Controller.Value); Equal(pending, RequiredResult(final, pending.Correlation.Command));
        Equal(true, final.Reply(pending.Interaction!.Id, 2).Accepted); destination.Runtime.AdvanceOne(); var next = final.Submit(SessionCheckpointRequest(1)); Equal(true, next.Correlation.Command.Value > pending.Correlation.Command.Value); destination.Runtime.AdvanceOne(); Equal(true, RequiredResult(final, next.Correlation.Command).Interaction!.Id.Value > pending.Interaction.Id.Value);
        // Destination command/interaction high-water marks survive replacement by the older snapshot too.
        destination.Restore(snapshot); var last = destination.Client(); last.Reply(pending.Interaction.Id, 2); destination.Runtime.AdvanceOne(); var newest = last.Submit(SessionCheckpointRequest(1)); destination.Runtime.AdvanceOne(); Equal(true, newest.Correlation.Command.Value > next.Correlation.Command.Value); Equal(true, RequiredResult(last, newest.Correlation.Command).Interaction!.Id.Value > pending.Interaction.Id.Value + 1);
    }
    private static void SessionCheckpointPeerWork()
    {
        using var idle = new CheckpointFixture(); byte[] bytes = idle.Save(); var definition = SessionCheckpointDefinition() with { Admit = (_, _, _) => new(Route: CommandRoute.AwaitPeer, Executor: new(7)), ApplyResult = (_, _) => throw new InvalidOperationException("must not apply") };
        using var remote = new CheckpointFixture(9, definition: definition); var client = remote.Client(); var pending = client.Submit(SessionCheckpointRequest(1)); var random = remote.Runtime.GameplayRandom.Capture();
        Throws<ArgumentException>(() => remote.Save()); Throws<ArgumentException>(() => remote.Restore(bytes)); Equal(pending, RequiredResult(client, pending.Correlation.Command)); Equal(random, remote.Runtime.GameplayRandom.Capture());
        var request = remote.Runtime.TakeRequests().Single(); Equal(new ConnectionId(7), request.Destination); Equal(pending.Correlation, request.Correlation); Throws<ArgumentException>(() => remote.Save()); Throws<ArgumentException>(() => remote.Restore(bytes));
        using var local = new CheckpointFixture(10, definition: SessionCheckpointDefinition() with { Checkpoint = null }); var actor = local.Client(); SuspendCheckpoint(local, actor); Throws<ArgumentException>(() => local.Save());
        using var peer = new CheckpointFixture(11); peer.Runtime.ReassignParticipant(peer.Participant, new(1), new(8)); var origin = new CommandCorrelation(new(90), new(91), new(92));
        var accepted = peer.Runtime.ReceiveRequest(new(8), peer.Participant, origin, SessionCheckpointRequest()); peer.Runtime.AdvanceOne(); peer.Runtime.CancelCommand(accepted.Correlation.Command); peer.Runtime.AdvanceOne();
        Throws<ArgumentException>(() => peer.Restore(bytes)); var reply = peer.Runtime.TakeReplies().Single(); Equal(origin, reply.Correlation); peer.Runtime.Disconnect(new(8)); Throws<ArgumentException>(() => peer.Restore(bytes));
        using var queued = new CheckpointFixture(12); var queuedClient = queued.Client(); var decision = SuspendCheckpoint(queued, queuedClient); queuedClient.Reply(decision.Interaction!.Id, 2); Throws<ArgumentException>(() => queued.Restore(bytes)); Equal(true, queued.Runtime.AdvanceOne()); Equal((int)CommandStatus.Completed, (int)RequiredResult(queuedClient, decision.Correlation.Command).Status);
    }
    private static void SessionCheckpointOwnerReentryAndCleanup()
    {
        using var source = new CheckpointFixture(); SuspendCheckpoint(source, source.Client()); byte[] bytes = source.Save(); var baseProfile = SessionCheckpointProfile();
        using var destination = new CheckpointFixture(2); var client = destination.Client(); byte[] before = destination.Save();
        Action[] reentries = [() => destination.Runtime.Dispose(), () => destination.Runtime.BeginClientSession(new()), () => destination.Runtime.Publish(), () => client.Submit(SessionCheckpointRequest()), () => client.Updates(), () => destination.Runtime.TakeReplies(), () => destination.Runtime.RestoreCheckpoint(bytes, baseProfile, SessionCheckpointContent), () => destination.Runtime.PinWorld(), () => destination.Runtime.World.Civilizations[1].Gold = 999];
        foreach (var reentry in reentries)
        {
            var encode = baseProfile with { Encode = (world, process) => { reentry(); return baseProfile.Encode(world, process); } };
            Throws<InvalidOperationException>(() => destination.Runtime.Checkpoint(encode, SessionCheckpointContent)); BytesEqual(before, destination.Save());
            var decode = baseProfile with { Decode = (data, world, process) => { baseProfile.Decode(data, world, process); reentry(); } };
            Throws<InvalidOperationException>(() => destination.Runtime.RestoreCheckpoint(bytes, decode, SessionCheckpointContent)); BytesEqual(before, destination.Save()); Equal(0, client.OwnUnits().Records.Count);
        }
        int factory = 0, released = 0; var definition = SessionCheckpointDefinition();
        using var replacement = new CheckpointFixture(3, definition: definition with { Execute = request => { ++factory; return new CheckpointDecision(5) { Release = () => { ++released; throw new InvalidOperationException("cleanup"); } }; } });
        var stale = replacement.Client(); var pending = SuspendCheckpoint(replacement, stale); replacement.Restore(bytes); Equal(1, factory); Equal(1, released); Throws<AccessException>(() => stale.Result(pending.Correlation.Command));
        var restored = replacement.Client(); Equal(95, replacement.Runtime.World.Civilizations[1].Gold); restored.Reply(pending.Interaction!.Id, 2); replacement.Runtime.AdvanceOne(); Equal(1, factory); Equal(1, released); Equal(2, RequiredResult(restored, pending.Correlation.Command).Committed.Count);
        var failing = definition with { Checkpoint = definition.Checkpoint! with { Decode = (_, _, _) => throw new InvalidOperationException("codec decode") } };
        using var failed = new CheckpointFixture(4, definition: failing); var retained = failed.Client(); byte[] saved = failed.Save(); Throws<InvalidOperationException>(() => failed.Restore(bytes)); BytesEqual(saved, failed.Save()); Equal(0, retained.OwnCities().Records.Count);
        using var missing = new CheckpointFixture(5, definition: definition with { Checkpoint = definition.Checkpoint! with { Decode = (_, _, _) => null! } }); saved = missing.Save(); Throws<ArgumentException>(() => missing.Restore(bytes)); BytesEqual(saved, missing.Save());
        using var restricted = new CheckpointFixture(6, definition: definition with { Descriptor = definition.Descriptor with { Access = AccessClass.Developer } }); saved = restricted.Save(); Throws<ArgumentException>(() => restricted.Restore(bytes)); BytesEqual(saved, restricted.Save());
        using var exhausted = new CheckpointFixture(7); var checkpoint = LocalCheckpointCodec.Decode(bytes); checkpoint = checkpoint with { Counters = checkpoint.Counters with { Controller = ulong.MaxValue, Command = ulong.MaxValue, Interaction = ulong.MaxValue } };
        exhausted.Restore(LocalCheckpointCodec.Encode(checkpoint)); Throws<OverflowException>(() => exhausted.Client()); Equal(95, exhausted.Runtime.World.Civilizations[1].Gold);
        // Atomic install also replaces arbitrary staged process/fallback contents. Failure above never touched them.
        using var transfer = new CheckpointFixture(8); var oldFallback = transfer.Process.Fallback; var oldText = transfer.Process.TextParameters;
        var staged = baseProfile with { Decode = (data, world, process) => { baseProfile.Decode(data, world, process); process.Fallback.Value.Region = 19; process.TextParameters.Numbers[0] = 23; process.Rules.IsLoaded = true; } };
        transfer.Runtime.RestoreCheckpoint(bytes, staged, SessionCheckpointContent); Equal(false, ReferenceEquals(oldFallback, transfer.Process.Fallback)); Equal(false, ReferenceEquals(oldText, transfer.Process.TextParameters)); Equal((byte)0, oldFallback.Value.Region); Equal(23, transfer.Process.TextParameters.Numbers[0]); Equal(true, transfer.Process.Rules.IsLoaded);
        transfer.Runtime.World.Map.Allocate(new(8, 8)); Equal((byte)19, transfer.Runtime.World.Map.TileAt(new(-1, -1)).Region); transfer.Runtime.World.Map.TileAt(new(-1, -1)).Region = 20; Equal((byte)20, transfer.Process.Fallback.Value.Region);
        using var advancing = new SessionRuntime(new(), new(9)); int checkedBoundary = 0;
        advancing.RegisterCommand(CommandDefinitionFor(_ => new TestContinuation((_, _) =>
        {
            Throws<ArgumentException>(() => advancing.Checkpoint(baseProfile, SessionCheckpointContent));
            Throws<ArgumentException>(() => advancing.RestoreCheckpoint(bytes, baseProfile, SessionCheckpointContent)); ++checkedBoundary; return CommandDone(0);
        })));
        advancing.BeginClientSession(new()); var active = advancing.Bind(advancing.AddParticipant(new(1)), ControllerKind.Scripted); var started = active.Submit(CommandRequestFor()); advancing.AdvanceOne();
        Equal(1, checkedBoundary); Equal((int)CommandStatus.Completed, (int)RequiredResult(active, started.Correlation.Command).Status);
    }
}
