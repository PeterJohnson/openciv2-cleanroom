using System.Globalization;
using System.Text;
using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;

namespace OpenCiv2.Formats.Classic;

public enum RuleLoadFault { Reference, MissingSection, InvalidTitleIndex }
public sealed class RuleLoadException(
    RuleLoadFault fault,
    string section,
    OwnedList<byte> line,
    int row,
    int position,
    int code)
    : Exception("Classic rule load stopped in " + section)
{
    public RuleLoadFault Fault { get; } = fault;
    public string Section { get; } = section;
    public OwnedList<byte> Line { get; } = line;
    public int Row { get; } = row;
    public int Position { get; } = position;
    public int Code { get; } = code;
    public const int Module = 3;
}
/// <summary>F03 §§2–7,9 and Appendix A. Decode unpublished rule operands; no live state or pool is needed.</summary>
public sealed class RuleLoader
{
    // Fixed external vocabulary from F03 §3, independent of display names.
    private static readonly string[] AdvanceCodes = [
        "AFl",
        "Alp",
        "Amp",
        "Ast",
        "Ato",
        "Aut",
        "Ban",
        "Bri",
        "Bro",
        "Cer",
        "Che",
        "Chi",
        "CoL",
        "CA",
        "Cmb",
        "Cmn",
        "Cmp",
        "Csc",
        "Cst",
        "Cor",
        "Cur",
        "Dem",
        "Eco",
        "E1",
        "E2",
        "Eng",
        "Env",
        "Esp",
        "Exp",
        "Feu",
        "Fli",
        "Fun",
        "FP",
        "Gen",
        "Gue",
        "Gun",
        "Hor",
        "Ind",
        "Inv",
        "Iro",
        "Lab",
        "Las",
        "Ldr",
        "Lit",
        "Too",
        "Mag",
        "Map",
        "Mas",
        "MP",
        "Mat",
        "Med",
        "Met",
        "Min",
        "Mob",
        "Mon",
        "MT",
        "Mys",
        "Nav",
        "NF",
        "NP",
        "Phi",
        "Phy",
        "Pla",
        "Plu",
        "PT",
        "Pot",
        "Rad",
        "RR",
        "Rec",
        "Ref",
        "Rfg",
        "Rep",
        "Rob",
        "Roc",
        "San",
        "Sea",
        "SFl",
        "Sth",
        "SE",
        "Stl",
        "Sup",
        "Tac",
        "The",
        "ToG",
        "Tra",
        "Uni",
        "War",
        "Whe",
        "Wri",
        "FT",
        "U1",
        "U2",
        "U3",
        "X1",
        "X2",
        "X3",
        "X4",
        "X5",
        "X6",
        "X7"
    ];
    private static readonly string[] TerrainCodes = [
        "Drt",
        "Pln",
        "Grs",
        "For",
        "Hil",
        "Mou",
        "Tun",
        "Gla",
        "Swa",
        "Jun",
        "Oce"
    ];
    private readonly TextReader reader;
    private readonly TextCase comparison;
    private readonly Action<RuleLoadException>? onFailure;
    private readonly RuleTables rules = new();
    private readonly int[] movement = new int[62];
    private readonly List<LeaderTitleOverride>[] titles = Enumerable.Range(0, 21)
        .Select(
            _ => new List<LeaderTitleOverride>())
        .ToArray();

    public RuleLoader(TextReader reader, TextCase comparison, Action<RuleLoadException>? onFailure = null)
    {
        ArgumentNullException.ThrowIfNull(comparison.Equal);
        this.reader = reader;
        this.comparison = comparison;
        this.onFailure = onFailure;
    }

    public RuleInput Snapshot() =>
        new()
        {
            Definitions = rules.Snapshot(),
            MovementPoints = new(movement),
            TitleOverrides = new(titles.Select(t => new OwnedList<LeaderTitleOverride>(t)))
        };

    public static AdvanceReference ResolveAdvance(ReadOnlySpan<byte> token)
    {
        if (token.SequenceEqual("nil"u8))
        {
            return AdvanceReference.None;
        }

        if (token.SequenceEqual("no"u8))
        {
            return AdvanceReference.Never;
        }

        int index = Array.IndexOf(AdvanceCodes, Encoding.Latin1.GetString(token));
        return index < 0 ? AdvanceReference.Invalid : (AdvanceReference)index;
    }

    public static TerrainResult ResolveTerrain(ReadOnlySpan<byte> token, TextCase comparison)
    {
        ArgumentNullException.ThrowIfNull(comparison.Equal);
        if (comparison.Equal(token, "no"u8))
        {
            return TerrainResult.NoResult;
        }

        if (comparison.Equal(token, "yes"u8))
        {
            return TerrainResult.ImproveInPlace;
        }

        int index = Array.IndexOf(TerrainCodes, Encoding.Latin1.GetString(token));
        return index < 0 ? TerrainResult.Invalid : (TerrainResult)index;
    }

    private static RuleText Text(byte[] bytes, bool padded)
    {
        if (padded && bytes.Length == 0)
        {
            bytes = [(byte)' '];
        }

        return new(ClassicText.DecodeWindows1252(bytes, "classic rule text"), new(bytes));
    }

    private RuleText Name(bool padded) => Text(reader.NextToken(), padded);

    private void Fail(RuleLoadFault fault, string section, int row, int position, int code)
    {
        var error = new RuleLoadException(fault, section, new(reader.Line.ToArray()), row, position, code);
        onFailure?.Invoke(error);
        throw error;
    }

    private void Section(string? filename, string section)
    {
        if (!reader.Open(filename, Encoding.ASCII.GetBytes(section)))
        {
            Fail(RuleLoadFault.MissingSection, section, 0, 0, 0);
        }
    }

    public RuleInput Load(string filename)
    {
        Section(filename, "COSMIC");
        Cosmic();
        Section(filename, "CIVILIZE");
        Advances();
        Section(filename, "IMPROVE");
        Improvements();
        Section(null, "ENDWONDER");
        WonderExpiry();
        Section(null, "UNITS");
        Units();
        Section(null, "TERRAIN");
        Terrain();
        Section(null, "GOVERNMENTS");
        Governments();
        Section(null, "LEADERS");
        Leaders();
        Section(null, "CARAVAN");
        Commodities();
        Section(null, "ORDERS");
        Orders();
        Section(null, "DIFFICULTY");
        Difficulties();
        Section(null, "ATTITUDES");
        Attitudes();
        reader.Close();
        return Snapshot();
    }

    public void Cosmic()
    {
        var values = new byte[22];
        for (int i = 0; i < values.Length; ++i)
        {
            reader.NextLine();
            values[i] = RuleNormalization.CosmicValue((CosmicParameter)i, reader.NextInteger());
        }
        rules.Cosmic = new(new OwnedList<byte>(values));
    }

    public void Advances()
    {
        rules.EnabledAdvanceCount = 0;
        for (int i = 0; i < 100; ++i)
        {
            rules.Advances[i] = rules.Advances[i] with
            {
                Prerequisites = new([AdvanceReference.Never, AdvanceReference.Never]),
                Enabled = false
            };
        }

        int index = 0;
        for (; index < 100; ++index)
        {
            reader.NextLine();
            if (reader.Line.IsEmpty || reader.Line[0] == ';')
            {
                break;
            }

            var name = Name(true);
            sbyte ai = RuleNormalization.Signed8(
                reader.NextInteger()), civil = RuleNormalization.Signed8(
                    reader.NextInteger());
            var prerequisites = new AdvanceReference[2];
            for (int p = 0; p < 2; ++p)
            {
                prerequisites[p] = ResolveAdvance(reader.NextToken());
                if (prerequisites[p] == AdvanceReference.Invalid)
                {
                    Fail(RuleLoadFault.Reference, "CIVILIZE", index, p, -4);
                }
            }
            rules.Advances[index] = new()
            {
                Identity = new("", name),
                AiValue = ai,
                CivilizedAiModifier = civil,
                Prerequisites = new(prerequisites),
                Epoch = (AdvanceEpoch)RuleNormalization.Code8(reader.NextInteger()),
                Category = (AdvanceCategory)RuleNormalization.Code8(reader.NextInteger()),
                Enabled = prerequisites[0] != AdvanceReference.Never
            };
        }
        for (; index < 100; ++index)
        {
            rules.Advances[index] = new()
            {
                Identity = new(
                    "",
                    Text(
                        Encoding.ASCII.GetBytes("Advance " + (index + 1).ToString(CultureInfo.InvariantCulture)),
                        true)),
                AiValue = 3
            };
        }
    }

    public void Improvements()
    {
        for (int i = 0; i < 67; ++i)
        {
            reader.NextLine();
            var name = Name(true);
            byte cost = RuleNormalization.Code8(
                reader.NextInteger()), upkeep = RuleNormalization.Code8(
                    reader.NextInteger());
            var prerequisite = ResolveAdvance(reader.NextToken());
            if (prerequisite == AdvanceReference.Invalid)
            {
                Fail(RuleLoadFault.Reference, "IMPROVE", i, 0, -5);
            }

            rules.Improvements[i] = new()
            {
                Identity = new("", name),
                CostRows = cost,
                Upkeep = upkeep,
                Prerequisite = prerequisite
            };
        }
    }

    public void WonderExpiry()
    {
        for (int i = 0; i < 28; ++i)
        {
            reader.NextLine();
            var value = ResolveAdvance(reader.NextToken());
            if (value == AdvanceReference.Invalid)
            {
                Fail(RuleLoadFault.Reference, "ENDWONDER", i, 0, -4);
            }

            rules.WonderObsolescence[i] = value;
        }
    }

    public void Units()
    {
        Array.Clear(movement);
        for (int i = 0; i < 62; ++i)
        {
            rules.Units[i] = rules.Units[i] with { Prerequisite = AdvanceReference.Never };
        }

        int index = 0;
        for (; index < 62; ++index)
        {
            reader.NextLine();
            if (reader.Line.IsEmpty || reader.Line[0] == ';')
            {
                break;
            }

            var name = Name(true);
            var obsolete = ResolveAdvance(reader.NextToken());
            var domain = (RuleDomain)RuleNormalization.Code8(reader.NextInteger());
            movement[index] = reader.NextInteger();
            byte range = RuleNormalization.Code8(
                reader.NextInteger()), attack = RuleNormalization.Code8(
                    reader.NextInteger()), defense = RuleNormalization.Code8(
                        reader.NextInteger());
            byte hp = RuleNormalization.ClassicHitPoints(
                reader.NextInteger()), firepower = RuleNormalization.Code8(
                    reader.NextInteger()), cost = RuleNormalization.UnitCost(
                        reader.NextInteger()), capacity = RuleNormalization.Code8(
                            reader.NextInteger());
            var role = (RuleRole)RuleNormalization.Code8(reader.NextInteger());
            var prerequisite = ResolveAdvance(reader.NextToken());
            uint abilities = reader.NextBinaryFlags();
            if (obsolete == AdvanceReference.Invalid || prerequisite == AdvanceReference.Invalid)
            {
                Fail(RuleLoadFault.Reference, "UNITS", index, 0, -6);
            }

            rules.Units[index] = new()
            {
                Identity = new("", name),
                ObsoleteBy = obsolete,
                Domain = domain,
                Range = range,
                Attack = attack,
                Defense = defense,
                HitPoints = hp,
                Firepower = firepower,
                CostRows = cost,
                Capacity = capacity,
                Role = role,
                Prerequisite = prerequisite,
                Abilities = abilities
            };
        }
        for (; index < 62; ++index)
        {
            rules.Units[index] = new()
            {
                Identity = new(
                    "",
                    Text(
                        Encoding.ASCII.GetBytes("Unit " + (index + 1).ToString(CultureInfo.InvariantCulture)),
                        true)),
                Attack = 1,
                CostRows = 1
            };
        }
    }

    public void Terrain()
    {
        for (int i = 0; i < 33; ++i)
        {
            reader.NextLine();
            var name = Name(true);
            byte movementCost = RuleNormalization.Code8(
                reader.NextInteger()), defense = RuleNormalization.Code8(
                    reader.NextInteger());
            byte food = RuleNormalization.Code8(
                reader.NextInteger()), shields = RuleNormalization.Code8(
                    reader.NextInteger()), trade = RuleNormalization.Code8(
                        reader.NextInteger());
            BaseTerrainWork? work = null;
            if (i < 11)
            {
                var parts = new TerrainWork[2];
                for (int p = 0; p < 2; ++p)
                {
                    var result = ResolveTerrain(reader.NextToken(), comparison);
                    byte bonus = RuleNormalization.Code8(
                        reader.NextInteger()), turns = RuleNormalization.Code8(
                            reader.NextInteger()), threshold = RuleNormalization.Code8(
                                reader.NextInteger());
                    if (result == TerrainResult.Invalid)
                    {
                        Fail(RuleLoadFault.Reference, "TERRAIN", i, p, -7);
                    }

                    parts[p] = new(result, bonus, turns, threshold);
                }
                var transformation = ResolveTerrain(reader.NextToken(), comparison);
                if (transformation == TerrainResult.Invalid)
                {
                    Fail(RuleLoadFault.Reference, "TERRAIN", i, 99, -7);
                }

                work = new(parts[0], parts[1], transformation);
            }
            rules.Terrain[i] = new()
            {
                Identity = new("", name),
                MovementCost = movementCost,
                DefenseFactor = defense,
                Food = food,
                Shields = shields,
                Trade = trade,
                Work = work
            };
        }
    }

    public void Governments()
    {
        for (int i = 0; i < 7; ++i)
        {
            reader.NextLine();
            rules.Governments[i] = new() { Identity = new("", Name(true)), Titles = new(Name(true), Name(true)) };
        }
    }

    public void Leaders()
    {
        for (int i = 0; i < 21; ++i)
        {
            titles[i].Clear();
            reader.NextLine();
            var names = new OwnedList<RuleText>([Name(true), Name(true)]);
            var gender = (Gender)Math.Clamp(reader.NextInteger(), 0, 1);
            byte color = RuleNormalization.LeaderColor(
                reader.NextInteger()), style = RuleNormalization.LeaderStyle(
                    reader.NextInteger());
            var plural = Name(true);
            var adjective = Name(true);
            var personality = new OwnedList<sbyte>(
                [
                    RuleNormalization.Signed8(reader.NextInteger()),
                    RuleNormalization.Signed8(reader.NextInteger()),
                    RuleNormalization.Signed8(reader.NextInteger())
                ]);
            rules.Tribes[i] = new()
            {
                Leaders = names,
                PreferredGender = gender,
                PreferredColor = color,
                CityStyle = style,
                Plural = plural,
                Adjective = adjective,
                Personality = personality
            };
            for (; ; )
            {
                int government = reader.NextInteger();
                if (government <= 0)
                {
                    break;
                }

                if (government > 6)
                {
                    Fail(RuleLoadFault.InvalidTitleIndex, "LEADERS", i, government, 0);
                }

                titles[i].Add(new((Government)government, new(Name(false), Name(false))));
            }
        }
    }

    public void Commodities()
    {
        for (int i = 0; i < 16; ++i)
        {
            reader.NextLine();
            rules.Commodities[i] = new("", Name(true));
        }
    }

    public void Orders()
    {
        for (int i = 0; i < 12; ++i)
        {
            reader.NextLine();
            var name = Name(false);
            byte[] key = reader.NextToken();
            rules.Orders[i] = new() { Identity = new("", name), Shortcut = key.Length == 0 ? (byte)0 : key[0] };
        }
    }

    public void Difficulties()
    {
        for (int i = 0; i < 6; ++i)
        {
            reader.NextLine();
            rules.Difficulties[i] = new("", Text(reader.Line.ToArray(), false));
        }
    }

    public void Attitudes()
    {
        for (int i = 0; i < 9; ++i)
        {
            reader.NextLine();
            rules.DiplomaticAttitudes[i] = new("", Text(reader.Line.ToArray(), false));
        }
    }
}
