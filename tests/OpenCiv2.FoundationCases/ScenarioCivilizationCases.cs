using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // S02 §9, with actual V04 force-war and retained V03 grants. Specification expectations.
    private static ScenarioCivilizationActions CivilizationActions(RetainedGrantFixture f, WarNotices? notices = null) =>
        new(f.World, War(f.G.Data, notices ?? new()), f.Grants);
    private static Event TechnologyEvent(RetainedGrantFixture f, int recipient = 2, int advance = AdvanceIds.Monarchy)
    {
        var entry = f.World.Events.Collection.Append(); entry.Values.GiveTechCiv = recipient; entry.Values.GiveTech = advance;
        return entry;
    }
    private static void ScenarioAggressionAdmission()
    {
        foreach (var (who, whom, resolvedWho, resolvedWhom, accepted) in new[]
        {
            (1,2,1,2,true), (-3,-4,1,2,true), (-4,-3,2,1,true), (2,2,2,2,true),
            (0,2,0,2,false), (1,8,1,8,false), (-2,2,-2,2,false), (1,3,1,3,false)
        })
        {
            var f = new RetainedGrantFixture(); var w = f.World; var entry = TechnologyEvent(f);
            entry.Values.WhoCiv = 1; entry.Values.WhomCiv = 2;
            entry.Values.AggressionWhoCiv = who; entry.Values.AggressionWhomCiv = whom;
            w.Civilizations[1].Treaties[2].HostileIntent = true;
            if (accepted) { w.Civilizations[resolvedWho].Treaties[resolvedWhom].Contact = true; }
            Equal(accepted ? 1 : 0, CivilizationActions(f).MakeAggression(entry));
            if (accepted)
            { Equal(true, w.Civilizations[resolvedWho].Treaties[resolvedWhom].War); Equal((byte)100, w.Civilizations[resolvedWho].Attitude[resolvedWhom]); }
            else { Equal(true, w.Civilizations[1].Treaties[2].HostileIntent); }
            f.G.Random.RequireChoicesConsumed();
        }
        var single = new RetainedGrantFixture(); var indirect = TechnologyEvent(single);
        indirect.Values.AggressionWhoCiv = -3; indirect.Values.WhoCiv = -4; indirect.Values.WhomCiv = 1; indirect.Values.AggressionWhomCiv = 2;
        Equal(0, CivilizationActions(single).MakeAggression(indirect));
        single.World.Globals.Multiplayer = GameMode.Tcp;
        Throws<InvalidOperationException>(() => CivilizationActions(single).MakeAggression(indirect));
    }
    private static void ScenarioAggressionActualEffects()
    {
        var f = new RetainedGrantFixture(); var d = f.G.Data; var w = f.World; var notices = new WarNotices();
        var service = CivilizationActions(f, notices); var entry = TechnologyEvent(f);
        entry.Values.AggressionWhoCiv = 1; entry.Values.AggressionWhomCiv = 2;
        d.AddCity(0, 1, new(4, 8)); d.AddCity(1, 2, new(12, 8)); d.AddUnit(0, 1, 0, new(10, 8));
        w.Map.TileAt(new(10, 8)).Region = 1; w.Civilizations[2].ContinentCities[1] = 1;
        DiplomaticRelations.Set(w, 1, 2, new() { Contact = true, Alliance = true });
        Equal(1, service.MakeAggression(entry)); Equal(new MapPoint(4, 8), d.Unit(0).Position);
        Equal(true, w.Civilizations[1].Treaties[2].War); Equal(true, w.Civilizations[2].Treaties[1].Vendetta);
        Equal(false, w.Civilizations[1].Treaties[2].Alliance); Equal(1, notices.Redraws.Count);
        Equal(DiplomaticWarNoticeKind.AllianceCancelled, notices.Notices.Single().Kind); Equal(0, service.MakeAggression(entry));
        // A zero child result still commits its effects and does not skip the next enabled action.
        f = new(); w = f.World; service = CivilizationActions(f); entry = TechnologyEvent(f);
        entry.Values.AggressionWhoCiv = 1; entry.Values.AggressionWhomCiv = 2;
        entry.Values.Actions = new() { MakeAggression = true, ChangeMoney = true, JustOnce = true };
        entry.Values.MoneyReceiverCiv = 1; entry.Values.MoneyAmount = 31;
        w.Civilizations[1].Treaties[2] = new() { Peace = true, HostileIntent = true };
        w.Civilizations[2].Treaties[1] = default; w.Globals.HumanPlayers = default;
        Equal(1, ScenarioActions.Execute(entry, (action, current) => action == ScenarioAction.MakeAggression
            ? service.MakeAggression(current) : ScenarioActions.ChangeMoney(w, current)));
        Equal(false, w.Civilizations[1].Treaties[2].Peace); Equal(false, w.Civilizations[1].Treaties[2].HostileIntent);
        Equal(false, w.Civilizations[1].Treaties[2].War); Equal(31, w.Civilizations[1].Gold); Equal(true, entry.Values.Actions.Consumed);
        f.G.Random.RequireChoicesConsumed();
    }
    private static void ScenarioTechnologyAdmissionAndStorage()
    {
        foreach (int selector in new[] { 0, 8, -2, 3, -3, -4 })
        {
            var f = new RetainedGrantFixture(); var entry = TechnologyEvent(f, selector);
            entry.Values.WhoCiv = -4; entry.Values.WhomCiv = 1;
            using var op = CivilizationActions(f).BeginGrant(entry, "scenario"); Equal(null, op.Resume());
            Equal(selector == -4 ? 1 : 0, op.Result); Equal(selector == -4, f.World.Civilizations[1].Advances.Contains(AdvanceIds.Monarchy));
            f.G.Random.RequireChoicesConsumed();
        }
        var zero = new RetainedGrantFixture(); zero.World.Globals.Turn = 0;
        var repeated = TechnologyEvent(zero, -3, 54); repeated.Values.WhoCiv = 1;
        zero.World.Civilizations[1].Researching = 54; zero.World.Civilizations[1].AdvanceSource[54] = 7;
        for (int i = 0; i < 2; ++i)
        { using var op = CivilizationActions(zero).BeginGrant(repeated, "zero"); Equal(null, op.Resume()); Equal(1, op.Result); }
        Equal((short)-1, zero.World.Civilizations[1].Researching); Equal((byte)0, zero.World.Civilizations[1].AdvanceSource[54]);
        Equal((byte)0, zero.World.Civilizations[1].NumAdvances); Equal(0, zero.G.Order.Count);
        foreach (int future in new[] { CivilizationKnowledge.FutureTechnology, 100, int.MaxValue })
        {
            repeated.Values.GiveTech = future;
            using var op = CivilizationActions(zero).BeginGrant(repeated, "future"); Equal(null, op.Resume()); Equal(1, op.Result);
        }
        Equal((byte)3, zero.World.Civilizations[1].FutureTechs); zero.G.Random.RequireChoicesConsumed();
    }
    private static void ScenarioTechnologySourceAndDuplicates()
    {
        var f = new RetainedGrantFixture(); var w = f.World; var entry = TechnologyEvent(f, 1);
        w.Globals.GameOptions.AdvanceCivilopedia = true; w.Globals.GameOptions.Tutorial = true;
        w.Events.Loaded = true;
        var received = w.Events.Collection.Append(); received.Values.Trigger = EventTrigger.ReceivedTechnology;
        received.Values.Technology = AdvanceIds.Monarchy; received.Values.Actions.ChangeMoney = true;
        received.Values.MoneyReceiverCiv = 1; received.Values.MoneyAmount = 100;
        for (int i = 0; i < 2; ++i)
        {
            using var op = CivilizationActions(f).BeginGrant(entry, "source"); Equal(null, op.Resume()); Equal(1, op.Result);
            Equal((byte)(i + 1), w.Civilizations[1].NumAdvances); Equal(null, op.Resume(1)); Equal(1, op.Result);
        }
        Equal((byte)0, w.Civilizations[1].AdvanceSource[54]); Equal((byte)1, w.Globals.FirstDiscoverer[54]);
        Equal(0, f.G.Acquisitions.Count); Equal(0, f.G.Tutorials.Count); Equal(0, f.G.GovernmentChoices.Count);
        Equal(false, f.G.Order.Any(x => x.StartsWith("pedia:") || x.StartsWith("revolution:")));
        Equal(Government.Despotism, w.Civilizations[1].Government); Equal(0, w.Civilizations[1].Gold);
        f.G.Random.RequireChoicesConsumed();
    }
    private static void ScenarioLibrary(RetainedGrantFixture f)
    {
        for (int terrain = 0; terrain < f.G.Rules.Terrain.Length; ++terrain)
        {
            f.G.Rules.Terrain[terrain] = f.G.Rules.Terrain[terrain] with
            { Food = 2, Shields = 1, Trade = 1, Work = new(new(TerrainResult.ImproveInPlace, 1), new(TerrainResult.ImproveInPlace, 2)) };
        }
        f.G.Data.AddCity(0, 1, new(10, 8)); f.World.Civilizations[1].CityCount = 1;
        f.World.Globals.WonderCity[WonderIds.GreatLibrary] = 0;
        f.World.Civilizations[3].Advances = f.World.Civilizations[3].Advances.With(54);
        f.World.Globals.AdvanceKnownBy[54] = CivilizationSet.Only(3);
    }
    private static void ScenarioTechnologyRetainedLibrary()
    {
        var f = new RetainedGrantFixture(); ScenarioLibrary(f); var entry = TechnologyEvent(f);
        using var op = CivilizationActions(f).BeginGrant(entry, "scenario");
        Equal(true, op.Resume() is ResearchGrantDecision.Government { Choice.Civilization: 1 });
        Equal(null, op.Result); Equal((byte)1, f.World.Civilizations[2].NumAdvances); Equal((byte)254, f.World.Civilizations[1].AdvanceSource[54]);
        entry.Values.GiveTechCiv = 1; entry.Values.GiveTech = 55;
        Equal(op.Pending, op.Resume()); Equal(null, op.Resume(1)); Equal(1, op.Result);
        Equal(Government.Anarchy, f.World.Civilizations[1].Government); Equal(false, f.World.Civilizations[1].Advances.Contains(55));
        Equal((byte)1, f.World.Civilizations[1].NumAdvances); Equal((byte)1, f.World.Civilizations[2].NumAdvances);
        f.G.Random.RequireChoicesConsumed();
    }
    private static Choice ScenarioEmissary() => Choice.Create(new("V03.emissary", "scenario/grant"), new SamplingLaw.SecondaryRange(0, 2), ChoiceCode.Secondary(0));
    private static void ScenarioTechnologyFailureAndDisposal()
    {
        var f = new RetainedGrantFixture(); ScenarioLibrary(f);
        using var disposed = CivilizationActions(f).BeginGrant(TechnologyEvent(f), "scenario"); disposed.Resume(); disposed.Dispose();
        Throws<ObjectDisposedException>(() => disposed.Resume(1)); Equal((byte)1, f.World.Civilizations[2].NumAdvances);
        Equal(Government.Despotism, f.World.Civilizations[1].Government);
        f = new() { FailRevolution = true }; ScenarioLibrary(f);
        using var failed = CivilizationActions(f).BeginGrant(TechnologyEvent(f), "scenario"); failed.Resume();
        Throws<InvalidOperationException>(() => failed.Resume(1)); Throws<InvalidOperationException>(() => failed.Resume(0));
        Equal(null, failed.Result); Equal((byte)1, f.World.Civilizations[1].NumAdvances);
        f = new(); f.World.Civilizations[1].Researching = -2;
        using var invalid = CivilizationActions(f).BeginGrant(TechnologyEvent(f, 1, -2), "invalid");
        Throws<ArgumentOutOfRangeException>(() => invalid.Resume()); Equal((short)-1, f.World.Civilizations[1].Researching);
        Throws<InvalidOperationException>(() => invalid.Resume());
        f = new(); f.World.Globals.Multiplayer = GameMode.Tcp;
        using var remote = CivilizationActions(f).BeginGrant(TechnologyEvent(f, 1), "remote");
        Throws<InvalidOperationException>(() => remote.Resume()); Equal((byte)0, f.World.Civilizations[1].NumAdvances);
    }
    private static void ScenarioTechnologyPrivateSession()
    {
        foreach (bool emissary in new[] { false, true })
        {
            using var runtime = new SessionRuntime(new(), new(255));
            var f = new RetainedGrantFixture(emissary ? [ScenarioEmissary()] : [], new(runtime.Process, runtime.World));
            if (!emissary) { ScenarioLibrary(f); }
            var entry = TechnologyEvent(f); ParticipantId recipient = default;
            runtime.RegisterCommand(new(new(1, "test.scenario-technology", "Scenario technology", AccessClass.Player, new([]), new([new("result", ValueKind.Integer)])),
                (_, _, _) => new(), _ => new ScenarioTechnologyOperation(CivilizationActions(f).BeginGrant(entry, "scenario"), _ => recipient)));
            runtime.BeginClientSession(new()); var actor = SessionClient(runtime, 2); var human = SessionClient(runtime, 1); recipient = human.Identity.Participant;
            var command = actor.Submit(new("test.scenario-technology", new([]))).Correlation.Command; runtime.AdvanceOne();
            var prompt = RequiredInteraction(human, command); Equal(false, actor.Reply(prompt, 1).Accepted); Equal(null, RequiredResult(actor, command).Interaction);
            Equal(true, human.Reply(prompt, 1).Accepted); runtime.AdvanceOne(); Equal(CommandStatus.Completed, RequiredResult(actor, command).Status);
            Equal(new CommandValue.Integer(1), RequiredResult(actor, command).Result.Single().Value);
            Equal(false, human.Reply(prompt, 1).Accepted); Equal(null, human.Result(command));
            Equal(emissary ? Government.Despotism : Government.Anarchy, f.World.Civilizations[1].Government); f.G.Random.RequireChoicesConsumed();
        }
        foreach (var input in new[] { OperationInputKind.Cancel, OperationInputKind.Timeout })
        {
            var f = new RetainedGrantFixture([ScenarioEmissary()]) { FailClose = true };
            var context = new OperationContext(f.World, f.G.Data.Process, f.G.Random, new PresentationRandom(1, "scenario"), new(default, new(2)), default, (_, _) => { }, () => { });
            using var adapter = new ScenarioTechnologyOperation(CivilizationActions(f).BeginGrant(TechnologyEvent(f), "scenario"), c => new((ulong)c));
            Equal(CommandStatus.AwaitingInteraction, adapter.Resume(context, new()).Status);
            var result = adapter.Resume(context, new(input)); Equal(CommandStatus.Failed, result.Status);
            Equal("scenario_technology_presentation_failed", result.Code); Equal(result, adapter.Resume(context, new(input)));
            Equal((byte)1, f.World.Civilizations[2].NumAdvances); Equal(1, f.Closed); Equal(false, f.G.Order.Contains("gape:54")); f.G.Random.RequireChoicesConsumed();
        }
    }
}
