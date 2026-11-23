using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // U05 Player airlift: authored specification expectations, actual U01/V01/V06 children.
    private sealed class PlayerAirliftFixture : IPlayerAirliftEffects
    {
        public EliminationFixture Life { get; }
        public RevelationFixture Data => Life.Data;
        public GameState World => Data.World;
        public PlayerAirlift Airlift { get; }
        public List<AirliftNotice> Notices { get; } = [];
        public Action? Changed;
        public Action<AirliftNotice>? OnNotice;
        public int Changes;
        public PlayerAirliftFixture(List<Choice>? choices = null, params int[] types)
        {
            Life = new(choices, CargoFixture(types.Length == 0 ? [0] : types));
            World.Globals.ActiveUnit = new(0);
            Data.AddCity(0, 1, CargoPoint); Data.AddCity(2, 1, new(20, 10));
            World.Entities.Cities.Storage[0].Name = "Origin"; World.Entities.Cities.Storage[2].Name = "Destination";
            BuildingQueries.SetBuilding(World, new(0), BuildingIds.Airport, true);
            BuildingQueries.SetBuilding(World, new(2), BuildingIds.Airport, true);
            for (int i = 0; i < World.Entities.Units.End; ++i)
            { ++World.Civilizations[1].UnitsActive[Data.Unit(i).Type.Value]; ++World.UnitTotals[1]; }
            var incidents = new IncidentFixture(Life.Random, Data);
            Airlift = new(World, Data.Rules, Data.Stacks, Life.Random, Life.Elimination, incidents.Incidents, this);
        }
        public PlayerAirlift.Operation Begin() => Airlift.Begin("lift");
        public PlayerAirlift.Operation Direct(int risk = 0, int? enemy = null) => Airlift.BeginTransfer(new(0), new(0), new(2), risk, enemy, "lift");
        public void Fighter(int slot, int owner, MapPoint point, bool veteran = false)
        {
            Data.Rules.Units[9] = Data.Rules.Units[0] with { Role = RuleRole.AirSuperiority, Domain = RuleDomain.Land };
            Data.AddUnit(slot, owner, 9, point); Data.Unit(slot).Flags.Veteran = veteran;
            World.Civilizations[1].Treaties[owner].War = true;
        }
        public void Finish(PlayerAirlift.Operation op)
        { for (int i = 0; i < 100 && !op.Completed; ++i) Equal(null, op.Resume()); Equal(true, op.Completed); Life.Random.RequireChoicesConsumed(); }
        public void Notice(AirliftNotice notice) { Notices.Add(notice); OnNotice?.Invoke(notice); }
        public void CityUnitChanged(UnitSlot unit) { Equal(new UnitSlot(0), unit); ++Changes; Changed?.Invoke(); }
    }
    private static Choice LiftRoll(int ordinal, int roll) => Choice.Create(new("U05.player-airlift", "lift", ordinal), new SamplingLaw.PrimaryModulo(6), ChoiceCode.Primary(roll));

    private static void PlayerAirliftAdmissionAndSelection()
    {
        foreach (int variant in Enumerable.Range(0, 7))
        {
            var f = new PlayerAirliftFixture();
            if (variant == 0) f.World.Globals.ActiveUnit = null;
            if (variant is 1 or 2) f.Data.Unit(0).Type = new((byte)variant);
            if (variant == 3) f.World.Map.TileAt(CargoPoint).Features &= ~MapFeatures.CitySite;
            if (variant == 4) BuildingQueries.SetBuilding(f.World, new(0), BuildingIds.Airport, false);
            if (variant == 5) f.World.Entities.Cities.Storage[0].Flags.Airlifted = true;
            if (variant == 6) BuildingQueries.SetBuilding(f.World, new(2), BuildingIds.Airport, false);
            using var op = f.Begin(); f.Finish(op); Equal(false, op.Transferred); Equal(0, f.Changes);
            if (variant == 0) Equal(0, f.Notices.Count);
            else Equal(new[] { AirliftNoticeKind.Ship, AirliftNoticeKind.Aircraft, AirliftNoticeKind.NoAirport, AirliftNoticeKind.NoAirport, AirliftNoticeKind.AlreadyUsed, AirliftNoticeKind.NoDestinations }[variant - 1], f.Notices.Single().Kind);
            if (variant is 3 or 4) Equal(variant == 3 ? (CitySlot?)null : new CitySlot(0), f.Notices.Single().DisplayCity);
        }
        var selection = new PlayerAirliftFixture(); selection.World.Entities.Cities.Storage[0].Owner = new(2);
        selection.Data.Unit(0).MovesUsed = 255; selection.Data.Unit(0).Flags.Paradropped = true;
        selection.Data.AddCity(1, 1, new(12, 12)); BuildingQueries.SetBuilding(selection.World, new(1), BuildingIds.Airport, true);
        selection.World.Entities.Cities.Storage[2].Flags.Airlifted = true;
        using var choose = selection.Begin(); var menu = (AirliftDecision.Destination)choose.Resume()!;
        Sequence(new ushort[] { 1, 2 }, menu.Cities.Select(c => c.City.Value)); Equal(menu, choose.Resume());
        choose.Resume(2); Equal(true, choose.Completed); Equal(false, choose.Transferred);
        Equal(AirliftNoticeKind.AlreadyUsed, selection.Notices.Single().Kind); Equal(new CitySlot(0), selection.Notices.Single().DisplayCity);
        Equal("Destination", selection.Notices.Single().Name); selection.Life.Random.RequireChoicesConsumed();
        var cancel = new PlayerAirliftFixture(); using var cancelled = cancel.Begin(); cancelled.Resume(); cancelled.Resume(-1);
        Equal(true, cancelled.Completed); Equal(false, cancel.World.Entities.Cities.Storage[0].Flags.Airlifted);
    }

    private static void PlayerAirliftRetainedSelection()
    {
        var f = new PlayerAirliftFixture(); using var op = f.Begin(); op.Resume();
        f.World.Globals.ActiveUnit = null; f.Data.Unit(0).Owner = new(3);
        f.World.Entities.Cities.Storage[2].Token = default; f.World.Entities.Cities.Storage[2].Owner = new(4);
        f.World.Entities.Cities.Storage[2].Position = new(22, 12);
        BuildingQueries.SetBuilding(f.World, new(2), BuildingIds.Airport, false);
        f.Fighter(1, 2, CargoPoint); // Captured owner 1 still determines threat direction after actor owner changes.
        var warning = (AirliftDecision.Fighters)op.Resume(2)!; Equal(1, warning.Actor); Equal(1, warning.Risk);
        op.Resume(0); Equal(true, op.Completed); Equal(false, op.Transferred);
        var noThreat = new PlayerAirliftFixture(); using var transfer = noThreat.Begin(); transfer.Resume();
        noThreat.World.Globals.ActiveUnit = null; noThreat.World.Entities.Cities.Storage[2].Token = default;
        noThreat.World.Entities.Cities.Storage[2].Owner = new(4); BuildingQueries.SetBuilding(noThreat.World, new(2), BuildingIds.Airport, false);
        noThreat.World.Entities.Cities.Storage[2].Position = new(22, 12); transfer.Resume(2); noThreat.Finish(transfer);
        Equal(new MapPoint(22, 12), noThreat.Data.Unit(0).Position); Equal(true, transfer.Transferred);
    }

    private static void PlayerAirliftThreatDirectionAndReach()
    {
        var f = new PlayerAirliftFixture([LiftRoll(0, 5), LiftRoll(1, 4), LiftRoll(2, 0)]);
        f.Fighter(1, 2, CargoPoint, true); f.Data.Unit(1).MovesUsed = 255; f.Data.Unit(1).Counter = 255;
        f.Fighter(2, 3, new(20, 10)); f.World.Names[3] = new() { Tribe = "Last fighters" };
        f.Fighter(3, 4, new(38, 22)); // Out of reach of both endpoints.
        f.Fighter(4, 5, CargoPoint); f.World.Civilizations[1].Treaties[5].War = false; f.World.Civilizations[5].Treaties[1].War = true;
        f.Fighter(5, 1, CargoPoint); // Same owner excluded even with a self-war flag.
        using var op = f.Begin(); op.Resume(); var warning = (AirliftDecision.Fighters)op.Resume(2)!;
        Equal(3, warning.Risk); Equal(0, f.Changes); Equal(0, f.Notices.Count);
        op.Resume(-1); f.Finish(op); Equal(true, op.ShotDown); Equal("Last fighters", f.Notices.Single().Name);
        Equal(AirliftNoticeKind.ShotDown, f.Notices.Single().Kind); Equal(false, f.Data.Unit(0).Token.Occupied);
        Equal(true, f.World.Entities.Cities.Storage[0].Flags.Airlifted && f.World.Entities.Cities.Storage[2].Flags.Airlifted);
        var zero = new PlayerAirliftFixture(); zero.Fighter(1, 2, CargoPoint); zero.Data.Rules.Cosmic = zero.Data.Rules.Cosmic with { RoadMovementMultiplier = 0 };
        using var failure = zero.Begin(); failure.Resume(); Throws<DivideByZeroException>(() => failure.Resume(2));
        Equal(false, zero.World.Entities.Cities.Storage[0].Flags.Airlifted); Equal(0, zero.Changes);
    }

    private static void PlayerAirliftExecutionAndLiveNotice()
    {
        foreach (int risk in new[] { -3, 0, 2 })
        {
            var f = new PlayerAirliftFixture(risk > 0 ? [LiftRoll(0, 1), LiftRoll(1, 5)] : [], 1, 0);
            f.Data.Unit(0).Orders = UnitOrder.GoTo; f.Data.Unit(0).Counter = 9; f.Data.Unit(0).Damage = 2;
            f.Data.Unit(1).Orders = UnitOrder.Sleeping; f.Data.Unit(1).Carrier = new(0);
            BuildingQueries.SetBuilding(f.World, new(0), BuildingIds.Airport, false); f.World.Entities.Cities.Storage[2].Flags.Airlifted = true;
            f.Changed = () =>
            {
                Equal(new MapPoint(20, 10), f.Data.Unit(0).Position); Equal(CargoPoint, f.Data.Unit(1).Position);
                Equal(true, f.World.Entities.Cities.Storage[0].Flags.Airlifted && f.World.Entities.Cities.Storage[2].Flags.Airlifted);
                Equal(UnitOrder.None, f.Data.Unit(0).Orders); Equal((byte)6, f.Data.Unit(0).MovesUsed);
                f.World.Entities.Cities.Storage[0].Name = "Live origin";
            };
            using var op = f.Direct(risk); f.Finish(op); Equal(true, op.Transferred); Equal(false, op.ShotDown);
            Equal("Live origin", f.Notices.Single().Name); Equal("Destination", f.Notices.Single().Destination);
            Equal((byte)9, f.Data.Unit(0).Counter); Equal((byte)2, f.Data.Unit(0).Damage); Equal(false, f.Data.Unit(0).Flags.Moved);
            Equal(UnitOrder.Sleeping, f.Data.Unit(1).Orders); Equal(new UnitSlot(0), f.Data.Unit(1).Carrier);
        }
        foreach (int variant in new[] { 0, 1, 2 })
        {
            var f = new PlayerAirliftFixture([LiftRoll(0, 0)]);
            f.Changed = () => { if (variant == 0) f.World.Multiplayer.Saved.AutoplayActive = true; if (variant == 1) f.World.Multiplayer.LocalHuman = 2; if (variant == 2) f.Data.Unit(0).Owner = new(2); };
            using var op = f.Direct(3); f.Finish(op); Equal(true, op.ShotDown); Equal(0, f.Notices.Count);
        }
        var sliced = new PlayerAirliftFixture(Enumerable.Range(0, 65).Select(i => LiftRoll(i, 1)).ToList());
        using var bounded = sliced.Direct(65); Equal(null, bounded.Resume()); Equal(false, bounded.Completed); Equal(1, sliced.Changes);
        sliced.Finish(bounded); Equal(1, sliced.Changes); Equal(1, sliced.Notices.Count);
    }

    private static void PlayerAirliftNestedDeletionAndSession()
    {
        var f = new PlayerAirliftFixture([LiftRoll(0, 0)]); CreationFixture(f.Data);
        f.Data.Unit(0).Type = new(0); f.World.Civilizations[1].CityCount = 0;
        // Direct transfer may use foreign airports. The actual V01 city scan must find no own city.
        f.World.Entities.Cities.Storage[0].Owner = new(2); f.World.Entities.Cities.Storage[2].Owner = new(2);
        f.World.Map.TileAt(CargoPoint).Owner = 2; f.World.Map.TileAt(new(20, 10)).Owner = 2;
        f.Data.AddUnit(1, 3, 4, new(24, 10)); f.Data.Unit(1).Flags.SpyMission = true; f.Data.Unit(1).Counter = 1;
        f.World.Civilizations[1].Treaties[3].Peace = true; f.Life.Recreate = c => Creation(f.Data, c, f.Life.Random);
        var context = new OperationContext(f.World, f.Data.Process, f.Life.Random, new PresentationRandom(1, "lift"),
            new(default, new(3)), default, (_, _) => { }, () => { });
        using var session = new PlayerAirliftOperation(f.Direct(1, 2), c => new((ulong)c));
        var pending = session.Resume(context, new()); Equal(CommandStatus.AwaitingInteraction, pending.Status);
        Equal(new ParticipantId(1), pending.Interaction!.Recipient); Equal(false, f.Data.Unit(0).Token.Occupied);
        Equal(1, f.Changes); Equal(AirliftNoticeKind.ShotDown, f.Notices.Single().Kind);
        var result = session.Resume(context, new(OperationInputKind.Cancel)); Equal(CommandStatus.Completed, result.Status);
        Equal(new CommandValue.Boolean(true), result.Result!.Single(r => r.Name == "shotDown").Value); f.Life.Random.RequireChoicesConsumed();
        foreach (bool warning in new[] { false, true })
        {
            var cancel = new PlayerAirliftFixture(); if (warning) cancel.Fighter(1, 2, CargoPoint);
            context = new(cancel.World, cancel.Data.Process, cancel.Life.Random, new PresentationRandom(1, "lift"), new(default, new(4)), default, (_, _) => { }, () => { });
            using var command = new PlayerAirliftOperation(cancel.Begin(), c => new((ulong)c));
            var menu = command.Resume(context, new()); Sequence(new[] { -1, 2 }, menu.Interaction!.Options.Select(o => o.Value));
            if (warning) Equal(CommandStatus.AwaitingInteraction, command.Resume(context, new(OperationInputKind.Choice, 2)).Status);
            Equal(CommandStatus.Completed, command.Resume(context, new(OperationInputKind.Timeout)).Status);
            Equal(false, cancel.World.Entities.Cities.Storage[0].Flags.Airlifted); cancel.Life.Random.RequireChoicesConsumed();
        }
    }

    private static void PlayerAirliftFailurePrefixes()
    {
        foreach (int stage in new[] { 0, 1, 2 })
        {
            var f = new PlayerAirliftFixture(stage == 1 ? [LiftRoll(0, 0)] : []);
            if (stage == 0) f.Changed = () => throw new InvalidOperationException("City window unavailable");
            else f.OnNotice = _ => throw new InvalidOperationException("Notice unavailable");
            using var op = f.Direct(stage == 1 ? 1 : 0, 2);
            Throws<InvalidOperationException>(() => op.Resume()); Throws<InvalidOperationException>(() => op.Resume());
            Equal(new MapPoint(20, 10), f.Data.Unit(0).Position); Equal(true, f.Data.Unit(0).Token.Occupied);
            Equal(true, f.World.Entities.Cities.Storage[0].Flags.Airlifted && f.World.Entities.Cities.Storage[2].Flags.Airlifted);
            Equal(1, f.Changes); f.Life.Random.RequireChoicesConsumed();
        }
        var cancel = new PlayerAirliftFixture(); var disposed = cancel.Begin(); disposed.Resume(); disposed.Dispose();
        Throws<ObjectDisposedException>(() => disposed.Resume(2)); Equal(false, cancel.World.Entities.Cities.Storage[0].Flags.Airlifted);
        var remote = new PlayerAirliftFixture(); remote.World.Globals.Multiplayer = GameMode.Tcp;
        using var rejected = remote.Direct(); Throws<InvalidOperationException>(() => rejected.Resume()); Equal(false, remote.World.Entities.Cities.Storage[0].Flags.Airlifted);
    }
}
