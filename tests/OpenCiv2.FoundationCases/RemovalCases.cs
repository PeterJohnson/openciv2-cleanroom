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
    // V01 §8 and S02 §9. Specification expectations; no original execution evidence is implied.
    private sealed class RemovalFixture
    {
        public EliminationFixture Life { get; }
        public RevelationFixture Data => Life.Data;
        public GameState World => Data.World;
        public IncidentFixture Incidents { get; }
        public CivilizationRemoval Service { get; }
        public Func<int, CivilizationCreation>? Create;
        public int Creations, Redraws;
        public bool FailRedraw;
        public RemovalFixture(List<Choice>? choices = null, RevelationFixture? data = null)
        {
            Life = new(choices, data); Incidents = new(Life.Random, Data);
            World.Globals.CivsAlive = CivilizationSet.Only(1).With(2).With(3);
            World.Globals.HumanPlayers = CivilizationSet.Only(1).With(2);
            World.Multiplayer.Saved.Passwords = new(Enumerable.Range(0, 8).Select(i => "password" + i));
            Service = new(World, Data.Rules, Data.Stacks, Life.Elimination, Incidents.Incidents,
                c => { ++Creations; return (Create ?? throw new InvalidOperationException("Unexpected recreation"))(c); },
                () => { ++Redraws; if (FailRedraw) { throw new InvalidOperationException("Missing map view"); } });
        }
        public void Finish(CivilizationRemoval.Operation op) { Equal(null, op.Resume()); Equal(true, op.Completed); Life.Random.RequireChoicesConsumed(); }
    }
    private static void RemovalControlAndLowBytes()
    {
        foreach (var (kill, policy, destroys) in new[] { (0, false, false), (256, false, false), (-256, false, false), (257, false, true), (0, true, true) })
        {
            var f = new RemovalFixture(); f.World.Multiplayer.Saved.KillOnRetirement = policy;
            f.Data.AddUnit(0, 2, 4, new(10, 8)); f.Data.AddCity(0, 2, new(10, 8));
            f.World.Civilizations[2].SpaceshipFlags.Launched = true;
            using var op = f.Service.Begin(2, kill, 256, "remove"); f.Finish(op);
            Equal(!destroys, f.Data.Unit(0).Token.Occupied); Equal(!destroys, f.World.Entities.Cities.Storage[0].Token.Occupied);
            Equal(!destroys, f.World.Globals.CivsAlive.Contains(2)); Equal(!destroys, f.World.Civilizations[2].SpaceshipFlags.Launched);
            Equal(false, f.World.Globals.HumanPlayers.Contains(2)); Equal("", f.World.Multiplayer.Saved.Passwords[2]);
            Equal("password1", f.World.Multiplayer.Saved.Passwords[1]); Equal(0, f.Creations); Equal(1, f.Redraws);
            Equal(null, op.Resume(new(1))); Equal(1, f.Redraws);
        }
        var last = new RemovalFixture(); last.World.Globals.HumanPlayers = CivilizationSet.Only(2);
        using var only = last.Service.Begin(2, 0, 1, "last-human"); last.Finish(only);
        Equal(0, last.Creations); Equal(0, last.Redraws); Equal(true, last.World.Globals.CivsAlive.Contains(2));
    }
    private static void RemovalInlineUnits()
    {
        var f = new RemovalFixture(); var d = f.Data; var w = f.World;
        d.AddUnit(0, 2, 4, new(10, 8)); d.AddUnit(1, 3, 4, new(10, 8));
        // Imported backed links are followed by the inline pass without an owner filter.
        d.Unit(0).Previous = new(1); d.Unit(1).Next = new(0);
        d.AddUnit(2, 2, 4, new(16, 8)); d.AddUnit(4, 2, 4, new(22, 8));
        d.Unit(2).Home = new(6); d.Unit(2).Visibility = CivilizationSet.Only(5); d.Unit(2).Damage = 7;
        d.Unit(3).Orders = UnitOrder.Sleeping; d.Unit(3).Carrier = new(2); d.Unit(3).Destination = new(99, 12);
        d.Rules.Units[4] = d.Rules.Units[4] with { Role = (RuleRole)255 };
        w.Civilizations[2].MilitaryCount = 2; w.Civilizations[2].UnitsActive[4] = 2; w.UnitTotals[2] = 2;
        using var op = f.Service.Begin(2, 1, 0, "units"); f.Finish(op);
        Equal((short)0, w.Civilizations[2].MilitaryCount); Equal((byte)0, w.Civilizations[2].UnitsActive[4]); Equal(0, w.UnitTotals[2]);
        Equal(true, d.Unit(1).Token.Occupied); Equal(null, d.Unit(1).Next); Equal(null, d.Unit(1).Previous);
        Equal(new MapPoint(-300, -300), d.Unit(2).Position); Equal(new CitySlot(6), d.Unit(2).Home);
        Equal(CivilizationSet.Only(5), d.Unit(2).Visibility); Equal((byte)7, d.Unit(2).Damage);
        Equal(true, w.Map.TileAt(new(16, 8)).Features.HasFlag(MapFeatures.Unit)); // Earlier relink suppresses later isolated clearing.
        Equal(true, w.Map.TileAt(new(22, 8)).Features.HasFlag(MapFeatures.Unit));
        Equal(UnitOrder.None, d.Unit(3).Orders); Equal(new MapPoint(2, 12), d.Unit(3).Destination); Equal(new UnitSlot(2), d.Unit(3).Carrier);
        Equal(4, w.Entities.Units.End); Equal((short)4, w.Globals.UnitEnd); Equal(false, d.Unit(3).Token.Occupied);
        Equal(0, f.Life.Checks); Equal(0, f.Life.Order.Count);
        var isolated = new RemovalFixture(); isolated.Data.AddUnit(0, 2, 4, new(10, 8));
        using var clear = isolated.Service.Begin(2, 1, 0, "isolated"); isolated.Finish(clear);
        Equal(false, isolated.World.Map.TileAt(new(10, 8)).Features.HasFlag(MapFeatures.Unit));
    }
    private static void RemovalCityRoutesAndRadius()
    {
        foreach (bool retainedDead in new[] { false, true })
        {
            var f = new RemovalFixture(); var d = f.Data; var w = f.World; var center = new MapPoint(10, 8);
            int removed = retainedDead ? 0 : 2;
            d.AddCity(removed, 2, center); d.AddCity(retainedDead ? 2 : 0, 3, new(30, 16));
            w.Entities.Cities.Storage[1].Position = new(20, 12); // A dead city still owns a radius repair visit.
            int foreign = retainedDead ? 2 : 0;
            ref var c = ref w.Entities.Cities.Storage[removed]; c.Improvements = c.Improvements.With(BuildingIds.Palace); c.FoodBox = 13;
            ref var other = ref w.Entities.Cities.Storage[foreign]; other.RouteCount = 3;
            other.RoutePartners[0] = new((ushort)removed); other.RoutePartners[1] = new(77); other.RoutePartners[2] = new((ushort)removed);
            w.Globals.WonderCity[0] = w.Globals.WonderCity[27] = (short)removed;
            d.AddUnit(0, 3, 4, center); d.AddUnit(1, 4, 4, new(12, 8)); d.Unit(0).Home = new((ushort)removed);
            foreach (ref var tile in w.Map.Tiles) { tile.Fertility = 3; tile.Claimant = 2; tile.RevealedTo = CivilizationSet.Only(2).With(5); }
            w.Map.TileAt(new(20, 12)).Terrain = Terrain.Ocean; w.Map.TileAt(new(20, 12)).Fertility = 15;
            using var op = f.Service.Begin(2, 1, 0, "cities"); f.Finish(op);
            Equal((short)-1, w.Civilizations[2].CityCount); Equal(false, c.Token.Occupied); Equal((short)13, c.FoodBox);
            Equal(true, c.Improvements.Contains(BuildingIds.Palace)); Equal(new CitySlot((ushort)removed), d.Unit(0).Home);
            Equal((byte)1, other.RouteCount); Equal(new CitySlot(77), other.RoutePartners[0]); Equal(true, other.Flags.TradeDirty);
            Equal((short)-2, w.Globals.WonderCity[0]); Equal((short)-2, w.Globals.WonderCity[27]);
            Equal(true, d.Unit(0).Visibility.Contains(4)); Equal(false, w.Map.TileAt(center).Features.HasFlag(MapFeatures.CitySite));
            Equal((byte)2, w.Map.TileAt(new(20, 12)).Claimant); Equal((byte)7, w.Map.TileAt(new(20, 12)).Fertility);
            Equal((byte)2, w.Map.TileAt(new(30, 16)).Claimant); Equal((byte)(retainedDead ? 3 : 11), w.Map.TileAt(center).Fertility);
            Equal((byte)(retainedDead ? 2 : 0), w.Map.TileAt(center).Claimant);
            Equal(retainedDead ? 3 : 2, w.Entities.Cities.End);
            Equal(true, w.Map.Tiles.ToArray().All(t => !t.RevealedTo.Contains(2) && t.RevealedTo.Contains(5)));
        }
    }
    private static void RemovalForeignMissionAndFailure()
    {
        var f = new RemovalFixture([EscapeChoice("remove/spy/0", 2, 1)]); var w = f.World;
        f.Data.AddUnit(0, 3, 47, new(10, 8)); f.Data.Unit(0).Flags.SpyMission = true; f.Data.Unit(0).Counter = 2;
        f.Data.AddCity(0, 3, new(30, 16)); f.Data.AddUnit(1, 2, 4, new(16, 8));
        w.Civilizations[2].Treaties[2].SpyChat = w.Civilizations[2].Treaties[3].SpyChat = true;
        using var op = f.Service.Begin(2, 1, 0, "remove"); f.Finish(op);
        Equal(false, f.Data.Unit(0).Flags.SpyMission); Equal(true, f.Data.Unit(0).Flags.Veteran);
        Equal(new MapPoint(30, 16), f.Data.Unit(0).Position); Equal(false, w.Civilizations[2].Treaties[2].SpyChat);
        Equal(true, w.Civilizations[2].Treaties[3].SpyChat); Equal(false, f.Data.Unit(1).Token.Occupied);
        var invalid = new RemovalFixture(); invalid.Data.AddUnit(0, 2, 4, new(10, 8)); invalid.Data.Unit(0).Previous = new(60000);
        invalid.World.Civilizations[2].UnitsActive[4] = 2;
        using var bad = invalid.Service.Begin(2, 1, 0, "bad"); Throws<ArgumentOutOfRangeException>(() => bad.Resume());
        Equal((byte)1, invalid.World.Civilizations[2].UnitsActive[4]); Equal(true, invalid.Data.Unit(0).Token.Occupied);
        Equal(false, invalid.World.Globals.HumanPlayers.Contains(2)); Equal("password2", invalid.World.Multiplayer.Saved.Passwords[2]);
        Throws<InvalidOperationException>(() => bad.Resume());
        var remote = new RemovalFixture(); remote.World.Globals.Multiplayer = GameMode.Tcp;
        using var network = remote.Service.Begin(2, 1, 1, "remote"); Throws<InvalidOperationException>(() => network.Resume());
        Equal(true, remote.World.Globals.HumanPlayers.Contains(2));
    }
    private static void RemovalRetainedRecreation()
    {
        foreach (bool dispose in new[] { false, true })
        {
            var f = new RemovalFixture(); var grants = new RestartGrantFixture(f.Data); f.Create = _ => grants.Create();
            f.Data.AddUnit(0, 2, 4, new(12, 8));
            using var op = f.Service.Begin(2, 1, 257, "remove"); var pending = op.Resume(); grants.Pending(pending);
            Equal(false, f.Data.Unit(0).Token.Occupied); Equal("", f.World.Multiplayer.Saved.Passwords[2]);
            Equal(0, f.Redraws); Equal(pending, op.Resume()); Equal(1, f.Creations);
            if (dispose) { op.Dispose(); Throws<ObjectDisposedException>(() => op.Resume(new(1))); Equal(0, f.Redraws); }
            else { Equal(null, op.Resume(new(1))); Equal(true, op.Completed); Equal(1, f.Redraws); Equal(Government.Anarchy, f.World.Civilizations[1].Government); }
            grants.Random.RequireChoicesConsumed();
        }
        var p = new PlacementContactFixture(); var life = p.Contact.F.T.T.Damage.Lifecycle;
        p.Data.AddUnit(5, 2, 4, new(30, 20)); var token = p.Data.Unit(5).Token; int redraw = 0;
        var service = new CivilizationRemoval(p.World, p.Data.Rules, p.Data.Stacks, life.Elimination, p.Contact.F.T.T.Damage.Incidents.Incidents, _ => p.Create(), () => ++redraw);
        using var retained = service.Begin(2, 0, 1, "retained"); Equal(true, retained.Resume() is LifecycleContact);
        Equal(token, p.Data.Unit(5).Token); Equal(null, retained.Resume(new(1))); Equal(true, retained.Completed);
        Equal(token, p.Data.Unit(5).Token); Equal(1, redraw); p.Consumed();
    }
    private static void RemovalRetainsForeignSpyLoss()
    {
        var f = new RemovalFixture(); var grants = new RestartGrantFixture(f.Data, owner: 3);
        f.Life.Recreate = owner => { Equal(3, owner); return grants.Create(); };
        f.Data.AddUnit(0, 3, 0, new(12, 8)); f.World.Civilizations[3].UnitsActive[0] = 1;
        f.Data.Unit(0).Flags.SpyMission = true; f.Data.Unit(0).Counter = 2;
        f.Data.AddUnit(1, 2, 4, new(16, 8));
        using var op = f.Service.Begin(2, 1, 0, "nested"); var pending = op.Resume(); grants.Pending(pending);
        Equal(false, f.Data.Unit(0).Token.Occupied); Equal(false, f.Data.Unit(0).Flags.SpyMission);
        Equal(true, f.Data.Unit(1).Token.Occupied); Equal("password2", f.World.Multiplayer.Saved.Passwords[2]);
        Equal(0, f.Redraws); Equal(pending, op.Resume());
        Equal(null, op.Resume(new(0))); Equal(true, op.Completed); Equal(false, f.Data.Unit(1).Token.Occupied);
        Equal(1, f.Life.Restarts); Equal(0, f.Creations); Equal(1, f.Redraws); grants.Random.RequireChoicesConsumed();
    }
    private static void ScenarioDestructionAdmission()
    {
        foreach (int selector in new[] { -4, -3, 0, 2, 8 })
        {
            var f = new RemovalFixture(); f.World.Globals.HumanPlayers = CivilizationSet.Only(2); f.World.Multiplayer.LocalHuman = 2;
            var entry = f.World.Events.Collection.Append(); entry.Values.DestroyCiv = selector; entry.Values.WhoCiv = 2; entry.Values.WhomCiv = -3;
            f.Data.AddUnit(0, 2, 4, new(10, 8));
            using var op = new ScenarioDestruction(f.World, f.Service).Begin(entry, "event"); Equal(null, op.Resume());
            bool accepted = selector is -3 or 2; Equal(accepted ? 1 : 0, op.Result);
            Equal(accepted ? EndReason.Defeat : EndReason.None, f.World.PendingEndReason); Equal(!accepted, f.Data.Unit(0).Token.Occupied);
            Equal(0, f.Creations); Equal(0, f.Redraws);
        }
    }
    private static void ScenarioDestructionPrivateSession()
    {
        using var runtime = new SessionRuntime(new(), new(255)); var f = new RemovalFixture(data: new(runtime.Process, runtime.World));
        var grants = new RestartGrantFixture(f.Data); f.Create = _ => grants.Create();
        var entry = f.World.Events.Collection.Append(); entry.Values.DestroyCiv = 2; ParticipantId recipient = default;
        runtime.RegisterCommand(new(new(1, "test.scenario-destroy", "Scenario destruction", AccessClass.Player, new([]), new([new("result", ValueKind.Integer)])),
            (_, _, _) => new(), _ => new ScenarioDestructionOperation(new ScenarioDestruction(f.World, f.Service).Begin(entry, "event"), _ => recipient)));
        runtime.BeginClientSession(new()); var actor = SessionClient(runtime, 2); var human = SessionClient(runtime, 1); recipient = human.Identity.Participant;
        var command = actor.Submit(new("test.scenario-destroy", new([]))).Correlation.Command; runtime.AdvanceOne();
        var prompt = RequiredInteraction(human, command); Equal(false, actor.Reply(prompt, 1).Accepted); Equal(null, RequiredResult(actor, command).Interaction);
        entry.Values.DestroyCiv = 1; Equal(true, human.Reply(prompt, 1).Accepted); runtime.AdvanceOne();
        Equal(CommandStatus.Completed, RequiredResult(actor, command).Status); Equal(null, human.Result(command)); Equal(false, human.Reply(prompt, 1).Accepted);
        Equal(true, f.World.Globals.CivsAlive.Contains(1)); Equal(Government.Anarchy, f.World.Civilizations[1].Government);
        Equal(1, f.Creations); Equal(1, f.Redraws); grants.Random.RequireChoicesConsumed();
        foreach (var input in new[] { OperationInputKind.Cancel, OperationInputKind.Timeout })
        {
            f = new(); grants = new(f.Data); f.Create = _ => grants.Create(); f.FailRedraw = true;
            var context = new OperationContext(f.World, f.Data.Process, grants.Random, new PresentationRandom(1, "removal"), new(default, new(1)), default, (_, _) => { }, () => { });
            using var op = new CivilizationRemovalOperation(f.Service.Begin(2, 1, 1, "remove"), c => new((ulong)c));
            Equal(CommandStatus.AwaitingInteraction, op.Resume(context, new()).Status); var result = op.Resume(context, new(input));
            Equal(CommandStatus.Failed, result.Status); Equal("civilization_removal_presentation_failed", result.Code);
            Equal(Government.Despotism, f.World.Civilizations[1].Government); Equal(1, f.Redraws); Equal(result, op.Resume(context, new(input))); grants.Random.RequireChoicesConsumed();
        }
    }
}
