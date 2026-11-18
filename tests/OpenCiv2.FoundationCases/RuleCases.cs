using System.Text;
using System.Text.Json.Nodes;
using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Formats;
using OpenCiv2.Formats.Content;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // Authored complete identities from the repository's cleanroom rule fixture.
    // The fields/counts are data-format/F03 contracts, not stock game data.
    private sealed class RulesInput
    {
        public JsonObject Rules { get; } = JsonNode.Parse("""{"format":"openciv2-rules/1","globalRules":{},"wonderObsolescence":[]}""")!.AsObject();
        public JsonObject Compatibility { get; } = JsonNode.Parse("""{"format":"openciv2-classic-compatibility/1","profile":"classic-mge-5.4.0f","encoding":"windows-1252","ruleSlots":{},"textLookups":{},"scenarioFiles":[],"scenarioResources":[],"retainedUnitFlags":[]}""")!.AsObject();
        public RulesInput()
        {
            string[] names = ["advances", "improvements", "units", "terrain", "governments", "tribes", "commodities", "orders", "difficulties", "diplomaticAttitudes"];
            int[] counts = [100, 67, 62, 33, 7, 21, 16, 12, 6, 9];
            string[] cosmic = ["roadMovementMultiplier", "triremeLossDenominator", "foodPerCitizen", "foodBoxRows", "shieldBoxRows", "settlerFoodLowGovernment", "settlerFoodHighGovernment", "unhappinessCitySize", "unhappinessEmpireSize", "aqueductSize", "sewerSize", "technologyCostParadigm", "transformationWork", "monarchyFreeSupport", "communismFreeSupport", "fundamentalismFreeSupport", "communismPalaceDistance", "fundamentalismScienceLossPercent", "productionSwitchLossPercent", "paradropRange", "spaceshipMassThrustParadigm", "fundamentalismScienceCapTenths"];
            int[] values = [3, 2, 2, 5, 5, 1, 2, 7, 14, 8, 12, 10, 20, 3, 3, 8, 1, 50, 50, 10, 75, 5];
            for (int i = 0; i < cosmic.Length; ++i) Rules["globalRules"]![cosmic[i]] = values[i];
            string[] prototypes = [
                """{"aiValue":3,"civilizedAiModifier":1,"prerequisites":[{"special":"none"},{"special":"none"}],"epoch":"ancient","category":"academic"}""",
                """{"costRows":10,"upkeep":1,"prerequisite":{"special":"none"}}""",
                """{"obsoleteBy":{"special":"never"},"domain":"land","movementPoints":2,"range":0,"attack":1,"defense":1,"hitPoints":30,"firepower":1,"costRows":10,"capacity":0,"role":"attack","prerequisite":{"special":"none"},"abilities":[]}""",
                """{"movementCost":1,"defenseMultiplier":{"numerator":3,"denominator":2},"food":1,"shields":1,"trade":0}""",
                """{"maleTitle":"Leader","femaleTitle":"Leader"}""",
                """{"adjective":"Example","leaderNames":{"male":"Leader","female":"Leader"},"preferredLeaderGender":"female","preferredColor":1,"cityStyle":0,"personality":{"aggression":0,"expansion":0,"civilizationPreference":0},"governmentTitles":[]}""",
                "{}", """{"shortcutHint":"x"}""", "{}", "{}"];
            for (int kind = 0; kind < names.Length; ++kind)
            {
                string collection = names[kind]; Rules[collection] = new JsonArray(); Compatibility["ruleSlots"]![collection] = new JsonArray();
                for (int slot = 0; slot < counts[kind]; ++slot)
                {
                    var row = JsonNode.Parse(prototypes[kind])!; string id = (kind == 9 ? "attitudes" : collection) + ".authored." + slot;
                    row["id"] = id; row[kind == 5 ? "pluralName" : "name"] = collection + " " + slot;
                    if (kind == 3 && slot < 11)
                    {
                        row["irrigation"] = JsonNode.Parse("""{"result":{"special":"improve-in-place"},"yieldBonus":1,"workTurns":3,"aiGovernmentThreshold":2}""");
                        row["mining"] = JsonNode.Parse("""{"result":{"special":"no-result"},"yieldBonus":0,"workTurns":4,"aiGovernmentThreshold":1}""");
                        row["transformation"] = JsonNode.Parse("""{"terrain":"terrain.authored.0"}""");
                    }
                    Rules[collection]!.AsArray().Add(row);
                    Compatibility["ruleSlots"]![collection]!.AsArray().Add((JsonNode)new JsonObject { ["id"] = id, ["slot"] = slot + (kind == 7 ? 1 : 0) });
                }
            }
            for (int slot = 39; slot < 67; ++slot) Rules["wonderObsolescence"]!.AsArray().Add((JsonNode)new JsonObject { ["wonder"] = "improvements.authored." + slot, ["advance"] = new JsonObject { ["special"] = "none" } });
        }
        public PackInput Pack(string? raw = null)
        {
            var p = new PackInput(); p.Manifest["files"]!["rules"] = p.Source("rules.json", raw ?? Rules.ToJsonString());
            p.Manifest["compatibility"] = p.Source("compatibility.json", Compatibility.ToJsonString()); return p;
        }
        public RuleContent Load(string? raw = null) => NativeRules.Load(ContentPacks.Resolve(Pack(raw).Mount()));
    }
    private static RuleState Installed(RuleInput input, int prefix = 0)
    {
        var state = new RuleState(); state.Text.Initialize();
        for (int i = 0; i < prefix; ++i) state.Text.Store("prefix"u8);
        new RuleInstaller(input, state).Load(true); return state;
    }
    private static void ClassicTextExact()
    {
        BytesEqual([67, 97, 102, 0xe9, 32, 0x80, 32, 0x8c, 32, 0x97], ClassicText.EncodeWindows1252("Café € Œ —", "text"));
        for (int i = 0; i <= 255; ++i)
        {
            byte[] raw = [(byte)i];
            if (i is 0x81 or 0x8d or 0x8f or 0x90 or 0x9d) ContentError("text-encoding", () => ClassicText.DecodeWindows1252(raw, "text"));
            else BytesEqual(raw, ClassicText.EncodeWindows1252(ClassicText.DecodeWindows1252(raw, "text"), "text"));
        }
        foreach (byte[] raw in new byte[][] { [0xc0, 0x80], [0xed, 0xa0, 0x80], [0xf4, 0x90, 0x80, 0x80], [0xe2, 0x82], [0x80], [0xf0, 0x9f, 0x98, 0x80], [0xc2, 0x81] })
            ContentError("text-encoding", () => ClassicText.EncodeWindows1252(raw, "text"));
        ContentError("text-encoding", () => ClassicText.EncodeWindows1252("\ud800", "text"));
    }
    private static void RulesIdentityOrder()
    {
        var input = new RulesInput(); var baseline = input.Load(); var r = Installed(baseline.Input).Definitions.Snapshot();
        Equal((ushort)100, r.EnabledAdvanceCount); Equal((byte)6, r.Units[0].Movement); Equal((byte)30, r.Units[0].HitPoints);
        Equal((byte)3, r.Terrain[0].DefenseFactor); Equal(0, (int)r.Terrain[0].Work!.Transformation); Equal(true, r.Terrain[11].Work is null);
        Equal((byte)6, r.Cosmic[CosmicParameter.FoodBoxRows]); Equal((byte)5, r.Cosmic[CosmicParameter.ShieldBoxRows]);
        Equal(new OwnedList<AdvanceReference?>([null, null, (AdvanceReference)54, (AdvanceReference)15, (AdvanceReference)31, (AdvanceReference)71, (AdvanceReference)21]), baseline.Input.GovernmentAdvances);
        foreach (var pair in input.Rules.ToArray()) if (pair.Value is JsonArray rows) input.Rules[pair.Key] = new JsonArray(rows.Reverse().Select(v => v!.DeepClone()).ToArray());
        Equal(r, Installed(input.Load().Input).Definitions.Snapshot());
        input.Rules["units"]![0]!["name"] = "Renamed"; Equal("Renamed", input.Load().Input.Definitions.Units[61].Identity.Name.Unicode);
        Equal("units 61", baseline.Input.Definitions.Units[61].Identity.Name.Unicode);
    }
    private static void RulesNumericNormalization()
    {
        BytesEqual([1, 1, 0, 4, 4, 0, 0, 4, 10, 4, 4, 3, 5, 0, 0, 0, 1, 0, 0, 4, 25, 0], RuleNormalization.Cosmic(Enumerable.Repeat(int.MinValue, 22).ToArray()).Values);
        BytesEqual([10, 100, 10, 20, 20, 10, 10, 12, 100, 50, 50, 10, 100, 8, 8, 8, 20, 100, 100, 100, 200, 10], RuleNormalization.Cosmic(Enumerable.Repeat(int.MaxValue, 22).ToArray()).Values);
        Equal((byte)1, RuleNormalization.UnitCost(-1)); Equal((byte)200, RuleNormalization.UnitCost(201));
        Equal((byte)1, RuleNormalization.LeaderColor(32768)); Equal((byte)3, RuleNormalization.LeaderStyle(32767));
        var i = new RulesInput(); i.Rules["globalRules"]!["roadMovementMultiplier"] = 30; i.Rules["globalRules"]!["foodBoxRows"] = 19; i.Rules["globalRules"]!["shieldBoxRows"] = 19;
        i.Rules["advances"]![0]!["aiValue"] = 128; i.Rules["advances"]![0]!["civilizedAiModifier"] = 255;
        i.Rules["improvements"]![0]!["costRows"] = 256; i.Rules["improvements"]![0]!["upkeep"] = -1;
        var unit = i.Rules["units"]![0]!; unit["movementPoints"] = 30; unit["hitPoints"] = 250; unit["costRows"] = 200; unit["attack"] = 300; unit["defense"] = 128;
        unit["abilities"] = JsonNode.Parse("""["detect-submarines","see-two"]"""); i.Compatibility["retainedUnitFlags"] = JsonNode.Parse("""[{"unit":"units.authored.0","positions":[15]}]""");
        i.Rules["terrain"]![0]!["food"] = -1; i.Rules["tribes"]![0]!["preferredColor"] = 65539; i.Rules["tribes"]![0]!["cityStyle"] = 65538; i.Rules["tribes"]![0]!["personality"]!["aggression"] = -129;
        var r = Installed(i.Load().Input).Definitions;
        Equal((byte)10, r.Cosmic[CosmicParameter.RoadMovementMultiplier]); Equal((byte)20, r.Cosmic[CosmicParameter.FoodBoxRows]); Equal((byte)19, r.Cosmic[CosmicParameter.ShieldBoxRows]);
        Equal((sbyte)-128, r.Advances[0].AiValue); Equal((sbyte)-1, r.Advances[0].CivilizedAiModifier); Equal((byte)0, r.Improvements[0].CostRows); Equal((byte)255, r.Improvements[0].Upkeep);
        Equal((byte)44, r.Units[0].Movement); Equal((byte)250, r.Units[0].HitPoints); Equal((byte)200, r.Units[0].CostRows); Equal((byte)44, r.Units[0].Attack); Equal((byte)128, r.Units[0].Defense); Equal(0xc001U, r.Units[0].Abilities);
        Equal((byte)255, r.Terrain[0].Food); Equal((byte)3, r.Tribes[0].PreferredColor); Equal((byte)2, r.Tribes[0].CityStyle); Equal((sbyte)127, r.Tribes[0].Personality[0]);
        Equal((byte)4, RuleNormalization.ClassicHitPoints(26)); Equal((byte)253, RuleNormalization.Movement(-1, 3));
        unit["movementPoints"] = "operand"; string raw = i.Rules.ToJsonString();
        Equal((byte)20, Installed(i.Load(raw.Replace("\"operand\"", "2.000e0", StringComparison.Ordinal)).Input).Definitions.Units[0].Movement);
        Equal((byte)0, Installed(i.Load(raw.Replace("\"operand\"", "-2147483648", StringComparison.Ordinal)).Input).Definitions.Units[0].Movement);
        ContentError("invalid-integer", () => i.Load(raw.Replace("\"operand\"", "2.000000000000000000001", StringComparison.Ordinal)));
        ContentError("integer-range", () => i.Load(raw.Replace("\"operand\"", "2147483648", StringComparison.Ordinal)));
    }
    private static void RulesAdvanceFixup()
    {
        var i = new RulesInput(); i.Rules["advances"]![1]!["prerequisites"]![0] = JsonNode.Parse("""{"special":"never"}""");
        i.Rules["advances"]![0]!["prerequisites"] = JsonNode.Parse("""[{"advance":"advances.authored.1"},{"special":"never"}]""");
        i.Rules["advances"]![2]!["prerequisites"]![0] = JsonNode.Parse("""{"advance":"advances.authored.3"}""");
        i.Rules["advances"]![3]!["prerequisites"]![0] = JsonNode.Parse("""{"advance":"advances.authored.2"}""");
        foreach (var node in new[] { i.Rules["improvements"]![1], i.Rules["units"]![0] }) node!["prerequisite"] = JsonNode.Parse("""{"advance":"advances.authored.1"}""");
        i.Rules["wonderObsolescence"]![0]!["advance"] = JsonNode.Parse("""{"advance":"advances.authored.1"}""");
        var input = i.Load().Input; var r = Installed(input).Definitions;
        Equal((ushort)99, r.EnabledAdvanceCount); Equal(true, r.Advances[0].Enabled); Equal(-2, (int)r.Advances[0].Prerequisites[0]); Equal(false, r.Advances[1].Enabled);
        Equal(3, (int)r.Advances[2].Prerequisites[0]); Equal(2, (int)r.Advances[3].Prerequisites[0]); Equal(-2, (int)r.Improvements[1].Prerequisite);
        Equal(1, (int)r.Units[0].Prerequisite); Equal(1, (int)r.WonderObsolescence[0]); Equal(1, (int)input.Definitions.Advances[0].Prerequisites[0]);
        var live = new RuleTables(); live.Advances[0] = new() { Enabled = false, Prerequisites = new([(AdvanceReference)1, AdvanceReference.None]) };
        live.Advances[1] = new() { Enabled = false, Prerequisites = new([(AdvanceReference)0, AdvanceReference.None]) };
        Throws<ArgumentException>(() => RuleNormalization.FollowDisabled((AdvanceReference)0, live.Advances));
        live.Advances[1] = live.Advances[1] with { Enabled = true }; Equal(1, (int)RuleNormalization.FollowDisabled((AdvanceReference)0, live.Advances));
        Throws<ArgumentException>(() => RuleNormalization.FollowDisabled((AdvanceReference)100, live.Advances));
    }
    private static void RulesRetainedInstallation()
    {
        var i = new RulesInput(); i.Rules["units"]![0]!["name"] = ""; i.Rules["improvements"]![1]!["name"] = "Café";
        i.Rules["orders"]![0]!["name"] = ""; i.Rules["orders"]![0]!["shortcutHint"] = "Œ"; i.Rules["difficulties"]![0]!["name"] = "First, second; kept";
        i.Rules["tribes"]![0]!["governmentTitles"] = JsonNode.Parse("""[{"government":"governments.authored.1","male":"First","female":"First"},{"government":"governments.authored.1","male":"Last","female":""}]""");
        var input = i.Load().Input;
        foreach (int prefix in new[] { 0, 70000 })
        {
            var state = Installed(input, prefix); var r = state.Definitions; var installer = new RuleInstaller(input, state);
            Equal(" ", r.Units[0].Identity.Name.Unicode); BytesEqual([67, 97, 102, 0xe9], state.Text.Text(state.Names.Improvements[1]));
            Equal("", r.Orders[0].Identity.Name.Unicode); Equal((byte)0x8c, r.Orders[0].Shortcut); Equal(0U, state.Names.Orders[0]);
            Equal("Last", r.Tribes[0].Titles[1].Male.Unicode); Equal("", r.Tribes[0].Titles[1].Female.Unicode); Equal(0U, state.Leaders[0].Titles[1][1].Id);
            BytesEqual("<nil>"u8.ToArray(), state.Text.Text(state.Leaders[0].Titles[1][1].Id));
            Equal(true, state.Names.Advances[0] > prefix); Equal(r.Governments[0].Titles, r.Tribes[0].Titles[0]);
            state.Leaders[0].Plural = new(state.Text.Store("custom"u8), true); state.Leaders[0].TimesUsed = 71; state.Leaders[0].CityNameIndex = 93;
            var retained = state.Leaders[0].Snapshot(); int count = state.Text.Count; installer.Leaders(false);
            Equal(retained, state.Leaders[0].Snapshot()); Equal(count, state.Text.Count);
            r.Units[0] = r.Units[0] with { Identity = new("old", new("Old name", new("Old name"u8.ToArray()))) }; state.DoubleMovementApplied = true;
            installer.Units(true); Equal("Old name", r.Units[0].Identity.Name.Unicode); Equal("units.authored.0", r.Units[0].Identity.Id); Equal((byte)6, r.Units[0].Movement); Equal((byte)30, r.Units[0].HitPoints); Equal(false, state.DoubleMovementApplied);
            var cosmic = r.Cosmic.Values.ToArray(); cosmic[0] = 7; r.Cosmic = new(new OwnedList<byte>(cosmic)); installer.Units(true); Equal((byte)14, r.Units[0].Movement); Equal((byte)30, r.Units[0].HitPoints);
            r.Terrain[11] = r.Terrain[11] with { Work = new(new(WorkTurns: 97), new()) }; installer.Terrain(); Equal((byte)97, r.Terrain[11].Work!.Irrigation.WorkTurns);
            var snapshot = r.Snapshot(); r.Units[0] = r.Units[0] with { Attack = 72 }; Equal((byte)1, snapshot.Units[0].Attack);
        }
        Throws<ArgumentException>(() => new RuleInstaller(input, new RuleState()));
    }
    private static void RuleTextAndLabelState()
    {
        var pool = new TextPool(); pool.Initialize(); Equal(1, pool.Count); Equal(0U, pool.Store([]));
        Equal(1U, pool.Store("name\0ignored"u8)); Equal(2U, pool.Store("name"u8)); BytesEqual("name"u8.ToArray(), pool.Text(1));
        Throws<ArgumentOutOfRangeException>(() => pool.Text(3)); var labels = new Labels(); Equal(888, Labels.Capacity);
        BytesEqual("OK"u8.ToArray(), labels.Popup(0, pool)); BytesEqual("Help"u8.ToArray(), labels.Popup(1, pool)); BytesEqual("Cancel"u8.ToArray(), labels.Popup(2, pool));
        labels.Entries.Add(null); Throws<ArgumentOutOfRangeException>(() => labels.Label(0, pool)); labels.Entries[0] = 2; BytesEqual("name"u8.ToArray(), labels.Label(0, pool));
        labels.Popups[0] = 0; BytesEqual("<nil>"u8.ToArray(), labels.Popup(0, pool)); pool.Initialize(); Equal(1, pool.Count);
    }
    private static void RulesReloadDependenciesAndFailurePrefix()
    {
        var fixture = new RulesInput(); var input = fixture.Load().Input; var state = Installed(input);
        var improvements = input.Definitions.Improvements.ToArray(); improvements[0] = improvements[0] with { Prerequisite = (AdvanceReference)1 };
        var installer = new RuleInstaller(input with { Definitions = input.Definitions with { Improvements = new(improvements) } }, state);
        installer.Improvements(); Equal(1, (int)state.Definitions.Improvements[0].Prerequisite);
        state.Definitions.Advances[1] = state.Definitions.Advances[1] with { Enabled = false, Prerequisites = new([AdvanceReference.None, AdvanceReference.Never]) };
        installer.Improvements(); Equal(-1, (int)state.Definitions.Improvements[0].Prerequisite);
        state.Definitions.Advances[1] = state.Definitions.Advances[1] with { Prerequisites = new([(AdvanceReference)1, AdvanceReference.Never]) };
        state.Definitions.Improvements[1] = state.Definitions.Improvements[1] with { CostRows = 97 };
        Throws<ArgumentException>(() => installer.Improvements());
        Equal(1, (int)state.Definitions.Improvements[0].Prerequisite); Equal((byte)10, state.Definitions.Improvements[0].CostRows);
        Equal((byte)97, state.Definitions.Improvements[1].CostRows);
        var overrides = input.TitleOverrides.ToArray(); overrides[0] = new([new((Government)1, new(new("Earlier", new("Earlier"u8.ToArray())), RuleText.Empty)), new((Government)0, new())]);
        var later = state.Leaders[1].Snapshot();
        Throws<ArgumentException>(() => new RuleInstaller(input with { TitleOverrides = new(overrides) }, state).Leaders(true));
        Equal("Earlier", state.Definitions.Tribes[0].Titles[1].Male.Unicode); Equal(later, state.Leaders[1].Snapshot());
        // Decoder admission is separate from ordered mutation, so a rejected document has no effects.
        state.Definitions.Units[0] = state.Definitions.Units[0] with { Attack = 72 }; fixture.Rules["units"]![0]!["hitPoints"] = 31;
        ContentError("invalid-content", () => new RuleInstaller(fixture.Load().Input, state).Load(true)); Equal((byte)72, state.Definitions.Units[0].Attack);
        var pack = new RulesInput().Pack(); var gameplay = new BundleInput(); gameplay.Custom();
        gameplay.Edit("governments", document => document["governments"]!["monarchy"]!["requiredAdvance"] = "advances.authored.7");
        foreach (var (path, bytes) in gameplay.Files) pack.Files["variant/" + path] = bytes;
        pack.Manifest["gameplay"] = pack.Source("variant/gameplay.json", gameplay.Manifest.ToJsonString());
        Equal(7, (int)NativeRules.Load(ContentPacks.Resolve(pack.Mount())).Input.GovernmentAdvances[2]!.Value);
        gameplay.Edit("governments", document => document["governments"]!["monarchy"]!["requiredAdvance"] = "missing.advance");
        foreach (var (path, bytes) in gameplay.Files) pack.Files["variant/" + path] = bytes;
        pack.Manifest["gameplay"] = pack.Source("variant/gameplay.json", gameplay.Manifest.ToJsonString());
        ContentError("invalid-content", () => NativeRules.Load(ContentPacks.Resolve(pack.Mount())));
    }
    private static void RulesRejection()
    {
        Action<RulesInput>[] changes = [
            i => i.Rules["advances"]!.AsArray().RemoveAt(99),
            i => i.Rules["units"]![1]!["id"] = "units.authored.0",
            i => i.Compatibility["ruleSlots"]!["units"]!.AsArray().RemoveAt(61),
            i => i.Compatibility["ruleSlots"]!["units"]![1]!["slot"] = 0,
            i => i.Compatibility["ruleSlots"]!["orders"]![0]!["slot"] = 0,
            i => i.Rules["units"]![0]!["hitPoints"] = 31,
            i => i.Rules["units"]![0]!["hitPoints"] = 260,
            i => i.Rules["units"]![0]!["domain"] = "unknown",
            i => i.Rules["units"]![0]!["movementPoints"] = false,
            i => i.Rules["units"]![0]!["abilities"] = JsonNode.Parse("""["see-two","see-two"]"""),
            i => i.Rules["units"]![0]!["prerequisite"] = JsonNode.Parse("""{"advance":"missing"}"""),
            i => i.Rules["wonderObsolescence"]![0]!["wonder"] = "improvements.authored.1",
            i => i.Rules["terrain"]![0]!["defenseMultiplier"]!["denominator"] = 5,
            i => i.Rules["terrain"]![0]!["defenseMultiplier"] = JsonNode.Parse("""{"numerator":128,"denominator":1}"""),
            i => i.Rules["terrain"]![0]!["transformation"] = JsonNode.Parse("""{"terrain":"terrain.authored.11"}"""),
            i => i.Rules["terrain"]![11]!["transformation"] = JsonNode.Parse("""{"special":"no-result"}"""),
            i => i.Rules["tribes"]![0]!["governmentTitles"] = JsonNode.Parse("""[{"government":"governments.authored.0","male":"X","female":"Y"}]"""),
            i => i.Rules["units"]![0]!["name"] = " leading",
            i => i.Rules["units"]![0]!["name"] = "with,comma",
            i => i.Rules["advances"]![0]!["name"] = ";termination",
            i => i.Rules["orders"]![0]!["shortcutHint"] = "ab",
            i => i.Rules["units"]![0]!["name"] = new string('x', 255),
            i => i.Rules["units"]![0]!["extra"] = 1,
            i => i.Compatibility["retainedUnitFlags"] = JsonNode.Parse("""[{"unit":"units.authored.0","positions":[14]}]""")];
        foreach (var change in changes) { var i = new RulesInput(); change(i); ContentError("invalid-content", () => i.Load()); }
        var encoded = new RulesInput(); encoded.Rules["tribes"]![0]!["pluralName"] = "😀"; ContentError("text-encoding", () => encoded.Load());
        var canonical = new RulesInput(); canonical.Rules["advances"]![54]!["id"] = "advance.monarchy";
        canonical.Compatibility["ruleSlots"]!["advances"]!.AsArray().RemoveAt(54); Equal("advance.monarchy", canonical.Load().Input.Definitions.Advances[54].Identity.Id);
        canonical.Compatibility["ruleSlots"]!["advances"]!.AsArray().Add(JsonNode.Parse("""{"id":"advance.monarchy","slot":53}""")); ContentError("invalid-content", () => canonical.Load());
    }
}
