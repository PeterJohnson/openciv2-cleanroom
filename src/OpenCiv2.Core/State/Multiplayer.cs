using System.Globalization;
using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;

namespace OpenCiv2.Core.State;

/// <summary>F02/F06 logical multiplayer values, independent of runtime authority and votes.</summary>
public record struct MultiplayerValues
{
    public MultiplayerValues() { }
    public Fixed8<short> CenterX, CenterY;
    public Fixed8<bool> Introduced;
    public int TurnTimeLimit;
    public Fixed256<byte> EncodedPasswords;
    public bool OpenGame, KillOnRetirement, ShowHumanStarts, ChatFromStart, Simultaneous;
    public int CheatTurnTime;
    public OwnedList<OwnedList<byte>> Emails = new(Enumerable.Repeat(OwnedList<byte>.Empty, 8));
    public byte ResumeCiv;
    public short ResumeEvent, AutoplayTurns;
    public bool AutoplayActive;
    public short ClaimedHumans;
    public bool DoubleProduction, DoubleMovement;
}
public record struct PasswordFlags
{
    public Fixed8<bool> Pending, Present;
}
public record struct OptionVoteState(int Pending, int Status, uint PopupTick);
public record struct MultiplayerState
{
    public MultiplayerState() { Saved = new(); }
    public MultiplayerValues Saved;
    public PasswordFlags Passwords;
    public int LocalHuman, ResumeCiv;
    public byte HotseatParticipants;
    public int NetworkTurnCiv;
    public bool NetworkHost;
    public int PlayerCount;
    public bool Handoff;
    public OptionVoteState Vote;
}
public static class MultiplayerOperations
{
    // F06 §9 circular bit rotation across rows and the array seam, followed by index XOR.
    public static void EncodePasswords(Span<byte> bytes)
    {
        if (bytes.Length != 256) throw new ArgumentException("Passwords require 256 bytes", nameof(bytes));
        Span<byte> prior = stackalloc byte[256]; bytes.CopyTo(prior);
        for (int i = 0; i < 256; ++i) bytes[i] = unchecked((byte)(((prior[i] >> 3) | ((prior[(i + 255) % 256] & 7) << 5)) ^ i));
    }
    public static void DecodePasswords(Span<byte> bytes)
    {
        if (bytes.Length != 256) throw new ArgumentException("Passwords require 256 bytes", nameof(bytes));
        Span<byte> prior = stackalloc byte[256]; bytes.CopyTo(prior);
        for (int i = 0; i < 256; ++i)
        {
            int next = (i + 1) % 256;
            bytes[i] = unchecked((byte)(((prior[i] ^ i) << 3) | ((prior[next] ^ next) >> 5)));
        }
    }
    public static void RefreshPasswordFlags(ref MultiplayerState state)
    {
        DecodePasswords(state.Saved.EncodedPasswords);
        for (int civ = 0; civ < 8; ++civ) state.Passwords.Pending[civ] = state.Passwords.Present[civ] = state.Saved.EncodedPasswords[civ * 32] != 0;
        EncodePasswords(state.Saved.EncodedPasswords);
    }
    public static void ResetPasswords(ref MultiplayerState state, GameplayRandom random, string call)
    {
        ArgumentException.ThrowIfNullOrEmpty(call);
        ArgumentNullException.ThrowIfNull(random);
        for (int i = 0; i < 255; ++i)
        {
            var key = new ChoiceKey("F06.password-reset.byte", call + "/row/" + (i / 32).ToString(CultureInfo.InvariantCulture) + "/byte/" + (i % 32).ToString(CultureInfo.InvariantCulture), 0);
            state.Saved.EncodedPasswords[i] = unchecked((byte)random.PrimaryCode(key));
        }
        for (int civ = 0; civ < 8; ++civ)
        {
            state.Saved.EncodedPasswords[civ * 32] = 0;
            state.Passwords.Pending[civ] = state.Passwords.Present[civ] = false;
        }
        EncodePasswords(state.Saved.EncodedPasswords);
    }
    /// <summary>F06 §6 completion only, after successful input/close. Equal modes have no effects.</summary>
    public static void AdaptLoadedMode(GameMode entry, ref GlobalValues globals, ref MultiplayerState state, ref MultiplayerPreferences preferences)
    {
        if (entry == globals.Multiplayer) return;
        state.Saved.OpenGame = true;
        if (entry == GameMode.SinglePlayer) state.Saved.DoubleProduction = state.Saved.DoubleMovement = false;
        preferences.Simultaneous = false;
        if (globals.HumanPlayers == 0)
        {
            uint resumeBit = 1U << (state.ResumeCiv & 31);
            if (state.ResumeCiv != 0 && (globals.CivsAlive & resumeBit) != 0) globals.HumanPlayers = unchecked((byte)resumeBit);
            else for (int civ = 1; civ < 8; ++civ)
                if ((globals.CivsAlive & (1 << civ)) != 0) { globals.HumanPlayers = (byte)(1 << civ); break; }
        }
        if (entry != GameMode.SinglePlayer) globals.Multiplayer = entry;
    }
}
