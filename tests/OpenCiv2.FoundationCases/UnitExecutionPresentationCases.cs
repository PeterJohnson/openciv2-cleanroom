using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;
using OpenCiv2.Formats.Classic;
using OpenCiv2.Formats.Content;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private sealed class UnitExecutionPresentationSession : IDisposable
    {
        public SessionRuntime Runtime { get; } = new(new(), new(320));
        public GameplayFixture F { get; }
        public SinglePlayerUnitExecution Units { get; }
        public SinglePlayerUnitExecutionPresentation Presentation { get; }
        public IPlayerClient Actor { get; }
        public IPlayerClient Observer { get; }
        public IPlayerClient Initiator { get; }
        public CommandId Command { get; private set; }
        public CitySlot? SelectedCity;
        public bool FailNotice, FailPublish, FailClose;
        public int Focuses, Closes;
        private Func<IContinuation>? next;
        private OperationContext? current, execution;
        public UnitExecutionPresentationSession()
        {
            var gameplay = new SinglePlayerGameplayPresentation(
                () => current ?? throw new InvalidOperationException("No active step"),
                () => Runtime.World.Multiplayer.LocalHuman, _ => 197,
                _ => throw new InvalidOperationException("Unexpected targeting"),
                () => throw new InvalidOperationException("Unexpected suppression"));
            Presentation = new(() => execution ?? throw new InvalidOperationException("No active step"),
                () => Runtime.World.Multiplayer.LocalHuman, () => SelectedCity, () => ++Focuses,
                city =>
                {
                    Equal(SelectedCity, (CitySlot?)city); SelectedCity = null; ++Closes;
                    if (FailClose) throw new IOException("City view unavailable");
                });
            F = new(data: new(Runtime.Process, Runtime.World), presentation: gameplay.Effects);
            var text = new ClassicTextInput();
            text.Files["scenario/CITY.TXT"] = "@BARBARIANS\n1\nSelected ransom message\n";
            Units = new(F.World, F.Data.Process, F.F.Life.Random, F.Services,
                GameplayContent.Builtin.Values.Calendar, new ClassicRansomText(text.Reader()), Presentation.Effects);
            Register("action", new([]));
            Register("founding", new([new("outcome", ValueKind.Text)]));
            Register("human", HumanMovementPhaseHost.ResultSchema);
            Runtime.BeginClientSession(new());
            Actor = SessionClient(Runtime); Observer = SessionClient(Runtime, 2); Initiator = SessionClient(Runtime, 3);
        }
        private void Register(string name, OwnedList<ValueSchema> schema)
        {
            Runtime.RegisterCommand(new(new(1, "test.execution-" + name, "Unit execution presentation", AccessClass.Player, new([]), schema),
                (_, _, _) => new(), _ =>
                {
                    var create = next ?? throw new InvalidOperationException("Missing test operation");
                    IContinuation? child = null;
                    return new TestContinuation((context, input) =>
                    {
                        current = context;
                        execution = context with
                        {
                            Publish = () => { context.Publish(); if (FailPublish) throw new IOException("View unavailable"); },
                            Notify = (recipient, text) => { context.Notify!(recipient, text); if (FailNotice) throw new IOException("Notice unavailable"); }
                        };
                        try { child ??= create(); return child.Resume(context, input); }
                        finally { current = execution = null; }
                    }, () => child?.Dispose());
                }));
        }
        public void Start(Action action) => Start(() => new TestContinuation((_, _) => { action(); return new(); }));
        public void Start(Func<IContinuation> create, string name = "action")
        {
            next = create;
            Command = Initiator.Submit(new("test.execution-" + name, new([]))).Correlation.Command; Advance();
        }
        public void Advance()
        {
            Equal(true, Runtime.AdvanceOne());
            for (int i = 0; i < 100 && Result.Status == CommandStatus.Pending; ++i) Equal(true, Runtime.AdvanceOne());
            Equal(false, Result.Status == CommandStatus.Pending);
        }
        public CommandUpdate Result => RequiredResult(Initiator, Command);
        public void Answer(IPlayerClient player, string? name = null)
        {
            var prompt = RequiredInteraction(player, Command);
            Equal(false, Runtime.AdvanceOne()); Equal(prompt, RequiredInteraction(player, Command));
            Equal(false, (name is null ? Initiator.Reply(prompt, 0) : Initiator.Reply(prompt, 0, name)).Accepted);
            Equal(true, (name is null ? player.Reply(prompt, 0) : player.Reply(prompt, 0, name)).Accepted); Advance();
        }
        public void Done()
        {
            Equal(false, Runtime.AdvanceOne()); Equal(0, Initiator.ObserveNotices()!.Items.Count);
            F.F.Consumed(); Throws<InvalidOperationException>(Presentation.Publish);
        }
        public void Dispose() => Runtime.Dispose();
    }

    // W04: the live owner's rejection includes installed names, before any work mutation.
    private static void UnitExecutionPresentationWork()
    {
        foreach (bool fail in new[] { false, true })
        {
            using var s = new UnitExecutionPresentationSession { FailNotice = fail }; var f = s.F;
            f.World.Multiplayer.LocalHuman = 2; f.Data.Unit(0).Type = new(0);
            f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Role = RuleRole.Settle, Identity = new("pioneers", "Pioneers") };
            f.Data.Rules.Advances[AdvanceIds.Radio] = f.Data.Rules.Advances[AdvanceIds.Radio] with { Identity = new("signals", "Wireless Signals") };
            var order = f.Data.Unit(0).Orders; var moves = f.Data.Unit(0).MovesUsed;
            var features = f.World.Map.TileAt(f.Data.Unit(0).Position).Features;
            s.Start(() => { Equal(false, s.Units.Work.Request(new(0), UnitOrder.Airbase)); });
            Equal(fail ? CommandStatus.Failed : CommandStatus.Completed, s.Result.Status);
            string notice = s.Actor.ObserveNotices()!.Items.Single().Text;
            Equal(true, notice.Contains("Pioneers cannot build an airbase", StringComparison.Ordinal));
            Equal(true, notice.Contains("Wireless Signals", StringComparison.Ordinal));
            Equal(0, s.Observer.ObserveNotices()!.Items.Count);
            Equal(order, f.Data.Unit(0).Orders); Equal(moves, f.Data.Unit(0).MovesUsed);
            Equal(features, f.World.Map.TileAt(f.Data.Unit(0).Position).Features); s.Done();
        }
    }

    // C01: real creation/deletion is retained across naming and the final city visit.
    // Required notice failure keeps the founded city; cosmetic failure reports after the visit.
    private static void UnitExecutionPresentationFounding()
    {
        foreach (int failure in new[] { 0, 1, 2 })
        {
            using var s = new UnitExecutionPresentationSession { FailNotice = failure == 1, FailPublish = failure == 2 }; var f = s.F;
            f.Data.Unit(0).Type = new(0); f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Role = RuleRole.Settle };
            f.World.Civilizations[1].Tribe = 0; f.World.Civilizations[1].CityCount = 0;
            f.World.Names[1].Tribe = "tribes 0"; f.Data.Process.Rules.Leaders[0].CityNameIndex = 0;
            f.World.Globals.GameOptions.Tutorial = true; f.World.Globals.RevealMap = 1; f.World.Multiplayer.LocalHuman = 2;
            var position = f.Data.Unit(0).Position; var ownerCenter = f.World.Views[1].Center;
            int visits = 0;
            s.Start(() => new CityFoundingOperation(s.Units.Founding.BeginPlayer(new(0), "present-founding"),
                c => new((ulong)c), city => { ++visits; return new("Visit " + city.Name, null, new CommandValue.City(city.City), new([0])); }),
                "founding");
            Equal(CommandStatus.AwaitingInteraction, s.Result.Status); Equal(true, f.Data.Unit(0).Token.Occupied);
            var citySlot = CityQueries.At(f.World, position)!.Value;
            s.Answer(s.Actor, "River Haven");
            Equal("River Haven", f.World.Entities.Cities.At(citySlot).Name); Equal(false, f.Data.Unit(0).Token.Occupied);
            Equal(position, f.World.Views[2].Center); Equal(ownerCenter, f.World.Views[1].Center);
            if (failure != 1)
            {
                Equal(CommandStatus.AwaitingInteraction, s.Result.Status); Equal(1, visits);
                Equal(true, f.World.Calculation.Processing); s.Answer(s.Actor);
                Equal(false, f.World.Calculation.Processing);
                Equal("Founded", ((CommandValue.Text)s.Result.Result.Single().Value).Value);
                if (failure == 2) Equal("city_founding_presentation_failed", s.Result.Code);
            }
            else Equal(0, visits);
            Equal(failure == 0 ? CommandStatus.Completed : CommandStatus.Failed, s.Result.Status);
            var notices = s.Actor.ObserveNotices()!.Items;
            Equal(failure == 1 ? 1 : 2, notices.Count); Equal(true, notices[0].Text.Contains("Founded River Haven", StringComparison.Ordinal));
            Equal(0, s.Observer.ObserveNotices()!.Items.Count); s.Done();
        }
    }

    // F04: input and city-view effects bind to the host while actual selection and orders run.
    private static void UnitExecutionPresentationHumanPhase()
    {
        foreach (bool fail in new[] { false, true })
        {
            using var s = new UnitExecutionPresentationSession { FailClose = fail, SelectedCity = new(0) }; var f = s.F;
            f.World.Globals.HumanPlayers = CivilizationSet.Only(1); f.World.Globals.TurnCiv = 1;
            f.World.TurnLoopActive = true; f.World.MovementMode = MovementMode.MoveUnits;
            f.Data.Unit(0).Orders = UnitOrder.None; f.Data.Unit(0).MovesUsed = 0;
            using var host = new HumanMovementPhaseHost(s.Runtime, s.Units.Human.Begin(new(), f.F.F.Inputs, false, "present-human"),
                () => new(new(18, 12), s.SelectedCity), c => new((ulong)c),
                _ => throw new InvalidOperationException("Unexpected visit"), _ => throw new InvalidOperationException("Unexpected intelligence"),
                _ => throw new InvalidOperationException("Unexpected capture"), _ => throw new InvalidOperationException("Unexpected hut"));
            s.Start(() => host.BeginSegment(false), "human");
            Equal(CommandStatus.Completed, s.Result.Status); Equal(true, host.NeedsInput); Equal(1, s.Focuses);
            Equal(false, s.Runtime.AdvanceOne()); Equal(1, s.Focuses); Equal(0, s.Closes);
            s.Start(() => s.Units.Fortification.Apply(new(0))); Equal(CommandStatus.Completed, s.Result.Status);
            s.Start(() => host.BeginSegment(true), "human");
            Equal(fail ? CommandStatus.Failed : CommandStatus.Completed, s.Result.Status);
            if (fail) Equal("human_movement_presentation_failed", s.Result.Code);
            Equal(true, host.Completed); Equal(false, f.World.PlayerPhaseActive); Equal(null, f.World.Globals.ActiveUnit);
            Equal(UnitOrder.Fortified, f.Data.Unit(0).Orders); Equal(null, s.SelectedCity); Equal(1, s.Closes);
            Equal(0, s.Actor.ObserveNotices()!.Items.Count); Equal(0, s.Observer.ObserveNotices()!.Items.Count); s.Done();
        }
    }
}
