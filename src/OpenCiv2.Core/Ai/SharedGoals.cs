using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.Ai;

/// <summary>Read-only U04 inputs on stable state; no mutation, event dispatch or goal reentry.</summary>
public interface IUnitMovementQueries
{
    bool CanAct(UnitSlot unit);

    int RemainingMoves(UnitSlot unit);
}

internal sealed class MovementGoalQueries(GameState world, RuleTables rules) : IUnitMovementQueries
{
    public bool CanAct(UnitSlot unit) => UnitActivation.AwakeWithMoves(world, rules, unit.Value);

    public int RemainingMoves(UnitSlot unit) => UnitMovement.Left(world, rules, world.Entities.Units.At(unit));
}
/// <summary>A01 §2 engine operations. Session authority and client admission belong to the caller.</summary>
public sealed class SharedGoals(GameState world, RuleTables rules, IUnitMovementQueries queries)
{
    /// <summary>Production composition with the actual U04 activation/movement queries.</summary>
    public SharedGoals(GameState world, RuleTables rules) : this(world, rules, new MovementGoalQueries(world, rules)) { }

    private ref Civilization Civilization(int slot)
    {
        if (slot < 0 || slot >= world.Civilizations.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(slot));
        }

        return ref world.Civilizations[slot];
    }

    public static void NegatePriority(ref CivilizationGoal goal) =>
        goal = goal with { Priority = unchecked((sbyte)-Math.Abs((int)goal.Priority)) };

    public static void ResetStanding(ref Civilization civilization)
    {
        foreach (ref var goal in (Span<CivilizationGoal>)civilization.StandingGoals)
        {
            goal = goal with { Role = -1, Priority = 0 };
        }
    }

    public static int PriorityAt(in Civilization civilization, MapPoint position, sbyte role)
    {
        int result = 0;
        foreach (ref readonly var goal in (ReadOnlySpan<CivilizationGoal>)civilization.Goals)
        {
            if (goal.Position == position && goal.Role == role
                && unchecked((sbyte)result) <= Math.Abs((int)goal.Priority))
            {
                result = Math.Abs((int)goal.Priority);
            }
        }

        return result;
    }

    private static bool Duplicate(ReadOnlySpan<CivilizationGoal> goals, MapPoint position, sbyte role, sbyte priority)
    {
        foreach (var goal in goals)
        {
            if (goal.Position == position && goal.Role == role && goal.Priority >= priority)
            {
                return true;
            }
        }

        return false;
    }

    private static void Insert(
        Span<CivilizationGoal> goals,
        MapPoint position,
        sbyte role,
        sbyte priority,
        bool standing)
    {
        for (int i = 0; i < goals.Length; ++i)
        {
            if (goals[i].Role != -1 && goals[i].Priority >= priority)
            {
                continue;
            }

            int end = goals.Length - 1;
            if (!standing)
            {
                end = i;
                while (end + 1 < goals.Length && goals[end].Role != -1)
                {
                    ++end;
                }
            }
            for (; end > i; --end)
            {
                goals[end] = goals[end - 1];
            }

            goals[i] = new(new(unchecked((short)position.X), unchecked((short)position.Y)), role, priority);
            return;
        }
    }

    public void AddCurrent(int civilization, MapPoint position, int role, int priority)
    {
        Span<CivilizationGoal> goals = Civilization(civilization).Goals;
        sbyte retainedRole = unchecked((sbyte)role), retainedPriority = unchecked((sbyte)priority);
        if (Duplicate(goals, position, retainedRole, retainedPriority))
        {
            return;
        }

        if (civilization == world.Globals.TurnCiv && !world.Globals.HumanPlayers.Contains(civilization)
            && role is 2 or 3)
        {
            int end = world.Entities.Units.End;
            for (int i = 0; i < end; ++i)
            {
                ref var unit = ref world.Entities.Units.Storage[i];
                if (!unit.Token.Occupied || unit.Owner.Value != civilization)
                {
                    continue;
                }

                var slot = new UnitSlot((ushort)i);
                if (!queries.CanAct(slot))
                {
                    continue;
                }

                if (unit.Type.Value >= rules.Units.Length)
                {
                    throw new ArgumentOutOfRangeException(nameof(unit.Type), "Goal redirection has no rule backing");
                }

                if (unchecked((sbyte)rules.Units[unit.Type.Value].Role) != role)
                {
                    continue;
                }

                byte region = world.Map.TileAt(unit.Position).Region;
                if (region != world.Map.TileAt(position).Region)
                {
                    continue;
                }

                int distance = world.Map.Geometry.WeightedDistance(
                    position,
                    unit.Position,
                    world.Globals.Rules.FlatWorld);
                int moves = queries.RemainingMoves(slot);
                if (unchecked(2 * distance) <= moves)
                {
                    unit.Orders = UnitOrder.GoTo;
                    unit.Destination = new(unchecked((short)position.X), unchecked((short)position.Y));
                }
            }
        }
        Insert(goals, position, retainedRole, retainedPriority, false);
    }

    public void AddStanding(int civilization, MapPoint position, int role, int priority)
    {
        Span<CivilizationGoal> goals = Civilization(civilization).StandingGoals;
        sbyte retainedRole = unchecked((sbyte)role), retainedPriority = unchecked((sbyte)priority);
        if (!Duplicate(goals, position, retainedRole, retainedPriority))
        {
            Insert(goals, position, retainedRole, retainedPriority, true);
        }
    }

    public void Refresh(int civilization)
    {
        ref var state = ref Civilization(civilization);
        foreach (ref var goal in (Span<CivilizationGoal>)state.Goals)
        {
            if (goal.Priority < 0)
            {
                goal = goal with { Role = -1 };
            }

            NegatePriority(ref goal);
        }
        foreach (var goal in (ReadOnlySpan<CivilizationGoal>)state.StandingGoals)
        {
            if (goal.Role >= 0)
            {
                AddCurrent(civilization, goal.Position, goal.Role, goal.Priority);
            }
        }
    }

    public void ClearStandingNear(int civilization, int role, MapPoint position, int limit)
    {
        foreach (ref var goal in (Span<CivilizationGoal>)Civilization(civilization).StandingGoals)
        {
            if (goal.Role != unchecked((sbyte)role))
            {
                continue;
            }

            int distance = world.Map.Geometry.WeightedDistance(
                position,
                goal.Position,
                world.Globals.Rules.FlatWorld);
            if (distance <= limit)
            {
                goal = goal with { Role = -1, Priority = 0 };
            }
        }
    }
}
