using OpenCiv2.Base;
using OpenCiv2.Contracts;

namespace OpenCiv2.Core.Rules;

/// <summary>F03 ordered installation. Live text, reload policy and normalization stay independent of input
/// syntax.</summary>
public sealed class RuleInstaller
{
    private readonly RuleInput input;
    private readonly RuleState retained;

    public RuleInstaller(RuleInput input, RuleState retained)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(retained);
        this.input = input;
        this.retained = retained;
    }

    private static string NormalizeText(string value, bool blankAsSpace)
    {
        return blankAsSpace && value.Length == 0 ? RuleText.Space : value;
    }

    private static RuleName NameInto(
        RuleName target,
        RuleName value,
        bool blankAsSpace,
        bool keep = false) =>
        new(
            value.Id.Length != 0 ? value.Id : target.Id,
            keep ? target.Name : NormalizeText(value.Name, blankAsSpace));

    private static string LeaderName(
        string target,
        ref LeaderText binding,
        string value,
        bool blankAsSpace,
        bool replace)
    {
        if (!replace)
        {
            return target;
        }

        var text = blankAsSpace && value.Length == 0 ? RuleText.Space : value;
        binding = new(RuleText.Display(text));
        return text;
    }

    public void Load(bool replaceLeaderData)
    {
        Cosmic();
        Advances();
        Improvements();
        WonderExpiry();
        Units();
        Terrain();
        Governments();
        Leaders(replaceLeaderData);
        Commodities();
        Orders();
        Difficulties();
        Attitudes();
        retained.IsLoaded = true;
    }

    public void Cosmic() => retained.Definitions.Cosmic = input.Definitions.Cosmic;

    public void Advances(bool keepNames = false)
    {
        var values = retained.Definitions;
        values.EnabledAdvanceCount = 0;
        for (int i = 0; i < RuleLimits.Advances; ++i)
        {
            values.Advances[i] = values.Advances[i] with
            {
                Prerequisites = new([AdvanceReference.Never, AdvanceReference.Never]),
                Enabled = false
            };
        }

        for (int i = 0; i < RuleLimits.Advances; ++i)
        {
            var source = input.Definitions.Advances[i];
            values.Advances[i] = source with
            {
                Identity = NameInto(
                    values.Advances[i].Identity,
                    source.Identity,
                    true,
                    keepNames),
                Enabled = source.Prerequisites[0] != AdvanceReference.Never
            };
        }
        for (int i = 0; i < RuleLimits.Advances; ++i)
        {
            if (values.Advances[i].Enabled)
            {
                ++values.EnabledAdvanceCount;
                for (int j = 0; j < RuleLimits.AdvancePrerequisites; ++j)
                {
                    var prerequisites = values.Advances[i].Prerequisites.ToArray();
                    prerequisites[j] = RuleNormalization.FollowDisabled(prerequisites[j], values.Advances);
                    values.Advances[i] = values.Advances[i] with { Prerequisites = new(prerequisites) };
                }
            }
        }
    }

    public void Improvements(bool keepNames = false)
    {
        var values = retained.Definitions;
        for (int i = 0; i < RuleLimits.Improvements; ++i)
        {
            var source = input.Definitions.Improvements[i];
            values.Improvements[i] = source with
            {
                Identity = NameInto(
                    values.Improvements[i].Identity,
                    source.Identity,
                    true,
                    keepNames)
            };
            values.Improvements[i] = values.Improvements[i] with
            {
                Prerequisite = RuleNormalization.FollowDisabled(source.Prerequisite, values.Advances)
            };
        }
    }

    public void WonderExpiry() =>
        input.Definitions.WonderObsolescence.ToArray().CopyTo(retained.Definitions.WonderObsolescence, 0);

    public void Units(bool keepNames = false)
    {
        var values = retained.Definitions;
        retained.DoubleMovementApplied = false;
        for (int i = 0; i < RuleLimits.UnitTypes; ++i)
        {
            values.Units[i] = values.Units[i] with { Prerequisite = AdvanceReference.Never };
        }

        for (int i = 0; i < RuleLimits.UnitTypes; ++i)
        {
            var source = input.Definitions.Units[i];
            values.Units[i] = source with
            {
                Identity = NameInto(
                    values.Units[i].Identity,
                    source.Identity,
                    true,
                    keepNames),
                Movement = RuleNormalization.Movement(
                    input.MovementPoints[i],
                    values.Cosmic.RoadMovementMultiplier)
            };
        }
    }

    public void Terrain()
    {
        var values = retained.Definitions;
        for (int i = 0; i < RuleLimits.Terrains; ++i)
        {
            var source = input.Definitions.Terrain[i];
            values.Terrain[i] = source with
            {
                Identity = NameInto(
                    values.Terrain[i].Identity,
                    source.Identity,
                    true),
                Work = i < RuleLimits.BaseTerrains ? source.Work : values.Terrain[i].Work
            };
        }
    }

    public void Governments()
    {
        var values = retained.Definitions;
        for (int i = 0; i < RuleLimits.Governments; ++i)
        {
            var source = input.Definitions.Governments[i];
            values.Governments[i] = values.Governments[i] with
            {
                Identity = NameInto(
                    values.Governments[i].Identity,
                    source.Identity,
                    true)
            };
            var male = NormalizeText(source.Titles.Male, true);
            values.Governments[i] = values.Governments[i] with
            {
                Titles = values.Governments[i].Titles with { Male = male }
            };
            var female = NormalizeText(source.Titles.Female, true);
            values.Governments[i] = values.Governments[i] with
            {
                Titles = values.Governments[i].Titles with { Female = female }
            };
        }
    }

    public void Leaders(bool replace)
    {
        var values = retained.Definitions;
        for (int i = 0; i < RuleLimits.Tribes; ++i)
        {
            var live = retained.Leaders[i];
            var source = input.Definitions.Tribes[i];
            if (source.Id.Length != 0)
            {
                values.Tribes[i] = values.Tribes[i] with { Id = source.Id };
            }

            for (int sex = 0; sex < RuleLimits.LeaderGenders; ++sex)
            {
                var names = values.Tribes[i].Leaders.ToArray();
                names[sex] = LeaderName(names[sex], ref live.Names[sex], source.Leaders[sex], true, replace);
                values.Tribes[i] = values.Tribes[i] with { Leaders = new(names) };
            }
            if (replace)
            {
                values.Tribes[i] = values.Tribes[i] with
                {
                    PreferredGender = source.PreferredGender,
                    PreferredColor = source.PreferredColor,
                    CityStyle = source.CityStyle
                };
                live.Female = source.PreferredGender == Gender.Female;
                live.LeaderName = live.Names[live.Female ? 1 : 0];
                live.Color = source.PreferredColor;
                live.CityStyle = source.CityStyle;
            }
            values.Tribes[i] = values.Tribes[i] with
            {
                Plural = LeaderName(values.Tribes[i].Plural, ref live.Plural, source.Plural, true, replace)
            };
            values.Tribes[i] = values.Tribes[i] with
            {
                Adjective = LeaderName(
                    values.Tribes[i].Adjective,
                    ref live.Adjective,
                    source.Adjective,
                    true,
                    replace)
            };
            if (replace)
            {
                values.Tribes[i] = values.Tribes[i] with
                {
                    Personality = source.Personality,
                    Titles = new(values.Governments.Select(g => g.Titles))
                };
                live.Attack = source.Personality[0];
                live.Expand = source.Personality[1];
                live.Civilize = source.Personality[2];
                for (int g = 0; g < RuleLimits.Governments; ++g)
                {
                    for (int sex = 0; sex < RuleLimits.LeaderGenders; ++sex)
                    {
                        var titles = values.Governments[g].Titles;
                        live.Titles[g][sex] = new(RuleText.Display((sex == 0 ? titles.Male : titles.Female)));
                    }
                }
            }
            foreach (var title in input.TitleOverrides[i])
            {
                int g = (int)title.Government;
                if (g <= 0 || g >= RuleLimits.Governments)
                {
                    throw new ArgumentException("Rule title override requires government 1..6");
                }

                var titles = values.Tribes[i].Titles.ToArray();
                titles[g] = titles[g] with
                {
                    Male = LeaderName(titles[g].Male, ref live.Titles[g][0], title.Titles.Male, false, replace)
                };
                values.Tribes[i] = values.Tribes[i] with { Titles = new(titles) };
                titles[g] = titles[g] with
                {
                    Female = LeaderName(
                        titles[g].Female,
                        ref live.Titles[g][1],
                        title.Titles.Female,
                        false,
                        replace)
                };
                values.Tribes[i] = values.Tribes[i] with { Titles = new(titles) };
            }
        }
    }

    public void Commodities()
    {
        for (int i = 0; i < RuleLimits.Commodities; ++i)
        {
            retained.Definitions.Commodities[i] = NameInto(
                retained.Definitions.Commodities[i],
                input.Definitions.Commodities[i],
                true);
        }
    }

    public void Orders()
    {
        for (int i = 0; i < RuleLimits.Orders; ++i)
        {
            var source = input.Definitions.Orders[i];
            retained.Definitions.Orders[i] = source with
            {
                Identity = NameInto(
                    retained.Definitions.Orders[i].Identity,
                    source.Identity,
                    false)
            };
        }
    }

    public void Difficulties()
    {
        for (int i = 0; i < RuleLimits.Difficulties; ++i)
        {
            retained.Definitions.Difficulties[i] = NameInto(
                retained.Definitions.Difficulties[i],
                input.Definitions.Difficulties[i],
                false);
        }
    }

    public void Attitudes()
    {
        for (int i = 0; i < RuleLimits.DiplomaticAttitudes; ++i)
        {
            retained.Definitions.DiplomaticAttitudes[i] = NameInto(
                retained.Definitions.DiplomaticAttitudes[i],
                input.Definitions.DiplomaticAttitudes[i],
                false);
        }
    }
}
