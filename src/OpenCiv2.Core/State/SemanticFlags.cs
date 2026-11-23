namespace OpenCiv2.Core.State;

/// <summary>F02 Appendix A: logical settings; unassigned stored bits belong to adapters.</summary>
public record struct UnitStatus
{
    public bool Scratch;
    public bool SpyMission;
    public bool BorderIntruder;
    public bool Retreating;
    public bool Paradropped;
    public bool BorderWarned;
    public bool Moved;
    public bool GoalReached;
    public bool Garrison;
    public bool Colonist;
    public bool Aggressive;
    public bool CostsShield;
    public bool Aboard;
    public bool Veteran;
    public bool Waiting;
    public bool Automated;
}

/// <summary>F02 Appendix A: logical settings; unassigned stored bits belong to adapters.</summary>
public record struct CityStatus
{
    public bool Disorder;
    public bool Celebrating;
    public bool ImprovementSold;
    public bool TechnologyStolen;
    public bool Autobuild;
    public bool DefenderLost;
    public bool SettlerNearby;
    public bool Coastal;
    public bool WonderBuilt;
    public bool FerryPort;
    public bool FreshPort;
    public bool HydroPossible;
    public bool HadCaravan;
    public bool SecondDisorderTurn;
    public bool MoodOnEdge;
    public bool SpecialistHeld;
    public bool Airlifted;
    public bool TradeDirty;
    public bool SamScoringInput;
    public bool TerrainWorkGoal;
    public bool LongDisorder;
    public bool OceanAccess;
    public bool Investigated;
    public bool UnitsOnlyAutomation;
    public bool ImprovementsOnlyAutomation;
    public bool Objective;
    public bool CoastalFortressUsed;
}

/// <summary>F02 Appendix A: logical settings; unassigned stored bits belong to adapters.</summary>
public record struct CivilizationStatus
{
    public bool Revolution;
    public bool AtWar;
    public bool SenateActive;
    public bool GovernmentChanged;
    public bool EiffelApplied;
    public bool PendingFreeAdvance;
    public bool TradedTechnology;
    public bool HasAllied;
    public bool FemaleLeader;
}

/// <summary>F02 Appendix A / V04: logical settings; unassigned stored bits belong to adapters.</summary>
public record struct Relationship
{
    public bool Contact;
    public bool Ceasefire;
    public bool Peace;
    public bool Alliance;
    public bool Vendetta;
    public bool HostileIntent;
    public bool BrokenTreaty;
    public bool Embassy;
    public bool NuclearThreat;
    public bool Fighting;
    public bool RecentContact;
    public bool Aggressor;
    public bool SneakAttack;
    public bool War;
    public bool ChangedThisTurn;
    public bool Incident;
    public bool Nuked;
    public bool Tribute;
    public bool ExtendedAggressor;
    public bool Mercenary;
    public bool WarThisTurn;
    public bool CityCaptured;
    public bool MetThisTurn;
    public bool SpyChat;
}

/// <summary>F02 Appendix A: logical settings; unassigned stored bits belong to adapters.</summary>
public record struct SpaceshipStatus
{
    public bool Exists;
    public bool Launched;
    public bool Arrived;
    public bool FusionAtLaunch;
    public bool FirstArrivalVictory;
}

/// <summary>A01 Appendix A: logical settings; unassigned stored bits belong to adapters.</summary>
public record struct RegionalPresence
{
    public bool CitiesWithoutPeace;
    public bool CitiesWithPeace;
    public bool MilitaryWithoutPeace;
    public bool MilitaryWithPeace;
    public bool StrongerEnemy;
    // W06 settlement override consumes these two additional retained regional restrictions.
    // F06 maps their independent values to the remaining low-seven flag components.
    public byte SettlementRestrictions;
}

/// <summary>S01 Appendix A: logical settings; unassigned stored bits belong to adapters.</summary>
public record struct ScenarioOptions
{
    public bool RevolutionRestriction;
    public bool ObjectiveVictory;
    public bool WonderObjectives;
    public bool WholeMapReveal;
    public bool GovernmentLock;
    public bool NoConquestTechnology;
    public bool NoPollution;
    public bool HistoricalAi;
}

/// <summary>F04 Appendix A: logical settings; unassigned stored bits belong to adapters.</summary>
public record struct TurnStatus
{
    public bool EndOfTurn;
    public bool CityWindowOpened;
    public bool AttitudeDirty;
}

/// <summary>U01/U04/V02 encoding appendices: logical settings; unassigned stored bits belong to adapters.</summary>
public record struct TutorialProgress
{
    public bool Aircraft;
    public bool Ships;
    public bool Caravan;
    public bool Government;
    public bool Damage;
    public bool SecondaryDamage;
}

/// <summary>F03 §8 / configuration encodings: logical settings; unassigned stored bits belong to adapters.</summary>
public record struct GameRules
{
    public bool NormalCombat;
    public bool Bloodlust;
    public bool NoRestart;
    public bool FlatWorld;

    public static GameRules Normal => new() { NormalCombat = true };
}

/// <summary>F03 §8 / F06 §2: logical settings; unassigned stored bits belong to adapters.</summary>
public record struct GameOptions
{
    public bool Music;
    public bool Sound;
    public bool Grid;
    public bool EnterClosesCity;
    public bool MouseGoTo;
    public bool Tutorial;
    public bool Advice;
    public bool FastSlide;
    public bool SkipEnemyPauses;
    public bool EnemyMoves;
    public bool Autosave;
    public bool Wait;
    public bool CheatMode;
    public bool WonderMovies;
    public bool Throne;
    public bool Diplomacy;
    public bool AdvanceCivilopedia;
    public bool Council;
    public bool Heralds;
}

/// <summary>F03 §8: logical settings; unassigned stored bits belong to adapters.</summary>
public record struct MessageSuppression
{
    public bool GrowthHalted;
    public bool ImprovementBuilt;
    public bool UnitBuilt;
    public bool InvalidBuild;
    public bool Disorder;
    public bool OrderRestored;
    public bool Celebration;
    public bool FoodLow;
    public bool Pollution;
    public bool ProductionPenalty;
    public bool ZoomToCity;
}

/// <summary>S02 Appendix A: logical settings; unassigned stored bits belong to adapters.</summary>
public record struct EventActions
{
    public bool Text;
    public bool MoveUnit;
    public bool CreateUnit;
    public bool ChangeMoney;
    public bool PlayWaveFile;
    public bool MakeAggression;
    public bool JustOnce;
    public bool PlayCdTrack;
    public bool SuppressWonderMovies;
    public bool ChangeTerrain;
    public bool DestroyCivilization;
    public bool GiveTechnology;
    public bool Consumed;
}

/// <summary>F03 / content data format §4: logical settings; unassigned stored bits belong to adapters.</summary>
public record struct UnitAbilities
{
    public bool SeeTwo;
    public bool IgnoreZonesOfControl;
    public bool Amphibious;
    public bool Submarine;
    public bool AttackAir;
    public bool CoastBound;
    public bool IgnoreWalls;
    public bool CarryAircraft;
    public bool Paradrop;
    public bool Alpine;
    public bool AntiHorseDefense;
    public bool FundamentalismFreeSupport;
    public bool DestroyedAfterAttack;
    public bool AntiAirDefense;
    public bool DetectSubmarines;
}
