using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Formats;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // A01 §2/W01 §4 authored regression inputs, with explicit bounded U04 query providers.
    private sealed class GoalFixture : IUnitMovementQueries
    {
        public ProcessContext Process { get; } = new();
        public GameState World { get; }
        public Func<UnitSlot, bool>? Awake = _ => true;
        public Func<UnitSlot, int>? Moves = _ => 4;
        public List<string> Reads { get; } = [];
        public GoalFixture(bool allocate = true)
        {
            World = new(Process); if (allocate) World.Map.Allocate(new(20, 12)); World.Globals.TurnCiv = 1;
            foreach (ref var civ in World.Civilizations.AsSpan())
            {
                ((Span<CivilizationGoal>)civ.Goals).Fill(new(-10, -20, -1, 0));
                ((Span<CivilizationGoal>)civ.StandingGoals).Fill(new(-30, -40, -1, 0));
            }
        }
        public ref Civilization Civ => ref World.Civilizations[1];
        public ref Unit Unit(int index) => ref World.Entities.Units.Storage[index];
        public SharedGoals Operations => new(World, Process.Rules.Definitions, this);
        public bool CanAct(UnitSlot unit) { Reads.Add("awake/" + unit.Value); return Awake?.Invoke(unit) ?? throw new InvalidOperationException("Missing awake query"); }
        public int RemainingMoves(UnitSlot unit) { Reads.Add("moves/" + unit.Value); return Moves?.Invoke(unit) ?? throw new InvalidOperationException("Missing moves query"); }
        public void Live(int index, int role = 2, Position? position = null)
        {
            var at = position ?? new(4, 4); ref var unit = ref Unit(index);
            unit.Token = new((uint)index + 1); unit.Owner = new(1); unit.Type = new(0); unit.X = (short)at.X; unit.Y = (short)at.Y;
            unit.Flags = 0x8912; unit.AiTask = 0x37; unit.DestinationX = -12; unit.DestinationY = -14;
            Process.Rules.Definitions.Units[0] = new() { Role = (RuleRole)role };
            World.Entities.Units.SetEnd(Math.Max(World.Entities.Units.End, index + 1)); World.Globals.UnitEnd = (short)World.Entities.Units.End;
        }
    }
    private static void GoalDuplicateAdmission()
    {
        var f = new GoalFixture(); f.Live(0); f.Civ.Goals[47] = new(2, 4, 2, -128); f.Civ.StandingGoals[15] = new(2, 4, 2, -128); var before = f.Civ;
        f.Operations.AddCurrent(1, new(2, 4), 258, 128); f.Operations.AddStanding(1, new(2, 4), 258, 128); Equal(before, f.Civ); Equal(0, f.Reads.Count);
        f.Civ.Goals[0] = new(2, 4, -1, 6); before = f.Civ; f.Operations.AddCurrent(1, new(2, 4), 255, 5); Equal(before, f.Civ);
        f.Operations.AddCurrent(1, new(65538, -65532), 258, 128); Equal(new CivilizationGoal(2, 4, 2, -128), f.Civ.Goals[0]); Equal(new CivilizationGoal(2, 4, 2, -128), f.Civ.Goals[47]);
    }
    private static void GoalPriorityInsertion()
    {
        var f = new GoalFixture(); f.Civ.Goals[0] = new(2, 2, 1, 6); f.Civ.Goals[1] = new(4, 4, 1, 4); f.Civ.Goals[2] = new(6, 6, 1, 4);
        f.Operations.AddCurrent(1, new(4, 4), 1, 5); f.Operations.AddCurrent(1, new(8, 8), 1, 4);
        CivilizationGoal[] expected = [new(2, 2, 1, 6), new(4, 4, 1, 5), new(4, 4, 1, 4), new(6, 6, 1, 4), new(8, 8, 1, 4)];
        for (int i = 0; i < expected.Length; ++i) Equal(expected[i], f.Civ.Goals[i]);
    }
    private static void GoalGapRules()
    {
        var f = new GoalFixture(); for (short i = 0; i < 16; ++i) f.Civ.Goals[i] = f.Civ.StandingGoals[i] = new(i, 3, 1, 2);
        f.Civ.Goals[2] = f.Civ.StandingGoals[2] = new(22, 23, -1, 99); f.Civ.Goals[47] = new(90, 91, 6, 5); var before = f.Civ;
        f.Operations.AddCurrent(1, new(10, 10), 7, 3); f.Operations.AddStanding(1, new(10, 10), 7, 3);
        Equal(new CivilizationGoal(10, 10, 7, 3), f.Civ.Goals[0]); Equal(new CivilizationGoal(10, 10, 7, 3), f.Civ.StandingGoals[0]);
        Equal(before.Goals[0], f.Civ.Goals[1]); Equal(before.Goals[1], f.Civ.Goals[2]); for (int i = 3; i < 48; ++i) Equal(before.Goals[i], f.Civ.Goals[i]); for (int i = 1; i < 16; ++i) Equal(before.StandingGoals[i - 1], f.Civ.StandingGoals[i]);
        f = new(); f.Civ.Goals[0] = new(5, 6, -1, 127); f.Civ.Goals[1] = new(7, 8, 4, 1); var expected = f.Civ; expected.Goals[0] = new(-32768, 32767, -2, -128);
        f.Operations.AddCurrent(1, new(32768, -32769), 254, 128); Equal(expected, f.Civ);
        ((Span<CivilizationGoal>)f.Civ.Goals).Fill(new(5, 6, -2, 127)); before = f.Civ; f.Operations.AddCurrent(1, new(2, 2), 1, 6); Equal(before, f.Civ);
    }
    private static void GoalFullLists()
    {
        var f = new GoalFixture(); ((Span<CivilizationGoal>)f.Civ.Goals).Fill(new(2, 2, 0, 3)); ((Span<CivilizationGoal>)f.Civ.StandingGoals).Fill(new(4, 4, 0, 3)); f.Civ.Goals[47] = new(8, 8, 0, 3); f.Civ.StandingGoals[15] = new(9, 9, 0, 3); var before = f.Civ;
        f.Operations.AddCurrent(1, new(6, 6), 1, 3); f.Operations.AddStanding(1, new(6, 6), 1, 3); Equal(before, f.Civ);
        f.Operations.AddCurrent(1, new(6, 6), 1, 4); f.Operations.AddStanding(1, new(6, 6), 1, 4); Equal(new CivilizationGoal(6, 6, 1, 4), f.Civ.Goals[0]); Equal(new CivilizationGoal(6, 6, 1, 4), f.Civ.StandingGoals[0]);
        for (int i = 1; i < 48; ++i) Equal(before.Goals[i - 1], f.Civ.Goals[i]); for (int i = 1; i < 16; ++i) Equal(before.StandingGoals[i - 1], f.Civ.StandingGoals[i]);
    }
    private static void GoalNegationAndQuery()
    {
        for (int priority = -128; priority <= 127; ++priority) { var goal = new CivilizationGoal(123, -456, -19, (sbyte)priority); SharedGoals.NegatePriority(ref goal); Equal(new CivilizationGoal(123, -456, -19, (sbyte)(priority > 0 ? -priority : priority)), goal); }
        var f = new GoalFixture(); f.Civ.Goals[0] = new(2, 4, 1, -128); f.Civ.Goals[1] = new(2, 4, 1, -3); Equal(3, SharedGoals.PriorityAt(f.Civ, new(2, 4), 1));
        (f.Civ.Goals[0], f.Civ.Goals[1]) = (f.Civ.Goals[1], f.Civ.Goals[0]); Equal(128, SharedGoals.PriorityAt(f.Civ, new(2, 4), 1)); f.Civ.Goals[47] = new(2, 4, 1, 0); Equal(0, SharedGoals.PriorityAt(f.Civ, new(2, 4), 1)); Equal(0, SharedGoals.PriorityAt(f.Civ, new(65538, 4), 1));
        f.Civ.Goals[0] = new(2, 4, -1, -7); Equal(7, SharedGoals.PriorityAt(f.Civ, new(2, 4), -1)); Equal(0, SharedGoals.PriorityAt(f.Civ, new(2, 4), 4));
    }
    private static void GoalRefreshOrdering()
    {
        var f = new GoalFixture(); CivilizationGoal[] current = [new(0, 0, 0, 6), new(2, 2, 0, -5), new(4, 4, 1, 0), new(6, 6, 0, -128), new(8, 8, -1, 7)];
        for (int i = 0; i < current.Length; ++i) f.Civ.Goals[i] = current[i];
        f.Civ.StandingGoals[0] = new(12, 2, 0, 3); f.Civ.StandingGoals[1] = new(14, 2, -2, 127); f.Civ.StandingGoals[2] = new(16, 2, 1, 3); f.Civ.StandingGoals[3] = new(18, 2, 1, 0); var standing = f.Civ.StandingGoals;
        f.Operations.Refresh(1); CivilizationGoal[] expected = [new(12, 2, 0, 3), new(16, 2, 1, 3), new(18, 2, 1, 0), new(0, 0, 0, -6), new(4, 4, 1, 0), new(-10, -20, -1, 0)];
        for (int i = 0; i < expected.Length; ++i) Equal(expected[i], f.Civ.Goals[i]); Equal(standing, f.Civ.StandingGoals);
        f = new(); f.Civ.Goals[0] = new(2, 2, 1, 5); f.Civ.Goals[1] = new(4, 4, 1, -128); f.Civ.Goals[2] = new(6, 6, 1, 0); f.Civ.StandingGoals[0] = new(8, 8, 1, 7); standing = f.Civ.StandingGoals; f.World.Civilizations[2] = f.Civ; var other = f.Civ;
        f.Operations.Refresh(1); SharedGoals.ResetStanding(ref f.Civ);
        for (int i = 0; i < 16; ++i) Equal(standing[i] with { Role = -1, Priority = 0 }, f.Civ.StandingGoals[i]);
        f.Operations.Refresh(1); Equal(new CivilizationGoal(8, 8, 1, -7), f.Civ.Goals[0]); Equal(new CivilizationGoal(2, 2, -1, -5), f.Civ.Goals[1]); Equal(new CivilizationGoal(6, 6, 1, 0), f.Civ.Goals[2]);
        f.Operations.Refresh(1); Equal(new CivilizationGoal(8, 8, -1, -7), f.Civ.Goals[0]); Equal(other, f.World.Civilizations[2]);
        f = new(); f.Civ.Goals[0] = new(2, 2, 1, -128); f.Civ.Goals[1] = new(4, 4, -1, 127); f.Civ.Goals[2] = new(6, 6, -2, 0); var before = f.Civ; before.Goals[0] = before.Goals[0] with { Role = -1 }; before.Goals[1] = before.Goals[1] with { Priority = -127 }; f.Operations.Refresh(1); Equal(before, f.Civ);
    }
    private static void GoalStandingDistance()
    {
        var f = new GoalFixture(); f.Civ.StandingGoals[0] = new(18, 4, 18, -128); f.Civ.StandingGoals[1] = new(4, 6, 18, 0); f.Civ.StandingGoals[2] = new(4, 8, 18, 6); f.Civ.StandingGoals[3] = new(0, 4, 1, 7); var before = f.Civ;
        f.Operations.ClearStandingNear(1, 274, new(0, 4), 2); var expected = before; expected.StandingGoals[0] = new(18, 4, -1, 0); Equal(expected, f.Civ);
        f.Civ = before; f.World.Globals.Rules |= 0x8000; f.Operations.ClearStandingNear(1, 18, new(0, 4), 3); expected = before; expected.StandingGoals[1] = new(4, 6, -1, 0); Equal(expected, f.Civ);
        f.Operations.ClearStandingNear(1, 1, new(0, 4), -1); Equal(expected, f.Civ); f.Operations.ClearStandingNear(1, 1, new(0, 4), 0); expected.StandingGoals[3] = new(0, 4, -1, 0); Equal(expected, f.Civ);
        f.World.Globals.Rules = 0; f.World.Map.Release(); f.Operations.ClearStandingNear(1, 18, new(0, 4), 1); Equal(new CivilizationGoal(18, 4, -1, 0), f.Civ.StandingGoals[0]); Equal(false, f.World.Map.Allocated);
    }
    private static void GoalRedirectionGates()
    {
        for (int gate = 0; gate < 4; ++gate)
        {
            var f = new GoalFixture(); f.Live(0); if (gate == 0) f.World.Globals.HumanPlayers = 2; if (gate == 1) f.World.Globals.TurnCiv = 2;
            f.Operations.AddCurrent(1, new(2, 4), gate == 2 ? 1 : gate == 3 ? 258 : 2, 5); Equal((sbyte)UnitOrder.None, (sbyte)f.Unit(0).Orders); Equal((sbyte)5, f.Civ.Goals[0].Priority); Equal(0, f.Reads.Count);
        }
        var filters = new GoalFixture(); for (int i = 0; i < 8; ++i) filters.Live(i);
        filters.Unit(0).Token = new(0); filters.Unit(1).Owner = new(2); filters.Unit(2).Type = new(255); filters.Unit(3).Type = new(1); filters.Process.Rules.Definitions.Units[1] = new() { Role = (RuleRole)254 };
        filters.Unit(4).X = 6; filters.World.Map.TileAt(new(6, 4)).Region = 1; filters.Unit(5).X = 8; filters.Unit(6).X = 5; filters.Unit(6).Y = 5; filters.Awake = slot => slot.Value != 2;
        var expected6 = filters.Unit(6); var expected7 = filters.Unit(7); expected6.Orders = expected7.Orders = UnitOrder.GoTo; expected6.DestinationX = expected7.DestinationX = 2; expected6.DestinationY = expected7.DestinationY = 4;
        filters.Operations.AddCurrent(1, new(2, 4), 2, 5); for (int i = 0; i < 6; ++i) Equal((sbyte)UnitOrder.None, (sbyte)filters.Unit(i).Orders); Equal(expected6, filters.Unit(6)); Equal(expected7, filters.Unit(7));
        Equal(new OwnedList<string>(["awake/2", "awake/3", "awake/4", "awake/5", "moves/5", "awake/6", "moves/6", "awake/7", "moves/7"]), new(filters.Reads));
    }
    private static void GoalRedirectionCapacity()
    {
        var f = new GoalFixture(); f.Live(0, 3); ((Span<CivilizationGoal>)f.Civ.Goals).Fill(new(0, 0, 0, 127)); var before = f.Civ;
        f.Operations.AddCurrent(1, new(2, 4), 3, 3); Equal(before, f.Civ); Equal((sbyte)UnitOrder.GoTo, (sbyte)f.Unit(0).Orders);
        f.Civ.Goals[47] = new(2, 4, 3, 3); f.Unit(0).Orders = UnitOrder.None; f.Reads.Clear(); f.Operations.AddCurrent(1, new(2, 4), 3, 3); Equal((sbyte)UnitOrder.None, (sbyte)f.Unit(0).Orders); Equal(0, f.Reads.Count);
    }
    private static void GoalRedirectionFailures()
    {
        for (int fault = 0; fault < 3; ++fault)
        {
            var f = new GoalFixture(); f.Live(0); f.Live(1); var before = f.Civ;
            if (fault == 0) f.Awake = null; if (fault == 1) f.Moves = null;
            if (fault == 2) { f.Unit(1).Type = new(255); Throws<ArgumentOutOfRangeException>(() => f.Operations.AddCurrent(1, new(2, 4), 2, 3)); }
            else Throws<InvalidOperationException>(() => f.Operations.AddCurrent(1, new(2, 4), 2, 3));
            Equal(before, f.Civ); Equal((sbyte)(fault == 2 ? UnitOrder.GoTo : UnitOrder.None), (sbyte)f.Unit(0).Orders); Equal((sbyte)UnitOrder.None, (sbyte)f.Unit(1).Orders);
        }
        var partial = new GoalFixture(); partial.Live(0); partial.Civ.Goals[0] = new(1, 2, 0, 5); partial.Civ.StandingGoals[0] = new(2, 4, 2, 7); partial.Moves = null;
        Throws<InvalidOperationException>(() => partial.Operations.Refresh(1)); Equal(new CivilizationGoal(1, 2, 0, -5), partial.Civ.Goals[0]);
    }
    private static void GoalBackingAndWrapping()
    {
        var f = new GoalFixture(false) { Awake = null, Moves = null }; f.Operations.AddCurrent(1, new(2, 4), 2, 3); f.Operations.ClearStandingNear(1, 18, new(2, 4), 3); f.Operations.AddStanding(1, new(2, 4), 18, 2); var before = f.Civ;
        Throws<InvalidOperationException>(() => f.Operations.ClearStandingNear(1, 18, new(2, 4), 3)); Throws<ArgumentOutOfRangeException>(() => f.Operations.AddCurrent(-1, new(2, 4), 0, 3)); Throws<ArgumentOutOfRangeException>(() => f.Operations.AddStanding(8, new(2, 4), 0, 3)); Throws<ArgumentOutOfRangeException>(() => f.Operations.Refresh(8)); Throws<ArgumentOutOfRangeException>(() => f.Operations.ClearStandingNear(-1, 18, new(2, 4), 3)); Equal(before, f.Civ);
        f = new(); f.Live(0, 2, new(0, 0)); f.World.Globals.Rules = 0x8000; f.Moves = _ => 0; f.Operations.AddCurrent(1, new(int.MaxValue, 0), 2, 4);
        Equal((sbyte)UnitOrder.GoTo, (sbyte)f.Unit(0).Orders); Equal((short)-1, f.Unit(0).DestinationX); Equal(new CivilizationGoal(-1, 0, 2, 4), f.Civ.Goals[0]);
    }
    private static void GoalRecordRetention()
    {
        var f = new GoalFixture(); f.Civ = StateConversion.ToLive(ClassicRecords.DecodeStoredCivilization(Enumerable.Repeat((byte)0xa5, 1428).ToArray())); var before = ClassicRecords.Encode(StateConversion.ToStored(f.Civ));
        SharedGoals.ResetStanding(ref f.Civ); f.Operations.AddStanding(1, new(32768, -32769), 511, 383); var encoded = ClassicRecords.Encode(StateConversion.ToStored(f.Civ));
        BytesEqual(before.Take(0x534), encoded.Take(0x534)); Equal(new CivilizationGoal(-32768, 32767, -1, 127), f.Civ.StandingGoals[0]); Equal(f.Civ, StateConversion.ToLive(ClassicRecords.DecodeStoredCivilization(encoded)));
    }
}
