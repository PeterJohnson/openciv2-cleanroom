using OpenCiv2.Base;
using OpenCiv2.Contracts;

namespace OpenCiv2.Core.State;

/// <summary>F02 process lifetime / F03 §8; stored CIV2.DAT fields are a separate codec.</summary>
public record struct OrdinaryPreferences
{
    public OrdinaryPreferences() { }

    public GameRules Rules;
    public short DefaultLevel, Tribe;
    public Difficulty Difficulty;
    public Gender Gender;
    public short Science, Tax;
    public GameOptions GameOptions;
    public MessageSuppression DisabledMessages;
    public short Opponents, Barbarians;
    public WorldSize MapSize;
    public Fixed5<short> WorldParameters;
    public string Text = string.Empty;
    public short AcceleratedStart;
    public RuleSelection RulesChoice;

    public void SetDefaults(int screenWidth, bool animationsAvailable)
    {
        this = new()
        {
            Rules = GameRules.Normal,
            DefaultLevel = screenWidth > 999 ? (short)2 : (short)0,
            Science = 6,
            Tax = 4,
            GameOptions = new()
            {
                Music = true,
                Sound = true,
                EnterClosesCity = true,
                Advice = true,
                EnemyMoves = true,
                Autosave = true,
                WonderMovies = true,
                Throne = true,
                Diplomacy = true,
                AdvanceCivilopedia = true,
                Council = true,
                Heralds = animationsAvailable
            },
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
    public string GameName = string.Empty, PlayerName = string.Empty,
        EmailAddress = string.Empty, IpAddress = string.Empty;
    public short Timer;
    public string CustomTimer = string.Empty;
    public short HotseatPlayers;
    public Rectangle Parley, Chat;
    public bool ClearChatNewGame, ClearChatBoot, DoubleProduction, DoubleMovement;

    public void SetDefaults() => this = new() { CustomTimer = "60" };
}
