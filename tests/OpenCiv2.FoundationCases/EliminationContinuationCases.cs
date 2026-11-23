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
    private static void EliminationSpySetup(EliminationFixture f, int spyType = 47)
    {
        var w = f.World; w.Globals.HumanPlayers = w.Globals.CivsAlive = CivilizationSet.Only(2);
        w.Multiplayer.LocalHuman = 2;
        var passwords = Enumerable.Repeat("kept", 8).ToArray(); passwords[2] = "secret"; w.Multiplayer.Saved.Passwords = new(passwords);
        foreach (ref var tile in w.Map.Tiles) { tile.RevealedTo = CivilizationSet.Only(2); }
        f.Data.AddUnit(0, 2, 4, new(20, 10)); f.Data.AddUnit(1, 1, spyType, new(22, 10));
        f.Data.Unit(1).Flags.SpyMission = true; f.Data.Unit(1).Counter = 2;
        w.Civilizations[2].Treaties[2].SpyChat = w.Civilizations[2].Treaties[1].SpyChat = true;
        DiplomaticRelations.Set(w, 1, 2, new() { Peace = true });
        w.Civilizations[2].SpaceshipFlags.Exists = true;
    }

    // V01 §6 retains the captured descending scan while V06 loss and the human reply complete.
    private static void EliminationResumesAfterSpyPretext()
    {
        foreach (int reply in new[] { 0, 1, 2 })
        {
            var f = new EliminationFixture([EscapeChoice("elim/spy/1", 2, 0)]); EliminationSpySetup(f);
            var incidents = new IncidentFixture(f.Random, f.Data); var w = f.World;
            using var op = f.Elimination.Begin(2, 3, "elim", incidents.Incidents);
            var prompt = op.Resume()!; Equal(2, prompt.Victim.Civilization); Equal(1, prompt.Actor.Civilization);
            Equal(null, op.Result); Equal(1, f.Destructions.Count); Equal(1, (int)w.Eliminations.Count);
            Equal(false, w.Civilizations[2].SpaceshipFlags.Exists); Equal(false, f.Data.Unit(1).Token.Occupied);
            Equal(true, f.Data.Unit(0).Token.Occupied); Equal("secret", w.Multiplayer.Saved.Passwords[2]);
            Equal(false, w.Civilizations[2].Treaties[2].SpyChat); Equal(true, w.Civilizations[2].Treaties[1].SpyChat);
            Equal(0, f.Incidents.Count); int prefix = f.Order.Count;
            Equal(prompt, op.Resume()); Equal(prefix, f.Order.Count);
            Equal(null, op.Resume(reply)); Equal(0, op.Result); Equal(false, f.Data.Unit(0).Token.Occupied);
            Equal(reply == 1, w.Civilizations[1].Treaties[2].War); Equal("", w.Multiplayer.Saved.Passwords[2]);
            Equal("kept", w.Multiplayer.Saved.Passwords[1]); Equal(EndReason.Defeat, w.PendingEndReason);
            Equal(true, w.Globals.CivsAlive.Contains(2)); Equal(true, w.Map.TileAt(new(8, 8)).RevealedTo.Contains(2));
            Equal(0, f.Restarts); Equal(1, f.Destructions.Count); f.Random.RequireChoicesConsumed();
            prefix = f.Order.Count; op.Resume(1); Equal(prefix, f.Order.Count);
        }
    }

    // A custom Spy with Settler role reaches U01's last-settler child during loss. That child
    // interrupts another mission and must finish its own incident before the outer loss resumes.
    private static void EliminationNestedLossAndSeparateReplies()
    {
        foreach (bool sea in new[] { false, true })
        {
            var f = new EliminationFixture([EscapeChoice("elim/spy/1", 2, 0), IncidentChoice(0, call: "elim/spy/1/incident")]);
            var w = f.World; w.Globals.HumanPlayers = w.Globals.CivsAlive = CivilizationSet.Only(1).With(2);
            f.Data.Rules.Units[47] = f.Data.Rules.Units[47] with { Role = RuleRole.Settle };
            f.Data.AddUnit(0, 3, 4, new(20, 10)); f.Data.Unit(0).Flags.SpyMission = true; f.Data.Unit(0).Counter = 1;
            f.Data.AddUnit(1, 1, 47, new(22, 10)); f.Data.Unit(1).Flags.SpyMission = true; f.Data.Unit(1).Counter = 2;
            if (sea)
            {
                f.Data.Rules.Units[47] = f.Data.Rules.Units[47] with { Domain = RuleDomain.Sea, Capacity = 1 };
                w.Map.TileAt(new(22, 10)).Terrain = Terrain.Ocean;
                f.Data.AddUnit(2, 1, 4, new(22, 10)); f.Data.Unit(2).Orders = UnitOrder.Sleeping; f.Data.Unit(2).Carrier = new(1);
            }
            w.Civilizations[1].CityCount = 0; w.Civilizations[1].UnitsActive[47] = 1;
            DiplomaticRelations.Set(w, 3, 1, new() { Peace = true }); DiplomaticRelations.Set(w, 1, 2, new() { Peace = true });
            var incidents = new IncidentFixture(f.Random, f.Data);
            f.Recreate = owner => Creation(f.Data, owner, f.Random); // Nonzero-turn human creation exits without draws.
            using var op = f.Elimination.Begin(2, 3, "elim", incidents.Incidents);
            var first = op.Resume()!; Equal(1, first.Victim.Civilization); Equal(3, first.Actor.Civilization);
            Equal(false, f.Data.Unit(0).Token.Occupied || f.Data.Unit(1).Token.Occupied);
            if (sea) { Equal(false, f.Data.Unit(2).Token.Occupied); }
            Equal(false, f.Order.Contains("units")); // The outer cargo-loss tail has not run yet.
            Equal(0, f.Restarts); Equal(0, f.Checks); // The operation runs the real nested child, not the synchronous adapter.
            var second = op.Resume(1)!; Equal(2, second.Victim.Civilization); Equal(1, second.Actor.Civilization);
            Equal(true, w.Civilizations[3].Treaties[1].War); Equal(false, w.Civilizations[1].Treaties[2].War);
            Equal(1, f.Restarts); Equal(null, op.Result); Equal(1, f.Destructions.Count);
            Equal(sea, f.Order.Contains("units"));
            Equal(null, op.Resume(0)); Equal(1, op.Result); Equal(2, f.Restarts); Equal(2, f.MapRefreshes);
            Equal(false, w.Civilizations[1].Treaties[2].War); Equal(false, w.UnitDeletion.Active);
            Equal(false, w.UnitDeletion.CargoActive); Equal(false, w.UnitDeletion.DestroyActive); f.Random.RequireChoicesConsumed();
        }
    }

    private static void EliminationBoundedCreationAndDisposal()
    {
        foreach (bool dispose in new[] { false, true })
        {
            var f = new EliminationFixture(); var w = f.World; w.Globals.CivsAlive = CivilizationSet.Only(1).With(2).With(3);
            f.Data.Process.Rules.Leaders[2].Color = 2; w.Map.TileAt(new(10, 8)).Terrain = Terrain.Ocean;
            var choices = CreationAttitudes(); CreationChoice(choices, "gender", 3, 1); CreationSiteChoices(choices, dispose ? 1 : 2);
            if (!dispose) { choices[^2] = Choice.Create(new("F05.start-site.x", "create/site", 1), new SamplingLaw.PrimaryModulo(40), ChoiceCode.Primary(12)); }
            var random = CreationRandom(choices); f.Recreate = owner => Creation(f.Data, owner, random);
            var incidents = new IncidentFixture(f.Random, f.Data);
            using var op = f.Elimination.Begin(2, -1, "search", incidents.Incidents, 1);
            Equal(null, op.Resume()); Equal(null, op.Result); Equal(1, f.Restarts); Equal(0, f.MapRefreshes);
            Equal(0, w.Entities.Units.End);
            if (dispose)
            {
                op.Dispose(); Throws<ObjectDisposedException>(() => op.Resume()); Equal(0, f.MapRefreshes);
            }
            else
            {
                Equal(null, op.Resume()); Equal(1, op.Result); Equal(1, f.Restarts); Equal(1, f.MapRefreshes);
                Equal(new MapPoint(12, 8), f.Data.Unit(0).Position);
            }
            random.RequireChoicesConsumed(); f.Random.RequireChoicesConsumed();
        }
    }

    private static void EliminationIncidentFailureAndAuthority()
    {
        var f = new EliminationFixture([EscapeChoice("failed/spy/1", 2, 0)]); EliminationSpySetup(f);
        var incidents = new IncidentFixture(f.Random, f.Data) { FailScandal = true }; var w = f.World;
        w.Globals.HumanPlayers = CivilizationSet.Only(1); w.Multiplayer.LocalHuman = 1;
        w.Civilizations[1].Government = Government.Democracy;
        using var op = f.Elimination.Begin(2, 3, "failed", incidents.Incidents);
        Throws<InvalidOperationException>(() => op.Resume()); int prefix = f.Order.Count;
        Throws<InvalidOperationException>(() => op.Resume()); Equal(prefix, f.Order.Count);
        Equal(null, op.Result); Equal(false, f.Data.Unit(1).Token.Occupied); Equal(true, f.Data.Unit(0).Token.Occupied);
        Equal(true, w.Civilizations[1].Treaties[2].War); Equal(Government.Democracy, w.Civilizations[1].Government);
        Equal(1, f.Destructions.Count); Equal(0, f.Restarts); f.Random.RequireChoicesConsumed();

        f = new(); incidents = new(f.Random, f.Data); w = f.World; w.Globals.Multiplayer = GameMode.Tcp;
        using var remote = f.Elimination.Begin(2, 3, "network", incidents.Incidents);
        Throws<InvalidOperationException>(() => remote.Resume()); Equal(0, f.Destructions.Count);
        using var barbarian = f.Elimination.Begin(0, 99, "", incidents.Incidents);
        Equal(null, barbarian.Resume()); Equal(0, barbarian.Result); f.Random.RequireChoicesConsumed();
    }

    private static void EliminationSessionVictimRouting()
    {
        foreach (bool cancel in new[] { false, true })
        {
            using var runtime = new SessionRuntime(new(), new(190));
            var data = CreationFixture(new(runtime.Process, runtime.World)); var f = new EliminationFixture(data: data);
            EliminationSpySetup(f, 4); var incidents = new IncidentFixture(f.Random, data);
            ParticipantId victim = default;
            runtime.RegisterCommand(new(new(1, "test.eliminate", "Authored lifecycle integration", AccessClass.Player, new([]), new([new("result")])),
                (_, _, _) => new(), _ => new CivilizationEliminationOperation(f.Elimination.Begin(2, 3, "session", incidents.Incidents),
                    c => c == 2 ? victim : throw new InvalidOperationException("Unexpected victim"))));
            runtime.BeginClientSession(new()); var actor = SessionClient(runtime); var decider = SessionClient(runtime, 2);
            victim = decider.Identity.Participant; var outsider = SessionClient(runtime, 3);
            var command = actor.Submit(new("test.eliminate", new([]))).Correlation.Command; Equal(true, runtime.AdvanceOne());
            var update = RequiredResult(decider, command); var prompt = update.Interaction!;
            Equal(victim, prompt.Recipient); Equal(new CivilizationSlot(2), prompt.Observer);
            Equal(CommandStatus.AwaitingInteraction, update.Status); Equal(null, RequiredResult(actor, command).Interaction);
            Equal(null, outsider.Result(command)); Equal(false, actor.Reply(prompt.Id, 1).Accepted);
            Equal(false, f.Data.Unit(1).Token.Occupied); Equal(true, f.Data.Unit(0).Token.Occupied);
            if (cancel) { Equal(true, runtime.CancelCommand(command).Accepted); }
            else { Equal(true, decider.Reply(prompt.Id, 1).Accepted); }
            Equal(true, runtime.AdvanceOne()); Equal(CommandStatus.Completed, RequiredResult(actor, command).Status);
            Equal(!cancel, runtime.World.Civilizations[1].Treaties[2].War);
            Equal(false, f.Data.Unit(0).Token.Occupied); Equal(false, runtime.HasPendingCommands);
            Equal("civilization.elimination.completed", RequiredResult(actor, command).Committed.Single().Name);
            Equal(null, decider.Result(command)); f.Random.RequireChoicesConsumed();
        }
    }
}
