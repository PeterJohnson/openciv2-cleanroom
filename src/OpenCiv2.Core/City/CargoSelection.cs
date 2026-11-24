using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.City;

public sealed record CargoOption(int Slot, string Name);
public abstract record CargoDecision(int Owner);
public sealed record CargoMenu(int Actor, CityTarget City, string CityName, OwnedList<CargoOption> Options) : CargoDecision(Actor);
public sealed record CargoConfirmation(int Actor, int Slot, string Name) : CargoDecision(Actor);
public sealed record CargoReport(int Actor, string Name, bool Food, OwnedList<CommodityCity> Cities) : CargoDecision(Actor);

/// <summary>C07 production cargo. Menu Extra=0 chooses, nonzero requests a report;
/// confirmation Selected=0 accepts, Extra!=0 retains the caller's city-zoom request.</summary>
public sealed class CargoSelection(GameState world, RuleTables rules, GameplayRandom random, string foodName)
{
    public const byte Food = 255;
    private GameState World => world;
    private string FoodName => foodName;
    private ref State.City City(CitySlot slot) => ref world.Entities.Cities.At(slot);
    private ref Unit Unit(UnitSlot slot) => ref world.Entities.Units.At(slot);
    private string Name(int cargo) => cargo >= 0 && cargo < rules.Commodities.Length
        ? rules.Commodities[cargo].Name : throw new InvalidOperationException("Cargo name requires explicit commodity backing: " + cargo);
    private int Supply(CitySlot city, int slot)
    {
        if (slot < 0 || slot >= CityLimits.TradeRoutes)
        { throw new InvalidOperationException("Cargo selection requires a backed supply slot"); }
        return City(city).Supplies[slot];
    }

    public void SelectAi(CitySlot city, UnitSlot unit, string call)
    {
        MapKnowledge.RequireLocal(world);
        ArgumentException.ThrowIfNullOrEmpty(call);
        Unit(unit).Counter = Food;
        int start = random.PrimaryModulo(new ChoiceKey("C07.cargo-start", call), 3);
        for (int i = 0; i < CityLimits.TradeRoutes; ++i)
        {
            int cargo = Supply(city, (start + i) % 3);
            if (cargo >= 0)
            { Unit(unit).Counter = (byte)cargo; break; }
        }
        if (unchecked((sbyte)Unit(unit).Counter) < 0)
        {
            int slot = random.PrimaryModulo(new ChoiceKey("C07.cargo-fallback", call), 3);
            Unit(unit).Counter = unchecked((byte)Math.Abs(Supply(city, slot)));
        }
        City(city).Flags.HadCaravan = true;
    }

    public Operation BeginHuman(CitySlot city, int owner, UnitSlot unit) => new(this, city, owner, unit);

    public sealed class Operation : IDisposable
    {
        private readonly CargoSelection parent;
        private readonly CitySlot city;
        private readonly UnitSlot unit;
        private readonly int owner;
        private int selected;
        private bool disposed, faulted;
        public bool Completed { get; private set; }
        public bool ZoomRequested { get; private set; }
        public CargoDecision? Pending { get; private set; }
        internal Operation(CargoSelection parent, CitySlot city, int owner, UnitSlot unit)
        { this.parent = parent; this.city = city; this.owner = owner; this.unit = unit; }

        public CargoDecision? Resume(ResearchReply? reply = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            {
                throw new InvalidOperationException("A failed cargo selection cannot resume");
            }

            if (Completed)
            {
                return null;
            }

            try
            {
                MapKnowledge.RequireLocal(parent.World);
                if (Pending is not null && reply is null)
                {
                    return Pending;
                }

                if (Pending is CargoMenu)
                {
                    selected = reply!.Value.Selected;
                    string name = selected < 3 ? parent.Name(parent.Supply(city, selected)) : parent.FoodName;
                    if (reply.Value.Extra == 0)
                    {
                        return Pending = new CargoConfirmation(owner, selected, name);
                    }

                    return Pending = new CargoReport(owner, name, selected >= 3, selected >= 3 ? new([])
                        : CommodityReport.Cities(parent.World, owner, parent.Supply(city, selected)));
                }
                if (Pending is CargoConfirmation && reply!.Value.Selected == 0)
                {
                    ZoomRequested = reply.Value.Extra != 0;
                    if (selected < 3)
                    {
                        int cargo = Math.Abs(parent.Supply(city, selected));
                        parent.Unit(unit).Counter = unchecked((byte)cargo);
                        parent.City(city).Supplies[selected] = unchecked((sbyte)-cargo);
                    }
                    else
                    {
                        parent.Unit(unit).Counter = Food;
                    }

                    Pending = null;
                    Completed = true;
                    return null;
                }
                // Report results never select cargo; confirmation rejection rebuilds live options.
                List<CargoOption> options = [];
                for (int i = 0; i < CityLimits.TradeRoutes; ++i)
                {
                    int cargo = parent.Supply(city, i);
                    if (cargo >= 0)
                    {
                        options.Add(new(i, parent.Name(cargo)));
                    }
                }
                options.Add(new(3, parent.FoodName));
                ref var live = ref parent.City(city);
                return Pending = new CargoMenu(owner, new(city, live.Token), live.Name, new(options));
            }
            catch { faulted = true; throw; }
        }
        public void Dispose() => disposed = true;
    }
}
