using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.Ai;

public sealed partial class CivilizationAssessment
{
    private sealed partial class Assessment
    {
        private void Assign()
        {
            if (owner == 0)
            {
                return;
            }

            bool human = Human;
            if (!human)
            {
                if (settlersTotal == 0 && Civ.CityCount == 0)
                {
                    parent.elimination.Check(owner, 0, call + "/eliminate");
                }

                if ((World.Globals.Turn + owner) % 8 == 0)
                {
                    parent.governments.Choose(owner, false, call + "/government");
                }
            }
            int basis = Arithmetic.ClampAB(Civ.MilitaryCount >> 3, 3, 99);
            var weights = new int[State.Civilization.CurrentGoalCapacity];
            for (int g = 0; g < weights.Length; ++g)
            {
                weights[g] = basis * (Civ.Goals[g].Role == (int)RuleRole.Attack ? 2 : 1);
            }

            bool historical = World.Globals.Status.HasScenario && World.Scenario.Flags.HistoricalAi && owner == 3 && !human;
            for (int i = World.Entities.Units.End - 1; i >= 0; --i)
            {
                ref var unit = ref World.Entities.Units.Storage[i];
                if (!unit.Token.Occupied || unit.Owner.Value != owner || human && !unit.Flags.Automated)
                {
                    continue;
                }

                if (Signed(Rules.Units[unit.Type.Value].Attack) >= 99)
                { unit.AiTask = AiTaskIds.Nuclear; continue; }
                if (unit.Orders == UnitOrder.AiIdle)
                {
                    continue;
                }

                if ((int)unit.Orders <= 15 && unit.Orders != UnitOrder.GoTo)
                {
                    unit.AiTask = AiTaskIds.Unassigned;
                    if ((int)unit.Orders >= 4)
                    {
                        continue;
                    }
                }
                if (unit.Orders == UnitOrder.Sleeping)
                {
                    continue;
                }

                var point = unit.Position;
                if (!World.Map.Geometry.InBounds(point))
                {
                    continue;
                }

                int region = World.Map.TileAt(point).Region;
                if (unit.AiTask == AiTaskIds.Preserved)
                {
                    continue;
                }

                if (historical && ScenarioAssignment(i, point))
                {
                    continue;
                }

                if (!human && (World.Globals.Turn & i & 15) == 0 && Retire(i, point, region))
                {
                    continue;
                }

                SelectGoal(i, point, region, human, basis, weights);
            }
        }

        private bool ScenarioAssignment(int slot, MapPoint point)
        {
            ref var unit = ref World.Entities.Units.Storage[slot];
            int type = unit.Type.Value;
            if (type is not (UnitTypeIds.Armor or UnitTypeIds.AlpineTroops) || point.Y is < 40 or > 92)
            {
                return false;
            }

            var saved = unit.Flags;
            if (World.Globals.Turn == 1 || World.Map.TileAt(new(43, 53)).Owner != owner
                || type == UnitTypeIds.AlpineTroops && CountAt(new(43, 53)) < 3)
            {
                UnitOrders.SetGoto(ref unit, AiTaskIds.Goal, new(43, 53));
                unit.Flags.Scratch = unit.Flags.SpyMission = false;
                return true;
            }
            if (type == UnitTypeIds.Armor && World.Globals.Turn is >= 4 and <= 12)
            {
                if (!unit.Flags.Scratch && !unit.Flags.SpyMission)
                {
                    if (parent.random.PrimaryModulo(new ChoiceKey("A01.scenario-route", call, slot), 3) == 0)
                    {
                        unit.Flags.SpyMission = true;
                    }
                    else
                    {
                        unit.Flags.Scratch = true;
                    }
                }
                if (unit.Flags.Scratch && World.Map.TileAt(new(76, 34)).Owner != owner)
                {
                    UnitOrders.SetGoto(ref unit, AiTaskIds.Goal, new(76, 34));
                }
                else if (unit.Flags.SpyMission && (World.Map.TileAt(new(62, 72)).Owner != owner || World.Map.TileAt(new(67, 91)).Owner != owner))
                {
                    UnitOrders.SetGoto(ref unit, AiTaskIds.Goal, new(67, 91));
                }
            }
            unit.Flags = saved;
            return false;
        }

        private bool Retire(int slot, MapPoint point, int region)
        {
            ref var unit = ref World.Entities.Units.Storage[slot];
            int obsolete = (int)Rules.Units[unit.Type.Value].ObsoleteBy;
            if (!(obsolete >= 0 && Knows(obsolete) || unit.Type.Value == UnitTypeIds.Warriors
                && (byte)Civ.Government > (int)Government.Despotism && Civ.ContinentPolicy[region] == 4))
            {
                return false;
            }

            if (NearbyEnemy(point) || World.Map.CityOwner(point) is not null && CountAt(point) <= 1
                || World.Map.TileAt(point).Ocean || Civ.ContinentFlags[region].StrongerEnemy)
            {
                return false;
            }

            obsolete = (int)Rules.Units[unit.Type.Value].ObsoleteBy;
            var tile = World.Map.TileAt(point);
            if (obsolete >= 0 && Knows(obsolete) && !unit.Visibility.Contains(World.Multiplayer.LocalHuman)
                && unit.Owner.Value != unchecked((byte)World.Multiplayer.LocalHuman)
                && (tile.Features.HasFlag(MapFeatures.Fortification) && !tile.Features.HasFlag(MapFeatures.CitySite)
                    || World.Map.CityOwner(point) is not null && CountAt(point) == 1))
            {
                for (int t = 0; t < RuleLimits.UnitTypes; ++t)
                {
                    if ((int)Rules.Units[t].Prerequisite == (int)Rules.Units[unit.Type.Value].ObsoleteBy && Rules.Units[t].Role == Rules.Units[unit.Type.Value].Role)
                    { unit.Type = new((byte)t); unit.Flags.Veteran = false; break; }
                }

                return false;
            }
            if (CityQueries.At(World, point) is { } city)
            {
                World.Mood.City = null;
                new CitySupport(World, Rules).Calculate(city);
                ref var record = ref World.Entities.Cities.At(city);
                record.ShieldBox = unchecked((short)(record.ShieldBox + unchecked(Signed(Rules.Units[unit.Type.Value].CostRows) * World.Calculation.ShieldRows) / 2));
            }
            parent.loss.DestroyAndRedraw(slot, true);
            return true;
        }

        private void SelectGoal(int slot, MapPoint point, int region, bool human, int basis, int[] weights)
        {
            unchecked
            {
                ref var unit = ref World.Entities.Units.Storage[slot];
                int role = AssessmentQueries.Role(Rules, unit);
                if (Rules.Units[unit.Type.Value].Role == RuleRole.Trade && unit.Orders == UnitOrder.GoTo
                    && World.Map.CityOwner(unit.Destination) is byte destinationOwner && destinationOwner != owner)
                {
                    return;
                }

                if (Rules.Units[unit.Type.Value].Role == RuleRole.Attack && Signed(Rules.Units[unit.Type.Value].Attack) == 0)
                {
                    return;
                }

                int bestScore = 9999, selected = -1;
                for (int g = 0; g < weights.Length; ++g)
                {
                    ref var goal = ref Civ.Goals[g];
                    int priority = goal.Priority;
                    if (goal.Role == -1 || priority <= 0 || !World.Map.Geometry.InBounds(goal.Position))
                    {
                        continue;
                    }

                    var rule = Rules.Units[unit.Type.Value];
                    if (goal.Role == AiGoalRoles.AntiSubmarine && role == (int)RuleRole.NavalSuperiority && rule.Abilities.DetectSubmarines)
                    {
                        role = AiGoalRoles.AntiSubmarine;
                    }

                    if (!(goal.Role == role && World.Map.TileAt(goal.Position).Region == region
                        || rule.Domain == RuleDomain.Air && Signed(rule.Attack) >= Signed(Rules.Units[UnitTypeIds.Bomber].Attack)
                            && (goal.Role == role || goal.Role == (int)RuleRole.NavalSuperiority)))
                    {
                        continue;
                    }

                    int distance = World.Map.Geometry.WeightedDistance(point, goal.Position, Flat);
                    int allocation = weights[g] / (goal.Role == (int)RuleRole.Attack ? 2 : 1), score;
                    if (rule.Domain == RuleDomain.Air)
                    {
                        if (rule.Range == 1)
                        {
                            int movement = Arithmetic.Divide32(Signed(rule.Movement), Rules.Cosmic.RoadMovementMultiplier);
                            if (distance > (movement - 2 >> 1))
                            {
                                continue;
                            }

                            score = distance;
                        }
                        else
                        {
                            if ((priority & 1) == 0 || (((World.Globals.Turn >> 1) + g) & 1) == 0)
                            {
                                continue;
                            }

                            int movement = Arithmetic.Divide32(Signed(rule.Movement), Rules.Cosmic.RoadMovementMultiplier);
                            score = (distance <= movement ? 4 : 2) * distance / (priority + 1);
                        }
                    }
                    else
                    {
                        score = allocation * distance / (priority + 1);
                    }

                    if (unit.Orders is UnitOrder.Fortifying or UnitOrder.Fortified
                        && (priority <= 1 || score > priority * basis && allocation != basis || World.Map.CityOwner(point) is not null))
                    {
                        continue;
                    }

                    if (human && weights[g] == basis && World.Map.TileAt(goal.Position).Features.HasFlag(MapFeatures.Pollution))
                    {
                        score /= 8;
                    }

                    if (score < bestScore && score / basis < priority * 4)
                    { bestScore = score; selected = g; }
                }
                if (selected < 0)
                {
                    return;
                }

                ref var chosen = ref Civ.Goals[selected];
                if (chosen.Position == unit.Position)
                { unit.Orders = UnitOrder.AiIdle; unit.Flags.GoalReached = true; return; }
                var current = World.Map.TileAt(unit.Position);
                if (role == (int)RuleRole.Defend && current.Features.HasFlag(MapFeatures.Fortification) && !current.Features.HasFlag(MapFeatures.CitySite))
                {
                    if ((chosen.Priority & 1) == 0)
                    {
                        return;
                    }

                    Goals.AddStanding(owner, unit.Position, (int)RuleRole.Defend, 2);
                }
                if (Rules.Units[unit.Type.Value].Abilities.Paradrop && World.Map.CityOwner(chosen.Position) != owner)
                {
                    return;
                }

                UnitOrders.SetGoto(ref unit, AiTaskIds.Goal, chosen.Position);
                if (Rules.Units[unit.Type.Value].Role == RuleRole.Settle && World.Map.CityOwner(chosen.Position) is not null)
                {
                    chosen = chosen with { Priority = 0 };
                }

                ++weights[selected];
            }
        }
    }
}
