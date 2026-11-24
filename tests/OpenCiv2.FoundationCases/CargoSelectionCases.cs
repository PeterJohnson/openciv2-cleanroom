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
    private static Choice CargoDraw(bool fallback, int code, string call = "cargo") => Choice.Create(
        new(fallback ? "C07.cargo-fallback" : "C07.cargo-start", call), new SamplingLaw.PrimaryModulo(3), ChoiceCode.Primary(code));

    private static void CargoNames(RevelationFixture data)
    {
        for (int i = 0; i < 16; ++i) data.Rules.Commodities[i] = new("cargo" + i, "Cargo " + i);
    }

    // C07 published selection vectors, including the signed-byte fallback boundary.
    private static void CargoAiSelection()
    {
        foreach (int variant in new[] { 0, 1, 2, 3 })
        {
            var f = CommodityFixture(); f.AddUnit(0, 1, 0, new(8, 6));
            ref var city = ref f.World.Entities.Cities.Storage[0];
            city.Supplies[0] = -1; city.Supplies[1] = variant == 0 ? (sbyte)4 : (sbyte)-4;
            city.Supplies[2] = variant == 0 ? (sbyte)6 : variant == 2 ? sbyte.MinValue : variant == 3 ? (sbyte)0 : (sbyte)-6;
            city.Flags.TradeDirty = false;
            var supplies = ((ReadOnlySpan<sbyte>)city.Supplies).ToArray();
            var choices = variant is 0 or 3 ? new List<Choice> { CargoDraw(false, 0) }
                : [CargoDraw(false, 1), CargoDraw(true, 2)];
            var random = new GameplayRandom(new ControlledChoices(new(choices)), "cargo");
            new CargoSelection(f.World, f.Rules, random, "Food").SelectAi(new(0), new(0), "cargo");
            Equal((byte)(variant switch { 0 => 4, 1 => 6, 2 => 128, _ => 0 }), f.Unit(0).Counter);
            Equal(true, city.Flags.HadCaravan); Equal(false, city.Flags.TradeDirty);
            Sequence(supplies, ((ReadOnlySpan<sbyte>)city.Supplies).ToArray()); random.RequireChoicesConsumed();
        }
    }

    private static void CargoHumanLiveSelection()
    {
        var f = CommodityFixture(); CargoNames(f); f.AddUnit(0, 1, 0, new(8, 6));
        ref var city = ref f.World.Entities.Cities.Storage[0];
        city.Supplies[0] = 0; city.Supplies[1] = -4; city.Supplies[2] = 3; city.Flags.TradeDirty = false;
        var random = new GameplayRandom(new ControlledChoices(new([])), "human-cargo");
        var selector = new CargoSelection(f.World, f.Rules, random, "Food");
        using var op = selector.BeginHuman(new(0), 1, new(0));
        var menu = (CargoMenu)op.Resume()!; Sequence(new[] { 0, 2, 3 }, menu.Options.Select(o => o.Slot));
        Equal(menu, op.Resume()); Equal((byte)0, f.Unit(0).Counter);
        var report = (CargoReport)op.Resume(new(2, 1))!;
        Equal("Cargo 3", report.Name); Equal(1, report.Cities.Count); Equal(false, report.Cities[0].Demand);
        Equal(true, op.Resume(new(0)) is CargoMenu); // Report's city0 is not a cargo selection.
        Equal("Cargo 3", ((CargoConfirmation)op.Resume(new(2))!).Name);
        city.Name = "Changed"; city.Supplies[1] = 5;
        menu = (CargoMenu)op.Resume(new(1))!; Equal("Changed", menu.CityName); Equal(4, menu.Options.Count);
        op.Resume(new(2)); city.Supplies[2] = -6; // Acceptance consumes the live entry, not the displayed name.
        Equal(null, op.Resume(new(0, 1))); Equal(true, op.Completed); Equal(true, op.ZoomRequested);
        Equal((byte)6, f.Unit(0).Counter); Equal((sbyte)-6, city.Supplies[2]); Equal(false, city.Flags.HadCaravan);
        Equal(false, city.Flags.TradeDirty); city.Supplies[2] = 9;
        Equal(null, op.Resume(new(0))); Equal((sbyte)9, city.Supplies[2]); Equal((byte)6, f.Unit(0).Counter);
        foreach (int slot in new[] { 0, 3, 9 })
        {
            using var next = selector.BeginHuman(new(0), 1, new(0)); next.Resume();
            if (slot == 3)
            {
                Equal(true, ((CargoReport)next.Resume(new(3, 1))!).Food); next.Resume(new(0));
            }
            next.Resume(new(slot)); next.Resume(new(0));
            Equal(slot == 0 ? (byte)0 : CargoSelection.Food, f.Unit(0).Counter); Equal((sbyte)0, city.Supplies[0]);
        }
        random.RequireChoicesConsumed();
    }

    private static void CargoReportPrivacy()
    {
        var f = CommodityFixture();
        for (ushort i = 1; i < 5; ++i) f.AddCity(i, 2, new((short)(8 + 2 * i), 6), (sbyte)(7 + i));
        for (int i = 0; i < 5; ++i)
        {
            ref var city = ref f.World.Entities.Cities.Storage[i]; city.Name = "City " + i;
            city.Demands[0] = city.Demands[1] = 3; city.Supplies[0] = city.Supplies[1] = 3;
        }
        f.World.Entities.Cities.Storage[1].RememberedSize[1] = 2;
        BuildingQueries.SetBuilding(f.World, new(2), BuildingIds.Palace, true);
        f.World.Entities.Cities.Storage[2].RememberedSize[1] = 4;
        f.World.Entities.Cities.Storage[4].Demands[0] = f.World.Entities.Cities.Storage[4].Demands[1] = -3;
        f.World.Entities.Cities.Storage[4].RememberedSize[1] = 5;
        var rows = CommodityReport.Cities(f.World, 1, 3);
        Sequence(new[] { 0, 1, 2, 0 }, rows.Select(r => (int)r.City.Slot.Value));
        Sequence(new[] { 3, 2, 9, 3 }, rows.Select(r => r.Size));
        Sequence(new[] { true, true, true, false }, rows.Select(r => r.Demand));
        Equal(null, rows[0].Adjective); Equal(f.World.Names[2].Adjective, rows[1].Adjective);
        f.World.Globals.RevealMap = 1;
        rows = CommodityReport.Cities(f.World, 1, 3);
        Sequence(new[] { 0, 1, 2, 3, 0, 1, 2, 3, 4 }, rows.Select(r => (int)r.City.Slot.Value));
        Equal(8, rows[1].Size); Equal((sbyte)-3, f.World.Entities.Cities.Storage[4].Demands[0]);
    }

    private static void CargoSelectionStops()
    {
        var f = CommodityFixture(); CargoNames(f); f.AddUnit(0, 1, 0, new(8, 6)); f.Unit(0).Counter = 14;
        var random = new GameplayRandom(new ControlledChoices(new([])), "stop");
        var selector = new CargoSelection(f.World, f.Rules, random, "Food");
        using var op = selector.BeginHuman(new(0), 1, new(0)); op.Resume();
        Throws<InvalidOperationException>(() => op.Resume(new(-1)));
        Equal((byte)14, f.Unit(0).Counter); Equal(false, op.Completed);
        Throws<InvalidOperationException>(() => op.Resume(new(3)));
        using var disposed = selector.BeginHuman(new(0), 1, new(0)); disposed.Resume(); disposed.Resume(new(0)); disposed.Dispose();
        Throws<ObjectDisposedException>(() => disposed.Resume(new(0))); Equal((byte)14, f.Unit(0).Counter);
        random.RequireChoicesConsumed();
    }

    private static void CargoProductionParent()
    {
        foreach (bool human in new[] { true, false })
        {
            var life = new EliminationFixture(human ? [] : [CargoDraw(false, 0, "production/cargo")]);
            var f = new CityProductionFixture(units: new(life, retainedCargo: true)); CargoNames(f.Units.Data);
            f.Rules.Units[4] = f.Rules.Units[4] with { Role = RuleRole.Trade };
            f.City.ShieldBox = 29; f.City.Supplies[0] = -1; f.City.Supplies[1] = 4; f.City.Supplies[2] = 6;
            if (!human) f.World.Globals.HumanPlayers = default;
            if (!human) f.Units.Choose = _ => Equal((byte)4, life.Data.Unit(0).Counter);
            var incidents = new IncidentFixture(life.Random, life.Data);
            using var op = f.Production.Begin(new(0), "production", life.Elimination, incidents.Incidents);
            Equal(null, op.Resume()); Equal(1, f.World.Entities.Units.End); Equal(0, f.Units.CargoSelections);
            if (human)
            {
                Equal(false, op.Finished); Equal((short)13, f.City.ShieldBox);
                Equal(true, ((UnitProductionCargo)op.UnitDecision!).Decision is CargoMenu);
                uint next = life.Data.Process.Tokens.NextUnit; op.Resume(); Equal(next, life.Data.Process.Tokens.NextUnit);
                op.Resume(new(1)); Equal(false, op.Finished); op.Resume(new(0));
                Equal((sbyte)-4, f.City.Supplies[1]); Equal((short)0, f.City.ShieldBox);
            }
            else Equal(true, f.City.Flags.HadCaravan);
            Equal(true, op.Finished); Equal((byte)4, life.Data.Unit(0).Counter);
            Equal(UnitProductionOutcome.Completed, op.Result!.Unit!.Outcome); life.Random.RequireChoicesConsumed();
        }
    }

    private static void CargoProductionPrivateSession()
    {
        using var runtime = new SessionRuntime(new(), new(271));
        var data = CreationFixture(new(runtime.Process, runtime.World));
        var life = new EliminationFixture(data: data);
        var f = new CityProductionFixture(units: new(life, retainedCargo: true)); CargoNames(data);
        f.Rules.Units[4] = f.Rules.Units[4] with { Role = RuleRole.Trade };
        f.City.ShieldBox = 29; f.City.Supplies[0] = 3; f.City.Supplies[1] = -4; f.City.Supplies[2] = 5;
        var incidents = new IncidentFixture(life.Random, data);
        ParticipantId ownerId = default;
        runtime.RegisterCommand(new(new(1, "test.cargo", "Cargo", AccessClass.Player, new([]), new([])),
            (_, _, _) => new(), _ => new CityProductionOperation(f.Production.Begin(new(0), "production", life.Elimination, incidents.Incidents), _ => ownerId)));
        runtime.BeginClientSession(new()); var owner = SessionClient(runtime, 1); var observer = SessionClient(runtime, 2); ownerId = owner.Identity.Participant;
        var command = owner.Submit(new("test.cargo", new([]))).Correlation.Command; runtime.AdvanceOne();
        var menu = RequiredInteraction(owner, command); Equal(false, observer.Reply(menu, 0).Accepted);
        Equal(true, owner.Reply(menu, 4).Accepted); runtime.AdvanceOne();
        var report = RequiredInteraction(owner, command); Equal(true, owner.Reply(report, 0).Accepted); runtime.AdvanceOne();
        menu = RequiredInteraction(owner, command); Equal(true, owner.Reply(menu, 2).Accepted); runtime.AdvanceOne();
        var confirm = RequiredInteraction(owner, command); Equal(true, owner.Reply(confirm, 1).Accepted); runtime.AdvanceOne();
        Equal((short)13, f.City.ShieldBox); Equal((sbyte)5, f.City.Supplies[2]);
        menu = RequiredInteraction(owner, command); Equal(true, owner.Reply(menu, 2).Accepted); runtime.AdvanceOne();
        confirm = RequiredInteraction(owner, command); Equal(true, owner.Reply(confirm, 0).Accepted); runtime.AdvanceOne();
        Equal(CommandStatus.Completed, RequiredResult(owner, command).Status); Equal(false, owner.Reply(confirm, 0).Accepted);
        Equal((byte)5, data.Unit(0).Counter); Equal((sbyte)-5, f.City.Supplies[2]); Equal((short)0, f.City.ShieldBox);
        Equal(1, RequiredResult(owner, command).Committed.Count(e => e.Name == "city.production.completed"));
    }

    private static void CargoProductionDismissal()
    {
        foreach (var kind in new[] { OperationInputKind.Cancel, OperationInputKind.Timeout })
        {
            var f = new CityProductionFixture(units: new(retainedCargo: true)); var life = f.Units.Lifecycle;
            CargoNames(life.Data); f.Rules.Units[4] = f.Rules.Units[4] with { Role = RuleRole.Trade };
            f.City.ShieldBox = 29; f.City.Supplies[0] = 3;
            var incidents = new IncidentFixture(life.Random, life.Data);
            var context = OutcomeContext(life.Data);
            using var op = new CityProductionOperation(f.Production.Begin(new(0), "production", life.Elimination, incidents.Incidents), c => new((ulong)c));
            Equal(CommandStatus.AwaitingInteraction, op.Resume(context, new()).Status);
            op.Resume(context, new(OperationInputKind.Choice, 0));
            var menu = op.Resume(context, new(kind)); Equal(CommandStatus.AwaitingInteraction, menu.Status);
            Equal(true, menu.Interaction!.Options!.Any(o => o.Value == 4));
            op.Resume(context, new(OperationInputKind.Choice, 4));
            Equal(CommandStatus.AwaitingInteraction, op.Resume(context, new(kind)).Status);
            Throws<InvalidOperationException>(() => op.Resume(context, new(kind)));
            Equal(1, f.World.Entities.Units.End); Equal((short)13, f.City.ShieldBox); Equal((sbyte)3, f.City.Supplies[0]);
            Equal((byte)0, life.Data.Unit(0).Counter); Equal(null, op.Result);
            Throws<InvalidOperationException>(() => op.Resume(context, new(OperationInputKind.Choice, 3)));
            Equal((uint)2, life.Data.Process.Tokens.NextUnit);
        }
    }
}
