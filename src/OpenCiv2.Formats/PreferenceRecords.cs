using OpenCiv2.Base;
using OpenCiv2.Core.State;

namespace OpenCiv2.Formats;

// F06 §4 CIV2.DAT: immutable stored values, including arbitrary scalar bits and opaque tails.
public sealed record StoredOrdinaryPreferences
{
    public ushort Rules { get; init; }
    public short DefaultLevel { get; init; }
    public short Difficulty { get; init; }
    public short Tribe { get; init; }
    public short Gender { get; init; }
    public short Science { get; init; }
    public short Tax { get; init; }
    public uint GameOptions { get; init; }
    public ushort DisabledMessages { get; init; }
    public short Opponents { get; init; }
    public short MapSize { get; init; }
    public short Barbarians { get; init; }
    public OwnedList<short> WorldParameters { get; init; } = new(new short[5]);
    public OwnedList<byte> Text { get; init; } = new(new byte[32]);
    public short RulesChoice { get; init; }
    public short AcceleratedStart { get; init; }
}

public sealed record StoredMultiplayerPreferences
{
    public short MainMenu { get; init; }
    public short Type { get; init; }
    public short HotseatMenu { get; init; }
    public short LanMenu { get; init; }
    public short LegacySetting1 { get; init; }
    public short EmailMenu { get; init; }
    public ushort Protocol { get; init; }
    public short OpenGame { get; init; }
    public short KillOnRetirement { get; init; }
    public short ShowHumanStarts { get; init; }
    public short ChatFromStart { get; init; }
    public short Simultaneous { get; init; }
    public OwnedList<byte> GameName { get; init; } = new(new byte[32]);
    public OwnedList<byte> PlayerName { get; init; } = new(new byte[32]);
    public OwnedList<byte> EmailAddress { get; init; } = new(new byte[32]);
    public OwnedList<byte> IpAddress { get; init; } = new(new byte[32]);
    public short Timer { get; init; }
    public OwnedList<byte> CustomTimer { get; init; } = new(new byte[6]);
    public short HotseatPlayers { get; init; }
    public Rectangle Parley { get; init; }
    public Rectangle Chat { get; init; }
    public short ClearChatNewGame { get; init; }
    public short ClearChatBoot { get; init; }
    public short DoubleProduction { get; init; }
    public short DoubleMovement { get; init; }
    public OwnedList<OwnedList<byte>> LegacySettings { get; init; } = new(Enumerable.Range(0, 3).Select(_ => new OwnedList<byte>(new byte[24])));
    public OwnedList<OwnedList<byte>> Settings { get; init; } = new(Enumerable.Range(0, 7).Select(_ => new OwnedList<byte>(new byte[24])));
    public short LegacySetting5 { get; init; }
}
