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
    // V04 §11: composed stale-contact continuation with the actual §10/V05 children.
    private sealed class ContactTurnFixture : IDiplomacyTurnEffects, IRelationReviewEffects
    {
        public ContactFixture F { get; }
        public GameState World => F.World;
        public RevelationFixture Data => F.Data;
        public DiplomacyTurn Service { get; }
        public List<BorderNotice> Borders { get; } = [];
        public int WarEnds;
        public ContactTurnFixture(List<Choice>? extra = null, RevelationFixture? data = null)
        {
            List<Choice> choices =
            [
                Choice.Create(new("V04.turn-random", "turn"), new SamplingLaw.PrimaryModulo(100), ChoiceCode.Primary(42)),
                Choice.Create(new("V04.turn-senate", "turn"), new SamplingLaw.PrimaryModulo(3), ChoiceCode.Primary(1))
            ];
            choices.AddRange(extra ?? []); F = new(choices, data);
            World.Globals.Turn = 16; World.Globals.CivsAlive = CivilizationSet.Only(1).With(2).With(6);
            World.Globals.Status.AiCoordination = true; World.Civilizations[6].Government = Government.Republic;
            World.Civilizations[2].Treaties[1].Contact = false;
            World.Civilizations[2].Treaties[6] = World.Civilizations[6].Treaties[2] = new() { Contact = true, Peace = true };
            var war = War(Data, F.F.C.F.F.WarEffects, F.F.T.T.Random);
            var government = new GovernmentChanges(World, Data.Process.Rules, Data.Stacks,
                _ => throw new InvalidOperationException("Unexpected government selector"),
                _ => throw new InvalidOperationException("Unexpected rates selector"), _ => { });
            var review = new RelationReview(World, Data.Process.Rules, F.F.T.T.Random,
                new(World, Data.Rules, Data.Stacks), F.F.C.Stance, F.F.C.Emissaries, Data.Stacks, government, war, this);
            var allies = new WarAllyRequests(World, Data.Process.Rules, F.F.C.Emissaries, F.F.T.T.Research, war, F.RefreshStatus);
            Service = new(World, Data.Rules, F.F.T.T.Random, new(World, Data.Process.Rules, F.F.T.T.Random, F.F.T.T.Research),
                allies, review, F.Service, this);
        }
        public DiplomacyTurn.Operation Begin() => Service.Begin(2, "turn");
        public void Done(DiplomacyTurn.Operation op) { Equal(true, op.Completed); F.F.T.T.Random.RequireChoicesConsumed(); }
        public void Library()
        {
            F.F.T.Know(6, AdvanceIds.Monarchy); F.F.T.Know(2, 94);
            Data.AddCity(0, 1, new(10, 8)); World.Civilizations[1].CityCount = 1;
            World.Globals.WonderCity[WonderIds.GreatLibrary] = 0;
        }
        public void AllyHelp()
        {
            World.Civilizations[2].Government = World.Civilizations[6].Government = Government.Despotism;
            World.Civilizations[6].UnitsActive[UnitTypeIds.NuclearMissile] = 1;
            World.Civilizations[1].Treaties[2].Alliance = World.Civilizations[2].Treaties[1].Alliance = true;
            World.Diplomacy.EmissaryResponse = 0;
        }
        public void ExchangeAdvances(int from, int to) => throw new InvalidOperationException("Actual exchange must run");
        public void Meet(int from, int to) => throw new InvalidOperationException("Actual contact must run");
        public void ReviewRelation(int from, int human) => throw new InvalidOperationException("Actual review must run");
        public void SeekWarAlly(int from, int human) => throw new InvalidOperationException("Actual ally requests must run");
        public void WarEnded(int from, int to) => ++WarEnds;
        public void Notice(BorderNotice notice) => Borders.Add(notice);
        public void RedrawTile(MapPoint point) { }
        public void RedrawMaps(int observer) { }
    }
    private static List<Choice> TurnContactLibraryChoices() =>
    [
        ExchangeDraw(AdvanceIds.Monarchy, call: "turn/6/contact/exchange"),
        ExchangeDraw(94, true, call: "turn/6/contact/exchange")
    ];

    private static void DiplomacyContactActualAllianceAndTail()
    {
        var f = new ContactTurnFixture(); var w = f.World;
        f.F.Hostile(2, 4, true); f.F.Hostile(6, 4); w.Globals.PowerRank[4] = 5; w.Globals.RevealMap = 1;
        w.Civilizations[2].Treaties[6].ChangedThisTurn = true;
        using var op = f.Begin(); Equal(null, op.Resume()); f.Done(op);
        Equal(true, w.Civilizations[2].Treaties[6].Alliance && w.Civilizations[2].Treaties[6].Peace);
        Equal(false, w.Civilizations[2].Treaties[6].Contact || w.Civilizations[6].Treaties[2].Contact);
        Equal(false, w.Civilizations[2].Treaties[6].RecentContact); Equal(true, w.Civilizations[6].Treaties[2].RecentContact);
        Equal(true, w.Diplomacy.RecentPeace[6]); Equal(false, w.Civilizations[2].Treaties[6].ChangedThisTurn);
        Equal(true, w.Civilizations[6].Treaties[4].War); Equal(ContactNoticeKind.SignAllied, f.F.Notices.Single().Kind);
        Equal((byte)42, w.Civilizations[2].DiploRandom); Equal(0, f.Borders.Count); Equal(0, f.WarEnds);
    }

    private static void DiplomacyContactLibraryRetention()
    {
        foreach (bool dispose in new[] { false, true })
        {
            var f = new ContactTurnFixture(TurnContactLibraryChoices()); f.Library(); var w = f.World;
            w.Civilizations[2].Treaties[7].ChangedThisTurn = true;
            using var op = f.Begin();
            var pending = op.Resume(); Equal(true, pending is DiplomacyTurnDecision.Contact
            { Decision: DiplomaticContactDecision.Grant { Value: ResearchGrantDecision.Government } });
            Equal(pending, op.Resume()); Equal(true, w.Civilizations[2].Treaties[6].RecentContact);
            Equal(true, w.Civilizations[2].Treaties[7].ChangedThisTurn);
            Equal(true, w.Civilizations[1].Advances.Contains(AdvanceIds.Monarchy));
            Equal(false, w.Civilizations[6].Advances.Contains(94));
            if (dispose)
            {
                op.Dispose(); Throws<ObjectDisposedException>(() => op.Resume(0));
                Equal(true, w.Civilizations[2].Treaties[6].Contact && w.Civilizations[2].Treaties[7].ChangedThisTurn);
                Equal(false, w.Civilizations[6].Advances.Contains(94));
            }
            else
            {
                op.ResumeDecision(new(0)); f.Done(op); Equal(true, w.Civilizations[6].Advances.Contains(94));
                Equal(false, w.Civilizations[2].Treaties[6].Contact || w.Civilizations[2].Treaties[6].RecentContact);
                Equal(false, w.Civilizations[2].Treaties[7].ChangedThisTurn);
            }
            f.F.F.T.T.Random.RequireChoicesConsumed();
        }
    }

    private static void DiplomacyContactFailureAndUnknownReview()
    {
        var f = new ContactTurnFixture(); var w = f.World; w.Globals.RevealMap = 1;
        f.F.Hostile(2, 4, true); f.F.Hostile(6, 4); w.Globals.PowerRank[4] = 5;
        f.F.FailNotice = ContactNoticeKind.SignAllied; w.Civilizations[2].Treaties[7].ChangedThisTurn = true;
        using var op = f.Begin(); Throws<InvalidOperationException>(() => op.Resume()); Throws<InvalidOperationException>(() => op.Resume());
        Equal(true, w.Civilizations[2].Treaties[6].RecentContact && w.Civilizations[2].Treaties[6].Contact);
        Equal(false, w.Civilizations[2].Treaties[6].Alliance); Equal(false, w.Civilizations[6].Treaties[4].War);
        Equal(true, w.Civilizations[2].Treaties[7].ChangedThisTurn); f.F.F.T.T.Random.RequireChoicesConsumed();
        var unknown = new ContactTurnFixture(); w = unknown.World; w.Civilizations[2].Treaties[1].Contact = true;
        foreach (ref var unit in w.Entities.Units.Scanned) { if (unit.Token.Occupied) { unit.Owner = new(3); } }
        using var review = unknown.Begin(); Throws<InvalidOperationException>(() => review.Resume());
        // Contact completed and its stale tail ran before the unresolved A08 input was needed.
        Equal(false, w.Civilizations[2].Treaties[6].Contact); Equal(true, w.Civilizations[6].Treaties[2].RecentContact);
        Equal((byte)20, w.Civilizations[2].Attitude[1]); Equal(0, unknown.Borders.Count);
        unknown.F.F.T.T.Random.RequireChoicesConsumed();
    }

    private static void DiplomacyContactRetainedAllyHelp()
    {
        var f = new ContactTurnFixture([GreetingChoice("turn/6/contact/ally-help/emissary/greet")]); f.AllyHelp();
        var w = f.World; using var op = f.Begin();
        Equal(true, op.Resume() is DiplomacyTurnDecision.Contact { Decision: DiplomaticContactDecision.Ally { Value: AllyHelpDecision.Emissary } });
        Equal(true, w.Civilizations[2].Treaties[6].Peace && w.Civilizations[2].Treaties[6].Contact);
        Equal(true, op.Resume(0) is DiplomacyTurnDecision.Contact { Decision: DiplomaticContactDecision.Ally { Value: AllyHelpDecision.Demand } });
        op.Resume(1); f.Done(op);
        Equal(true, w.Civilizations[1].Treaties[6].War);
        // The enclosing stale-contact clear also clears the AI pair's newly set War.
        Equal(false, w.Civilizations[2].Treaties[6].War || w.Civilizations[2].Treaties[6].Contact);
        Equal(ContactNoticeKind.AllyMakesWar, f.F.Notices.Single().Kind); Equal(1, f.F.F.C.Closed);
    }

    private static void DiplomacyContactSessionPrivateGrant()
    {
        using var runtime = new SessionRuntime(new(), new(255));
        var f = new ContactTurnFixture(TurnContactLibraryChoices(), CreationFixture(new(runtime.Process, runtime.World))); f.Library();
        ParticipantId recipient = default;
        runtime.RegisterCommand(new(new(1, "test.turn-contact", "Authored diplomacy contact turn", AccessClass.Player, new([]),
            new([new("completed", ValueKind.Boolean)])), (_, _, _) => new(),
            _ => new DiplomacyTurnOperation(f.Begin(), _ => recipient, _ => throw new InvalidOperationException())));
        runtime.BeginClientSession(new()); var actor = SessionClient(runtime, 2); var player = SessionClient(runtime, 1); recipient = player.Identity.Participant;
        var command = actor.Submit(new("test.turn-contact", new([]))).Correlation.Command; runtime.AdvanceOne();
        var prompt = RequiredInteraction(player, command); Equal(false, actor.Reply(prompt, 0).Accepted);
        Equal(null, RequiredResult(actor, command).Interaction); Equal(true, f.World.Civilizations[2].Treaties[6].Contact);
        Equal(true, player.Reply(prompt, 0).Accepted); runtime.AdvanceOne();
        Equal(CommandStatus.Completed, RequiredResult(actor, command).Status); Equal(null, player.Result(command));
        Equal(false, f.World.Civilizations[2].Treaties[6].Contact); Equal(true, f.World.Civilizations[6].Advances.Contains(94));
        f.F.F.T.T.Random.RequireChoicesConsumed();
    }

    private static void DiplomacyContactSessionCancellation()
    {
        foreach (var input in new[] { OperationInputKind.Cancel, OperationInputKind.Timeout })
        {
            var f = new ContactTurnFixture(); f.AllyHelp(); f.F.F.C.FailClose = true;
            var context = new OperationContext(f.World, f.Data.Process, f.F.F.T.T.Random,
                new PresentationRandom(1, "turn"), new(default, new(2)), default, (_, _) => { }, () => { });
            using var op = new DiplomacyTurnOperation(f.Begin(), c => new((ulong)c), _ => throw new InvalidOperationException());
            Equal(CommandStatus.AwaitingInteraction, op.Resume(context, new()).Status);
            var result = op.Resume(context, new(input)); Equal(CommandStatus.Failed, result.Status);
            Equal("diplomacy_presentation_failed", result.Code); Equal(false, f.World.Civilizations[2].Treaties[6].Contact);
            Equal(false, f.World.Civilizations[1].Treaties[6].War); Equal(1, f.F.F.C.Closed);
            f.F.F.T.T.Random.RequireChoicesConsumed();
        }
    }
}
