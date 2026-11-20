using OpenCiv2.Base;
using OpenCiv2.Contracts;

namespace OpenCiv2.Core.State;

/// <summary>F02/F06 logical multiplayer values, independent of runtime authority and votes.</summary>
public record struct MultiplayerValues
{
    public MultiplayerValues() { }

    public CivilizationArray<MapPoint> Centers;
    public CivilizationArray<bool> Introduced;
    public int TurnTimeLimit;
    public OwnedList<string> Passwords = new(Enumerable.Repeat("", EntityLimits.Civilizations));
    public bool OpenGame, KillOnRetirement, ShowHumanStarts, ChatFromStart, Simultaneous;
    public int CheatTurnTime;
    public OwnedList<string> Emails = new(Enumerable.Repeat(string.Empty, EntityLimits.Civilizations));
    public byte ResumeCiv;
    public short ResumeEvent, AutoplayTurns;
    public bool AutoplayActive;
    public short ClaimedHumans;
    public bool DoubleProduction, DoubleMovement;
}
public record struct PasswordFlags
{
    public CivilizationArray<bool> Pending, Present;
}
public record struct OptionVoteState(int Pending, int Status, uint PopupTick);
public record struct MultiplayerState
{
    public MultiplayerState()
    {
        Saved = new();
    }

    public MultiplayerValues Saved;
    public PasswordFlags Passwords;
    public int LocalHuman, ResumeCiv;
    public CivilizationSet HotseatParticipants;
    public int NetworkTurnCiv;
    public bool NetworkHost;
    public int PlayerCount;
    public bool Handoff;
    public OptionVoteState Vote;
}
public static class MultiplayerOperations
{
    public static void RefreshPasswordFlags(ref MultiplayerState state)
    {
        for (int civ = 0; civ < EntityLimits.Civilizations; ++civ)
        {
            state.Passwords.Pending[civ] = state.Passwords.Present[civ] = state.Saved.Passwords[civ].Length != 0;
        }
    }

    /// <summary>F06 §6 completion only, after successful input/close. Equal modes have no effects.</summary>
    public static void AdaptLoadedMode(
        GameMode entry,
        ref GlobalValues globals,
        ref MultiplayerState state,
        ref MultiplayerPreferences preferences)
    {
        if (entry == globals.Multiplayer)
        {
            return;
        }

        state.Saved.OpenGame = true;
        if (entry == GameMode.SinglePlayer)
        {
            state.Saved.DoubleProduction = state.Saved.DoubleMovement = false;
        }

        preferences.Simultaneous = false;
        if (globals.HumanPlayers.Count == 0)
        {
            int resumeCiv = state.ResumeCiv % 32;
            if (state.ResumeCiv != 0 && globals.CivsAlive.Contains(resumeCiv))
            {
                globals.HumanPlayers = CivilizationSet.Only(resumeCiv);
            }
            else
            {
                for (int civ = 1; civ < EntityLimits.Civilizations; ++civ)
                {
                    if (globals.CivsAlive.Contains(civ))
                    {
                        globals.HumanPlayers = CivilizationSet.Only(civ);
                        break;
                    }
                }
            }
        }
        if (entry != GameMode.SinglePlayer)
        {
            globals.Multiplayer = entry;
        }
    }
}
