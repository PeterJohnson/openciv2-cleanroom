using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.Ai;

public sealed partial class CityProductionChoice
{
    private sealed partial class Evaluation
    {
        private void Survey()
        {
            unchecked
            {
                C = (sbyte)City.Owner.Value;
                R = World.Map.TileAt(City.Position).Region;
                P = Civ.ContinentPolicy[R];
                T = P is 0 or 1 || World.Globals.Status.AiCoordination && P == 4 ? 1 : 0;
                Race = (SpaceshipQueries.RaceBehind(World, Rules, C) ? 1 : 0) + (SpaceshipQueries.RaceActive(World) ? 1 : 0);
                int lost = 0;
                for (int i = 0; i < World.Entities.Cities.End; ++i)
                {
                    ref var other = ref World.Entities.Cities.Storage[i];
                    if (other.Token.Occupied && other.Founder.Value == C && other.Owner.Value != C
                        && World.Globals.HumanPlayers.Contains(other.Owner.Value) && World.Map.TileAt(other.Position).Region == R)
                    {
                        ++lost;
                    }
                }
                if (lost > A + 1)
                {
                    T = 1;
                }

                F = Work.Food - City.Size * Rules.Cosmic.FoodPerCitizen - Work.SupportedSettlers * Work.SettlerFood;
                S = 2 * Work.Shields - Work.ShieldSupport;
                Q = Work.Trade - Work.TradeCorruption;
                Wip = IsWonder(City.Production) && City.ShieldBox > 39;
                for (int w = 0; w < rivalry.Length; ++w)
                {
                    rivalry[w] = default;
                    if (BuildingQueries.ActiveCity(World, Rules, w) == slot.Value)
                    {
                        ++Wh;
                    }

                    if (Wonder(w))
                    {
                        ++Wo;
                    }
                }
                U = City.Flags.Disorder || City.Flags.MoodOnEdge && City.Flags.SpecialistHeld ? 2 : World.Mood.Happy == World.Mood.Unhappy ? 1 : 0;
                D = CityQueries.Nearest(World, City.Position, C, -1, -2).Distance;
                Tiles();
                Cities();
                // X01/A02: omit the conditional block that reads the nonexistent 63rd unit rule.
                for (int i = 0; i < World.Entities.Units.End; ++i)
                {
                    ref var other = ref World.Entities.Units.Storage[i];
                    if (!other.Token.Occupied || other.Owner.Value != C)
                    {
                        continue;
                    }

                    ++Ut;
                    if (World.Map.TileAt(other.Position).Region == R && other.Type.Value < present.Length)
                    {
                        ++present[other.Type.Value];
                    }
                }
                if (City.Flags.TerrainWorkGoal && !City.Flags.SettlerNearby && E == 0 && Work.SupportedSettlers == 0 && P != 1 && City.Size > 2)
                {
                    int nearest = 9999;
                    for (cursor = 0; cursor < World.Entities.Cities.End; ++cursor)
                    {
                        ref var other = ref World.Entities.Cities.Storage[cursor];
                        if (cursor != slot.Value && other.Token.Occupied && other.Owner.Value == C)
                        {
                            nearest = Math.Min(nearest, World.Map.Geometry.StepDistance(City.Position, other.Position, Flat));
                        }
                    }
                    if (nearest > 8)
                    {
                        Expand = 1;
                    }

                    if (City.Size > 6 && Government > (int)Contracts.Government.Fundamentalism && F > 0 && P != 0)
                    {
                        ++Expand;
                    }
                }
                for (int i = 0; i < Civilization.StandingGoalCapacity; ++i)
                {
                    if (Civ.StandingGoals[i].Role == (int)RuleRole.Defend && World.Map.TileAt(Civ.StandingGoals[i].Position).Region == R)
                    {
                        ++G;
                    }
                }

                if (T != 0)
                {
                    G += G >> 1;
                }
            }
        }

        private void Tiles()
        {
            int selected = -1;
            bool seek = F == 0 && !Human && !City.Flags.SettlerNearby && Work.SupportedSettlers == 0;
            for (int i = 0; i < 20; ++i)
            {
                var point = MapNeighborhoods.Offset(World.Map.Geometry, City.Position, MapNeighborhoods.Radius[i], Flat);
                if (!World.Map.Geometry.InBounds(point))
                {
                    continue;
                }

                var tile = World.Map.TileAt(point);
                if (tile.Ocean)
                {
                    O += 1 + (City.WorkedTiles.Contains(i) ? 3 : 0);
                }

                if (World.Map.UnitOwner(point) is byte owner && owner != C)
                {
                    int first = parent.stacks.FirstAt(point);
                    if (first >= 0 && World.Entities.Units.Storage[first].Owner.Value != C)
                    {
                        E += parent.stacks.Count(first);
                    }
                }
                if (tile.Features.HasFlag(MapFeatures.Irrigation | MapFeatures.Mine))
                {
                    ++Fm;
                }

                if (seek && TerrainImprovementChoice.Choose(World, Rules, C, point) == 1 && !tile.Features.HasFlag(MapFeatures.Irrigation)
                    && (int)(Rules.Terrain[(int)tile.Terrain].Work ?? throw new InvalidOperationException("A02 requires terrain work rules")).Irrigation.Result < 0)
                {
                    selected = i;
                    if (tile.Terrain == Terrain.Plains && World.Map.SpecialResource(point) != 0)
                    {
                        seek = false;
                    }
                }
            }
            if (selected < 0)
            {
                return;
            }

            var chosen = MapNeighborhoods.Offset(World.Map.Geometry, City.Position, MapNeighborhoods.Radius[selected], Flat);
            if (World.Map.TileAt(chosen).RevealedTo.Intersects(World.Globals.HumanPlayers))
            {
                return;
            }

            var features = MapFeatures.Irrigation | (World.Map.TileAt(chosen).River ? 0 : MapFeatures.Road);
            World.Map.Mutate(new TileChange.Features(chosen, features), true);
            ++F;
        }

        private void Cities()
        {
            B = Wonder(WonderIds.SunTzu) ? 4 : 0;
            for (cursor = 0; cursor < World.Entities.Cities.End; ++cursor)
            {
                ref var other = ref World.Entities.Cities.Storage[cursor];
                if (!other.Token.Occupied)
                {
                    continue;
                }

                var otherSlot = new CitySlot((ushort)cursor);
                int production = other.Production;
                if (other.Owner.Value == C)
                {
                    if (Has(otherSlot, BuildingIds.Palace) || production == -BuildingIds.Palace)
                    { ++palaces; largestPalace = Math.Max(largestPalace, other.Size); }
                    if (cursor == slot.Value)
                    {
                        continue;
                    }

                    if (production == City.Production)
                    {
                        ++Same;
                    }

                    if (other.TotalShields > City.TotalShields)
                    {
                        ++Ahead;
                    }

                    if (production is >= -37 and <= -35)
                    {
                        ++Sb;
                    }

                    if (World.Map.TileAt(other.Position).Region != R)
                    {
                        continue;
                    }

                    if (Has(otherSlot, BuildingIds.Barracks))
                    {
                        ++B;
                    }

                    if (Has(otherSlot, BuildingIds.Airport))
                    {
                        ++Ap;
                    }

                    if (Has(otherSlot, BuildingIds.PortFacility))
                    {
                        ++Pf;
                    }

                    if (production == -BuildingIds.Barracks)
                    {
                        ++B;
                    }

                    if (IsWonder(production))
                    {
                        ++Wb;
                        int w = -production - 39;
                        if (w < rivalry.Length && other.TotalShields > City.TotalShields)
                        {
                            rivalry[w] = rivalry[w].With(C);
                        }

                        if (Rules.Improvements[-production].CostRows * Work.ShieldRows <= other.ShieldBox && w < rivalry.Length)
                        {
                            rivalry[w] = rivalry[w].With(0);
                        }
                    }
                    if (production >= 0 && production < queued.Length)
                    {
                        ++queued[production];
                    }
                }
                else
                {
                    if (production == City.Production)
                    {
                        ++ForeignSame;
                    }

                    if (IsWonder(production) && -production - 39 < rivalry.Length)
                    {
                        rivalry[-production - 39] = rivalry[-production - 39].With(other.Owner.Value);
                    }

                    if (World.Globals.HumanPlayers.Contains(other.Owner.Value) && production is >= -37 and <= -35)
                    {
                        ++Hsb;
                    }
                }
            }
        }

        private int Barbarian()
        {
            int current = City.Production;
            if (current < 1)
            {
                return World.Globals.AdvanceKnownBy[AdvanceIds.Explosives] != default ? UnitTypeIds.Dragoons : UnitTypeIds.Legion;
            }

            var rule = Rules.Units[current];
            int obsolete = (int)rule.ObsoleteBy;
            if (obsolete < 0 || Difficulty == 0)
            {
                return current;
            }

            var known = World.Globals.AdvanceKnownBy[obsolete];
            int count = 0;
            for (int c = 0; c < EntityLimits.Civilizations; ++c)
            {
                if (known.Contains(c))
                {
                    ++count;
                }
            }

            if (count < 2 || Difficulty < (int)Contracts.Difficulty.Emperor && !known.Intersects(World.Globals.HumanPlayers))
            {
                return current;
            }

            for (int t = 0; t < RuleLimits.UnitTypes; ++t)
            {
                if (Rules.Units[t].Role == rule.Role && (int)Rules.Units[t].Prerequisite == obsolete)
                {
                    return t;
                }
            }

            return current;
        }

        private void DefenseAndAirlift()
        {
            if (P == 4)
            {
                for (int c = 1; c < EntityLimits.Civilizations; ++c)
                {
                    if (Civ.Treaties[c].War && World.Civilizations[c].ContinentCities[R] == 0)
                    {
                        Offshore = 1;
                    }
                }
            }

            int first = parent.stacks.FirstAt(City.Position);
            int defenders = StackRole(first, RuleRole.Defend);
            Naval = (StackRole(first, RuleRole.SeaTransport) >> 1) + StackRole(first, RuleRole.NavalSuperiority);
            if (defenders == 0)
            {
                Need = Urgent = 2;
            }
            else
            {
                int perDefender = SpatialControl.AdjacentNonalliedUnit(World, City.Position, C, true) is null ? 4 : 3;
                if (Has(BuildingIds.Palace))
                {
                    --perDefender;
                }
                else if (P == 5)
                {
                    ++perDefender;
                }

                int desired = (T != 0 || RegionalFlags ? 1 : 0) + Div(City.Size, perDefender);
                if (Has(BuildingIds.Palace))
                {
                    desired = Math.Max(desired, 1);
                    if (City.Size > 3)
                    {
                        desired = Math.Max(desired, 2);
                    }

                    if (Civ.CityCount > 7)
                    {
                        desired = Math.Max(desired, 3);
                    }

                    if (Civ.CityCount > 11)
                    {
                        desired = Math.Max(desired, 4);
                    }
                }
                if (defenders < desired)
                {
                    Need = 1;
                }

                if (defenders + 1 < desired)
                {
                    Urgent = 1;
                }
            }
            if (Human)
            {
                return;
            }

            foreach (var (wonder, building) in new[] { (WonderIds.Pyramids, BuildingIds.Granary), (WonderIds.Seti, BuildingIds.ResearchLab),
                (WonderIds.WomensSuffrage, BuildingIds.PoliceStation), (WonderIds.Michelangelo, BuildingIds.Cathedral) })
            {
                if (Wonder(wonder) && Has(building))
                {
                    BuildingQueries.SetBuilding(World, slot, building, false);
                    Civ.Gold = unchecked(Civ.Gold + Rules.Improvements[building].CostRows * Work.ShieldRows);
                }
            }

            if (!Has(BuildingIds.Airport) || C != World.Globals.TurnCiv || City.Flags.Airlifted || P is 4 or 5)
            {
                return;
            }

            for (cursor = 0; cursor < World.Entities.Cities.End; ++cursor)
            {
                ref var donor = ref World.Entities.Cities.Storage[cursor];
                var donorSlot = new CitySlot((ushort)cursor);
                if (!donor.Token.Occupied || cursor == slot.Value || donor.Owner.Value != C || !Has(donorSlot, BuildingIds.Airport)
                    || donor.Flags.Airlifted || SpatialControl.AdjacentNonalliedUnit(World, donor.Position, C) is not null)
                {
                    continue;
                }

                int region = World.Map.TileAt(donor.Position).Region;
                if (region == R ? Need <= 1 : Civ.ContinentPolicy[region] == P)
                {
                    continue;
                }

                int actor = parent.stacks.FirstAt(donor.Position);
                if (actor < 0 || P == 1 && StackRole(actor, RuleRole.Defend) < 2)
                {
                    continue;
                }

                int count = parent.stacks.Count(actor);
                if (count < (Civ.ContinentPolicy[region] == 4 ? 2 : 3))
                {
                    continue;
                }

                for (; actor >= 0; actor = parent.stacks.Next(actor))
                {
                    if (unchecked((sbyte)Rules.Units[World.Entities.Units.Storage[actor].Type.Value].Role) == P)
                    {
                        parent.airlift.Transfer(donorSlot, slot, new((ushort)actor), C, call + "/airlift");
                        break;
                    }
                }

                break;
            }
        }
    }
}
