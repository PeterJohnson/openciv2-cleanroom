using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;

namespace OpenCiv2.Formats;

// F06 §§3–4 stored fields. Explicit widths; no live state or struct-layout serialization.
public sealed record StoredGlobals
{
    public ushort Rules { get; init; } = (ushort)0;
    public uint GameOptions { get; init; } = (uint)0;
    public ushort TransientFlags { get; init; } = (ushort)0;
    public ushort GameFlags { get; init; } = (ushort)0;
    public ushort MessageOptions { get; init; } = (ushort)0;
    public ushort ShownEventFlags { get; init; } = (ushort)0;
    public ushort Retained0e { get; init; } = (ushort)0;
    public short Turn { get; init; } = (short)0;
    public short Year { get; init; } = (short)0;
    public short FirstLaunchTurn { get; init; } = (short)0;
    public short ActiveUnit { get; init; } = (short)-1;
    public short MovingUnit { get; init; } = (short)-1;
    public byte Multiplayer { get; init; } = (byte)0;
    public sbyte PlayerCiv { get; init; } = (sbyte)0;
    public sbyte SavedCurrentCiv { get; init; } = (sbyte)0;
    public sbyte TurnCiv { get; init; } = (sbyte)0;
    public sbyte ViewCiv { get; init; } = (sbyte)0;
    public byte RevealMap { get; init; } = (byte)0;
    public byte Difficulty { get; init; } = (byte)0;
    public byte BarbarianActivity { get; init; } = (byte)0;
    public byte CivsAlive { get; init; } = (byte)0;
    public byte HumanPlayers { get; init; } = (byte)0;
    public byte CivsAtStart { get; init; } = (byte)0;
    public byte NumEnemies { get; init; } = (byte)0;
    public sbyte WarmingCounter { get; init; } = (sbyte)0;
    public sbyte GlobalWarmings { get; init; } = (sbyte)0;
    public short WarmingPollution { get; init; } = (short)0;
    public short PollutedTiles { get; init; } = (short)0;
    public short PeaceTurns { get; init; } = (short)0;
    public short UnitEnd { get; init; } = (short)0;
    public short CityEnd { get; init; } = (short)0;
    public short NumAdvances { get; init; } = (short)0;
    public short Retained34 { get; init; } = (short)0;
    public OwnedList<byte> FirstDiscoverer { get; init; } = new(Enumerable.Range(0, 100).Select(_ => (byte)0));
    public OwnedList<byte> AdvanceKnownBy { get; init; } = new(Enumerable.Range(0, 100).Select(_ => (byte)0));
    public OwnedList<short> WonderCity { get; init; } = new(Enumerable.Range(0, 28).Select(_ => (short)0));
    public short NextHistoryTurn { get; init; } = (short)0;
    public byte BestHumanRank { get; init; } = (byte)0;
    public byte WorstHumanRank { get; init; } = (byte)0;
    public OwnedList<byte> PowerRank { get; init; } = new(Enumerable.Range(0, 8).Select(_ => (byte)0));
    public OwnedList<byte> CivByRank { get; init; } = new(Enumerable.Range(0, 8).Select(_ => (byte)0));
}

public sealed record StoredCivilization
{
    public ushort Flags { get; init; } = (ushort)0;
    public int Gold { get; init; } = (int)0;
    public short Tribe { get; init; } = (short)0;
    public short ResearchProgress { get; init; } = (short)0;
    public short Researching { get; init; } = (short)0;
    public short CapitalX { get; init; } = (short)0;
    public short LastCityTurn { get; init; } = (short)0;
    public byte NumAdvances { get; init; } = (byte)0;
    public byte FutureTechs { get; init; } = (byte)0;
    public byte ResearchCostAdvances { get; init; } = (byte)0;
    public byte ScienceRate { get; init; } = (byte)0;
    public byte TaxRate { get; init; } = (byte)0;
    public byte Government { get; init; } = (byte)0;
    public byte DiploRandom { get; init; } = (byte)0;
    public OwnedList<byte> WondersByGroup { get; init; } = new(Enumerable.Range(0, 4).Select(_ => (byte)0));
    public byte Retained1b { get; init; } = (byte)0;
    public short SubversionCount { get; init; } = (short)0;
    public byte Reputation { get; init; } = (byte)0;
    public sbyte DiploRequestCount { get; init; } = (sbyte)0;
    public OwnedList<uint> Treaties { get; init; } = new(Enumerable.Range(0, 8).Select(_ => (uint)0));
    public OwnedList<byte> Attitude { get; init; } = new(Enumerable.Range(0, 8).Select(_ => (byte)0));
    public OwnedList<sbyte> ReputationCredit { get; init; } = new(Enumerable.Range(0, 8).Select(_ => (sbyte)0));
    public OwnedList<byte> AggressionCounter { get; init; } = new(Enumerable.Range(0, 8).Select(_ => (byte)0));
    public OwnedList<byte> Advances { get; init; } = new(Enumerable.Range(0, 13).Select(_ => (byte)0));
    public byte Retained65 { get; init; } = (byte)0;
    public short MilitaryCount { get; init; } = (short)0;
    public short CityCount { get; init; } = (short)0;
    public short ShipCount { get; init; } = (short)0;
    public short TotalCitySize { get; init; } = (short)0;
    public ushort MilitaryStrength { get; init; } = (ushort)0;
    public short TotalAttack { get; init; } = (short)0;
    public short PopulationMilestone { get; init; } = (short)0;
    public OwnedList<byte> AdvanceSource { get; init; } = new(Enumerable.Range(0, 100).Select(_ => (byte)0));
    public OwnedList<byte> UnitsActive { get; init; } = new(Enumerable.Range(0, 62).Select(_ => (byte)0));
    public OwnedList<byte> UnitsLost { get; init; } = new(Enumerable.Range(0, 62).Select(_ => (byte)0));
    public OwnedList<byte> UnitsInProduction { get; init; } = new(Enumerable.Range(0, 62).Select(_ => (byte)0));
    public OwnedList<ushort> ContinentMilitary { get; init; } = new(Enumerable.Range(0, 64).Select(_ => (ushort)0));
    public OwnedList<ushort> ContinentAttack { get; init; } = new(Enumerable.Range(0, 64).Select(_ => (ushort)0));
    public OwnedList<byte> ContinentCities { get; init; } = new(Enumerable.Range(0, 64).Select(_ => (byte)0));
    public OwnedList<byte> ContinentCitySize { get; init; } = new(Enumerable.Range(0, 64).Select(_ => (byte)0));
    public OwnedList<byte> ContinentTradeUnits { get; init; } = new(Enumerable.Range(0, 64).Select(_ => (byte)0));
    public OwnedList<byte> ContinentFlags { get; init; } = new(Enumerable.Range(0, 64).Select(_ => (byte)0));
    public OwnedList<byte> ContinentPolicy { get; init; } = new(Enumerable.Range(0, 64).Select(_ => (byte)0));
    public short BestShieldSurplus { get; init; } = (short)0;
    public OwnedList<short> GovernmentScore { get; init; } = new(Enumerable.Range(0, 7).Select(_ => (short)0));
    public OwnedList<short> LastContactTurn { get; init; } = new(Enumerable.Range(0, 8).Select(_ => (short)0));
    public byte LeaderPortrait { get; init; } = (byte)0;
    public OwnedList<byte> ThroneRoom { get; init; } = new(Enumerable.Range(0, 10).Select(_ => (byte)0));
    public byte Retained3fd { get; init; } = (byte)0;
    public short ThroneRoomUpgrades { get; init; } = (short)0;
    public byte SpaceshipFlags { get; init; } = (byte)0;
    public byte Retained401 { get; init; } = (byte)0;
    public short SpaceshipArrivalYear { get; init; } = (short)0;
    public short SpaceshipLaunchYear { get; init; } = (short)0;
    public short SpaceshipPopulation { get; init; } = (short)0;
    public OwnedList<short> SpaceshipParts { get; init; } = new(Enumerable.Range(0, 6).Select(_ => (short)0));
    public OwnedList<CivilizationGoal> Goals { get; init; } = new(Enumerable.Range(0, 48).Select(_ => new CivilizationGoal()));
    public OwnedList<CivilizationGoal> StandingGoals { get; init; } = new(Enumerable.Range(0, 16).Select(_ => new CivilizationGoal()));
}

public sealed record StoredCivilizationNames
{
    public short CityStyle { get; init; } = (short)0;
    public OwnedList<byte> Leader { get; init; } = new(new byte[24]);
    public OwnedList<byte> Tribe { get; init; } = new(new byte[24]);
    public OwnedList<byte> Adjective { get; init; } = new(new byte[24]);
    public OwnedList<OwnedList<byte>> Titles { get; init; } = new(Enumerable.Range(0, 7).Select(_ => new OwnedList<byte>(new byte[24])));
}

public sealed record StoredLeader
{
    public sbyte Attack { get; init; } = (sbyte)0;
    public sbyte Expand { get; init; } = (sbyte)0;
    public sbyte Civilize { get; init; } = (sbyte)0;
    public byte TimesUsed { get; init; } = (byte)0;
    public byte Female { get; init; } = (byte)0;
    public byte CityNameIndex { get; init; } = (byte)0;
    public short Color { get; init; } = (short)0;
    public short CityStyle { get; init; } = (short)0;
    public short LeaderName { get; init; } = (short)0;
    public short Plural { get; init; } = (short)0;
    public short Adjective { get; init; } = (short)0;
    public OwnedList<short> Names { get; init; } = new(Enumerable.Range(0, 2).Select(_ => (short)0));
    public OwnedList<OwnedList<short>> Titles { get; init; } = new(Enumerable.Range(0, 7).Select(_ => new OwnedList<short>(Enumerable.Range(0, 2).Select(_ => (short)0))));
}

public sealed record SavedRectangle
{
    public short Left { get; init; } = (short)0;
    public short Top { get; init; } = (short)0;
    public short Right { get; init; } = (short)0;
    public short Bottom { get; init; } = (short)0;
}

public sealed record SavedLayout
{
    public short Valid { get; init; } = (short)0;
    public OwnedList<SavedRectangle> Rectangles { get; init; } = new(Enumerable.Range(0, 13).Select(_ => new SavedRectangle()));
}

public sealed record SavedMapView
{
    public short Open { get; init; } = (short)0;
    public short TrackMoves { get; init; } = (short)0;
    public short X { get; init; } = (short)0;
    public short Y { get; init; } = (short)0;
    public short Zoom { get; init; } = (short)0;
}

public sealed record ScenarioParameters
{
    public ushort Flags { get; init; } = (ushort)0;
    public OwnedList<byte> Name { get; init; } = new(new byte[80]);
    public short TechnologyParadigm { get; init; } = (short)0;
    public short YearIncrement { get; init; } = (short)0;
    public short StartYear { get; init; } = (short)0;
    public short MaximumTurns { get; init; } = (short)0;
    public short ObjectiveCiv { get; init; } = (short)0;
    public OwnedList<short> VictoryThresholds { get; init; } = new(Enumerable.Range(0, 4).Select(_ => (short)0));
}

public sealed record EliminationHistory
{
    public short Count { get; init; } = (short)0;
    public OwnedList<short> Turns { get; init; } = new(Enumerable.Range(0, 12).Select(_ => (short)0));
    public OwnedList<byte> Killers { get; init; } = new(Enumerable.Range(0, 12).Select(_ => (byte)0));
    public OwnedList<byte> Portraits { get; init; } = new(Enumerable.Range(0, 12).Select(_ => (byte)0));
    public OwnedList<OwnedList<byte>> Names { get; init; } = new(Enumerable.Range(0, 12).Select(_ => new OwnedList<byte>(new byte[24])));
}

public sealed record MultiplayerRecord
{
    public OwnedList<short> CenterX { get; init; } = new(Enumerable.Range(0, 8).Select(_ => (short)0));
    public OwnedList<short> CenterY { get; init; } = new(Enumerable.Range(0, 8).Select(_ => (short)0));
    public OwnedList<short> Introduced { get; init; } = new(Enumerable.Range(0, 8).Select(_ => (short)0));
    public int TurnTimeLimit { get; init; } = (int)0;
    public OwnedList<byte> EncodedPasswords { get; init; } = new(Enumerable.Range(0, 256).Select(_ => (byte)0));
    public short OpenGame { get; init; } = (short)0;
    public short KillOnRetirement { get; init; } = (short)0;
    public short ShowHumanStarts { get; init; } = (short)0;
    public short ChatFromStart { get; init; } = (short)0;
    public short Simultaneous { get; init; } = (short)0;
    public int CheatTurnTime { get; init; } = (int)0;
    public OwnedList<byte> Retained142 { get; init; } = new(Enumerable.Range(0, 290).Select(_ => (byte)0));
    public OwnedList<OwnedList<byte>> Emails { get; init; } = new(Enumerable.Range(0, 8).Select(_ => new OwnedList<byte>(Enumerable.Range(0, 32).Select(_ => (byte)0))));
    public OwnedList<OwnedList<byte>> PlayerSetup { get; init; } = new(Enumerable.Range(0, 8).Select(_ => new OwnedList<byte>(Enumerable.Range(0, 32).Select(_ => (byte)0))));
    public byte ResumeCiv { get; init; } = (byte)0;
    public byte Retained465 { get; init; } = (byte)0;
    public short ResumeEvent { get; init; } = (short)0;
    public short AutoplayActive { get; init; } = (short)0;
    public short AutoplayTurns { get; init; } = (short)0;
    public short DoubleProduction { get; init; } = (short)0;
    public short DoubleMovement { get; init; } = (short)0;
    public short ClaimedHumans { get; init; } = (short)0;
    public OwnedList<byte> Retained472 { get; init; } = new(Enumerable.Range(0, 34).Select(_ => (byte)0));
}

public sealed record EventRecord
{
    public uint Triggers { get; init; } = (uint)0;
    public uint Actions { get; init; } = (uint)0;
    public uint UnitName { get; init; } = (uint)0;
    public int UnitType { get; init; } = (int)0;
    public uint CityName { get; init; } = (uint)0;
    public uint WhoName { get; init; } = (uint)0;
    public int WhoCiv { get; init; } = (int)0;
    public int TalkerType { get; init; } = (int)0;
    public uint WhomName { get; init; } = (uint)0;
    public int WhomCiv { get; init; } = (int)0;
    public int ListenerType { get; init; } = (int)0;
    public int Turn { get; init; } = (int)0;
    public int Denominator { get; init; } = (int)0;
    public int Technology { get; init; } = (int)0;
    public OwnedList<uint> Text { get; init; } = new(Enumerable.Range(0, 20).Select(_ => (uint)0));
    public uint MoveOwnerName { get; init; } = (uint)0;
    public int MoveOwnerCiv { get; init; } = (int)0;
    public uint MoveUnitName { get; init; } = (uint)0;
    public int MoveUnitType { get; init; } = (int)0;
    public int MoveCount { get; init; } = (int)0;
    public OwnedList<int> MoveRectangle { get; init; } = new(Enumerable.Range(0, 8).Select(_ => (int)0));
    public int MoveX { get; init; } = (int)0;
    public int MoveY { get; init; } = (int)0;
    public uint AggressionWhomName { get; init; } = (uint)0;
    public int AggressionWhomCiv { get; init; } = (int)0;
    public uint AggressionWhoName { get; init; } = (uint)0;
    public int AggressionWhoCiv { get; init; } = (int)0;
    public uint CreateOwnerName { get; init; } = (uint)0;
    public int CreateOwnerCiv { get; init; } = (int)0;
    public uint CreateUnitName { get; init; } = (uint)0;
    public int CreateUnitType { get; init; } = (int)0;
    public OwnedList<int> CreateLocations { get; init; } = new(Enumerable.Range(0, 20).Select(_ => (int)0));
    public int CreateLocationCount { get; init; } = (int)0;
    public int CreateVeteran { get; init; } = (int)0;
    public uint CreateHomecity { get; init; } = (uint)0;
    public uint AuxiliaryCivName { get; init; } = (uint)0;
    public int AuxiliaryScalar { get; init; } = (int)0;
    public uint AuxiliaryUnitName { get; init; } = (uint)0;
    public OwnedList<byte> Retained { get; init; } = new(Enumerable.Range(0, 40).Select(_ => (byte)0));
    public uint MoneyReceiverName { get; init; } = (uint)0;
    public int MoneyReceiverCiv { get; init; } = (int)0;
    public int MoneyAmount { get; init; } = (int)0;
    public int NegotiationFlag { get; init; } = (int)0;
    public uint WaveFile { get; init; } = (uint)0;
    public int CdTrack { get; init; } = (int)0;
    public int TerrainType { get; init; } = (int)0;
    public OwnedList<int> TerrainRectangle { get; init; } = new(Enumerable.Range(0, 8).Select(_ => (int)0));
    public int DestroyCiv { get; init; } = (int)0;
    public int GiveTech { get; init; } = (int)0;
    public int GiveTechCiv { get; init; } = (int)0;
}

public sealed record StoredUnit
{
    public short X { get; init; } = (short)0;
    public short Y { get; init; } = (short)0;
    public ushort Flags { get; init; } = (ushort)0;
    public UnitType Type { get; init; } = new UnitType(0);
    public CivilizationSlot Owner { get; init; } = new CivilizationSlot(0);
    public byte MovesUsed { get; init; } = (byte)0;
    public byte Visibility { get; init; } = (byte)0;
    public byte Damage { get; init; } = (byte)0;
    public byte LastDirection { get; init; } = (byte)0;
    public byte AiTask { get; init; } = (byte)0;
    public byte Counter { get; init; } = (byte)0;
    public byte AuxiliaryTimer { get; init; } = (byte)0;
    public UnitOrder Orders { get; init; } = UnitOrder.None;
    public CitySlot? Home { get; init; } = null;
    public byte RetainedByte { get; init; } = (byte)0;
    public short DestinationX { get; init; } = (short)0;
    public short DestinationY { get; init; } = (short)0;
    public short Previous { get; init; } = (short)-1;
    public short Next { get; init; } = (short)-1;
    public UnitToken Token { get; init; } = new UnitToken(0);
    public ushort RetainedWord { get; init; } = (ushort)0;
}

public sealed record StoredCity
{
    public short X { get; init; } = (short)0;
    public short Y { get; init; } = (short)0;
    public uint Flags { get; init; } = (uint)0;
    public CivilizationSlot Owner { get; init; } = new CivilizationSlot(0);
    public sbyte Size { get; init; } = (sbyte)0;
    public CivilizationSlot Founder { get; init; } = new CivilizationSlot(0);
    public byte TurnsCaptured { get; init; } = (byte)0;
    public byte KnownTo { get; init; } = (byte)0;
    public OwnedList<byte> RememberedSize { get; init; } = new(Enumerable.Range(0, 8).Select(_ => (byte)0));
    public byte RetainedByte { get; init; } = (byte)0;
    public uint SpecialistKinds { get; init; } = (uint)0;
    public short FoodBox { get; init; } = (short)0;
    public short ShieldBox { get; init; } = (short)0;
    public short BaseTrade { get; init; } = (short)0;
    public OwnedList<byte> Name { get; init; } = new(new byte[16]);
    public uint WorkedTiles { get; init; } = (uint)0;
    public OwnedList<byte> Improvements { get; init; } = new(Enumerable.Range(0, 5).Select(_ => (byte)0));
    public sbyte Production { get; init; } = (sbyte)0;
    public byte RouteCount { get; init; } = (byte)0;
    public OwnedList<sbyte> Supplies { get; init; } = new(Enumerable.Range(0, 3).Select(_ => (sbyte)0));
    public OwnedList<sbyte> Demands { get; init; } = new(Enumerable.Range(0, 3).Select(_ => (sbyte)0));
    public OwnedList<sbyte> RouteGoods { get; init; } = new(Enumerable.Range(0, 3).Select(_ => (sbyte)0));
    public OwnedList<short> RoutePartners { get; init; } = new(Enumerable.Range(0, 3).Select(_ => (short)-1));
    public short Science { get; init; } = (short)0;
    public short Tax { get; init; } = (short)0;
    public short Trade { get; init; } = (short)0;
    public byte TotalFood { get; init; } = (byte)0;
    public byte TotalShields { get; init; } = (byte)0;
    public byte Happy { get; init; } = (byte)0;
    public byte Unhappy { get; init; } = (byte)0;
    public CityToken Token { get; init; } = new CityToken(0);
}

public sealed record MapHeader
{
    public short Width { get; init; } = (short)0;
    public short Height { get; init; } = (short)0;
    public short Area { get; init; } = (short)0;
    public ushort Topology { get; init; } = (ushort)0;
    public ushort Seed { get; init; } = (ushort)0;
    public short CoarseWidth { get; init; } = (short)0;
    public short CoarseHeight { get; init; } = (short)0;
}
