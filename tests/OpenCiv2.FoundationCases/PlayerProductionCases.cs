using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // C06 player purchasing/selling; real C03 recalculation and retained C06 snapshot.
    private sealed class PlayerProductionFixture : IPlayerProductionEffects
    {
        public OrdersFixture Orders { get; } = new();
        public GameState World => Orders.World;
        public RuleTables Rules => Orders.Rules;
        public ref City City => ref Orders.City;
        public PlayerProduction Service { get; }
        public List<string> Events { get; } = [];
        public bool FailPurchased;
        public PlayerProductionFixture()
        {
            Rules.Cosmic = Rules.Cosmic with { ShieldBoxRows = 10, FoodBoxRows = 10, FoodPerCitizen = 2 };
            for (int i = 0; i < Rules.Terrain.Length; ++i)
                Rules.Terrain[i] = new()
                {
                    MovementCost = 1,
                    Food = 2,
                    Shields = 1,
                    Trade = 1,
                    Work = new(new(TerrainResult.ImproveInPlace, 1), new(TerrainResult.ImproveInPlace, 1))
                };
            World.Civilizations[1].Gold = 1000;
            Service = new(World, Rules, new(World, Orders.Data.Process.Rules, Orders.Data.Stacks), Orders.Orders, this);
        }
        public void Sound(ProductionTransactionSound sound) => Events.Add(sound.ToString());
        public void SaleDenied(CityTarget city, ProductionSaleDenial reason) => Events.Add(reason.ToString());
        public void Purchased(ProductionPurchaseQuote quote)
        { Events.Add("paid"); if (FailPurchased) throw new InvalidOperationException("presentation failed"); }
        public void Sold(ProductionSaleQuote quote) => Events.Add("sold");
        public void Changed(ProductionChangeQuote quote, bool accepted) => Events.Add(accepted ? "changed" : "rejected");
    }

    private static void PlayerPurchasePricesAndCompletion()
    {
        var f = new PlayerProductionFixture(); f.City.ShieldBox = 10;
        var purchase = f.Service.BeginPurchase(new(0))!;
        Equal(105, purchase.Quote.Price); Equal(1000, purchase.Quote.Treasury);
        Equal(ProductionPurchaseAvailability.Available, purchase.Quote.Availability);
        Equal(true, purchase.Complete(true)); Equal(895, f.World.Civilizations[1].Gold);
        Equal((short)40, f.City.ShieldBox); Equal(40, f.World.Production.SavedStock);
        Equal(ProductionCategory.Units, f.World.Production.SavedCategory); Equal(0, f.World.Entities.Units.End);
        Sequence(new[] { "Purchase", "paid" }, f.Events);
        Throws<InvalidOperationException>(() => purchase.Complete(true)); Equal(895, f.World.Civilizations[1].Gold);
        f.City.ShieldBox = 0; Equal(320, f.Service.BeginPurchase(new(0))!.Quote.Price);
        f.City.Production = -4; Equal(120, f.Service.BeginPurchase(new(0))!.Quote.Price);
        f.City.ShieldBox = 10; Equal(40, f.Service.BeginPurchase(new(0))!.Quote.Price);
        f.City.Production = -35; Equal(80, f.Service.BeginPurchase(new(0))!.Quote.Price);
        f.City.Production = -39; Equal(80, f.Service.BeginPurchase(new(0))!.Quote.Price);
        f.City.ShieldBox = 0; Equal(240, f.Service.BeginPurchase(new(0))!.Quote.Price);
        f.City.Production = -38; Equal<PlayerProduction.Purchase?>(null, f.Service.BeginPurchase(new(0)));
    }

    private static void PlayerPurchaseDenialCancellationAndFailure()
    {
        var f = new PlayerProductionFixture(); f.City.ShieldBox = 10;
        var purchase = f.Service.BeginPurchase(new(0))!; Equal(false, purchase.Complete(false));
        Equal(1000, f.World.Civilizations[1].Gold); Equal((short)10, f.City.ShieldBox); Equal(0, f.Events.Count);
        f.World.Civilizations[1].Gold = 104; purchase = f.Service.BeginPurchase(new(0))!;
        Equal(ProductionPurchaseAvailability.InsufficientFunds, purchase.Quote.Availability); Equal(false, purchase.Complete(true));
        f.City.Flags.Disorder = true; purchase = f.Service.BeginPurchase(new(0))!;
        Equal(ProductionPurchaseAvailability.Disorder, purchase.Quote.Availability); Equal(false, purchase.Complete(true));
        f.City.Production = -4; purchase = f.Service.BeginPurchase(new(0))!;
        Equal(ProductionPurchaseAvailability.Available, purchase.Quote.Availability); Equal(true, purchase.Complete(true));
        Equal(64, f.World.Civilizations[1].Gold); Equal(false, BuildingQueries.HasBuilding(f.World, new(0), 4));
        f.World.Multiplayer.LocalHuman = 2; Equal<PlayerProduction.Purchase?>(null, f.Service.BeginPurchase(new(0)));
        f.World.Globals.RevealMap = 1; Equal(true, f.Service.BeginPurchase(new(0)) is not null);
        f = new(); f.City.ShieldBox = 10; f.FailPurchased = true; purchase = f.Service.BeginPurchase(new(0))!;
        Throws<InvalidOperationException>(() => purchase.Complete(true)); Equal(895, f.World.Civilizations[1].Gold);
        Equal((short)40, f.City.ShieldBox); Throws<InvalidOperationException>(() => purchase.Complete(true));
        Equal(895, f.World.Civilizations[1].Gold);
    }

    private static void PlayerPurchaseSignedCostsAndLimits()
    {
        var f = new PlayerProductionFixture(); f.Rules.Units[2] = f.Rules.Units[2] with { CostRows = 255 };
        f.City.ShieldBox = 1; var purchase = f.Service.BeginPurchase(new(0))!;
        Equal(0, purchase.Quote.Price); Equal(true, purchase.Complete(true)); Equal((short)-10, f.City.ShieldBox);
        f.City.ShieldBox = short.MinValue; purchase = f.Service.BeginPurchase(new(0))!;
        Equal(51898, purchase.Quote.Price); // Remaining clamps to999 before the quadratic price.
        f.City.Production = -4; f.Rules.Improvements[4] = f.Rules.Improvements[4] with { CostRows = 255 };
        f.Rules.Cosmic = f.Rules.Cosmic with { ShieldBoxRows = 255 }; f.City.ShieldBox = 0;
        f.World.Civilizations[1].Gold = 10000; purchase = f.Service.BeginPurchase(new(0))!;
        Equal(3996, purchase.Quote.Price); Equal(true, purchase.Complete(true)); Equal((short)-511, f.City.ShieldBox);
    }

    private static void PlayerSaleRestrictionsAndCommit()
    {
        var f = new PlayerProductionFixture(); f.City.Improvements = default(ImprovementSet).With(1).With(4).With(6);
        Equal<PlayerProduction.Sale?>(null, f.Service.BeginSale(new(0), 1));
        Sequence(new[] { "Denied", "Palace" }, f.Events); f.Events.Clear();
        Equal<PlayerProduction.Sale?>(null, f.Service.BeginSale(new(0), 35));
        Equal<PlayerProduction.Sale?>(null, f.Service.BeginSale(new(0), 5));
        var sale = f.Service.BeginSale(new(0), 4)!; Equal(30, sale.Quote.Price);
        Equal(false, sale.Complete(false)); Equal(true, f.City.Improvements.Contains(4)); Equal(false, f.City.Flags.ImprovementSold);
        sale = f.Service.BeginSale(new(0), 4)!; Equal(true, sale.Complete(true));
        Equal(1030, f.World.Civilizations[1].Gold); Equal(false, f.City.Improvements.Contains(4)); Equal(true, f.City.Flags.ImprovementSold);
        Equal((sbyte)2, f.City.Production); Sequence(new[] { "Sale", "sold" }, f.Events);
        Throws<InvalidOperationException>(() => sale.Complete(true)); Equal(1030, f.World.Civilizations[1].Gold);
        Equal<PlayerProduction.Sale?>(null, f.Service.BeginSale(new(0), 6)); Equal("AlreadySold", f.Events.Last());
        f.World.Globals.RevealMap = 1; Equal<PlayerProduction.Sale?>(null, f.Service.BeginSale(new(0), 6));
        f.World.Globals.Status.Scenario = true;
        Equal(true, f.Service.BeginSale(new(0), 1)!.Complete(true)); Equal(false, f.City.Improvements.Contains(1));
        f.World.Civilizations[1].Gold = int.MaxValue;
        Equal(true, f.Service.BeginSale(new(0), 6)!.Complete(true)); Equal(unchecked(int.MaxValue + 30), f.World.Civilizations[1].Gold);
    }

    private static void PlayerProductionChangeRetainsSnapshot()
    {
        var f = new PlayerProductionFixture(); f.City.ShieldBox = 101; f.Orders.Orders.SaveBuildState(new(0));
        f.World.Civilizations[1].UnitsInProduction[2] = 1;
        f.City.Flags.Autobuild = f.City.Flags.UnitsOnlyAutomation = f.City.Flags.ImprovementsOnlyAutomation = true;
        var change = f.Service.BeginChange(new(0), -4)!;
        Equal(true, change.Quote.ConfirmationRequired); Equal(51, change.Quote.Stock); Equal(50, change.Quote.PenaltyPercent);
        Equal(false, f.City.Flags.Autobuild); Equal(false, f.City.Flags.UnitsOnlyAutomation); Equal(false, f.City.Flags.ImprovementsOnlyAutomation);
        Equal(false, change.Complete(false)); Equal((sbyte)2, f.City.Production); Equal((short)101, f.City.ShieldBox);
        Equal((byte)1, f.World.Civilizations[1].UnitsInProduction[2]); Equal(101, f.World.Production.SavedStock);
        change = f.Service.BeginChange(new(0), -4)!; Equal(true, change.Complete(true));
        Equal((short)51, f.City.ShieldBox); Equal((byte)0, f.World.Civilizations[1].UnitsInProduction[2]);
        change = f.Service.BeginChange(new(0), 2)!; Equal(false, change.Quote.ConfirmationRequired); Equal(true, change.Complete(true));
        Equal((short)101, f.City.ShieldBox); Equal(101, f.World.Production.SavedStock); Equal((byte)1, f.World.Civilizations[1].UnitsInProduction[2]);
        Equal(0, f.Orders.Choices.Count); Equal(0, f.Orders.Notices.Count);
        Throws<InvalidOperationException>(() => change.Complete(true)); Equal((byte)1, f.World.Civilizations[1].UnitsInProduction[2]);
    }

    private static void PlayerProductionChangeSuppressionAndDirectAi()
    {
        var f = new PlayerProductionFixture(); f.City.ShieldBox = 101; f.Orders.Orders.SaveBuildState(new(0));
        f.World.Globals.MessageOptions.ProductionPenalty = true;
        var change = f.Service.BeginChange(new(0), -39)!; Equal(false, change.Quote.ConfirmationRequired);
        f.World.Globals.HumanPlayers = default; f.World.Globals.RevealMap = 1;
        Equal(true, change.Complete(true)); Equal((short)51, f.City.ShieldBox); Equal(0, f.Orders.Notices.Count);
        f.World.Globals.Difficulty = Difficulty.Chieftain; change = f.Service.BeginChange(new(0), -4)!;
        Equal(101, change.Quote.Stock); Equal(true, change.Complete(true)); Equal((short)101, f.City.ShieldBox);
        f.World.Globals.Difficulty = Difficulty.King; f.World.Production.SavedStock = -1;
        change = f.Service.BeginChange(new(0), -4)!; Equal(-32768, change.Quote.Stock); Equal(true, change.Complete(true));
        Equal(short.MinValue, f.City.ShieldBox);
    }
}
