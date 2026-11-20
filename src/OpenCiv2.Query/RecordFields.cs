using System.Globalization;
using OpenCiv2.Core;
using OpenCiv2.Formats;

namespace OpenCiv2.Query;

// Statically enumerated F06 projections: trimming-safe and independent of CLR layouts.
internal static class RecordFields
{
    public static List<Field> Of(StoredGlobals value)
    {
        var fields = new List<Field>();
        Append(value, "", fields);
        return fields;
    }

    private static void Append(StoredGlobals v, string prefix, List<Field> fields)
    {
        fields.Add(new(prefix + "rules", "u16", new FieldValue.Unsigned(v.Rules)));
        fields.Add(new(prefix + "game_options", "u32", new FieldValue.Unsigned(v.GameOptions)));
        fields.Add(new(prefix + "transient_flags", "u16", new FieldValue.Unsigned(v.TransientFlags)));
        fields.Add(new(prefix + "game_flags", "u16", new FieldValue.Unsigned(v.GameFlags)));
        fields.Add(new(prefix + "message_options", "u16", new FieldValue.Unsigned(v.MessageOptions)));
        fields.Add(new(prefix + "shown_event_flags", "u16", new FieldValue.Unsigned(v.ShownEventFlags)));
        fields.Add(new(prefix + "retained_0e", "u16", new FieldValue.Unsigned(v.Retained0e)));
        fields.Add(new(prefix + "turn", "i16", new FieldValue.Signed(v.Turn)));
        fields.Add(new(prefix + "year", "i16", new FieldValue.Signed(v.Year)));
        fields.Add(new(prefix + "first_launch_turn", "i16", new FieldValue.Signed(v.FirstLaunchTurn)));
        fields.Add(new(prefix + "active_unit", "i16", new FieldValue.Signed(v.ActiveUnit)));
        fields.Add(new(prefix + "moving_unit", "i16", new FieldValue.Signed(v.MovingUnit)));
        fields.Add(new(prefix + "multiplayer", "u8", new FieldValue.Unsigned(v.Multiplayer)));
        fields.Add(new(prefix + "player_civ", "i8", new FieldValue.Signed(v.PlayerCiv)));
        fields.Add(new(prefix + "saved_current_civ", "i8", new FieldValue.Signed(v.SavedCurrentCiv)));
        fields.Add(new(prefix + "turn_civ", "i8", new FieldValue.Signed(v.TurnCiv)));
        fields.Add(new(prefix + "view_civ", "i8", new FieldValue.Signed(v.ViewCiv)));
        fields.Add(new(prefix + "reveal_map", "u8", new FieldValue.Unsigned(v.RevealMap)));
        fields.Add(new(prefix + "difficulty", "u8", new FieldValue.Unsigned(v.Difficulty)));
        fields.Add(new(prefix + "barbarian_activity", "u8", new FieldValue.Unsigned(v.BarbarianActivity)));
        fields.Add(new(prefix + "civs_alive", "u8", new FieldValue.Unsigned(v.CivsAlive)));
        fields.Add(new(prefix + "human_players", "u8", new FieldValue.Unsigned(v.HumanPlayers)));
        fields.Add(new(prefix + "civs_at_start", "u8", new FieldValue.Unsigned(v.CivsAtStart)));
        fields.Add(new(prefix + "num_enemies", "u8", new FieldValue.Unsigned(v.NumEnemies)));
        fields.Add(new(prefix + "warming_counter", "i8", new FieldValue.Signed(v.WarmingCounter)));
        fields.Add(new(prefix + "global_warmings", "i8", new FieldValue.Signed(v.GlobalWarmings)));
        fields.Add(new(prefix + "warming_pollution", "i16", new FieldValue.Signed(v.WarmingPollution)));
        fields.Add(new(prefix + "polluted_tiles", "i16", new FieldValue.Signed(v.PollutedTiles)));
        fields.Add(new(prefix + "peace_turns", "i16", new FieldValue.Signed(v.PeaceTurns)));
        fields.Add(new(prefix + "unit_end", "i16", new FieldValue.Signed(v.UnitEnd)));
        fields.Add(new(prefix + "city_end", "i16", new FieldValue.Signed(v.CityEnd)));
        fields.Add(new(prefix + "num_advances", "i16", new FieldValue.Signed(v.NumAdvances)));
        fields.Add(new(prefix + "retained_34", "i16", new FieldValue.Signed(v.Retained34)));
        for (int i1 = 0; i1 < v.FirstDiscoverer.Count; ++i1)
        {
            fields.Add(
                new(
                    prefix + "first_discoverer" + "[" + i1.ToString(CultureInfo.InvariantCulture) + "]",
                    "u8",
                    new FieldValue.Unsigned(v.FirstDiscoverer[i1])));
        }
        for (int i2 = 0; i2 < v.AdvanceKnownBy.Count; ++i2)
        {
            fields.Add(
                new(
                    prefix + "advance_known_by" + "[" + i2.ToString(CultureInfo.InvariantCulture) + "]",
                    "u8",
                    new FieldValue.Unsigned(v.AdvanceKnownBy[i2])));
        }
        for (int i3 = 0; i3 < v.WonderCity.Count; ++i3)
        {
            fields.Add(
                new(
                    prefix + "wonder_city" + "[" + i3.ToString(CultureInfo.InvariantCulture) + "]",
                    "i16",
                    new FieldValue.Signed(v.WonderCity[i3])));
        }
        fields.Add(new(prefix + "next_history_turn", "i16", new FieldValue.Signed(v.NextHistoryTurn)));
        fields.Add(new(prefix + "best_human_rank", "u8", new FieldValue.Unsigned(v.BestHumanRank)));
        fields.Add(new(prefix + "worst_human_rank", "u8", new FieldValue.Unsigned(v.WorstHumanRank)));
        for (int i4 = 0; i4 < v.PowerRank.Count; ++i4)
        {
            fields.Add(
                new(
                    prefix + "power_rank" + "[" + i4.ToString(CultureInfo.InvariantCulture) + "]",
                    "u8",
                    new FieldValue.Unsigned(v.PowerRank[i4])));
        }
        for (int i5 = 0; i5 < v.CivByRank.Count; ++i5)
        {
            fields.Add(
                new(
                    prefix + "civ_by_rank" + "[" + i5.ToString(CultureInfo.InvariantCulture) + "]",
                    "u8",
                    new FieldValue.Unsigned(v.CivByRank[i5])));
        }
    }

    public static List<Field> Of(StoredCivilization value)
    {
        var fields = new List<Field>();
        Append(value, "", fields);
        return fields;
    }

    private static void Append(StoredCivilization v, string prefix, List<Field> fields)
    {
        fields.Add(new(prefix + "flags", "u16", new FieldValue.Unsigned(v.Flags)));
        fields.Add(new(prefix + "gold", "i32", new FieldValue.Signed(v.Gold)));
        fields.Add(new(prefix + "tribe", "i16", new FieldValue.Signed(v.Tribe)));
        fields.Add(new(prefix + "research_progress", "i16", new FieldValue.Signed(v.ResearchProgress)));
        fields.Add(new(prefix + "researching", "i16", new FieldValue.Signed(v.Researching)));
        fields.Add(new(prefix + "capital_x", "i16", new FieldValue.Signed(v.CapitalX)));
        fields.Add(new(prefix + "last_city_turn", "i16", new FieldValue.Signed(v.LastCityTurn)));
        fields.Add(new(prefix + "num_advances", "u8", new FieldValue.Unsigned(v.NumAdvances)));
        fields.Add(new(prefix + "future_techs", "u8", new FieldValue.Unsigned(v.FutureTechs)));
        fields.Add(new(prefix + "research_cost_advances", "u8", new FieldValue.Unsigned(v.ResearchCostAdvances)));
        fields.Add(new(prefix + "science_rate", "u8", new FieldValue.Unsigned(v.ScienceRate)));
        fields.Add(new(prefix + "tax_rate", "u8", new FieldValue.Unsigned(v.TaxRate)));
        fields.Add(new(prefix + "government", "u8", new FieldValue.Unsigned(v.Government)));
        fields.Add(new(prefix + "diplo_random", "u8", new FieldValue.Unsigned(v.DiploRandom)));
        for (int i6 = 0; i6 < v.WondersByGroup.Count; ++i6)
        {
            fields.Add(
                new(
                    prefix + "wonders_by_group" + "[" + i6.ToString(CultureInfo.InvariantCulture) + "]",
                    "u8",
                    new FieldValue.Unsigned(v.WondersByGroup[i6])));
        }
        fields.Add(new(prefix + "retained_1b", "u8", new FieldValue.Unsigned(v.Retained1b)));
        fields.Add(new(prefix + "subversion_count", "i16", new FieldValue.Signed(v.SubversionCount)));
        fields.Add(new(prefix + "reputation", "u8", new FieldValue.Unsigned(v.Reputation)));
        fields.Add(new(prefix + "diplo_request_count", "i8", new FieldValue.Signed(v.DiploRequestCount)));
        for (int i7 = 0; i7 < v.Treaties.Count; ++i7)
        {
            fields.Add(
                new(
                    prefix + "treaties" + "[" + i7.ToString(CultureInfo.InvariantCulture) + "]",
                    "u32",
                    new FieldValue.Unsigned(v.Treaties[i7])));
        }
        for (int i8 = 0; i8 < v.Attitude.Count; ++i8)
        {
            fields.Add(
                new(
                    prefix + "attitude" + "[" + i8.ToString(CultureInfo.InvariantCulture) + "]",
                    "u8",
                    new FieldValue.Unsigned(v.Attitude[i8])));
        }
        for (int i9 = 0; i9 < v.ReputationCredit.Count; ++i9)
        {
            fields.Add(
                new(
                    prefix + "reputation_credit" + "[" + i9.ToString(CultureInfo.InvariantCulture) + "]",
                    "i8",
                    new FieldValue.Signed(v.ReputationCredit[i9])));
        }
        for (int i10 = 0; i10 < v.AggressionCounter.Count; ++i10)
        {
            fields.Add(
                new(
                    prefix + "aggression_counter" + "[" + i10.ToString(CultureInfo.InvariantCulture) + "]",
                    "u8",
                    new FieldValue.Unsigned(v.AggressionCounter[i10])));
        }
        for (int i11 = 0; i11 < v.Advances.Count; ++i11)
        {
            fields.Add(
                new(
                    prefix + "advances" + "[" + i11.ToString(CultureInfo.InvariantCulture) + "]",
                    "u8",
                    new FieldValue.Unsigned(v.Advances[i11])));
        }
        fields.Add(new(prefix + "retained_65", "u8", new FieldValue.Unsigned(v.Retained65)));
        fields.Add(new(prefix + "military_count", "i16", new FieldValue.Signed(v.MilitaryCount)));
        fields.Add(new(prefix + "city_count", "i16", new FieldValue.Signed(v.CityCount)));
        fields.Add(new(prefix + "ship_count", "i16", new FieldValue.Signed(v.ShipCount)));
        fields.Add(new(prefix + "total_city_size", "i16", new FieldValue.Signed(v.TotalCitySize)));
        fields.Add(new(prefix + "military_strength", "u16", new FieldValue.Unsigned(v.MilitaryStrength)));
        fields.Add(new(prefix + "total_attack", "i16", new FieldValue.Signed(v.TotalAttack)));
        fields.Add(new(prefix + "population_milestone", "i16", new FieldValue.Signed(v.PopulationMilestone)));
        for (int i12 = 0; i12 < v.AdvanceSource.Count; ++i12)
        {
            fields.Add(
                new(
                    prefix + "advance_source" + "[" + i12.ToString(CultureInfo.InvariantCulture) + "]",
                    "u8",
                    new FieldValue.Unsigned(v.AdvanceSource[i12])));
        }
        for (int i13 = 0; i13 < v.UnitsActive.Count; ++i13)
        {
            fields.Add(
                new(
                    prefix + "units_active" + "[" + i13.ToString(CultureInfo.InvariantCulture) + "]",
                    "u8",
                    new FieldValue.Unsigned(v.UnitsActive[i13])));
        }
        for (int i14 = 0; i14 < v.UnitsLost.Count; ++i14)
        {
            fields.Add(
                new(
                    prefix + "units_lost" + "[" + i14.ToString(CultureInfo.InvariantCulture) + "]",
                    "u8",
                    new FieldValue.Unsigned(v.UnitsLost[i14])));
        }
        for (int i15 = 0; i15 < v.UnitsInProduction.Count; ++i15)
        {
            fields.Add(
                new(
                    prefix + "units_in_production" + "[" + i15.ToString(CultureInfo.InvariantCulture) + "]",
                    "u8",
                    new FieldValue.Unsigned(v.UnitsInProduction[i15])));
        }
        for (int i16 = 0; i16 < v.ContinentMilitary.Count; ++i16)
        {
            fields.Add(
                new(
                    prefix + "continent_military" + "[" + i16.ToString(CultureInfo.InvariantCulture) + "]",
                    "u16",
                    new FieldValue.Unsigned(v.ContinentMilitary[i16])));
        }
        for (int i17 = 0; i17 < v.ContinentAttack.Count; ++i17)
        {
            fields.Add(
                new(
                    prefix + "continent_attack" + "[" + i17.ToString(CultureInfo.InvariantCulture) + "]",
                    "u16",
                    new FieldValue.Unsigned(v.ContinentAttack[i17])));
        }
        for (int i18 = 0; i18 < v.ContinentCities.Count; ++i18)
        {
            fields.Add(
                new(
                    prefix + "continent_cities" + "[" + i18.ToString(CultureInfo.InvariantCulture) + "]",
                    "u8",
                    new FieldValue.Unsigned(v.ContinentCities[i18])));
        }
        for (int i19 = 0; i19 < v.ContinentCitySize.Count; ++i19)
        {
            fields.Add(
                new(
                    prefix + "continent_city_size" + "[" + i19.ToString(CultureInfo.InvariantCulture) + "]",
                    "u8",
                    new FieldValue.Unsigned(v.ContinentCitySize[i19])));
        }
        for (int i20 = 0; i20 < v.ContinentTradeUnits.Count; ++i20)
        {
            fields.Add(
                new(
                    prefix + "continent_trade_units" + "[" + i20.ToString(CultureInfo.InvariantCulture) + "]",
                    "u8",
                    new FieldValue.Unsigned(v.ContinentTradeUnits[i20])));
        }
        for (int i21 = 0; i21 < v.ContinentFlags.Count; ++i21)
        {
            fields.Add(
                new(
                    prefix + "continent_flags" + "[" + i21.ToString(CultureInfo.InvariantCulture) + "]",
                    "u8",
                    new FieldValue.Unsigned(v.ContinentFlags[i21])));
        }
        for (int i22 = 0; i22 < v.ContinentPolicy.Count; ++i22)
        {
            fields.Add(
                new(
                    prefix + "continent_policy" + "[" + i22.ToString(CultureInfo.InvariantCulture) + "]",
                    "u8",
                    new FieldValue.Unsigned(v.ContinentPolicy[i22])));
        }
        fields.Add(new(prefix + "best_shield_surplus", "i16", new FieldValue.Signed(v.BestShieldSurplus)));
        for (int i23 = 0; i23 < v.GovernmentScore.Count; ++i23)
        {
            fields.Add(
                new(
                    prefix + "government_score" + "[" + i23.ToString(CultureInfo.InvariantCulture) + "]",
                    "i16",
                    new FieldValue.Signed(v.GovernmentScore[i23])));
        }
        for (int i24 = 0; i24 < v.LastContactTurn.Count; ++i24)
        {
            fields.Add(
                new(
                    prefix + "last_contact_turn" + "[" + i24.ToString(CultureInfo.InvariantCulture) + "]",
                    "i16",
                    new FieldValue.Signed(v.LastContactTurn[i24])));
        }
        fields.Add(new(prefix + "leader_portrait", "u8", new FieldValue.Unsigned(v.LeaderPortrait)));
        for (int i25 = 0; i25 < v.ThroneRoom.Count; ++i25)
        {
            fields.Add(
                new(
                    prefix + "throne_room" + "[" + i25.ToString(CultureInfo.InvariantCulture) + "]",
                    "u8",
                    new FieldValue.Unsigned(v.ThroneRoom[i25])));
        }
        fields.Add(new(prefix + "retained_3fd", "u8", new FieldValue.Unsigned(v.Retained3fd)));
        fields.Add(new(prefix + "throne_room_upgrades", "i16", new FieldValue.Signed(v.ThroneRoomUpgrades)));
        fields.Add(new(prefix + "spaceship_flags", "u8", new FieldValue.Unsigned(v.SpaceshipFlags)));
        fields.Add(new(prefix + "retained_401", "u8", new FieldValue.Unsigned(v.Retained401)));
        fields.Add(new(prefix + "spaceship_arrival_year", "i16", new FieldValue.Signed(v.SpaceshipArrivalYear)));
        fields.Add(new(prefix + "spaceship_launch_year", "i16", new FieldValue.Signed(v.SpaceshipLaunchYear)));
        fields.Add(new(prefix + "spaceship_population", "i16", new FieldValue.Signed(v.SpaceshipPopulation)));
        for (int i26 = 0; i26 < v.SpaceshipParts.Count; ++i26)
        {
            fields.Add(
                new(
                    prefix + "spaceship_parts" + "[" + i26.ToString(CultureInfo.InvariantCulture) + "]",
                    "i16",
                    new FieldValue.Signed(v.SpaceshipParts[i26])));
        }
        for (int i27 = 0; i27 < v.Goals.Count; ++i27)
        {
            Append(
                v.Goals[i27],
                prefix + "goals" + "[" + i27.ToString(CultureInfo.InvariantCulture) + "]" + ".",
                fields);
        }
        for (int i28 = 0; i28 < v.StandingGoals.Count; ++i28)
        {
            Append(
                v.StandingGoals[i28],
                prefix + "standing_goals" + "[" + i28.ToString(CultureInfo.InvariantCulture) + "]" + ".",
                fields);
        }
    }

    public static List<Field> Of(StoredCivilizationNames value)
    {
        var fields = new List<Field>();
        Append(value, "", fields);
        return fields;
    }

    private static void Append(StoredCivilizationNames v, string prefix, List<Field> fields)
    {
        fields.Add(new(prefix + "city_style", "i16", new FieldValue.Signed(v.CityStyle)));
        fields.Add(new(prefix + "leader", "classic-text", new FieldValue.Bytes(v.Leader)));
        fields.Add(new(prefix + "tribe", "classic-text", new FieldValue.Bytes(v.Tribe)));
        fields.Add(new(prefix + "adjective", "classic-text", new FieldValue.Bytes(v.Adjective)));
        for (int i29 = 0; i29 < v.Titles.Count; ++i29)
        {
            fields.Add(
                new(
                    prefix + "titles" + "[" + i29.ToString(CultureInfo.InvariantCulture) + "]",
                    "classic-text",
                    new FieldValue.Bytes(v.Titles[i29])));
        }
    }

    public static List<Field> Of(StoredLeader value)
    {
        var fields = new List<Field>();
        Append(value, "", fields);
        return fields;
    }

    private static void Append(StoredLeader v, string prefix, List<Field> fields)
    {
        fields.Add(new(prefix + "attack", "i8", new FieldValue.Signed(v.Attack)));
        fields.Add(new(prefix + "expand", "i8", new FieldValue.Signed(v.Expand)));
        fields.Add(new(prefix + "civilize", "i8", new FieldValue.Signed(v.Civilize)));
        fields.Add(new(prefix + "times_used", "u8", new FieldValue.Unsigned(v.TimesUsed)));
        fields.Add(new(prefix + "female", "u8", new FieldValue.Unsigned(v.Female)));
        fields.Add(new(prefix + "city_name_index", "u8", new FieldValue.Unsigned(v.CityNameIndex)));
        fields.Add(new(prefix + "color", "i16", new FieldValue.Signed(v.Color)));
        fields.Add(new(prefix + "city_style", "i16", new FieldValue.Signed(v.CityStyle)));
        fields.Add(new(prefix + "leader_name", "i16", new FieldValue.Signed(v.LeaderName)));
        fields.Add(new(prefix + "plural", "i16", new FieldValue.Signed(v.Plural)));
        fields.Add(new(prefix + "adjective", "i16", new FieldValue.Signed(v.Adjective)));
        for (int i30 = 0; i30 < v.Names.Count; ++i30)
        {
            fields.Add(
                new(
                    prefix + "names" + "[" + i30.ToString(CultureInfo.InvariantCulture) + "]",
                    "i16",
                    new FieldValue.Signed(v.Names[i30])));
        }
        for (int i31 = 0; i31 < v.Titles.Count; ++i31)
        {
            for (int i32 = 0; i32 < v.Titles[i31].Count; ++i32)
            {
                fields.Add(
                    new(
                        prefix + "titles" + "[" + i31.ToString(
                            CultureInfo.InvariantCulture) + "]" + "[" + i32.ToString(
                                CultureInfo.InvariantCulture) + "]",
                        "i16",
                        new FieldValue.Signed(v.Titles[i31][i32])));
            }
        }
    }

    public static List<Field> Of(SavedRectangle value)
    {
        var fields = new List<Field>();
        Append(value, "", fields);
        return fields;
    }

    private static void Append(SavedRectangle v, string prefix, List<Field> fields)
    {
        fields.Add(new(prefix + "left", "i16", new FieldValue.Signed(v.Left)));
        fields.Add(new(prefix + "top", "i16", new FieldValue.Signed(v.Top)));
        fields.Add(new(prefix + "right", "i16", new FieldValue.Signed(v.Right)));
        fields.Add(new(prefix + "bottom", "i16", new FieldValue.Signed(v.Bottom)));
    }

    public static List<Field> Of(SavedLayout value)
    {
        var fields = new List<Field>();
        Append(value, "", fields);
        return fields;
    }

    private static void Append(SavedLayout v, string prefix, List<Field> fields)
    {
        fields.Add(new(prefix + "valid", "i16", new FieldValue.Signed(v.Valid)));
        for (int i33 = 0; i33 < v.Rectangles.Count; ++i33)
        {
            Append(
                v.Rectangles[i33],
                prefix + "rectangles" + "[" + i33.ToString(CultureInfo.InvariantCulture) + "]" + ".",
                fields);
        }
    }

    public static List<Field> Of(SavedMapView value)
    {
        var fields = new List<Field>();
        Append(value, "", fields);
        return fields;
    }

    private static void Append(SavedMapView v, string prefix, List<Field> fields)
    {
        fields.Add(new(prefix + "open", "i16", new FieldValue.Signed(v.Open)));
        fields.Add(new(prefix + "track_moves", "i16", new FieldValue.Signed(v.TrackMoves)));
        fields.Add(new(prefix + "x", "i16", new FieldValue.Signed(v.X)));
        fields.Add(new(prefix + "y", "i16", new FieldValue.Signed(v.Y)));
        fields.Add(new(prefix + "zoom", "i16", new FieldValue.Signed(v.Zoom)));
    }

    public static List<Field> Of(ScenarioParameters value)
    {
        var fields = new List<Field>();
        Append(value, "", fields);
        return fields;
    }

    private static void Append(ScenarioParameters v, string prefix, List<Field> fields)
    {
        fields.Add(new(prefix + "flags", "u16", new FieldValue.Unsigned(v.Flags)));
        fields.Add(new(prefix + "name", "classic-text", new FieldValue.Bytes(v.Name)));
        fields.Add(new(prefix + "technology_paradigm", "i16", new FieldValue.Signed(v.TechnologyParadigm)));
        fields.Add(new(prefix + "year_increment", "i16", new FieldValue.Signed(v.YearIncrement)));
        fields.Add(new(prefix + "start_year", "i16", new FieldValue.Signed(v.StartYear)));
        fields.Add(new(prefix + "maximum_turns", "i16", new FieldValue.Signed(v.MaximumTurns)));
        fields.Add(new(prefix + "objective_civ", "i16", new FieldValue.Signed(v.ObjectiveCiv)));
        for (int i34 = 0; i34 < v.VictoryThresholds.Count; ++i34)
        {
            fields.Add(
                new(
                    prefix + "victory_thresholds" + "[" + i34.ToString(CultureInfo.InvariantCulture) + "]",
                    "i16",
                    new FieldValue.Signed(v.VictoryThresholds[i34])));
        }
    }

    public static List<Field> Of(EliminationHistory value)
    {
        var fields = new List<Field>();
        Append(value, "", fields);
        return fields;
    }

    private static void Append(EliminationHistory v, string prefix, List<Field> fields)
    {
        fields.Add(new(prefix + "count", "i16", new FieldValue.Signed(v.Count)));
        for (int i35 = 0; i35 < v.Turns.Count; ++i35)
        {
            fields.Add(
                new(
                    prefix + "turns" + "[" + i35.ToString(CultureInfo.InvariantCulture) + "]",
                    "i16",
                    new FieldValue.Signed(v.Turns[i35])));
        }
        for (int i36 = 0; i36 < v.Killers.Count; ++i36)
        {
            fields.Add(
                new(
                    prefix + "killers" + "[" + i36.ToString(CultureInfo.InvariantCulture) + "]",
                    "u8",
                    new FieldValue.Unsigned(v.Killers[i36])));
        }
        for (int i37 = 0; i37 < v.Portraits.Count; ++i37)
        {
            fields.Add(
                new(
                    prefix + "portraits" + "[" + i37.ToString(CultureInfo.InvariantCulture) + "]",
                    "u8",
                    new FieldValue.Unsigned(v.Portraits[i37])));
        }
        for (int i38 = 0; i38 < v.Names.Count; ++i38)
        {
            fields.Add(
                new(
                    prefix + "names" + "[" + i38.ToString(CultureInfo.InvariantCulture) + "]",
                    "classic-text",
                    new FieldValue.Bytes(v.Names[i38])));
        }
    }

    public static List<Field> Of(MultiplayerRecord value)
    {
        var fields = new List<Field>();
        Append(value, "", fields);
        return fields;
    }

    private static void Append(MultiplayerRecord v, string prefix, List<Field> fields)
    {
        for (int i39 = 0; i39 < v.CenterX.Count; ++i39)
        {
            fields.Add(
                new(
                    prefix + "center_x" + "[" + i39.ToString(CultureInfo.InvariantCulture) + "]",
                    "i16",
                    new FieldValue.Signed(v.CenterX[i39])));
        }
        for (int i40 = 0; i40 < v.CenterY.Count; ++i40)
        {
            fields.Add(
                new(
                    prefix + "center_y" + "[" + i40.ToString(CultureInfo.InvariantCulture) + "]",
                    "i16",
                    new FieldValue.Signed(v.CenterY[i40])));
        }
        for (int i41 = 0; i41 < v.Introduced.Count; ++i41)
        {
            fields.Add(
                new(
                    prefix + "introduced" + "[" + i41.ToString(CultureInfo.InvariantCulture) + "]",
                    "i16",
                    new FieldValue.Signed(v.Introduced[i41])));
        }
        fields.Add(new(prefix + "turn_time_limit", "i32", new FieldValue.Signed(v.TurnTimeLimit)));
        for (int i42 = 0; i42 < v.EncodedPasswords.Count; ++i42)
        {
            fields.Add(
                new(
                    prefix + "encoded_passwords" + "[" + i42.ToString(CultureInfo.InvariantCulture) + "]",
                    "u8",
                    new FieldValue.Unsigned(v.EncodedPasswords[i42])));
        }
        fields.Add(new(prefix + "open_game", "i16", new FieldValue.Signed(v.OpenGame)));
        fields.Add(new(prefix + "kill_on_retirement", "i16", new FieldValue.Signed(v.KillOnRetirement)));
        fields.Add(new(prefix + "show_human_starts", "i16", new FieldValue.Signed(v.ShowHumanStarts)));
        fields.Add(new(prefix + "chat_from_start", "i16", new FieldValue.Signed(v.ChatFromStart)));
        fields.Add(new(prefix + "simultaneous", "i16", new FieldValue.Signed(v.Simultaneous)));
        fields.Add(new(prefix + "cheat_turn_time", "i32", new FieldValue.Signed(v.CheatTurnTime)));
        for (int i43 = 0; i43 < v.Retained142.Count; ++i43)
        {
            fields.Add(
                new(
                    prefix + "retained_142" + "[" + i43.ToString(CultureInfo.InvariantCulture) + "]",
                    "u8",
                    new FieldValue.Unsigned(v.Retained142[i43])));
        }
        for (int i44 = 0; i44 < v.Emails.Count; ++i44)
        {
            for (int i45 = 0; i45 < v.Emails[i44].Count; ++i45)
            {
                fields.Add(
                    new(
                        prefix + "emails" + "[" + i44.ToString(
                            CultureInfo.InvariantCulture) + "]" + "[" + i45.ToString(
                                CultureInfo.InvariantCulture) + "]",
                        "u8",
                        new FieldValue.Unsigned(v.Emails[i44][i45])));
            }
        }
        for (int i46 = 0; i46 < v.PlayerSetup.Count; ++i46)
        {
            for (int i47 = 0; i47 < v.PlayerSetup[i46].Count; ++i47)
            {
                fields.Add(
                    new(
                        prefix + "player_setup" + "[" + i46.ToString(
                            CultureInfo.InvariantCulture) + "]" + "[" + i47.ToString(
                                CultureInfo.InvariantCulture) + "]",
                        "u8",
                        new FieldValue.Unsigned(v.PlayerSetup[i46][i47])));
            }
        }
        fields.Add(new(prefix + "resume_civ", "u8", new FieldValue.Unsigned(v.ResumeCiv)));
        fields.Add(new(prefix + "retained_465", "u8", new FieldValue.Unsigned(v.Retained465)));
        fields.Add(new(prefix + "resume_event", "i16", new FieldValue.Signed(v.ResumeEvent)));
        fields.Add(new(prefix + "autoplay_active", "i16", new FieldValue.Signed(v.AutoplayActive)));
        fields.Add(new(prefix + "autoplay_turns", "i16", new FieldValue.Signed(v.AutoplayTurns)));
        fields.Add(new(prefix + "double_production", "i16", new FieldValue.Signed(v.DoubleProduction)));
        fields.Add(new(prefix + "double_movement", "i16", new FieldValue.Signed(v.DoubleMovement)));
        fields.Add(new(prefix + "claimed_humans", "i16", new FieldValue.Signed(v.ClaimedHumans)));
        for (int i48 = 0; i48 < v.Retained472.Count; ++i48)
        {
            fields.Add(
                new(
                    prefix + "retained_472" + "[" + i48.ToString(CultureInfo.InvariantCulture) + "]",
                    "u8",
                    new FieldValue.Unsigned(v.Retained472[i48])));
        }
    }

    public static List<Field> Of(EventRecord value)
    {
        var fields = new List<Field>();
        Append(value, "", fields);
        return fields;
    }

    private static void Append(EventRecord v, string prefix, List<Field> fields)
    {
        fields.Add(new(prefix + "triggers", "u32", new FieldValue.Unsigned(v.Triggers)));
        fields.Add(new(prefix + "actions", "u32", new FieldValue.Unsigned(v.Actions)));
        fields.Add(new(prefix + "unit_name", "u32", new FieldValue.Unsigned(v.UnitName)));
        fields.Add(new(prefix + "unit_type", "i32", new FieldValue.Signed(v.UnitType)));
        fields.Add(new(prefix + "city_name", "u32", new FieldValue.Unsigned(v.CityName)));
        fields.Add(new(prefix + "who_name", "u32", new FieldValue.Unsigned(v.WhoName)));
        fields.Add(new(prefix + "who_civ", "i32", new FieldValue.Signed(v.WhoCiv)));
        fields.Add(new(prefix + "talker_type", "i32", new FieldValue.Signed(v.TalkerType)));
        fields.Add(new(prefix + "whom_name", "u32", new FieldValue.Unsigned(v.WhomName)));
        fields.Add(new(prefix + "whom_civ", "i32", new FieldValue.Signed(v.WhomCiv)));
        fields.Add(new(prefix + "listener_type", "i32", new FieldValue.Signed(v.ListenerType)));
        fields.Add(new(prefix + "turn", "i32", new FieldValue.Signed(v.Turn)));
        fields.Add(new(prefix + "denominator", "i32", new FieldValue.Signed(v.Denominator)));
        fields.Add(new(prefix + "technology", "i32", new FieldValue.Signed(v.Technology)));
        for (int i49 = 0; i49 < v.Text.Count; ++i49)
        {
            fields.Add(
                new(
                    prefix + "text" + "[" + i49.ToString(CultureInfo.InvariantCulture) + "]",
                    "u32",
                    new FieldValue.Unsigned(v.Text[i49])));
        }
        fields.Add(new(prefix + "move_owner_name", "u32", new FieldValue.Unsigned(v.MoveOwnerName)));
        fields.Add(new(prefix + "move_owner_civ", "i32", new FieldValue.Signed(v.MoveOwnerCiv)));
        fields.Add(new(prefix + "move_unit_name", "u32", new FieldValue.Unsigned(v.MoveUnitName)));
        fields.Add(new(prefix + "move_unit_type", "i32", new FieldValue.Signed(v.MoveUnitType)));
        fields.Add(new(prefix + "move_count", "i32", new FieldValue.Signed(v.MoveCount)));
        for (int i50 = 0; i50 < v.MoveRectangle.Count; ++i50)
        {
            fields.Add(
                new(
                    prefix + "move_rectangle" + "[" + i50.ToString(CultureInfo.InvariantCulture) + "]",
                    "i32",
                    new FieldValue.Signed(v.MoveRectangle[i50])));
        }
        fields.Add(new(prefix + "move_x", "i32", new FieldValue.Signed(v.MoveX)));
        fields.Add(new(prefix + "move_y", "i32", new FieldValue.Signed(v.MoveY)));
        fields.Add(new(prefix + "aggression_whom_name", "u32", new FieldValue.Unsigned(v.AggressionWhomName)));
        fields.Add(new(prefix + "aggression_whom_civ", "i32", new FieldValue.Signed(v.AggressionWhomCiv)));
        fields.Add(new(prefix + "aggression_who_name", "u32", new FieldValue.Unsigned(v.AggressionWhoName)));
        fields.Add(new(prefix + "aggression_who_civ", "i32", new FieldValue.Signed(v.AggressionWhoCiv)));
        fields.Add(new(prefix + "create_owner_name", "u32", new FieldValue.Unsigned(v.CreateOwnerName)));
        fields.Add(new(prefix + "create_owner_civ", "i32", new FieldValue.Signed(v.CreateOwnerCiv)));
        fields.Add(new(prefix + "create_unit_name", "u32", new FieldValue.Unsigned(v.CreateUnitName)));
        fields.Add(new(prefix + "create_unit_type", "i32", new FieldValue.Signed(v.CreateUnitType)));
        for (int i51 = 0; i51 < v.CreateLocations.Count; ++i51)
        {
            fields.Add(
                new(
                    prefix + "create_locations" + "[" + i51.ToString(CultureInfo.InvariantCulture) + "]",
                    "i32",
                    new FieldValue.Signed(v.CreateLocations[i51])));
        }
        fields.Add(new(prefix + "create_location_count", "i32", new FieldValue.Signed(v.CreateLocationCount)));
        fields.Add(new(prefix + "create_veteran", "i32", new FieldValue.Signed(v.CreateVeteran)));
        fields.Add(new(prefix + "create_homecity", "u32", new FieldValue.Unsigned(v.CreateHomecity)));
        fields.Add(new(prefix + "auxiliary_civ_name", "u32", new FieldValue.Unsigned(v.AuxiliaryCivName)));
        fields.Add(new(prefix + "auxiliary_scalar", "i32", new FieldValue.Signed(v.AuxiliaryScalar)));
        fields.Add(new(prefix + "auxiliary_unit_name", "u32", new FieldValue.Unsigned(v.AuxiliaryUnitName)));
        for (int i52 = 0; i52 < v.Retained.Count; ++i52)
        {
            fields.Add(
                new(
                    prefix + "retained" + "[" + i52.ToString(CultureInfo.InvariantCulture) + "]",
                    "u8",
                    new FieldValue.Unsigned(v.Retained[i52])));
        }
        fields.Add(new(prefix + "money_receiver_name", "u32", new FieldValue.Unsigned(v.MoneyReceiverName)));
        fields.Add(new(prefix + "money_receiver_civ", "i32", new FieldValue.Signed(v.MoneyReceiverCiv)));
        fields.Add(new(prefix + "money_amount", "i32", new FieldValue.Signed(v.MoneyAmount)));
        fields.Add(new(prefix + "negotiation_flag", "i32", new FieldValue.Signed(v.NegotiationFlag)));
        fields.Add(new(prefix + "wave_file", "u32", new FieldValue.Unsigned(v.WaveFile)));
        fields.Add(new(prefix + "cd_track", "i32", new FieldValue.Signed(v.CdTrack)));
        fields.Add(new(prefix + "terrain_type", "i32", new FieldValue.Signed(v.TerrainType)));
        for (int i53 = 0; i53 < v.TerrainRectangle.Count; ++i53)
        {
            fields.Add(
                new(
                    prefix + "terrain_rectangle" + "[" + i53.ToString(CultureInfo.InvariantCulture) + "]",
                    "i32",
                    new FieldValue.Signed(v.TerrainRectangle[i53])));
        }
        fields.Add(new(prefix + "destroy_civ", "i32", new FieldValue.Signed(v.DestroyCiv)));
        fields.Add(new(prefix + "give_tech", "i32", new FieldValue.Signed(v.GiveTech)));
        fields.Add(new(prefix + "give_tech_civ", "i32", new FieldValue.Signed(v.GiveTechCiv)));
    }

    public static List<Field> Of(StoredUnit value)
    {
        var fields = new List<Field>();
        Append(value, "", fields);
        return fields;
    }

    private static void Append(StoredUnit v, string prefix, List<Field> fields)
    {
        fields.Add(new(prefix + "x", "i16", new FieldValue.Signed(v.X)));
        fields.Add(new(prefix + "y", "i16", new FieldValue.Signed(v.Y)));
        fields.Add(new(prefix + "flags", "u16", new FieldValue.Unsigned(v.Flags)));
        fields.Add(new(prefix + "type", "u8", new FieldValue.Unsigned(v.Type.Value)));
        fields.Add(new(prefix + "owner", "u8", new FieldValue.Unsigned(v.Owner.Value)));
        fields.Add(new(prefix + "moves_used", "u8", new FieldValue.Unsigned(v.MovesUsed)));
        fields.Add(new(prefix + "visibility", "u8", new FieldValue.Unsigned(v.Visibility)));
        fields.Add(new(prefix + "damage", "u8", new FieldValue.Unsigned(v.Damage)));
        fields.Add(new(prefix + "last_direction", "u8", new FieldValue.Unsigned(v.LastDirection)));
        fields.Add(new(prefix + "ai_task", "u8", new FieldValue.Unsigned(v.AiTask)));
        fields.Add(new(prefix + "counter", "u8", new FieldValue.Unsigned(v.Counter)));
        fields.Add(new(prefix + "auxiliary_timer", "u8", new FieldValue.Unsigned(v.AuxiliaryTimer)));
        fields.Add(new(prefix + "orders", "i8", new FieldValue.Signed((sbyte)v.Orders)));
        fields.Add(new(prefix + "home", "u16", v.Home is { } home ? new FieldValue.Unsigned(home.Value) : null));
        fields.Add(new(prefix + "retained_byte", "u8", new FieldValue.Unsigned(v.RetainedByte)));
        fields.Add(new(prefix + "destination_x", "i16", new FieldValue.Signed(v.DestinationX)));
        fields.Add(new(prefix + "destination_y", "i16", new FieldValue.Signed(v.DestinationY)));
        fields.Add(new(prefix + "previous", "i16", new FieldValue.Signed(v.Previous)));
        fields.Add(new(prefix + "next", "i16", new FieldValue.Signed(v.Next)));
        fields.Add(new(prefix + "token", "u32", new FieldValue.Unsigned(v.Token.Value)));
        fields.Add(new(prefix + "retained_word", "u16", new FieldValue.Unsigned(v.RetainedWord)));
    }

    public static List<Field> Of(StoredCity value)
    {
        var fields = new List<Field>();
        Append(value, "", fields);
        return fields;
    }

    private static void Append(StoredCity v, string prefix, List<Field> fields)
    {
        fields.Add(new(prefix + "x", "i16", new FieldValue.Signed(v.X)));
        fields.Add(new(prefix + "y", "i16", new FieldValue.Signed(v.Y)));
        fields.Add(new(prefix + "flags", "u32", new FieldValue.Unsigned(v.Flags)));
        fields.Add(new(prefix + "owner", "u8", new FieldValue.Unsigned(v.Owner.Value)));
        fields.Add(new(prefix + "size", "i8", new FieldValue.Signed(v.Size)));
        fields.Add(new(prefix + "founder", "u8", new FieldValue.Unsigned(v.Founder.Value)));
        fields.Add(new(prefix + "turns_captured", "u8", new FieldValue.Unsigned(v.TurnsCaptured)));
        fields.Add(new(prefix + "known_to", "u8", new FieldValue.Unsigned(v.KnownTo)));
        for (int i54 = 0; i54 < v.RememberedSize.Count; ++i54)
        {
            fields.Add(
                new(
                    prefix + "remembered_size" + "[" + i54.ToString(CultureInfo.InvariantCulture) + "]",
                    "u8",
                    new FieldValue.Unsigned(v.RememberedSize[i54])));
        }
        fields.Add(new(prefix + "retained_byte", "u8", new FieldValue.Unsigned(v.RetainedByte)));
        fields.Add(new(prefix + "specialist_kinds", "u32", new FieldValue.Unsigned(v.SpecialistKinds)));
        fields.Add(new(prefix + "food_box", "i16", new FieldValue.Signed(v.FoodBox)));
        fields.Add(new(prefix + "shield_box", "i16", new FieldValue.Signed(v.ShieldBox)));
        fields.Add(new(prefix + "base_trade", "i16", new FieldValue.Signed(v.BaseTrade)));
        fields.Add(new(prefix + "name", "classic-text", new FieldValue.Bytes(v.Name)));
        fields.Add(new(prefix + "worked_tiles", "u32", new FieldValue.Unsigned(v.WorkedTiles)));
        for (int i55 = 0; i55 < v.Improvements.Count; ++i55)
        {
            fields.Add(
                new(
                    prefix + "improvements" + "[" + i55.ToString(CultureInfo.InvariantCulture) + "]",
                    "u8",
                    new FieldValue.Unsigned(v.Improvements[i55])));
        }
        fields.Add(new(prefix + "production", "i8", new FieldValue.Signed(v.Production)));
        fields.Add(new(prefix + "route_count", "u8", new FieldValue.Unsigned(v.RouteCount)));
        for (int i56 = 0; i56 < v.Supplies.Count; ++i56)
        {
            fields.Add(
                new(
                    prefix + "supplies" + "[" + i56.ToString(CultureInfo.InvariantCulture) + "]",
                    "i8",
                    new FieldValue.Signed(v.Supplies[i56])));
        }
        for (int i57 = 0; i57 < v.Demands.Count; ++i57)
        {
            fields.Add(
                new(
                    prefix + "demands" + "[" + i57.ToString(CultureInfo.InvariantCulture) + "]",
                    "i8",
                    new FieldValue.Signed(v.Demands[i57])));
        }
        for (int i58 = 0; i58 < v.RouteGoods.Count; ++i58)
        {
            fields.Add(
                new(
                    prefix + "route_goods" + "[" + i58.ToString(CultureInfo.InvariantCulture) + "]",
                    "i8",
                    new FieldValue.Signed(v.RouteGoods[i58])));
        }
        for (int i59 = 0; i59 < v.RoutePartners.Count; ++i59)
        {
            fields.Add(
                new(
                    prefix + "route_partners" + "[" + i59.ToString(CultureInfo.InvariantCulture) + "]",
                    "i16",
                    new FieldValue.Signed(v.RoutePartners[i59])));
        }
        fields.Add(new(prefix + "science", "i16", new FieldValue.Signed(v.Science)));
        fields.Add(new(prefix + "tax", "i16", new FieldValue.Signed(v.Tax)));
        fields.Add(new(prefix + "trade", "i16", new FieldValue.Signed(v.Trade)));
        fields.Add(new(prefix + "total_food", "u8", new FieldValue.Unsigned(v.TotalFood)));
        fields.Add(new(prefix + "total_shields", "u8", new FieldValue.Unsigned(v.TotalShields)));
        fields.Add(new(prefix + "happy", "u8", new FieldValue.Unsigned(v.Happy)));
        fields.Add(new(prefix + "unhappy", "u8", new FieldValue.Unsigned(v.Unhappy)));
        fields.Add(new(prefix + "token", "u32", new FieldValue.Unsigned(v.Token.Value)));
    }

    public static List<Field> Of(MapHeader value)
    {
        var fields = new List<Field>();
        Append(value, "", fields);
        return fields;
    }

    private static void Append(MapHeader v, string prefix, List<Field> fields)
    {
        fields.Add(new(prefix + "width", "i16", new FieldValue.Signed(v.Width)));
        fields.Add(new(prefix + "height", "i16", new FieldValue.Signed(v.Height)));
        fields.Add(new(prefix + "area", "i16", new FieldValue.Signed(v.Area)));
        fields.Add(new(prefix + "topology", "u16", new FieldValue.Unsigned(v.Topology)));
        fields.Add(new(prefix + "seed", "u16", new FieldValue.Unsigned(v.Seed)));
        fields.Add(new(prefix + "coarse_width", "i16", new FieldValue.Signed(v.CoarseWidth)));
        fields.Add(new(prefix + "coarse_height", "i16", new FieldValue.Signed(v.CoarseHeight)));
    }

    public static List<Field> Of(CivilizationGoal value)
    {
        var fields = new List<Field>();
        Append(value, "", fields);
        return fields;
    }

    private static void Append(CivilizationGoal v, string prefix, List<Field> fields)
    {
        fields.Add(new(prefix + "x", "i16", new FieldValue.Signed(v.Position.X)));
        fields.Add(new(prefix + "y", "i16", new FieldValue.Signed(v.Position.Y)));
        fields.Add(new(prefix + "role", "i8", new FieldValue.Signed(v.Role)));
        fields.Add(new(prefix + "priority", "i8", new FieldValue.Signed(v.Priority)));
    }

    public static List<Field> Of(StoredTile v) =>
    [
        new("terrain", "u8", new FieldValue.Unsigned((byte)v.Terrain)),
        new("terrain_flags", "u8", new FieldValue.Unsigned(v.TerrainFlags)),
        new("features", "u8", new FieldValue.Unsigned(v.Features)),
        new("claimant", "u8", new FieldValue.Unsigned(v.Claimant)),
        new("radius_other", "u8", new FieldValue.Unsigned(v.RadiusOther)),
        new("region", "u8", new FieldValue.Unsigned(v.Region)),
        new("revelation", "u8", new FieldValue.Unsigned(v.Revelation)),
        new("fertility", "u8", new FieldValue.Unsigned(v.Fertility)),
        new("owner", "u8", v.Owner is { } owner ? new FieldValue.Unsigned(owner) : null)
    ];

    public static List<Field> Of(StoredRegionSummary v) =>
    [
        new("land", "i16", new FieldValue.Signed(v.Land)),
        new("fertile", "i16", new FieldValue.Signed(v.Fertile)),
        new("water", "i16", new FieldValue.Signed(v.Water)),
        new("retained[0]", "u8", new FieldValue.Unsigned(v.Retained[0])),
        new("retained[1]", "u8", new FieldValue.Unsigned(v.Retained[1])),
        new("neighboring_water", "u64", new FieldValue.Unsigned(v.NeighboringWater))
    ];
}
