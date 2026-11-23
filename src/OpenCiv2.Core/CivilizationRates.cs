using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

public enum RateKind { Tax, Science, Luxury }
public readonly record struct RateValues(int Tax, int Science, int Luxury);
public readonly record struct RateHolds(int Tax, int Science, int Luxury);
public readonly record struct RatePreview(int Tax, int Upkeep, int Science);
public readonly record struct RateReport(RatePreview Preview, int DiscoveryTurns);

/// <summary>V02 §9 bounded rate draft. Preferred values and the cap belong to preparation;
/// adjustments do not change the civilization. Holds may retain multiple imported selections.</summary>
public sealed class RateDraft
{
    public int Cap { get; }
    public RateValues Preferred { get; }
    public RateValues Selected { get; private set; }
    public RateHolds Holds { get; private set; }
    public bool LimitWarning { get; private set; }

    internal RateDraft(int cap, RateValues values, RateHolds holds, bool warning)
    {
        Cap = cap;
        Preferred = Selected = values;
        Holds = holds;
        LimitWarning = warning;
    }

    public void Hold(RateKind kind, int value)
    {
        if (kind is < RateKind.Tax or > RateKind.Luxury)
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }
        if (value != 0)
        {
            Holds = default;
        }
        Holds = kind switch
        {
            RateKind.Tax => Holds with { Tax = value },
            RateKind.Science => Holds with { Science = value },
            _ => Holds with { Luxury = value }
        };
    }

    public void Adjust(RateKind kind, int requested)
    {
        if (kind is < RateKind.Tax or > RateKind.Luxury)
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }
        LimitWarning |= requested > Cap;
        int a = Math.Clamp(requested, 0, Cap);
        var (b, c, preferred, holdB, holdC) = kind switch
        {
            RateKind.Tax => (Selected.Science, Selected.Luxury, Preferred.Science, Holds.Science, Holds.Luxury),
            RateKind.Science => (Selected.Luxury, Selected.Tax, Preferred.Luxury, Holds.Luxury, Holds.Tax),
            _ => (Selected.Tax, Selected.Science, Preferred.Tax, Holds.Tax, Holds.Science)
        };
        // Draft values originate in byte-rate normalization and these adjustments;
        // no arbitrary signed extremes enter the reference's potentially wrapping loops.
        while (a + b + c < 10)
        {
            if (b < Cap && b < preferred && holdB == 0)
            {
                ++b;
            }
            else if (c < Cap && holdC == 0)
            {
                ++c;
            }
            else if (b < Cap && holdB == 0)
            {
                ++b;
            }
            else
            {
                ++a;
            }
        }
        while (a + b + c > 10)
        {
            if (b != 0 && holdB == 0)
            {
                --b;
            }
            else if (c != 0 && holdC == 0)
            {
                --c;
            }
            else
            {
                --a;
            }
        }
        Selected = kind switch
        {
            RateKind.Tax => new(a, b, c),
            RateKind.Science => new(c, a, b),
            _ => new(b, c, a)
        };
    }
}

/// <summary>V02 §§7–10 local rate preparation, real city distribution previews and accepted changes.
/// Preparation normalization and preview city fields are committed effects, independent of repainting.
/// Preference persistence is an explicit required child; a failed write does not undo accepted rates.</summary>
public sealed class CivilizationRates(GameState world, ProcessContext process, StackOperations stacks)
{
    private readonly CityEconomy economy = new(world, process.Rules, stacks);
    private readonly BuildingMaintenance upkeep = new(world, process.Rules.Definitions,
        _ => throw new InvalidOperationException("Rate preview cannot collect maintenance"));

    public static int Maximum(GameState world, int owner) => unchecked((byte)world.Civilizations[owner].Government)
        switch
    {
        < 2 => 6,
        2 => 7,
        < 6 => 8,
        _ => 10
    };

    public static bool Allowed(GameState world, int owner, int tax, int science)
    {
        int cap = Maximum(world, owner);
        return tax >= 0 && tax <= cap && science >= 0 && science <= cap
            && 10 - science - tax >= 0 && 10 - science - tax <= cap;
    }

    public RateDraft Prepare(int owner, RateHolds holds = default)
    {
        MapKnowledge.RequireLocal(world);
        int cap = Maximum(world, owner);
        ref var civ = ref world.Civilizations[owner];
        bool warning = false;
        while (civ.TaxRate > cap)
        {
            --civ.TaxRate;
            if (civ.ScienceRate < cap)
            {
                ++civ.ScienceRate;
            }
            warning = true;
        }
        while (civ.ScienceRate > cap)
        {
            --civ.ScienceRate;
            if (civ.TaxRate < cap)
            {
                ++civ.TaxRate;
            }
            warning = true;
        }
        while (10 - civ.ScienceRate - civ.TaxRate > cap)
        {
            if (civ.TaxRate < cap)
            {
                ++civ.TaxRate;
            }
            else
            {
                ++civ.ScienceRate;
            }
            warning = true;
        }
        return new(cap, new(civ.TaxRate, civ.ScienceRate, 10 - civ.ScienceRate - civ.TaxRate), holds, warning);
    }

    /// <summary>One captured cap across real rate decisions. Network routing belongs to the mode adapter.</summary>
    public void EnforceMaximum(int owner, Action<int> chooseRates)
    {
        MapKnowledge.RequireLocal(world);
        if (!world.Globals.HumanPlayers.Contains(owner))
        {
            return;
        }
        int cap = Maximum(world, owner);
        if (world.Civilizations[owner].TaxRate > cap)
        {
            chooseRates(owner);
        }
        if (10 - world.Civilizations[owner].ScienceRate - world.Civilizations[owner].TaxRate > cap)
        {
            chooseRates(owner);
        }
        if (world.Civilizations[owner].ScienceRate > cap)
        {
            chooseRates(owner);
        }
    }

    public RatePreview Preview(int owner, RateDraft draft)
    {
        MapKnowledge.RequireLocal(world);
        ref var civ = ref world.Civilizations[owner];
        byte science = civ.ScienceRate, tax = civ.TaxRate;
        civ.TaxRate = unchecked((byte)draft.Selected.Tax);
        civ.ScienceRate = unchecked((byte)draft.Selected.Science);
        var buildings = new int[BuildingIds.Capitalization + 1];
        int totalTax = 0, totalScience = 0, totalUpkeep = 0;
        for (int i = 0; i < world.Entities.Cities.End; ++i)
        {
            ref var city = ref world.Entities.Cities.Storage[i];
            if (!city.Token.Occupied || unchecked((sbyte)city.Owner.Value) != owner)
            {
                continue;
            }
            var slot = new CitySlot((ushort)i);
            economy.Distribute(slot, city.Trade, 1);
            for (int building = 0; building < buildings.Length; ++building)
            {
                if (BuildingQueries.HasBuilding(world, slot, building))
                {
                    ++buildings[building];
                }
            }
            if (!city.Flags.Disorder)
            {
                totalTax = unchecked(totalTax + city.Tax);
                totalScience = unchecked(totalScience + city.Science);
            }
        }
        for (int building = 0; building < buildings.Length; ++building)
        {
            if (buildings[building] != 0)
            {
                totalUpkeep = unchecked(totalUpkeep + buildings[building] * upkeep.Quote(owner, building));
            }
        }
        // A real child failure retains the accepted prefix, including temporary rates.
        civ.TaxRate = tax;
        civ.ScienceRate = science;
        return new(totalTax, totalUpkeep, totalScience);
    }

    /// <summary>Explicit report preparation owns the science floor; drawing only reads this result.</summary>
    public RateReport Report(int owner, RatePreview preview)
    {
        int cost = Math.Max(1, Research.Cost(world, process.Rules.Definitions, owner));
        preview = preview with { Science = Math.Max(1, preview.Science) };
        return new(preview, unchecked(preview.Science - 1 + cost) / preview.Science);
    }

    /// <summary>Accept full-width selections. UI/command owners validate their current authority and
    /// choice domain first. Returns preference persistence success; rates survive a false result.</summary>
    public bool Accept(int owner, RateValues selected, Func<bool> savePreferences)
    {
        ArgumentNullException.ThrowIfNull(savePreferences);
        MapKnowledge.RequireLocal(world);
        ref var civ = ref world.Civilizations[owner];
        bool changed = civ.TaxRate != selected.Tax || civ.ScienceRate != selected.Science;
        civ.TaxRate = unchecked((byte)selected.Tax);
        civ.ScienceRate = unchecked((byte)selected.Science);
        bool saved = true;
        if (world.Globals.Turn < 11 && unchecked((byte)civ.Government) < (byte)Government.Monarchy)
        {
            process.OrdinaryPreferences.Science = civ.ScienceRate;
            process.OrdinaryPreferences.Tax = civ.TaxRate;
            saved = savePreferences();
        }
        if (changed)
        {
            world.Globals.TransientFlags.AttitudeDirty = false;
            for (int i = 0; i < world.Entities.Cities.End; ++i)
            {
                ref var city = ref world.Entities.Cities.Storage[i];
                if (city.Token.Occupied && unchecked((sbyte)city.Owner.Value) == owner)
                {
                    economy.Calculate(new((ushort)i));
                }
            }
        }
        return saved;
    }
}
