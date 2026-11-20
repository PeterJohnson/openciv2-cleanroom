using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.State;
using RuntimeCity = OpenCiv2.Core.State.City;

namespace OpenCiv2.Formats;

/// <summary>F06 §3 explicit stored/live adapters. No installation, census or lifetime renewal.</summary>
public static partial class StateConversion
{
    private static void Copy<T>(OwnedList<T> source, Span<T> target)
    {
        if (source.Count != target.Length)
        {
            throw new ArgumentException("Stored field has an incorrect extent", nameof(source));
        }

        for (int i = 0; i < target.Length; ++i)
        {
            target[i] = source[i];
        }
    }

    private static OwnedList<byte> LiveText(OwnedList<byte> source, int length)
    {
        if (source.Count != length)
        {
            throw new ArgumentException("Stored text has an incorrect extent", nameof(source));
        }

        return new(source.TakeWhile(value => value != 0));
    }

    private static OwnedList<byte> StoredText(OwnedList<byte> source, int length)
    {
        if (source.Count > length || source.Contains((byte)0))
        {
            throw new ArgumentException("Text cannot be represented in the classic field", nameof(source));
        }

        byte[] result = new byte[length];
        for (int i = 0; i < source.Count; ++i)
        {
            result[i] = source[i];
        }

        return new(result);
    }

    public static Unit ToLive(StoredUnit value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var result = new Unit();
        result.X = value.X;
        result.Y = value.Y;
        result.Flags = value.Flags;
        result.Type = value.Type;
        result.Owner = value.Owner;
        result.MovesUsed = value.MovesUsed;
        result.Visibility = value.Visibility;
        result.Damage = value.Damage;
        result.LastDirection = value.LastDirection;
        result.AiTask = value.AiTask;
        result.Counter = value.Counter;
        result.AuxiliaryTimer = value.AuxiliaryTimer;
        result.Orders = value.Orders;
        result.Home = value.Home;
        result.DestinationX = value.DestinationX;
        result.DestinationY = value.DestinationY;
        result.Previous = new(value.Previous);
        result.Next = new(value.Next);
        result.Token = value.Token;
        return result;
    }

    public static StoredUnit ToStored(in Unit value) => new()
    {
        X = value.X,
        Y = value.Y,
        Flags = value.Flags,
        Type = value.Type,
        Owner = value.Owner,
        MovesUsed = value.MovesUsed,
        Visibility = value.Visibility,
        Damage = value.Damage,
        LastDirection = value.LastDirection,
        AiTask = value.AiTask,
        Counter = value.Counter,
        AuxiliaryTimer = value.AuxiliaryTimer,
        Orders = value.Orders,
        Home = value.Home,
        DestinationX = value.DestinationX,
        DestinationY = value.DestinationY,
        Previous = value.Previous.Value,
        Next = value.Next.Value,
        Token = value.Token,
    };

    public static StoredUnit PreserveOpaque(StoredUnit output, StoredUnit imported) => output with
    {
        RetainedByte = imported.RetainedByte,
        RetainedWord = imported.RetainedWord,
    };

    public static RuntimeCity ToLive(StoredCity value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var result = new RuntimeCity();
        result.X = value.X;
        result.Y = value.Y;
        result.Flags = value.Flags;
        result.Owner = value.Owner;
        result.Size = value.Size;
        result.Founder = value.Founder;
        result.TurnsCaptured = value.TurnsCaptured;
        result.KnownTo = value.KnownTo;
        Copy(value.RememberedSize, result.RememberedSize);
        result.SpecialistKinds = value.SpecialistKinds;
        result.FoodBox = value.FoodBox;
        result.ShieldBox = value.ShieldBox;
        result.BaseTrade = value.BaseTrade;
        result.Name = LiveText(value.Name, 16);
        result.WorkedTiles = value.WorkedTiles;
        Copy(value.Improvements, result.Improvements);
        result.Production = value.Production;
        result.RouteCount = value.RouteCount;
        Copy(value.Supplies, result.Supplies);
        Copy(value.Demands, result.Demands);
        Copy(value.RouteGoods, result.RouteGoods);
        if (value.RoutePartners.Count != 3)
        {
            throw new ArgumentException("Stored route partners require three values", nameof(value));
        }

        for (int i = 0; i < 3; ++i)
        {
            result.RoutePartners[i] = new(value.RoutePartners[i]);
        }

        result.Science = value.Science;
        result.Tax = value.Tax;
        result.Trade = value.Trade;
        result.TotalFood = value.TotalFood;
        result.TotalShields = value.TotalShields;
        result.Happy = value.Happy;
        result.Unhappy = value.Unhappy;
        result.Token = value.Token;
        return result;
    }

    public static StoredCity ToStored(in RuntimeCity value) => new()
    {
        X = value.X,
        Y = value.Y,
        Flags = value.Flags,
        Owner = value.Owner,
        Size = value.Size,
        Founder = value.Founder,
        TurnsCaptured = value.TurnsCaptured,
        KnownTo = value.KnownTo,
        RememberedSize = new(((ReadOnlySpan<byte>)value.RememberedSize).ToArray()),
        SpecialistKinds = value.SpecialistKinds,
        FoodBox = value.FoodBox,
        ShieldBox = value.ShieldBox,
        BaseTrade = value.BaseTrade,
        Name = StoredText(value.Name, 16),
        WorkedTiles = value.WorkedTiles,
        Improvements = new(((ReadOnlySpan<byte>)value.Improvements).ToArray()),
        Production = value.Production,
        RouteCount = value.RouteCount,
        Supplies = new(((ReadOnlySpan<sbyte>)value.Supplies).ToArray()),
        Demands = new(((ReadOnlySpan<sbyte>)value.Demands).ToArray()),
        RouteGoods = new(((ReadOnlySpan<sbyte>)value.RouteGoods).ToArray()),
        RoutePartners = new([value.RoutePartners[0].Value, value.RoutePartners[1].Value, value.RoutePartners[2].Value]),
        Science = value.Science,
        Tax = value.Tax,
        Trade = value.Trade,
        TotalFood = value.TotalFood,
        TotalShields = value.TotalShields,
        Happy = value.Happy,
        Unhappy = value.Unhappy,
        Token = value.Token,
    };

    public static StoredCity PreserveOpaque(StoredCity output, StoredCity imported) => output with
    {
        RetainedByte = imported.RetainedByte,
    };

    public static GlobalValues ToLive(StoredGlobals value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var result = new GlobalValues();
        result.Rules = value.Rules;
        result.GameOptions = value.GameOptions;
        result.TransientFlags = value.TransientFlags;
        result.GameFlags = value.GameFlags;
        result.MessageOptions = value.MessageOptions;
        result.ShownEventFlags = value.ShownEventFlags;
        result.Turn = value.Turn;
        result.Year = value.Year;
        result.FirstLaunchTurn = value.FirstLaunchTurn;
        result.ActiveUnit = new(value.ActiveUnit);
        result.MovingUnit = new(value.MovingUnit);
        result.Multiplayer = (GameMode)value.Multiplayer;
        result.PlayerCiv = value.PlayerCiv;
        result.SavedCurrentCiv = value.SavedCurrentCiv;
        result.TurnCiv = value.TurnCiv;
        result.ViewCiv = value.ViewCiv;
        result.RevealMap = value.RevealMap;
        result.Difficulty = (Difficulty)value.Difficulty;
        result.BarbarianActivity = value.BarbarianActivity;
        result.CivsAlive = value.CivsAlive;
        result.HumanPlayers = value.HumanPlayers;
        result.CivsAtStart = value.CivsAtStart;
        result.NumEnemies = value.NumEnemies;
        result.WarmingCounter = value.WarmingCounter;
        result.GlobalWarmings = value.GlobalWarmings;
        result.WarmingPollution = value.WarmingPollution;
        result.PollutedTiles = value.PollutedTiles;
        result.PeaceTurns = value.PeaceTurns;
        result.UnitEnd = value.UnitEnd;
        result.CityEnd = value.CityEnd;
        result.NumAdvances = value.NumAdvances;
        Copy(value.FirstDiscoverer, result.FirstDiscoverer);
        Copy(value.AdvanceKnownBy, result.AdvanceKnownBy);
        Copy(value.WonderCity, result.WonderCity);
        result.NextHistoryTurn = value.NextHistoryTurn;
        result.BestHumanRank = value.BestHumanRank;
        result.WorstHumanRank = value.WorstHumanRank;
        Copy(value.PowerRank, result.PowerRank);
        Copy(value.CivByRank, result.CivByRank);
        return result;
    }

    public static StoredGlobals ToStored(in GlobalValues value) => new()
    {
        Rules = value.Rules,
        GameOptions = value.GameOptions,
        TransientFlags = value.TransientFlags,
        GameFlags = value.GameFlags,
        MessageOptions = value.MessageOptions,
        ShownEventFlags = value.ShownEventFlags,
        Turn = value.Turn,
        Year = value.Year,
        FirstLaunchTurn = value.FirstLaunchTurn,
        ActiveUnit = value.ActiveUnit.Value,
        MovingUnit = value.MovingUnit.Value,
        Multiplayer = unchecked((byte)value.Multiplayer),
        PlayerCiv = value.PlayerCiv,
        SavedCurrentCiv = value.SavedCurrentCiv,
        TurnCiv = value.TurnCiv,
        ViewCiv = value.ViewCiv,
        RevealMap = value.RevealMap,
        Difficulty = unchecked((byte)value.Difficulty),
        BarbarianActivity = value.BarbarianActivity,
        CivsAlive = value.CivsAlive,
        HumanPlayers = value.HumanPlayers,
        CivsAtStart = value.CivsAtStart,
        NumEnemies = value.NumEnemies,
        WarmingCounter = value.WarmingCounter,
        GlobalWarmings = value.GlobalWarmings,
        WarmingPollution = value.WarmingPollution,
        PollutedTiles = value.PollutedTiles,
        PeaceTurns = value.PeaceTurns,
        UnitEnd = value.UnitEnd,
        CityEnd = value.CityEnd,
        NumAdvances = value.NumAdvances,
        FirstDiscoverer = new(((ReadOnlySpan<byte>)value.FirstDiscoverer).ToArray()),
        AdvanceKnownBy = new(((ReadOnlySpan<byte>)value.AdvanceKnownBy).ToArray()),
        WonderCity = new(((ReadOnlySpan<short>)value.WonderCity).ToArray()),
        NextHistoryTurn = value.NextHistoryTurn,
        BestHumanRank = value.BestHumanRank,
        WorstHumanRank = value.WorstHumanRank,
        PowerRank = new(((ReadOnlySpan<byte>)value.PowerRank).ToArray()),
        CivByRank = new(((ReadOnlySpan<byte>)value.CivByRank).ToArray()),
    };

    public static StoredGlobals PreserveOpaque(StoredGlobals output, StoredGlobals imported) => output with
    {
        Retained0e = imported.Retained0e,
        Retained34 = imported.Retained34,
    };

    public static Civilization ToLive(StoredCivilization value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var result = new Civilization();
        result.Flags = value.Flags;
        result.Gold = value.Gold;
        result.Tribe = value.Tribe;
        result.ResearchProgress = value.ResearchProgress;
        result.Researching = value.Researching;
        result.CapitalX = value.CapitalX;
        result.LastCityTurn = value.LastCityTurn;
        result.NumAdvances = value.NumAdvances;
        result.FutureTechs = value.FutureTechs;
        result.ResearchCostAdvances = value.ResearchCostAdvances;
        result.ScienceRate = value.ScienceRate;
        result.TaxRate = value.TaxRate;
        result.Government = (Government)value.Government;
        result.DiploRandom = value.DiploRandom;
        Copy(value.WondersByGroup, result.WondersByGroup);
        result.SubversionCount = value.SubversionCount;
        result.Reputation = value.Reputation;
        result.DiploRequestCount = value.DiploRequestCount;
        Copy(value.Treaties, result.Treaties);
        Copy(value.Attitude, result.Attitude);
        Copy(value.ReputationCredit, result.ReputationCredit);
        Copy(value.AggressionCounter, result.AggressionCounter);
        Copy(value.Advances, result.Advances);
        result.MilitaryCount = value.MilitaryCount;
        result.CityCount = value.CityCount;
        result.ShipCount = value.ShipCount;
        result.TotalCitySize = value.TotalCitySize;
        result.MilitaryStrength = value.MilitaryStrength;
        result.TotalAttack = value.TotalAttack;
        result.PopulationMilestone = value.PopulationMilestone;
        Copy(value.AdvanceSource, result.AdvanceSource);
        Copy(value.UnitsActive, result.UnitsActive);
        Copy(value.UnitsLost, result.UnitsLost);
        Copy(value.UnitsInProduction, result.UnitsInProduction);
        Copy(value.ContinentMilitary, result.ContinentMilitary);
        Copy(value.ContinentAttack, result.ContinentAttack);
        Copy(value.ContinentCities, result.ContinentCities);
        Copy(value.ContinentCitySize, result.ContinentCitySize);
        Copy(value.ContinentTradeUnits, result.ContinentTradeUnits);
        Copy(value.ContinentFlags, result.ContinentFlags);
        Copy(value.ContinentPolicy, result.ContinentPolicy);
        result.BestShieldSurplus = value.BestShieldSurplus;
        Copy(value.GovernmentScore, result.GovernmentScore);
        Copy(value.LastContactTurn, result.LastContactTurn);
        result.LeaderPortrait = value.LeaderPortrait;
        Copy(value.ThroneRoom, result.ThroneRoom);
        result.ThroneRoomUpgrades = value.ThroneRoomUpgrades;
        result.SpaceshipFlags = value.SpaceshipFlags;
        result.SpaceshipArrivalYear = value.SpaceshipArrivalYear;
        result.SpaceshipLaunchYear = value.SpaceshipLaunchYear;
        result.SpaceshipPopulation = value.SpaceshipPopulation;
        Copy(value.SpaceshipParts, result.SpaceshipParts);
        Copy(value.Goals, result.Goals);
        Copy(value.StandingGoals, result.StandingGoals);
        return result;
    }

    public static StoredCivilization ToStored(in Civilization value) => new()
    {
        Flags = value.Flags,
        Gold = value.Gold,
        Tribe = value.Tribe,
        ResearchProgress = value.ResearchProgress,
        Researching = value.Researching,
        CapitalX = value.CapitalX,
        LastCityTurn = value.LastCityTurn,
        NumAdvances = value.NumAdvances,
        FutureTechs = value.FutureTechs,
        ResearchCostAdvances = value.ResearchCostAdvances,
        ScienceRate = value.ScienceRate,
        TaxRate = value.TaxRate,
        Government = unchecked((byte)value.Government),
        DiploRandom = value.DiploRandom,
        WondersByGroup = new(((ReadOnlySpan<byte>)value.WondersByGroup).ToArray()),
        SubversionCount = value.SubversionCount,
        Reputation = value.Reputation,
        DiploRequestCount = value.DiploRequestCount,
        Treaties = new(((ReadOnlySpan<uint>)value.Treaties).ToArray()),
        Attitude = new(((ReadOnlySpan<byte>)value.Attitude).ToArray()),
        ReputationCredit = new(((ReadOnlySpan<sbyte>)value.ReputationCredit).ToArray()),
        AggressionCounter = new(((ReadOnlySpan<byte>)value.AggressionCounter).ToArray()),
        Advances = new(((ReadOnlySpan<byte>)value.Advances).ToArray()),
        MilitaryCount = value.MilitaryCount,
        CityCount = value.CityCount,
        ShipCount = value.ShipCount,
        TotalCitySize = value.TotalCitySize,
        MilitaryStrength = value.MilitaryStrength,
        TotalAttack = value.TotalAttack,
        PopulationMilestone = value.PopulationMilestone,
        AdvanceSource = new(((ReadOnlySpan<byte>)value.AdvanceSource).ToArray()),
        UnitsActive = new(((ReadOnlySpan<byte>)value.UnitsActive).ToArray()),
        UnitsLost = new(((ReadOnlySpan<byte>)value.UnitsLost).ToArray()),
        UnitsInProduction = new(((ReadOnlySpan<byte>)value.UnitsInProduction).ToArray()),
        ContinentMilitary = new(((ReadOnlySpan<ushort>)value.ContinentMilitary).ToArray()),
        ContinentAttack = new(((ReadOnlySpan<ushort>)value.ContinentAttack).ToArray()),
        ContinentCities = new(((ReadOnlySpan<byte>)value.ContinentCities).ToArray()),
        ContinentCitySize = new(((ReadOnlySpan<byte>)value.ContinentCitySize).ToArray()),
        ContinentTradeUnits = new(((ReadOnlySpan<byte>)value.ContinentTradeUnits).ToArray()),
        ContinentFlags = new(((ReadOnlySpan<byte>)value.ContinentFlags).ToArray()),
        ContinentPolicy = new(((ReadOnlySpan<byte>)value.ContinentPolicy).ToArray()),
        BestShieldSurplus = value.BestShieldSurplus,
        GovernmentScore = new(((ReadOnlySpan<short>)value.GovernmentScore).ToArray()),
        LastContactTurn = new(((ReadOnlySpan<short>)value.LastContactTurn).ToArray()),
        LeaderPortrait = value.LeaderPortrait,
        ThroneRoom = new(((ReadOnlySpan<byte>)value.ThroneRoom).ToArray()),
        ThroneRoomUpgrades = value.ThroneRoomUpgrades,
        SpaceshipFlags = value.SpaceshipFlags,
        SpaceshipArrivalYear = value.SpaceshipArrivalYear,
        SpaceshipLaunchYear = value.SpaceshipLaunchYear,
        SpaceshipPopulation = value.SpaceshipPopulation,
        SpaceshipParts = new(((ReadOnlySpan<short>)value.SpaceshipParts).ToArray()),
        Goals = new(((ReadOnlySpan<CivilizationGoal>)value.Goals).ToArray()),
        StandingGoals = new(((ReadOnlySpan<CivilizationGoal>)value.StandingGoals).ToArray()),
    };

    public static StoredCivilization PreserveOpaque(StoredCivilization output, StoredCivilization imported) =>
        output with
        {
            Retained1b = imported.Retained1b,
            Retained65 = imported.Retained65,
            Retained3fd = imported.Retained3fd,
            Retained401 = imported.Retained401,
        };
}
