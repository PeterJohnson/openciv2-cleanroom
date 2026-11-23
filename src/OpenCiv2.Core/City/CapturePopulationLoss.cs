using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.City;

/// <summary>C09 population loss and possible C01 deletion. The capture parent owns the mode/restoration gate.</summary>
public sealed class CapturePopulationLoss(GameState world, GameplayRandom random, CityRemoval removal,
    CivilizationElimination lifecycle, EspionageIncidents incidents)
{
    public Operation Begin(CitySlot city, int newOwner, int oldOwner, MapPoint capturedCenter, string call) =>
        new(this, city, newOwner, oldOwner, capturedCenter, call);
    private GameState World => world;
    private bool Human(int owner) => world.Globals.HumanPlayers.Contains(owner & 31);

    private void Reduce(CitySlot slot, int newOwner, int oldOwner, MapPoint center, string call)
    {
        bool humanOnRegion = false;
        if (newOwner == 0)
        {
            int region = world.Map.TileAt(center).Region;
            for (int c = 1; c < EntityLimits.Civilizations; ++c)
            {
                if (Human(c) && world.Civilizations[c].ContinentCities[region] != 0)
                {
                    humanOnRegion = true;
                }
            }
        }
        ref var city = ref world.Entities.Cities.At(slot);
        bool exceptional = city.Size < 2 && (!Human(newOwner) && !Human(oldOwner) || newOwner == 0 && Human(oldOwner))
            && (newOwner != 0 || humanOnRegion) && !world.Globals.HumanPlayers.Intersects(city.KnownTo)
            && world.Globals.PowerRank[newOwner] != 7;
        if (!exceptional || world.Globals.PowerRank[world.Globals.WorstHumanRank] < world.Globals.PowerRank[newOwner]
            && random.PrimaryModulo(new ChoiceKey("C09.capture.size-one", call), 2) != 0)
        {
            city.Size = unchecked((sbyte)(city.Size - 1));
        }
    }

    public sealed class Operation : IDisposable
    {
        private readonly CapturePopulationLoss parent;
        private readonly CitySlot city;
        private readonly int newOwner, oldOwner;
        private readonly MapPoint center;
        private readonly string call;
        private CivilizationElimination.Operation? child;
        private bool started, faulted, disposed;
        public bool Completed { get; private set; }
        public CitySlot? RemainingCity { get; private set; }
        internal Operation(CapturePopulationLoss parent, CitySlot city, int newOwner, int oldOwner, MapPoint center, string call)
        {
            ArgumentException.ThrowIfNullOrEmpty(call);
            this.parent = parent;
            this.city = city;
            this.newOwner = newOwner;
            this.oldOwner = oldOwner;
            this.center = center;
            this.call = call;
            RemainingCity = city;
        }
        /// <summary>A pending incident or null; Completed=false with null means bounded lifecycle work remains.</summary>
        public LifecycleDecision? Resume(int? reply = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            {
                throw new InvalidOperationException("A failed capture population operation cannot resume");
            }

            if (Completed)
            {
                return null;
            }

            try
            {
                MapKnowledge.RequireLocal(parent.World);
                if (!started)
                {
                    started = true;
                    parent.Reduce(city, newOwner, oldOwner, center, call);
                    if (parent.World.Entities.Cities.At(city).Size == 0)
                    {
                        child = parent.Remove(city, call);
                    }
                    else
                    { Completed = true; return null; }
                    reply = null;
                }
                var pending = child!.Resume(reply);
                if (pending is not null)
                {
                    return pending;
                }

                if (child.Result is null)
                {
                    return null;
                }

                child.Dispose();
                child = null;
                RemainingCity = null;
                Completed = true;
                return null;
            }
            catch { faulted = true; child?.Dispose(); throw; }
        }
        public void Dispose() { disposed = true; child?.Dispose(); }
    }
    private CivilizationElimination.Operation Remove(CitySlot city, string call) =>
        lifecycle.BeginRemoveCity(removal, city.Value, call + "/delete", incidents);
}
