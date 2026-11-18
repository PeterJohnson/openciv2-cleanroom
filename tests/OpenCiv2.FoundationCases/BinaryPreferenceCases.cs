using System.Buffers.Binary;
using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.State;
using OpenCiv2.Formats;
using OpenCiv2.IO;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // F06 §4 CIV2.DAT and F03 §8; authored offset images, not external captures.
    private static void PreferenceWord(byte[] bytes, int at, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(at), value);
    private static void PreferenceDword(byte[] bytes, int at, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at), value);
    private static void BinaryOrdinaryPreferences()
    {
        byte[] bytes = Enumerable.Repeat((byte)0xa5, 72).ToArray(); PreferenceWord(bytes, 0, 0x813f);
        for (int i = 1; i <= 6; ++i) PreferenceWord(bytes, i * 2, (ushort)(0xfff0 + i));
        PreferenceDword(bytes, 14, 0xfedcba98); PreferenceWord(bytes, 18, 0x8001);
        for (int i = 0; i < 8; ++i) PreferenceWord(bytes, 20 + 2 * i, (ushort)(101 + i));
        PreferenceWord(bytes, 68, 0x8000); PreferenceWord(bytes, 70, 0x7fff);
        var p = ClassicPreferences.DecodeOrdinary(bytes);
        Equal((ushort)0x813f, p.Rules); Equal((short)-15, p.DefaultLevel); Equal((short)-14, p.Difficulty); Equal((short)-13, p.Tribe); Equal((short)-12, p.Gender);
        Equal((short)-11, p.Science); Equal((short)-10, p.Tax); Equal(0xfedcba98U, p.GameOptions); Equal((ushort)0x8001, p.DisabledMessages);
        Equal((short)101, p.Opponents); Equal((short)102, p.MapSize); Equal((short)103, p.Barbarians); Equal(new OwnedList<short>([104, 105, 106, 107, 108]), p.WorldParameters);
        Equal(new OwnedList<byte>(Enumerable.Repeat((byte)0xa5, 32)), p.Text); Equal(short.MinValue, p.RulesChoice); Equal(short.MaxValue, p.AcceleratedStart);
        Equal(new OwnedList<byte>(bytes), new(ClassicPreferences.EncodeOrdinary(p)));
        var live = StateConversion.ToLive(p); Equal(-14, (int)live.Difficulty); Equal(-12, (int)live.Gender); Equal(102, (int)live.MapSize); Equal(-32768, (int)live.RulesChoice);
        Equal(p, StateConversion.ToStored(live)); bytes[36] = 0; Equal((byte)0xa5, p.Text[0]);
    }
    private static void BinaryMultiplayerPreferences()
    {
        byte[] bytes = new byte[444]; for (int i = 0; i < 12; ++i) PreferenceWord(bytes, i * 2, (ushort)(0xff00 + i));
        for (int i = 0; i < 4; ++i) bytes.AsSpan(0x18 + i * 32, 32).Fill((byte)(0xa1 + i));
        PreferenceWord(bytes, 0x98, 0x8000); bytes.AsSpan(0x9a, 6).Fill(0xb5); PreferenceWord(bytes, 0xa0, 0x7fff);
        for (int i = 0; i < 8; ++i) PreferenceDword(bytes, 0xa2 + i * 4, 0x80000001U + (uint)i);
        for (int i = 0; i < 4; ++i) PreferenceWord(bytes, 0xc2 + i * 2, (ushort)(0x4000 + i));
        for (int i = 0; i < 10; ++i) bytes.AsSpan(0xca + i * 24, 24).Fill((byte)(0xc0 + i)); PreferenceWord(bytes, 0x1ba, 0x8899);
        var p = ClassicPreferences.DecodeMultiplayer(bytes);
        short[] initial = [p.MainMenu, p.Type, p.HotseatMenu, p.LanMenu, p.LegacySetting1, p.EmailMenu, unchecked((short)p.Protocol), p.OpenGame, p.KillOnRetirement, p.ShowHumanStarts, p.ChatFromStart, p.Simultaneous];
        for (int i = 0; i < 12; ++i) Equal((short)(-256 + i), initial[i]);
        var texts = new[] { p.GameName, p.PlayerName, p.EmailAddress, p.IpAddress };
        for (int i = 0; i < 4; ++i) Equal(new OwnedList<byte>(Enumerable.Repeat((byte)(0xa1 + i), 32)), texts[i]);
        Equal(short.MinValue, p.Timer); Equal(new OwnedList<byte>(Enumerable.Repeat((byte)0xb5, 6)), p.CustomTimer); Equal(short.MaxValue, p.HotseatPlayers);
        Equal(new Rectangle(int.MinValue + 1, int.MinValue + 2, int.MinValue + 3, int.MinValue + 4), p.Parley);
        Equal(new Rectangle(int.MinValue + 5, int.MinValue + 6, int.MinValue + 7, int.MinValue + 8), p.Chat);
        Equal((short)0x4000, p.ClearChatNewGame); Equal((short)0x4001, p.ClearChatBoot); Equal((short)0x4002, p.DoubleProduction); Equal((short)0x4003, p.DoubleMovement);
        for (int i = 0; i < 10; ++i) Equal(new OwnedList<byte>(Enumerable.Repeat((byte)(0xc0 + i), 24)), i < 3 ? p.LegacySettings[i] : p.Settings[i - 3]);
        Equal(unchecked((short)0x8899), p.LegacySetting5); Equal(new OwnedList<byte>(bytes), new(ClassicPreferences.EncodeMultiplayer(p)));
    }
    private static void BinaryPreferenceRoundTrips()
    {
        foreach (int size in new[] { 72, 444 })
        {
            for (int bit = 0; bit < size * 8; ++bit)
            {
                var bytes = new byte[size]; bytes[bit / 8] = (byte)(1 << (bit % 8));
                byte[] result = size == 72 ? ClassicPreferences.EncodeOrdinary(ClassicPreferences.DecodeOrdinary(bytes)) : ClassicPreferences.EncodeMultiplayer(ClassicPreferences.DecodeMultiplayer(bytes));
                Equal(new OwnedList<byte>(bytes), new(result));
            }
            var pattern = Enumerable.Range(0, size).Select(i => unchecked((byte)(i * 71 + 139))).ToArray();
            for (int length = 0; length < size; ++length)
            {
                try
                {
                    if (size == 72) ClassicPreferences.DecodeOrdinary(pattern.AsSpan(0, length)); else ClassicPreferences.DecodeMultiplayer(pattern.AsSpan(0, length));
                    throw new InvalidOperationException("Truncated preference item was admitted");
                }
                catch (DecodeException error) { Equal(length, error.Offset); }
            }
            if (size == 72) Throws<DecodeException>(() => ClassicPreferences.DecodeOrdinary(new byte[73])); else Throws<DecodeException>(() => ClassicPreferences.DecodeMultiplayer(new byte[445]));
        }
    }
    private static void BinaryPreferencePrefixes()
    {
        foreach (int size in new[] { 72, 444 })
        {
            var original = Enumerable.Range(0, size).Select(i => unchecked((byte)(i * 31 + 3))).ToArray(); var incoming = Enumerable.Range(0, size).Select(i => unchecked((byte)(i * 11 + 171))).ToArray();
            var ordinary = size == 72 ? ClassicPreferences.DecodeOrdinary(original) : new StoredOrdinaryPreferences();
            var multiplayer = size == 444 ? ClassicPreferences.DecodeMultiplayer(original) : new StoredMultiplayerPreferences();
            for (int length = 0; length <= size; ++length) foreach (bool complete in new[] { false, true })
            {
                var item = new ReadResult(new(incoming.Take(length)), complete);
                byte[] Run() => size == 72 ? ClassicPreferences.EncodeOrdinary(ClassicPreferences.DecodeOrdinaryItem(item, ordinary)) : ClassicPreferences.EncodeMultiplayer(ClassicPreferences.DecodeMultiplayerItem(item, multiplayer));
                if (complete && length != size) { Throws<ArgumentException>(() => Run()); continue; }
                var expected = original.ToArray(); incoming.AsSpan(0, length).CopyTo(expected); Equal(new OwnedList<byte>(expected), new(Run()));
            }
            var oversized = new ReadResult(new(new byte[size + 1]));
            if (size == 72) Throws<ArgumentException>(() => ClassicPreferences.DecodeOrdinaryItem(oversized, ordinary));
            else Throws<ArgumentException>(() => ClassicPreferences.DecodeMultiplayerItem(oversized, multiplayer));
            Equal(new OwnedList<byte>(original), new(size == 72 ? ClassicPreferences.EncodeOrdinary(ordinary) : ClassicPreferences.EncodeMultiplayer(multiplayer)));
        }
    }
    private static void BinaryPreferenceLiveConversion()
    {
        int[] flagOffsets = [0x0e, 0x10, 0x12, 0x14, 0x16, 0xc2, 0xc4, 0xc6, 0xc8];
        foreach (ushort flag in new ushort[] { 0, 1, 2, 0x8000, 0xffff })
        {
            var bytes = Enumerable.Repeat((byte)0xab, 444).ToArray(); foreach (int at in flagOffsets) PreferenceWord(bytes, at, flag);
            var stored = ClassicPreferences.DecodeMultiplayer(bytes); var live = StateConversion.ToLive(stored);
            foreach (bool value in new[] { live.OpenGame, live.KillOnRetirement, live.ShowHumanStarts, live.ChatFromStart, live.Simultaneous, live.ClearChatNewGame, live.ClearChatBoot, live.DoubleProduction, live.DoubleMovement }) Equal(flag != 0, value);
            Equal(0xabab, (int)live.Protocol); Equal(stored.Parley, live.Parley); Equal(stored.Chat, live.Chat);
            var exported = StateConversion.ToStored(live); var output = ClassicPreferences.EncodeMultiplayer(exported);
            foreach (int at in flagOffsets) Equal((short)(flag == 0 ? 0 : 1), BinaryPrimitives.ReadInt16LittleEndian(output.AsSpan(at)));
            Equal((short)0, exported.LegacySetting1); Equal((short)0, exported.LegacySetting5); Equal(true, exported.LegacySettings.Concat(exported.Settings).All(row => row.All(b => b == 0)));
            var preserved = StateConversion.PreserveOpaque(exported, stored); Equal(stored.LegacySetting1, preserved.LegacySetting1); Equal(stored.LegacySetting5, preserved.LegacySetting5); Equal(stored.LegacySettings, preserved.LegacySettings); Equal(stored.Settings, preserved.Settings);
        }
        var ordinary = new OrdinaryPreferences(); ordinary.SetDefaults(1000, true); byte[] expected = new byte[72];
        PreferenceWord(expected, 0, 0x3f); PreferenceWord(expected, 2, 2); PreferenceWord(expected, 10, 6); PreferenceWord(expected, 12, 4); PreferenceDword(expected, 14, 0x3f3258); PreferenceWord(expected, 22, 1); PreferenceWord(expected, 24, 2);
        Equal(new OwnedList<byte>(expected), new(ClassicPreferences.EncodeOrdinary(ordinary)));
        var multi = new MultiplayerPreferences(); multi.SetDefaults(); expected = new byte[444]; expected[0x9a] = (byte)'6'; expected[0x9b] = (byte)'0';
        Equal(new OwnedList<byte>(expected), new(ClassicPreferences.EncodeMultiplayer(multi)));
    }
    private static void BinaryPreferenceTextAndShape()
    {
        var bytes = Enumerable.Repeat((byte)0x81, 72).ToArray(); bytes[36] = (byte)'A'; bytes[37] = 0;
        var stored = ClassicPreferences.DecodeOrdinary(bytes); var live = StateConversion.ToLive(stored);
        Equal(new OwnedList<byte>([(byte)'A']), live.Text); Equal((byte)0x81, stored.Text[2]); var output = StateConversion.ToStored(live); Equal(true, output.Text.Skip(1).All(b => b == 0));
        live.Text = new(new byte[] { 65, 0, 66 }); Throws<ArgumentException>(() => StateConversion.ToStored(live));
        live.Text = new(Enumerable.Repeat((byte)65, 33)); Throws<ArgumentException>(() => StateConversion.ToStored(live));
        live.Text = new(Enumerable.Repeat((byte)65, 32)); Equal(32, StateConversion.ToStored(live).Text.Count);
        Throws<ArgumentException>(() => ClassicPreferences.EncodeOrdinary(stored with { Text = new(new byte[31]) }));
        Throws<ArgumentException>(() => StateConversion.ToLive(stored with { Text = new(new byte[33]) }));
        Throws<ArgumentException>(() => ClassicPreferences.EncodeOrdinary(stored with { WorldParameters = new(new short[4]) }));
        Throws<ArgumentException>(() => StateConversion.ToLive(stored with { WorldParameters = new(new short[6]) }));
        var multi = new StoredMultiplayerPreferences();
        foreach (var bad in new[] { multi with { GameName = new(new byte[31]) }, multi with { PlayerName = new(new byte[31]) }, multi with { EmailAddress = new(new byte[33]) }, multi with { IpAddress = new(new byte[0]) }, multi with { CustomTimer = new(new byte[7]) } })
        { Throws<ArgumentException>(() => ClassicPreferences.EncodeMultiplayer(bad)); Throws<ArgumentException>(() => StateConversion.ToLive(bad)); }
        Throws<ArgumentException>(() => ClassicPreferences.EncodeMultiplayer(multi with { LegacySettings = new(new[] { new OwnedList<byte>(new byte[24]) }) }));
        Throws<ArgumentException>(() => ClassicPreferences.EncodeMultiplayer(multi with { Settings = new(Enumerable.Repeat(new OwnedList<byte>(new byte[23]), 7)) }));
        var full = StateConversion.ToLive(ClassicPreferences.DecodeMultiplayer(Enumerable.Repeat((byte)0x81, 444).ToArray())); Equal(6, full.CustomTimer.Count); Equal(32, full.GameName.Count);
        full.CustomTimer = new(Enumerable.Repeat((byte)'1', 7)); Throws<ArgumentException>(() => StateConversion.ToStored(full));
    }
}
