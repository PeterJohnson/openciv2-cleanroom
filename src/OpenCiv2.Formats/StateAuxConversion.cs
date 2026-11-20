using OpenCiv2.Base;
using OpenCiv2.Core.State;
using RuntimeEliminations = OpenCiv2.Core.State.EliminationHistory;
using RuntimeScenario = OpenCiv2.Core.State.ScenarioParameters;

namespace OpenCiv2.Formats;

public static partial class StateConversion
{
    private static OwnedList<OwnedList<byte>> TextFields(
        OwnedList<OwnedList<byte>> source,
        int count,
        int width,
        bool stored)
    {
        if (source.Count != count)
        {
            throw new ArgumentException("Text field group has an incorrect extent", nameof(source));
        }

        return new(source.Select(text => stored ? StoredText(text, width) : LiveText(text, width)));
    }

    public static CivilizationNames ToLive(StoredCivilizationNames value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var result = new CivilizationNames();
        result.CityStyle = value.CityStyle;
        result.Leader = LiveText(value.Leader, 24);
        result.Tribe = LiveText(value.Tribe, 24);
        result.Adjective = LiveText(value.Adjective, 24);
        result.Titles = TextFields(value.Titles, 7, 24, false);
        return result;
    }

    public static StoredCivilizationNames ToStored(in CivilizationNames value) => new()
    {
        CityStyle = value.CityStyle,
        Leader = StoredText(value.Leader, 24),
        Tribe = StoredText(value.Tribe, 24),
        Adjective = StoredText(value.Adjective, 24),
        Titles = TextFields(value.Titles, 7, 24, true),
    };

    public static RuntimeScenario ToLive(ScenarioParameters value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var result = new RuntimeScenario();
        result.Flags = value.Flags;
        result.Name = LiveText(value.Name, 80);
        result.TechnologyParadigm = value.TechnologyParadigm;
        result.YearIncrement = value.YearIncrement;
        result.StartYear = value.StartYear;
        result.MaximumTurns = value.MaximumTurns;
        result.ObjectiveCiv = value.ObjectiveCiv;
        Copy(value.VictoryThresholds, result.VictoryThresholds);
        return result;
    }

    public static ScenarioParameters ToStored(in RuntimeScenario value) => new()
    {
        Flags = value.Flags,
        Name = StoredText(value.Name, 80),
        TechnologyParadigm = value.TechnologyParadigm,
        YearIncrement = value.YearIncrement,
        StartYear = value.StartYear,
        MaximumTurns = value.MaximumTurns,
        ObjectiveCiv = value.ObjectiveCiv,
        VictoryThresholds = new(((ReadOnlySpan<short>)value.VictoryThresholds).ToArray()),
    };

    public static RuntimeEliminations ToLive(EliminationHistory value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var result = new RuntimeEliminations();
        result.Count = value.Count;
        Copy(value.Turns, result.Turns);
        Copy(value.Killers, result.Killers);
        Copy(value.Portraits, result.Portraits);
        result.Names = TextFields(value.Names, 12, 24, false);
        return result;
    }

    public static EliminationHistory ToStored(in RuntimeEliminations value) => new()
    {
        Count = value.Count,
        Turns = new(((ReadOnlySpan<short>)value.Turns).ToArray()),
        Killers = new(((ReadOnlySpan<byte>)value.Killers).ToArray()),
        Portraits = new(((ReadOnlySpan<byte>)value.Portraits).ToArray()),
        Names = TextFields(value.Names, 12, 24, true),
    };

    public static MultiplayerValues ToLive(MultiplayerRecord value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var result = new MultiplayerValues();
        Copy(value.CenterX, result.CenterX);
        Copy(value.CenterY, result.CenterY);
        if (value.Introduced.Count != 8)
        {
            throw new ArgumentException("Introductions require eight flags", nameof(value));
        }

        for (int i = 0; i < 8; ++i)
        {
            result.Introduced[i] = value.Introduced[i] != 0;
        }

        result.TurnTimeLimit = value.TurnTimeLimit;
        Copy(value.EncodedPasswords, result.EncodedPasswords);
        result.OpenGame = value.OpenGame != 0;
        result.KillOnRetirement = value.KillOnRetirement != 0;
        result.ShowHumanStarts = value.ShowHumanStarts != 0;
        result.ChatFromStart = value.ChatFromStart != 0;
        result.Simultaneous = value.Simultaneous != 0;
        result.CheatTurnTime = value.CheatTurnTime;
        result.Emails = TextFields(value.Emails, 8, 32, false);
        result.ResumeCiv = value.ResumeCiv;
        result.ResumeEvent = value.ResumeEvent;
        result.AutoplayActive = value.AutoplayActive != 0;
        result.AutoplayTurns = value.AutoplayTurns;
        result.DoubleProduction = value.DoubleProduction != 0;
        result.DoubleMovement = value.DoubleMovement != 0;
        result.ClaimedHumans = value.ClaimedHumans;
        return result;
    }

    public static MultiplayerRecord ToStored(in MultiplayerValues value) => new()
    {
        CenterX = new(((ReadOnlySpan<short>)value.CenterX).ToArray()),
        CenterY = new(((ReadOnlySpan<short>)value.CenterY).ToArray()),
        Introduced = new(((ReadOnlySpan<bool>)value.Introduced).ToArray().Select(flag => flag ? (short)1 : (short)0)),
        TurnTimeLimit = value.TurnTimeLimit,
        EncodedPasswords = new(((ReadOnlySpan<byte>)value.EncodedPasswords).ToArray()),
        OpenGame = value.OpenGame ? (short)1 : (short)0,
        KillOnRetirement = value.KillOnRetirement ? (short)1 : (short)0,
        ShowHumanStarts = value.ShowHumanStarts ? (short)1 : (short)0,
        ChatFromStart = value.ChatFromStart ? (short)1 : (short)0,
        Simultaneous = value.Simultaneous ? (short)1 : (short)0,
        CheatTurnTime = value.CheatTurnTime,
        Emails = TextFields(value.Emails, 8, 32, true),
        ResumeCiv = value.ResumeCiv,
        ResumeEvent = value.ResumeEvent,
        AutoplayActive = value.AutoplayActive ? (short)1 : (short)0,
        AutoplayTurns = value.AutoplayTurns,
        DoubleProduction = value.DoubleProduction ? (short)1 : (short)0,
        DoubleMovement = value.DoubleMovement ? (short)1 : (short)0,
        ClaimedHumans = value.ClaimedHumans,
    };

    public static MultiplayerRecord PreserveOpaque(MultiplayerRecord output, MultiplayerRecord imported) => output with
    {
        Retained142 = imported.Retained142,
        PlayerSetup = imported.PlayerSetup,
        Retained465 = imported.Retained465,
        Retained472 = imported.Retained472,
    };

    public static MapView ToLive(SavedMapView value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var result = new MapView();
        result.Open = value.Open != 0;
        result.TrackMoves = value.TrackMoves != 0;
        result.X = value.X;
        result.Y = value.Y;
        result.Zoom = value.Zoom;
        return result;
    }

    public static SavedMapView ToStored(in MapView value) => new()
    {
        Open = value.Open ? (short)1 : (short)0,
        TrackMoves = value.TrackMoves ? (short)1 : (short)0,
        X = value.X,
        Y = value.Y,
        Zoom = value.Zoom,
    };

    public static EventValues ToLive(EventRecord value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var result = new EventValues();
        result.Triggers = value.Triggers;
        result.Actions = value.Actions;
        result.UnitType = value.UnitType;
        result.WhoCiv = value.WhoCiv;
        result.TalkerType = value.TalkerType;
        result.WhomCiv = value.WhomCiv;
        result.ListenerType = value.ListenerType;
        result.Turn = value.Turn;
        result.Denominator = value.Denominator;
        result.Technology = value.Technology;
        result.MoveOwnerCiv = value.MoveOwnerCiv;
        result.MoveUnitType = value.MoveUnitType;
        result.MoveCount = value.MoveCount;
        Copy(value.MoveRectangle, result.MoveRectangle);
        result.MoveX = value.MoveX;
        result.MoveY = value.MoveY;
        result.AggressionWhomCiv = value.AggressionWhomCiv;
        result.AggressionWhoCiv = value.AggressionWhoCiv;
        result.CreateOwnerCiv = value.CreateOwnerCiv;
        result.CreateUnitType = value.CreateUnitType;
        Copy(value.CreateLocations, result.CreateLocations);
        result.CreateLocationCount = value.CreateLocationCount;
        result.CreateVeteran = value.CreateVeteran != 0;
        result.AuxiliaryScalar = value.AuxiliaryScalar;
        result.MoneyReceiverCiv = value.MoneyReceiverCiv;
        result.MoneyAmount = value.MoneyAmount;
        result.NegotiationFlag = value.NegotiationFlag != 0;
        result.CdTrack = value.CdTrack;
        result.TerrainType = value.TerrainType;
        Copy(value.TerrainRectangle, result.TerrainRectangle);
        result.DestroyCiv = value.DestroyCiv;
        result.GiveTech = value.GiveTech;
        result.GiveTechCiv = value.GiveTechCiv;
        return result;
    }

    public static EventRecord ToStored(in EventValues value) => new()
    {
        Triggers = value.Triggers,
        Actions = value.Actions,
        UnitType = value.UnitType,
        WhoCiv = value.WhoCiv,
        TalkerType = value.TalkerType,
        WhomCiv = value.WhomCiv,
        ListenerType = value.ListenerType,
        Turn = value.Turn,
        Denominator = value.Denominator,
        Technology = value.Technology,
        MoveOwnerCiv = value.MoveOwnerCiv,
        MoveUnitType = value.MoveUnitType,
        MoveCount = value.MoveCount,
        MoveRectangle = new(((ReadOnlySpan<int>)value.MoveRectangle).ToArray()),
        MoveX = value.MoveX,
        MoveY = value.MoveY,
        AggressionWhomCiv = value.AggressionWhomCiv,
        AggressionWhoCiv = value.AggressionWhoCiv,
        CreateOwnerCiv = value.CreateOwnerCiv,
        CreateUnitType = value.CreateUnitType,
        CreateLocations = new(((ReadOnlySpan<int>)value.CreateLocations).ToArray()),
        CreateLocationCount = value.CreateLocationCount,
        CreateVeteran = value.CreateVeteran ? (int)1 : (int)0,
        AuxiliaryScalar = value.AuxiliaryScalar,
        MoneyReceiverCiv = value.MoneyReceiverCiv,
        MoneyAmount = value.MoneyAmount,
        NegotiationFlag = value.NegotiationFlag ? (int)1 : (int)0,
        CdTrack = value.CdTrack,
        TerrainType = value.TerrainType,
        TerrainRectangle = new(((ReadOnlySpan<int>)value.TerrainRectangle).ToArray()),
        DestroyCiv = value.DestroyCiv,
        GiveTech = value.GiveTech,
        GiveTechCiv = value.GiveTechCiv,
    };
}
