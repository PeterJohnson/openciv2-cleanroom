using OpenCiv2.Base;
using OpenCiv2.Contracts;

namespace OpenCiv2.Core.State;

/// <summary>F02 process lifetime / F03 §8; stored CIV2.DAT fields are a separate codec.</summary>
public record struct OrdinaryPreferences
{
    public OrdinaryPreferences() { }
    public ushort Rules;
    public short DefaultLevel, Tribe;
    public Difficulty Difficulty;
    public Gender Gender;
    public short Science, Tax;
    public uint GameOptions;
    public ushort DisabledMessages;
    public short Opponents, Barbarians;
    public WorldSize MapSize;
    public Fixed5<short> WorldParameters;
    public OwnedList<byte> Text = OwnedList<byte>.Empty;
    public short AcceleratedStart;
    public RuleSelection RulesChoice;
    public void SetDefaults(int screenWidth, bool animationsAvailable)
    {
        this = new()
        {
            Rules = 0x003f,
            DefaultLevel = screenWidth > 999 ? (short)2 : (short)0,
            Science = 6,
            Tax = 4,
            GameOptions = animationsAvailable ? 0x003f3258U : 0x001f3258U,
            MapSize = WorldSize.Normal,
            Barbarians = 2,
        };
    }
}
public record struct MultiplayerPreferences
{
    public MultiplayerPreferences() { }
    public bool KillOnRetirement, ShowHumanStarts, ChatFromStart, Simultaneous;
    public short MainMenu, Type, HotseatMenu, LanMenu, EmailMenu;
    public NetworkProtocol Protocol;
    public bool OpenGame;
    public OwnedList<byte> GameName = OwnedList<byte>.Empty, PlayerName = OwnedList<byte>.Empty,
        EmailAddress = OwnedList<byte>.Empty, IpAddress = OwnedList<byte>.Empty;
    public short Timer;
    public OwnedList<byte> CustomTimer = OwnedList<byte>.Empty;
    public short HotseatPlayers;
    public Rectangle Parley, Chat;
    public bool ClearChatNewGame, ClearChatBoot, DoubleProduction, DoubleMovement;
    public void SetDefaults() => this = new() { CustomTimer = new("60"u8.ToArray()) };
}
