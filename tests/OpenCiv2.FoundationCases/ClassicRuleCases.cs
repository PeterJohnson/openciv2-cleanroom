using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Formats.Classic;
using ClassicReader = OpenCiv2.Formats.Classic.TextReader;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static string RepeatLines(string line, int count) => string.Concat(Enumerable.Repeat(line + "\n", count));
    // Independently authored external F03 records, not native-fixture serialization.
    private static string[] ClassicRuleSections()
    {
        string[] s = ["@COSMIC\n3\n2\n2\n5\n5\n1\n2\n7\n14\n8\n12\n10\n20\n3\n3\n8\n1\n50\n50\n10\n75\n5\n", "@CIVILIZE\n", "@IMPROVE\n", "@ENDWONDER\n", "@UNITS\n", "@TERRAIN\n", "@GOVERNMENTS\n", "@LEADERS\n", "@CARAVAN\n", "@ORDERS\n", "@DIFFICULTY\n", "@ATTITUDES\n"];
        for (int i = 0; i < 100; ++i) s[1] += $"advances {i},3,1,nil,nil,0,3\n";
        for (int i = 0; i < 67; ++i) s[2] += $"improvements {i},10,1,nil\n";
        s[3] += RepeatLines("nil", 28);
        for (int i = 0; i < 62; ++i) s[4] += $"units {i},no,0,2,0,1,1,3,1,10,0,0,nil,0\n";
        for (int i = 0; i < 33; ++i) s[5] += $"terrain {i},1,3,1,1,0" + (i < 11 ? ",yes,1,3,2,no,0,4,1,Drt" : "") + "\n";
        for (int i = 0; i < 7; ++i) s[6] += $"governments {i},Leader,Leader\n";
        for (int i = 0; i < 21; ++i) s[7] += $"Leader,Leader,1,1,0,tribes {i},Example,0,0,0\n";
        for (int i = 0; i < 16; ++i) s[8] += $"commodities {i}\n";
        for (int i = 0; i < 12; ++i) s[9] += $"orders {i},x\n";
        for (int i = 0; i < 6; ++i) s[10] += $"difficulties {i}\n";
        for (int i = 0; i < 9; ++i) s[11] += $"diplomaticAttitudes {i}\n";
        return s;
    }
    private static void ClassicRulesComplete()
    {
        var native = new RulesInput().Load().Input; var state = Installed(native); var expected = state.Definitions.Snapshot();
        state.Names.Orders[0] = 777; state.Leaders[0].TimesUsed = 19; state.Leaders[0].CityNameIndex = 23; state.DoubleMovementApplied = true;
        var f = new ClassicTextInput(); f.Files["scenario/RULES.TXT"] = string.Concat(ClassicRuleSections().Reverse());
        using var reader = f.Reader(); var loader = new RuleLoader(reader, TextCase.Ascii); var input = loader.Load("rules");
        Equal("", input.Definitions.Units[0].Identity.Id); Equal(2, input.MovementPoints[0]); Equal((byte)30, input.Definitions.Units[0].HitPoints);
        new RuleInstaller(input, state).Load(true); Equal(expected, state.Definitions.Snapshot());
        Equal(new OwnedList<string>(Enumerable.Repeat("scenario/RULES.TXT", 3)), new(f.Opens)); Equal(false, reader.IsOpen); Equal("caller", f.Directories.Working);
        Equal(777U, state.Names.Orders[0]); Equal((byte)19, state.Leaders[0].TimesUsed); Equal((byte)23, state.Leaders[0].CityNameIndex); Equal(false, state.DoubleMovementApplied);
        Equal(state.Leaders[0].Names[1], state.Leaders[0].LeaderName); Equal("tribes 0", ByteString(state.Text.Text(state.Leaders[0].Plural.Id)));
        f.Files.Clear(); Equal("units 61", input.Definitions.Units[61].Identity.Name.Unicode);
    }
    private static void ClassicRuleOperandsAndReload()
    {
        var s = ClassicRuleSections(); s[1] = "@CIVILIZE\nDisabled,3,1,no,nil,0,3\nDependent,3,1,AFl,nil,0,3\n; stop\n";
        s[2] = "@IMPROVE\nBuilding,10,1,AFl\n" + RepeatLines("Building,10,1,nil", 66);
        s[4] = "@UNITS\nWrapped,no,0,259,0,1,1,3,1,10,0,0,nil,0\n; stop\n";
        s[7] = "@LEADERS\n" + RepeatLines("Leader,Leader,1,1,0,Plural,Example,0,0,0,1,First,First Lady,1,Last,Last Lady", 21);
        var f = new ClassicTextInput(); f.Files["scenario/RULES.TXT"] = string.Concat(s); using var reader = f.Reader(); var loader = new RuleLoader(reader, TextCase.Ascii);
        var input = loader.Load("RULES"); var state = Installed(input); var rules = state.Definitions;
        Equal(0, (int)input.Definitions.Advances[1].Prerequisites[0]); Equal(-2, (int)rules.Advances[1].Prerequisites[0]); Equal(0, (int)input.Definitions.Improvements[0].Prerequisite); Equal(-2, (int)rules.Improvements[0].Prerequisite);
        Equal(259, input.MovementPoints[0]); Equal((byte)9, rules.Units[0].Movement); Equal((byte)30, rules.Units[0].HitPoints); Equal(0, input.MovementPoints[1]); Equal("Unit 2", rules.Units[1].Identity.Name.Unicode);
        Equal(2, input.TitleOverrides[0].Count); Equal("First", input.TitleOverrides[0][0].Titles.Male.Unicode); Equal("Last", input.TitleOverrides[0][1].Titles.Male.Unicode); Equal("Last", rules.Tribes[0].Titles[1].Male.Unicode); Equal(rules.Governments[2].Titles, rules.Tribes[0].Titles[2]);
        f.Files["scenario/RULES.TXT"] = string.Concat(ClassicRuleSections()); var again = loader.Load("RULES");
        Equal(2, again.MovementPoints[0]); Equal(-1, (int)again.Definitions.Advances[1].Prerequisites[0]); Equal(-1, (int)again.Definitions.Improvements[0].Prerequisite);
        Equal(true, again.TitleOverrides.All(t => t.Count == 0)); Equal(259, input.MovementPoints[0]);
    }
    private static void ClassicRuleVocabularyAndDefaults()
    {
        foreach (var (token, expected) in new (string, int)[] { ("nil", -1), ("no", -2), ("NIL", -3), ("afl", -3), ("nil ; note", -3), ("AFl", 0), ("FP", 32), ("X7", 99) }) Equal(expected, (int)RuleLoader.ResolveAdvance(ByteText(token)));
        foreach (var (token, expected) in new (string, int)[] { ("YES", -2), ("No", -1), ("Drt", 0), ("Oce", 10), ("drt", -3) }) Equal(expected, (int)RuleLoader.ResolveTerrain(ByteText(token), TextCase.Ascii));
        var f = new ClassicTextInput(); f.Files["scenario/RULES.TXT"] = "@CIVILIZE\nA,3,0,Alp,nil,0,0\nB,3,0,no,nil,0,0\nC,3,0,nil,no,0,0\n; end\n@IMPROVE\n" + RepeatLines("Building,-1,258,Alp", 67) + "@ENDWONDER\n" + RepeatLines("Alp,ignored", 28);
        using var reader = f.Reader(); var loader = new RuleLoader(reader, TextCase.Ascii); var state = new RuleState(); state.Text.Initialize();
        Equal(true, reader.Open("rules", ByteText("CIVILIZE"))); loader.Advances(); new RuleInstaller(loader.Snapshot(), state).Advances();
        Equal((ushort)2, state.Definitions.EnabledAdvanceCount); Equal(true, state.Definitions.Advances[0].Enabled); Equal(-2, (int)state.Definitions.Advances[0].Prerequisites[0]); Equal(true, state.Definitions.Advances[2].Enabled);
        Equal("Advance 4", state.Definitions.Advances[3].Identity.Name.Unicode); Equal("Advance 100", state.Definitions.Advances[99].Identity.Name.Unicode); Equal((sbyte)3, state.Definitions.Advances[3].AiValue);
        Equal(true, reader.Open("rules", ByteText("IMPROVE"))); loader.Improvements(); new RuleInstaller(loader.Snapshot(), state).Improvements(); Equal((byte)255, state.Definitions.Improvements[0].CostRows); Equal((byte)2, state.Definitions.Improvements[0].Upkeep); Equal(-2, (int)state.Definitions.Improvements[0].Prerequisite);
        Equal(true, reader.Open("rules", ByteText("ENDWONDER"))); loader.WonderExpiry(); new RuleInstaller(loader.Snapshot(), state).WonderExpiry(); Equal(1, (int)state.Definitions.WonderObsolescence[0]);
        f.Files["scenario/RULES.TXT"] = "@CIVILIZE\nA,3,0,Alp,AFl,0,0\nB,3,0,AFl,nil,0,0\n\n";
        Equal(true, reader.Open("rules", ByteText("CIVILIZE"))); loader.Advances(); new RuleInstaller(loader.Snapshot(), state).Advances(); Equal((ushort)2, state.Definitions.EnabledAdvanceCount); Equal(1, (int)state.Definitions.Advances[0].Prerequisites[0]);
    }
    private static void ClassicRuleRetainedNames()
    {
        var f = new ClassicTextInput(); f.Files["scenario/RULES.TXT"] = "@CIVILIZE\nNew,-129,258,nil,nil,258,259\n\n@IMPROVE\n" + RepeatLines("New,10,1,nil", 67) + "@UNITS\nNew,no,0,43,258,-1,256,13,2,0,257,7,nil,101x11\n\n";
        using var reader = f.Reader(); var loader = new RuleLoader(reader, TextCase.Ascii); var state = Installed(new RulesInput().Load().Input); int count = state.Text.Count;
        var names = new OwnedList<uint>(state.Names.Units); var advanceNames = new OwnedList<uint>(state.Names.Advances);
        Equal(true, reader.Open("rules", ByteText("CIVILIZE"))); loader.Advances(); new RuleInstaller(loader.Snapshot(), state).Advances(true);
        Equal((sbyte)127, state.Definitions.Advances[0].AiValue); Equal((sbyte)2, state.Definitions.Advances[0].CivilizedAiModifier); Equal(2, (int)state.Definitions.Advances[0].Epoch); Equal(3, (int)state.Definitions.Advances[0].Category); Equal("advances 99", state.Definitions.Advances[99].Identity.Name.Unicode);
        Equal(true, reader.Open("rules", ByteText("IMPROVE"))); loader.Improvements(); new RuleInstaller(loader.Snapshot(), state).Improvements(true); Equal("improvements 66", state.Definitions.Improvements[66].Identity.Name.Unicode);
        Equal(true, reader.Open("rules", ByteText("UNITS"))); loader.Units(); new RuleInstaller(loader.Snapshot(), state).Units(true); var unit = state.Definitions.Units[0];
        Equal((byte)129, unit.Movement); Equal((byte)2, unit.Range); Equal((byte)255, unit.Attack); Equal((byte)0, unit.Defense); Equal((byte)130, unit.HitPoints); Equal((byte)2, unit.Firepower); Equal((byte)1, unit.CostRows); Equal((byte)1, unit.Capacity); Equal(7, (int)unit.Role); Equal(5U, unit.Abilities);
        Equal("units 61", state.Definitions.Units[61].Identity.Name.Unicode); Equal((byte)1, state.Definitions.Units[61].Attack); Equal((byte)1, state.Definitions.Units[61].CostRows); Equal(0U, state.Definitions.Units[61].Abilities);
        Equal(names, new(state.Names.Units)); Equal(advanceNames, new(state.Names.Advances)); Equal(count, state.Text.Count);
    }
    private static void RuleError(RuleLoadFault fault, string section, int row, int position, int code, string line, Action action)
    {
        try { action(); }
        catch (RuleLoadException error)
        {
            Equal((int)fault, (int)error.Fault); Equal(section, error.Section); Equal(row, error.Row); Equal(position, error.Position); Equal(code, error.Code); Equal(line, ByteString(error.Line)); Equal(3, RuleLoadException.Module); return;
        }
        throw new InvalidOperationException("Expected classic rule failure");
    }
    private static void ClassicRuleRejection()
    {
        var state = Installed(new RulesInput().Load().Input); var before = state.Definitions.Snapshot(); int count = state.Text.Count;
        (string Section, string Line, int Row, int Position, int Code, string Prefix)[] errors = [
            ("CIVILIZE", "Name,-1,2,NIL,nil,3,4", 0, 0, -4, ""), ("CIVILIZE", "Name,-1,2,nil,NIL,3,4", 0, 1, -4, ""),
            ("IMPROVE", "Bad,-1,2,NIL", 1, 0, -5, "Good,7,8,nil\n"), ("ENDWONDER", "NIL,ignored", 0, 0, -4, ""),
            ("UNITS", "Bad,NIL,2,3,4,5,6,7,8,999,9,1,NIL,101", 0, 0, -6, ""),
            ("TERRAIN", "Land,1,2,3,4,5,bad,6,7,8,NO,9,10,11,Drt", 0, 0, -7, ""),
            ("TERRAIN", "Land,1,2,3,4,5,YES,6,7,8,bad,9,10,11,Drt", 0, 1, -7, ""),
            ("TERRAIN", "Land,1,2,3,4,5,YES,6,7,8,NO,9,10,11,bad", 0, 99, -7, "")];
        foreach (var e in errors)
        {
            var f = new ClassicTextInput(); f.Files["scenario/RULES.TXT"] = "@" + e.Section + "\n" + e.Prefix + e.Line + "\n"; using var reader = f.Reader(); int reports = 0; var loader = new RuleLoader(reader, TextCase.Ascii, _ => ++reports);
            Equal(true, reader.Open("rules", ByteText(e.Section)));
            Action decode = e.Section switch { "CIVILIZE" => loader.Advances, "IMPROVE" => loader.Improvements, "ENDWONDER" => loader.WonderExpiry, "UNITS" => loader.Units, _ => loader.Terrain };
            RuleError(RuleLoadFault.Reference, e.Section, e.Row, e.Position, e.Code, e.Line, () => { decode(); new RuleInstaller(loader.Snapshot(), state).Load(true); });
            Equal(1, reports); Equal(before, state.Definitions.Snapshot()); Equal(count, state.Text.Count);
        }
        var eof = new ClassicTextInput(); eof.Files["scenario/RULES.TXT"] = "@COSMIC\n9\n"; using var eofReader = eof.Reader(); Equal(true, eofReader.Open("rules", ByteText("COSMIC")));
        TextError(TextFault.UnavailableStream, () => { var loader = new RuleLoader(eofReader, TextCase.Ascii); loader.Cosmic(); new RuleInstaller(loader.Snapshot(), state).Cosmic(); }); Equal(before, state.Definitions.Snapshot());
    }
    private static void ClassicRuleTextAndTitles()
    {
        var f = new ClassicTextInput(); f.Files["scenario/RULES.TXT"] = "@GOVERNMENTS\n" + RepeatLines("Government,Default M,Default F", 7) + "@LEADERS\n" + RepeatLines("Male,Female,2,65538,65538,People,Adjective,-129,258,-1,1,A,B,1,C,D,6,,E", 21);
        using var reader = f.Reader(); var loader = new RuleLoader(reader, TextCase.Ascii); var state = Installed(new RulesInput().Load().Input);
        Equal(true, reader.Open("rules", ByteText("GOVERNMENTS"))); loader.Governments(); new RuleInstaller(loader.Snapshot(), state).Governments();
        Equal(true, reader.Open("rules", ByteText("LEADERS"))); loader.Leaders(); new RuleInstaller(loader.Snapshot(), state).Leaders(true); var tribe = state.Definitions.Tribes[0];
        Equal((int)Gender.Female, (int)tribe.PreferredGender); Equal((byte)2, tribe.PreferredColor); Equal((byte)2, tribe.CityStyle); Equal(new OwnedList<sbyte>([127, 2, -1]), tribe.Personality);
        Equal("Default M", tribe.Titles[0].Male.Unicode); Equal("C", tribe.Titles[1].Male.Unicode); Equal("D", tribe.Titles[1].Female.Unicode); Equal("", tribe.Titles[6].Male.Unicode); Equal(0U, state.Leaders[0].Titles[6][0].Id); Equal("E", tribe.Titles[6].Female.Unicode);
        var before = state.Definitions.Snapshot(); var leader = state.Leaders[0].Snapshot(); f.Files["scenario/RULES.TXT"] = "@LEADERS\n" + RepeatLines("New,Other,0,7,3,New People,New Adj,1,2,3,2,King,Queen", 21);
        Equal(true, reader.Open("rules", ByteText("LEADERS"))); loader.Leaders(); new RuleInstaller(loader.Snapshot(), state).Leaders(false); Equal(before, state.Definitions.Snapshot()); Equal(leader, state.Leaders[0].Snapshot());
        const string invalid = "Male,Female,1,1,0,People,Adj,0,0,0,7,X,Y"; f.Files["scenario/RULES.TXT"] = "@LEADERS\n" + invalid + "\n";
        Equal(true, reader.Open("rules", ByteText("LEADERS"))); RuleError(RuleLoadFault.InvalidTitleIndex, "LEADERS", 0, 7, 0, invalid, loader.Leaders);
        f.Files["scenario/RULES.TXT"] = "@ORDERS\n,\n" + RepeatLines("Order, x extra", 11) + "@DIFFICULTY\n" + RepeatLines("  A,B;label\t", 6) + "@ATTITUDES\n" + RepeatLines(";friendly, really", 9) + "@CARAVAN\n\n; commodity\nCaf\xe9\n" + RepeatLines("A commodity name longer than ten", 13);
        Equal(true, reader.Open("rules", ByteText("ORDERS"))); loader.Orders(); Equal(true, reader.Open("rules", ByteText("DIFFICULTY"))); loader.Difficulties(); Equal(true, reader.Open("rules", ByteText("ATTITUDES"))); loader.Attitudes(); Equal(true, reader.Open("rules", ByteText("CARAVAN"))); loader.Commodities(); var decoded = loader.Snapshot().Definitions;
        Equal("", decoded.Orders[0].Identity.Name.Unicode); Equal((byte)0, decoded.Orders[0].Shortcut); Equal((byte)'x', decoded.Orders[11].Shortcut); Equal("A,B;label", decoded.Difficulties[5].Name.Unicode); Equal(";friendly, really", decoded.DiplomaticAttitudes[8].Name.Unicode);
        Equal(" ", decoded.Commodities[0].Name.Unicode); Equal("; commodity", decoded.Commodities[1].Name.Unicode); Equal("Café", decoded.Commodities[2].Name.Unicode); Equal("A commodity name longer than ten", decoded.Commodities[15].Name.Unicode);
    }
    private static void ClassicRuleSelectionFailures()
    {
        var s = ClassicRuleSections(); var f = new ClassicTextInput(); f.Files["scenario/RULES.TXT"] = s[0]; f.Files["home/RULES.TXT"] = string.Concat(s.Skip(1)); using var reader = f.Reader(); var loader = new RuleLoader(reader, TextCase.Ascii);
        var input = loader.Load("rules"); Equal((byte)3, input.Definitions.Cosmic.Values[0]); Equal((byte)6, Installed(input).Definitions.Units[0].Movement);
        Equal(new OwnedList<string>(["scenario/RULES.TXT", "scenario/RULES.TXT", "home/RULES.TXT", "scenario/RULES.TXT", "home/RULES.TXT"]), new(f.Opens));
        f.Files["scenario/RULES.TXT"] = s[0] + s[1] + s[2]; f.Files["home/RULES.TXT"] = string.Concat(s); f.Opens.Clear();
        var state = Installed(new RulesInput().Load().Input); var before = state.Definitions.Snapshot();
        TextError(TextFault.UnavailableStream, () => new RuleInstaller(loader.Load("rules"), state).Load(true)); Equal(before, state.Definitions.Snapshot()); Equal(false, reader.IsOpen);
        Equal(new OwnedList<string>(Enumerable.Repeat("scenario/RULES.TXT", 3)), new(f.Opens)); Equal("scenario", f.Directories.Search);
        f.Files.Clear(); RuleError(RuleLoadFault.MissingSection, "COSMIC", 0, 0, 0, "", () => loader.Load("rules")); Equal(before, state.Definitions.Snapshot());
    }
}
