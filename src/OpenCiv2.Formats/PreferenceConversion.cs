using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.State;

namespace OpenCiv2.Formats;

public static partial class StateConversion
{
    public static OrdinaryPreferences ToLive(StoredOrdinaryPreferences value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var result = new OrdinaryPreferences();
        result.Rules = DecodeGameRules(value.Rules);
        result.DefaultLevel = value.DefaultLevel;
        result.Difficulty = (Difficulty)value.Difficulty;
        result.Tribe = value.Tribe;
        result.Gender = (Gender)value.Gender;
        result.Science = value.Science;
        result.Tax = value.Tax;
        result.GameOptions = DecodeGameOptions(value.GameOptions);
        result.DisabledMessages = DecodeMessageSuppression(value.DisabledMessages);
        result.Opponents = value.Opponents;
        result.MapSize = (WorldSize)value.MapSize;
        result.Barbarians = value.Barbarians;
        Copy(value.WorldParameters, result.WorldParameters);
        result.Text = LiveText(value.Text, 32);
        result.RulesChoice = (RuleSelection)value.RulesChoice;
        result.AcceleratedStart = value.AcceleratedStart;
        return result;
    }

    public static StoredOrdinaryPreferences ToStored(in OrdinaryPreferences value) => new()
    {
        Rules = EncodeGameRules(value.Rules),
        DefaultLevel = value.DefaultLevel,
        Difficulty = unchecked((short)value.Difficulty),
        Tribe = value.Tribe,
        Gender = unchecked((short)value.Gender),
        Science = value.Science,
        Tax = value.Tax,
        GameOptions = EncodeGameOptions(value.GameOptions),
        DisabledMessages = EncodeMessageSuppression(value.DisabledMessages),
        Opponents = value.Opponents,
        MapSize = unchecked((short)value.MapSize),
        Barbarians = value.Barbarians,
        WorldParameters = new(((ReadOnlySpan<short>)value.WorldParameters).ToArray()),
        Text = StoredText(value.Text, 32),
        RulesChoice = unchecked((short)value.RulesChoice),
        AcceleratedStart = value.AcceleratedStart,
    };

    public static StoredOrdinaryPreferences PreserveOpaque(StoredOrdinaryPreferences output, StoredOrdinaryPreferences imported) => output with
    {
        Rules = (ushort)((output.Rules & GameRulesMask) | (imported.Rules & ~GameRulesMask)),
        GameOptions = (output.GameOptions & GameOptionsMask) | (imported.GameOptions & ~GameOptionsMask),
        DisabledMessages = (ushort)((output.DisabledMessages & MessageSuppressionMask) | (imported.DisabledMessages & ~MessageSuppressionMask))
    };

    public static MultiplayerPreferences ToLive(StoredMultiplayerPreferences value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var result = new MultiplayerPreferences();
        result.MainMenu = value.MainMenu;
        result.Type = value.Type;
        result.HotseatMenu = value.HotseatMenu;
        result.LanMenu = value.LanMenu;
        result.EmailMenu = value.EmailMenu;
        result.Protocol = (NetworkProtocol)value.Protocol;
        result.OpenGame = value.OpenGame != 0;
        result.KillOnRetirement = value.KillOnRetirement != 0;
        result.ShowHumanStarts = value.ShowHumanStarts != 0;
        result.ChatFromStart = value.ChatFromStart != 0;
        result.Simultaneous = value.Simultaneous != 0;
        result.GameName = LiveText(value.GameName, 32);
        result.PlayerName = LiveText(value.PlayerName, 32);
        result.EmailAddress = LiveText(value.EmailAddress, 32);
        result.IpAddress = LiveText(value.IpAddress, 32);
        result.Timer = value.Timer;
        result.CustomTimer = LiveText(value.CustomTimer, 6);
        result.HotseatPlayers = value.HotseatPlayers;
        result.Parley = value.Parley;
        result.Chat = value.Chat;
        result.ClearChatNewGame = value.ClearChatNewGame != 0;
        result.ClearChatBoot = value.ClearChatBoot != 0;
        result.DoubleProduction = value.DoubleProduction != 0;
        result.DoubleMovement = value.DoubleMovement != 0;
        return result;
    }

    public static StoredMultiplayerPreferences ToStored(in MultiplayerPreferences value) => new()
    {
        MainMenu = value.MainMenu,
        Type = value.Type,
        HotseatMenu = value.HotseatMenu,
        LanMenu = value.LanMenu,
        EmailMenu = value.EmailMenu,
        Protocol = unchecked((ushort)value.Protocol),
        OpenGame = value.OpenGame ? (short)1 : (short)0,
        KillOnRetirement = value.KillOnRetirement ? (short)1 : (short)0,
        ShowHumanStarts = value.ShowHumanStarts ? (short)1 : (short)0,
        ChatFromStart = value.ChatFromStart ? (short)1 : (short)0,
        Simultaneous = value.Simultaneous ? (short)1 : (short)0,
        GameName = StoredText(value.GameName, 32),
        PlayerName = StoredText(value.PlayerName, 32),
        EmailAddress = StoredText(value.EmailAddress, 32),
        IpAddress = StoredText(value.IpAddress, 32),
        Timer = value.Timer,
        CustomTimer = StoredText(value.CustomTimer, 6),
        HotseatPlayers = value.HotseatPlayers,
        Parley = value.Parley,
        Chat = value.Chat,
        ClearChatNewGame = value.ClearChatNewGame ? (short)1 : (short)0,
        ClearChatBoot = value.ClearChatBoot ? (short)1 : (short)0,
        DoubleProduction = value.DoubleProduction ? (short)1 : (short)0,
        DoubleMovement = value.DoubleMovement ? (short)1 : (short)0,
    };

    public static StoredMultiplayerPreferences PreserveOpaque(
        StoredMultiplayerPreferences output,
        StoredMultiplayerPreferences imported) =>
        output with
        {
            LegacySetting1 = imported.LegacySetting1,
            LegacySetting5 = imported.LegacySetting5,
            LegacySettings = imported.LegacySettings,
            Settings = imported.Settings,
        };
}
