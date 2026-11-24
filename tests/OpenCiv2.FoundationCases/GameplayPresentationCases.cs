using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private sealed class GameplayPresentationSession : IDisposable
    {
        public SessionRuntime Runtime { get; } = new(new(), new(319));
        public SinglePlayerGameplayPresentation Presentation { get; }
        public GameplayFixture F { get; }
        public IPlayerClient Viewer { get; private set; } = null!;
        public IPlayerClient Other { get; private set; } = null!;
        public IPlayerClient Initiator { get; private set; } = null!;
        public CommandId Command { get; private set; }
        public bool FailNotice, FailSuppression;
        public int Suppressions;
        private OperationContext? current;
        public GameplayPresentationSession(List<Choice>? choices = null)
        {
            Presentation = new(() => current ?? throw new InvalidOperationException("No active step"),
                () => Runtime.World.Multiplayer.LocalHuman, _ => 197,
                _ => throw new InvalidOperationException("No targeting in this case"),
                () => { ++Suppressions; if (FailSuppression) throw new InvalidOperationException("Presentation request failed"); });
            F = new(choices, data: new(Runtime.Process, Runtime.World), presentation: Presentation.Effects);
        }
        public void Start(Action action) => Start(() => new TestContinuation((_, _) => { action(); return new(); }));
        public void Start(Func<IContinuation> create, bool movement = false)
        {
            IContinuation? child = null;
            Runtime.RegisterCommand(new(new(1, "test.gameplay-presentation", "Gameplay presentation", AccessClass.Player,
                new([]), movement ? new([new("result", ValueKind.Integer)]) : new([])), (_, _, _) => new(),
                _ => new TestContinuation((context, input) =>
                {
                    current = context with
                    {
                        Notify = (recipient, text) =>
                    { context.Notify!(recipient, text); if (FailNotice) throw new InvalidOperationException("Notice failed"); }
                    };
                    try { child ??= create(); return child.Resume(context, input); }
                    finally { current = null; }
                }, () => child?.Dispose())));
            Runtime.BeginClientSession(new()); Viewer = SessionClient(Runtime); Other = SessionClient(Runtime, 2); Initiator = SessionClient(Runtime, 3);
            Command = Initiator.Submit(new("test.gameplay-presentation", new([]))).Correlation.Command; Advance();
        }
        public void Advance()
        {
            Equal(true, Runtime.AdvanceOne());
            for (int i = 0; i < 100 && RequiredResult(Initiator, Command).Status == CommandStatus.Pending; ++i)
                Equal(true, Runtime.AdvanceOne());
            Equal(false, RequiredResult(Initiator, Command).Status == CommandStatus.Pending);
        }
        public void Done()
        {
            Equal(FailNotice || FailSuppression ? CommandStatus.Failed : CommandStatus.Completed, RequiredResult(Initiator, Command).Status);
            Equal(false, Runtime.AdvanceOne()); Equal(0, Initiator.ObserveNotices()!.Items.Count);
            F.F.Consumed(); Throws<InvalidOperationException>(() => Presentation.Effects.Selection.RedrawMaps());
        }
        public void Dispose() => Runtime.Dispose();
    }

    // U02/W03/V04/S02: the complete graph uses the concrete bundle through retained contact.
    private static void GameplayPresentationMovementScenario()
    {
        foreach (bool fail in new[] { false, true })
        {
            using var s = new GameplayPresentationSession { FailSuppression = fail }; var f = s.F;
            f.Data.AddUnit(2, 2, 4, new(21, 13)); f.World.Multiplayer.LocalHuman = 2; f.World.Events.Loaded = true;
            var entry = NegotiationEvent(f.World, 1, 2, deny: true);
            entry.Values.Actions = new() { Text = true, JustOnce = true, SuppressWonderMovies = true };
            entry.SetString(Event.TextBegin, new("Root presentation contact"));
            s.Start(() => new MovementOperation(f.Begin(), c => new((ulong)c),
                _ => throw new InvalidOperationException("No mission"), _ => throw new InvalidOperationException("No intelligence"),
                _ => throw new InvalidOperationException("No capture"), _ => throw new InvalidOperationException("No hut")), movement: true);
            Equal(new MapPoint(20, 12), f.Data.Unit(0).Position); Equal(0, s.Suppressions);
            var prompt = RequiredInteraction(s.Other, s.Command); Equal(false, s.Runtime.AdvanceOne());
            Equal(prompt, RequiredInteraction(s.Other, s.Command)); Equal(false, s.Initiator.Reply(prompt, 0).Accepted);
            Equal(true, s.Other.Reply(prompt, 0).Accepted); s.Advance(); s.Done();
            Equal(1, s.Suppressions); Equal(!fail, entry.Values.Actions.Consumed); Equal(false, f.World.UnitMovementActive);
            Equal((byte)(fail ? 0 : 1), f.Data.Unit(0).LastDirection);
        }
    }

    // V02: the local observer sees the old ruler title before government/tax/revolution mutation.
    private static void GameplayPresentationGovernment()
    {
        foreach (bool revolution in new[] { false, true })
            foreach (bool fail in new[] { false, true })
            {
                using var s = new GameplayPresentationSession { FailNotice = fail }; var f = s.F;
                f.World.Globals.HumanPlayers = CivilizationSet.Only(1); f.World.Civilizations[1].Treaties[2].Embassy = true;
                f.World.Civilizations[2].Flags.Revolution = revolution; f.World.Civilizations[2].TaxRate = 7;
                f.World.Names[2].Adjective = "Highland"; f.World.Names[2].Leader = "Mira";
                f.World.Names[2].Titles = new(Enumerable.Range(0, RuleLimits.Governments).Select(i => $"Title {i}"));
                f.Data.Rules.Governments[2] = f.Data.Rules.Governments[2] with { Identity = new("crown", "Crown Council") };
                s.Start(() => f.Services.Governments.Change(2, 2)); s.Done();
                Equal(fail ? Government.Despotism : Government.Monarchy, f.World.Civilizations[2].Government);
                Equal(fail ? revolution : !revolution, f.World.Civilizations[2].Flags.Revolution);
                Equal((byte)(fail ? 7 : 3), f.World.Civilizations[2].TaxRate); Equal(true, f.World.Civilizations[2].Flags.GovernmentChanged);
                string text = s.Viewer.ObserveNotices()!.Items.Single().Text;
                Equal(true, text.Contains("Title 1 Mira", StringComparison.Ordinal)); Equal(true, text.Contains("Crown Council", StringComparison.Ordinal));
                Equal(!revolution, text.Contains("overthrown", StringComparison.Ordinal)); Equal(0, s.Other.ObserveNotices()!.Items.Count);
            }
    }

    // C08: owner-directed upgrade information precedes assignment and retains installed names.
    private static void GameplayPresentationLeonardo()
    {
        foreach (bool fail in new[] { false, true })
        {
            using var s = new GameplayPresentationSession { FailNotice = fail }; var f = s.F;
            f.World.Multiplayer.LocalHuman = 2; f.Data.AddCity(2, 1, new(4, 4));
            f.World.Globals.WonderCity[WonderIds.Leonardo] = 2; f.World.Civilizations[1].Advances = default(AdvanceSet).With(3);
            f.Data.Rules.WonderObsolescence[WonderIds.Leonardo] = AdvanceReference.None;
            f.Data.Rules.Improvements[39 + WonderIds.Leonardo] = new() { Identity = new("workshop", "Inventor's Workshop") };
            f.Data.Rules.Units[0] = new() { Identity = new("old", "Old Guard"), Role = RuleRole.Attack, ObsoleteBy = (AdvanceReference)3 };
            f.Data.Rules.Units[61] = new() { Identity = new("new", "New Guard"), Role = RuleRole.Attack, Prerequisite = (AdvanceReference)3 };
            f.Data.Unit(0).Type = new(0); f.Data.Unit(0).Flags.Veteran = true;
            s.Start(() => f.Services.Wonders.UpgradeForLeonardo(1)); s.Done();
            Equal(new UnitType((byte)(fail ? 0 : 61)), f.Data.Unit(0).Type); Equal(fail, f.Data.Unit(0).Flags.Veteran);
            string text = s.Viewer.ObserveNotices()!.Items.Single().Text;
            foreach (string name in new[] { "Old Guard", "New Guard", "Inventor's Workshop" }) Equal(true, text.Contains(name, StringComparison.Ordinal));
            Equal(0, s.Other.ObserveNotices()!.Items.Count);
        }
    }

    // C06: public wonder announcements disclose civilization and projects, not a hidden city.
    private static void GameplayPresentationWonderProduction()
    {
        foreach (var kind in Enum.GetValues<ProductionWonderNoticeKind>())
            foreach (bool fail in new[] { false, true })
            {
                using var s = new GameplayPresentationSession { FailNotice = fail }; var f = s.F;
                f.World.Globals.HumanPlayers = CivilizationSet.Only(1); f.World.Names[2].Tribe = "Hill Folk";
                ref var city = ref f.World.Entities.Cities.Storage[0]; city.Name = "Hidden Capital"; city.ShieldBox = 9;
                city.Production = (sbyte)(kind == ProductionWonderNoticeKind.Start ? 4 : -39);
                f.World.Globals.WonderCity[0] = f.World.Globals.WonderCity[1] = BuildingQueries.Unbuilt;
                f.Data.Rules.Improvements[39] = new() { Identity = new("old", "Old Wonder") };
                f.Data.Rules.Improvements[40] = new() { Identity = new("new", "New Wonder") };
                int request = kind == ProductionWonderNoticeKind.Start ? -39 : kind == ProductionWonderNoticeKind.Switch ? -40 : 4;
                s.Start(() => f.Services.Production.Set(new(0), request)); s.Done(); Equal((sbyte)request, city.Production);
                string text = s.Viewer.ObserveNotices()!.Items.Single().Text;
                Equal(true, text.Contains("Hill Folk", StringComparison.Ordinal)); Equal(true, text.Contains("Old Wonder", StringComparison.Ordinal));
                Equal(kind == ProductionWonderNoticeKind.Switch, text.Contains("New Wonder", StringComparison.Ordinal));
                Equal(false, text.Contains("Hidden Capital", StringComparison.Ordinal)); Equal(0, s.Other.ObserveNotices()!.Items.Count);
            }
    }

    // U05: transfer/deletion precede observer information, even when the notice fails.
    private static void GameplayPresentationAirlift()
    {
        foreach (bool shot in new[] { false, true })
            foreach (bool fail in new[] { false, true })
            {
                using var s = new GameplayPresentationSession([AiFlightChoice("flight", 2, false, shot ? 0 : uint.MaxValue)]) { FailNotice = fail };
                var f = s.F; f.World.Globals.HumanPlayers = CivilizationSet.Only(1);
                f.Data.AddCity(1, 2, new(24, 10)); f.World.Entities.Cities.Storage[0].Name = "Cloud Gate";
                f.Data.Unit(0).Owner = new(2); f.Data.Unit(0).Home = new(1); f.Data.Unit(0).Orders = UnitOrder.Fortified;
                f.Services.Stacks.Relocate(0, new(24, 10), false);
                f.Data.Rules.Units[27] = new() { Role = RuleRole.AirSuperiority, Domain = RuleDomain.Air, Movement = 30 };
                f.Data.AddUnit(2, 1, 27, new(24, 10)); f.World.Civilizations[1].Treaties[2].War = true;
                var airlift = new AiProductionAirlift(f.World, f.Data.Rules, f.Services.Stacks, f.Services.Deletion, f.F.Life.Random, s.Presentation.Effects.Airlift);
                s.Start(() => { airlift.Transfer(new(1), new(0), new(0), 2, "flight"); }); s.Done();
                Equal(!shot, f.Data.Unit(0).Token.Occupied); Equal(true, f.World.Entities.Cities.Storage[0].Flags.Airlifted);
                Equal(true, f.World.Entities.Cities.Storage[1].Flags.Airlifted);
                if (!shot) { Equal(f.World.Entities.Cities.Storage[0].Position, f.Data.Unit(0).Position); Equal(UnitOrder.Fortified, f.Data.Unit(0).Orders); }
                string text = s.Viewer.ObserveNotices()!.Items.Single().Text;
                Equal(true, text.Contains("Cloud Gate", StringComparison.Ordinal)); Equal(shot, text.Contains("shot down", StringComparison.Ordinal));
                Equal(0, s.Other.ObserveNotices()!.Items.Count);
            }
    }
}
