using OpenCiv2.Core;
using OpenCiv2.Core.State;
using OpenCiv2.Formats;
using OpenCiv2.Formats.Classic;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // S02 §§3–6 authored grammar cases; F03 reader and actual S02/F06 load children.
    private static ClassicEventParser EventParser(OpenCiv2.Formats.Classic.TextReader reader) =>
        new(reader, i => "Civ" + i, i => "Unit" + i, ClassicText.Compare(TextCase.Ascii.Equal));
    private static (EventParseResult Result, EventCollection Events) ParseEvents(string text, EventCollection? events = null)
    {
        var input = new ClassicTextInput(); input.Files["scenario/EVENTS.TXT"] = "@BEGINEVENTS\n" + text;
        using var reader = input.Reader(); events ??= new();
        return (EventParser(reader).Read(events), events);
    }
    private static void ClassicEventTriggers()
    {
        var (result, events) = ParseEvents("""
            @IF
            UnitKilled
            Unit=AnyUnit
            Attacker=Anybody
            Defender=TriggerDefender
            Unit=unit61
            @ENDIF
            @IF
            CityTaken
            City=
            Defender=Civ7
            Attacker=Civ0
            @ENDIF
            @IF
            Turn
            Turn=Every
            @ENDIF
            @IF
            TurnInterval
            Interval=0D-2
            @ENDIF
            @IF
            RandomTurn
            Denominator=-1
            Denominator=1000
            @ENDIF
            @IF
            Negotiation
            Talker=Civ1
            Listener=Anybody
            TalkerType=Human
            ListenerType=HumanOrComputer
            @ENDIF
            @IF
            ScenarioLoaded
            @ENDIF
            @IF
            NoSchism
            Defender=TriggerAttacker
            @ENDIF
            @IF
            ReceivedTechnology
            Receiver=TriggerReceiver
            Technology=0XFFFFFFFF
            @ENDIF
            @ENDEVENTS
            """);
        Equal(true, result.Success); Equal(9, events.Count);
        Equal(EventTrigger.UnitKilled, events.At(0).Values.Trigger); Equal(61, events.At(0).Values.UnitType);
        Equal(-2, events.At(0).Values.WhoCiv); Equal(-4, events.At(0).Values.WhomCiv); Equal("unit61", events.At(0).Strings[0].Value);
        Equal(EventTrigger.CityTaken, events.At(1).Values.Trigger); Equal("", events.At(1).Strings[1].Value); Equal(7, events.At(1).Values.WhomCiv);
        Equal(-1, events.At(2).Values.Turn); Equal(-2, events.At(3).Values.Turn); Equal(1000, events.At(4).Values.Denominator);
        Equal(EventTrigger.Negotiation, events.At(5).Values.Trigger); Equal(true, events.At(5).Values.DenyNegotiation);
        Equal(1, events.At(5).Values.TalkerType); Equal(4, events.At(5).Values.ListenerType);
        Equal(EventTrigger.ScenarioLoaded, events.At(6).Values.Trigger); Equal(-3, events.At(7).Values.WhomCiv);
        Equal(EventTrigger.ReceivedTechnology, events.At(8).Values.Trigger); Equal(-1, events.At(8).Values.Technology); Equal(-4, events.At(8).Values.WhomCiv);
    }
    private static void ClassicEventActions()
    {
        var (result, events) = ParseEvents("""
            @IF
            ScenarioLoaded
            @THEN
            ChangeTerrain
            TerrainType=0D-1
            MAPRECT
            65537,-65538,3,4,5,6,7,8
            CreateUnit
            Unit=AnyUnit
            Owner=Anybody
            Veteran=True
            HomeCity=
            LOCATIONS
            4,6
            ENDLOCATIONS
            ChangeMoney
            Receiver=Civ1
            Amount=0XFFFFFFFF
            JustOnce
            DontPlayWonders
            MakeAggression
            Who=Civ2
            Whom=Civ3
            DestroyACivilization
            Whom=TriggerDefender
            GiveTechnology
            Receiver=TriggerReceiver
            Technology=0B101
            MoveUnit
            Unit=Unit4
            Owner=Civ5
            MAPRECT
            2,4,6,8,10,12,14,16
            MOVETO
            9,11
            NumberToMove=All
            PlayCDTrack
            3,ignored
            PlayCDTrack
            0
            PlayWaveFile
            @authored.wav
            Text
            first
            ENDTEXT
            @ENDIF
            """);
        Equal(true, result.Success); var e = events.At(0); var v = e.Values;
        Equal(true, v.Actions.ChangeTerrain && v.Actions.CreateUnit && v.Actions.ChangeMoney && v.Actions.JustOnce);
        Equal(true, v.Actions.SuppressWonderMovies && v.Actions.MakeAggression && v.Actions.DestroyCivilization && v.Actions.GiveTechnology);
        Equal(true, v.Actions.MoveUnit && v.Actions.PlayCdTrack && v.Actions.PlayWaveFile && v.Actions.Text); Equal(false, v.Actions.Consumed);
        Equal(-1, v.TerrainType); Equal(new OpenCiv2.Contracts.MapPoint(65537, -65538), v.TerrainRectangle[0]);
        Equal(65537, StateConversion.ToStored(e).TerrainRectangle[0]);
        Equal(-2, v.CreateUnitType); Equal(-2, v.CreateOwnerCiv); Equal(true, v.CreateVeteran); Equal(1, v.CreateLocationCount);
        Equal("", e.Strings[30].Value); Equal("AnyUnit", e.Strings[29].Value); Equal("Anybody", e.Strings[28].Value);
        Equal(-1, v.MoneyAmount); Equal(1, v.MoneyReceiverCiv); Equal("Civ1", e.Strings[33].Value);
        Equal(2, v.AggressionWhoCiv); Equal(3, v.AggressionWhomCiv); Equal("Civ2", e.Strings[27].Value); Equal("Civ3", e.Strings[26].Value);
        Equal(-4, v.DestroyCiv); Equal(-4, v.GiveTechCiv); Equal(5, v.GiveTech);
        Equal(4, v.MoveUnitType); Equal(5, v.MoveOwnerCiv); Equal(-2, v.MoveCount); Equal(new OpenCiv2.Contracts.MapPoint(9, 11), v.MoveTo);
        Equal("Unit4", e.Strings[25].Value); Equal("Civ5", e.Strings[24].Value); Equal(0, v.CdTrack); Equal("@authored.wav", e.Strings[34].Value);
        Equal("first", e.Strings[4].Value);
    }
    private static void ClassicEventSharedRecognition()
    {
        var (result, events) = ParseEvents("""
            @IF
            Turn
            Turn=9
            Turn
            ignored
            Turn=invalid
            TurnInterval
            Interval=4
            TurnInterval
            ignored
            @THEN
            MoveUnit
            Unit=Unit5
            Owner=Civ6
            NumberToMove=0D-7
            MOVETO
            -1,2
            MAPRECT
            0,0,2,0,2,2,0,2
            CreateUnit
            HomeCity=None
            Veteran=No
            LOCATIONS
            6,8
            ENDLOCATIONS
            ChangeMoney
            Receiver=Civ1
            Amount=7
            ChangeMoney
            Amount=99
            @ENDIF
            @IF
            NoSchism
            Defender=Civ1
            ReceivedTechnology
            Technology=9
            @ENDIF
            """);
        Equal(true, result.Success); Equal(2, events.Count); var v = events.At(0).Values;
        Equal(EventTrigger.TurnInterval, v.Trigger); Equal(4, v.Turn); Equal(-7, v.MoveCount);
        Equal(true, v.Actions.CreateUnit); Equal(0, v.CreateUnitType); Equal(0, v.CreateOwnerCiv); Equal(false, events.At(0).Strings[28].Present);
        Equal(5, v.MoveUnitType); Equal(6, v.MoveOwnerCiv); Equal(7, v.MoneyAmount);
        Equal(EventTrigger.ReceivedTechnology, events.At(1).Values.Trigger); Equal(1, events.At(1).Values.WhomCiv);
    }
    private static void ClassicEventTextAndFailures()
    {
        string lines = string.Join('\n', Enumerable.Range(0, 22).Select(i => "Line" + i));
        var (result, events) = ParseEvents("@IF\n@THEN\nText\n" + lines + "\nENDTEXT\nText\n@first\nENDTEXT\nText\nENDTEXT\n@ENDIF");
        Equal(true, result.Success); Equal("@first", events.At(0).Strings[4].Value); Equal("Line19", events.At(0).Strings[23].Value);
        (result, events) = ParseEvents("@IF\n@THEN\nText\nkept\n@ENDIF\n");
        Equal("events-directive-in-helper", result.Code); Equal("kept", events.At(0).Strings[4].Value); Equal(false, events.At(0).Values.Actions.Text);
        (result, events) = ParseEvents("@IF\nRandomTurn\nDenominator=0D-1\n");
        Equal("events-invalid-denominator", result.Code); Equal(-1, events.At(0).Values.Denominator); Equal(EventTrigger.None, events.At(0).Values.Trigger);
        (result, events) = ParseEvents("@THEN\nJustOnce\n"); Equal("events-missing-if", result.Code); Equal(0, events.Count);
        (result, events) = ParseEvents("@IF\n@THEN\nPlayCDTrack\n"); Equal("events-incomplete-helper", result.Code); Equal(false, events.At(0).Values.Actions.PlayCdTrack);
        (result, events) = ParseEvents("@IF\n@IF\nunknown\n"); Equal(true, result.Success); Equal(2, events.Count);
    }
    private static void ClassicEventLocationsAndRectangles()
    {
        var (result, events) = ParseEvents("""
            @IF
            @THEN
            CreateUnit
            Unit=Unit1
            Owner=Civ1
            HomeCity=None
            LOCATIONS
            invalid
            4,6
            @ignored
            ENDLOCATIONS
            LOCATIONS
            8,10
            ENDLOCATIONS
            Veteran=Yes
            ChangeTerrain
            MAPRECT
            @not-an-outer-directive
            TerrainType=11
            @ENDIF
            """);
        Equal(true, result.Success); var v = events.At(0).Values;
        Equal(2, v.CreateLocationCount); Equal(new OpenCiv2.Contracts.MapPoint(8, 10), v.CreateLocations[0]);
        Equal(new OpenCiv2.Contracts.MapPoint(4, 6), v.CreateLocations[1]); Equal(default(OpenCiv2.Contracts.MapPoint), v.TerrainRectangle[3]);
        Equal(11, v.TerrainType);
        (result, events) = ParseEvents("@IF\n@THEN\nChangeTerrain\nMAPRECT\n1,2\nMAPRECT\n3,4\nTerrainType=2\n@ENDIF\n");
        Equal("events-directive-in-helper", result.Code); Equal(false, events.At(0).Values.Actions.ChangeTerrain);
        Equal(new OpenCiv2.Contracts.MapPoint(3, 4), events.At(0).Values.TerrainRectangle[0]);
        string ten = string.Join('\n', Enumerable.Range(0, 10).Select(i => $"{i * 2},2"));
        (result, events) = ParseEvents("@IF\n@THEN\nCreateUnit\nUnit=Unit1\nOwner=Civ1\nHomeCity=\nVeteran=No\nLOCATIONS\n" + ten + "\nJustOnce\n");
        Equal(true, result.Success); Equal(10, events.At(0).Values.CreateLocationCount); Equal(true, events.At(0).Values.Actions.JustOnce);
    }
    private static void ClassicEventReaderBoundaries()
    {
        var input = new ClassicTextInput(); input.Files["home/EVENTS.TXT"] = "ignored\n@BEGINEVENTS\n@IF\n@DEBUG\n@ENDEVENTS\ntail\n";
        using var reader = input.Reader(); var events = new EventCollection(); events.Append().Values.Turn = 123;
        Equal(true, EventParser(reader).Read(events).Success); Equal(2, events.Count); Equal(123, events.At(0).Values.Turn);
        Equal(true, reader.IsOpen); Equal(true, reader.NextLine()); Equal("tail", ClassicText.DecodeWindows1252(reader.Line)); Equal(false, reader.NextLine()); Equal(false, reader.IsOpen);
        Equal(false, EventParser(reader).Read(events, section: "missing").Success); Equal(2, events.Count);
        input.Files["scenario/EVENTS.TXT"] = "@BEGINEVENTS\n@IF\n@THEN\nChangeMoney\nReceiver =Civ1\nReceiver= Civ1\nReceiver=Civ1\nAmount=+\n";
        Equal(true, EventParser(reader).Read(events).Success); Equal(3, events.Count); Equal(1, events.At(2).Values.MoneyReceiverCiv); Equal(0, events.At(2).Values.MoneyAmount);
        Equal(false, reader.IsOpen);
    }
    private static void ClassicEventParsedDispatch()
    {
        var f = new EventFixture(); int gold = f.World.Civilizations[1].Gold;
        f.World.Names[1].Tribe = "Civ1";
        var input = new ClassicTextInput(); input.Files["scenario/EVENTS.TXT"] = "@BEGINEVENTS\n@IF\nTurn\nTurn=Every\n@THEN\nJustOnce\nText\nParsed event\nENDTEXT\nChangeMoney\nReceiver=Civ1\nAmount=19\n@ENDIF\n";
        using var reader = input.Reader();
        var parser = new ClassicEventParser(reader, f.World, f.Data.Rules, ClassicText.Compare(TextCase.Ascii.Equal));
        var result = parser.Read(f.World.Events.Collection);
        Equal(true, result.Success);
        using var op = f.Triggers.BeginTurn(3, "parsed"); Equal(true, op.Resume() is ScenarioText);
        Equal(gold + 19, f.World.Civilizations[1].Gold); Equal("Parsed event", f.Texts.Single()[0]);
        Equal(false, f.World.Events.Collection.At(0).Values.Actions.Consumed); Equal(null, op.Resume(new(0)));
        Equal(true, f.World.Events.Collection.At(0).Values.Actions.Consumed);
        using var repeat = f.Triggers.BeginTurn(4, "repeat"); FinishTrigger(repeat, 1); Equal(gold + 19, f.World.Civilizations[1].Gold); f.Consumed();
    }
    private static void ClassicEventLiveNames()
    {
        var f = new EventFixture(); f.World.Names[0].Tribe = "Anybody";
        f.World.Names[1].Tribe = f.World.Names[2].Tribe = "Duplicate";
        f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Identity = new("u0", "Shared"), Prerequisite = OpenCiv2.Core.Rules.AdvanceReference.Never };
        f.Data.Rules.Units[1] = f.Data.Rules.Units[1] with { Identity = new("u1", "Shared") };
        var input = new ClassicTextInput(); input.Files["scenario/EVENTS.TXT"] = "@BEGINEVENTS\n@IF\nUnitKilled\nUnit=sHaReD\nAttacker=Anybody\nDefender=Duplicate\n@ENDIF\n";
        using var reader = input.Reader(); var parser = new ClassicEventParser(reader, f.World, f.Data.Rules, ClassicText.Compare(TextCase.Ascii.Equal));
        var events = new EventCollection(); Equal(true, parser.Read(events).Success);
        Equal(0, events.At(0).Values.UnitType); Equal(-2, events.At(0).Values.WhoCiv); Equal(1, events.At(0).Values.WhomCiv);
        f.World.Names[1].Tribe = "Changed"; f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Identity = new("u0", "Changed") };
        Equal(true, parser.Read(events).Success); Equal(1, events.At(1).Values.UnitType); Equal(2, events.At(1).Values.WhomCiv);
        Equal(1, events.At(0).Values.WhomCiv); Equal("Duplicate", events.At(0).Strings[3].Value);
    }
    private static void ClassicEventParsedLoad()
    {
        using var f = new RetainedLoadFixture(); var input = new ClassicTextInput();
        input.Files["scenario/EVENTS.TXT"] = "@BEGINEVENTS\n@IF\nScenarioLoaded\n@THEN\nChangeMoney\nReceiver=Civ1\nAmount=23\nText\nActual parsed load\nENDTEXT\nJustOnce\n@ENDIF\n";
        using var reader = input.Reader(); f.Load.Services = f.Load.Services with { LoadEvents = () => EventParser(reader).ReadCatalog() };
        using var op = f.Begin(); Equal(true, op.Resume() is ScenarioText); Equal(33, f.World.Civilizations[1].Gold);
        Equal("Actual parsed load", f.Events.Texts.Single()[0]); Equal(false, f.Load.File.Trace.Contains("music"));
        Equal(null, op.Resume(new(0))); Equal(true, op.Completed); Equal((CoreLoadStatus?)CoreLoadStatus.Success, op.Result);
        Equal(true, f.World.Events.Loaded); Equal(true, f.World.Events.Collection.At(0).Values.Actions.Consumed); Equal(true, f.Load.File.Trace.Contains("music"));
        input.Files["scenario/EVENTS.TXT"] = "@BEGINEVENTS\n@IF\nRandomTurn\nDenominator=0\n";
        Throws<InvalidDataException>(() => EventParser(reader).ReadCatalog());
    }
}
