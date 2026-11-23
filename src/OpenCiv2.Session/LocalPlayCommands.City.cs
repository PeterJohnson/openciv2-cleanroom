using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Session;

public static partial class LocalPlayCommands
{
    public const string Found = "city.found";
    public const string Worker = "city.worker";
    public const string Production = "city.production";
    public const string Buy = "city.buy";
    public const string Sell = "city.sell";

    private static void RegisterCityCommands(SessionRuntime runtime)
    {
        runtime.RegisterCommand(Define(Found, "Found your first city with a pioneer and an accepted name.",
            [new("unit", ValueKind.Unit), new("name", ValueKind.Text)], [new("city", ValueKind.City)],
            FoundCity, ValidateUnit));
        runtime.RegisterCommand(Define(Worker, "Toggle a worked radius tile (0–20). Select 20 to reset workers.",
            [new("city", ValueKind.City), new("tile")], [new("city", ValueKind.City)], AssignWorker, ValidateCity));
        runtime.RegisterCommand(new(
            new(1, Production, "Choose a listed production item for an owned city.", AccessClass.Player,
                new([new("city", ValueKind.City), new("type")]), new([new("city", ValueKind.City)])),
            (actor, request, world) => new(ValidatePhase(actor, world) ?? ValidateCity(actor, request, world)),
            request => new ProductionTransaction(request, false, change: true)));
        foreach (bool sale in new[] { false, true })
        {
            runtime.RegisterCommand(new(
                new(1, sale ? Sell : Buy, sale ? "Confirm selling an owned improvement." : "Confirm buying shields for current production.",
                    AccessClass.Player, new(sale ? [new("city", ValueKind.City), new("improvement")] : [new("city", ValueKind.City)]),
                    new([new("city", ValueKind.City)])),
                (actor, request, world) => new(ValidatePhase(actor, world) ?? ValidateCity(actor, request, world)),
                request => new ProductionTransaction(request, sale)));
        }
    }

    private static CityTarget CityArgument(CommandRequest request) =>
        ((CommandValue.City)CommandDescriptor.Value(request.Arguments, "city")).Value;

    private static string? ValidateCity(CommandActor actor, CommandRequest request, GameState world)
    {
        var target = CityArgument(request);
        var cities = world.Entities.Cities;
        return cities.Live(target.Slot) && cities.At(target.Slot).Token == target.Token
            && cities.At(target.Slot).Owner == actor.Civilization ? null : "city_not_available";
    }

    private static OperationStep FoundCity(OperationContext context, CommandRequest request)
    {
        var world = context.World;
        var rules = context.Process.Rules.Definitions;
        var target = UnitArgument(request);
        var unit = world.Entities.Units.At(target.Slot);
        string name = ((CommandValue.Text)CommandDescriptor.Value(request.Arguments, "name")).Value.Trim();
        if (name.Length is < 1 or > 15 || name.Any(c => c is < ' ' or > '~'))
        {
            return new(CommandStatus.Failed, Code: "invalid_city_name");
        }

        ref var civ = ref world.Civilizations[unit.Owner.Value];
        var map = world.Map;
        var point = unit.Position;
        ref var tile = ref map.TileAt(point);
        if (unit.Type.Value != LocalGameRules.Settler || unit.Orders != UnitOrder.None
            || civ.CityCount != 0 || tile.Ocean || tile.City || tile.Airbase)
        {
            return new(CommandStatus.Failed, Code: "founding_not_available");
        }

        // The bounded profile supplies an accepted player name and has no default-name catalog.
        // C01 creates before U01 erases the founder, avoiding the last-settler elimination branch.
        var stacks = new StackOperations(world, new(100000, 64));
        var units = new UnitCreation(world, context.Process, stacks, new LocalCreation(world, rules, stacks));
        var creator = new CityCreation(world, context.Process, new NameCatalog([], StringComparer.OrdinalIgnoreCase.Equals), units,
            () => throw new InvalidOperationException("City capacity cannot be reached in the one-city profile"));
        if (creator.Create(unit.Owner, point) is not { } slot)
        {
            return new(CommandStatus.Failed, Code: "city_capacity");
        }
        ref var city = ref world.Entities.Cities.At(slot);
        city.Name = name;
        var token = city.Token;
        civ.TotalCitySize = 1;

        EraseLocalUnit(world, rules, target.Slot);
        tile.Features &= ~(MapFeatures.Irrigation | MapFeatures.Mine | MapFeatures.Road | MapFeatures.Railroad);
        for (int observer = 1; observer < EntityLimits.Civilizations; observer++)
        {
            map.Remembered(observer, point) = tile.Features;
        }

        LocalCityEconomy.RememberCity(world, ref city);
        LocalCityEconomy.Recalculate(world, context.Process.Rules, slot);
        SelectReadyUnit(context);
        return Commit(context, "city.founded", new NamedValue("city", new CommandValue.City(new(slot, token))));
    }

    private static OperationStep AssignWorker(OperationContext context, CommandRequest request)
    {
        var target = CityArgument(request);
        int index = Number(request, "tile");
        var world = context.World;
        var city = world.Entities.Cities.At(target.Slot);
        if (index is < 0 or > LocalCityEconomy.Center || !LocalCityEconomy.Available(world, city, index))
        {
            return new(CommandStatus.Failed, Code: "worker_tile_not_available");
        }

        LocalCityEconomy.Toggle(world, context.Process.Rules, target.Slot, index);
        return Commit(context, "city.workers", new NamedValue("city", new CommandValue.City(target)));
    }

    private sealed class ProductionTransaction(CommandRequest request, bool sale, bool change = false) : IContinuation, IPlayerProductionEffects
    {
        private PlayerProduction.Purchase? purchase;
        private PlayerProduction.Sale? selling;
        private PlayerProduction.Change? changing;
        private OperationContext? current;
        private OperationContext Current => current ?? throw new InvalidOperationException("No active production command");

        public OperationStep Resume(OperationContext context, OperationInput input)
        {
            current = context;
            try
            {
                if (input.Kind is OperationInputKind.Cancel or OperationInputKind.Timeout)
                {
                    purchase?.Complete(false);
                    selling?.Complete(false);
                    changing?.Complete(false);
                    return new(CommandStatus.Cancelled, Code: "cancelled");
                }
                if (input.Kind == OperationInputKind.Begin)
                {
                    if ((ValidatePhase(context.Actor, context.World) ?? ValidateCity(context.Actor, request, context.World)) is { } error)
                    {
                        return new(CommandStatus.Failed, Code: error);
                    }

                    var world = context.World;
                    var rules = context.Process.Rules;
                    var city = CityArgument(request);
                    if (change && !LocalGameRules.ProductionAvailable(world, rules.Definitions, Number(request, "type"), city.Slot))
                    {
                        return new(CommandStatus.Failed, Code: "production_not_available");
                    }

                    var orders = new ProductionOrders(world, rules.Definitions,
                        _ => throw new InvalidOperationException("Manual production transactions do not choose automatically"),
                        _ => throw new InvalidOperationException("Manual production transactions do not invoke AI wonder changes"));
                    var service = new PlayerProduction(world, rules.Definitions,
                        new(world, rules, new(world, new(5_000_000, 64))), orders, this);
                    if (change)
                    {
                        // This atomic command opens one selection; the general city dialog keeps
                        // its opening snapshot across previews. The authored profile is Chieftain.
                        orders.SaveBuildState(city.Slot);
                        changing = service.BeginChange(city.Slot, Number(request, "type"));
                        if (changing is null)
                        {
                            return new(CommandStatus.Failed, Code: "production_not_available");
                        }

                        var proposal = changing.Quote;
                        if (proposal.ConfirmationRequired)
                        {
                            return new(CommandStatus.AwaitingInteraction, Interaction: new(
                                $"Change {proposal.PreviousName} to {proposal.Name}? The {proposal.PenaltyPercent}% category penalty leaves {proposal.Stock} shields.",
                                null, null, new([0, 1])));
                        }

                        changing.Complete(true);
                        return new(Result: new([new("city", new CommandValue.City(city))]));
                    }
                    if (sale)
                    {
                        selling = service.BeginSale(city.Slot, Number(request, "improvement"));
                        if (selling is null)
                        {
                            return new(CommandStatus.Failed, Code: "sale_not_available");
                        }

                        return new(CommandStatus.AwaitingInteraction, Interaction: new(
                            $"Sell {selling.Quote.Name} for {selling.Quote.Price} gold?", null, null, new([0, 1])));
                    }
                    purchase = service.BeginPurchase(city.Slot);
                    if (purchase is null)
                    {
                        return new(CommandStatus.Failed, Code: "purchase_not_available");
                    }

                    var quote = purchase.Quote;
                    string prompt = quote.Availability switch
                    {
                        ProductionPurchaseAvailability.Disorder => "Units cannot be purchased while this city is in disorder.",
                        ProductionPurchaseAvailability.InsufficientFunds => $"Buying {quote.Name} costs {quote.Price} gold; treasury has {quote.Treasury}.",
                        _ => $"Buy shields for {quote.Name} for {quote.Price} gold? Treasury: {quote.Treasury}. Production completes during the city turn."
                    };
                    return new(CommandStatus.AwaitingInteraction, Interaction: new(prompt, null, null,
                        new(quote.Availability == ProductionPurchaseAvailability.Available ? [0, 1] : [0])));
                }
                bool accepted = input.Kind == OperationInputKind.Choice && input.Choice == 1;
                bool completed = change ? changing!.Complete(accepted) : sale ? selling!.Complete(accepted) : purchase!.Complete(accepted);
                return completed ? new(Result: new([new("city", new CommandValue.City(CityArgument(request)))]))
                    : new(CommandStatus.Cancelled, Code: "cancelled");
            }
            finally { current = null; }
        }

        public void Sound(ProductionTransactionSound sound) => Current.Effect("city.transaction_sound",
            new([new("sound", new CommandValue.Text(sound.ToString()))]));
        public void SaleDenied(CityTarget city, ProductionSaleDenial reason) => Current.Effect("city.sale_denied",
            new([new("city", new CommandValue.City(city)), new("reason", new CommandValue.Text(reason.ToString()))]));
        public void Purchased(ProductionPurchaseQuote quote) => Current.Effect("city.production_bought",
            new([new("city", new CommandValue.City(quote.City)), new("price", new CommandValue.Integer(quote.Price))]));
        public void Sold(ProductionSaleQuote quote) => Current.Effect("city.improvement_sold",
            new([new("city", new CommandValue.City(quote.City)), new("improvement", new CommandValue.Integer(quote.Improvement)),
                new("price", new CommandValue.Integer(quote.Price))]));
        public void Changed(ProductionChangeQuote quote, bool accepted) => Current.Effect(accepted ? "city.production" : "city.production_rejected",
            new([new("city", new CommandValue.City(quote.City))]));
    }

    internal static UnitSlot? CreateUnit(SessionRuntime runtime, int type, MapPoint position, CitySlot? home) =>
        CreateUnit(runtime.World, runtime.Process, type, position, home);

    private static UnitSlot? CreateUnit(GameState world, ProcessContext process, int type, MapPoint position, CitySlot? home)
    {
        var stacks = new StackOperations(world, new(100000, 64));
        var creator = new UnitCreation(world, process, stacks, new LocalCreation(world, process.Rules.Definitions, stacks));
        var slot = creator.Create(new((byte)type), new(LocalGameRules.Owner), position);
        if (slot is { } created)
        {
            world.Entities.Units.At(created).Home = home;
        }
        return slot;
    }

    private sealed class LocalCreation(GameState world, RuleTables rules, StackOperations stacks)
        : IUnitCreationEffects, IUnitRevelationEffects
    {
        public void RevealSurroundings(int slot, bool allowContact) =>
            new UnitRevelation(world, rules, stacks, this).Reveal(slot, allowContact);

        public void Notice(UnitCreationNotice notice)
        {
            if (notice != UnitCreationNotice.TooManyUnits)
            {
                throw new InvalidOperationException("Creation tutorials require their session presentation service");
            }
            // The production caller reports capacity refusal with its own city result.
        }

        public void RevealOrigin(MapPoint position) =>
            throw new InvalidOperationException("Foreign contact is outside the authored local profile");

        public void Meet(int actingCivilization, int otherCivilization, MapPoint position) =>
            throw new InvalidOperationException("Diplomacy requires its owning session service");

        public void SynchronizeCreation() => throw new InvalidOperationException("Transport is unavailable");
    }

    private static void EraseLocalUnit(GameState world, RuleTables rules, UnitSlot slot) =>
        new UnitDeletion(world, rules, new(100000, 64), LocalDeletion.Instance).Erase(slot.Value, false);

    // This profile has neither transport nor a path to eliminating the last player city.
    private sealed class LocalDeletion : IUnitDeletionEffects
    {
        public static LocalDeletion Instance { get; } = new();
        public void UnitRemoved(MapPoint position) => throw new InvalidOperationException("Use a session effect");
        public void RefreshHomeCity(int city) => throw new InvalidOperationException("Recalculate in the owning phase");
        public void CheckCivilization(int owner, int killer) => throw new InvalidOperationException("Elimination is unavailable");
        public void SynchronizeDeletion(MapPoint position, CitySlot? home) =>
            throw new InvalidOperationException("Transport is unavailable");
    }
}
