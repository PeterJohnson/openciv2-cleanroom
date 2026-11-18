using OpenCiv2.Base;
using OpenCiv2.Console;
using OpenCiv2.Contracts;
using OpenCiv2.ViewModels;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static void ClientModelCommandParity()
    {
        foreach (bool text in new[] { false, true })
        {
            using var f = new LocalUnitCommandFixture(); var model = new SessionPanel(); model.Bind(f.Client);
            var request = text ? CommandText.Parse("unit.stack-bottom unit=0:100", model.View.Commands) : f.Request();
            Equal(true, model.Submit(request)); Equal(false, model.Submit(request)); Equal((short)1, f.Unit(0).Next.Value);
            f.Runtime.AdvanceOne(); model.Refresh(); Equal((int)CommandStatus.AwaitingInteraction, (int)model.View.Command!.Status);
            Equal(false, model.Reply(7)); Equal(true, model.Reply(1)); Equal(false, model.Reply(1));
            f.Runtime.AdvanceOne(); model.Refresh(); Equal((int)CommandStatus.Completed, (int)model.View.Command!.Status); Equal((short)2, f.Unit(0).Previous.Value);
            Equal(1, model.View.Command.Committed.Count); var final = model.View.Command;
            Equal(true, f.Runtime.RetireCommand(final.Correlation.Command)); model.Refresh(); Equal(final, model.View.Command!);
        }
    }
    private static void ClientModelPrivacyAndRebind()
    {
        using var f = new LocalUnitCommandFixture(); var model = new SessionPanel(); model.Bind(f.Client); Equal(3, model.View.Units!.Records.Count);
        model.Submit(f.Request()); f.Runtime.AdvanceOne(); model.Refresh(); var old = model.View;
        model.Bind(f.Other); Equal(1, model.View.Units!.Records.Count); Equal((ushort)3, model.View.Units.Records[0].Slot.Value); Equal(true, model.View.Command is null); Equal(false, model.Reply(1));
        f.Runtime.Revoke(f.Other.Identity.Controller); model.Refresh(); Equal(true, model.View.Units is null); Equal(true, model.View.Cities is null); Equal(0, model.View.Commands.Count); Equal(true, model.View.Identity is null);
        Equal(false, model.Submit(f.Request())); Equal(false, model.Cancel()); Equal(3, old.Units!.Records.Count);
        model.Bind(f.Client); Equal(true, model.View.Command is null); Equal(3, model.View.Units!.Records.Count);
        model.Bind(null); Equal(SessionView.Empty, model.View);
    }
    private static void ClientModelCancellationAndRevocation()
    {
        using var f = new LocalUnitCommandFixture(); var model = new SessionPanel(); model.Bind(f.Client); model.Submit(f.Request()); f.Runtime.AdvanceOne(); model.Refresh();
        Equal(true, model.Cancel()); f.Runtime.AdvanceOne(); model.Refresh(); Equal((int)CommandStatus.Cancelled, (int)model.View.Command!.Status); Equal((short)1, f.Unit(0).Next.Value);
        model.Submit(f.Request(3, 103)); Equal((int)CommandStatus.Rejected, (int)model.View.Command!.Status); model.Refresh(); Equal("unit_not_available", model.View.Command!.Code);
        f.Runtime.Revoke(f.Client.Identity.Controller); Equal(false, model.Submit(f.Request())); Equal(true, model.View.Identity is null); Equal(true, model.View.Command is null);
    }
    private static void CommandTextTypedGrammar()
    {
        var descriptor = new CommandDescriptor(1, "typed", "All closed contract values", AccessClass.Player,
            new([new("n"), new("flag", ValueKind.Boolean), new("name", ValueKind.Text), new("civ", ValueKind.Civilization), new("unit", ValueKind.Unit), new("city", ValueKind.City)]), new([]));
        var request = CommandText.Parse("typed city=255:4294967295 name=\"two words \\\"quoted\\\" \\\\ tail\" n=-2147483648 flag=true unit=2047:1 civ=7", [descriptor]);
        Equal(new CommandValue.Integer(int.MinValue), CommandDescriptor.Value(request.Arguments, "n"));
        Equal(new CommandValue.Boolean(true), CommandDescriptor.Value(request.Arguments, "flag"));
        Equal(new CommandValue.Text("two words \"quoted\" \\ tail"), CommandDescriptor.Value(request.Arguments, "name"));
        Equal(new CommandValue.Civilization(new(7)), CommandDescriptor.Value(request.Arguments, "civ"));
        Equal(new CommandValue.Unit(new(new(2047), new(1))), CommandDescriptor.Value(request.Arguments, "unit"));
        Equal(new CommandValue.City(new(new(255), new(uint.MaxValue))), CommandDescriptor.Value(request.Arguments, "city"));
        Equal(true, CommandText.Help(descriptor).Contains("unit=<unit>", StringComparison.Ordinal));
        var optional = descriptor with { Arguments = new([new("name", ValueKind.Text, false)]) };
        Equal(0, CommandText.Parse("typed", [optional]).Arguments.Count); Equal(new CommandValue.Text(""), CommandDescriptor.Value(CommandText.Parse("typed name=\"\"", [optional]).Arguments, "name"));
    }
    private static void CommandTextRejectsWithoutSubmission()
    {
        using var f = new LocalUnitCommandFixture(); var descriptors = f.Client.Commands();
        foreach (string input in new[] { "", "missing", "unit.stack-bottom", "unit.stack-bottom unit=0:0", "unit.stack-bottom unit=2048:100", "unit.stack-bottom unit=00:100",
            "unit.stack-bottom unit=0:+100", "unit.stack-bottom unit=0:4294967296", "unit.stack-bottom unit=0:100 unit=0:100", "unit.stack-bottom other=0:100", "unit.stack-bottom 0:100",
            "unit.stack-bottom unit=\"0:100", "unit.stack-bottom unit=\"0:\\n100\"", new string(' ', 4097), "unit.stack-bottom " + string.Join(' ', Enumerable.Repeat("unit=0:100", 65)) })
            Throws<ArgumentException>(() => CommandText.Parse(input, descriptors));
        Equal(false, f.Runtime.HasPendingCommands); Equal((short)1, f.Unit(0).Next.Value);
    }
}
