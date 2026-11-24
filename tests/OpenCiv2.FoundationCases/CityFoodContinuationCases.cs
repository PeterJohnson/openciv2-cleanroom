using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // C05 famine, C01 removal and actual U01/V01/V06 children; no original-game captures.
    private static (AssessmentFixture F, IncidentFixture Incidents, GameplayRandom Restart,
        FoodEffects Effects, List<CityTarget> Removed, CityFood Food) RetainedFoodFixture(bool remove, RevelationFixture? data = null)
    {
        string call = remove ? "city-food/remove/unit/2/check" : "city-food/settler/2/unit/2/check";
        var (f, incidents, restart) = RetainedAssessmentFixture(true, data, call);
        f.Data.AddCity(0, 4, new(8, 10), remove ? 1 : 3);
        f.World.Civilizations[4].CityCount = 1;
        f.World.Entities.Cities.Storage[0].FoodBox = -7;
        f.World.Entities.Cities.Storage[0].KnownTo = CivilizationSet.Only(2);
        if (remove)
        {
            // No homed settler is selected by famine. C01 later destroys this supported
            // ship and its foreign unhomed last-settler cargo, pausing before city cleanup.
            f.Rules.Units[4] = f.Rules.Units[4] with { Domain = RuleDomain.Sea, Capacity = 1 };
            f.World.Map.TileAt(new(10, 10)).Terrain = Terrain.Ocean;
            f.Data.AddUnit(3, 4, 4, new(10, 10)); f.Data.Unit(3).Home = new(0);
            f.Data.Unit(3).Previous = null; f.Data.Unit(3).Next = new(2);
            f.Data.Unit(2).Previous = new(3); f.Data.Unit(2).Next = null;
        }
        else f.Data.Unit(2).Home = new(0);
        var effects = new FoodEffects(); var removed = new List<CityTarget>();
        var removal = new CityRemoval(f.World, f.Rules, f.Data.Stacks, f.Lifecycle.Loss, removed.Add);
        return (f, incidents, restart, effects, removed, new(f.World, f.Rules, removal, f.Lifecycle.Loss, f.Lifecycle, effects));
    }

    private static void FoodRetainedSettlerTail()
    {
        foreach (int reply in new[] { 0, 1 })
        {
            var (f, incidents, restart, effects, removed, food) = RetainedFoodFixture(false);
            using var op = food.Begin(new(0), f.Lifecycle.Elimination, incidents.Incidents, "city-food");
            var pending = op.ResumeDecision(); Equal(2, ((EspionagePretext)pending!).Victim.Civilization);
            Equal((bool?)null, op.Removed); Equal(false, f.Data.Unit(2).Token.Occupied);
            Equal((short)-7, f.World.Entities.Cities.Storage[0].FoodBox); Equal(0, effects.Redraws);
            Equal(FamineCause.SettlerSupport, effects.Famines.Single().Cause);
            int count = f.Lifecycle.Order.Count; Equal(pending, op.ResumeDecision()); Equal(count, f.Lifecycle.Order.Count);
            f.World.TurnLoopActive = false;
            Equal(null, op.ResumeDecision(new(reply))); Equal((bool?)false, op.Removed);
            Equal((sbyte)3, f.World.Entities.Cities.Storage[0].Size); Equal((short)0, f.World.Entities.Cities.Storage[0].FoodBox);
            Equal(1, effects.Redraws); Equal(0, removed.Count); Equal(0, f.Lifecycle.Checks);
            Equal(false, f.Lifecycle.Redraws.Contains(new(10, 10))); // Cargo deletion has no destroy/redraw wrapper.
            Equal(reply == 1, f.World.Civilizations[3].Treaties[2].War);
            Equal(null, op.ResumeDecision(new(1))); Equal(1, effects.Redraws);
            f.Random.RequireChoicesConsumed(); restart.RequireChoicesConsumed();
        }
    }

    private static void FoodRetainedCityRemovalTail()
    {
        var (f, incidents, restart, effects, removed, food) = RetainedFoodFixture(true);
        f.World.Globals.WonderCity[0] = 0;
        f.World.Civilizations[4].SpaceshipFlags.Exists = true;
        f.World.Globals.HumanPlayers = CivilizationSet.Only(2).With(4); f.World.Multiplayer.LocalHuman = 4;
        using var op = food.Begin(new(0), f.Lifecycle.Elimination, incidents.Incidents, "city-food");
        Equal(true, op.ResumeDecision() is EspionagePretext);
        Equal((sbyte)0, f.World.Entities.Cities.Storage[0].Size);
        Equal(true, f.World.Entities.Cities.Storage[0].Token.Occupied);
        Equal((short)0, f.World.Civilizations[4].CityCount); Equal((short)0, f.World.Globals.WonderCity[0]);
        Equal((bool?)null, op.Removed); Equal(0, removed.Count); Equal(0, effects.Redraws);
        // The captured owner still receives the final check after C01 finishes.
        f.World.Entities.Cities.Storage[0].Owner = new(3);
        Equal(null, op.ResumeDecision(new(0))); Equal((bool?)true, op.Removed);
        Equal(false, f.World.Entities.Cities.Storage[0].Token.Occupied);
        Equal((short)-2, f.World.Globals.WonderCity[0]); Equal(1, removed.Count);
        Equal((short)-7, f.World.Entities.Cities.Storage[0].FoodBox); Equal(0, effects.Redraws);
        Equal(false, f.World.Civilizations[4].SpaceshipFlags.Exists); Equal(EndReason.Defeat, f.World.PendingEndReason);
        Equal(3, f.Lifecycle.Restarts); Equal(0, f.Lifecycle.Checks); // Two human creation gates, one actual AI recreation.
        f.Random.RequireChoicesConsumed(); restart.RequireChoicesConsumed();
    }

    private static void FoodRetainedSearchAndFailure()
    {
        var f = new AssessmentFixture(); var world = f.World;
        world.Globals.CivsAlive = CivilizationSet.Only(1).With(2).With(3);
        f.Data.Process.Rules.Leaders[2].Color = 2; world.Map.TileAt(new(10, 8)).Terrain = Terrain.Ocean;
        f.Data.AddCity(0, 2, new(8, 8), 1); world.Civilizations[2].CityCount = 1;
        world.Entities.Cities.Storage[0].FoodBox = -3;
        var choices = CreationAttitudes(); CreationChoice(choices, "gender", 3, 1); CreationSiteChoices(choices, 2);
        choices[^2] = Choice.Create(new("F05.start-site.x", "create/site", 1), new SamplingLaw.PrimaryModulo(40), ChoiceCode.Primary(12));
        var random = CreationRandom(choices); f.Lifecycle.Recreate = owner => Creation(f.Data, owner, random);
        var incidents = new IncidentFixture(f.Random, f.Data); var effects = new FoodEffects();
        var removal = new CityRemoval(world, f.Rules, f.Data.Stacks, f.Lifecycle.Loss, _ => { });
        var food = new CityFood(world, f.Rules, removal, f.Lifecycle.Loss, f.Lifecycle, effects);
        using var op = food.Begin(new(0), f.Lifecycle.Elimination, incidents.Incidents, "search", 1);
        Equal(null, op.ResumeDecision()); Equal(false, op.Completed); Equal((bool?)null, op.Removed);
        Equal(false, world.Entities.Cities.Storage[0].Token.Occupied); Equal(1, f.Lifecycle.Restarts);
        Equal(null, op.ResumeDecision()); Equal((bool?)true, op.Removed);
        Equal(new MapPoint(12, 8), f.Data.Unit(0).Position); Equal(0, effects.Redraws);
        random.RequireChoicesConsumed(); f.Random.RequireChoicesConsumed();

        var failed = new EliminationFixture(data: FoodFixture()); failed.World.Globals.HumanPlayers = default;
        failed.World.Entities.Cities.Storage[0].Size = 1; failed.World.Entities.Cities.Storage[0].FoodBox = -4;
        var failureEffects = new FoodEffects(); var failureIncidents = new IncidentFixture(failed.Random, failed.Data);
        var failing = new CityFood(failed.World, failed.Data.Rules,
            new(failed.World, failed.Data.Rules, failed.Data.Stacks, failed.Loss, _ => { }), failed.Loss, failed, failureEffects);
        using var fault = failing.Begin(new(0), failed.Elimination, failureIncidents.Incidents, "failure");
        Throws<InvalidOperationException>(() => fault.ResumeDecision()); // Missing required recreation provider.
        Equal(false, failed.World.Entities.Cities.Storage[0].Token.Occupied); Equal((bool?)null, fault.Removed);
        Equal((short)-4, failed.World.Entities.Cities.Storage[0].FoodBox); Equal(0, failureEffects.Redraws);
        Throws<InvalidOperationException>(() => fault.ResumeDecision()); Equal(1, failed.Restarts);
    }

    private static void FoodRetainedCancelAndDispose()
    {
        foreach (var kind in new[] { OperationInputKind.Cancel, OperationInputKind.Timeout })
        {
            var (f, incidents, restart, effects, _, food) = RetainedFoodFixture(false);
            var context = OutcomeContext(f.Data);
            using var op = new CityFoodOperation(food.Begin(new(0), f.Lifecycle.Elimination, incidents.Incidents, "city-food"), c => new((ulong)c));
            Throws<InvalidOperationException>(() => _ = op.Removed);
            Equal(new ParticipantId(2), op.Resume(context, new()).Interaction!.Recipient!.Value);
            var result = op.Resume(context, new(kind)); Equal(CommandStatus.Completed, result.Status); Equal(false, op.Removed);
            Equal(false, f.World.Civilizations[3].Treaties[2].War); Equal(1, effects.Redraws);
            Equal(result, op.Resume(context, new())); Equal(1, effects.Redraws);
            f.Random.RequireChoicesConsumed(); restart.RequireChoicesConsumed();
        }
        foreach (bool remove in new[] { false, true })
        {
            var (f, incidents, _, effects, removed, food) = RetainedFoodFixture(remove);
            using var op = food.Begin(new(0), f.Lifecycle.Elimination, incidents.Incidents, "city-food");
            Equal(true, op.ResumeDecision() is EspionagePretext); op.Dispose(); op.Dispose();
            Throws<ObjectDisposedException>(() => op.ResumeDecision(new(0)));
            Equal((bool?)null, op.Removed); Equal(0, effects.Redraws); Equal(0, removed.Count);
            Equal((short)-7, f.World.Entities.Cities.Storage[0].FoodBox);
            Equal(true, f.World.Entities.Cities.Storage[0].Token.Occupied); Equal(0, f.Lifecycle.Restarts);
        }
    }

    private static void FoodRetainedPrivateSession()
    {
        using var runtime = new SessionRuntime(new(), new(266));
        var (f, incidents, restart, effects, _, food) = RetainedFoodFixture(false, CreationFixture(new(runtime.Process, runtime.World)));
        ParticipantId recipient = default;
        runtime.RegisterCommand(new(new(1, "test.city-food", "City food", AccessClass.Player, new([]), new([])),
            (_, _, _) => new(), _ => new CityFoodOperation(food.Begin(new(0), f.Lifecycle.Elimination,
                incidents.Incidents, "city-food"), _ => recipient)));
        runtime.BeginClientSession(new()); var actor = SessionClient(runtime, 4); var human = SessionClient(runtime, 2); recipient = human.Identity.Participant;
        var command = actor.Submit(new("test.city-food", new([]))).Correlation.Command; runtime.AdvanceOne();
        var pending = RequiredInteraction(human, command); Equal(null, RequiredResult(actor, command).Interaction);
        Equal(0, effects.Redraws); Equal(false, actor.Reply(pending, 1).Accepted);
        Equal(true, human.Reply(pending, 1).Accepted); runtime.AdvanceOne();
        Equal(CommandStatus.Completed, RequiredResult(actor, command).Status); Equal(false, human.Reply(pending, 1).Accepted);
        Equal(true, f.World.Civilizations[3].Treaties[2].War); Equal(1, effects.Redraws);
        Equal(1, RequiredResult(actor, command).Committed.Count(e => e.Name == "city.food.completed"));
        f.Random.RequireChoicesConsumed(); restart.RequireChoicesConsumed();
    }
}
