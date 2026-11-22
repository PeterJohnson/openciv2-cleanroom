using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.City;

public enum ProductionPurchaseAvailability { Available, InsufficientFunds, Disorder }
public enum ProductionSaleDenial { AlreadySold, Palace }
public enum ProductionTransactionSound { Purchase, Sale, Denied }
public sealed record ProductionPurchaseQuote(CityTarget City, int Owner, int Item, string Name,
    int Price, int Treasury, ProductionPurchaseAvailability Availability);
public sealed record ProductionSaleQuote(CityTarget City, int Improvement, string Name, int Price);

public interface IPlayerProductionEffects
{
    void Sound(ProductionTransactionSound sound);
    void SaleDenied(CityTarget city, ProductionSaleDenial reason);
    void Purchased(ProductionPurchaseQuote quote);
    void Sold(ProductionSaleQuote quote);
}

/// <summary>C06 local player purchase and sale. Accepted commands retain a semantic target;
/// incidental window selection and original list-row offsets do not retarget a pending decision.
/// The session owns serialization, cancellation and stale-target admission.</summary>
public sealed class PlayerProduction(GameState world, RuleTables rules, CityEconomy economy,
    ProductionOrders orders, IPlayerProductionEffects effects)
{
    private GameState World => world;
    private RuleTables Rules => rules;
    private CityEconomy Economy => economy;
    private ProductionOrders Orders => orders;
    private IPlayerProductionEffects Effects => effects;
    private bool MayControl(int owner) => world.Multiplayer.LocalHuman == owner || world.Globals.RevealMap != 0;
    private bool ScenarioEdit => world.Globals.RevealMap != 0 && world.Globals.Status.HasScenario;
    private CityTarget Target(CitySlot slot) => new(slot, world.Entities.Cities.At(slot).Token);
    private bool Live(CityTarget target) => world.Entities.Cities.Live(target.Slot)
        && world.Entities.Cities.At(target.Slot).Token == target.Token;

    public Purchase? BeginPurchase(CitySlot slot)
    {
        MapKnowledge.RequireLocal(world);
        economy.Calculate(slot, false);
        ref var city = ref world.Entities.Cities.At(slot);
        int owner = unchecked((sbyte)city.Owner.Value), item = city.Production;
        if (!MayControl(owner) || item == -BuildingIds.Capitalization)
        {
            return null;
        }

        int cost = item >= 0 ? unchecked((sbyte)rules.Units[item].CostRows) : rules.Improvements[-item].CostRows;
        int remaining = Arithmetic.ClampAB(unchecked(rules.Cosmic.ShieldBoxRows * cost - city.ShieldBox), 0, 999);
        int price = item >= 0 ? remaining * remaining / 20 + 2 * remaining
            : -item < BuildingIds.Structural ? 2 * remaining : 4 * remaining;
        if (city.ShieldBox == 0)
        {
            price *= 2;
        }

        int treasury = world.Civilizations[owner].Gold;
        var availability = item >= 0 && city.Flags.Disorder ? ProductionPurchaseAvailability.Disorder
            : price <= treasury ? ProductionPurchaseAvailability.Available : ProductionPurchaseAvailability.InsufficientFunds;
        string name = item >= 0 ? rules.Units[item].Identity.Name : rules.Improvements[-item].Identity.Name;
        return new(this, new(Target(slot), owner, item, name, price, treasury, availability), cost);
    }

    /// <summary>Improvement identities come from the displayed owned-facility list, not a raw UI row.</summary>
    public Sale? BeginSale(CitySlot slot, int improvement)
    {
        MapKnowledge.RequireLocal(world);
        ref var city = ref world.Entities.Cities.At(slot);
        if (!MayControl(unchecked((sbyte)city.Owner.Value)))
        {
            return null;
        }

        if (city.Flags.ImprovementSold && !ScenarioEdit)
        {
            Deny(slot, ProductionSaleDenial.AlreadySold);
            return null;
        }

        economy.Calculate(slot, false);
        if (improvement is < BuildingIds.Palace or > BuildingIds.Last || !BuildingQueries.HasBuilding(world, slot, improvement))
        {
            return null;
        }

        if (improvement == BuildingIds.Palace && !ScenarioEdit)
        {
            Deny(slot, ProductionSaleDenial.Palace);
            return null;
        }

        int price = rules.Improvements[improvement].CostRows * rules.Cosmic.ShieldBoxRows;
        return new(this, new(Target(slot), improvement, rules.Improvements[improvement].Identity.Name, price));
    }

    private void Deny(CitySlot slot, ProductionSaleDenial reason)
    {
        effects.Sound(ProductionTransactionSound.Denied);
        effects.SaleDenied(Target(slot), reason);
    }

    public sealed class Purchase
    {
        private readonly PlayerProduction parent;
        private readonly int cost;
        public ProductionPurchaseQuote Quote { get; }
        public bool Finished { get; private set; }
        internal Purchase(PlayerProduction parent, ProductionPurchaseQuote quote, int cost)
        { this.parent = parent; Quote = quote; this.cost = cost; }

        public bool Complete(bool accepted)
        {
            if (Finished)
            {
                throw new InvalidOperationException("Purchase already ended.");
            }
            // Mark terminal before effects so failure cannot repeat a payment.
            Finished = true;
            if (!accepted || Quote.Availability != ProductionPurchaseAvailability.Available || !parent.Live(Quote.City))
            {
                return false;
            }

            var world = parent.World;
            MapKnowledge.RequireLocal(world);
            world.Civilizations[Quote.Owner].Gold = unchecked(world.Civilizations[Quote.Owner].Gold - Quote.Price);
            world.Entities.Cities.At(Quote.City.Slot).ShieldBox = unchecked((short)(parent.Rules.Cosmic.ShieldBoxRows * cost));
            parent.Orders.SaveBuildState(Quote.City.Slot);
            parent.Effects.Sound(ProductionTransactionSound.Purchase);
            parent.Economy.Calculate(Quote.City.Slot);
            parent.Effects.Purchased(Quote);
            return true;
        }
    }

    public sealed class Sale
    {
        private readonly PlayerProduction parent;
        public ProductionSaleQuote Quote { get; }
        public bool Finished { get; private set; }
        internal Sale(PlayerProduction parent, ProductionSaleQuote quote) { this.parent = parent; Quote = quote; }

        public bool Complete(bool accepted)
        {
            if (Finished)
            {
                throw new InvalidOperationException("Sale already ended.");
            }

            Finished = true;
            if (!accepted || !parent.Live(Quote.City))
            {
                return false;
            }

            var world = parent.World;
            MapKnowledge.RequireLocal(world);
            parent.Effects.Sound(ProductionTransactionSound.Sale);
            BuildingQueries.SetBuilding(world, Quote.City.Slot, Quote.Improvement, false);
            ref var city = ref world.Entities.Cities.At(Quote.City.Slot);
            int owner = unchecked((sbyte)city.Owner.Value);
            world.Civilizations[owner].Gold = unchecked(world.Civilizations[owner].Gold + Quote.Price);
            city.Flags.ImprovementSold = true;
            parent.Economy.Calculate(Quote.City.Slot);
            parent.Effects.Sold(Quote);
            return true;
        }
    }
}
