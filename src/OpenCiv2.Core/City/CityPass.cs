using OpenCiv2.Contracts;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.City;

/// <summary>F04 §6 city-pass boundaries and descending live traversal. The caller
/// completes each actual city turn before reporting its balance or removal.
/// This does not supply F04's still-open city-analysis arithmetic.</summary>
public sealed class CityPass(GameState world, GovernmentPolicy governments)
{
    public Operation Begin(int owner)
    {
        MapKnowledge.RequireLocal(world);
        if (owner < 0 || owner >= EntityLimits.Civilizations)
        { throw new ArgumentOutOfRangeException(nameof(owner)); }

        world.Civilizations[owner].BestShieldSurplus = 0;
        world.Civilizations[owner].GovernmentScore = default;
        world.Production.WonderNotices[owner] = default;
        world.Globals.TransientFlags.AttitudeDirty = false;
        return new(world, governments, owner);
    }

    public sealed class Operation
    {
        private readonly GameState world;
        private readonly GovernmentPolicy governments;
        private readonly int owner;
        private int nextCity;
        private CitySlot? selected;
        private bool exhausted, largeBalancedCity, disorder, faulted;
        public bool Finished { get; private set; }

        internal Operation(GameState world, GovernmentPolicy governments, int owner)
        {
            this.world = world;
            this.governments = governments;
            this.owner = owner;
            nextCity = world.Entities.Cities.End - 1;
        }

        public CitySlot? NextCity()
        {
            RequireUsable();
            if (selected is not null)
            { throw new InvalidOperationException("Complete the selected city turn before advancing the pass"); }
            while (nextCity >= 0)
            {
                var slot = new CitySlot((ushort)nextCity--);
                ref var city = ref world.Entities.Cities.At(slot);
                if (!city.Token.Occupied || city.Owner.Value != owner)
                { continue; }
                city.Flags.Airlifted = false;
                selected = slot;
                return slot;
            }
            exhausted = true;
            return null;
        }

        /// <param name="happinessBalance">The city's returned happy-minus-unhappy
        /// count, or null when the actual city turn removed the city.</param>
        public void CompleteCity(int? happinessBalance)
        {
            RequireUsable();
            if (selected is not { } slot)
            { throw new InvalidOperationException("No city turn is selected"); }
            if (happinessBalance is { } balance)
            {
                ref var city = ref world.Entities.Cities.At(slot);
                largeBalancedCity |= balance == 0 && city.Size > 5;
                disorder |= city.Flags.Disorder;
                for (int observer = 1; observer < EntityLimits.Civilizations; ++observer)
                {
                    if (world.Civilizations[owner].Treaties[observer].Alliance
                        || world.Globals.WonderCity[WonderIds.Apollo] != -1)
                    { city.RememberedSize[observer] = unchecked((byte)city.Size); }
                }
            }
            selected = null;
        }

        public void Finish()
        {
            RequireUsable();
            if (Finished)
            { return; }
            if (!exhausted || selected is not null)
            { throw new InvalidOperationException("Complete the city scan before finishing the pass"); }
            try
            {
                ref var scores = ref world.Civilizations[owner].GovernmentScore;
                scores[(int)Government.Anarchy] = unchecked((short)(scores[(int)Government.Anarchy]
                    + 2 * scores[(int)Government.Despotism]));
                governments.AfterCities(owner, largeBalancedCity, disorder);
                Finished = true;
            }
            catch
            {
                faulted = true;
                throw;
            }
        }

        private void RequireUsable()
        {
            if (faulted)
            { throw new InvalidOperationException("The city pass failed and cannot be resumed"); }
            MapKnowledge.RequireLocal(world);
        }
    }
}
