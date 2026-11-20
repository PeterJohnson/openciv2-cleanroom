using OpenCiv2.Base;
using OpenCiv2.Core.State;
using OpenCiv2.Formats;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // F06 §§3–4: explicit semantic flags/text versus lossless stored images.
    private static void NamesLiveConversion()
    {
        var stored = ClassicRecords.DecodeStoredCivilizationNames(Enumerable.Repeat((byte)0xa5, 242).ToArray()); var live = StateConversion.ToLive(stored);
        Equal(stored, StateConversion.ToStored(live)); Equal(24, live.Leader.Length); Equal(24, live.Titles[6].Length);
        var names = new CivilizationNames { CityStyle = -1, Leader = "\u00e9", Tribe = "\u20ac", Adjective = "\u00ff" };
        var encoded = ClassicRecords.Encode(StateConversion.ToStored(names)); Equal((byte)0xff, encoded[0]); Equal((byte)0xff, encoded[1]); Equal((byte)0xe9, encoded[2]); Equal((byte)0, encoded[3]);
        Equal(names, StateConversion.ToLive(ClassicRecords.DecodeStoredCivilizationNames(encoded)));
        byte[] tail = Enumerable.Repeat((byte)0x7f, 24).ToArray(); tail[0] = 65; tail[1] = 0; var decoded = StateConversion.ToLive(stored with { Leader = new(tail) }); Equal("A", decoded.Leader);
        Equal(true, StateConversion.ToStored(decoded).Leader.Skip(1).All(b => b == 0));
        names.Leader = ClassicText.DecodeWindows1252((Enumerable.Repeat((byte)65, 25)).ToArray()); Throws<ArgumentException>(() => StateConversion.ToStored(names));
        names.Leader = "A\u0000B"; Throws<ArgumentException>(() => StateConversion.ToStored(names));
        Throws<ArgumentException>(() => StateConversion.ToLive(stored with { Titles = new(stored.Titles.Take(6)) }));
        live.Titles = new(live.Titles.Take(6)); Throws<ArgumentException>(() => StateConversion.ToStored(live));
    }
    private static void ScenarioHistoryLiveConversion()
    {
        var scenario = ClassicRecords.DecodeScenarioParameters(Pattern(100)); var liveScenario = StateConversion.ToLive(scenario); Equal(scenario, StateConversion.PreserveOpaque(StateConversion.ToStored(liveScenario), scenario));
        liveScenario.Name = ClassicText.DecodeWindows1252((Enumerable.Repeat((byte)65, 81)).ToArray()); Throws<ArgumentException>(() => StateConversion.ToStored(liveScenario));
        Throws<ArgumentException>(() => StateConversion.ToLive(scenario with { VictoryThresholds = new([1, 2, 3]) }));
        var history = ClassicRecords.DecodeEliminationHistory(Enumerable.Repeat((byte)0xa5, 338).ToArray()); var live = StateConversion.ToLive(history); Equal(history, StateConversion.ToStored(live));
        Equal(unchecked((short)0xa5a5), live.Count); Equal(24, live.Names[11].Length); var snapshot = StateConversion.ToStored(live); live.Turns[11] = 0; Equal(history.Turns[11], snapshot.Turns[11]);
        live.Names = new(live.Names.Take(11)); Throws<ArgumentException>(() => StateConversion.ToStored(live));
        Throws<ArgumentException>(() => StateConversion.ToLive(history with { Names = new(history.Names.Take(11)) }));
        Throws<ArgumentException>(() => StateConversion.ToLive(history with { Killers = new(new byte[13]) }));
    }
    private static void MultiplayerLiveConversion()
    {
        var stored = ClassicRecords.DecodeMultiplayerRecord(Enumerable.Repeat((byte)0xa5, 1172).ToArray()); var live = StateConversion.ToLive(stored);
        var expected = stored with
        {
            Introduced = new(Enumerable.Repeat((short)1, 8)),
            OpenGame = 1,
            KillOnRetirement = 1,
            ShowHumanStarts = 1,
            ChatFromStart = 1,
            Simultaneous = 1,
            AutoplayActive = 1,
            DoubleProduction = 1,
            DoubleMovement = 1,
            Retained142 = new(new byte[290]),
            Retained465 = 0,
            Retained472 = new(new byte[34]),
            PlayerSetup = new(Enumerable.Range(0, 8).Select(_ => new OwnedList<byte>(new byte[32]))),
        };
        Equal(expected, StateConversion.ToStored(live)); var retained = StateConversion.PreserveOpaque(expected, stored);
        Equal(stored with { Introduced = expected.Introduced, OpenGame = 1, KillOnRetirement = 1, ShowHumanStarts = 1, ChatFromStart = 1, Simultaneous = 1, AutoplayActive = 1, DoubleProduction = 1, DoubleMovement = 1 }, retained);
        Equal(live, StateConversion.ToLive(ClassicRecords.DecodeMultiplayerRecord(ClassicRecords.Encode(retained))));
        var snapshot = StateConversion.ToStored(live); live.Passwords = new(Enumerable.Repeat("changed", 8)); Equal((byte)0xa5, snapshot.EncodedPasswords[255]); live.Introduced[7] = false; Equal((short)0, StateConversion.ToStored(live).Introduced[7]);
        Throws<ArgumentException>(() => StateConversion.ToLive(stored with { Introduced = new(new short[7]) }));
        Throws<ArgumentException>(() => StateConversion.ToLive(stored with { EncodedPasswords = new(new byte[255]) }));
        live.Emails = new(Enumerable.Repeat(new string('a', 33), 8)); Throws<ArgumentException>(() => StateConversion.ToStored(live));
    }
    private static void MapViewLiveConversion()
    {
        var stored = new SavedMapView { Open = short.MinValue, TrackMoves = 19, X = -1, Y = short.MinValue, Zoom = -7 }; var live = StateConversion.ToLive(stored);
        Equal(true, live.Open); Equal(true, live.TrackMoves); Equal(stored with { Open = 1, TrackMoves = 1 }, StateConversion.ToStored(live));
        Equal(live, StateConversion.ToLive(ClassicRecords.DecodeSavedMapView(ClassicRecords.Encode(StateConversion.ToStored(live)))));
        live.Open = false; live.TrackMoves = false; Equal((short)0, StateConversion.ToStored(live).Open); Equal((short)0, StateConversion.ToStored(live).TrackMoves);
    }
    private static void EventValuesLiveConversion()
    {
        var stored = ClassicRecords.DecodeEventRecord(Pattern(444)); var live = StateConversion.ToLive(stored);
        var expected = stored with
        {
            Triggers = 0,
            Actions = stored.Actions & StateConversion.EventActionsMask,
            UnitName = 0,
            CityName = 0,
            WhoName = 0,
            WhomName = 0,
            Text = new(new uint[20]),
            MoveOwnerName = 0,
            MoveUnitName = 0,
            AggressionWhomName = 0,
            AggressionWhoName = 0,
            CreateOwnerName = 0,
            CreateUnitName = 0,
            CreateHomecity = 0,
            AuxiliaryCivName = 0,
            AuxiliaryUnitName = 0,
            MoneyReceiverName = 0,
            WaveFile = 0,
            Retained = new(new byte[40]),
            CreateVeteran = 1,
            NegotiationFlag = 1,
        };
        Equal(expected, StateConversion.ToStored(live)); Equal(live with { Trigger = EventTrigger.None }, StateConversion.ToLive(ClassicRecords.DecodeEventRecord(ClassicRecords.Encode(expected))));
        var snapshot = StateConversion.ToStored(live); live.CreateLocations[9] = default; Equal(stored.CreateLocations[19], snapshot.CreateLocations[19]);
        live.CreateVeteran = false; live.NegotiationFlag = false; Equal(0, StateConversion.ToStored(live).CreateVeteran); Equal(0, StateConversion.ToStored(live).NegotiationFlag);
        Throws<ArgumentException>(() => StateConversion.ToLive(stored with { TerrainRectangle = new(new int[9]) }));
        Throws<ArgumentException>(() => StateConversion.ToLive(stored with { CreateLocations = new(new int[19]) }));
    }
}
