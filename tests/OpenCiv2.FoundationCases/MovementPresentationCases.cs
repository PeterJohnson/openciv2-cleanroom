using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.Units;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private sealed class MovementPresentationSession : IDisposable
    {
        public SessionRuntime Runtime { get; } = new(new(), new(318));
        public SinglePlayerMovementPresentation Presentation { get; }
        public SinglePlayerMovementFixture F { get; }
        public IPlayerClient Actor { get; private set; } = null!;
        public IPlayerClient Observer { get; private set; } = null!;
        public IPlayerClient Initiator { get; private set; } = null!;
        public CommandId Command { get; private set; }
        public bool FailNotice, FailPublication;
        private OperationContext? current;
        public MovementPresentationSession(List<Choice>? choices = null)
        {
            Presentation = new(() => current ?? throw new InvalidOperationException("No active step"), () => Runtime.World.Multiplayer.LocalHuman);
            F = new(choices, new(Runtime.Process, Runtime.World), Presentation.Effects);
        }
        public void Start(Func<IContinuation>? create = null)
        {
            bool selection = create is not null;
            create ??= () => F.Session(); IContinuation? child = null;
            Runtime.RegisterCommand(new(new(1, "test.movement-presentation", "Movement presentation", AccessClass.Player,
                new([]), selection ? new([]) : new([new("result", ValueKind.Integer)])), (_, _, _) => new(), _ => new TestContinuation((context, input) =>
                {
                    current = context with
                    {
                        Publish = () => { context.Publish(); if (FailPublication) throw new InvalidOperationException("Display unavailable"); },
                        Notify = (recipient, text) =>
                        { context.Notify!(recipient, text); if (FailNotice) throw new InvalidOperationException("Notice unavailable"); }
                    };
                    try { child ??= create(); return child.Resume(context, input); }
                    finally { current = null; }
                }, () => child?.Dispose())));
            Runtime.RegisterCommand(new(new(1, "test.after-selection", "Queued command", AccessClass.Player, new([]), new([])),
                (_, _, _) => new(), _ => new TestContinuation((_, _) => new())));
            Runtime.BeginClientSession(new()); Actor = SessionClient(Runtime); Observer = SessionClient(Runtime, 2); Initiator = SessionClient(Runtime, 3);
            Command = Initiator.Submit(new("test.movement-presentation", new([]))).Correlation.Command; Advance();
        }
        private void Advance()
        {
            Equal(true, Runtime.AdvanceOne());
            for (int i = 0; i < 100 && RequiredResult(Initiator, Command).Status == CommandStatus.Pending; ++i)
                Equal(true, Runtime.AdvanceOne());
            Equal(false, RequiredResult(Initiator, Command).Status == CommandStatus.Pending);
        }
        public void Answer(IPlayerClient recipient, int choice)
        {
            var prompt = RequiredInteraction(recipient, Command); Equal(false, Initiator.Reply(prompt, choice).Accepted);
            Equal(true, recipient.Reply(prompt, choice).Accepted); Advance();
        }
        public void Done()
        {
            Equal(FailNotice || FailPublication ? CommandStatus.Failed : CommandStatus.Completed, RequiredResult(Initiator, Command).Status);
            Equal(0, Initiator.ObserveNotices()!.Items.Count); Equal(false, F.World.UnitMovementActive);
            Throws<InvalidOperationException>(() => Presentation.Publish()); F.Consumed();
        }
        public void Dispose() => Runtime.Dispose();
    }

    // U02/W06: notice failure preserves arrival/hut consumption but precedes nomad creation.
    private static void MovementPresentationHutAndFailure()
    {
        foreach (bool fail in new[] { false, true })
        {
            using var s = new MovementPresentationSession(MoveChoices(HutStart(0), "hut", "move/0/finish/hut")) { FailNotice = fail };
            var f = s.F; var destination = new MapPoint(20, 12);
            f.World.Map.PatternSeed = 31; f.World.Map.TileAt(destination).Owner = null; f.World.Map.TileAt(destination).Fertility = 1;
            f.Data.AddCity(2, 1, new(4, 4)); f.World.Civilizations[1].CityCount = 1;
            Equal(true, f.World.Map.Hut(destination)); s.Start(); s.Done();
            Equal(destination, f.Data.Unit(0).Position); Equal(false, f.World.Map.Hut(destination));
            int settlers = Enumerable.Range(1, f.World.Entities.Units.End).Count(i => f.Data.Unit(i).Token.Occupied
                && f.Data.Unit(i).Type.Value == UnitTypeIds.Settlers);
            Equal(fail ? 0 : 1, settlers); Equal((byte)(fail ? 0 : 1), f.Data.Unit(0).LastDirection);
            string text = s.Actor.ObserveNotices()!.Items.Single().Text;
            Equal(true, text.Contains("nomads", StringComparison.Ordinal)); Equal(true, text.Contains("(20, 12)", StringComparison.Ordinal));
            Equal(0, s.Observer.ObserveNotices()!.Items.Count); Equal(false, s.Runtime.AdvanceOne());
        }
    }

    // C07/U02: retained caravan choice, committed donation, then notice and actual unit deletion.
    private static void MovementPresentationDonationAndFailure()
    {
        foreach (bool fail in new[] { false, true })
        {
            using var s = new MovementPresentationSession { FailNotice = fail };
            var f = s.F; f.AtBirch(); f.Data.Unit(0).Type = new(3);
            f.Data.Unit(0).Counter = 255; f.Data.Unit(0).Home = null; f.Data.Unit(0).Destination = new(-1, -1);
            f.F.Capture.City.Owner = new(1); f.F.Capture.City.Name = "Stone Haven";
            f.World.Map.TileAt(new(12, 8)).Owner = 1; f.F.Capture.City.Production = -39;
            f.Data.Rules.Units[3] = f.Data.Rules.Units[3] with { CostRows = 5 };
            s.Start(); Equal((short)0, f.F.Capture.City.ShieldBox); Equal(0, s.Actor.ObserveNotices()!.Items.Count);
            var prompt = RequiredInteraction(s.Actor, s.Command); Equal(false, s.Runtime.AdvanceOne()); Equal(prompt, RequiredInteraction(s.Actor, s.Command));
            s.Answer(s.Actor, (int)CaravanAction.HelpBuildWonder); s.Done();
            Equal((short)50, f.F.Capture.City.ShieldBox); Equal(fail, f.Data.Unit(0).Token.Occupied);
            string text = s.Actor.ObserveNotices()!.Items.Single().Text;
            Equal(true, text.Contains("Stone Haven", StringComparison.Ordinal)); Equal(true, text.Contains("50 shields", StringComparison.Ordinal));
            Equal(0, s.Observer.ObserveNotices()!.Items.Count); Equal(false, s.Runtime.AdvanceOne());
        }
    }

    // U04/W03/V04/S02: focus precedes real contact; cosmetic failure preserves selection and queued commands.
    private static void MovementPresentationSelectionContact()
    {
        foreach (bool fail in new[] { false, true })
        {
            using var s = new MovementPresentationSession { FailPublication = fail };
            var f = s.F; f.World.Globals.TurnCiv = 1; f.World.Globals.ActiveUnit = new(0);
            f.Data.Unit(0).MovesUsed = 0; f.Data.Unit(0).AuxiliaryTimer = 7;
            f.Data.AddUnit(2, 2, 4, new(19, 13)); f.World.Multiplayer.LocalHuman = 2;
            f.World.Events.Loaded = true; var entry = NegotiationEvent(f.World, 1, 2, deny: true);
            entry.Values.Actions = new() { Text = true, JustOnce = true };
            entry.SetString(OpenCiv2.Core.State.Event.TextBegin, new("Selection contact"));
            var actorView = f.World.Views[1];
            var selection = new UnitSelection(f.World, f.Data.Rules, f.Combat.Services.State, f.Life.Services.Revelation, s.Presentation);
            s.Start(() => new UnitSelectionOperation(selection.BeginNext(new(18, 12), 0, 1, "selection"), c => new((ulong)c),
                _ => throw new InvalidOperationException("No intelligence in this contact")));
            Equal(new UnitSlot(0), f.World.Globals.ActiveUnit); Equal((byte)0, f.Data.Unit(0).AuxiliaryTimer);
            Equal(actorView, f.World.Views[1]); Equal(new MapPoint(18, 12), f.World.Views[2].Center);
            var prompt = RequiredInteraction(s.Observer, s.Command); Equal(false, s.Runtime.AdvanceOne()); Equal(prompt, RequiredInteraction(s.Observer, s.Command));
            var queued = s.Initiator.Submit(new("test.after-selection", new([]))).Correlation.Command;
            s.Answer(s.Observer, 0); s.Done(); Equal(true, entry.Values.Actions.Consumed);
            // The ready queue executes the admitted command before the later interaction reply.
            Equal(CommandStatus.Completed, RequiredResult(s.Initiator, queued).Status); Equal(false, s.Runtime.AdvanceOne());
            Equal(0, s.Actor.ObserveNotices()!.Items.Count); Equal(0, s.Observer.ObserveNotices()!.Items.Count);
        }
    }
}
