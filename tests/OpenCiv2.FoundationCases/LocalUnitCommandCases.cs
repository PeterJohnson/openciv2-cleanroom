using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.State;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private sealed class LocalUnitCommandFixture : IDisposable
    {
        public readonly SessionRuntime Runtime = new(new(), new(503));
        public readonly IPlayerClient Client, Other;
        public LocalUnitCommandFixture(int count = 3)
        {
            Runtime.RegisterCommand(LocalUnitCommands.StackBottom()); Runtime.BeginClientSession(new(17, 23));
            Runtime.World.Map.Allocate(new(8, 6)); Runtime.World.Entities.Units.SetEnd(count + 1); Runtime.World.Globals.UnitEnd = (short)(count + 1);
            for (int i = 0; i <= count; ++i) Unit(i) = new()
            {
                Token = new((uint)i + 100),
                Owner = new((byte)(i == count ? 2 : 1)),
                X = 2,
                Y = 2,
                Previous = new((short)(i == count ? -1 : i - 1)),
                Next = new((short)(i >= count - 1 ? -1 : i + 1))
            };
            Client = SessionClient(Runtime); Other = SessionClient(Runtime, 2);
        }
        public ref Unit Unit(int slot) => ref Runtime.World.Entities.Units.Storage[slot];
        public CommandRequest Request(int slot = 0, uint token = 100) => new(LocalUnitCommands.StackBottomName, new([new("unit", new CommandValue.Unit(new(new((ushort)slot), new(token))))]));
        public CommandId Start()
        {
            var update = Client.Submit(Request()); Equal((int)CommandStatus.Pending, (int)update.Status); Equal(true, Runtime.AdvanceOne());
            Equal((int)CommandStatus.AwaitingInteraction, (int)Client.Result(update.Correlation.Command)!.Status); return update.Correlation.Command;
        }
        public void Dispose() => Runtime.Dispose();
    }
    private static void LocalUnitCommandConfirmation()
    {
        using var f = new LocalUnitCommandFixture(); var game = f.Runtime.GameplayRandom.Capture(); var presentation = f.Runtime.PresentationRandom.Capture();
        var observed = f.Client.OwnUnits(); var foreign = f.Unit(3); var id = f.Start(); var interaction = f.Client.Result(id)!.Interaction!;
        Equal(new OwnedList<int>([0, 1]), interaction.Spec.Choices); Equal((short)1, f.Unit(0).Next.Value);
        Equal(false, f.Other.Reply(interaction.Id, 1).Accepted); Equal(true, f.Other.Result(id) is null); Equal(0, f.Other.Updates().Count);
        Equal(true, f.Client.Reply(interaction.Id, 1).Accepted); Equal(false, f.Client.Reply(interaction.Id, 1).Accepted);
        Equal((short)1, f.Unit(0).Next.Value); Equal(true, f.Runtime.AdvanceOne()); Equal(false, f.Runtime.AdvanceOne());
        var result = f.Client.Result(id)!; Equal((int)CommandStatus.Completed, (int)result.Status); Equal(new CommandValue.Boolean(true), CommandDescriptor.Value(result.Result, "changed"));
        Equal(1, result.Committed.Count); Equal("unit.stack-reordered", result.Committed[0].Name);
        Equal((short)-1, f.Unit(1).Previous.Value); Equal((short)2, f.Unit(1).Next.Value); Equal((short)0, f.Unit(2).Next.Value); Equal((short)2, f.Unit(0).Previous.Value); Equal((short)-1, f.Unit(0).Next.Value);
        Equal(observed.Records, f.Client.OwnUnits().Records); Equal(foreign, f.Unit(3)); Equal(game, f.Runtime.GameplayRandom.Capture()); Equal(presentation, f.Runtime.PresentationRandom.Capture());
        // Repeating the command on the now-last member is an explicit no-op, without another effect.
        id = f.Start(); f.Client.Reply(f.Client.Result(id)!.Interaction!.Id, 1); f.Runtime.AdvanceOne();
        Equal(new CommandValue.Boolean(false), CommandDescriptor.Value(f.Client.Result(id)!.Result, "changed")); Equal(0, f.Client.Result(id)!.Committed.Count);
    }
    private static void LocalUnitCommandCancellation()
    {
        for (int stage = 0; stage < 3; ++stage)
        {
            using var f = new LocalUnitCommandFixture(); var before = f.Runtime.World.Entities.Units.Storage.ToArray();
            var id = stage == 0 ? f.Client.Submit(f.Request()).Correlation.Command : f.Start();
            if (stage == 2) f.Client.Reply(f.Client.Result(id)!.Interaction!.Id, 0); else Equal(true, f.Client.Cancel(id).Accepted);
            while (f.Runtime.AdvanceOne()) { }
            Equal((int)CommandStatus.Cancelled, (int)f.Client.Result(id)!.Status); Equal(0, f.Client.Result(id)!.Committed.Count);
            Equal(true, before.SequenceEqual(f.Runtime.World.Entities.Units.Storage.ToArray())); Equal(false, f.Runtime.HasPendingCommands);
        }
    }
    private static void LocalUnitCommandStaleTarget()
    {
        for (int timing = 0; timing < 3; ++timing)
            for (int change = 0; change < 4; ++change)
            {
                using var f = new LocalUnitCommandFixture(); var id = timing == 0 ? f.Client.Submit(f.Request()).Correlation.Command : f.Start();
                if (timing == 2) f.Client.Reply(f.Client.Result(id)!.Interaction!.Id, 1);
                if (change == 0) f.Unit(0).Token = new(900);
                else if (change == 1) f.Unit(0).Owner = new(2);
                else if (change == 2) f.Unit(1).Owner = new(2); else f.Runtime.World.Globals.Multiplayer = GameMode.Tcp;
                var before = f.Runtime.World.Entities.Units.Storage.ToArray();
                if (timing == 1) f.Client.Reply(f.Client.Result(id)!.Interaction!.Id, 1);
                Equal(true, f.Runtime.AdvanceOne()); Equal((int)CommandStatus.Failed, (int)f.Client.Result(id)!.Status);
                Equal(0, f.Client.Result(id)!.Committed.Count); Equal(true, before.SequenceEqual(f.Runtime.World.Entities.Units.Storage.ToArray()));
            }
    }
    private static void LocalUnitCommandAdmission()
    {
        for (int invalid = 0; invalid < 11; ++invalid)
        {
            using var f = new LocalUnitCommandFixture(); var request = f.Request();
            switch (invalid)
            {
                case 0: request = f.Request(3, 103); break;
                case 1: request = f.Request(0, 0); break;
                case 2: request = f.Request(0, 999); break;
                case 3: f.Unit(2).Next = new(0); f.Unit(0).Previous = new(2); break;
                case 4: f.Unit(1).Previous = new(-1); break;
                case 5: f.Unit(1).Token = new(0); break;
                case 6: f.Unit(1).Owner = new(2); break;
                case 7: f.Unit(1).Y = 4; break;
                case 8: f.Runtime.World.Map.Release(); break;
                case 9: f.Runtime.World.Globals.Multiplayer = GameMode.Hotseat; break;
                case 10: f.Unit(2).Next = new(2047); break;
            }
            var before = f.Runtime.World.Entities.Units.Storage.ToArray(); var result = f.Client.Submit(request);
            Equal((int)CommandStatus.Rejected, (int)result.Status); Equal(false, f.Runtime.AdvanceOne()); Equal(0, result.Committed.Count);
            Equal(true, before.SequenceEqual(f.Runtime.World.Entities.Units.Storage.ToArray()));
        }
    }
    private static void LocalUnitCommandFiniteBounds()
    {
        using var f = new LocalUnitCommandFixture(2047); var id = f.Start(); f.Client.Reply(f.Client.Result(id)!.Interaction!.Id, 1);
        Equal(true, f.Runtime.AdvanceOne()); Equal((int)CommandStatus.Completed, (int)f.Client.Result(id)!.Status); Equal((short)2046, f.Unit(0).Previous.Value);
        using var full = new LocalUnitCommandFixture(2047); full.Unit(2047).Owner = new(1); full.Unit(2047).Previous = new(2046); full.Unit(2046).Next = new(2047);
        Equal((int)CommandStatus.Rejected, (int)full.Client.Submit(full.Request()).Status); Equal((short)1, full.Unit(0).Next.Value);
    }
}
