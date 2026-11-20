using OpenCiv2.Base;
using OpenCiv2.Core.State;
using OpenCiv2.IO;

namespace OpenCiv2.Formats;

/// <summary>F06 §4 exact preference items. No defaults, range checks or session installation.</summary>
public static class ClassicPreferences
{
    public const int OrdinarySize = 72, MultiplayerSize = 444;

    public static StoredOrdinaryPreferences DecodeOrdinary(ReadOnlySpan<byte> bytes)
    {
        RequireSize(bytes, 72);
        var r = new ByteReader(bytes);
        var rules = r.U16();
        var defaultLevel = r.I16();
        var difficulty = r.I16();
        var tribe = r.I16();
        var gender = r.I16();
        var science = r.I16();
        var tax = r.I16();
        var gameOptions = r.U32();
        var disabledMessages = r.U16();
        var opponents = r.I16();
        var mapSize = r.I16();
        var barbarians = r.I16();
        var worldParameters = new short[5];
        for (int i = 0; i < 5; ++i)
        {
            worldParameters[i] = r.I16();
        }

        var text = new OwnedList<byte>(r.Take(32).ToArray());
        var rulesChoice = r.I16();
        var acceleratedStart = r.I16();
        return new()
        {
            Rules = rules,
            DefaultLevel = defaultLevel,
            Difficulty = difficulty,
            Tribe = tribe,
            Gender = gender,
            Science = science,
            Tax = tax,
            GameOptions = gameOptions,
            DisabledMessages = disabledMessages,
            Opponents = opponents,
            MapSize = mapSize,
            Barbarians = barbarians,
            WorldParameters = new(worldParameters),
            Text = text,
            RulesChoice = rulesChoice,
            AcceleratedStart = acceleratedStart,
        };
    }

    public static byte[] EncodeOrdinary(StoredOrdinaryPreferences value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var w = new ByteWriter();
        w.U16(value.Rules);
        w.I16(value.DefaultLevel);
        w.I16(value.Difficulty);
        w.I16(value.Tribe);
        w.I16(value.Gender);
        w.I16(value.Science);
        w.I16(value.Tax);
        w.U32(value.GameOptions);
        w.U16(value.DisabledMessages);
        w.I16(value.Opponents);
        w.I16(value.MapSize);
        w.I16(value.Barbarians);
        RequireCount(value.WorldParameters, 5);
        foreach (var item in value.WorldParameters)
        {
            w.I16(item);
        }

        RequireCount(value.Text, 32);
        foreach (var item in value.Text)
        {
            w.U8(item);
        }

        w.I16(value.RulesChoice);
        w.I16(value.AcceleratedStart);
        return w.ToArray();
    }

    public static StoredOrdinaryPreferences DecodeOrdinaryItem(ReadResult item, StoredOrdinaryPreferences prior) =>
        DecodeOrdinary(Merge(item, 72, () => EncodeOrdinary(prior)));

    public static byte[] EncodeOrdinary(in OrdinaryPreferences value) =>
        EncodeOrdinary(StateConversion.ToStored(value));

    public static StoredMultiplayerPreferences DecodeMultiplayer(ReadOnlySpan<byte> bytes)
    {
        RequireSize(bytes, 444);
        var r = new ByteReader(bytes);
        var mainMenu = r.I16();
        var type = r.I16();
        var hotseatMenu = r.I16();
        var lanMenu = r.I16();
        var legacySetting1 = r.I16();
        var emailMenu = r.I16();
        var protocol = r.U16();
        var openGame = r.I16();
        var killOnRetirement = r.I16();
        var showHumanStarts = r.I16();
        var chatFromStart = r.I16();
        var simultaneous = r.I16();
        var gameName = new OwnedList<byte>(r.Take(32).ToArray());
        var playerName = new OwnedList<byte>(r.Take(32).ToArray());
        var emailAddress = new OwnedList<byte>(r.Take(32).ToArray());
        var ipAddress = new OwnedList<byte>(r.Take(32).ToArray());
        var timer = r.I16();
        var customTimer = new OwnedList<byte>(r.Take(6).ToArray());
        var hotseatPlayers = r.I16();
        var parley = new Rectangle(r.I32(), r.I32(), r.I32(), r.I32());
        var chat = new Rectangle(r.I32(), r.I32(), r.I32(), r.I32());
        var clearChatNewGame = r.I16();
        var clearChatBoot = r.I16();
        var doubleProduction = r.I16();
        var doubleMovement = r.I16();
        var legacySettings = new OwnedList<byte>[3];
        for (int i = 0; i < 3; ++i)
        {
            legacySettings[i] = new(r.Take(24).ToArray());
        }

        var settings = new OwnedList<byte>[7];
        for (int i = 0; i < 7; ++i)
        {
            settings[i] = new(r.Take(24).ToArray());
        }

        var legacySetting5 = r.I16();
        return new()
        {
            MainMenu = mainMenu,
            Type = type,
            HotseatMenu = hotseatMenu,
            LanMenu = lanMenu,
            LegacySetting1 = legacySetting1,
            EmailMenu = emailMenu,
            Protocol = protocol,
            OpenGame = openGame,
            KillOnRetirement = killOnRetirement,
            ShowHumanStarts = showHumanStarts,
            ChatFromStart = chatFromStart,
            Simultaneous = simultaneous,
            GameName = gameName,
            PlayerName = playerName,
            EmailAddress = emailAddress,
            IpAddress = ipAddress,
            Timer = timer,
            CustomTimer = customTimer,
            HotseatPlayers = hotseatPlayers,
            Parley = parley,
            Chat = chat,
            ClearChatNewGame = clearChatNewGame,
            ClearChatBoot = clearChatBoot,
            DoubleProduction = doubleProduction,
            DoubleMovement = doubleMovement,
            LegacySettings = new(legacySettings),
            Settings = new(settings),
            LegacySetting5 = legacySetting5,
        };
    }

    public static byte[] EncodeMultiplayer(StoredMultiplayerPreferences value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var w = new ByteWriter();
        w.I16(value.MainMenu);
        w.I16(value.Type);
        w.I16(value.HotseatMenu);
        w.I16(value.LanMenu);
        w.I16(value.LegacySetting1);
        w.I16(value.EmailMenu);
        w.U16(value.Protocol);
        w.I16(value.OpenGame);
        w.I16(value.KillOnRetirement);
        w.I16(value.ShowHumanStarts);
        w.I16(value.ChatFromStart);
        w.I16(value.Simultaneous);
        RequireCount(value.GameName, 32);
        foreach (var item in value.GameName)
        {
            w.U8(item);
        }

        RequireCount(value.PlayerName, 32);
        foreach (var item in value.PlayerName)
        {
            w.U8(item);
        }

        RequireCount(value.EmailAddress, 32);
        foreach (var item in value.EmailAddress)
        {
            w.U8(item);
        }

        RequireCount(value.IpAddress, 32);
        foreach (var item in value.IpAddress)
        {
            w.U8(item);
        }

        w.I16(value.Timer);
        RequireCount(value.CustomTimer, 6);
        foreach (var item in value.CustomTimer)
        {
            w.U8(item);
        }

        w.I16(value.HotseatPlayers);
        WriteRectangle(w, value.Parley);
        WriteRectangle(w, value.Chat);
        w.I16(value.ClearChatNewGame);
        w.I16(value.ClearChatBoot);
        w.I16(value.DoubleProduction);
        w.I16(value.DoubleMovement);
        RequireCount(value.LegacySettings, 3);
        foreach (var row in value.LegacySettings)
        {
            RequireCount(row, 24);
            foreach (byte item in row)
            {
                w.U8(item);
            }
        }
        RequireCount(value.Settings, 7);
        foreach (var row in value.Settings)
        {
            RequireCount(row, 24);
            foreach (byte item in row)
            {
                w.U8(item);
            }
        }
        w.I16(value.LegacySetting5);
        return w.ToArray();
    }

    public static StoredMultiplayerPreferences DecodeMultiplayerItem(
        ReadResult item,
        StoredMultiplayerPreferences prior) =>
        DecodeMultiplayer(Merge(item, 444, () => EncodeMultiplayer(prior)));

    public static byte[] EncodeMultiplayer(in MultiplayerPreferences value) =>
        EncodeMultiplayer(StateConversion.ToStored(value));

    private static void RequireSize(ReadOnlySpan<byte> bytes, int size)
    {
        if (bytes.Length != size)
        {
            throw new DecodeException(Math.Min(bytes.Length, size), "Incorrect preference item length");
        }
    }

    private static void RequireCount<T>(OwnedList<T> values, int count)
    {
        if (values.Count != count)
        {
            throw new ArgumentException("Preference field has an incorrect extent", nameof(values));
        }
    }

    private static void WriteRectangle(ByteWriter w, Rectangle value)
    {
        w.I32(value.Left);
        w.I32(value.Top);
        w.I32(value.Right);
        w.I32(value.Bottom);
    }

    private static byte[] Merge(ReadResult item, int size, Func<byte[]> prior)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Prefix.Count > size || (item.Complete && item.Prefix.Count != size))
        {
            throw new ArgumentException("Preference read has an inconsistent prefix", nameof(item));
        }

        var bytes = prior();
        for (int i = 0; i < item.Prefix.Count; ++i)
        {
            bytes[i] = item.Prefix[i];
        }

        return bytes;
    }
}
