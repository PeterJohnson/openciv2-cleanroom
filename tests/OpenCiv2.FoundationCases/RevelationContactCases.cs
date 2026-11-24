using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // W03 §§3–5, V04 §10 and U02 common finish: actual contact at the revelation boundary.
    private static UnitRevelation ContactRevelation(ContactFixture f) => new(f.World, f.Data.Rules, f.Data.Stacks, f.Service, f.Data);
    private static void ContactNeighbors(ContactFixture f, bool city = false)
    {
        f.Data.Rules.Units[10] = f.Data.Rules.Units[0] with { Domain = RuleDomain.Land, Role = RuleRole.Attack, Abilities = new() { SeeTwo = true } };
        f.Data.AddUnit(10, 1, 10, new(20, 12));
        if (city) { f.Data.AddCity(5, 2, new(22, 12)); }
        else { f.Data.AddUnit(11, 2, 10, new(22, 12)); }
        f.Data.AddUnit(12, 2, 10, new(24, 12));
        f.World.Civilizations[1].Treaties[2].Contact = false;
    }
    private static void RevelationActualContactOrder()
    {
        foreach (bool city in new[] { false, true })
        {
            var f = new ContactFixture(); ContactNeighbors(f, city); var d = f.Data;
            using var op = ContactRevelation(f).Begin(10, true, "reveal");
            var pending = op.Resume(); Equal(true, pending is DiplomaticContactDecision.Audience { Value: DiplomaticAudienceDecision.Emissary });
            Equal(pending, op.Resume()); Equal(false, op.Completed);
            Equal(true, f.World.Map.TileAt(new(20, 14)).RevealedTo.Contains(1));
            Equal(true, d.Unit(10).Visibility.Contains(2)); Equal(1, d.PublishedOrigins.Count);
            Equal(false, d.Unit(12).Visibility.Contains(1));
            if (city) { Equal(true, f.World.Entities.Cities.Storage[5].KnownTo.Contains(1)); }
            else { Equal(true, d.Unit(11).Visibility.Contains(1)); Equal(0, d.Priority(2, new(22, 12), RuleRole.Defend)); }
            Equal(null, op.Resume(new(1))); Equal(true, op.Completed);
            Equal(true, d.Unit(12).Visibility.Contains(1)); Equal(true, f.World.Civilizations[1].Treaties[2].Contact);
            Equal(0, d.Meetings.Count); Equal(1, f.F.C.Closed); Equal(null, op.Resume(new(1)));
            if (!city) { Equal(2, d.Priority(2, new(22, 12), RuleRole.Defend)); }
            f.F.T.T.Random.RequireChoicesConsumed();
        }
    }
    private static void RevelationContactLiveReads()
    {
        var f = new ContactFixture(); ContactNeighbors(f); var d = f.Data;
        using var op = ContactRevelation(f).Begin(10, true, "reveal"); op.Resume();
        // Changes at an admitted player decision distinguish cached neighbor type/owner from live rule and treaty reads.
        d.Rules.Units[13] = d.Rules.Units[10] with { Role = RuleRole.Diplomacy };
        d.Unit(10).Type = new(13); d.Unit(10).Owner = new(4); d.Unit(11).Type = new(13);
        f.World.Civilizations[1].Treaties[2].Peace = false;
        op.Resume(new(1)); Equal(true, op.Completed);
        Equal(0, d.Priority(2, new(22, 12), RuleRole.Defend));
        Equal(3, d.Priority(1, new(22, 12), RuleRole.Attack));
        Equal(0, d.Priority(4, new(22, 12), RuleRole.Attack));
        Equal(true, d.Unit(12).Visibility.Contains(1)); Equal(false, d.Unit(12).Visibility.Contains(4));
        f.F.T.T.Random.RequireChoicesConsumed();
    }
    private static void RevelationContactAdmissionAndDisposal()
    {
        var f = new ContactFixture(); ContactNeighbors(f); var service = ContactRevelation(f);
        Throws<InvalidOperationException>(() => service.Reveal(10, true));
        Equal(false, f.World.Map.TileAt(new(20, 14)).RevealedTo.Contains(1));
        service.Reveal(10, false); Equal(true, f.Data.Unit(12).Visibility.Contains(1)); Equal(0, f.Data.Meetings.Count);
        var held = new ContactFixture(); ContactNeighbors(held);
        using var op = ContactRevelation(held).Begin(10, true, "reveal"); op.Resume(); op.Dispose();
        Throws<ObjectDisposedException>(() => op.Resume(new(1)));
        Equal(true, held.Data.Unit(11).Visibility.Contains(1)); Equal(false, held.Data.Unit(12).Visibility.Contains(1));
        Equal(false, held.World.Civilizations[1].Treaties[2].Contact);
        var denied = new ContactFixture(); ContactNeighbors(denied); denied.World.Events.Loaded = true;
        NegotiationEvent(denied.World, 1, 2); denied.F.FailEvent = true;
        using var failure = ContactRevelation(denied).Begin(10, true, "reveal");
        Throws<InvalidOperationException>(() => failure.Resume()); Throws<InvalidOperationException>(() => failure.Resume());
        Equal(true, denied.Data.Unit(11).Visibility.Contains(1)); Equal(false, denied.Data.Unit(12).Visibility.Contains(1));
    }
    private static void RevelationActualLibraryContact()
    {
        var f = new ContactFixture([ExchangeDraw(AdvanceIds.Monarchy, call: "reveal/adjacent-1/exchange"), ExchangeDraw(94, true, call: "reveal/adjacent-1/exchange")]);
        ContactNeighbors(f); f.Data.Unit(10).Owner = new(2); f.Data.Unit(11).Owner = new(3); f.Data.Unit(12).Owner = new(3);
        f.World.Map.TileAt(new(20, 12)).Owner = 2;
        f.World.Map.TileAt(new(22, 12)).Owner = f.World.Map.TileAt(new(24, 12)).Owner = 3;
        f.F.T.Know(3, AdvanceIds.Monarchy); f.F.T.Know(2, 94);
        f.Data.AddCity(5, 1, new(30, 12)); f.World.Civilizations[1].CityCount = 1; f.World.Globals.WonderCity[WonderIds.GreatLibrary] = 5;
        using var op = ContactRevelation(f).Begin(10, true, "reveal");
        Equal(true, op.Resume() is DiplomaticContactDecision.Grant { Value: ResearchGrantDecision.Government });
        Equal(true, f.World.Civilizations[1].Advances.Contains(AdvanceIds.Monarchy));
        Equal(false, f.World.Civilizations[3].Advances.Contains(94)); Equal(false, f.Data.Unit(12).Visibility.Contains(2));
        op.Resume(new(0)); Equal(true, op.Completed); Equal(true, f.World.Civilizations[3].Advances.Contains(94));
        Equal(true, f.World.Civilizations[2].Treaties[3].Peace); Equal(0, f.Data.Priority(2, new(22, 12), RuleRole.Attack));
        Equal(true, f.Data.Unit(12).Visibility.Contains(2)); f.F.T.T.Random.RequireChoicesConsumed();
    }
    private static DispatcherFixture ContactMovement(out ContactFixture contact, RevelationFixture? data = null)
    {
        ContactFixture? built = null;
        var f = new DispatcherFixture(data: data, revelation: d =>
        { built = new(data: d); return ContactRevelation(built); });
        contact = built!;
        f.Data.Unit(0).Type = new(4); f.Data.Rules.Units[4] = f.Data.Rules.Units[4] with { Domain = RuleDomain.Land, Role = RuleRole.Attack, Movement = 6 };
        f.Data.AddUnit(10, 2, 4, new(22, 12));
        f.World.Civilizations[1].Treaties[2].Contact = false; f.Data.Unit(0).LastDirection = 7;
        return f;
    }
    private static void MovementActualContactRetention()
    {
        foreach (bool dispose in new[] { false, true })
        {
            var f = ContactMovement(out var contact); using var op = f.Begin();
            Equal(true, op.Resume() is MovementDecision.Finish { Decision: MovementCompletionDecision.Contact });
            Equal(new MapPoint(20, 12), f.Data.Unit(0).Position); Equal((byte)3, f.Data.Unit(0).MovesUsed);
            Equal((byte)7, f.Data.Unit(0).LastDirection); Equal(0, f.Finish.OldPositions.Count); Equal(true, f.World.UnitMovementActive);
            if (dispose)
            { op.Dispose(); Throws<ObjectDisposedException>(() => op.Resume(new(1))); Equal(false, f.World.UnitMovementActive); Equal((byte)7, f.Data.Unit(0).LastDirection); }
            else
            { op.Resume(new(1)); f.Complete(op); Equal((byte)1, f.Data.Unit(0).LastDirection); Equal(1, f.Finish.OldPositions.Count); Equal(1, contact.F.C.Closed); }
            Equal((byte)3, f.Data.Unit(0).MovesUsed); Equal(0, f.Data.Meetings.Count); contact.F.T.T.Random.RequireChoicesConsumed();
        }
    }
    private static void MovementContactSessionPrivacy()
    {
        using var runtime = new SessionRuntime(new(), new(255));
        var f = ContactMovement(out var contact, CreationFixture(new(runtime.Process, runtime.World)));
        ParticipantId local = default;
        runtime.RegisterCommand(new(new(1, "test.contact-move", "Authored movement contact", AccessClass.Player, new([]), new([new("result", ValueKind.Integer)])),
            (_, _, _) => new(), _ => new MovementOperation(f.Begin(), _ => local,
                _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(),
                _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException())));
        runtime.BeginClientSession(new()); var actor = SessionClient(runtime, 2); var player = SessionClient(runtime, 1); local = player.Identity.Participant;
        var command = actor.Submit(new("test.contact-move", new([]))).Correlation.Command; runtime.AdvanceOne();
        var prompt = RequiredInteraction(player, command); Equal(false, actor.Reply(prompt, 1).Accepted); Equal(null, RequiredResult(actor, command).Interaction);
        Equal(new MapPoint(20, 12), f.Data.Unit(0).Position); Equal(true, f.World.UnitMovementActive);
        Equal(true, player.Reply(prompt, 1).Accepted); runtime.AdvanceOne();
        Equal(CommandStatus.Completed, RequiredResult(actor, command).Status); Equal(false, f.World.UnitMovementActive); Equal(null, player.Result(command));
        Equal((byte)1, f.Data.Unit(0).LastDirection); contact.F.T.T.Random.RequireChoicesConsumed();
    }
    private static void MovementContactSessionCancellation()
    {
        foreach (bool standalone in new[] { false, true }) foreach (var input in new[] { OperationInputKind.Cancel, OperationInputKind.Timeout })
        {
            var f = ContactMovement(out var contact); contact.F.C.FailClose = true;
            var context = new OperationContext(f.World, f.Data.Process, contact.F.T.T.Random,
                new PresentationRandom(1, "movement"), new(default, new(1)), default, (_, _) => { }, () => { });
            // The standalone completion starts at its supplied destination; the dispatcher reaches it by real relocation.
            if (standalone) { f.Finish.Step = f.Finish.Step with { To = new(20, 12) }; f.Data.Stacks.Relocate(0, new(20, 12), false); }
            using IContinuation op = standalone
                ? new MovementCompletionOperation(f.Finish.Begin(), c => new((ulong)c), _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException())
                : new MovementOperation(f.Begin(), c => new((ulong)c), _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException());
            Equal(CommandStatus.AwaitingInteraction, op.Resume(context, new()).Status);
            var result = op.Resume(context, new(input)); Equal(CommandStatus.Failed, result.Status); Equal("movement_presentation_failed", result.Code);
            Equal(result, op.Resume(context, new(input))); Equal(false, f.World.UnitMovementActive);
            Equal(true, f.World.Civilizations[1].Treaties[2].Contact); Equal((byte)1, f.Data.Unit(0).LastDirection); Equal(1, contact.F.C.Closed);
            contact.F.T.T.Random.RequireChoicesConsumed();
        }
    }
}
