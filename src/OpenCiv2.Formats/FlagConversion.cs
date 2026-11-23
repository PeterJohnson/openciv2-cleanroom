using OpenCiv2.Core.State;

namespace OpenCiv2.Formats;

/// <summary>Explicit encodings of the repository's named flag contracts. No implicit numeric conversions.</summary>
public static partial class StateConversion
{
    public const ushort UnitStatusMask = 0xffff;

    public static UnitStatus DecodeUnitStatus(ushort bits) => new()
    {
        Scratch = (bits & 0x1) != 0,
        SpyMission = (bits & 0x2) != 0,
        BorderIntruder = (bits & 0x4) != 0,
        Retreating = (bits & 0x8) != 0,
        Paradropped = (bits & 0x10) != 0,
        BorderWarned = (bits & 0x20) != 0,
        Moved = (bits & 0x40) != 0,
        GoalReached = (bits & 0x80) != 0,
        Garrison = (bits & 0x100) != 0,
        Colonist = (bits & 0x200) != 0,
        Aggressive = (bits & 0x400) != 0,
        CostsShield = (bits & 0x800) != 0,
        Aboard = (bits & 0x1000) != 0,
        Veteran = (bits & 0x2000) != 0,
        Waiting = (bits & 0x4000) != 0,
        Automated = (bits & 0x8000) != 0
    };

    public static ushort EncodeUnitStatus(UnitStatus value) => (ushort)(
        (value.Scratch ? 0x1U : 0U) |
        (value.SpyMission ? 0x2U : 0U) |
        (value.BorderIntruder ? 0x4U : 0U) |
        (value.Retreating ? 0x8U : 0U) |
        (value.Paradropped ? 0x10U : 0U) |
        (value.BorderWarned ? 0x20U : 0U) |
        (value.Moved ? 0x40U : 0U) |
        (value.GoalReached ? 0x80U : 0U) |
        (value.Garrison ? 0x100U : 0U) |
        (value.Colonist ? 0x200U : 0U) |
        (value.Aggressive ? 0x400U : 0U) |
        (value.CostsShield ? 0x800U : 0U) |
        (value.Aboard ? 0x1000U : 0U) |
        (value.Veteran ? 0x2000U : 0U) |
        (value.Waiting ? 0x4000U : 0U) |
        (value.Automated ? 0x8000U : 0U));

    public const uint CityStatusMask = 0xf7fffff;

    public static CityStatus DecodeCityStatus(uint bits) => new()
    {
        Disorder = (bits & 0x1) != 0,
        Celebrating = (bits & 0x2) != 0,
        ImprovementSold = (bits & 0x4) != 0,
        TechnologyStolen = (bits & 0x8) != 0,
        Autobuild = (bits & 0x10) != 0,
        DefenderLost = (bits & 0x20) != 0,
        SettlerNearby = (bits & 0x40) != 0,
        Coastal = (bits & 0x80) != 0,
        WonderBuilt = (bits & 0x100) != 0,
        FerryPort = (bits & 0x200) != 0,
        FreshPort = (bits & 0x400) != 0,
        HydroPossible = (bits & 0x800) != 0,
        HadCaravan = (bits & 0x1000) != 0,
        SecondDisorderTurn = (bits & 0x2000) != 0,
        MoodOnEdge = (bits & 0x4000) != 0,
        SpecialistHeld = (bits & 0x8000) != 0,
        Airlifted = (bits & 0x10000) != 0,
        TradeDirty = (bits & 0x20000) != 0,
        SamScoringInput = (bits & 0x40000) != 0,
        TerrainWorkGoal = (bits & 0x80000) != 0,
        LongDisorder = (bits & 0x100000) != 0,
        OceanAccess = (bits & 0x200000) != 0,
        Investigated = (bits & 0x400000) != 0,
        UnitsOnlyAutomation = (bits & 0x1000000) != 0,
        ImprovementsOnlyAutomation = (bits & 0x2000000) != 0,
        Objective = (bits & 0x4000000) != 0,
        CoastalFortressUsed = (bits & 0x8000000) != 0
    };

    public static uint EncodeCityStatus(CityStatus value) => (uint)(
        (value.Disorder ? 0x1U : 0U) |
        (value.Celebrating ? 0x2U : 0U) |
        (value.ImprovementSold ? 0x4U : 0U) |
        (value.TechnologyStolen ? 0x8U : 0U) |
        (value.Autobuild ? 0x10U : 0U) |
        (value.DefenderLost ? 0x20U : 0U) |
        (value.SettlerNearby ? 0x40U : 0U) |
        (value.Coastal ? 0x80U : 0U) |
        (value.WonderBuilt ? 0x100U : 0U) |
        (value.FerryPort ? 0x200U : 0U) |
        (value.FreshPort ? 0x400U : 0U) |
        (value.HydroPossible ? 0x800U : 0U) |
        (value.HadCaravan ? 0x1000U : 0U) |
        (value.SecondDisorderTurn ? 0x2000U : 0U) |
        (value.MoodOnEdge ? 0x4000U : 0U) |
        (value.SpecialistHeld ? 0x8000U : 0U) |
        (value.Airlifted ? 0x10000U : 0U) |
        (value.TradeDirty ? 0x20000U : 0U) |
        (value.SamScoringInput ? 0x40000U : 0U) |
        (value.TerrainWorkGoal ? 0x80000U : 0U) |
        (value.LongDisorder ? 0x100000U : 0U) |
        (value.OceanAccess ? 0x200000U : 0U) |
        (value.Investigated ? 0x400000U : 0U) |
        (value.UnitsOnlyAutomation ? 0x1000000U : 0U) |
        (value.ImprovementsOnlyAutomation ? 0x2000000U : 0U) |
        (value.Objective ? 0x4000000U : 0U) |
        (value.CoastalFortressUsed ? 0x8000000U : 0U));

    public const ushort CivilizationStatusMask = 0x3bf;

    public static CivilizationStatus DecodeCivilizationStatus(ushort bits) => new()
    {
        Revolution = (bits & 0x1) != 0,
        AtWar = (bits & 0x2) != 0,
        SenateActive = (bits & 0x4) != 0,
        GovernmentChanged = (bits & 0x8) != 0,
        EiffelApplied = (bits & 0x10) != 0,
        PendingFreeAdvance = (bits & 0x20) != 0,
        TradedTechnology = (bits & 0x80) != 0,
        HasAllied = (bits & 0x100) != 0,
        FemaleLeader = (bits & 0x200) != 0
    };

    public static ushort EncodeCivilizationStatus(CivilizationStatus value) => (ushort)(
        (value.Revolution ? 0x1U : 0U) |
        (value.AtWar ? 0x2U : 0U) |
        (value.SenateActive ? 0x4U : 0U) |
        (value.GovernmentChanged ? 0x8U : 0U) |
        (value.EiffelApplied ? 0x10U : 0U) |
        (value.PendingFreeAdvance ? 0x20U : 0U) |
        (value.TradedTechnology ? 0x80U : 0U) |
        (value.HasAllied ? 0x100U : 0U) |
        (value.FemaleLeader ? 0x200U : 0U));

    public const uint RelationshipMask = 0x1ff7fff;

    public static Relationship DecodeRelationship(uint bits) => new()
    {
        Contact = (bits & 0x1) != 0,
        Ceasefire = (bits & 0x2) != 0,
        Peace = (bits & 0x4) != 0,
        Alliance = (bits & 0x8) != 0,
        Vendetta = (bits & 0x10) != 0,
        HostileIntent = (bits & 0x20) != 0,
        BrokenTreaty = (bits & 0x40) != 0,
        Embassy = (bits & 0x80) != 0,
        NuclearThreat = (bits & 0x100) != 0,
        Fighting = (bits & 0x200) != 0,
        RecentContact = (bits & 0x400) != 0,
        Aggressor = (bits & 0x800) != 0,
        SneakAttack = (bits & 0x1000) != 0,
        War = (bits & 0x2000) != 0,
        ChangedThisTurn = (bits & 0x4000) != 0,
        Incident = (bits & 0x10000) != 0,
        Nuked = (bits & 0x20000) != 0,
        Tribute = (bits & 0x40000) != 0,
        ExtendedAggressor = (bits & 0x80000) != 0,
        Mercenary = (bits & 0x100000) != 0,
        WarThisTurn = (bits & 0x200000) != 0,
        CityCaptured = (bits & 0x400000) != 0,
        MetThisTurn = (bits & 0x800000) != 0,
        SpyChat = (bits & 0x1000000) != 0
    };

    public static uint EncodeRelationship(Relationship value) => (uint)(
        (value.Contact ? 0x1U : 0U) |
        (value.Ceasefire ? 0x2U : 0U) |
        (value.Peace ? 0x4U : 0U) |
        (value.Alliance ? 0x8U : 0U) |
        (value.Vendetta ? 0x10U : 0U) |
        (value.HostileIntent ? 0x20U : 0U) |
        (value.BrokenTreaty ? 0x40U : 0U) |
        (value.Embassy ? 0x80U : 0U) |
        (value.NuclearThreat ? 0x100U : 0U) |
        (value.Fighting ? 0x200U : 0U) |
        (value.RecentContact ? 0x400U : 0U) |
        (value.Aggressor ? 0x800U : 0U) |
        (value.SneakAttack ? 0x1000U : 0U) |
        (value.War ? 0x2000U : 0U) |
        (value.ChangedThisTurn ? 0x4000U : 0U) |
        (value.Incident ? 0x10000U : 0U) |
        (value.Nuked ? 0x20000U : 0U) |
        (value.Tribute ? 0x40000U : 0U) |
        (value.ExtendedAggressor ? 0x80000U : 0U) |
        (value.Mercenary ? 0x100000U : 0U) |
        (value.WarThisTurn ? 0x200000U : 0U) |
        (value.CityCaptured ? 0x400000U : 0U) |
        (value.MetThisTurn ? 0x800000U : 0U) |
        (value.SpyChat ? 0x1000000U : 0U));

    public const byte SpaceshipStatusMask = 0x1f;

    public static SpaceshipStatus DecodeSpaceshipStatus(byte bits) => new()
    {
        Exists = (bits & 0x1) != 0,
        Launched = (bits & 0x2) != 0,
        Arrived = (bits & 0x4) != 0,
        FusionAtLaunch = (bits & 0x8) != 0,
        FirstArrivalVictory = (bits & 0x10) != 0
    };

    public static byte EncodeSpaceshipStatus(SpaceshipStatus value) => (byte)(
        (value.Exists ? 0x1U : 0U) |
        (value.Launched ? 0x2U : 0U) |
        (value.Arrived ? 0x4U : 0U) |
        (value.FusionAtLaunch ? 0x8U : 0U) |
        (value.FirstArrivalVictory ? 0x10U : 0U));

    public const byte RegionalPresenceMask = 0x7f;

    public static RegionalPresence DecodeRegionalPresence(byte bits) => new()
    {
        CitiesWithoutPeace = (bits & 0x1) != 0,
        CitiesWithPeace = (bits & 0x2) != 0,
        MilitaryWithoutPeace = (bits & 0x4) != 0,
        MilitaryWithPeace = (bits & 0x8) != 0,
        StrongerEnemy = (bits & 0x10) != 0,
        SettlementRestrictions = (byte)((bits >> 5) & 3)
    };

    public static byte EncodeRegionalPresence(RegionalPresence value) => (byte)(
        (value.CitiesWithoutPeace ? 0x1U : 0U) |
        (value.CitiesWithPeace ? 0x2U : 0U) |
        (value.MilitaryWithoutPeace ? 0x4U : 0U) |
        (value.MilitaryWithPeace ? 0x8U : 0U) |
        (value.StrongerEnemy ? 0x10U : 0U) |
        ((uint)(value.SettlementRestrictions & 3) << 5));

    public const ushort ScenarioOptionsMask = 0x807f;

    public static ScenarioOptions DecodeScenarioOptions(ushort bits) => new()
    {
        RevolutionRestriction = (bits & 0x1) != 0,
        ObjectiveVictory = (bits & 0x2) != 0,
        WonderObjectives = (bits & 0x4) != 0,
        WholeMapReveal = (bits & 0x8) != 0,
        GovernmentLock = (bits & 0x10) != 0,
        NoConquestTechnology = (bits & 0x20) != 0,
        NoPollution = (bits & 0x40) != 0,
        HistoricalAi = (bits & 0x8000) != 0
    };

    public static ushort EncodeScenarioOptions(ScenarioOptions value) => (ushort)(
        (value.RevolutionRestriction ? 0x1U : 0U) |
        (value.ObjectiveVictory ? 0x2U : 0U) |
        (value.WonderObjectives ? 0x4U : 0U) |
        (value.WholeMapReveal ? 0x8U : 0U) |
        (value.GovernmentLock ? 0x10U : 0U) |
        (value.NoConquestTechnology ? 0x20U : 0U) |
        (value.NoPollution ? 0x40U : 0U) |
        (value.HistoricalAi ? 0x8000U : 0U));

    public const ushort TurnStatusMask = 0x7;

    public static TurnStatus DecodeTurnStatus(ushort bits) => new()
    {
        EndOfTurn = (bits & 0x1) != 0,
        CityWindowOpened = (bits & 0x2) != 0,
        AttitudeDirty = (bits & 0x4) != 0
    };

    public static ushort EncodeTurnStatus(TurnStatus value) => (ushort)(
        (value.EndOfTurn ? 0x1U : 0U) |
        (value.CityWindowOpened ? 0x2U : 0U) |
        (value.AttitudeDirty ? 0x4U : 0U));

    public const ushort TutorialProgressMask = 0xf6;

    public static TutorialProgress DecodeTutorialProgress(ushort bits) => new()
    {
        Aircraft = (bits & 0x2) != 0,
        Ships = (bits & 0x4) != 0,
        Caravan = (bits & 0x10) != 0,
        Government = (bits & 0x20) != 0,
        Damage = (bits & 0x40) != 0,
        SecondaryDamage = (bits & 0x80) != 0
    };

    public static ushort EncodeTutorialProgress(TutorialProgress value) => (ushort)(
        (value.Aircraft ? 0x2U : 0U) |
        (value.Ships ? 0x4U : 0U) |
        (value.Caravan ? 0x10U : 0U) |
        (value.Government ? 0x20U : 0U) |
        (value.Damage ? 0x40U : 0U) |
        (value.SecondaryDamage ? 0x80U : 0U));

    public const ushort GameRulesMask = 0x8190;

    public static GameRules DecodeGameRules(ushort bits) => new()
    {
        NormalCombat = (bits & 0x10) != 0,
        Bloodlust = (bits & 0x80) != 0,
        NoRestart = (bits & 0x100) != 0,
        FlatWorld = (bits & 0x8000) != 0
    };

    public static ushort EncodeGameRules(GameRules value) => (ushort)(
        0x002fU |
        (value.NormalCombat ? 0x10U : 0U) |
        (value.Bloodlust ? 0x80U : 0U) |
        (value.NoRestart ? 0x100U : 0U) |
        (value.FlatWorld ? 0x8000U : 0U));

    public const uint GameOptionsMask = 0x3ffff8;

    public static GameOptions DecodeGameOptions(uint bits) => new()
    {
        Music = (bits & 0x8) != 0,
        Sound = (bits & 0x10) != 0,
        Grid = (bits & 0x20) != 0,
        EnterClosesCity = (bits & 0x40) != 0,
        MouseGoTo = (bits & 0x80) != 0,
        Tutorial = (bits & 0x100) != 0,
        Advice = (bits & 0x200) != 0,
        FastSlide = (bits & 0x400) != 0,
        SkipEnemyPauses = (bits & 0x800) != 0,
        EnemyMoves = (bits & 0x1000) != 0,
        Autosave = (bits & 0x2000) != 0,
        Wait = (bits & 0x4000) != 0,
        CheatMode = (bits & 0x8000) != 0,
        WonderMovies = (bits & 0x10000) != 0,
        Throne = (bits & 0x20000) != 0,
        Diplomacy = (bits & 0x40000) != 0,
        AdvanceCivilopedia = (bits & 0x80000) != 0,
        Council = (bits & 0x100000) != 0,
        Heralds = (bits & 0x200000) != 0
    };

    public static uint EncodeGameOptions(GameOptions value) => (uint)(
        (value.Music ? 0x8U : 0U) |
        (value.Sound ? 0x10U : 0U) |
        (value.Grid ? 0x20U : 0U) |
        (value.EnterClosesCity ? 0x40U : 0U) |
        (value.MouseGoTo ? 0x80U : 0U) |
        (value.Tutorial ? 0x100U : 0U) |
        (value.Advice ? 0x200U : 0U) |
        (value.FastSlide ? 0x400U : 0U) |
        (value.SkipEnemyPauses ? 0x800U : 0U) |
        (value.EnemyMoves ? 0x1000U : 0U) |
        (value.Autosave ? 0x2000U : 0U) |
        (value.Wait ? 0x4000U : 0U) |
        (value.CheatMode ? 0x8000U : 0U) |
        (value.WonderMovies ? 0x10000U : 0U) |
        (value.Throne ? 0x20000U : 0U) |
        (value.Diplomacy ? 0x40000U : 0U) |
        (value.AdvanceCivilopedia ? 0x80000U : 0U) |
        (value.Council ? 0x100000U : 0U) |
        (value.Heralds ? 0x200000U : 0U));

    public const ushort MessageSuppressionMask = 0x7ff;

    public static MessageSuppression DecodeMessageSuppression(ushort bits) => new()
    {
        GrowthHalted = (bits & 0x1) != 0,
        ImprovementBuilt = (bits & 0x2) != 0,
        UnitBuilt = (bits & 0x4) != 0,
        InvalidBuild = (bits & 0x8) != 0,
        Disorder = (bits & 0x10) != 0,
        OrderRestored = (bits & 0x20) != 0,
        Celebration = (bits & 0x40) != 0,
        FoodLow = (bits & 0x80) != 0,
        Pollution = (bits & 0x100) != 0,
        ProductionPenalty = (bits & 0x200) != 0,
        ZoomToCity = (bits & 0x400) != 0
    };

    public static ushort EncodeMessageSuppression(MessageSuppression value) => (ushort)(
        (value.GrowthHalted ? 0x1U : 0U) |
        (value.ImprovementBuilt ? 0x2U : 0U) |
        (value.UnitBuilt ? 0x4U : 0U) |
        (value.InvalidBuild ? 0x8U : 0U) |
        (value.Disorder ? 0x10U : 0U) |
        (value.OrderRestored ? 0x20U : 0U) |
        (value.Celebration ? 0x40U : 0U) |
        (value.FoodLow ? 0x80U : 0U) |
        (value.Pollution ? 0x100U : 0U) |
        (value.ProductionPenalty ? 0x200U : 0U) |
        (value.ZoomToCity ? 0x400U : 0U));

    public const uint EventActionsMask = 0x2fff;

    public static EventActions DecodeEventActions(uint bits) => new()
    {
        Text = (bits & 0x1) != 0,
        MoveUnit = (bits & 0x2) != 0,
        CreateUnit = (bits & 0x4) != 0,
        ChangeMoney = (bits & 0x8) != 0,
        PlayWaveFile = (bits & 0x10) != 0,
        MakeAggression = (bits & 0x20) != 0,
        JustOnce = (bits & 0x40) != 0,
        PlayCdTrack = (bits & 0x80) != 0,
        SuppressWonderMovies = (bits & 0x100) != 0,
        ChangeTerrain = (bits & 0x200) != 0,
        DestroyCivilization = (bits & 0x400) != 0,
        GiveTechnology = (bits & 0x800) != 0,
        Consumed = (bits & 0x2000) != 0
    };

    public static uint EncodeEventActions(EventActions value) => (uint)(
        (value.Text ? 0x1U : 0U) |
        (value.MoveUnit ? 0x2U : 0U) |
        (value.CreateUnit ? 0x4U : 0U) |
        (value.ChangeMoney ? 0x8U : 0U) |
        (value.PlayWaveFile ? 0x10U : 0U) |
        (value.MakeAggression ? 0x20U : 0U) |
        (value.JustOnce ? 0x40U : 0U) |
        (value.PlayCdTrack ? 0x80U : 0U) |
        (value.SuppressWonderMovies ? 0x100U : 0U) |
        (value.ChangeTerrain ? 0x200U : 0U) |
        (value.DestroyCivilization ? 0x400U : 0U) |
        (value.GiveTechnology ? 0x800U : 0U) |
        (value.Consumed ? 0x2000U : 0U));

    public const uint UnitAbilitiesMask = 0x7fff;

    public static UnitAbilities DecodeUnitAbilities(uint bits) => new()
    {
        SeeTwo = (bits & 0x1) != 0,
        IgnoreZonesOfControl = (bits & 0x2) != 0,
        Amphibious = (bits & 0x4) != 0,
        Submarine = (bits & 0x8) != 0,
        AttackAir = (bits & 0x10) != 0,
        CoastBound = (bits & 0x20) != 0,
        IgnoreWalls = (bits & 0x40) != 0,
        CarryAircraft = (bits & 0x80) != 0,
        Paradrop = (bits & 0x100) != 0,
        Alpine = (bits & 0x200) != 0,
        AntiHorseDefense = (bits & 0x400) != 0,
        FundamentalismFreeSupport = (bits & 0x800) != 0,
        DestroyedAfterAttack = (bits & 0x1000) != 0,
        AntiAirDefense = (bits & 0x2000) != 0,
        DetectSubmarines = (bits & 0x4000) != 0
    };

    public static uint EncodeUnitAbilities(UnitAbilities value) => (uint)(
        (value.SeeTwo ? 0x1U : 0U) |
        (value.IgnoreZonesOfControl ? 0x2U : 0U) |
        (value.Amphibious ? 0x4U : 0U) |
        (value.Submarine ? 0x8U : 0U) |
        (value.AttackAir ? 0x10U : 0U) |
        (value.CoastBound ? 0x20U : 0U) |
        (value.IgnoreWalls ? 0x40U : 0U) |
        (value.CarryAircraft ? 0x80U : 0U) |
        (value.Paradrop ? 0x100U : 0U) |
        (value.Alpine ? 0x200U : 0U) |
        (value.AntiHorseDefense ? 0x400U : 0U) |
        (value.FundamentalismFreeSupport ? 0x800U : 0U) |
        (value.DestroyedAfterAttack ? 0x1000U : 0U) |
        (value.AntiAirDefense ? 0x2000U : 0U) |
        (value.DetectSubmarines ? 0x4000U : 0U));

}
