using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json.Nodes;
using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Formats.Classic;

namespace OpenCiv2.Formats.Content;

public static partial class ContentConversions
{
    private static string Id(string collection, int slot) =>
        "classic." + collection.ToLowerInvariant() + "." + slot.ToString(CultureInfo.InvariantCulture);

    private static JsonObject Reference(AdvanceReference value) => value switch
    {
        AdvanceReference.None => new() { ["special"] = "none" },
        AdvanceReference.Never => new() { ["special"] = "never" },
        _ => new() { ["advance"] = Id("advances", (short)value) }
    };

    private static JsonObject Result(TerrainResult value) => value switch
    {
        TerrainResult.NoResult => new() { ["special"] = "no-result" },
        TerrainResult.ImproveInPlace => new() { ["special"] = "improve-in-place" },
        _ => new() { ["terrain"] = Id("terrain", (short)value) }
    };

    private static JsonObject Work(TerrainWork value) => new()
    {
        ["result"] = Result(value.Result),
        ["yieldBonus"] = value.YieldBonus,
        ["workTurns"] = value.WorkTurns,
        ["aiGovernmentThreshold"] = value.AiGovernmentThreshold
    };

    private static string Named(int value, string[] names) =>
        value >= 0 && value < names.Length
            ? names[value]
            : throw new ContentException(
                "unsupported-classic-value",
                "rules",
                "Classic numeric code has no native named value");

    public static RuleConversion Rules(ContentSource source, TextCase comparison)
    {
        Admit(source);
        using var reader = Reader(source, comparison);
        var state = new RuleState();

        RuleInput operands;
        try
        {
            operands = new RuleLoader(reader, comparison).Load("RULES");
            new RuleInstaller(operands, state).Load(true);
        }
        catch (RuleLoadException error)
        {
            throw new ContentException(
                "invalid-classic-rules",
                source.Identity + "/" + error.Section,
                error.Message + " at row " + error.Row.ToString(CultureInfo.InvariantCulture));
        }
        catch (TextException error)
        {
            throw TextError(error, source.Identity);
        }
        var values = state.Definitions;
        var global = new JsonObject();
        var cosmic = CosmicConversion.Encode(values.Cosmic);
        for (int i = 0; i < 22; ++i)
        {
            global[NativeRules.CosmicNames[i]] = cosmic[i];
        }

        var rules = new JsonObject { ["format"] = "openciv2-rules/1", ["globalRules"] = global };
        var slots = new JsonObject();
        var flags = new JsonArray();
        var compatibility = new JsonObject
        {
            ["format"] = "openciv2-classic-compatibility/1",
            ["profile"] = GameplayContent.Target,
            ["encoding"] = "windows-1252",
            ["ruleSlots"] = slots,
            ["textLookups"] = new JsonObject(),
            ["scenarioFiles"] = new JsonArray(),
            ["scenarioResources"] = new JsonArray(),
            ["retainedUnitFlags"] = flags
        };
        void Collection<T>(string name, T[] table, Func<T, string, T> identify, Func<T, int, JsonObject> emit)
        {
            var rows = new JsonArray();
            var mappings = new JsonArray();
            rules[name] = rows;
            slots[name] = mappings;
            for (int i = 0; i < table.Length; ++i)
            {
                string identity = Id(name, i);
                table[i] = identify(table[i], identity);
                var row = emit(table[i], i);
                row["id"] = identity;
                rows.Add((JsonNode)row);
                mappings.Add((JsonNode)new JsonObject { ["id"] = identity, ["slot"] = i + (name == "orders" ? 1 : 0) });
            }
        }
        Collection(
            "advances",
            values.Advances,
            (v, id) => v with { Identity = v.Identity with { Id = id } },
            (v, i) => new()
            {
                ["name"] = v.Identity.Name,
                ["aiValue"] = v.AiValue,
                ["civilizedAiModifier"] = v.CivilizedAiModifier,
                ["prerequisites"] = new JsonArray(
                    operands.Definitions.Advances[i].Prerequisites.Select(p => (JsonNode)Reference(p)).ToArray()),
                ["epoch"] = Named((byte)v.Epoch, ["ancient", "renaissance", "industrial", "modern"]),
                ["category"] = Named((byte)v.Category, ["military", "economic", "social", "academic", "applied"])
            });
        Collection(
            "improvements",
            values.Improvements,
            (v, id) => v with { Identity = v.Identity with { Id = id } },
            (v, i) => new()
            {
                ["name"] = v.Identity.Name,
                ["costRows"] = v.CostRows,
                ["upkeep"] = v.Upkeep,
                ["prerequisite"] = Reference(operands.Definitions.Improvements[i].Prerequisite)
            });
        rules["wonderObsolescence"] = new JsonArray(
            Enumerable.Range(0, RuleLimits.Wonders)
                .Select(
                    i => (JsonNode)new JsonObject
                    {
                        ["wonder"] = Id("improvements", i + RuleLimits.BuildingTypes),
                        ["advance"] = Reference(values.WonderObsolescence[i])
                    })
                .ToArray());
        Collection("units", values.Units, (v, id) => v with { Identity = v.Identity with { Id = id } }, (v, i) =>
        {
            if ((RuleCompatibility.RetainedUnitFlags(operands, i) & 0x8000) != 0)
            {
                flags.Add(
                    (JsonNode)new JsonObject
                    {
                        ["unit"] = Id("units", i),
                        ["positions"] = new JsonArray(JsonValue.Create(15))
                    });
            }

            return new()
            {
                ["name"] = v.Identity.Name,
                ["obsoleteBy"] = Reference(v.ObsoleteBy),
                ["prerequisite"] = Reference(v.Prerequisite),
                ["movementPoints"] = operands.MovementPoints[i],
                ["domain"] = Named((byte)v.Domain, ["land", "air", "sea"]),
                ["role"] = Named(
                    (byte)v.Role,
                    [
                        "attack",
                        "defend",
                        "naval-superiority",
                        "air-superiority",
                        "sea-transport",
                        "settle",
                        "diplomacy",
                        "trade"
                    ]),
                ["range"] = v.Range,
                ["attack"] = v.Attack,
                ["defense"] = v.Defense,
                ["hitPoints"] = v.HitPoints,
                ["firepower"] = v.Firepower,
                ["costRows"] = v.CostRows,
                ["capacity"] = v.Capacity,
                ["abilities"] = Strings(
                    Enumerable.Range(0, 15).Where(bit => (StateConversion.EncodeUnitAbilities(v.Abilities) & (1U << bit)) != 0)
                        .Select(
                            bit => NativeRules.AbilityNames[bit]))
            };
        });
        Collection("terrain", values.Terrain, (v, id) => v with { Identity = v.Identity with { Id = id } }, (v, i) =>
        {
            var row = new JsonObject
            {
                ["name"] = v.Identity.Name,
                ["movementCost"] = v.MovementCost,
                ["defenseMultiplier"] = new JsonObject { ["numerator"] = v.DefenseFactor, ["denominator"] = 2 },
                ["food"] = v.Food,
                ["shields"] = v.Shields,
                ["trade"] = v.Trade
            };
            if (i < 11)
            {
                row["irrigation"] = Work(v.Work!.Irrigation);
                row["mining"] = Work(v.Work.Mining);
                row["transformation"] = Result(v.Work.Transformation);
            }
            return row;
        });
        Collection(
            "governments",
            values.Governments,
            (v, id) => v with { Identity = v.Identity with { Id = id } },
            (v, _) => new()
            {
                ["name"] = v.Identity.Name,
                ["maleTitle"] = v.Titles.Male,
                ["femaleTitle"] = v.Titles.Female
            });
        Collection("tribes", values.Tribes, (v, id) => v with { Id = id }, (v, i) => new()
        {
            ["pluralName"] = v.Plural,
            ["adjective"] = v.Adjective,
            ["leaderNames"] = new JsonObject { ["male"] = v.Leaders[0], ["female"] = v.Leaders[1] },
            ["preferredLeaderGender"] = v.PreferredGender == Gender.Female ? "female" : "male",
            ["preferredColor"] = v.PreferredColor,
            ["cityStyle"] = v.CityStyle,
            ["personality"] = new JsonObject
            {
                ["aggression"] = v.Personality[0],
                ["expansion"] = v.Personality[1],
                ["civilizationPreference"] = v.Personality[2]
            },
            ["governmentTitles"] = new JsonArray(
                operands.TitleOverrides[i].Select(
                    t => (JsonNode)new JsonObject
                    {
                        ["government"] = Id("governments", (int)t.Government),
                        ["male"] = t.Titles.Male,
                        ["female"] = t.Titles.Female
                    })
                    .ToArray())
        });
        foreach (var (name, table) in new[] {
            ("commodities", values.Commodities),
            ("difficulties", values.Difficulties),
            ("diplomaticAttitudes", values.DiplomaticAttitudes)
            })
        {
            Collection(name, table, (v, id) => v with { Id = id }, (v, _) => new() { ["name"] = v.Name });
        }

        Collection(
            "orders",
            values.Orders,
            (v, id) => v with { Identity = v.Identity with { Id = id } },
            (v, _) => new()
            {
                ["name"] = v.Identity.Name,
                ["shortcutHint"] = v.Shortcut
            });
        byte[] ruleBytes = Serialize(rules), compatibilityBytes = Serialize(compatibility);
        var ruleDocument = Document("converted-rules", ruleBytes);
        var compatibilityDocument = Document("converted-compatibility", compatibilityBytes);
        var components = ImmutableSortedDictionary<string, ResolvedDocument>.Empty.Add("rules", ruleDocument);
        var pack = new ResolvedPack(
            new([]),
            GameplayContent.Target,
            "",
            "",
            components,
            ImmutableSortedDictionary<string, ImmutableSortedDictionary<string, ResolvedDocument>>.Empty,
            compatibilityDocument,
            new([]),
            new([]),
            new(ruleDocument.File.Owner, null, GameplayContent.Builtin));
        var native = NativeRules.Load(pack);
        var installed = new RuleState();

        new RuleInstaller(native.Input, installed).Load(true);
        if (installed.Definitions.Snapshot() != values.Snapshot())
        {
            throw new ContentException(
                "unsupported-classic-value",
                source.Identity,
                "Native installation does not preserve classic rule values");
        }

        var receipt = new JsonObject
        {
            ["format"] = "openciv2-content-conversion/1",
            ["conversion"] = "classic-rules/1",
            ["profile"] = pack.Profile,
            ["source"] = SourceReceipt(source),
            ["outputs"] = new JsonObject
            {
                ["rules"] = new JsonObject
                {
                    ["format"] = "openciv2-rules/1",
                    ["sha256"] = ContentIdentity.Sha256(ruleBytes)
                },
                ["compatibility"] = new JsonObject
                {
                    ["format"] = "openciv2-classic-compatibility/1",
                    ["sha256"] = ContentIdentity.Sha256(compatibilityBytes)
                }
            },
            ["validationGameplaySemanticSha256"] = pack.Gameplay.Bundle.SemanticSha256,
            ["unsupportedFields"] = new JsonArray(),
            ["lossyFields"] = new JsonArray(),
            ["limitations"] = Strings(
                ["Component outputs, not a playable pack. Identities derive from classic slots, never display names."])
        };
        return new(new(ruleBytes), new(compatibilityBytes), Freeze(receipt));
    }
}
