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
    // U01 §5; S02 §9. Expected results come from the specifications, with actual U01/W03/V04 children.
    private static UnitCreation RetainedCreation(ContactFixture f) => new(f.World, f.Data.Process, f.Data.Stacks, ContactRevelation(f), f.Data);
    private static ContactFixture CreationContact(RevelationFixture? data = null)
    {
        var f = new ContactFixture(data: data);
        f.Data.Rules.Units[10] = f.Data.Rules.Units[0] with { Domain = RuleDomain.Land, Role = RuleRole.Attack };
        f.Data.AddUnit(10, 2, 10, new(22, 12));
        f.World.Civilizations[1].Treaties[2].Contact = false;
        return f;
    }
    private static void UnitCreationRetainedContact()
    {
        var f = CreationContact(); var d = f.Data; f.World.Globals.GameOptions.Tutorial = true;
        using var op = RetainedCreation(f).Begin(new(10), new(1), new(20, 12), "create");
        Equal(true, op.Resume() is DiplomaticContactDecision.Audience { Value: DiplomaticAudienceDecision.Emissary });
        int created = d.Stacks.FirstAt(new(20, 12)); var token = d.Unit(created).Token;
        Equal(new UnitType(10), d.Unit(created).Type); Equal((byte)0, d.Unit(created).MovesUsed); Equal((byte)0, d.Unit(created).Damage);
        Equal(true, d.Unit(created).Visibility.Contains(2)); Equal(false, op.Completed); Equal(null, op.Result); Equal(0, d.Notices.Count);
        // Tutorials use the requested type with live rules after the actual contact decision.
        d.Rules.Units[10] = d.Rules.Units[10] with { Domain = RuleDomain.Sea }; d.Unit(created).Type = new(0);
        op.Resume(new(1)); Equal(true, op.Completed); Equal(new UnitSlot((ushort)created), op.Result);
        Equal(token, d.Unit(created).Token); Sequence(new[] { UnitCreationNotice.Ships }, d.Notices);
        Equal(true, f.World.Globals.ShownEventFlags.Ships); Equal(0, d.Meetings.Count); Equal(null, op.Resume(new(1)));
        f.F.T.T.Random.RequireChoicesConsumed();
    }
    private static void UnitCreationRetainedBoundaries()
    {
        var f = CreationContact(); var creator = RetainedCreation(f); uint next = f.Data.Process.Tokens.NextUnit;
        Throws<InvalidOperationException>(() => creator.Create(new(10), new(1), new(20, 12)));
        Equal(next, f.Data.Process.Tokens.NextUnit); Equal(-1, f.Data.Stacks.FirstAt(new(20, 12)));
        using var held = creator.Begin(new(10), new(1), new(20, 12), "create"); held.Resume();
        int created = f.Data.Stacks.FirstAt(new(20, 12)); held.Dispose(); Throws<ObjectDisposedException>(() => held.Resume(new(1)));
        Equal(true, f.Data.Unit(created).Token.Occupied); Equal(0, f.Data.Notices.Count); Equal(false, f.World.Civilizations[1].Treaties[2].Contact);
        var failed = CreationContact(); failed.World.Events.Loaded = true; NegotiationEvent(failed.World, 1, 2); failed.F.FailEvent = true;
        using var op = RetainedCreation(failed).Begin(new(10), new(1), new(20, 12), "create");
        Throws<InvalidOperationException>(() => op.Resume()); Throws<InvalidOperationException>(() => op.Resume());
        Equal(true, failed.Data.Unit(failed.Data.Stacks.FirstAt(new(20, 12))).Visibility.Contains(2)); Equal(0, failed.Data.Notices.Count);
    }
    private static Event CreateUnitEvent(RevelationFixture d, int owner = 1, int type = 0, MapPoint? point = null)
    {
        d.World.Globals.CivsAlive = d.World.Globals.CivsAlive.With(owner & 31);
        var e = d.World.Events.Collection.Append(); e.Values.CreateOwnerCiv = owner; e.Values.CreateUnitType = type;
        e.Values.CreateLocationCount = 1; e.Values.CreateLocations[0] = point ?? new(20, 12);
        return e;
    }
    private static ScenarioUnitActions UnitActions(RevelationFixture d) => new(d.World, d.Rules, new(d.World, d.Process, d.Stacks, d));
    private static int ExecuteCreation(ScenarioUnitActions actions, Event e)
    {
        using var op = actions.BeginCreate(e, "scenario"); Equal(null, op.Resume()); Equal(true, op.Completed); return op.Result!.Value;
    }
    private static void ScenarioUnitCreationLocations()
    {
        foreach (int variant in Enumerable.Range(0, 8))
        {
            var d = new RevelationFixture(); var point = new MapPoint(20, 12); int type = variant < 3 ? 1 : variant == 5 ? 2 : 0;
            var e = CreateUnitEvent(d, type: type); bool accepted = variant is 0 or 1 or 5 or 7;
            if (variant is 0 or 4 or 5) { d.World.Map.TileAt(point).Terrain = Terrain.Ocean; }
            if (variant == 0) { d.AddUnit(0, 2, 0, point); } // Sea on ocean deliberately ignores enemy occupancy.
            if (variant is 1 or 2 or 3)
            { d.AddCity(0, variant == 3 ? 2 : 1, point); d.World.Entities.Cities.Storage[0].Flags.Coastal = variant == 1; }
            if (variant == 6) { d.AddUnit(0, 2, 0, point); }
            if (variant == 7) { d.AddUnit(0, 1, 0, point); }
            Equal(accepted ? 1 : 0, ExecuteCreation(UnitActions(d), e));
        }
        var f = new RevelationFixture(); var entry = CreateUnitEvent(f); entry.Values.CreateLocationCount = 3;
        entry.Values.CreateLocations[0] = new(-1, -1); entry.Values.CreateLocations[1] = new(20, 12); entry.Values.CreateLocations[2] = new(24, 12);
        Equal(1, ExecuteCreation(UnitActions(f), entry)); Equal(new MapPoint(20, 12), f.Unit(0).Position);
        var empty = new RevelationFixture(); entry = CreateUnitEvent(empty); entry.Values.CreateLocationCount = 0;
        Equal(0, ExecuteCreation(UnitActions(empty), entry)); Equal(0, empty.World.Entities.Units.End);
        entry.Values.CreateLocationCount = 11;
        for (int i = 0; i < 10; ++i) { entry.Values.CreateLocations[i] = new(-1, -1); }
        using var invalid = UnitActions(empty).BeginCreate(entry, "invalid"); Throws<InvalidOperationException>(() => invalid.Resume());
        Equal(0, empty.World.Entities.Units.End);
    }
    private static void ScenarioUnitCreationHomeAndSelectors()
    {
        foreach (bool firstForeign in new[] { false, true })
        {
            var d = new RevelationFixture(); var e = CreateUnitEvent(d); e.Values.CreateOwnerCiv = -3; e.Values.WhoCiv = 1; e.Values.CreateVeteran = true;
            d.AddCity(0, firstForeign ? 2 : 1, new(20, 10)); d.AddCity(1, 1, new(24, 12));
            d.World.Entities.Cities.Storage[0].Name = "HOME"; d.World.Entities.Cities.Storage[1].Name = "home";
            e.SetString(ScenarioUnitActions.HomeNameField, new("Home"));
            Equal(1, ExecuteCreation(UnitActions(d), e)); Equal(true, d.Unit(0).Flags.Veteran);
            Equal(firstForeign ? null : new CitySlot(0), d.Unit(0).Home);
        }
        var limit = new RevelationFixture(); var last = CreateUnitEvent(limit);
        limit.AddCity(255, 1, new(20, 10)); limit.World.Entities.Cities.Storage[255].Name = "Last";
        last.SetString(ScenarioUnitActions.HomeNameField, new("Last"));
        Equal(1, ExecuteCreation(UnitActions(limit), last)); Equal(null, limit.Unit(0).Home);
        var barbarian = new RevelationFixture(); var b = CreateUnitEvent(barbarian, 0);
        Equal(1, ExecuteCreation(UnitActions(barbarian), b)); Equal(new CivilizationSlot(0), barbarian.Unit(0).Owner);
        var missing = new RevelationFixture(); var e2 = CreateUnitEvent(missing); e2.Values.CreateOwnerCiv = -4; e2.Values.WhomCiv = -3;
        Equal(0, ExecuteCreation(UnitActions(missing), e2)); // Resolve only once; no invented recursive Who lookup.
    }
    private static void ScenarioUnitCreationActualContact()
    {
        foreach (bool dispose in new[] { false, true })
        {
            var f = CreationContact(); var e = CreateUnitEvent(f.Data, type: 10);
            f.Data.AddCity(5, 1, new(20, 10)); f.World.Entities.Cities.Storage[5].Name = "First";
            f.Data.AddCity(6, 1, new(26, 12)); f.World.Entities.Cities.Storage[6].Name = "Later";
            e.SetString(ScenarioUnitActions.HomeNameField, new("First"));
            using var op = new ScenarioUnitActions(f.World, f.Data.Rules, RetainedCreation(f)).BeginCreate(e, "scenario");
            Equal(true, op.Resume() is DiplomaticContactDecision.Audience); int created = f.Data.Stacks.FirstAt(new(20, 12));
            Equal(new CitySlot(5), f.Data.Unit(created).Home); Equal(false, f.Data.Unit(created).Flags.Veteran);
            e.Values.CreateVeteran = true; e.SetString(ScenarioUnitActions.HomeNameField, new("later")); e.Values.CreateOwnerCiv = 2;
            if (dispose)
            { op.Dispose(); Throws<ObjectDisposedException>(() => op.Resume(new(1))); Equal(new CitySlot(5), f.Data.Unit(created).Home); Equal(false, f.Data.Unit(created).Flags.Veteran); }
            else
            { op.Resume(new(1)); Equal(1, op.Result); Equal(new CitySlot(6), f.Data.Unit(created).Home); Equal(true, f.Data.Unit(created).Flags.Veteran); Equal(new CivilizationSlot(1), f.Data.Unit(created).Owner); }
            f.F.T.T.Random.RequireChoicesConsumed();
        }
    }
    private static Event MoveUnitEvent(RevelationFixture d)
    {
        d.World.Globals.CivsAlive = CivilizationSet.Only(2);
        var e = d.World.Events.Collection.Append(); e.Values.MoveOwnerCiv = 2; e.Values.MoveUnitType = -2; e.Values.MoveCount = -2;
        e.Values.MoveRectangle[0] = new(4, 4); e.Values.MoveRectangle[1] = new(30, 4);
        e.Values.MoveRectangle[2] = new(30, 20); e.Values.MoveRectangle[3] = new(4, 20); e.Values.MoveTo = new(65537, -65538);
        return e;
    }
    private static void ScenarioMoveUnitSlotsAndCounts()
    {
        foreach (int quantity in new[] { -2, -3, 0, 1, 2, 9 })
        {
            var d = new RevelationFixture(); var e = MoveUnitEvent(d); e.Values.MoveCount = quantity;
            for (int i = 0; i < 3; ++i) { d.AddUnit(i, 2, 0, new(10 + i * 2, 10)); }
            int expected = quantity is -2 or 9 ? 3 : quantity == 2 ? 2 : 1;
            Equal(expected, UnitActions(d).Move(e));
            for (int i = 0; i < 3; ++i)
            {
                Equal(new MapPoint(10 + i * 2, 10), d.Unit(i).Position); Equal((byte)0, d.Unit(i).MovesUsed);
                Equal(i >= 3 - expected ? UnitOrder.GoTo : UnitOrder.None, d.Unit(i).Orders);
                if (i >= 3 - expected) { Equal((byte)55, d.Unit(i).AiTask); Equal(new MapPoint(1, -2), d.Unit(i).Destination); }
            }
        }
        var f = new RevelationFixture(); var entry = MoveUnitEvent(f); f.AddUnit(0, 2, 2, new(10, 10)); f.AddUnit(2, 2, 0, new(12, 10));
        entry.Values.MoveUnitType = 2; Equal(1, UnitActions(f).Move(entry)); Equal(UnitOrder.GoTo, f.Unit(2).Orders); Equal(UnitOrder.None, f.Unit(0).Orders);
    }
    private static void ScenarioMoveUnitEligibility()
    {
        var d = new RevelationFixture(); var e = MoveUnitEvent(d);
        for (int i = 0; i < 9; ++i) { d.AddUnit(i, 2, 0, new(10, 10)); }
        d.Unit(0).Orders = UnitOrder.Sleeping; d.Unit(1).Orders = UnitOrder.AiIdle; d.Unit(2).Orders = UnitOrder.GoTo;
        d.Unit(3).Owner = new(1); d.Unit(4).Position = new(-1, -1); d.Unit(5).Orders = (UnitOrder)(-128);
        d.Rules.Units[4] = d.Rules.Units[0] with { Attack = 99 }; d.Unit(6).Type = new(4);
        d.Rules.Units[5] = d.Rules.Units[0] with { Attack = 255 }; d.Unit(7).Type = new(5); d.Unit(8).Orders = UnitOrder.Fortified;
        Equal(3, UnitActions(d).Move(e)); Equal(UnitOrder.GoTo, d.Unit(5).Orders); Equal(UnitOrder.GoTo, d.Unit(7).Orders); Equal(UnitOrder.GoTo, d.Unit(8).Orders);
        var f = new RevelationFixture(); e = MoveUnitEvent(f); f.AddUnit(0, 2, 0, new(10, 10));
        e.Values.MoveRectangle[2] = new(9, 20); Equal(0, UnitActions(f).Move(e));
        e.Values.MoveRectangle[2] = new(30, 20); e.Values.MoveRectangle[1] = new(30, 11); Equal(0, UnitActions(f).Move(e));
        e.Values.MoveRectangle[1] = new(30, 4); f.World.Globals.HumanPlayers = f.World.Globals.HumanPlayers.With(2); Equal(0, UnitActions(f).Move(e));
        f.World.Globals.HumanPlayers = default; e.Values.MoveOwnerCiv = 34; Equal(0, UnitActions(f).Move(e)); // Membership aliases; owner comparison does not.
        e.Values.MoveOwnerCiv = -4; e.Values.WhomCiv = 2; Equal(1, UnitActions(f).Move(e));
    }
    private static void ScenarioCreationPrivateSession()
    {
        using var runtime = new SessionRuntime(new(), new(255));
        var f = CreationContact(CreationFixture(new(runtime.Process, runtime.World))); var e = CreateUnitEvent(f.Data, type: 10); e.Values.CreateVeteran = true;
        ParticipantId recipient = default;
        runtime.RegisterCommand(new(new(1, "test.scenario-create", "Authored scenario unit creation", AccessClass.Player, new([]), new([new("result", ValueKind.Integer)])),
            (_, _, _) => new(), _ => new ScenarioUnitCreationOperation(new ScenarioUnitActions(f.World, f.Data.Rules, RetainedCreation(f)).BeginCreate(e, "scenario"),
                _ => recipient, _ => throw new InvalidOperationException())));
        runtime.BeginClientSession(new()); var actor = SessionClient(runtime, 2); var local = SessionClient(runtime, 1); recipient = local.Identity.Participant;
        var command = actor.Submit(new("test.scenario-create", new([]))).Correlation.Command; runtime.AdvanceOne();
        var prompt = RequiredInteraction(local, command); int created = f.Data.Stacks.FirstAt(new(20, 12));
        Equal(false, actor.Reply(prompt, 1).Accepted); Equal(null, RequiredResult(actor, command).Interaction); Equal(false, f.Data.Unit(created).Flags.Veteran);
        Equal(true, local.Reply(prompt, 1).Accepted); runtime.AdvanceOne();
        Equal(CommandStatus.Completed, RequiredResult(actor, command).Status); Equal(null, local.Result(command)); Equal(true, f.Data.Unit(created).Flags.Veteran);
        f.F.T.T.Random.RequireChoicesConsumed();
        foreach (var input in new[] { OperationInputKind.Cancel, OperationInputKind.Timeout })
        {
            f = CreationContact(); f.F.C.FailClose = true; e = CreateUnitEvent(f.Data, type: 10); e.Values.CreateVeteran = true;
            var context = new OperationContext(f.World, f.Data.Process, f.F.T.T.Random, new PresentationRandom(1, "create"), new(default, new(1)), default, (_, _) => { }, () => { });
            using var op = new ScenarioUnitCreationOperation(new ScenarioUnitActions(f.World, f.Data.Rules, RetainedCreation(f)).BeginCreate(e, "scenario"), c => new((ulong)c), _ => throw new InvalidOperationException());
            Equal(CommandStatus.AwaitingInteraction, op.Resume(context, new()).Status);
            var result = op.Resume(context, new(input)); Equal(CommandStatus.Failed, result.Status); Equal("scenario_creation_presentation_failed", result.Code);
            Equal(true, f.Data.Unit(f.Data.Stacks.FirstAt(new(20, 12))).Flags.Veteran); Equal(1, f.F.C.Closed); f.F.T.T.Random.RequireChoicesConsumed();
        }
    }
}
