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
    private sealed class SpecialMovementFixture : ISpecialMovementEffects
    {
        public EliminationFixture Life { get; }
        public RevelationFixture Data => Life.Data;
        public GameState World => Data.World;
        public SpecialMovementFinish Finish { get; }
        public MovementContinuation Movement { get; }
        public List<string> Effects { get; } = [];
        public List<MovementLossNotice> Notices { get; } = [];
        public bool FailSound, FailRedraw, FailNotice;
        public SpecialMovementFixture(List<Choice>? choices = null, params int[] types)
        {
            Life = new(choices, CargoFixture(types.Length == 0 ? [2] : types));
            var incidents = new IncidentFixture(Life.Random, Data);
            foreach (ref var tile in World.Map.Tiles) tile.Terrain = Terrain.Ocean;
            World.Civilizations[1].Government = Government.Despotism;
            Data.Rules.Units[1] = Data.Rules.Units[1] with { Abilities = new() { CoastBound = true } };
            Data.Rules.Units[2] = Data.Rules.Units[2] with { Range = 2, Identity = new("plane", "Plane") };
            for (int i = 0; i < World.Entities.Units.End; ++i)
            { ++World.Civilizations[1].UnitsActive[Data.Unit(i).Type.Value]; ++World.UnitTotals[1]; }
            Movement = new(new(0), World.Entities.Units.End, new(2, 2)) { LocalHuman = true, WorkFlag = true };
            Finish = new(World, Data.Rules, Data.Stacks, Life.Random, Life.Elimination, Life.Loss, incidents.Incidents, this);
        }
        public SpecialMovementFinish.Operation Aircraft() => Finish.BeginAircraft(Movement, CargoPoint, "flight");
        public SpecialMovementFinish.Operation Coast(int owner = 1, MapPoint? destination = null) => Finish.BeginCoastBound(Movement, owner, destination ?? CargoPoint, "coast");
        public void Complete(SpecialMovementFinish.Operation op, bool stopped)
        {
            int limit = 50;
            while (!op.Completed && --limit > 0) Equal(null, op.Resume());
            Equal(true, op.Completed); Equal(stopped, op.Stopped); Life.Random.RequireChoicesConsumed();
        }
        public void Sound(MovementLossSound sound) { Effects.Add("sound:" + sound); if (FailSound) throw new InvalidOperationException("Sound unavailable"); }
        public void Redraw(MapPoint point) { Effects.Add("draw:" + point.X + "," + point.Y); if (FailRedraw) throw new InvalidOperationException("Redraw unavailable"); }
        public void Lost(MovementLossNotice notice) { Notices.Add(notice); Effects.Add("notice"); if (FailNotice) throw new InvalidOperationException("Notice unavailable"); }
    }
    private static Choice CoastRoll(int odds, int roll) => Choice.Create(new("U05.coast-loss", "coast"), new SamplingLaw.PrimaryModulo(odds), ChoiceCode.Primary(roll));

    private static void AircraftBasingAndAttackException()
    {
        foreach (int variant in Enumerable.Range(0, 6))
        {
            var f = new SpecialMovementFixture(null, 2, 1); f.Data.Unit(0).Counter = 99;
            if (variant is 0 or 1)
            {
                ref var tile = ref f.World.Map.TileAt(CargoPoint); tile.Features |= MapFeatures.CitySite;
                if (variant == 1) tile.Features |= MapFeatures.Fortification;
                tile.Owner = 2;
            }
            if (variant == 2) { f.Data.Rules.Units[1] = f.Data.Rules.Units[1] with { Capacity = 0, Abilities = new() { CarryAircraft = true } }; f.Data.Unit(1).Owner = new(2); }
            if (variant is 3 or 4) f.Data.Rules.Units[1] = f.Data.Rules.Units[1] with { Abilities = new() { Submarine = true } };
            if (variant == 3) f.Data.Rules.Units[2] = f.Data.Rules.Units[2] with { Abilities = new() { DestroyedAfterAttack = true } };
            using var op = f.Aircraft(); f.Complete(op, false);
            Equal(variant < 4, f.Movement.WorkFlag); Equal((byte)(variant < 4 ? 0 : 99), f.Data.Unit(0).Counter);
            Equal((byte)(variant < 4 ? 6 : 0), f.Data.Unit(0).MovesUsed); Equal(variant < 4 ? "1,0" : "0,1", Chain(f.Data, 0));
            Equal(variant < 4 ? 1 : 0, f.Effects.Count); Equal(0, f.Notices.Count);
        }
        foreach (byte range in new byte[] { 0, 1, 2 })
            foreach (bool attacked in new[] { false, true })
                foreach (byte moves in new byte[] { 0, 6 })
                {
                    var f = new SpecialMovementFixture(); f.World.Map.TileAt(CargoPoint).Features |= MapFeatures.CitySite;
                    f.Data.Rules.Units[2] = f.Data.Rules.Units[2] with { Range = range }; f.Data.Unit(0).MovesUsed = moves; f.Data.Unit(0).Counter = 5;
                    f.Movement.AttackDone = attacked; using var op = f.Aircraft(); f.Complete(op, false);
                    bool based = !(range == 1 && moves == 0 && attacked);
                    Equal(based, f.Movement.WorkFlag); Equal((byte)(based ? 0 : 5), f.Data.Unit(0).Counter); Equal((byte)(based ? 6 : 0), f.Data.Unit(0).MovesUsed);
                }
    }

    private static void AircraftFuelWrapAndBasicLoss()
    {
        foreach (var (counter, range, stopped, next) in new[] { (0, 0, false, 0), (0, 2, false, 1), (1, 2, true, 2), (127, 127, false, 128), (255, 1, false, 0), (0, 128, true, 1) })
        {
            var f = new SpecialMovementFixture(null, 2, 0); f.Data.Unit(0).MovesUsed = 6; f.Data.Unit(0).Counter = (byte)counter;
            f.Data.Unit(1).Orders = UnitOrder.Sleeping; f.Data.Unit(1).Carrier = new(0); f.Data.Rules.Units[2] = f.Data.Rules.Units[2] with { Range = (byte)range };
            using var op = f.Aircraft(); f.Complete(op, stopped); Equal((byte)next, f.Data.Unit(0).Counter); Equal(!stopped, f.Data.Unit(0).Token.Occupied);
            Equal(true, f.Data.Unit(1).Token.Occupied); Equal(stopped ? UnitOrder.None : UnitOrder.Sleeping, f.Data.Unit(1).Orders);
            Equal(stopped ? CargoPoint : new MapPoint(2, 2), f.Movement.Origin); Equal(false, f.Movement.WorkFlag);
            if (stopped) { Sequence(new[] { "sound:EngineSputter", "draw:10,10", "notice" }, f.Effects); Equal(new UnitType(2), f.Notices.Single().Type); }
            Equal(stopped ? 1 : 0, f.Life.Order.Count); Equal(false, f.World.UnitDeletion.Active);
        }
        foreach (bool local in new[] { false, true })
        {
            var f = new SpecialMovementFixture(); f.Data.Rules.Units[30] = f.Data.Rules.Units[2] with { Range = 1 }; f.Data.Unit(0).Type = new(30); f.Data.Unit(0).MovesUsed = 6;
            f.Movement.LocalHuman = local; using var op = f.Aircraft(); f.Complete(op, true);
            Equal(local ? "sound:JetSputter" : "draw:10,10", f.Effects[0]); Equal(local ? 1 : 0, f.Notices.Count);
        }
    }

    private static void CoastBoundGatesAndOdds()
    {
        foreach (int variant in Enumerable.Range(0, 5))
        {
            var f = new SpecialMovementFixture(null, 1); f.Data.Unit(0).MovesUsed = 6;
            if (variant == 0) f.Data.Rules.Units[1] = f.Data.Rules.Units[1] with { Abilities = new() };
            if (variant == 1) f.Movement.LocalHuman = false;
            if (variant == 2) f.Data.Unit(0).MovesUsed = 0;
            if (variant == 3) { f.Data.AddCity(0, 2, new(2, 2)); f.World.Globals.WonderCity[WonderIds.Lighthouse] = 0; }
            if (variant == 4) f.World.Map.TileAt(CargoPoint).Terrain = Terrain.Grassland;
            using var op = f.Coast(variant == 3 ? 2 : 1); f.Complete(op, false); Equal(true, f.Movement.WorkFlag); Equal(0, f.Effects.Count);
        }
        foreach (var (initial, seafaring, navigation, odds) in new[] { (0, false, false, 0), (1, false, false, 1), (0, true, false, 2), (1, true, true, 4), (3, true, true, 12), (255, true, true, 1020) })
        {
            var f = new SpecialMovementFixture(odds > 1 ? [CoastRoll(odds, 0)] : [], 1); f.Data.Unit(0).MovesUsed = 6;
            f.Data.Rules.Cosmic = f.Data.Rules.Cosmic with { TriremeLossDenominator = (byte)initial };
            if (seafaring) f.World.Civilizations[1].Advances = f.World.Civilizations[1].Advances.With(AdvanceIds.Seafaring);
            if (navigation) f.World.Civilizations[1].Advances = f.World.Civilizations[1].Advances.With(AdvanceIds.Navigation);
            f.World.Map.TileAt(CargoPoint).Terrain = Terrain.Grassland; // Draw still occurs before checking the center.
            using var op = f.Coast(); f.Complete(op, false); Equal(true, f.Movement.WorkFlag);
        }
        foreach (bool incoming in new[] { false, true })
        {
            var f = new SpecialMovementFixture([CoastRoll(3, 1)], 1); f.Data.Unit(0).MovesUsed = 6; f.Data.Rules.Cosmic = f.Data.Rules.Cosmic with { TriremeLossDenominator = 3 };
            f.Movement.WorkFlag = incoming; using var op = f.Coast(); f.Complete(op, false); Equal(incoming, f.Movement.WorkFlag);
        }
    }

    private static void CoastBoundCargoAndTopology()
    {
        var f = new SpecialMovementFixture(null, 1, 0, 1, 0); f.Data.Unit(0).MovesUsed = 6;
        f.Data.Rules.Units[1] = f.Data.Rules.Units[1] with { Capacity = 1 };
        using var sink = f.Coast(); f.Complete(sink, true);
        for (int i = 0; i < 4; ++i) Equal(false, f.Data.Unit(i).Token.Occupied); // U01's at-sea physical-stack policy includes unselected cargo/ships.
        Sequence(new[] { "sound:Sinking", "draw:10,10", "notice" }, f.Effects); Sequence(new[] { "units" }, f.Life.Order);
        Equal(CargoPoint, f.Movement.Origin); Equal(false, f.World.UnitDeletion.CargoActive); Equal(false, f.Movement.WorkFlag);
        var landType = new SpecialMovementFixture(null, 0, 1); landType.Data.Rules.Units[0] = landType.Data.Rules.Units[0] with { Abilities = new() { CoastBound = true } };
        landType.Data.Unit(0).MovesUsed = 6; using var unusual = landType.Coast(); landType.Complete(unusual, true);
        Equal(true, landType.Data.Unit(1).Token.Occupied); Equal(false, landType.Data.Unit(0).Token.Occupied); Equal(0, landType.Life.Order.Count);
        foreach (bool flat in new[] { false, true })
        {
            var edge = new SpecialMovementFixture(null, 1); edge.World.Globals.Rules.FlatWorld = flat; edge.Data.Unit(0).MovesUsed = 6;
            edge.World.Map.TileAt(new(39, 9)).Terrain = Terrain.Grassland;
            using var op = edge.Coast(destination: new(0, 10)); edge.Complete(op, flat); Equal(!flat, edge.Movement.WorkFlag);
        }
    }

    private static SpecialMovementFixture NestedFlight()
    {
        var f = new SpecialMovementFixture(); CreationFixture(f.Data);
        f.Data.Rules.Units[2] = f.Data.Rules.Units[2] with { Role = RuleRole.Settle, Range = 1 };
        f.Data.Unit(0).MovesUsed = 6; f.World.Civilizations[1].CityCount = 0;
        f.Data.AddUnit(1, 3, 4, new(24, 10)); f.Data.Unit(1).Flags.SpyMission = true; f.Data.Unit(1).Counter = 1;
        f.World.Civilizations[1].Treaties[3].Peace = true; f.Life.Recreate = c => Creation(f.Data, c, f.Life.Random);
        return f;
    }
    private static void SpecialMovementNestedLossAndSession()
    {
        var f = NestedFlight(); using var op = f.Aircraft(); var decision = op.Resume()!;
        Equal(1, decision.Victim.Civilization); Equal(3, decision.Actor.Civilization); Equal(decision, op.Resume());
        Equal(false, op.Completed); Equal(false, f.Data.Unit(0).Token.Occupied); Equal(1, f.Effects.Count); Equal(CargoPoint, f.Movement.Origin);
        op.Resume(0); f.Complete(op, true); Equal(1, f.Notices.Count);
        f = NestedFlight(); var context = new OperationContext(f.World, f.Data.Process, f.Life.Random, new PresentationRandom(1, "flight"),
            new(default, new(3)), default, (_, _) => { }, () => { });
        using var session = new SpecialMovementOperation(f.Aircraft(), c => new((ulong)c));
        var question = session.Resume(context, new()); Equal(CommandStatus.AwaitingInteraction, question.Status); Equal(new ParticipantId(1), question.Interaction!.Recipient);
        var result = session.Resume(context, new(OperationInputKind.Cancel)); Equal(CommandStatus.Completed, result.Status);
        Equal(new CommandValue.Boolean(true), result.Result!.Single().Value); f.Life.Random.RequireChoicesConsumed();
        // The cargo-aware lifecycle entry must also suspend inside a passenger's last-Settler check.
        var ship = new SpecialMovementFixture(null, 1, 0); CreationFixture(ship.Data);
        ship.Data.Unit(0).MovesUsed = 6; ship.Data.Unit(1).Orders = UnitOrder.Sleeping; ship.Data.Unit(1).Carrier = new(0);
        ship.Data.AddUnit(2, 3, 4, new(24, 10)); ship.Data.Unit(2).Flags.SpyMission = true; ship.Data.Unit(2).Counter = 1;
        ship.World.Civilizations[1].Treaties[3].Peace = true; ship.Life.Recreate = c => Creation(ship.Data, c, ship.Life.Random);
        using var sinking = ship.Coast(); var pretext = sinking.Resume()!;
        Equal(1, pretext.Victim.Civilization); Equal(false, sinking.Completed);
        Equal(false, ship.Data.Unit(0).Token.Occupied || ship.Data.Unit(1).Token.Occupied); Equal(0, ship.Notices.Count);
        sinking.Resume(0); ship.Complete(sinking, true); Equal(false, ship.World.UnitDeletion.CargoActive); Equal(1, ship.Notices.Count);
    }

    private static void SpecialMovementFailurePrefixes()
    {
        foreach (int stage in new[] { 0, 1, 2 })
        {
            var f = new SpecialMovementFixture { FailSound = stage == 0, FailRedraw = stage == 1, FailNotice = stage == 2 };
            f.Data.Rules.Units[2] = f.Data.Rules.Units[2] with { Range = 1 }; f.Data.Unit(0).MovesUsed = 6;
            using var op = f.Aircraft();
            if (stage == 2) { Throws<InvalidOperationException>(() => op.Resume()); Throws<InvalidOperationException>(() => op.Resume()); }
            else { f.Complete(op, true); Equal(1, op.PresentationFailures.Count); Equal(1, f.Notices.Count); }
            Equal((byte)1, f.Data.Unit(0).Counter); Equal(false, f.Data.Unit(0).Token.Occupied);
            Equal(CargoPoint, f.Movement.Origin);
        }
        var disposed = NestedFlight(); var pending = disposed.Aircraft(); pending.Resume(); pending.Dispose();
        Throws<ObjectDisposedException>(() => pending.Resume(0)); Equal(false, disposed.Data.Unit(0).Token.Occupied); Equal(0, disposed.Notices.Count);
        var remote = new SpecialMovementFixture(); remote.World.Globals.Multiplayer = GameMode.Tcp;
        using var rejected = remote.Aircraft(); Throws<InvalidOperationException>(() => rejected.Resume()); Equal(true, remote.Movement.WorkFlag); Equal(0, remote.Effects.Count);
    }
}
