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
    // V01 §§3,6,9 with actual V03 Library recursion and V02 government/production effects.
    private sealed class RestartGrantFixture
    {
        public RevelationFixture Data { get; }
        public GameState World => Data.World;
        public GameplayRandom Random { get; }
        public RetainedGrantFixture Research { get; }
        public int Owner { get; }
        public RestartGrantFixture(RevelationFixture? data = null, int owner = 2, bool later = false, bool placement = false)
        {
            Data = CreationFixture(data); Owner = owner;
            var choices = CreationAttitudes(); CreationChoice(choices, "catch-up", 3, 1, AdvanceIds.Monarchy);
            if (later) CreationChoice(choices, "catch-up", 3, 1, 55);
            if (placement) { CreationChoice(choices, "gender", 3, 1); CreationSiteChoices(choices); }
            Random = CreationRandom(choices); Research = new(new GrantFixture(Data, Random));
            World.Globals.Turn = 25; World.Globals.Difficulty = Difficulty.Prince;
            World.Globals.HumanPlayers = CivilizationSet.Only(1).With(4);
            World.Globals.CivsAlive = CivilizationSet.Only(1).With(owner).With(3);
            World.Multiplayer.LocalHuman = 1;
            Data.AddCity(0, 1, new(30, 20)); World.Civilizations[1].CityCount = 1;
            World.Globals.WonderCity[WonderIds.GreatLibrary] = 0;
            World.Entities.Cities.Storage[0].Production = UnitTypeIds.Fanatics;
            World.Civilizations[owner].UnitsActive[4] = 7; World.Civilizations[owner].UnitsLost[4] = 9;
            for (int i = 0; i < Data.Rules.Terrain.Length; ++i)
            {
                Data.Rules.Terrain[i] = Data.Rules.Terrain[i] with
                { Food = 2, Shields = 1, Trade = 1, Work = new(new(TerrainResult.ImproveInPlace, 1), new(TerrainResult.ImproveInPlace, 2)) };
            }
            foreach (int advance in later ? new[] { AdvanceIds.Monarchy, 55 } : new[] { AdvanceIds.Monarchy })
            {
                World.Civilizations[4].Advances = World.Civilizations[4].Advances.With(advance);
                World.Globals.AdvanceKnownBy[advance] = CivilizationSet.Only(4); World.Globals.FirstDiscoverer[advance] = 7;
            }
            if (placement) Data.Process.Rules.Leaders[owner].Color = (byte)owner;
        }
        public CivilizationCreation Create() => Creation(Data, Owner, Random, grants: Research.Grants);
        public void Pending(LifecycleDecision? decision)
        {
            Equal(true, decision is ResearchGrantDecision.Government { Choice.Civilization: 1 });
            Equal(false, World.Globals.CivsAlive.Contains(Owner));
            Equal(true, World.Civilizations[Owner].Advances.Contains(AdvanceIds.Monarchy));
            Equal(true, World.Civilizations[1].Advances.Contains(AdvanceIds.Monarchy));
            Equal(Government.Despotism, World.Civilizations[1].Government);
            Equal((sbyte)UnitTypeIds.Fanatics, World.Entities.Cities.Storage[0].Production);
            Equal((byte)9, World.Civilizations[Owner].UnitsLost[4]);
        }
    }
    private static void CreationRetainsLibraryGovernment()
    {
        foreach (int answer in new[] { 0, 1 })
        {
            var f = new RestartGrantFixture(later: true, placement: true); using var creation = f.Create();
            Equal(null, creation.Advance(1)); f.Pending(creation.Pending);
            Equal((byte)7, f.World.Civilizations[2].UnitsActive[4]); Equal(null, creation.Site);
            Equal(false, f.World.Civilizations[2].Advances.Contains(55));
            Equal(1, f.Research.G.Acquisitions.Count); var pending = creation.Pending;
            Equal(null, creation.Advance(1)); Equal(pending, creation.Pending); Equal(1, f.Research.G.Acquisitions.Count);
            Equal(1, creation.Advance(1, answer)); f.Random.RequireChoicesConsumed();
            Equal(answer == 1 ? Government.Anarchy : Government.Despotism, f.World.Civilizations[1].Government);
            Equal((sbyte)(answer == 1 ? UnitTypeIds.Riflemen : UnitTypeIds.Fanatics), f.World.Entities.Cities.Storage[0].Production);
            Equal(2, f.Research.G.Acquisitions.Count); Equal(true, f.World.Civilizations[2].Advances.Contains(55));
            Equal((byte)1, f.World.Civilizations[2].NumAdvances); Equal((byte)2, f.World.Civilizations[1].NumAdvances);
            Equal((byte)0, f.World.Civilizations[2].UnitsLost[4]); Equal(true, f.World.Globals.CivsAlive.Contains(2));
            Equal(new MapPoint(10, 8), creation.Site!.Value.Position); Equal(2, f.World.Entities.Units.End); // Unseen, turn > 20: V01 places the extra Settlers.
            Equal(1, creation.Advance(1, answer)); Equal(2, f.Research.G.Acquisitions.Count);
        }
    }
    private static void CreationGrantFailureAndDisposal()
    {
        foreach (bool dispose in new[] { false, true })
        {
            var f = new RestartGrantFixture(); using var creation = f.Create(); creation.Advance(1); var pending = creation.Pending; f.Pending(pending);
            if (dispose) { creation.Dispose(); Throws<ObjectDisposedException>(() => creation.Advance(1, 1)); }
            else
            {
                f.Research.FailRevolution = true;
                Throws<InvalidOperationException>(() => creation.Advance(1, 1));
                Throws<InvalidOperationException>(() => creation.Advance(1, 0));
            }
            f.Pending(pending);
            Equal((byte)7, f.World.Civilizations[2].UnitsActive[4]); Equal(null, creation.Result); Equal(null, creation.Site);
            Equal(1, f.Research.G.Acquisitions.Count); f.Random.RequireChoicesConsumed();
        }
        var sync = new EliminationFixture(); var catchup = new RestartGrantFixture(sync.Data);
        sync.Recreate = _ => catchup.Create();
        Throws<InvalidOperationException>(() => sync.Elimination.Check(2, -1, "sync-retained"));
        Equal(0, sync.MapRefreshes); Equal(false, sync.World.Globals.CivsAlive.Contains(2));
        Equal(Government.Despotism, sync.World.Civilizations[1].Government); catchup.Random.RequireChoicesConsumed();
    }
    private static void EliminationRetainsCatchupGrant()
    {
        foreach (bool dispose in new[] { false, true })
        {
            var life = new EliminationFixture(); var incidents = new IncidentFixture(life.Random, life.Data);
            var f = new RestartGrantFixture(life.Data); life.Recreate = _ => f.Create();
            f.Data.AddUnit(0, 2, 0, new(12, 8)); f.World.Civilizations[2].UnitsActive[0] = 1;
            using var deletion = life.Elimination.BeginDelete(0, true, "last-settler", incidents.Incidents);
            var pending = deletion.Resume(); f.Pending(pending); Equal(false, f.Data.Unit(0).Token.Occupied);
            Equal(1, life.Restarts); Equal(0, life.MapRefreshes); Equal(pending, deletion.Resume());
            if (dispose)
            { deletion.Dispose(); Throws<ObjectDisposedException>(() => deletion.Resume(1)); Equal(0, life.MapRefreshes); }
            else
            {
                Equal(null, deletion.Resume(1)); Equal(1, deletion.Result); Equal(1, life.MapRefreshes);
                Equal(Government.Anarchy, f.World.Civilizations[1].Government); Equal((byte)0, f.World.Civilizations[2].UnitsLost[4]);
            }
            Equal(dispose, f.World.UnitDeletion.Active); // U01 clears its activity marker only on the normal tail.
            f.Random.RequireChoicesConsumed(); life.Random.RequireChoicesConsumed();
        }
    }
    private sealed class RestartWarEffects : ICivilWarEffects
    {
        public int Notices, Redraws;
        public void Schism(CivilWarNotice notice) => ++Notices;
        public void RedrawMaps() => ++Redraws;
    }
    private static void CivilWarRetainsCatchupGrant()
    {
        var f = new RestartGrantFixture(owner: 7); var w = f.World;
        for (int c = 1; c < 7; ++c) w.Civilizations[c].CityCount = 1;
        f.Data.AddCity(1, 2, new(4, 4), 9); w.Civilizations[2].TotalCitySize = 9; w.Civilizations[2].Gold = 101;
        BuildingQueries.SetBuilding(w, new(1), BuildingIds.Palace, true);
        var effects = new RestartWarEffects();
        var war = new CivilWar(w, f.Data.Stacks, _ => f.Create(), _ => throw new InvalidOperationException("No matching event"), effects);
        using var operation = war.Begin(2);
        Equal(null, operation.Advance()); f.Pending(operation.Pending);
        Equal(101, w.Civilizations[2].Gold); Equal(0, effects.Notices); Equal(0, effects.Redraws);
        Equal(null, operation.Advance()); Equal(1, f.Research.G.Acquisitions.Count);
        Equal(1, operation.Advance(answer: 1)); Equal(Government.Anarchy, w.Civilizations[1].Government);
        Equal(51, w.Civilizations[2].Gold); Equal(50, w.Civilizations[7].Gold); Equal(true, w.Globals.CivsAlive.Contains(7));
        Equal(1, effects.Notices); Equal(1, effects.Redraws); f.Random.RequireChoicesConsumed();
    }
    private static void RestartGrantPrivateSessions()
    {
        foreach (bool elimination in new[] { false, true })
            foreach (bool cancel in new[] { false, true })
            {
                using var runtime = new SessionRuntime(new(), new(279));
                var life = new EliminationFixture(data: CreationFixture(new(runtime.Process, runtime.World)));
                var incidents = new IncidentFixture(life.Random, life.Data); var f = new RestartGrantFixture(life.Data);
                life.Recreate = _ => f.Create(); ParticipantId ownerSeat = default;
                runtime.RegisterCommand(new(new(1, "test.restart", "Authored retained restart", AccessClass.Player, new([]), new([new("result", ValueKind.Integer)])), (_, _, _) => new(),
                    _ => elimination
                        ? new CivilizationEliminationOperation(life.Elimination.Begin(2, -1, "restart", incidents.Incidents), _ => ownerSeat)
                        : new CivilizationCreationOperation(f.Create(), _ => ownerSeat)));
                runtime.BeginClientSession(new()); var actor = SessionClient(runtime, 3); var owner = SessionClient(runtime, 1); var unrelated = SessionClient(runtime, 4);
                ownerSeat = owner.Identity.Participant;
                var id = actor.Submit(new("test.restart", new([]))).Correlation.Command; runtime.AdvanceOne();
                var prompt = RequiredInteraction(owner, id); Equal(null, RequiredResult(actor, id).Interaction); Equal(null, unrelated.Result(id));
                Equal(false, actor.Reply(prompt, 1).Accepted); Equal(false, unrelated.Reply(prompt, 1).Accepted);
                Equal(false, f.World.Globals.CivsAlive.Contains(2)); Equal(0, life.MapRefreshes);
                if (cancel) { Equal(true, owner.Cancel(id).Accepted); }
                else { Equal(true, owner.Reply(prompt, 1).Accepted); }
                runtime.AdvanceOne(); Equal(CommandStatus.Completed, RequiredResult(actor, id).Status);
                Equal(cancel ? Government.Despotism : Government.Anarchy, f.World.Civilizations[1].Government);
                Equal(elimination ? 1 : 0, life.MapRefreshes); Equal(1, f.Research.G.Acquisitions.Count);
                f.Random.RequireChoicesConsumed(); life.Random.RequireChoicesConsumed();
            }
    }
    private static void CombatCasualtyRetainsRestartGrant()
    {
        foreach (var input in new[] { new OperationInput(OperationInputKind.Choice, 1), new OperationInput(OperationInputKind.Cancel), new OperationInput(OperationInputKind.Timeout) })
        {
            var casualty = new CasualtyFixture(); var f = new RestartGrantFixture(casualty.Data);
            casualty.Life.Recreate = _ => f.Create();
            f.Data.Rules.Units[4] = f.Data.Rules.Units[4] with { Role = RuleRole.Settle };
            f.World.Civilizations[2].CityCount = 0; f.World.Civilizations[2].UnitsActive[4] = 1;
            using var core = casualty.Casualties.Begin(new(3), new(0), new(20, 10), "restart-casualty");
            using var session = new CombatCasualtyOperation(core, c => new((ulong)c));
            var context = new OperationContext(f.World, f.Data.Process, f.Random, new PresentationRandom(1, "restart-casualty"),
                new(default, new(3)), default, (_, _) => { }, () => { });
            var pending = session.Resume(context, new()); Equal(CommandStatus.AwaitingInteraction, pending.Status);
            Equal((ParticipantId?)new ParticipantId(1), pending.Interaction!.Recipient);
            Equal(false, f.Data.Unit(3).Token.Occupied); Equal(1, f.World.Casualties[2].Count);
            Equal((byte)10, f.World.Civilizations[2].UnitsLost[4]); Equal(0, casualty.Life.MapRefreshes);
            Equal(CommandStatus.AwaitingInteraction, session.Resume(context, new()).Status);
            var result = session.Resume(context, input);
            for (int i = 0; i < 10 && result.Status == CommandStatus.Pending; ++i) result = session.Resume(context, new());
            Equal(CommandStatus.Completed, result.Status); Equal(1, casualty.Life.MapRefreshes);
            Equal(input.Kind == OperationInputKind.Choice ? Government.Anarchy : Government.Despotism, f.World.Civilizations[1].Government);
            Equal((byte)0, f.World.Civilizations[2].UnitsLost[4]); Equal(false, f.World.UnitDeletion.Active);
            Equal(1, f.World.Casualties[2].Count); Equal(1, f.Research.G.Acquisitions.Count);
            f.Random.RequireChoicesConsumed(); casualty.Life.Random.RequireChoicesConsumed();
        }
    }

}
