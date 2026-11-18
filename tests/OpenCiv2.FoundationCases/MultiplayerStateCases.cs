using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.State;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // F06 §§6,9/F01: independent bit destinations and semantic choices.
    private static ChoiceKey PasswordKey(string call, int i) => new("F06.password-reset.byte", $"{call}/row/{i / 32}/byte/{i % 32}");
    private static int PasswordCode(int i) => (0x1234 + i * 97) % 32768;
    private static ControlledChoices PasswordChoices(string call, int count = 255) => new(new(Enumerable.Range(0, count).Select(i =>
        Choice.Create(PasswordKey(call, i), new SamplingLaw.PrimaryCode(), ChoiceCode.Primary(PasswordCode(i))))));
    private static MultiplayerState IncomingMultiplayer()
    {
        var result = new MultiplayerState { LocalHuman = 300, ResumeCiv = 301, NetworkHost = true, HotseatParticipants = 0xa5, Vote = new(-3, -7, uint.MaxValue) };
        result.Saved.TurnTimeLimit = 123456; result.Saved.Simultaneous = true; result.Saved.DoubleMovement = true; result.Saved.DoubleProduction = true;
        for (int i = 0; i < 256; ++i) result.Saved.EncodedPasswords[i] = 0xa5;
        for (int i = 0; i < 8; ++i) result.Passwords.Pending[i] = result.Passwords.Present[i] = true;
        return result;
    }
    private static void PasswordRotation()
    {
        var zero = new byte[256]; var index = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray(); var encoded = zero.ToArray();
        MultiplayerOperations.EncodePasswords(encoded); BytesEqual(index, encoded); MultiplayerOperations.DecodePasswords(encoded); BytesEqual(zero, encoded);
        for (int i = 0; i < 256; ++i) for (int bit = 0; bit < 8; ++bit)
        {
            var plain = new byte[256]; plain[i] = (byte)(1 << bit); var expected = index.ToArray();
            int destination = bit >= 3 ? i : (i + 1) % 256; expected[destination] ^= (byte)(1 << (bit >= 3 ? bit - 3 : bit + 5));
            var actual = plain.ToArray(); MultiplayerOperations.EncodePasswords(actual); BytesEqual(expected, actual); MultiplayerOperations.DecodePasswords(actual); BytesEqual(plain, actual);
        }
        foreach (int count in new[] { 0, 255, 257 }) { var bytes = new byte[count]; Throws<ArgumentException>(() => MultiplayerOperations.EncodePasswords(bytes)); Throws<ArgumentException>(() => MultiplayerOperations.DecodePasswords(bytes)); Equal(true, bytes.All(b => b == 0)); }
    }
    private static void PasswordReset()
    {
        var state = IncomingMultiplayer(); state.Saved.EncodedPasswords[255] = 0x5a; var before = state; var random = new GameplayRandom(PasswordChoices("load/one"), "session");
        MultiplayerOperations.ResetPasswords(ref state, random, "load/one"); random.RequireChoicesConsumed(); Equal(0UL, random.Capture().Position.Draws); Equal(255, random.Capture().Trace!.Events.Count); Equal(PasswordKey("load/one", 254), random.Capture().Trace!.Events[254].Key);
        Equal((byte)0x40, state.Saved.EncodedPasswords[0]); var plain = state.Saved.EncodedPasswords; MultiplayerOperations.DecodePasswords(plain);
        for (int i = 0; i < 255; ++i) Equal(i % 32 == 0 ? (byte)0 : unchecked((byte)PasswordCode(i)), plain[i]);
        Equal((byte)0x5a, plain[255]); var expected = before; expected.Saved.EncodedPasswords = state.Saved.EncodedPasswords; expected.Passwords = default; Equal(expected, state);
    }
    private static void PasswordResetFailure()
    {
        var state = IncomingMultiplayer(); var before = state; var random = new GameplayRandom(PasswordChoices("load/short", 100), "session");
        ChoiceFails(ChoiceFailure.Missing, () => MultiplayerOperations.ResetPasswords(ref state, random, "load/short"), PasswordKey("load/short", 100));
        var expected = before; for (int i = 0; i < 100; ++i) expected.Saved.EncodedPasswords[i] = unchecked((byte)PasswordCode(i));
        Equal(expected, state); Equal((byte)0x34, state.Saved.EncodedPasswords[0]); Equal(100, random.Capture().Trace!.Events.Count);
        Throws<ArgumentException>(() => MultiplayerOperations.ResetPasswords(ref state, random, "")); Equal(expected, state);
    }
    private static void PasswordRefresh()
    {
        var state = IncomingMultiplayer(); for (int i = 0; i < 256; ++i) state.Saved.EncodedPasswords[i] = 0x7b;
        state.Saved.EncodedPasswords[0] = 0; state.Saved.EncodedPasswords[224] = 0; MultiplayerOperations.EncodePasswords(state.Saved.EncodedPasswords); var before = state;
        MultiplayerOperations.RefreshPasswordFlags(ref state);
        var expected = before; for (int civ = 0; civ < 8; ++civ) expected.Passwords.Pending[civ] = expected.Passwords.Present[civ] = civ != 0 && civ != 7; Equal(expected, state);
        for (int i = 0; i < 256; ++i) state.Saved.EncodedPasswords[i] = 255;
        MultiplayerOperations.EncodePasswords(state.Saved.EncodedPasswords); MultiplayerOperations.RefreshPasswordFlags(ref state);
        for (int civ = 0; civ < 8; ++civ) Equal(true, state.Passwords.Present[civ]);
    }
    private static void PasswordReplay()
    {
        var random = new GameplayRandom(123, "session"); var presentation = new PresentationRandom(456, "session"); var cosmetic = new CosmeticRandom(789, "frontend");
        var before = random.Capture(); var presentationBefore = presentation.Capture(); var cosmeticBefore = cosmetic.Capture(); var first = IncomingMultiplayer(); var second = first;
        random.RecordChoices(255); MultiplayerOperations.ResetPasswords(ref first, random, "load/replay"); var after = random.Capture(); Equal(255UL, after.Position.Draws); Equal(255, after.Trace!.Events.Count);
        Equal(presentationBefore, presentation.Capture()); Equal(cosmeticBefore, cosmetic.Capture()); random.Restore(before, 1000); random.RecordChoices(255);
        presentation.PrimaryCode("presentation.before-load"); cosmetic.PrimaryCode("cosmetic.before-load"); MultiplayerOperations.ResetPasswords(ref second, random, "load/replay"); Equal(first, second); Equal(after, random.Capture());
    }
    private static void MultiplayerModeAdaptation()
    {
        foreach (GameMode entry in new[] { GameMode.SinglePlayer, GameMode.Hotseat, GameMode.Tcp }) foreach (GameMode loaded in new[] { GameMode.SinglePlayer, GameMode.Hotseat, GameMode.Tcp })
        {
            var state = IncomingMultiplayer(); state.ResumeCiv = 3; var expected = state;
            var globals = new GlobalValues { Multiplayer = loaded, CivsAlive = 0x0b, PlayerCiv = 5 }; var preferences = new MultiplayerPreferences { Simultaneous = true, KillOnRetirement = true };
            MultiplayerOperations.AdaptLoadedMode(entry, ref globals, ref state, ref preferences);
            if (entry != loaded) { expected.Saved.OpenGame = true; if (entry == GameMode.SinglePlayer) expected.Saved.DoubleProduction = expected.Saved.DoubleMovement = false; }
            Equal(expected, state); Equal((int)(entry == GameMode.SinglePlayer ? loaded : entry), (int)globals.Multiplayer); Equal(entry == loaded ? (byte)0 : (byte)8, globals.HumanPlayers); Equal((sbyte)5, globals.PlayerCiv); Equal(entry == loaded, preferences.Simultaneous); Equal(true, preferences.KillOnRetirement);
        }
        foreach (var (humans, alive, resume, expected) in new (byte, byte, int, byte)[] { (0x40, 0, 3, 0x40), (0, 0x83, 0, 2), (0, 0x83, 3, 2), (0, 1, 0, 0), (0, 0, 7, 0), (0, 0x82, 7, 0x80), (0, 0x82, 33, 2), (0, 0x82, -31, 2) })
        {
            var state = IncomingMultiplayer(); state.ResumeCiv = resume; var globals = new GlobalValues { Multiplayer = GameMode.Tcp, HumanPlayers = humans, CivsAlive = alive }; var preferences = new MultiplayerPreferences();
            MultiplayerOperations.AdaptLoadedMode(GameMode.SinglePlayer, ref globals, ref state, ref preferences); Equal(expected, globals.HumanPlayers);
        }
    }
}
