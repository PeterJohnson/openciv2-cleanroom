using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;
using OpenCiv2.Formats;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // V01/V06/S02 production lifecycle joins. The same V03/V04 services own recursive decisions.
    private sealed class SinglePlayerLifecycleFixture
    {
        public SinglePlayerDiplomacyFixture Diplomacy { get; }
        public RevelationFixture Data => Diplomacy.Data;
        public GameState World => Data.World;
        public EliminationFixture Life => Diplomacy.Events.Life;
        public GameplayRandom Random => Diplomacy.Grant.G.Random;
        public SinglePlayerLifecycle Services { get; }
        public List<EspionageIncidentNotice> Incidents { get; } = [];
        public List<CityTarget> Removed { get; } = [];
        public RestartWarEffects War { get; } = new();
        public SinglePlayerLifecycleFixture(List<Choice>? choices = null, RevelationFixture? data = null,
            SinglePlayerLifecycleEffects? presentation = null)
        {
            Diplomacy = new(choices, CreationFixture(data));
            Services = new(World, Data.Process, Random, Data.Stacks, new(World, Data.Rules, Data.Stacks), Life.Deletion,
                new(), Diplomacy.Grant.Setter, Diplomacy.Grant.Services, Diplomacy.Services, Diplomacy.Events.Permissions,
                new ScenarioTerrainEncoding(World.Map, new(World.Map)), Diplomacy.Events.Presentation,
                presentation ?? new(Data, Data, Life, Life, Life, War, Incidents.Add, Removed.Add, Life.RedrawTile, Life.RedrawMaps));
            Diplomacy.Events.UseDispatcher(Services.Events, Services.Triggers);
        }
        public void Restart(int owner = 2)
        {
            World.Globals.Turn = 25; World.Globals.Difficulty = Difficulty.Prince;
            World.Globals.HumanPlayers = CivilizationSet.Only(1).With(4);
            World.Globals.CivsAlive = CivilizationSet.Only(1).With(owner).With(3);
            World.Multiplayer.LocalHuman = 1;
            for (int i = 0; i < World.Entities.Cities.End; ++i)
            {
                ref var city = ref World.Entities.Cities.Storage[i];
                if (city.Token.Occupied && city.Owner.Value == owner)
                { city.Owner = new(3); World.Map.TileAt(city.Position).Owner = 3; }
            }
            Data.AddCity(0, 1, new(30, 20)); World.Civilizations[1].CityCount = 1;
            World.Civilizations[owner].CityCount = 0;
            World.Globals.WonderCity[WonderIds.GreatLibrary] = 0;
            World.Civilizations[1].Government = Government.Despotism;
            for (int c = 0; c < EntityLimits.Civilizations; ++c) World.Civilizations[c].Advances = default;
            World.Globals.AdvanceKnownBy = default; World.Globals.FirstDiscoverer = default;
            World.Civilizations[4].Advances = default(AdvanceSet).With(AdvanceIds.Monarchy);
            World.Globals.AdvanceKnownBy[AdvanceIds.Monarchy] = CivilizationSet.Only(4);
            World.Globals.FirstDiscoverer[AdvanceIds.Monarchy] = 7;
            for (int i = 0; i < Data.Rules.Terrain.Length; ++i)
            {
                Data.Rules.Terrain[i] = Data.Rules.Terrain[i] with
                { Food = 2, Shields = 1, Trade = 1, Work = new(new(TerrainResult.ImproveInPlace, 1), new(TerrainResult.ImproveInPlace, 2)) };
            }
        }
        public void Consumed() { Random.RequireChoicesConsumed(); Life.Random.RequireChoicesConsumed(); }
    }
    private static List<Choice> LifecycleCreationChoices(string call)
    {
        var choices = CreationAttitudes(); CreationChoice(choices, "catch-up", 3, 1, AdvanceIds.Monarchy);
        return choices.Select(c => c with { Key = c.Key with { Call = call } }).ToList();
    }

    private static void SinglePlayerLifecycleScenarioCycle()
    {
        foreach (bool dispose in new[] { false, true })
        {
            var f = new SinglePlayerLifecycleFixture(); var entry = ScenarioNestedCreation(f.Diplomacy.Events);
            int gold = f.World.Civilizations[1].Gold;
            using var operation = f.Services.Events.Begin(entry, "scenario"); var pending = operation.Resume();
            Equal(true, pending is LifecycleContact { Decision: DiplomaticContactDecision.Scenario { Value: ResearchGrantDecision.Government } });
            int unit = f.Data.Stacks.FirstAt(new(20, 12)); var token = f.Data.Unit(unit).Token;
            Equal(pending, operation.Resume()); Equal(gold, f.World.Civilizations[1].Gold);
            if (dispose)
            { operation.Dispose(); Throws<ObjectDisposedException>(() => operation.Resume(new(0))); Equal(false, entry.Values.Actions.Consumed); }
            else
            {
                Equal(true, operation.Resume(new(0)) is ScenarioText); Equal(gold + 7, f.World.Civilizations[1].Gold);
                Equal(null, operation.Resume(new(0))); Equal(1, operation.Result); Equal(true, entry.Values.Actions.Consumed);
            }
            Equal(token, f.Data.Unit(unit).Token); f.Consumed();
        }
    }

    private static void SinglePlayerLifecycleContextualRecreation()
    {
        foreach (bool removal in new[] { false, true })
        {
            string call = removal ? "retire" : "eliminate";
            var f = new SinglePlayerLifecycleFixture(LifecycleCreationChoices(call + "/creation")); f.Restart();
            if (removal)
            {
                using var operation = f.Services.Civilizations.Begin(2, 1, 1, call);
                Equal(true, operation.Resume() is ResearchGrantDecision.Government { Choice.Civilization: 1 });
                Equal(false, operation.Completed); operation.Resume(new(0)); Equal(true, operation.Completed);
            }
            else
            {
                using var operation = f.Services.Elimination.Begin(2, -1, call, f.Services.Incidents);
                Equal(true, operation.ResumeDecision() is ResearchGrantDecision.Government { Choice.Civilization: 1 });
                Equal(null, operation.Result); operation.ResumeDecision(new(0)); Equal(1, operation.Result);
            }
            Equal(true, f.World.Civilizations[2].Advances.Contains(AdvanceIds.Monarchy));
            Equal(true, f.World.Civilizations[1].Advances.Contains(AdvanceIds.Monarchy));
            Equal(Government.Despotism, f.World.Civilizations[1].Government); f.Consumed();
        }
        var war = new SinglePlayerLifecycleFixture(LifecycleCreationChoices("split/creation")); war.Restart(7);
        for (int c = 1; c < 7; ++c) war.World.Civilizations[c].CityCount = 1;
        war.Data.AddCity(1, 2, new(4, 4), 9); war.World.Civilizations[2].TotalCitySize = 9; war.World.Civilizations[2].Gold = 101;
        BuildingQueries.SetBuilding(war.World, new(1), BuildingIds.Palace, true);
        using var schism = war.Services.CivilWar.Begin(2, "split");
        Equal(null, schism.AdvanceDecision()); Equal(true, schism.Pending is ResearchGrantDecision.Government);
        Equal(101, war.World.Civilizations[2].Gold); Equal(1, schism.AdvanceDecision(answer: new(0)));
        Equal(51, war.World.Civilizations[2].Gold); Equal(50, war.World.Civilizations[7].Gold); Equal(1, war.War.Notices); war.Consumed();
    }

    private static void SinglePlayerLifecyclePrivateDeletion()
    {
        foreach (bool cancel in new[] { false, true })
        {
            using var runtime = new SessionRuntime(new(), new(cancel ? 269UL : 268UL));
            var f = new SinglePlayerLifecycleFixture(LifecycleCreationChoices("delete/unit/0/check/creation"), new(runtime.Process, runtime.World));
            f.Restart(); f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Role = RuleRole.Settle };
            f.Data.AddUnit(0, 2, 0, new(12, 8)); f.World.Civilizations[2].UnitsActive[0] = 1;
            ParticipantId human = default;
            runtime.RegisterCommand(new(new(1, "test.lifecycle-delete", "Retained deletion and catch-up", AccessClass.Player, new([]), new([new("result", ValueKind.Integer)])),
                (_, _, _) => new(), _ => new CivilizationEliminationOperation(f.Services.Elimination.BeginDelete(0, true, "delete", f.Services.Incidents), _ => human)));
            runtime.BeginClientSession(new()); var actor = SessionClient(runtime, 3); var witness = SessionClient(runtime, 1); human = witness.Identity.Participant;
            var command = actor.Submit(new("test.lifecycle-delete", new([]))).Correlation.Command; runtime.AdvanceOne();
            var prompt = RequiredInteraction(witness, command); Equal(false, actor.Reply(prompt, 0).Accepted);
            Equal(null, RequiredResult(actor, command).Interaction); Equal(false, f.Data.Unit(0).Token.Occupied);
            Equal(true, f.World.UnitDeletion.Active); Equal(0, f.Life.MapRefreshes);
            if (cancel) Equal(true, actor.Cancel(command).Accepted); else Equal(true, witness.Reply(prompt, 1).Accepted);
            runtime.AdvanceOne(); Equal(CommandStatus.Completed, RequiredResult(actor, command).Status);
            Equal(cancel ? Government.Despotism : Government.Anarchy, f.World.Civilizations[1].Government);
            Equal(false, f.World.UnitDeletion.Active); Equal(1, f.Life.MapRefreshes); Equal(false, witness.Reply(prompt, 0).Accepted); f.Consumed();
        }
    }

    private static void SinglePlayerLifecycleTerrainAndIncidents()
    {
        var f = new SinglePlayerLifecycleFixture(LifecycleCreationChoices("terrain/cityless/2/creation")); f.Restart();
        for (int c = 0; c < EntityLimits.Civilizations; ++c)
        { f.Data.AddCity(c, c, new(32, 2 + 2 * c)); f.World.Civilizations[c].CityCount = 1; }
        f.World.Entities.Cities.Storage[2].Token = default; f.World.Civilizations[2].CityCount = 0;
        f.World.Globals.WonderCity[WonderIds.GreatLibrary] = 1;
        var entry = f.World.Events.Collection.Append();
        entry.Values.TerrainRectangle[0] = new(2, 0); entry.Values.TerrainRectangle[1] = new(0, 0);
        using (var operation = f.Services.Terrain.Begin(entry, "terrain"))
        {
            Equal(true, operation.Resume() is ResearchGrantDecision.Government { Choice.Civilization: 1 });
            Equal(0, f.Life.MapRefreshes); Equal(null, operation.Result);
            operation.Resume(new(0)); Equal(0, operation.Result); Equal(1, f.Life.MapRefreshes); f.Consumed();
        }
        f = new(); f.World.Globals.HumanPlayers = CivilizationSet.Only(2);
        DiplomaticRelations.Set(f.World, 1, 2, new() { Peace = true });
        var incident = f.Services.Incidents.Begin(1, 2, "incident"); var prompt = incident.Resume();
        Equal(2, prompt!.Victim.Civilization); Equal(prompt, incident.Resume());
        Equal(false, f.World.Civilizations[2].Treaties[1].War);
        incident.Resume(1); Equal(true, incident.Completed); Equal(true, f.World.Civilizations[2].Treaties[1].War);
        Equal(f.World.Globals.Turn, f.World.Civilizations[2].LastContactTurn[1]); f.Consumed();
    }

    private static void SinglePlayerLifecycleCreationFailureAndPlacement()
    {
        foreach (bool dispose in new[] { false, true })
        {
            var f = new SinglePlayerLifecycleFixture(LifecycleCreationChoices("failed/creation")); f.Restart();
            using var operation = f.Services.Elimination.Begin(2, -1, "failed", f.Services.Incidents);
            Equal(true, operation.ResumeDecision() is ResearchGrantDecision.Government);
            if (dispose)
            { operation.Dispose(); Throws<ObjectDisposedException>(() => operation.ResumeDecision(new(1))); }
            else
            {
                f.Diplomacy.Grant.FailRevolution = true;
                Throws<InvalidOperationException>(() => operation.ResumeDecision(new(1)));
                Throws<InvalidOperationException>(() => operation.ResumeDecision());
            }
            Equal(null, operation.Result); Equal(0, f.Life.MapRefreshes);
            Equal(true, f.World.Civilizations[2].Advances.Contains(AdvanceIds.Monarchy));
            Equal(true, f.World.Civilizations[1].Advances.Contains(AdvanceIds.Monarchy));
            Equal(false, f.World.Globals.CivsAlive.Contains(2)); f.Consumed();
        }
        var choices = CreationAttitudes(); CreationSiteChoices(choices);
        choices = choices.Select(c => c with { Key = c.Key with { Call = c.Key.Call.Replace("create", "spawn", StringComparison.Ordinal) } }).ToList();
        var placement = new SinglePlayerLifecycleFixture(choices);
        var world = placement.World; world.Globals.Turn = 0; world.Globals.HumanPlayers = CivilizationSet.Only(1);
        world.Globals.CivsAlive = CivilizationSet.Only(1); world.Multiplayer.LocalHuman = 1;
        world.Entities.Cities.SetEnd(0); world.Entities.Units.SetEnd(0);
        foreach (ref var tile in world.Map.Tiles) { tile.Features = default; tile.Owner = 0; }
        placement.Data.Rules.Units[0] = placement.Data.Rules.Units[0] with { Role = RuleRole.Settle, Prerequisite = AdvanceReference.None };
        using var creation = placement.Services.Create(1, "spawn");
        Equal(1, creation.AdvanceDecision(1)); Equal(new MapPoint(10, 8), creation.Site!.Value.Position);
        int unit = placement.Data.Stacks.FirstAt(new(10, 8)); Equal(true, unit >= 0);
        Equal(new CivilizationSlot(1), placement.Data.Unit(unit).Owner); Equal(new UnitType(0), placement.Data.Unit(unit).Type);
        placement.Consumed();
    }
}
