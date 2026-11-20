using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.IO;

namespace OpenCiv2.Formats;

public static partial class ClassicRecords
{
    public static StoredGlobals DecodeStoredGlobals(ReadOnlySpan<byte> bytes)
    {
        RequireSize(bytes, 330);
        var r = new ByteReader(bytes);
        return ReadStoredGlobals(ref r);
    }

    internal static StoredGlobals ReadStoredGlobals(ref ByteReader r)
    {
        var rules = r.U16();
        var game_options = r.U32();
        var transient_flags = r.U16();
        var game_flags = r.U16();
        var message_options = r.U16();
        var shown_event_flags = r.U16();
        var retained_0e = r.U16();
        var turn = r.I16();
        var year = r.I16();
        var first_launch_turn = r.I16();
        var active_unit = r.I16();
        var moving_unit = r.I16();
        var multiplayer = r.U8();
        var player_civ = r.I8();
        var saved_current_civ = r.I8();
        var turn_civ = r.I8();
        var view_civ = r.I8();
        var reveal_map = r.U8();
        var difficulty = r.U8();
        var barbarian_activity = r.U8();
        var civs_alive = r.U8();
        var human_players = r.U8();
        var civs_at_start = r.U8();
        var num_enemies = r.U8();
        var warming_counter = r.I8();
        var global_warmings = r.I8();
        var warming_pollution = r.I16();
        var polluted_tiles = r.I16();
        var peace_turns = r.I16();
        var unit_end = r.I16();
        var city_end = r.I16();
        var num_advances = r.I16();
        var retained_34 = r.I16();
        var items1 = new byte[100];
        for (int i1 = 0; i1 < 100; ++i1)
        {
            items1[i1] = r.U8();
        }
        var first_discoverer = new OwnedList<byte>(items1);
        var items2 = new byte[100];
        for (int i2 = 0; i2 < 100; ++i2)
        {
            items2[i2] = r.U8();
        }
        var advance_known_by = new OwnedList<byte>(items2);
        var items3 = new short[28];
        for (int i3 = 0; i3 < 28; ++i3)
        {
            items3[i3] = r.I16();
        }
        var wonder_city = new OwnedList<short>(items3);
        var next_history_turn = r.I16();
        var best_human_rank = r.U8();
        var worst_human_rank = r.U8();
        var items4 = new byte[8];
        for (int i4 = 0; i4 < 8; ++i4)
        {
            items4[i4] = r.U8();
        }
        var power_rank = new OwnedList<byte>(items4);
        var items5 = new byte[8];
        for (int i5 = 0; i5 < 8; ++i5)
        {
            items5[i5] = r.U8();
        }
        var civ_by_rank = new OwnedList<byte>(items5);
        return new()
        {
            Rules = rules,
            GameOptions = game_options,
            TransientFlags = transient_flags,
            GameFlags = game_flags,
            MessageOptions = message_options,
            ShownEventFlags = shown_event_flags,
            Retained0e = retained_0e,
            Turn = turn,
            Year = year,
            FirstLaunchTurn = first_launch_turn,
            ActiveUnit = active_unit,
            MovingUnit = moving_unit,
            Multiplayer = multiplayer,
            PlayerCiv = player_civ,
            SavedCurrentCiv = saved_current_civ,
            TurnCiv = turn_civ,
            ViewCiv = view_civ,
            RevealMap = reveal_map,
            Difficulty = difficulty,
            BarbarianActivity = barbarian_activity,
            CivsAlive = civs_alive,
            HumanPlayers = human_players,
            CivsAtStart = civs_at_start,
            NumEnemies = num_enemies,
            WarmingCounter = warming_counter,
            GlobalWarmings = global_warmings,
            WarmingPollution = warming_pollution,
            PollutedTiles = polluted_tiles,
            PeaceTurns = peace_turns,
            UnitEnd = unit_end,
            CityEnd = city_end,
            NumAdvances = num_advances,
            Retained34 = retained_34,
            FirstDiscoverer = first_discoverer,
            AdvanceKnownBy = advance_known_by,
            WonderCity = wonder_city,
            NextHistoryTurn = next_history_turn,
            BestHumanRank = best_human_rank,
            WorstHumanRank = worst_human_rank,
            PowerRank = power_rank,
            CivByRank = civ_by_rank
        };
    }

    public static byte[] Encode(StoredGlobals value)
    {
        var w = new ByteWriter();
        WriteStoredGlobals(w, value);
        return w.ToArray();
    }

    internal static void WriteStoredGlobals(ByteWriter w, StoredGlobals v)
    {
        w.U16(v.Rules);
        w.U32(v.GameOptions);
        w.U16(v.TransientFlags);
        w.U16(v.GameFlags);
        w.U16(v.MessageOptions);
        w.U16(v.ShownEventFlags);
        w.U16(v.Retained0e);
        w.I16(v.Turn);
        w.I16(v.Year);
        w.I16(v.FirstLaunchTurn);
        w.I16(v.ActiveUnit);
        w.I16(v.MovingUnit);
        w.U8(v.Multiplayer);
        w.I8(v.PlayerCiv);
        w.I8(v.SavedCurrentCiv);
        w.I8(v.TurnCiv);
        w.I8(v.ViewCiv);
        w.U8(v.RevealMap);
        w.U8(v.Difficulty);
        w.U8(v.BarbarianActivity);
        w.U8(v.CivsAlive);
        w.U8(v.HumanPlayers);
        w.U8(v.CivsAtStart);
        w.U8(v.NumEnemies);
        w.I8(v.WarmingCounter);
        w.I8(v.GlobalWarmings);
        w.I16(v.WarmingPollution);
        w.I16(v.PollutedTiles);
        w.I16(v.PeaceTurns);
        w.I16(v.UnitEnd);
        w.I16(v.CityEnd);
        w.I16(v.NumAdvances);
        w.I16(v.Retained34);
        RequireCount(v.FirstDiscoverer, 100);
        foreach (var item6 in v.FirstDiscoverer)
        {
            w.U8(item6);
        }
        RequireCount(v.AdvanceKnownBy, 100);
        foreach (var item7 in v.AdvanceKnownBy)
        {
            w.U8(item7);
        }
        RequireCount(v.WonderCity, 28);
        foreach (var item8 in v.WonderCity)
        {
            w.I16(item8);
        }
        w.I16(v.NextHistoryTurn);
        w.U8(v.BestHumanRank);
        w.U8(v.WorstHumanRank);
        RequireCount(v.PowerRank, 8);
        foreach (var item9 in v.PowerRank)
        {
            w.U8(item9);
        }
        RequireCount(v.CivByRank, 8);
        foreach (var item10 in v.CivByRank)
        {
            w.U8(item10);
        }
    }

    public static StoredCivilization DecodeStoredCivilization(ReadOnlySpan<byte> bytes)
    {
        RequireSize(bytes, 1428);
        var r = new ByteReader(bytes);
        return ReadStoredCivilization(ref r);
    }

    internal static StoredCivilization ReadStoredCivilization(ref ByteReader r)
    {
        var flags = r.U16();
        var gold = r.I32();
        var tribe = r.I16();
        var research_progress = r.I16();
        var researching = r.I16();
        var capital_x = r.I16();
        var last_city_turn = r.I16();
        var num_advances = r.U8();
        var future_techs = r.U8();
        var research_cost_advances = r.U8();
        var science_rate = r.U8();
        var tax_rate = r.U8();
        var government = r.U8();
        var diplo_random = r.U8();
        var items11 = new byte[4];
        for (int i11 = 0; i11 < 4; ++i11)
        {
            items11[i11] = r.U8();
        }
        var wonders_by_group = new OwnedList<byte>(items11);
        var retained_1b = r.U8();
        var subversion_count = r.I16();
        var reputation = r.U8();
        var diplo_request_count = r.I8();
        var items12 = new uint[8];
        for (int i12 = 0; i12 < 8; ++i12)
        {
            items12[i12] = r.U32();
        }
        var treaties = new OwnedList<uint>(items12);
        var items13 = new byte[8];
        for (int i13 = 0; i13 < 8; ++i13)
        {
            items13[i13] = r.U8();
        }
        var attitude = new OwnedList<byte>(items13);
        var items14 = new sbyte[8];
        for (int i14 = 0; i14 < 8; ++i14)
        {
            items14[i14] = r.I8();
        }
        var reputation_credit = new OwnedList<sbyte>(items14);
        var items15 = new byte[8];
        for (int i15 = 0; i15 < 8; ++i15)
        {
            items15[i15] = r.U8();
        }
        var aggression_counter = new OwnedList<byte>(items15);
        var items16 = new byte[13];
        for (int i16 = 0; i16 < 13; ++i16)
        {
            items16[i16] = r.U8();
        }
        var advances = new OwnedList<byte>(items16);
        var retained_65 = r.U8();
        var military_count = r.I16();
        var city_count = r.I16();
        var ship_count = r.I16();
        var total_city_size = r.I16();
        var military_strength = r.U16();
        var total_attack = r.I16();
        var population_milestone = r.I16();
        var items17 = new byte[100];
        for (int i17 = 0; i17 < 100; ++i17)
        {
            items17[i17] = r.U8();
        }
        var advance_source = new OwnedList<byte>(items17);
        var items18 = new byte[62];
        for (int i18 = 0; i18 < 62; ++i18)
        {
            items18[i18] = r.U8();
        }
        var units_active = new OwnedList<byte>(items18);
        var items19 = new byte[62];
        for (int i19 = 0; i19 < 62; ++i19)
        {
            items19[i19] = r.U8();
        }
        var units_lost = new OwnedList<byte>(items19);
        var items20 = new byte[62];
        for (int i20 = 0; i20 < 62; ++i20)
        {
            items20[i20] = r.U8();
        }
        var units_in_production = new OwnedList<byte>(items20);
        var items21 = new ushort[64];
        for (int i21 = 0; i21 < 64; ++i21)
        {
            items21[i21] = r.U16();
        }
        var continent_military = new OwnedList<ushort>(items21);
        var items22 = new ushort[64];
        for (int i22 = 0; i22 < 64; ++i22)
        {
            items22[i22] = r.U16();
        }
        var continent_attack = new OwnedList<ushort>(items22);
        var items23 = new byte[64];
        for (int i23 = 0; i23 < 64; ++i23)
        {
            items23[i23] = r.U8();
        }
        var continent_cities = new OwnedList<byte>(items23);
        var items24 = new byte[64];
        for (int i24 = 0; i24 < 64; ++i24)
        {
            items24[i24] = r.U8();
        }
        var continent_city_size = new OwnedList<byte>(items24);
        var items25 = new byte[64];
        for (int i25 = 0; i25 < 64; ++i25)
        {
            items25[i25] = r.U8();
        }
        var continent_trade_units = new OwnedList<byte>(items25);
        var items26 = new byte[64];
        for (int i26 = 0; i26 < 64; ++i26)
        {
            items26[i26] = r.U8();
        }
        var continent_flags = new OwnedList<byte>(items26);
        var items27 = new byte[64];
        for (int i27 = 0; i27 < 64; ++i27)
        {
            items27[i27] = r.U8();
        }
        var continent_policy = new OwnedList<byte>(items27);
        var best_shield_surplus = r.I16();
        var items28 = new short[7];
        for (int i28 = 0; i28 < 7; ++i28)
        {
            items28[i28] = r.I16();
        }
        var government_score = new OwnedList<short>(items28);
        var items29 = new short[8];
        for (int i29 = 0; i29 < 8; ++i29)
        {
            items29[i29] = r.I16();
        }
        var last_contact_turn = new OwnedList<short>(items29);
        var leader_portrait = r.U8();
        var items30 = new byte[10];
        for (int i30 = 0; i30 < 10; ++i30)
        {
            items30[i30] = r.U8();
        }
        var throne_room = new OwnedList<byte>(items30);
        var retained_3fd = r.U8();
        var throne_room_upgrades = r.I16();
        var spaceship_flags = r.U8();
        var retained_401 = r.U8();
        var spaceship_arrival_year = r.I16();
        var spaceship_launch_year = r.I16();
        var spaceship_population = r.I16();
        var items31 = new short[6];
        for (int i31 = 0; i31 < 6; ++i31)
        {
            items31[i31] = r.I16();
        }
        var spaceship_parts = new OwnedList<short>(items31);
        var items32 = new CivilizationGoal[48];
        for (int i32 = 0; i32 < 48; ++i32)
        {
            items32[i32] = ReadCivilizationGoal(ref r);
        }
        var goals = new OwnedList<CivilizationGoal>(items32);
        var items33 = new CivilizationGoal[16];
        for (int i33 = 0; i33 < 16; ++i33)
        {
            items33[i33] = ReadCivilizationGoal(ref r);
        }
        var standing_goals = new OwnedList<CivilizationGoal>(items33);
        return new()
        {
            Flags = flags,
            Gold = gold,
            Tribe = tribe,
            ResearchProgress = research_progress,
            Researching = researching,
            CapitalX = capital_x,
            LastCityTurn = last_city_turn,
            NumAdvances = num_advances,
            FutureTechs = future_techs,
            ResearchCostAdvances = research_cost_advances,
            ScienceRate = science_rate,
            TaxRate = tax_rate,
            Government = government,
            DiploRandom = diplo_random,
            WondersByGroup = wonders_by_group,
            Retained1b = retained_1b,
            SubversionCount = subversion_count,
            Reputation = reputation,
            DiploRequestCount = diplo_request_count,
            Treaties = treaties,
            Attitude = attitude,
            ReputationCredit = reputation_credit,
            AggressionCounter = aggression_counter,
            Advances = advances,
            Retained65 = retained_65,
            MilitaryCount = military_count,
            CityCount = city_count,
            ShipCount = ship_count,
            TotalCitySize = total_city_size,
            MilitaryStrength = military_strength,
            TotalAttack = total_attack,
            PopulationMilestone = population_milestone,
            AdvanceSource = advance_source,
            UnitsActive = units_active,
            UnitsLost = units_lost,
            UnitsInProduction = units_in_production,
            ContinentMilitary = continent_military,
            ContinentAttack = continent_attack,
            ContinentCities = continent_cities,
            ContinentCitySize = continent_city_size,
            ContinentTradeUnits = continent_trade_units,
            ContinentFlags = continent_flags,
            ContinentPolicy = continent_policy,
            BestShieldSurplus = best_shield_surplus,
            GovernmentScore = government_score,
            LastContactTurn = last_contact_turn,
            LeaderPortrait = leader_portrait,
            ThroneRoom = throne_room,
            Retained3fd = retained_3fd,
            ThroneRoomUpgrades = throne_room_upgrades,
            SpaceshipFlags = spaceship_flags,
            Retained401 = retained_401,
            SpaceshipArrivalYear = spaceship_arrival_year,
            SpaceshipLaunchYear = spaceship_launch_year,
            SpaceshipPopulation = spaceship_population,
            SpaceshipParts = spaceship_parts,
            Goals = goals,
            StandingGoals = standing_goals
        };
    }

    public static byte[] Encode(StoredCivilization value)
    {
        var w = new ByteWriter();
        WriteStoredCivilization(w, value);
        return w.ToArray();
    }

    internal static void WriteStoredCivilization(ByteWriter w, StoredCivilization v)
    {
        w.U16(v.Flags);
        w.I32(v.Gold);
        w.I16(v.Tribe);
        w.I16(v.ResearchProgress);
        w.I16(v.Researching);
        w.I16(v.CapitalX);
        w.I16(v.LastCityTurn);
        w.U8(v.NumAdvances);
        w.U8(v.FutureTechs);
        w.U8(v.ResearchCostAdvances);
        w.U8(v.ScienceRate);
        w.U8(v.TaxRate);
        w.U8(v.Government);
        w.U8(v.DiploRandom);
        RequireCount(v.WondersByGroup, 4);
        foreach (var item34 in v.WondersByGroup)
        {
            w.U8(item34);
        }
        w.U8(v.Retained1b);
        w.I16(v.SubversionCount);
        w.U8(v.Reputation);
        w.I8(v.DiploRequestCount);
        RequireCount(v.Treaties, 8);
        foreach (var item35 in v.Treaties)
        {
            w.U32(item35);
        }
        RequireCount(v.Attitude, 8);
        foreach (var item36 in v.Attitude)
        {
            w.U8(item36);
        }
        RequireCount(v.ReputationCredit, 8);
        foreach (var item37 in v.ReputationCredit)
        {
            w.I8(item37);
        }
        RequireCount(v.AggressionCounter, 8);
        foreach (var item38 in v.AggressionCounter)
        {
            w.U8(item38);
        }
        RequireCount(v.Advances, 13);
        foreach (var item39 in v.Advances)
        {
            w.U8(item39);
        }
        w.U8(v.Retained65);
        w.I16(v.MilitaryCount);
        w.I16(v.CityCount);
        w.I16(v.ShipCount);
        w.I16(v.TotalCitySize);
        w.U16(v.MilitaryStrength);
        w.I16(v.TotalAttack);
        w.I16(v.PopulationMilestone);
        RequireCount(v.AdvanceSource, 100);
        foreach (var item40 in v.AdvanceSource)
        {
            w.U8(item40);
        }
        RequireCount(v.UnitsActive, 62);
        foreach (var item41 in v.UnitsActive)
        {
            w.U8(item41);
        }
        RequireCount(v.UnitsLost, 62);
        foreach (var item42 in v.UnitsLost)
        {
            w.U8(item42);
        }
        RequireCount(v.UnitsInProduction, 62);
        foreach (var item43 in v.UnitsInProduction)
        {
            w.U8(item43);
        }
        RequireCount(v.ContinentMilitary, 64);
        foreach (var item44 in v.ContinentMilitary)
        {
            w.U16(item44);
        }
        RequireCount(v.ContinentAttack, 64);
        foreach (var item45 in v.ContinentAttack)
        {
            w.U16(item45);
        }
        RequireCount(v.ContinentCities, 64);
        foreach (var item46 in v.ContinentCities)
        {
            w.U8(item46);
        }
        RequireCount(v.ContinentCitySize, 64);
        foreach (var item47 in v.ContinentCitySize)
        {
            w.U8(item47);
        }
        RequireCount(v.ContinentTradeUnits, 64);
        foreach (var item48 in v.ContinentTradeUnits)
        {
            w.U8(item48);
        }
        RequireCount(v.ContinentFlags, 64);
        foreach (var item49 in v.ContinentFlags)
        {
            w.U8(item49);
        }
        RequireCount(v.ContinentPolicy, 64);
        foreach (var item50 in v.ContinentPolicy)
        {
            w.U8(item50);
        }
        w.I16(v.BestShieldSurplus);
        RequireCount(v.GovernmentScore, 7);
        foreach (var item51 in v.GovernmentScore)
        {
            w.I16(item51);
        }
        RequireCount(v.LastContactTurn, 8);
        foreach (var item52 in v.LastContactTurn)
        {
            w.I16(item52);
        }
        w.U8(v.LeaderPortrait);
        RequireCount(v.ThroneRoom, 10);
        foreach (var item53 in v.ThroneRoom)
        {
            w.U8(item53);
        }
        w.U8(v.Retained3fd);
        w.I16(v.ThroneRoomUpgrades);
        w.U8(v.SpaceshipFlags);
        w.U8(v.Retained401);
        w.I16(v.SpaceshipArrivalYear);
        w.I16(v.SpaceshipLaunchYear);
        w.I16(v.SpaceshipPopulation);
        RequireCount(v.SpaceshipParts, 6);
        foreach (var item54 in v.SpaceshipParts)
        {
            w.I16(item54);
        }
        RequireCount(v.Goals, 48);
        foreach (var item55 in v.Goals)
        {
            WriteCivilizationGoal(w, item55);
        }
        RequireCount(v.StandingGoals, 16);
        foreach (var item56 in v.StandingGoals)
        {
            WriteCivilizationGoal(w, item56);
        }
    }

    public static StoredCivilizationNames DecodeStoredCivilizationNames(ReadOnlySpan<byte> bytes)
    {
        RequireSize(bytes, 242);
        var r = new ByteReader(bytes);
        return ReadStoredCivilizationNames(ref r);
    }

    internal static StoredCivilizationNames ReadStoredCivilizationNames(ref ByteReader r)
    {
        var city_style = r.I16();
        var leader = new OwnedList<byte>(r.Take(24).ToArray());
        var tribe = new OwnedList<byte>(r.Take(24).ToArray());
        var adjective = new OwnedList<byte>(r.Take(24).ToArray());
        var items57 = new OwnedList<byte>[7];
        for (int i57 = 0; i57 < 7; ++i57)
        {
            items57[i57] = new OwnedList<byte>(r.Take(24).ToArray());
        }
        var titles = new OwnedList<OwnedList<byte>>(items57);
        return new() { CityStyle = city_style, Leader = leader, Tribe = tribe, Adjective = adjective, Titles = titles };
    }

    public static byte[] Encode(StoredCivilizationNames value)
    {
        var w = new ByteWriter();
        WriteStoredCivilizationNames(w, value);
        return w.ToArray();
    }

    internal static void WriteStoredCivilizationNames(ByteWriter w, StoredCivilizationNames v)
    {
        w.I16(v.CityStyle);
        RequireCount(v.Leader, 24);
        foreach (byte b in v.Leader)
        {
            w.U8(b);
        }

        RequireCount(v.Tribe, 24);
        foreach (byte b in v.Tribe)
        {
            w.U8(b);
        }

        RequireCount(v.Adjective, 24);
        foreach (byte b in v.Adjective)
        {
            w.U8(b);
        }

        RequireCount(v.Titles, 7);
        foreach (var item58 in v.Titles)
        {
            RequireCount(item58, 24);
            foreach (byte b in item58)
            {
                w.U8(b);
            }
        }
    }

    public static StoredLeader DecodeStoredLeader(ReadOnlySpan<byte> bytes)
    {
        RequireSize(bytes, 48);
        var r = new ByteReader(bytes);
        return ReadStoredLeader(ref r);
    }

    internal static StoredLeader ReadStoredLeader(ref ByteReader r)
    {
        var attack = r.I8();
        var expand = r.I8();
        var civilize = r.I8();
        var times_used = r.U8();
        var female = r.U8();
        var city_name_index = r.U8();
        var color = r.I16();
        var city_style = r.I16();
        var leader_name = r.I16();
        var plural = r.I16();
        var adjective = r.I16();
        var items59 = new short[2];
        for (int i59 = 0; i59 < 2; ++i59)
        {
            items59[i59] = r.I16();
        }
        var names = new OwnedList<short>(items59);
        var items60 = new OwnedList<short>[7];
        for (int i60 = 0; i60 < 7; ++i60)
        {
            var items61 = new short[2];
            for (int i61 = 0; i61 < 2; ++i61)
            {
                items61[i61] = r.I16();
            }
            items60[i60] = new OwnedList<short>(items61);
        }
        var titles = new OwnedList<OwnedList<short>>(items60);
        return new()
        {
            Attack = attack,
            Expand = expand,
            Civilize = civilize,
            TimesUsed = times_used,
            Female = female,
            CityNameIndex = city_name_index,
            Color = color,
            CityStyle = city_style,
            LeaderName = leader_name,
            Plural = plural,
            Adjective = adjective,
            Names = names,
            Titles = titles
        };
    }

    public static byte[] Encode(StoredLeader value)
    {
        var w = new ByteWriter();
        WriteStoredLeader(w, value);
        return w.ToArray();
    }

    internal static void WriteStoredLeader(ByteWriter w, StoredLeader v)
    {
        w.I8(v.Attack);
        w.I8(v.Expand);
        w.I8(v.Civilize);
        w.U8(v.TimesUsed);
        w.U8(v.Female);
        w.U8(v.CityNameIndex);
        w.I16(v.Color);
        w.I16(v.CityStyle);
        w.I16(v.LeaderName);
        w.I16(v.Plural);
        w.I16(v.Adjective);
        RequireCount(v.Names, 2);
        foreach (var item62 in v.Names)
        {
            w.I16(item62);
        }
        RequireCount(v.Titles, 7);
        foreach (var item63 in v.Titles)
        {
            RequireCount(item63, 2);
            foreach (var item64 in item63)
            {
                w.I16(item64);
            }
        }
    }

    public static SavedRectangle DecodeSavedRectangle(ReadOnlySpan<byte> bytes)
    {
        RequireSize(bytes, 8);
        var r = new ByteReader(bytes);
        return ReadSavedRectangle(ref r);
    }

    internal static SavedRectangle ReadSavedRectangle(ref ByteReader r)
    {
        var left = r.I16();
        var top = r.I16();
        var right = r.I16();
        var bottom = r.I16();
        return new() { Left = left, Top = top, Right = right, Bottom = bottom };
    }

    public static byte[] Encode(SavedRectangle value)
    {
        var w = new ByteWriter();
        WriteSavedRectangle(w, value);
        return w.ToArray();
    }

    internal static void WriteSavedRectangle(ByteWriter w, SavedRectangle v)
    {
        w.I16(v.Left);
        w.I16(v.Top);
        w.I16(v.Right);
        w.I16(v.Bottom);
    }

    public static SavedLayout DecodeSavedLayout(ReadOnlySpan<byte> bytes)
    {
        RequireSize(bytes, 106);
        var r = new ByteReader(bytes);
        return ReadSavedLayout(ref r);
    }

    internal static SavedLayout ReadSavedLayout(ref ByteReader r)
    {
        var valid = r.I16();
        var items65 = new SavedRectangle[13];
        for (int i65 = 0; i65 < 13; ++i65)
        {
            items65[i65] = ReadSavedRectangle(ref r);
        }
        var rectangles = new OwnedList<SavedRectangle>(items65);
        return new() { Valid = valid, Rectangles = rectangles };
    }

    public static byte[] Encode(SavedLayout value)
    {
        var w = new ByteWriter();
        WriteSavedLayout(w, value);
        return w.ToArray();
    }

    internal static void WriteSavedLayout(ByteWriter w, SavedLayout v)
    {
        w.I16(v.Valid);
        RequireCount(v.Rectangles, 13);
        foreach (var item66 in v.Rectangles)
        {
            WriteSavedRectangle(w, item66);
        }
    }

    public static SavedMapView DecodeSavedMapView(ReadOnlySpan<byte> bytes)
    {
        RequireSize(bytes, 10);
        var r = new ByteReader(bytes);
        return ReadSavedMapView(ref r);
    }

    internal static SavedMapView ReadSavedMapView(ref ByteReader r)
    {
        var open = r.I16();
        var track_moves = r.I16();
        var x = r.I16();
        var y = r.I16();
        var zoom = r.I16();
        return new() { Open = open, TrackMoves = track_moves, X = x, Y = y, Zoom = zoom };
    }

    public static byte[] Encode(SavedMapView value)
    {
        var w = new ByteWriter();
        WriteSavedMapView(w, value);
        return w.ToArray();
    }

    internal static void WriteSavedMapView(ByteWriter w, SavedMapView v)
    {
        w.I16(v.Open);
        w.I16(v.TrackMoves);
        w.I16(v.X);
        w.I16(v.Y);
        w.I16(v.Zoom);
    }

    public static ScenarioParameters DecodeScenarioParameters(ReadOnlySpan<byte> bytes)
    {
        RequireSize(bytes, 100);
        var r = new ByteReader(bytes);
        return ReadScenarioParameters(ref r);
    }

    internal static ScenarioParameters ReadScenarioParameters(ref ByteReader r)
    {
        var flags = r.U16();
        var name = new OwnedList<byte>(r.Take(80).ToArray());
        var technology_paradigm = r.I16();
        var year_increment = r.I16();
        var start_year = r.I16();
        var maximum_turns = r.I16();
        var objective_civ = r.I16();
        var items67 = new short[4];
        for (int i67 = 0; i67 < 4; ++i67)
        {
            items67[i67] = r.I16();
        }
        var victory_thresholds = new OwnedList<short>(items67);
        return new()
        {
            Flags = flags,
            Name = name,
            TechnologyParadigm = technology_paradigm,
            YearIncrement = year_increment,
            StartYear = start_year,
            MaximumTurns = maximum_turns,
            ObjectiveCiv = objective_civ,
            VictoryThresholds = victory_thresholds
        };
    }

    public static byte[] Encode(ScenarioParameters value)
    {
        var w = new ByteWriter();
        WriteScenarioParameters(w, value);
        return w.ToArray();
    }

    internal static void WriteScenarioParameters(ByteWriter w, ScenarioParameters v)
    {
        w.U16(v.Flags);
        RequireCount(v.Name, 80);
        foreach (byte b in v.Name)
        {
            w.U8(b);
        }

        w.I16(v.TechnologyParadigm);
        w.I16(v.YearIncrement);
        w.I16(v.StartYear);
        w.I16(v.MaximumTurns);
        w.I16(v.ObjectiveCiv);
        RequireCount(v.VictoryThresholds, 4);
        foreach (var item68 in v.VictoryThresholds)
        {
            w.I16(item68);
        }
    }

    public static EliminationHistory DecodeEliminationHistory(ReadOnlySpan<byte> bytes)
    {
        RequireSize(bytes, 338);
        var r = new ByteReader(bytes);
        return ReadEliminationHistory(ref r);
    }

    internal static EliminationHistory ReadEliminationHistory(ref ByteReader r)
    {
        var count = r.I16();
        var items69 = new short[12];
        for (int i69 = 0; i69 < 12; ++i69)
        {
            items69[i69] = r.I16();
        }
        var turns = new OwnedList<short>(items69);
        var items70 = new byte[12];
        for (int i70 = 0; i70 < 12; ++i70)
        {
            items70[i70] = r.U8();
        }
        var killers = new OwnedList<byte>(items70);
        var items71 = new byte[12];
        for (int i71 = 0; i71 < 12; ++i71)
        {
            items71[i71] = r.U8();
        }
        var portraits = new OwnedList<byte>(items71);
        var items72 = new OwnedList<byte>[12];
        for (int i72 = 0; i72 < 12; ++i72)
        {
            items72[i72] = new OwnedList<byte>(r.Take(24).ToArray());
        }
        var names = new OwnedList<OwnedList<byte>>(items72);
        return new() { Count = count, Turns = turns, Killers = killers, Portraits = portraits, Names = names };
    }

    public static byte[] Encode(EliminationHistory value)
    {
        var w = new ByteWriter();
        WriteEliminationHistory(w, value);
        return w.ToArray();
    }

    internal static void WriteEliminationHistory(ByteWriter w, EliminationHistory v)
    {
        w.I16(v.Count);
        RequireCount(v.Turns, 12);
        foreach (var item73 in v.Turns)
        {
            w.I16(item73);
        }
        RequireCount(v.Killers, 12);
        foreach (var item74 in v.Killers)
        {
            w.U8(item74);
        }
        RequireCount(v.Portraits, 12);
        foreach (var item75 in v.Portraits)
        {
            w.U8(item75);
        }
        RequireCount(v.Names, 12);
        foreach (var item76 in v.Names)
        {
            RequireCount(item76, 24);
            foreach (byte b in item76)
            {
                w.U8(b);
            }
        }
    }

    public static MultiplayerRecord DecodeMultiplayerRecord(ReadOnlySpan<byte> bytes)
    {
        RequireSize(bytes, 1172);
        var r = new ByteReader(bytes);
        return ReadMultiplayerRecord(ref r);
    }

    internal static MultiplayerRecord ReadMultiplayerRecord(ref ByteReader r)
    {
        var items77 = new short[8];
        for (int i77 = 0; i77 < 8; ++i77)
        {
            items77[i77] = r.I16();
        }
        var center_x = new OwnedList<short>(items77);
        var items78 = new short[8];
        for (int i78 = 0; i78 < 8; ++i78)
        {
            items78[i78] = r.I16();
        }
        var center_y = new OwnedList<short>(items78);
        var items79 = new short[8];
        for (int i79 = 0; i79 < 8; ++i79)
        {
            items79[i79] = r.I16();
        }
        var introduced = new OwnedList<short>(items79);
        var turn_time_limit = r.I32();
        var items80 = new byte[256];
        for (int i80 = 0; i80 < 256; ++i80)
        {
            items80[i80] = r.U8();
        }
        var encoded_passwords = new OwnedList<byte>(items80);
        var open_game = r.I16();
        var kill_on_retirement = r.I16();
        var show_human_starts = r.I16();
        var chat_from_start = r.I16();
        var simultaneous = r.I16();
        var cheat_turn_time = r.I32();
        var items81 = new byte[290];
        for (int i81 = 0; i81 < 290; ++i81)
        {
            items81[i81] = r.U8();
        }
        var retained_142 = new OwnedList<byte>(items81);
        var items82 = new OwnedList<byte>[8];
        for (int i82 = 0; i82 < 8; ++i82)
        {
            var items83 = new byte[32];
            for (int i83 = 0; i83 < 32; ++i83)
            {
                items83[i83] = r.U8();
            }
            items82[i82] = new OwnedList<byte>(items83);
        }
        var emails = new OwnedList<OwnedList<byte>>(items82);
        var items84 = new OwnedList<byte>[8];
        for (int i84 = 0; i84 < 8; ++i84)
        {
            var items85 = new byte[32];
            for (int i85 = 0; i85 < 32; ++i85)
            {
                items85[i85] = r.U8();
            }
            items84[i84] = new OwnedList<byte>(items85);
        }
        var player_setup = new OwnedList<OwnedList<byte>>(items84);
        var resume_civ = r.U8();
        var retained_465 = r.U8();
        var resume_event = r.I16();
        var autoplay_active = r.I16();
        var autoplay_turns = r.I16();
        var double_production = r.I16();
        var double_movement = r.I16();
        var claimed_humans = r.I16();
        var items86 = new byte[34];
        for (int i86 = 0; i86 < 34; ++i86)
        {
            items86[i86] = r.U8();
        }
        var retained_472 = new OwnedList<byte>(items86);
        return new()
        {
            CenterX = center_x,
            CenterY = center_y,
            Introduced = introduced,
            TurnTimeLimit = turn_time_limit,
            EncodedPasswords = encoded_passwords,
            OpenGame = open_game,
            KillOnRetirement = kill_on_retirement,
            ShowHumanStarts = show_human_starts,
            ChatFromStart = chat_from_start,
            Simultaneous = simultaneous,
            CheatTurnTime = cheat_turn_time,
            Retained142 = retained_142,
            Emails = emails,
            PlayerSetup = player_setup,
            ResumeCiv = resume_civ,
            Retained465 = retained_465,
            ResumeEvent = resume_event,
            AutoplayActive = autoplay_active,
            AutoplayTurns = autoplay_turns,
            DoubleProduction = double_production,
            DoubleMovement = double_movement,
            ClaimedHumans = claimed_humans,
            Retained472 = retained_472
        };
    }

    public static byte[] Encode(MultiplayerRecord value)
    {
        var w = new ByteWriter();
        WriteMultiplayerRecord(w, value);
        return w.ToArray();
    }

    internal static void WriteMultiplayerRecord(ByteWriter w, MultiplayerRecord v)
    {
        RequireCount(v.CenterX, 8);
        foreach (var item87 in v.CenterX)
        {
            w.I16(item87);
        }
        RequireCount(v.CenterY, 8);
        foreach (var item88 in v.CenterY)
        {
            w.I16(item88);
        }
        RequireCount(v.Introduced, 8);
        foreach (var item89 in v.Introduced)
        {
            w.I16(item89);
        }
        w.I32(v.TurnTimeLimit);
        RequireCount(v.EncodedPasswords, 256);
        foreach (var item90 in v.EncodedPasswords)
        {
            w.U8(item90);
        }
        w.I16(v.OpenGame);
        w.I16(v.KillOnRetirement);
        w.I16(v.ShowHumanStarts);
        w.I16(v.ChatFromStart);
        w.I16(v.Simultaneous);
        w.I32(v.CheatTurnTime);
        RequireCount(v.Retained142, 290);
        foreach (var item91 in v.Retained142)
        {
            w.U8(item91);
        }
        RequireCount(v.Emails, 8);
        foreach (var item92 in v.Emails)
        {
            RequireCount(item92, 32);
            foreach (var item93 in item92)
            {
                w.U8(item93);
            }
        }
        RequireCount(v.PlayerSetup, 8);
        foreach (var item94 in v.PlayerSetup)
        {
            RequireCount(item94, 32);
            foreach (var item95 in item94)
            {
                w.U8(item95);
            }
        }
        w.U8(v.ResumeCiv);
        w.U8(v.Retained465);
        w.I16(v.ResumeEvent);
        w.I16(v.AutoplayActive);
        w.I16(v.AutoplayTurns);
        w.I16(v.DoubleProduction);
        w.I16(v.DoubleMovement);
        w.I16(v.ClaimedHumans);
        RequireCount(v.Retained472, 34);
        foreach (var item96 in v.Retained472)
        {
            w.U8(item96);
        }
    }

    public static EventRecord DecodeEventRecord(ReadOnlySpan<byte> bytes)
    {
        RequireSize(bytes, 444);
        var r = new ByteReader(bytes);
        return ReadEventRecord(ref r);
    }

    internal static EventRecord ReadEventRecord(ref ByteReader r)
    {
        var triggers = r.U32();
        var actions = r.U32();
        var unit_name = r.U32();
        var unit_type = r.I32();
        var city_name = r.U32();
        var who_name = r.U32();
        var who_civ = r.I32();
        var talker_type = r.I32();
        var whom_name = r.U32();
        var whom_civ = r.I32();
        var listener_type = r.I32();
        var turn = r.I32();
        var denominator = r.I32();
        var technology = r.I32();
        var items97 = new uint[20];
        for (int i97 = 0; i97 < 20; ++i97)
        {
            items97[i97] = r.U32();
        }
        var text = new OwnedList<uint>(items97);
        var move_owner_name = r.U32();
        var move_owner_civ = r.I32();
        var move_unit_name = r.U32();
        var move_unit_type = r.I32();
        var move_count = r.I32();
        var items98 = new int[8];
        for (int i98 = 0; i98 < 8; ++i98)
        {
            items98[i98] = r.I32();
        }
        var move_rectangle = new OwnedList<int>(items98);
        var move_x = r.I32();
        var move_y = r.I32();
        var aggression_whom_name = r.U32();
        var aggression_whom_civ = r.I32();
        var aggression_who_name = r.U32();
        var aggression_who_civ = r.I32();
        var create_owner_name = r.U32();
        var create_owner_civ = r.I32();
        var create_unit_name = r.U32();
        var create_unit_type = r.I32();
        var items99 = new int[20];
        for (int i99 = 0; i99 < 20; ++i99)
        {
            items99[i99] = r.I32();
        }
        var create_locations = new OwnedList<int>(items99);
        var create_location_count = r.I32();
        var create_veteran = r.I32();
        var create_homecity = r.U32();
        var auxiliary_civ_name = r.U32();
        var auxiliary_scalar = r.I32();
        var auxiliary_unit_name = r.U32();
        var items100 = new byte[40];
        for (int i100 = 0; i100 < 40; ++i100)
        {
            items100[i100] = r.U8();
        }
        var retained = new OwnedList<byte>(items100);
        var money_receiver_name = r.U32();
        var money_receiver_civ = r.I32();
        var money_amount = r.I32();
        var negotiation_flag = r.I32();
        var wave_file = r.U32();
        var cd_track = r.I32();
        var terrain_type = r.I32();
        var items101 = new int[8];
        for (int i101 = 0; i101 < 8; ++i101)
        {
            items101[i101] = r.I32();
        }
        var terrain_rectangle = new OwnedList<int>(items101);
        var destroy_civ = r.I32();
        var give_tech = r.I32();
        var give_tech_civ = r.I32();
        return new()
        {
            Triggers = triggers,
            Actions = actions,
            UnitName = unit_name,
            UnitType = unit_type,
            CityName = city_name,
            WhoName = who_name,
            WhoCiv = who_civ,
            TalkerType = talker_type,
            WhomName = whom_name,
            WhomCiv = whom_civ,
            ListenerType = listener_type,
            Turn = turn,
            Denominator = denominator,
            Technology = technology,
            Text = text,
            MoveOwnerName = move_owner_name,
            MoveOwnerCiv = move_owner_civ,
            MoveUnitName = move_unit_name,
            MoveUnitType = move_unit_type,
            MoveCount = move_count,
            MoveRectangle = move_rectangle,
            MoveX = move_x,
            MoveY = move_y,
            AggressionWhomName = aggression_whom_name,
            AggressionWhomCiv = aggression_whom_civ,
            AggressionWhoName = aggression_who_name,
            AggressionWhoCiv = aggression_who_civ,
            CreateOwnerName = create_owner_name,
            CreateOwnerCiv = create_owner_civ,
            CreateUnitName = create_unit_name,
            CreateUnitType = create_unit_type,
            CreateLocations = create_locations,
            CreateLocationCount = create_location_count,
            CreateVeteran = create_veteran,
            CreateHomecity = create_homecity,
            AuxiliaryCivName = auxiliary_civ_name,
            AuxiliaryScalar = auxiliary_scalar,
            AuxiliaryUnitName = auxiliary_unit_name,
            Retained = retained,
            MoneyReceiverName = money_receiver_name,
            MoneyReceiverCiv = money_receiver_civ,
            MoneyAmount = money_amount,
            NegotiationFlag = negotiation_flag,
            WaveFile = wave_file,
            CdTrack = cd_track,
            TerrainType = terrain_type,
            TerrainRectangle = terrain_rectangle,
            DestroyCiv = destroy_civ,
            GiveTech = give_tech,
            GiveTechCiv = give_tech_civ
        };
    }

    public static byte[] Encode(EventRecord value)
    {
        var w = new ByteWriter();
        WriteEventRecord(w, value);
        return w.ToArray();
    }

    internal static void WriteEventRecord(ByteWriter w, EventRecord v)
    {
        w.U32(v.Triggers);
        w.U32(v.Actions);
        w.U32(v.UnitName);
        w.I32(v.UnitType);
        w.U32(v.CityName);
        w.U32(v.WhoName);
        w.I32(v.WhoCiv);
        w.I32(v.TalkerType);
        w.U32(v.WhomName);
        w.I32(v.WhomCiv);
        w.I32(v.ListenerType);
        w.I32(v.Turn);
        w.I32(v.Denominator);
        w.I32(v.Technology);
        RequireCount(v.Text, 20);
        foreach (var item102 in v.Text)
        {
            w.U32(item102);
        }
        w.U32(v.MoveOwnerName);
        w.I32(v.MoveOwnerCiv);
        w.U32(v.MoveUnitName);
        w.I32(v.MoveUnitType);
        w.I32(v.MoveCount);
        RequireCount(v.MoveRectangle, 8);
        foreach (var item103 in v.MoveRectangle)
        {
            w.I32(item103);
        }
        w.I32(v.MoveX);
        w.I32(v.MoveY);
        w.U32(v.AggressionWhomName);
        w.I32(v.AggressionWhomCiv);
        w.U32(v.AggressionWhoName);
        w.I32(v.AggressionWhoCiv);
        w.U32(v.CreateOwnerName);
        w.I32(v.CreateOwnerCiv);
        w.U32(v.CreateUnitName);
        w.I32(v.CreateUnitType);
        RequireCount(v.CreateLocations, 20);
        foreach (var item104 in v.CreateLocations)
        {
            w.I32(item104);
        }
        w.I32(v.CreateLocationCount);
        w.I32(v.CreateVeteran);
        w.U32(v.CreateHomecity);
        w.U32(v.AuxiliaryCivName);
        w.I32(v.AuxiliaryScalar);
        w.U32(v.AuxiliaryUnitName);
        RequireCount(v.Retained, 40);
        foreach (var item105 in v.Retained)
        {
            w.U8(item105);
        }
        w.U32(v.MoneyReceiverName);
        w.I32(v.MoneyReceiverCiv);
        w.I32(v.MoneyAmount);
        w.I32(v.NegotiationFlag);
        w.U32(v.WaveFile);
        w.I32(v.CdTrack);
        w.I32(v.TerrainType);
        RequireCount(v.TerrainRectangle, 8);
        foreach (var item106 in v.TerrainRectangle)
        {
            w.I32(item106);
        }
        w.I32(v.DestroyCiv);
        w.I32(v.GiveTech);
        w.I32(v.GiveTechCiv);
    }

    public static StoredUnit DecodeStoredUnit(ReadOnlySpan<byte> bytes)
    {
        RequireSize(bytes, 32);
        var r = new ByteReader(bytes);
        return ReadStoredUnit(ref r);
    }

    internal static StoredUnit ReadStoredUnit(ref ByteReader r)
    {
        var x = r.I16();
        var y = r.I16();
        var flags = r.U16();
        var type = new UnitType(r.U8());
        var owner = new CivilizationSlot(r.U8());
        var moves_used = r.U8();
        var visibility = r.U8();
        var damage = r.U8();
        var last_direction = r.U8();
        var ai_task = r.U8();
        var counter = r.U8();
        var auxiliary_timer = r.U8();
        var orders = (UnitOrder)r.I8();
        var home = ReadHome(ref r);
        var retained_byte = r.U8();
        var destination_x = r.I16();
        var destination_y = r.I16();
        var previous = r.I16();
        var next = r.I16();
        var token = new UnitToken(r.U32());
        var retained_word = r.U16();
        return new()
        {
            X = x,
            Y = y,
            Flags = flags,
            Type = type,
            Owner = owner,
            MovesUsed = moves_used,
            Visibility = visibility,
            Damage = damage,
            LastDirection = last_direction,
            AiTask = ai_task,
            Counter = counter,
            AuxiliaryTimer = auxiliary_timer,
            Orders = orders,
            Home = home,
            RetainedByte = retained_byte,
            DestinationX = destination_x,
            DestinationY = destination_y,
            Previous = previous,
            Next = next,
            Token = token,
            RetainedWord = retained_word
        };
    }

    public static byte[] Encode(StoredUnit value)
    {
        var w = new ByteWriter();
        WriteStoredUnit(w, value);
        return w.ToArray();
    }

    internal static void WriteStoredUnit(ByteWriter w, StoredUnit v)
    {
        w.I16(v.X);
        w.I16(v.Y);
        w.U16(v.Flags);
        w.U8(v.Type.Value);
        w.U8(v.Owner.Value);
        w.U8(v.MovesUsed);
        w.U8(v.Visibility);
        w.U8(v.Damage);
        w.U8(v.LastDirection);
        w.U8(v.AiTask);
        w.U8(v.Counter);
        w.U8(v.AuxiliaryTimer);
        w.I8((sbyte)v.Orders);
        WriteHome(w, v.Home);
        w.U8(v.RetainedByte);
        w.I16(v.DestinationX);
        w.I16(v.DestinationY);
        w.I16(v.Previous);
        w.I16(v.Next);
        w.U32(v.Token.Value);
        w.U16(v.RetainedWord);
    }

    public static StoredCity DecodeStoredCity(ReadOnlySpan<byte> bytes)
    {
        RequireSize(bytes, 88);
        var r = new ByteReader(bytes);
        return ReadStoredCity(ref r);
    }

    internal static StoredCity ReadStoredCity(ref ByteReader r)
    {
        var x = r.I16();
        var y = r.I16();
        var flags = r.U32();
        var owner = new CivilizationSlot(r.U8());
        var size = r.I8();
        var founder = new CivilizationSlot(r.U8());
        var turns_captured = r.U8();
        var known_to = r.U8();
        var items107 = new byte[8];
        for (int i107 = 0; i107 < 8; ++i107)
        {
            items107[i107] = r.U8();
        }
        var remembered_size = new OwnedList<byte>(items107);
        var retained_byte = r.U8();
        var specialist_kinds = r.U32();
        var food_box = r.I16();
        var shield_box = r.I16();
        var base_trade = r.I16();
        var name = new OwnedList<byte>(r.Take(16).ToArray());
        var worked_tiles = r.U32();
        var items108 = new byte[5];
        for (int i108 = 0; i108 < 5; ++i108)
        {
            items108[i108] = r.U8();
        }
        var improvements = new OwnedList<byte>(items108);
        var production = r.I8();
        var route_count = r.U8();
        var items109 = new sbyte[3];
        for (int i109 = 0; i109 < 3; ++i109)
        {
            items109[i109] = r.I8();
        }
        var supplies = new OwnedList<sbyte>(items109);
        var items110 = new sbyte[3];
        for (int i110 = 0; i110 < 3; ++i110)
        {
            items110[i110] = r.I8();
        }
        var demands = new OwnedList<sbyte>(items110);
        var items111 = new sbyte[3];
        for (int i111 = 0; i111 < 3; ++i111)
        {
            items111[i111] = r.I8();
        }
        var route_goods = new OwnedList<sbyte>(items111);
        var items112 = new short[3];
        for (int i112 = 0; i112 < 3; ++i112)
        {
            items112[i112] = r.I16();
        }
        var route_partners = new OwnedList<short>(items112);
        var science = r.I16();
        var tax = r.I16();
        var trade = r.I16();
        var total_food = r.U8();
        var total_shields = r.U8();
        var happy = r.U8();
        var unhappy = r.U8();
        var token = new CityToken(r.U32());
        return new()
        {
            X = x,
            Y = y,
            Flags = flags,
            Owner = owner,
            Size = size,
            Founder = founder,
            TurnsCaptured = turns_captured,
            KnownTo = known_to,
            RememberedSize = remembered_size,
            RetainedByte = retained_byte,
            SpecialistKinds = specialist_kinds,
            FoodBox = food_box,
            ShieldBox = shield_box,
            BaseTrade = base_trade,
            Name = name,
            WorkedTiles = worked_tiles,
            Improvements = improvements,
            Production = production,
            RouteCount = route_count,
            Supplies = supplies,
            Demands = demands,
            RouteGoods = route_goods,
            RoutePartners = route_partners,
            Science = science,
            Tax = tax,
            Trade = trade,
            TotalFood = total_food,
            TotalShields = total_shields,
            Happy = happy,
            Unhappy = unhappy,
            Token = token
        };
    }

    public static byte[] Encode(StoredCity value)
    {
        var w = new ByteWriter();
        WriteStoredCity(w, value);
        return w.ToArray();
    }

    internal static void WriteStoredCity(ByteWriter w, StoredCity v)
    {
        w.I16(v.X);
        w.I16(v.Y);
        w.U32(v.Flags);
        w.U8(v.Owner.Value);
        w.I8(v.Size);
        w.U8(v.Founder.Value);
        w.U8(v.TurnsCaptured);
        w.U8(v.KnownTo);
        RequireCount(v.RememberedSize, 8);
        foreach (var item113 in v.RememberedSize)
        {
            w.U8(item113);
        }
        w.U8(v.RetainedByte);
        w.U32(v.SpecialistKinds);
        w.I16(v.FoodBox);
        w.I16(v.ShieldBox);
        w.I16(v.BaseTrade);
        RequireCount(v.Name, 16);
        foreach (byte b in v.Name)
        {
            w.U8(b);
        }

        w.U32(v.WorkedTiles);
        RequireCount(v.Improvements, 5);
        foreach (var item114 in v.Improvements)
        {
            w.U8(item114);
        }
        w.I8(v.Production);
        w.U8(v.RouteCount);
        RequireCount(v.Supplies, 3);
        foreach (var item115 in v.Supplies)
        {
            w.I8(item115);
        }
        RequireCount(v.Demands, 3);
        foreach (var item116 in v.Demands)
        {
            w.I8(item116);
        }
        RequireCount(v.RouteGoods, 3);
        foreach (var item117 in v.RouteGoods)
        {
            w.I8(item117);
        }
        RequireCount(v.RoutePartners, 3);
        foreach (var item118 in v.RoutePartners)
        {
            w.I16(item118);
        }
        w.I16(v.Science);
        w.I16(v.Tax);
        w.I16(v.Trade);
        w.U8(v.TotalFood);
        w.U8(v.TotalShields);
        w.U8(v.Happy);
        w.U8(v.Unhappy);
        w.U32(v.Token.Value);
    }

    public static MapHeader DecodeMapHeader(ReadOnlySpan<byte> bytes)
    {
        RequireSize(bytes, 14);
        var r = new ByteReader(bytes);
        return ReadMapHeader(ref r);
    }

    internal static MapHeader ReadMapHeader(ref ByteReader r)
    {
        var width = r.I16();
        var height = r.I16();
        var area = r.I16();
        var topology = r.U16();
        var seed = r.U16();
        var coarse_width = r.I16();
        var coarse_height = r.I16();
        return new()
        {
            Width = width,
            Height = height,
            Area = area,
            Topology = topology,
            Seed = seed,
            CoarseWidth = coarse_width,
            CoarseHeight = coarse_height
        };
    }

    public static byte[] Encode(MapHeader value)
    {
        var w = new ByteWriter();
        WriteMapHeader(w, value);
        return w.ToArray();
    }

    internal static void WriteMapHeader(ByteWriter w, MapHeader v)
    {
        w.I16(v.Width);
        w.I16(v.Height);
        w.I16(v.Area);
        w.U16(v.Topology);
        w.U16(v.Seed);
        w.I16(v.CoarseWidth);
        w.I16(v.CoarseHeight);
    }

    public static CivilizationGoal DecodeCivilizationGoal(ReadOnlySpan<byte> bytes)
    {
        RequireSize(bytes, 6);
        var r = new ByteReader(bytes);
        return ReadCivilizationGoal(ref r);
    }

    internal static CivilizationGoal ReadCivilizationGoal(ref ByteReader r)
    {
        var x = r.I16();
        var y = r.I16();
        var role = r.I8();
        var priority = r.I8();
        return new() { X = x, Y = y, Role = role, Priority = priority };
    }

    public static byte[] Encode(CivilizationGoal value)
    {
        var w = new ByteWriter();
        WriteCivilizationGoal(w, value);
        return w.ToArray();
    }

    internal static void WriteCivilizationGoal(ByteWriter w, CivilizationGoal v)
    {
        w.I16(v.X);
        w.I16(v.Y);
        w.I8(v.Role);
        w.I8(v.Priority);
    }

}
