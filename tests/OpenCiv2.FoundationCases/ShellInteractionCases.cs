using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Session;
using OpenCiv2.ViewModels;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // P04/P05 required presentation contracts: availability is a query, not authority or execution.
    private static void ShellActionAvailability()
    {
        using var f = new LocalUnitCommandFixture();
        var panel = new SessionPanel(); panel.Bind(f.Client);
        UnitTarget target = new(new(0), new(100));
        var random = f.Runtime.GameplayRandom.Capture();
        var actions = ShellActions.Create(panel.View, target, new(Grid: true));
        Equal(6, actions.Select(action => action.Id).Distinct().Count());
        Equal(true, actions.Single(action => action.Id == ShellActions.StackBottom).Enabled);
        Equal(true, actions.Single(action => action.Id == ShellActions.Grid).Checked == true);
        Equal(false, actions.Single(action => action.Id == ShellActions.Bare).Checked == true);
        Equal(false, f.Runtime.HasPendingCommands); Equal(random, f.Runtime.GameplayRandom.Capture());
        Equal(false, ShellActions.Create(panel.View, new(new(0), new(999)), new())[0].Enabled);
        Equal(true, ShellActions.Create(panel.View with { Commands = new([]) }, target, new())[0].Hidden);
        Equal(false, ShellActions.Submit(panel, "unregistered", target));
        Equal(true, ShellActions.Submit(panel, ShellActions.StackBottom, target));
        Equal(false, ShellActions.Submit(panel, ShellActions.StackBottom, target));
        Equal(true, ShellActions.Create(panel.View, target, new()).All(action => !action.Enabled));
        f.Runtime.AdvanceOne(); panel.Refresh(); panel.Cancel(); f.Runtime.AdvanceOne(); panel.Refresh();
        // An already painted enabled menu does not authorize a reused slot.
        f.Unit(0).Token = new(900);
        Equal(false, ShellActions.Submit(panel, ShellActions.StackBottom, target));
        Equal(false, f.Runtime.HasPendingCommands);
    }

    private static void DialogIdentityLifecycle()
    {
        using var f = new LocalUnitCommandFixture();
        var panel = new SessionPanel(); panel.Bind(f.Client);
        var dialog = new InteractionDialogModel();
        DialogIdentity Open()
        {
            Equal(true, panel.Submit(f.Request())); f.Runtime.AdvanceOne(); panel.Refresh(); dialog.Refresh(panel.View);
            return dialog.View!.Identity;
        }

        var first = Open();
        Equal(new OwnedList<int>([0, 1]), dialog.View!.Spec.Choices);
        Equal(false, dialog.Respond(panel, first, 99));
        Equal(false, dialog.View!.Submitted);
        Equal(true, dialog.Respond(panel, first, 1));
        Equal(true, dialog.View!.Submitted);
        Equal(false, dialog.Respond(panel, first, 1));
        Equal(false, dialog.Respond(panel, first, null));
        f.Runtime.AdvanceOne(); panel.Refresh(); dialog.Refresh(panel.View);
        Equal(true, dialog.View is null); Equal(1, panel.View.Command!.Committed.Count);
        var second = Open(); Equal(false, second == first);
        Equal(false, dialog.Respond(panel, first, 0));
        Equal(true, dialog.Respond(panel, second, null));
        f.Runtime.AdvanceOne(); panel.Refresh(); Equal(CommandStatus.Cancelled, panel.View.Command!.Status);
        var third = Open();
        panel.Bind(f.Other); dialog.Refresh(panel.View);
        Equal(true, dialog.View is null); Equal(false, dialog.Respond(panel, third, 1));
        f.Runtime.Revoke(f.Other.Identity.Controller); panel.Refresh(); dialog.Refresh(panel.View);
        Equal(true, dialog.View is null);
    }

    private static void DialogTargetRevalidation()
    {
        for (int change = 0; change < 3; change++)
        {
            using var f = new LocalUnitCommandFixture();
            var panel = new SessionPanel(); panel.Bind(f.Client); panel.Submit(f.Request());
            f.Runtime.AdvanceOne(); panel.Refresh();
            var dialog = new InteractionDialogModel(); dialog.Refresh(panel.View);
            var identity = dialog.View!.Identity;
            if (change == 0) f.Unit(0).Token = new(900);
            else if (change == 1) f.Unit(0).Owner = new(2);
            else f.Unit(1).Previous = null;
            var before = f.Runtime.World.Entities.Units.Storage.ToArray();
            Equal(true, dialog.Respond(panel, identity, 1));
            f.Runtime.AdvanceOne(); panel.Refresh(); dialog.Refresh(panel.View);
            Equal(CommandStatus.Failed, panel.View.Command!.Status);
            Equal(0, panel.View.Command.Committed.Count); Equal(true, dialog.View is null);
            Equal(true, before.SequenceEqual(f.Runtime.World.Entities.Units.Storage.ToArray()));
        }
    }

    private static void DialogDeadlineAndPeerTraffic()
    {
        using var runtime = new SessionRuntime(new(), new(1));
        runtime.RegisterCommand(CommandDefinitionFor(_ => DecisionContinuation(cancellable: false)));
        runtime.RegisterCommand(CommandDefinitionFor(_ => new TestContinuation((_, _) => CommandDone(7)), "test.traffic"));
        runtime.BeginClientSession(new());
        var client = SessionClient(runtime); var peer = runtime.AddParticipant(new(2), new(7));
        var panel = new SessionPanel(); panel.Bind(client); panel.Submit(CommandRequestFor()); runtime.AdvanceOne(); panel.Refresh();
        var dialog = new InteractionDialogModel(); dialog.Refresh(panel.View);
        var identity = dialog.View!.Identity;
        Equal(false, dialog.Respond(panel, identity, null));
        for (ulong message = 1; message <= 8; message++)
        {
            var received = runtime.ReceiveRequest(new(7), peer, new(new(77), new(1), new(message)), CommandRequestFor(name: "test.traffic"));
            Equal(CommandStatus.Pending, received.Status); Equal(true, runtime.AdvanceOne());
            Equal(CommandStatus.Completed, runtime.TakeReplies().Single().Status);
            panel.Refresh(); dialog.Refresh(panel.View); Equal(identity, dialog.View!.Identity);
        }
        Equal(0, runtime.ExpireInteractions(49)); Equal(1, runtime.ExpireInteractions(50));
        Equal(false, dialog.Respond(panel, identity, 4));
        Equal(true, runtime.AdvanceOne()); panel.Refresh(); dialog.Refresh(panel.View);
        Equal(CommandStatus.Completed, panel.View.Command!.Status); Equal(true, dialog.View is null);
        Equal(CommandValues(-3), panel.View.Command.Result);
        Equal(2, panel.View.Command.Committed.Count);
    }
}
