using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;

namespace OpenCiv2.Formats.Content;

public sealed record RuleContent(RuleInput Input, ResolvedFile RulesSource, ResolvedFile? CompatibilitySource);

public static class NativeRules
{
    public static OwnedList<string> Collections { get; } = new(["advances", "improvements", "units", "terrain", "governments", "tribes", "commodities", "orders", "difficulties", "diplomaticAttitudes"]);
    public static OwnedList<int> Counts { get; } = new([100, 67, 62, 33, 7, 21, 16, 12, 6, 9]);
    public static OwnedList<string> CosmicNames { get; } = new(["roadMovementMultiplier", "triremeLossDenominator", "foodPerCitizen", "foodBoxRows", "shieldBoxRows", "settlerFoodLowGovernment", "settlerFoodHighGovernment", "unhappinessCitySize", "unhappinessEmpireSize", "aqueductSize", "sewerSize", "technologyCostParadigm", "transformationWork", "monarchyFreeSupport", "communismFreeSupport", "fundamentalismFreeSupport", "communismPalaceDistance", "fundamentalismScienceLossPercent", "productionSwitchLossPercent", "paradropRange", "spaceshipMassThrustParadigm", "fundamentalismScienceCapTenths"]);
    public static OwnedList<string> AbilityNames { get; } = new(["see-two", "ignore-zoc", "amphibious", "submarine", "attack-air", "coast-bound", "ignore-walls", "carry-aircraft", "paradrop", "alpine", "anti-horse-defense", "fundamentalism-free-support", "destroyed-after-attack", "anti-air-defense", "detect-submarines"]);
    internal static short? CanonicalAdvance(string id) => id switch
    {
        "advance.monarchy" => 54,
        "advance.communism" => 15,
        "advance.fundamentalism" => 31,
        "advance.republic" => 71,
        "advance.democracy" => 21,
        _ => null
    };
    private static RuleText Text(ContentReader field, bool padded, bool token = true, bool earlyTermination = false)
    {
        string unicode = field.Text(false); byte[] encoded = ClassicText.EncodeWindows1252(unicode, field.Path);
        if (encoded.Any(b => b is 0 or 13 or 10 or 26) || encoded.Length > 254) field.Error("name cannot pass the classic single-line byte boundary");
        if (token && encoded.Contains((byte)',')) field.Error("name contains a classic token delimiter");
        if (padded && encoded.Length == 0) { encoded = [(byte)' ']; unicode = " "; }
        bool paddedSpace = padded && encoded.Length == 1 && encoded[0] == ' ';
        if (encoded.Length != 0 && !paddedSpace && (encoded[0] is 32 or 9 || encoded[^1] is 32 or 9)) field.Error("name would change under classic edge trimming");
        if (earlyTermination && encoded.Length != 0 && encoded[0] == ';') field.Error("name would terminate the classic advance/unit table");
        return new(unicode, new(encoded));
    }
    private static RuleName Name(ContentReader row, bool padded = true, bool token = true, bool early = false) => new(row.At("id").Id(), Text(row.At("name"), padded, token, early));
    private static byte Named(ContentReader field, IReadOnlyList<string> names)
    {
        string value = field.Text();
        for (int i = 0; i < names.Count; ++i) if (names[i] == value) return checked((byte)i);
        field.Error("unknown named domain value"); return 0;
    }
    private sealed class IdMap(int count)
    {
        public SortedDictionary<string, short> Ids { get; } = new(StringComparer.Ordinal);
        public int[] Rows { get; } = Enumerable.Repeat(count, count).ToArray();
        public short Lookup(ContentReader field)
        {
            string id = field.Id(); if (!Ids.TryGetValue(id, out short slot)) field.Error("unresolved rule identity " + id);
            return slot;
        }
    }
    private static IdMap[] MapIdentities(ContentReader root, ContentReader? compatibility)
    {
        var maps = new IdMap[Collections.Count]; compatibility?.At("ruleSlots").Members([], Collections);
        for (int kind = 0; kind < Collections.Count; ++kind)
        {
            var list = root.At(Collections[kind]); int count = Counts[kind];
            if (list.ArraySize() != count) list.Error("wrong rule identity count");
            var definitions = new SortedDictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < count; ++i) if (!definitions.TryAdd(list.Element(i).At("id").Id(), i)) list.Element(i).Error("duplicate rule identity");
            int first = kind == 7 ? 1 : 0; var explicitSlots = new Dictionary<string, short>(StringComparer.Ordinal);
            if (compatibility is not null && compatibility.At("ruleSlots").Has(Collections[kind]))
            {
                var slots = compatibility.At("ruleSlots").At(Collections[kind]);
                for (int i = 0; i < slots.ArraySize(); ++i)
                {
                    var entry = slots.Element(i); entry.Fields(["id", "slot"]); string id = entry.At("id").Id();
                    short slot = (short)entry.At("slot").Integer(first, first + count - 1);
                    if (!definitions.ContainsKey(id)) entry.Error("mapping has no selected rule definition");
                    if (!explicitSlots.TryAdd(id, slot)) entry.Error("duplicate explicit rule mapping");
                    if (kind == 0 && CanonicalAdvance(id) is { } fixedSlot && slot != fixedSlot) entry.Error("mapping relocates a registered identity");
                }
            }
            var map = maps[kind] = new(count);
            foreach (var (id, row) in definitions)
            {
                short? slot = explicitSlots.TryGetValue(id, out short explicitSlot) ? explicitSlot : kind == 0 ? CanonicalAdvance(id) : null;
                if (slot is null) list.Element(row).Error("unmapped rule identity; declare compatibility.ruleSlots");
                int index = slot!.Value - first;
                if (map.Rows[index] != count) list.Error("multiple rule definitions occupy one classic identity");
                map.Rows[index] = row; map.Ids.Add(id, slot.Value);
            }
        }
        return maps;
    }
    private static AdvanceReference Advance(ContentReader field, IdMap map)
    {
        if (field.Has("advance")) { field.Fields(["advance"]); return (AdvanceReference)map.Lookup(field.At("advance")); }
        field.Fields(["special"]); return Named(field.At("special"), ["none", "never"]) == 0 ? AdvanceReference.None : AdvanceReference.Never;
    }
    private static TerrainResult Result(ContentReader field, IdMap map)
    {
        if (field.Has("terrain"))
        {
            field.Fields(["terrain"]); short slot = map.Lookup(field.At("terrain"));
            if (slot > 10) field.Error("terrain result must select a base terrain identity");
            return (TerrainResult)slot;
        }
        field.Fields(["special"]); return Named(field.At("special"), ["no-result", "improve-in-place"]) == 0 ? TerrainResult.NoResult : TerrainResult.ImproveInPlace;
    }
    private static TerrainWork Work(ContentReader field, IdMap map)
    {
        field.Fields(["result", "yieldBonus", "workTurns", "aiGovernmentThreshold"]);
        return new(Result(field.At("result"), map), RuleNormalization.Code8(field.At("yieldBonus").Integer()), RuleNormalization.Code8(field.At("workTurns").Integer()), RuleNormalization.Code8(field.At("aiGovernmentThreshold").Integer()));
    }
    public static RuleContent Load(ResolvedPack pack)
    {
        if (!pack.Components.TryGetValue("rules", out var source)) throw new ContentException("invalid-content", "rules", "requires resolved rules and gameplay selections");
        var root = new ContentReader(source.Read(), source.File.Receipt.Path);
        root.Fields(["format", "globalRules", "advances", "improvements", "wonderObsolescence", "units", "terrain", "governments", "tribes", "commodities", "orders", "difficulties", "diplomaticAttitudes"], true);
        root.At("format").Constant("openciv2-rules/1"); ContentReader? compat = null;
        if (pack.Compatibility is not null)
        {
            compat = new(pack.Compatibility.Read(), "compatibility.json");
            compat.Fields(["format", "profile", "encoding", "ruleSlots", "textLookups", "scenarioFiles", "scenarioResources", "retainedUnitFlags"], true);
            compat.At("format").Constant("openciv2-classic-compatibility/1"); compat.At("profile").Constant(GameplayContent.Target); compat.At("encoding").Constant("windows-1252");
            compat.At("textLookups").Object(); compat.At("scenarioFiles").ArraySize(); compat.At("scenarioResources").ArraySize();
        }
        var maps = MapIdentities(root, compat);
        ContentReader Row(int kind, int slot) => root.At(Collections[kind]).Element(maps[kind].Rows[slot]);
        var values = new RuleTables(); var movement = new int[62]; var titleOverrides = new List<LeaderTitleOverride>[21]; var governmentAdvances = new AdvanceReference?[7];
        var cosmic = root.At("globalRules"); cosmic.Fields(CosmicNames);
        values.Cosmic = RuleNormalization.Cosmic(CosmicNames.Select(name => cosmic.At(name).Integer()).ToArray());
        for (int i = 0; i < 100; ++i)
        {
            var r = Row(0, i); r.Fields(["id", "name", "aiValue", "civilizedAiModifier", "prerequisites", "epoch", "category"]);
            var name = Name(r, true, true, true); sbyte ai = RuleNormalization.Signed8(r.At("aiValue").Integer()), civil = RuleNormalization.Signed8(r.At("civilizedAiModifier").Integer());
            var prerequisites = r.At("prerequisites"); if (prerequisites.ArraySize() != 2) prerequisites.Error("expected two prerequisites");
            var refs = new OwnedList<AdvanceReference>([Advance(prerequisites.Element(0), maps[0]), Advance(prerequisites.Element(1), maps[0])]);
            values.Advances[i] = new()
            {
                Identity = name,
                AiValue = ai,
                CivilizedAiModifier = civil,
                Prerequisites = refs,
                Enabled = refs[0] != AdvanceReference.Never,
                Epoch = (AdvanceEpoch)Named(r.At("epoch"), ["ancient", "renaissance", "industrial", "modern"]),
                Category = (AdvanceCategory)Named(r.At("category"), ["military", "economic", "social", "academic", "applied"])
            };
        }
        for (int i = 0; i < 67; ++i)
        {
            var r = Row(1, i); r.Fields(["id", "name", "costRows", "upkeep", "prerequisite"]);
            values.Improvements[i] = new() { Identity = Name(r), CostRows = RuleNormalization.Code8(r.At("costRows").Integer()), Upkeep = RuleNormalization.Code8(r.At("upkeep").Integer()), Prerequisite = Advance(r.At("prerequisite"), maps[0]) };
        }
        var wonders = root.At("wonderObsolescence"); if (wonders.ArraySize() != 28) wonders.Error("expected 28 wonder expiry definitions");
        var seenWonders = new HashSet<short>();
        for (int i = 0; i < 28; ++i)
        {
            var r = wonders.Element(i); r.Fields(["wonder", "advance"]); short slot = maps[1].Lookup(r.At("wonder"));
            if (slot < 39 || !seenWonders.Add(slot)) r.Error("expected each wonder identity exactly once");
            values.WonderObsolescence[slot - 39] = Advance(r.At("advance"), maps[0]);
        }
        for (int i = 0; i < 62; ++i)
        {
            var r = Row(2, i); r.Fields(["id", "name", "obsoleteBy", "domain", "movementPoints", "range", "attack", "defense", "hitPoints", "firepower", "costRows", "capacity", "role", "prerequisite", "abilities"]);
            var name = Name(r, true, true, true); var obsolete = Advance(r.At("obsoleteBy"), maps[0]); var prerequisite = Advance(r.At("prerequisite"), maps[0]);
            var domain = (RuleDomain)Named(r.At("domain"), ["land", "air", "sea"]);
            var role = (RuleRole)Named(r.At("role"), ["attack", "defend", "naval-superiority", "air-superiority", "sea-transport", "settle", "diplomacy", "trade"]);
            movement[i] = r.At("movementPoints").Integer(); byte range = RuleNormalization.Code8(r.At("range").Integer()), attack = RuleNormalization.Code8(r.At("attack").Integer()), defense = RuleNormalization.Code8(r.At("defense").Integer());
            int hp = r.At("hitPoints").Integer(0, 250); if (hp % 10 != 0) r.At("hitPoints").Error("hit points must be a multiple of ten");
            byte firepower = RuleNormalization.Code8(r.At("firepower").Integer()), cost = RuleNormalization.UnitCost(r.At("costRows").Integer()), capacity = RuleNormalization.Code8(r.At("capacity").Integer());
            var abilities = r.At("abilities"); uint flags = 0;
            for (int j = 0; j < abilities.ArraySize(); ++j)
            {
                uint flag = 1U << Named(abilities.Element(j), AbilityNames); if ((flags & flag) != 0) abilities.Error("duplicate ability"); flags |= flag;
            }
            values.Units[i] = new()
            {
                Identity = name,
                ObsoleteBy = obsolete,
                Prerequisite = prerequisite,
                Domain = domain,
                Role = role,
                Range = range,
                Attack = attack,
                Defense = defense,
                HitPoints = RuleNormalization.ClassicHitPoints(hp / 10),
                Firepower = firepower,
                CostRows = cost,
                Capacity = capacity,
                Abilities = flags
            };
        }
        if (compat is not null)
        {
            var flags = compat.At("retainedUnitFlags"); var seen = new HashSet<short>();
            for (int i = 0; i < flags.ArraySize(); ++i)
            {
                var r = flags.Element(i); r.Fields(["unit", "positions"]); short slot = maps[2].Lookup(r.At("unit"));
                if (!seen.Add(slot)) r.Error("duplicate retained-unit-flag record"); var positions = r.At("positions");
                if (positions.ArraySize() > 1) positions.Error("only one retained position is defined");
                if (positions.ArraySize() != 0) { positions.Element(0).Integer(15, 15); values.Units[slot] = values.Units[slot] with { Abilities = values.Units[slot].Abilities | 0x8000 }; }
            }
        }
        for (int i = 0; i < 33; ++i)
        {
            var r = Row(3, i); string[] fields = ["id", "name", "movementCost", "defenseMultiplier", "food", "shields", "trade"];
            r.Fields(i < 11 ? [.. fields, "irrigation", "mining", "transformation"] : fields);
            var name = Name(r); byte movementCost = RuleNormalization.Code8(r.At("movementCost").Integer()); var ratio = r.At("defenseMultiplier");
            ratio.Fields(["numerator", "denominator"]); long doubled = 2L * ratio.At("numerator").Integer(0); int denominator = ratio.At("denominator").Integer(1);
            if (doubled % denominator != 0 || doubled / denominator > 255) ratio.Error("terrain defense must have an integral doubled factor in 0..255");
            byte food = RuleNormalization.Code8(r.At("food").Integer()), shields = RuleNormalization.Code8(r.At("shields").Integer()), trade = RuleNormalization.Code8(r.At("trade").Integer());
            values.Terrain[i] = new()
            {
                Identity = name,
                MovementCost = movementCost,
                DefenseFactor = (byte)(doubled / denominator),
                Food = food,
                Shields = shields,
                Trade = trade,
                Work = i < 11 ? new(Work(r.At("irrigation"), maps[3]), Work(r.At("mining"), maps[3]), Result(r.At("transformation"), maps[3])) : null
            };
        }
        for (int i = 0; i < 7; ++i)
        {
            var r = Row(4, i); r.Fields(["id", "name", "maleTitle", "femaleTitle"]);
            values.Governments[i] = new() { Identity = Name(r), Titles = new(Text(r.At("maleTitle"), true), Text(r.At("femaleTitle"), true)) };
        }
        for (int i = 0; i < 21; ++i)
        {
            var r = Row(5, i); r.Fields(["id", "pluralName", "adjective", "leaderNames", "preferredLeaderGender", "preferredColor", "cityStyle", "personality", "governmentTitles"]);
            string id = r.At("id").Id(); var plural = Text(r.At("pluralName"), true); var adjective = Text(r.At("adjective"), true);
            var leaders = r.At("leaderNames"); leaders.Fields(["male", "female"]); var names = new OwnedList<RuleText>([Text(leaders.At("male"), true), Text(leaders.At("female"), true)]);
            var gender = (Gender)Named(r.At("preferredLeaderGender"), ["male", "female"]); byte color = RuleNormalization.LeaderColor(r.At("preferredColor").Integer()), style = RuleNormalization.LeaderStyle(r.At("cityStyle").Integer());
            var personality = r.At("personality"); personality.Fields(["aggression", "expansion", "civilizationPreference"]);
            values.Tribes[i] = new()
            {
                Id = id,
                Plural = plural,
                Adjective = adjective,
                Leaders = names,
                PreferredGender = gender,
                PreferredColor = color,
                CityStyle = style,
                Personality = new([RuleNormalization.Signed8(personality.At("aggression").Integer()), RuleNormalization.Signed8(personality.At("expansion").Integer()), RuleNormalization.Signed8(personality.At("civilizationPreference").Integer())])
            };
            var overrides = r.At("governmentTitles"); var titles = titleOverrides[i] = [];
            for (int j = 0; j < overrides.ArraySize(); ++j)
            {
                var title = overrides.Element(j); title.Fields(["government", "male", "female"]); short government = maps[4].Lookup(title.At("government"));
                if (government == 0) title.Error("Anarchy titles cannot be overridden"); titles.Add(new((Government)government, new(Text(title.At("male"), false), Text(title.At("female"), false))));
            }
        }
        void SimpleNames(RuleName[] output, int kind, bool padded, bool token)
        {
            for (int i = 0; i < output.Length; ++i) { var r = Row(kind, i); r.Fields(["id", "name"]); output[i] = Name(r, padded, token); }
        }
        SimpleNames(values.Commodities, 6, true, true); SimpleNames(values.Difficulties, 8, false, false); SimpleNames(values.DiplomaticAttitudes, 9, false, false);
        for (int i = 0; i < 12; ++i)
        {
            var r = Row(7, i); r.Fields(["id", "name", "shortcutHint"]); var shortcut = Text(r.At("shortcutHint"), false);
            if (shortcut.Classic.Count > 1) r.At("shortcutHint").Error("shortcut must be empty or one encoded character");
            values.Orders[i] = new() { Identity = Name(r, false), Shortcut = shortcut.Classic.Count == 0 ? (byte)0 : shortcut.Classic[0] };
        }
        for (int i = 0; i < 7; ++i)
        {
            string? required = pack.Gameplay.Bundle.Values.Governments[i].RequiredAdvance; if (required is null) continue;
            if (CanonicalAdvance(required) is { } fixedSlot) governmentAdvances[i] = (AdvanceReference)fixedSlot;
            else if (maps[0].Ids.TryGetValue(required, out short slot)) governmentAdvances[i] = (AdvanceReference)slot;
            else throw new ContentException("invalid-content", "gameplay/governments", "unresolved required advance " + required);
        }
        return new(new() { Definitions = values.Snapshot(), MovementPoints = new(movement), TitleOverrides = new(titleOverrides.Select(t => new OwnedList<LeaderTitleOverride>(t))), GovernmentAdvances = new(governmentAdvances) }, source.File, pack.Compatibility?.File);
    }
}
