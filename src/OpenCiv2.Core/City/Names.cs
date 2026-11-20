using OpenCiv2.Base;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using RuntimeCity = OpenCiv2.Core.State.City;

namespace OpenCiv2.Core.City;

public static class CityNames
{
    /// <summary>C01 default naming: capture owner/plural, reread its live tribe, increment before lookup and retry
    /// at most twice.</summary>
    public static bool DefaultName(
        ref RuntimeCity city,
        Civilization[] civilizations,
        Leader[] leaders,
        Func<uint, OwnedList<byte>> resolvePlural,
        NameCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(resolvePlural);
        int owner = city.Owner.Value;
        Leader LiveLeader()
        {
            if (owner >= civilizations.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(city), "Naming owner has no civilization backing");
            }

            int tribe = civilizations[owner].Tribe;
            if (tribe < 0 || tribe >= leaders.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(civilizations), "Naming tribe has no leader backing");
            }

            return leaders[tribe];
        }
        var plural = resolvePlural(LiveLeader().Plural.Id);
        for (int attempt = 0; attempt < 2; ++attempt)
        {
            var leader = LiveLeader();
            leader.CityNameIndex = unchecked((byte)(leader.CityNameIndex + 1));
            var result = catalog.Select(plural.ToArray(), leader.CityNameIndex);
            if (result.Outcome == NameOutcome.Unavailable)
            {
                break;
            }

            if (result.Outcome == NameOutcome.Selected)
            {
                city.Name = new(result.Bytes.TakeWhile(b => b != 0).Take(15));
                return true;
            }
            LiveLeader().CityNameIndex = 0;
        }
        return false;
    }

    public static bool DefaultName(
        ref RuntimeCity city,
        Civilization[] civilizations,
        RuleState rules,
        NameCatalog catalog) =>
        DefaultName(ref city, civilizations, rules.Leaders, rules.Text.Text, catalog);
}
