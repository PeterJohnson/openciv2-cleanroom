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
    private sealed class TransportFixture : ITransportMovementEffects
    {
        public RevelationFixture Data { get; }
        public GameState World => Data.World;
        public TransportMovement Transport { get; }
        public MovementContinuation Movement { get; }
        public List<string> Effects { get; } = [];
        public bool FailDraw, FailDenial;
        public TransportFixture(params int[] types)
        {
            Data = CargoFixture(types); World.Map.TileAt(CargoPoint).Terrain = Terrain.Ocean; World.Globals.TurnCiv = 1;
            Data.Rules.Units[1] = Data.Rules.Units[1] with { Identity = new("ferry", "Ferry") };
            Transport = new(World, Data.Rules, Data.Stacks, new(World, Data.Rules, Data.Stacks), this);
            Movement = new(new(0), World.Entities.Units.End, CargoPoint) { LocalHuman = true, Options = new(true, true), Answer = 77, WorkFlag = true };
        }
        public void NoLandfall(UnitSlot ship, string name) { Effects.Add("no:" + ship.Value); if (FailDenial) throw new InvalidOperationException("No landfall presentation"); }
        public void Redraw(MapPoint point) { Effects.Add("draw"); if (FailDraw) throw new InvalidOperationException("No transport redraw"); }
        public void Publish() => Effects.Add("publish");
        public void FocusSelectedUnit(UnitSlot unit, MapPoint position)
        { Equal(World.Globals.ActiveUnit, unit); Equal(CargoPoint, position); Effects.Add("focus"); }
    }

    private static void TransportLandfallSelectionAndReply()
    {
        foreach (int answer in new[] { 0, 1, -1, 9 })
        {
            var f = new TransportFixture(1, 0, 0, 0); f.Data.Unit(0).Orders = UnitOrder.GoTo;
            f.Data.Unit(1).Orders = UnitOrder.Sleeping; f.Data.Unit(1).Carrier = new(99);
            f.Data.Unit(2).MovesUsed = 4; // Only two road units left.
            f.Data.Unit(3).Owner = new(2); f.Data.Unit(3).Orders = UnitOrder.Fortified;
            using var op = f.Transport.BeginLandfall(new(0), f.Movement); var choice = op.Resume()!;
            Equal("Ferry", choice.ShipName); Equal(1, choice.Actor); Equal(choice, op.Resume());
            Equal(0, f.Movement.UnitBound); Equal(UnitOrder.None, f.Data.Unit(0).Orders); Equal(77, f.Movement.Answer);
            op.Resume(answer); Equal(true, op.Completed); Equal(answer != 0, op.Restart); Equal(answer, f.Movement.Answer);
            Equal(new UnitSlot((ushort)(answer == 0 ? 0 : 3)), f.Movement.Unit);
            Equal(answer == 0 ? UnitOrder.Fortified : UnitOrder.None, f.Data.Unit(3).Orders); Equal((byte)0, f.Data.Unit(3).MovesUsed);
            Equal(new UnitSlot(99), f.Data.Unit(1).Carrier); Equal(CargoPoint, f.Data.Unit(0).Position);
        }
        foreach (bool autoplay in new[] { false, true })
        {
            var f = new TransportFixture(1, 0); f.World.Multiplayer.Saved.AutoplayActive = autoplay; f.Movement.Options = new(autoplay, true); f.Movement.Answer = 0;
            f.Data.Unit(1).Orders = UnitOrder.Sleeping; f.Data.Unit(1).Carrier = new(0);
            using var op = f.Transport.BeginLandfall(new(0), f.Movement); Equal(null, op.Resume()); Equal(true, op.Restart); Equal(0, f.Movement.Answer);
            Equal(UnitOrder.None, f.Data.Unit(1).Orders); Equal(new UnitSlot(0), f.Data.Unit(1).Carrier);
        }
    }

    private static void TransportLandfallRefusalAndFailures()
    {
        foreach (int variant in Enumerable.Range(0, 7))
        {
            var f = new TransportFixture(1, 0); f.Data.Unit(0).Orders = UnitOrder.GoTo; f.Data.Unit(1).MovesUsed = 6;
            if (variant == 0) f.Movement.LocalHuman = false;
            if (variant == 1) { f.World.Map.TileAt(CargoPoint).Features |= MapFeatures.CitySite; }
            if (variant == 2) f.Movement.UnitBound = 99;
            if (variant == 3) { f.Data.Unit(1).Orders = UnitOrder.Sleeping; f.Data.Unit(1).Carrier = new(99); }
            if (variant == 4) f.World.Multiplayer.Saved.AutoplayActive = true;
            if (variant == 5) f.Movement.Options = new(false, true);
            using var op = f.Transport.BeginLandfall(new(0), f.Movement); Equal(null, op.Resume()); Equal(true, op.Completed); Equal(false, op.Restart);
            Equal(variant < 2 ? 2 : 0, f.Movement.UnitBound); Equal(77, f.Movement.Answer);
            Equal(variant <= 2 ? UnitOrder.GoTo : UnitOrder.None, f.Data.Unit(0).Orders);
            Equal(variant is 2 or 6 ? 1 : 0, f.Effects.Count);
        }
        var denied = new TransportFixture(1, 0) { FailDenial = true }; denied.Data.Unit(1).MovesUsed = 6;
        using var fault = denied.Transport.BeginLandfall(new(0), denied.Movement); Throws<InvalidOperationException>(() => fault.Resume());
        Equal(0, denied.Movement.UnitBound); Throws<InvalidOperationException>(() => fault.Resume());
        var disposed = new TransportFixture(1, 0); var pending = disposed.Transport.BeginLandfall(new(0), disposed.Movement); pending.Resume(); pending.Dispose();
        Throws<ObjectDisposedException>(() => pending.Resume(1)); Equal(new UnitSlot(0), disposed.Movement.Unit); Equal(0, disposed.Movement.UnitBound);
        var remote = new TransportFixture(1, 0); remote.World.Globals.Multiplayer = GameMode.Tcp;
        using var network = remote.Transport.BeginLandfall(new(0), remote.Movement); Throws<InvalidOperationException>(() => network.Resume()); Equal(2, remote.Movement.UnitBound);
    }

    private static void TransportBoardingActualCargo()
    {
        var f = new TransportFixture(1, 0, 1, 0);
        f.Data.Rules.Units[1] = f.Data.Rules.Units[1] with { Capacity = 1 };
        f.Data.Unit(0).MovesUsed = 6; f.Data.Unit(0).Orders = UnitOrder.GoTo;
        f.Data.Unit(2).MovesUsed = 6; f.Data.Unit(2).Orders = UnitOrder.Fortified;
        f.Data.Unit(3).Orders = UnitOrder.Sleeping; f.Data.Unit(3).Carrier = new(0);
        f.Data.Unit(1).Orders = UnitOrder.GoTo; f.Data.Unit(1).Carrier = new(99); f.Data.Unit(1).Destination = new(99, 71);
        f.Transport.Board(new(1), CargoPoint);
        Equal(UnitOrder.Sleeping, f.Data.Unit(1).Orders); Equal((byte)6, f.Data.Unit(1).MovesUsed); Equal(new UnitSlot(2), f.Data.Unit(1).Carrier);
        Equal(new MapPoint(2, 71), f.Data.Unit(1).Destination); Equal(new UnitSlot(2), f.World.Globals.ActiveUnit);
        Equal(UnitOrder.GoTo, f.Data.Unit(0).Orders); Equal(UnitOrder.None, f.Data.Unit(2).Orders);
        Equal(new UnitSlot(0), f.Data.Unit(3).Carrier); Equal(false, f.Data.Unit(3).Flags.Aboard); // Second gather has real side effects on earlier cargo.
        Sequence(new[] { "draw", "publish" }, f.Effects);
        foreach (bool sleeping in new[] { false, true })
        {
            f = new(1, 0); f.Data.Unit(0).MovesUsed = 7; f.Data.Unit(1).Carrier = new(99);
            f.Data.Unit(1).Orders = sleeping ? UnitOrder.Sleeping : UnitOrder.None;
            f.Transport.Board(new(1), CargoPoint); Equal(sleeping ? new UnitSlot(99) : null, f.Data.Unit(1).Carrier); Equal(null, f.World.Globals.ActiveUnit);
            Equal((byte)6, f.Data.Unit(1).MovesUsed); Sequence(new[] { "draw", "publish" }, f.Effects);
        }
        f = new(1, 0) { FailDraw = true }; Throws<InvalidOperationException>(() => f.Transport.Board(new(1), CargoPoint));
        Equal(new UnitSlot(0), f.Data.Unit(1).Carrier); Equal(new UnitSlot(0), f.World.Globals.ActiveUnit); Equal(1, f.Effects.Count);
    }

    private static void TransportUnloadAndPortWakeup()
    {
        var f = new TransportFixture(1, 0, 0, 0, 2); f.World.Globals.ActiveUnit = new(0); f.World.MovementMode = MovementMode.MoveUnits;
        for (int i = 1; i <= 4; ++i) { f.Data.Unit(i).Orders = UnitOrder.Sleeping; f.Data.Unit(i).Carrier = new(0); }
        f.Data.Unit(2).Owner = new(2); f.Transport.Unload();
        Equal(true, f.Data.Unit(0).Flags.Waiting); Equal(new UnitSlot(3), f.World.Globals.ActiveUnit); Equal(MovementMode.MoveUnits, f.World.MovementMode);
        for (int i = 1; i <= 3; ++i) { Equal(UnitOrder.None, f.Data.Unit(i).Orders); Equal(new UnitSlot(0), f.Data.Unit(i).Carrier); }
        Equal(UnitOrder.Sleeping, f.Data.Unit(4).Orders); Sequence(new[] { "focus" }, f.Effects);
        f = new(1, 0); f.World.Globals.ActiveUnit = new(0); f.World.MovementMode = MovementMode.MoveUnits;
        f.Data.Unit(1).Orders = UnitOrder.Sleeping; f.Data.Unit(1).MovesUsed = 6; f.Transport.Unload();
        Equal(UnitOrder.None, f.Data.Unit(1).Orders); Equal(new UnitSlot(0), f.World.Globals.ActiveUnit); Equal(MovementMode.MoveUnits, f.World.MovementMode); Equal(0, f.Effects.Count);
        foreach (int variant in Enumerable.Range(0, 5))
        {
            f = new(1, 0, 2); for (int i = 0; i < 3; ++i) { f.Data.Unit(i).Orders = UnitOrder.Sleeping; f.Data.Unit(i).Carrier = new(9); }
            if (variant == 1) f.Data.Rules.Units[1] = f.Data.Rules.Units[1] with { Capacity = 0 };
            f.Transport.ArriveInPort(new(0), variant != 2, variant == 3 ? null : new(0), variant == 4 ? 2 : 1);
            for (int i = 0; i < 3; ++i) { Equal(variant == 0 ? UnitOrder.None : UnitOrder.Sleeping, f.Data.Unit(i).Orders); Equal(new UnitSlot(9), f.Data.Unit(i).Carrier); }
        }
    }

    private static void TransportExplorationWakeup()
    {
        foreach (int variant in Enumerable.Range(0, 7))
        {
            var f = new TransportFixture(1, 0, 3); f.Data.Unit(0).MovesUsed = 6;
            f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Attack = 0, Role = RuleRole.Attack };
            f.Data.Rules.Units[3] = f.Data.Rules.Units[3] with { Attack = 0, Role = RuleRole.Settle };
            f.Data.Unit(1).Orders = f.Data.Unit(2).Orders = UnitOrder.Fortified; f.Data.Unit(1).Carrier = new(0);
            foreach (ref var tile in f.World.Map.Tiles) { tile.Region = 5; tile.RevealedTo = CivilizationSet.Only(1); }
            f.World.Map.TileAt(new(14, 10)).RevealedTo = default;
            if (variant == 1) f.World.Civilizations[1].ContinentPolicy[5] = 4;
            if (variant == 2) f.Data.Unit(0).MovesUsed = 3;
            if (variant == 3) f.Data.Rules.Units[1] = f.Data.Rules.Units[1] with { Capacity = 0 };
            if (variant == 4) f.World.Globals.HumanPlayers = default;
            if (variant == 5) f.Data.Unit(1).MovesUsed = 1;
            f.Transport.Explore(f.Movement, 1, variant == 6 ? RuleDomain.Land : RuleDomain.Sea, CargoPoint);
            Equal(variant == 0 ? UnitOrder.None : UnitOrder.Fortified, f.Data.Unit(1).Orders);
            Equal(UnitOrder.Fortified, f.Data.Unit(2).Orders); Equal(new UnitSlot(0), f.Data.Unit(1).Carrier);
            Equal(variant != 1, f.Movement.WorkFlag); // Failed entry gates retain true; policy scan clears it.
        }
        var edge = new TransportFixture(1, 0); edge.World.Globals.Rules.FlatWorld = false;
        edge.Data.Unit(0).MovesUsed = 6; edge.Data.Rules.Units[0] = edge.Data.Rules.Units[0] with { Attack = 0 };
        foreach (ref var tile in edge.World.Map.Tiles) { tile.Terrain = Terrain.Ocean; tile.RevealedTo = CivilizationSet.Only(1); }
        edge.World.Map.TileAt(new(39, 9)).Terrain = Terrain.Grassland; edge.World.Map.TileAt(new(38, 8)).RevealedTo = default;
        edge.Data.Unit(1).Orders = UnitOrder.Fortified; edge.Transport.Explore(edge.Movement, 1, RuleDomain.Sea, new(0, 10));
        Equal(true, edge.Movement.WorkFlag); Equal(UnitOrder.None, edge.Data.Unit(1).Orders);
    }

    private static void TransportLandfallSession()
    {
        foreach (bool cancel in new[] { false, true })
        {
            var f = new TransportFixture(1, 0); f.Data.Unit(1).Orders = UnitOrder.Sleeping;
            var context = new OperationContext(f.World, f.Data.Process, new GameplayRandom(1, "landfall"), new PresentationRandom(1, "landfall"),
                new(default, new(1)), default, (_, _) => { }, () => { });
            using var op = new LandfallOperation(f.Transport.BeginLandfall(new(0), f.Movement), c => new((ulong)c));
            var prompt = op.Resume(context, new()); Equal(new ParticipantId(1), prompt.Interaction!.Recipient);
            Equal(CommandStatus.AwaitingInteraction, prompt.Status); Sequence(new[] { 0, 1 }, prompt.Interaction.Choices);
            var result = op.Resume(context, cancel ? new(OperationInputKind.Cancel) : new(OperationInputKind.Choice, 1));
            Equal(CommandStatus.Completed, result.Status); Equal(new CommandValue.Boolean(!cancel), result.Result!.Single().Value);
            Equal(0, f.Movement.UnitBound); Equal(cancel ? new UnitSlot(0) : new UnitSlot(1), f.Movement.Unit);
        }
    }
}
