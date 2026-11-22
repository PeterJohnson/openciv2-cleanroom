using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.City;

public sealed partial class CityWorking
{
    /// <summary>C02 blocked release never removes the center or changes specialists.</summary>
    public bool ReleaseBlocked(CitySlot slot)
    {
        ref var city = ref world.Entities.Cities.At(slot);
        bool humanBlocker = false;
        for (int i = 0; i < 20; ++i)
        {
            var status = Work.Availability[i];
            humanBlocker |= status.HasFlag(TileAvailability.HumanBlocker);
            if (status != TileAvailability.None)
            {
                city.WorkedTiles = city.WorkedTiles.With(i, false);
            }
        }
        return humanBlocker;
    }

    public int FoodRequirement(CitySlot city) => unchecked(world.Entities.Cities.At(city).Size * rules.Cosmic.FoodPerCitizen
        + Work.SupportedSettlers * Work.SettlerFood);

    /// <summary>C02 allocation against the existing availability/support workspace. Does not normalize
    /// specialist kinds, process food routes, advance stocks, or publish final city economy.</summary>
    public void Assign(CitySlot slot)
    {
        MapKnowledge.RequireLocal(world);
        ref var city = ref world.Entities.Cities.At(slot);
        int owner = unchecked((sbyte)city.Owner.Value);
        CityWorkSet selected = default;
        bool kept = false;
        Work.WorkersOverflow = false;
        Work.Unassigned = city.Size + 1;
        int target = city.SpecialistCount;
        if (!world.Globals.HumanPlayers.Contains(owner))
        {
            if (!city.Flags.Disorder && Work.Reallocating && target > 0)
            {
                if (!city.Flags.MoodOnEdge)
                {
                    --target;
                }
                else
                {
                    city.Flags.SpecialistHeld = true;
                }
            }
            if (city.Flags.Disorder)
            {
                city.WorkedTiles = default;
                city.SpecialistCount = 0;
            }
        }
        if (target == 0)
        {
            city.Flags.SpecialistHeld = false;
        }

        bool blocker = ReleaseBlocked(slot);
        Array.Clear(Work.Totals);
        var taken = new bool[21];
        for (int i = 0; i < 20; ++i)
        {
            taken[i] = Work.Availability[i] != TileAvailability.None;
        }

        if (world.Globals.HumanPlayers.Contains(owner) || (world.Globals.Turn + slot.Value) % 4 != 0 || city.Flags.Disorder || blocker)
        {
            for (int i = 0; i <= 20; ++i)
            {
                if (city.WorkedTiles.Contains(i))
                {
                    --Work.Unassigned;
                }
            }

            if (Work.Unassigned < 0)
            {
                Work.WorkersOverflow = true;
                Work.Unassigned = city.Size + 1;
            }
            else
            {
                Work.Unassigned = city.Size + 1;
                for (int visit = 0; visit <= 20; ++visit)
                {
                    int i = visit == 0 ? 20 : visit - 1;
                    if (!city.WorkedTiles.Contains(i))
                    {
                        continue;
                    }

                    Accumulate(slot, i, true);
                    taken[i] = true;
                    selected = selected.With(i);
                    if (--Work.Unassigned == 0)
                    {
                        break;
                    }
                }
            }
            kept = Work.Unassigned == target || Work.Unassigned == 0;
        }
        if (!kept)
        {
            if (Work.Unassigned > 0 && !taken[20])
            {
                Select(20);
            }

            int last = 0, divisor = Math.Max((int)rules.Cosmic.FoodPerCitizen, 1);
            while ((unchecked(Work.Totals[0] + Work.Unassigned / divisor) < FoodRequirement(slot) || city.Size < 3)
                && More() && last >= 0)
            {
                last = -1;
                int bestFood = 0, bestSecondary = 0;
                for (int i = 0; i < 20; ++i)
                {
                    if (taken[i])
                    {
                        continue;
                    }

                    int food = Yield(slot, i, CityYield.Food), shields = Yield(slot, i, CityYield.Shields);
                    if (Work.Unassigned == 1 && Work.Totals[1] == 0 && unchecked(shields * 2) == 0)
                    {
                        continue;
                    }

                    int secondary = unchecked(shields * 2 + Yield(slot, i, CityYield.Trade));
                    if (food > bestFood || (food == bestFood && secondary > bestSecondary))
                    { last = i; bestFood = food; bestSecondary = secondary; }
                }
                if (last >= 0)
                {
                    Select(last);
                }
            }
            if (!world.Globals.HumanPlayers.Contains(owner) && city.Flags.Disorder && Work.Reallocating)
            {
                city.Flags.SpecialistHeld = true;
                ++target;
            }
            while (More())
            {
                int best = -1, bestScore = 0;
                for (int i = 0; i < 20; ++i)
                {
                    if (taken[i])
                    {
                        continue;
                    }

                    Accumulate(slot, i, false);
                    int food = unchecked(16 / Arithmetic.ClampAB(unchecked(Work.Totals[0] - FoodRequirement(slot)), 1, 99) * Work.TileYield[0]);
                    int shields = unchecked(city.Size * 3 / Arithmetic.ClampAB(unchecked(Work.Totals[1] - Work.ShieldSupport), 1, 99) * Work.TileYield[1]);
                    int trade = unchecked(city.Size * 2 / Arithmetic.ClampAB(Work.Totals[2], 1, 99) * Work.TileYield[2]);
                    int score = unchecked(food + shields + trade);
                    if (score > bestScore)
                    { best = i; bestScore = score; }
                }
                if (best < 0)
                {
                    break;
                }

                Select(best);
            }
        }
        city.WorkedTiles = selected;
        city.SpecialistCount = (byte)(Work.Unassigned & 63);
        if (Work.Totals[0] < FoodRequirement(slot))
        {
            if (city.FoodBox == 0)
            {
                city.FoodBox = -1;
            }
        }
        else if (city.FoodBox < 0)
        {
            city.FoodBox = 0;
        }

        bool More() => unchecked((uint)Work.Unassigned) != (uint)target && unchecked(Work.Unassigned - target) >= 0;
        void Select(int i)
        {
            taken[i] = true;
            Accumulate(slot, i, true);
            selected = selected.With(i);
            --Work.Unassigned;
        }
    }

    /// <summary>C02 nominal specialist kinds; negative indices and non-kind arguments are outside this API's domain.</summary>
    public SpecialistKind Specialist(CitySlot slot, int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        return index >= 16 ? SpecialistKind.Entertainer : world.Entities.Cities.At(slot).Specialists[index];
    }

    public void SetSpecialist(CitySlot slot, int index, SpecialistKind kind)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        if (kind is < SpecialistKind.Unused or > SpecialistKind.Scientist)
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        if (index < 16)
        {
            world.Entities.Cities.At(slot).Specialists[index] = kind;
        }
    }

    public int CountSpecialists(CitySlot slot, SpecialistKind kind)
    {
        int count = 0;
        for (int i = 0; i < 16; ++i)
        {
            if (Specialist(slot, i) == kind)
            {
                ++count;
            }
        }

        return count;
    }

    public void AdjustSpecialists(CitySlot slot, int delta)
    {
        ref var city = ref world.Entities.Cities.At(slot);
        city.SpecialistCount = delta >= 0 ? (byte)(unchecked(city.SpecialistCount + delta) & 63)
            : (byte)Math.Max((long)city.SpecialistCount + delta, 0);
    }

    public void FillSpecialists(CitySlot slot)
    {
        Work.SpecialistCount = Work.Unassigned;
        ArgumentOutOfRangeException.ThrowIfNegative(Work.SpecialistCount);
        for (int i = 0; i < Math.Min(Work.SpecialistCount, 16); ++i)
        {
            if (Specialist(slot, i) == SpecialistKind.Unused)
            {
                SetSpecialist(slot, i, SpecialistKind.Entertainer);
            }
        }

        for (int i = Work.SpecialistCount; i < 16; ++i)
        {
            SetSpecialist(slot, i, SpecialistKind.Unused);
        }
    }
}
