using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // V03 §§10–11 retained grant order with actual V02 setter, V05 reception and recursive Library.
    private sealed class RetainedGrantFixture : IEmissaryEffects, IResearchGovernmentEffects
    {
        public GrantFixture G { get; }
        public GameState World => G.World;
        public ResearchGrants Grants { get; }
        public ResearchGovernmentPrompts Governments { get; }
        public SinglePlayerResearch Services { get; }
        public GovernmentChanges Setter { get; }
        public List<EmissaryNotice> EmissaryNotices { get; } = [];
        public int Closed;
        public bool FailClose, FailRevolution;
        public RetainedGrantFixture(List<Choice>? choices = null, RevelationFixture? data = null)
            : this(new GrantFixture(data, (choices ?? []).ToArray())) { }
        public RetainedGrantFixture(GrantFixture grant)
        {
            G = grant;
            World.Civilizations[1].Government = Government.Despotism;
            World.Civilizations[1].MilitaryStrength = World.Civilizations[2].MilitaryStrength = 20;
            World.Globals.CivsAlive = CivilizationSet.Only(1).With(2);
            World.Civilizations[2].Treaties[1].Contact = true;
            for (int i = 0; i < RuleLimits.Governments; ++i)
            { G.Rules.Governments[i] = G.Rules.Governments[i] with { Identity = new("government-" + i, "Government " + i) }; }
            Setter = new GovernmentChanges(World, G.Data.Process.Rules, G.Data.Stacks,
                _ => throw new InvalidOperationException("No government selector in direct Anarchy"),
                _ => throw new InvalidOperationException("No rates dialog in direct Anarchy"), _ => { });
            Services = new(World, G.Data.Process.Rules, G.Random, G.Data.Stacks, Setter,
                new(World, G.Rules, G.Upgrades.Add, _ => { }), this, G, this);
            Governments = Services.GovernmentPrompts;
            Grants = Services.Grants;
        }
        public ResearchGrants.Operation Begin(int civilization = 1, int advance = AdvanceIds.Monarchy, int source = 1, string call = "grant") =>
            Grants.Begin(civilization, advance, source, false, call);
        public void Complete(ResearchGrants.Operation operation)
        {
            for (int i = 0; i < 20 && !operation.Completed; ++i) Equal(null, operation.Resume());
            Equal(true, operation.Completed); G.Random.RequireChoicesConsumed();
        }
        public int ConfirmGovernment(ResearchGovernmentChoice choice) => throw new InvalidOperationException("Retained prompts must not use synchronous replies");
        public void RevolutionStarting(int civilization) { G.RevolutionStarting(civilization); if (FailRevolution) throw new InvalidOperationException("Required revolution notice unavailable"); }
        public void Tutorial(int civilization, ResearchTutorial tutorial) => G.Tutorial(civilization, tutorial);
        public void Notice(EmissaryNotice notice) => EmissaryNotices.Add(notice);
        public void OpenLeader(int counterpart) { }
        public void CloseLeader() { ++Closed; if (FailClose) throw new InvalidOperationException("Leader close unavailable"); }
        public void EnsureVisible(MapPoint point, int observer) { }
    }
    private static Choice RetainedGrantEmissary() => Choice.Create(new("V03.emissary", "grant"), new SamplingLaw.SecondaryRange(0, 2), ChoiceCode.Secondary(0));
    private static void ResearchGrantRetainedGovernment()
    {
        foreach (int answer in new[] { 0, 1, 2 })
        {
            var f = new RetainedGrantFixture(); using var op = f.Begin();
            var pending = (ResearchGrantDecision.Government)op.Resume()!;
            Equal(new ResearchGovernmentChoice(1, AdvanceIds.Monarchy, Government.Despotism, Government.Monarchy, true), pending.Choice);
            Equal(true, f.World.Civilizations[1].Advances.Contains(AdvanceIds.Monarchy)); Equal((byte)1, f.World.Civilizations[1].NumAdvances);
            Equal((byte)0, f.World.Globals.FirstDiscoverer[AdvanceIds.Monarchy]); Equal(false, op.Completed);
            Equal(pending, op.Resume()); Equal((byte)1, f.World.Civilizations[1].NumAdvances); Equal(1, f.G.Acquisitions.Count);
            // A genuine decision boundary may change the government. Acceptance is not revalidated.
            f.World.Civilizations[1].Government = Government.Monarchy;
            Equal(null, op.Resume(answer)); f.Complete(op); Equal(answer == 1 ? Government.Anarchy : Government.Monarchy, f.World.Civilizations[1].Government);
            Equal((byte)1, f.World.Globals.FirstDiscoverer[AdvanceIds.Monarchy]); Equal(0, f.G.GovernmentChoices.Count);
            Equal(null, op.Resume(1)); Equal((byte)1, f.World.Civilizations[1].NumAdvances);
        }
        var tutorial = new RetainedGrantFixture(); tutorial.World.Globals.GameOptions.Tutorial = true;
        using var republic = tutorial.Begin(advance: AdvanceIds.Republic); tutorial.Complete(republic);
        Sequence(new[] { ResearchTutorial.Republic }, tutorial.G.Tutorials); Equal(Government.Despotism, tutorial.World.Civilizations[1].Government);
        var zero = new RetainedGrantFixture(); zero.World.Globals.Turn = 0;
        using var initial = zero.Begin(); zero.Complete(initial); Equal((byte)0, zero.World.Civilizations[1].NumAdvances); Equal(0, zero.G.Acquisitions.Count);
    }
    private static void ResearchGrantRetainedLibrary()
    {
        var f = new RetainedGrantFixture(); var w = f.World;
        f.G.Data.AddCity(0, 1, new(10, 8)); w.Civilizations[1].CityCount = 1;
        w.Globals.WonderCity[WonderIds.GreatLibrary] = 0;
        w.Civilizations[3].Advances = w.Civilizations[3].Advances.With(AdvanceIds.Monarchy);
        w.Globals.AdvanceKnownBy[AdvanceIds.Monarchy] = CivilizationSet.Only(3);
        using var op = f.Begin(2, source: 0); var pending = (ResearchGrantDecision.Government)op.Resume()!;
        Equal(1, pending.Choice.Civilization); Equal((byte)1, w.Civilizations[1].NumAdvances); Equal((byte)1, w.Civilizations[2].NumAdvances);
        Equal((byte)254, w.Civilizations[1].AdvanceSource[AdvanceIds.Monarchy]); Equal(false, op.Completed);
        Equal(pending, op.Resume()); Equal(1, f.G.Acquisitions.Count); Equal("Building 43", f.G.Acquisitions[0].SourceName);
        op.Resume(0); f.Complete(op); Equal((byte)1, w.Civilizations[1].NumAdvances); Equal((byte)1, w.Civilizations[2].NumAdvances);
        Equal(CivilizationSet.Only(1).With(2).With(3), w.Globals.AdvanceKnownBy[AdvanceIds.Monarchy]);
    }
    private static void ResearchGrantRetainedEmissary()
    {
        foreach (bool accept in new[] { false, true })
        {
            var choices = new List<Choice> { RetainedGrantEmissary() };
            if (accept) choices.Add(GreetingChoice("grant/emissary/greet"));
            var f = new RetainedGrantFixture(choices) { FailClose = true }; using var op = f.Begin(2, source: 0);
            var pending = op.Resume(); Equal(true, pending is ResearchGrantDecision.Emissary { Decision: EmissaryDecision.Receive });
            Equal((byte)1, f.World.Civilizations[2].NumAdvances); Equal((byte)0, f.World.Globals.FirstDiscoverer[AdvanceIds.Monarchy]);
            Equal(pending, op.Resume()); Equal(0, f.Closed); Equal(0, f.G.Order.Count);
            op.Resume(accept ? 0 : 1); f.Complete(op); Equal(accept ? 1 : 0, f.EmissaryNotices.Count);
            Equal(accept, f.G.Order.Contains("gape:54")); Equal(false, f.G.Order.Any(s => s.StartsWith("emissary:")));
            Equal(1, f.Closed); Equal(1, op.PresentationFailures.Count);
            Equal(false, f.World.Diplomacy.MeetingActive || f.World.Diplomacy.AskedEmissary || f.World.Diplomacy.InitialEvaluationDone);
            Equal(0, f.World.Diplomacy.EmissaryResponse); Equal((byte)2, f.World.Globals.FirstDiscoverer[AdvanceIds.Monarchy]);
        }
    }
    private static void ResearchGrantRetainedFailureAndDisposal()
    {
        var f = new RetainedGrantFixture(); using var disposed = f.Begin(); Equal(true, disposed.Resume() is ResearchGrantDecision.Government);
        disposed.Dispose(); Throws<ObjectDisposedException>(() => disposed.Resume(1));
        Equal((byte)1, f.World.Civilizations[1].NumAdvances); Equal((byte)0, f.World.Globals.FirstDiscoverer[AdvanceIds.Monarchy]); Equal(Government.Despotism, f.World.Civilizations[1].Government);
        f = new() { FailRevolution = true }; using var failed = f.Begin(); failed.Resume();
        Throws<InvalidOperationException>(() => failed.Resume(1)); Throws<InvalidOperationException>(() => failed.Resume(0));
        Equal(Government.Despotism, f.World.Civilizations[1].Government); Equal((byte)1, f.World.Civilizations[1].NumAdvances); Equal((byte)0, f.World.Globals.FirstDiscoverer[AdvanceIds.Monarchy]);
        f = new(); f.G.OnAcquired = _ => throw new InvalidOperationException("Required acquisition notice unavailable"); using var notice = f.Begin();
        Throws<InvalidOperationException>(() => notice.Resume()); Equal((byte)1, f.World.Civilizations[1].NumAdvances); Equal((byte)0, f.World.Globals.FirstDiscoverer[AdvanceIds.Monarchy]);
        f = new(); Throws<InvalidOperationException>(() => f.Grants.Grant(1, 54, 1, false, "sync")); Equal((byte)0, f.World.Civilizations[1].NumAdvances);
        var legacy = new GrantFixture(); legacy.World.Civilizations[1].Government = Government.Despotism;
        using var compatible = legacy.Grants.Begin(1, 54, 1, false, "legacy");
        Equal(null, compatible.Resume()); Equal(true, compatible.Completed); Equal(1, legacy.GovernmentChoices.Count);
        f.World.Globals.Multiplayer = GameMode.Tcp; using var remote = f.Begin(); Throws<InvalidOperationException>(() => remote.Resume()); Equal((byte)0, f.World.Civilizations[1].NumAdvances);
    }
    private static void ResearchGrantPrivateSession()
    {
        foreach (var input in new[] { OperationInputKind.Cancel, OperationInputKind.Timeout })
            foreach (bool emissary in new[] { false, true })
            {
                var f = new RetainedGrantFixture(emissary ? [RetainedGrantEmissary()] : []);
                var context = new OperationContext(f.World, f.G.Data.Process, f.G.Random, new PresentationRandom(1, "grant"),
                    new(default, new(3)), default, (_, _) => { }, () => { });
                using var core = f.Begin(emissary ? 2 : 1, source: emissary ? 0 : 1);
                using var session = new ResearchGrantOperation(core, c => new((ulong)c),
                    _ => throw new InvalidOperationException("No intelligence in this grant"));
                Equal(CommandStatus.AwaitingInteraction, session.Resume(context, new()).Status);
                Equal(CommandStatus.Completed, session.Resume(context, new(input)).Status);
                Equal(Government.Despotism, f.World.Civilizations[1].Government);
                Equal(emissary ? 1 : 0, f.Closed); Equal(false, f.G.Order.Contains("gape:54"));
                Equal((byte)1, f.World.Civilizations[emissary ? 2 : 1].NumAdvances); f.G.Random.RequireChoicesConsumed();
            }
        foreach (bool emissary in new[] { false, true })
        {
            using var runtime = new SessionRuntime(new(), new(271));
            var f = new RetainedGrantFixture(emissary ? [RetainedGrantEmissary()] : [], new(runtime.Process, runtime.World));
            ParticipantId actorSeat = default, ownerSeat = default;
            runtime.RegisterCommand(new(new(1, "test.grant", "Authored retained grant", AccessClass.Player, new([]), new([new("granted", ValueKind.Boolean)])), (_, _, _) => new(),
                _ => new ResearchGrantOperation(f.Begin(emissary ? 2 : 1, source: emissary ? 0 : 1), c => c == 1 ? ownerSeat : actorSeat,
                    _ => throw new InvalidOperationException("No intelligence in this grant"))));
            runtime.BeginClientSession(new()); var actor = SessionClient(runtime, 2); var owner = SessionClient(runtime, 1);
            actorSeat = actor.Identity.Participant; ownerSeat = owner.Identity.Participant;
            var id = actor.Submit(new("test.grant", new([]))).Correlation.Command; runtime.AdvanceOne();
            var prompt = RequiredInteraction(owner, id); Equal(null, RequiredResult(actor, id).Interaction); Equal(false, actor.Reply(prompt, 1).Accepted);
            Equal(true, owner.Reply(prompt, 1).Accepted); runtime.AdvanceOne(); Equal(CommandStatus.Completed, RequiredResult(actor, id).Status);
            Equal(new CommandValue.Boolean(true), RequiredResult(actor, id).Result.Single().Value);
            Equal(emissary ? Government.Despotism : Government.Anarchy, f.World.Civilizations[1].Government);
            Equal(emissary ? 1 : 0, f.Closed); f.G.Random.RequireChoicesConsumed();
        }
    }

    private static void ResearchGrantPresentationFailure()
    {
        var f = new RetainedGrantFixture([RetainedGrantEmissary()]) { FailClose = true };
        var effects = new List<string>();
        var context = new OperationContext(f.World, f.G.Data.Process, f.G.Random, new PresentationRandom(1, "grant"),
            new(default, new(1)), default, (name, _) => effects.Add(name), () => { });
        using var operation = new ResearchGrantOperation(f.Begin(2, source: 0), c => new((ulong)c),
            _ => throw new InvalidOperationException("No intelligence request in this case"));
        Equal(CommandStatus.AwaitingInteraction, operation.Resume(context, new()).Status);
        var result = operation.Resume(context, new(OperationInputKind.Choice, 1));
        Equal(CommandStatus.Failed, result.Status); Equal("research_grant_presentation_failed", result.Code);
        Equal(new CommandValue.Boolean(true), result.Result!.Single().Value);
        Equal((byte)2, f.World.Globals.FirstDiscoverer[AdvanceIds.Monarchy]);
        Equal((byte)1, f.World.Civilizations[2].NumAdvances); Equal(1, f.Closed);
        Equal(false, f.World.Diplomacy.MeetingActive || f.World.Diplomacy.AskedEmissary);
        Equal(result, operation.Resume(context, new()));
        Sequence(new[] { "research.granted" }, effects); f.G.Random.RequireChoicesConsumed();
    }
}
