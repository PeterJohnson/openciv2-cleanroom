using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static AttackResponse Response(ParadropFixture f) => new(f.World, f.Data.Rules, f.Data.Stacks, f.Drop);
    private static List<Choice> ResponseDropChoices() => Enumerable.Range(0, 8)
        .Select(i => Choice.Create(new("U05.paradrop-approach", "response/paradrop", i), new SamplingLaw.PrimaryModulo(6), ChoiceCode.Primary(0))).ToList();
    private static void CompleteResponse(ParadropFixture f, AttackResponse.Operation op)
    {
        for (int i = 0; i < 100 && !op.Completed; ++i) Equal(null, op.Resume());
        Equal(true, op.Completed); f.Capture.Life.Random.RequireChoicesConsumed();
    }
    private static void AttackResponseGroundAdmission()
    {
        var f = new ParadropFixture(); var target = new MapPoint(10, 12);
        f.World.Paths.UniformCost = true; f.Data.Stacks.Relocate(0, new(8, 12), false);
        f.Data.Rules.Terrain[(int)Terrain.Grassland] = new() { MovementCost = 1 };
        f.Data.Rules.Units[20] = f.Data.Rules.Units[0] with { Role = RuleRole.Defend };
        f.Data.Rules.Units[21] = f.Data.Rules.Units[0] with { Attack = 0 };
        f.Data.Rules.Units[22] = f.Data.Rules.Units[0] with { Domain = RuleDomain.Sea };
        f.Data.Rules.Units[23] = f.Data.Rules.Units[0] with { Movement = 24 }; // Enough allowance for the nearby-enemy stack penalty.
        f.World.Globals.HumanPlayers = CivilizationSet.Only(1).With(4);
        foreach (int ally in new[] { 3, 4, 5 }) f.World.Civilizations[1].Treaties[ally].Alliance = true;
        f.World.Map.TileAt(target).Owner = 5; // Raw owner matters even without City/Unit feature gates.
        f.Data.AddUnit(1, 3, 23, new(10, 14)); f.Data.AddUnit(2, 4, 0, new(12, 12));
        f.Data.AddUnit(3, 5, 0, new(9, 13)); f.Data.AddUnit(4, 6, 0, new(11, 13));
        f.Data.AddCity(2, 2, new(6, 12)); f.Data.AddUnit(5, 1, 20, new(6, 12));
        f.Data.AddUnit(6, 1, 20, new(10, 10)); f.Data.AddUnit(7, 1, 0, new(14, 12)); f.Data.Unit(7).MovesUsed = 6;
        f.Data.AddUnit(8, 1, 22, new(14, 10)); f.Data.AddUnit(9, 1, 21, new(16, 12));
        f.Data.AddUnit(10, 1, 0, new(24, 12)); f.Data.AddUnit(11, 1, 0, new(16, 10)); f.World.Map.TileAt(new(16, 10)).Region = 2;
        f.Data.Unit(0).MovesUsed = 1; f.Data.Unit(0).Counter = 9; f.Data.Unit(0).AiTask = 7;
        using var op = Response(f).Begin(1, target, "response"); CompleteResponse(f, op);
        Equal((UnitSlot?)null, op.Paratrooper); Equal(3, op.RetaskedUnits);
        Equal("0,1,6", string.Join(',', Enumerable.Range(0, 12).Where(i => f.Data.Unit(i).Orders == UnitOrder.GoTo)));
        foreach (int slot in new[] { 0, 1, 6 })
        { Equal(UnitOrder.GoTo, f.Data.Unit(slot).Orders); Equal(target, f.Data.Unit(slot).Destination); Equal(AiTaskIds.RespondToAttack, f.Data.Unit(slot).AiTask); }
        foreach (int slot in new[] { 2, 3, 4, 5, 7, 8, 9, 10, 11 }) Equal(UnitOrder.None, f.Data.Unit(slot).Orders);
        Equal((byte)1, f.Data.Unit(0).MovesUsed); Equal((byte)9, f.Data.Unit(0).Counter);
        Equal(true, f.World.Paths.UniformCost); Equal(target, f.World.Paths.Goal); Equal(1, f.World.Paths.Owner); Equal(20, f.World.Paths.SelectedUnitType);
        op.Resume(); Equal(3, op.RetaskedUnits); Equal(0UL, f.Capture.Life.Random.Draws);
    }

    private static void AttackResponseDirectionAndModes()
    {
        foreach (int direction in new[] { 0, 1 })
        {
            var f = new ParadropFixture(); f.Data.Stacks.Relocate(0, new(8, 12), false); f.World.Paths.UniformCost = true;
            var target = direction == 0 ? new MapPoint(9, 11) : new MapPoint(10, 12);
            f.Data.Unit(0).Destination = new(2, 2); f.Data.Unit(0).AiTask = 99;
            using var op = Response(f).Begin(1, target, "response"); CompleteResponse(f, op);
            Equal(direction, op.RetaskedUnits); Equal(direction == 0 ? UnitOrder.None : UnitOrder.GoTo, f.Data.Unit(0).Orders);
            Equal(direction == 0 ? new MapPoint(2, 2) : target, f.Data.Unit(0).Destination);
            Equal(direction == 0 ? (byte)99 : AiTaskIds.RespondToAttack, f.Data.Unit(0).AiTask);
        }
        var roads = new ParadropFixture(); roads.Data.Stacks.Relocate(0, new(8, 12), false); roads.World.Paths.RoadsOnly = true;
        roads.Data.Rules.Terrain[(int)Terrain.Grassland] = new() { MovementCost = 1 };
        using var stopped = Response(roads).Begin(1, new(10, 12), "response"); CompleteResponse(roads, stopped);
        Equal(0, stopped.RetaskedUnits); Equal(true, roads.World.Paths.RoadsOnly); Equal(false, roads.World.Paths.UniformCost);
        var normal = new ParadropFixture(); normal.Data.Stacks.Relocate(0, new(8, 12), false);
        normal.Data.Rules.Units[0] = normal.Data.Rules.Units[0] with { Movement = 12 };
        normal.Data.Rules.Terrain[(int)Terrain.Grassland] = new() { MovementCost = 1 };
        using var ordinary = Response(normal).Begin(1, new(10, 12), "response"); CompleteResponse(normal, ordinary);
        Equal(1, ordinary.RetaskedUnits); Equal(13, normal.World.Paths.ResultCost); Equal(false, normal.World.Paths.UniformCost);
    }

    private static void AttackResponseParadropPrecedence()
    {
        foreach (bool reject in new[] { false, true })
        {
            var f = new ParadropFixture(reject ? [] : ResponseDropChoices());
            f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Abilities = new() { Paradrop = true } };
            f.Data.AddCity(2, 3, f.Data.Unit(0).Position); // Departure need not belong to responding civilization.
            f.Data.Unit(0).MovesUsed = 4; f.Data.Unit(0).Flags.Garrison = true;
            f.Data.AddUnit(1, 1, 0, new(16, 8)); f.Data.AddCity(3, 1, new(16, 8));
            if (reject) f.World.Map.TileAt(DropTarget).Terrain = Terrain.Ocean;
            using var op = Response(f).Begin(1, DropTarget, "response"); CompleteResponse(f, op);
            Equal(new UnitSlot(0), op.Paratrooper); Equal(0, op.RetaskedUnits);
            Equal(reject ? new MapPoint(10, 8) : DropTarget, f.Data.Unit(0).Position);
            Equal(!reject, f.Data.Unit(0).Flags.Paradropped); Equal(new MapPoint(16, 8), f.Data.Unit(1).Position);
            Equal(UnitOrder.None, f.Data.Unit(1).Orders); Equal((MapPoint?)null, f.World.Paths.CachedGoal);
        }
        foreach (int excluded in Enumerable.Range(0, 4))
        {
            var f = new ParadropFixture(); f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Abilities = new() { Paradrop = true } };
            if (excluded != 0) f.Data.AddCity(2, 1, f.Data.Unit(0).Position);
            if (excluded == 1) f.Data.Unit(0).MovesUsed = 1;
            if (excluded == 2) f.Data.Unit(0).Flags.Paradropped = true;
            var target = excluded == 3 ? new MapPoint(38, 20) : DropTarget;
            using var op = Response(f).Begin(1, target, "response"); CompleteResponse(f, op); Equal((UnitSlot?)null, op.Paratrooper);
        }
    }

    private static void AttackResponsePendingAndFailures()
    {
        var f = new ParadropFixture(); f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Abilities = new() { Paradrop = true } };
        f.Data.AddCity(2, 1, f.Data.Unit(0).Position); f.World.Civilizations[1].Treaties[2].Peace = true;
        using var op = Response(f).Begin(1, new(12, 8), "response"); var pending = op.Resume();
        Equal(true, pending is ParadropDecision.Attack); Equal(pending, op.Resume()); Equal(false, op.Completed);
        op.Resume(new(0)); Equal(true, op.Completed); Equal(0, op.RetaskedUnits); Equal(new MapPoint(10, 8), f.Data.Unit(0).Position);
        var remote = new ParadropFixture(); remote.World.Globals.Multiplayer = GameMode.Tcp;
        using var denied = Response(remote).Begin(1, DropTarget, "response"); Throws<InvalidOperationException>(() => denied.Resume());
        Equal((MapPoint?)null, remote.World.Paths.CachedGoal); Throws<InvalidOperationException>(() => denied.Resume());
        var disposed = Response(f).Begin(1, DropTarget, "response"); disposed.Dispose(); Throws<ObjectDisposedException>(() => disposed.Resume());
        var missing = new ParadropFixture(); missing.Data.Rules.Units[0] = missing.Data.Rules.Units[0] with { Abilities = new() { Paradrop = true } };
        missing.Data.AddCity(2, 1, missing.Data.Unit(0).Position);
        using var failed = Response(missing).Begin(1, DropTarget, "response"); Throws<ChoiceException>(() => failed.Resume());
        Equal(new UnitSlot(0), failed.Paratrooper); Equal(false, failed.Completed); Equal((MapPoint?)null, missing.World.Paths.CachedGoal);
        Throws<InvalidOperationException>(() => failed.Resume());
    }
}
