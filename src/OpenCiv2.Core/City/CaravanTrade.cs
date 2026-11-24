using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.City;

public enum CaravanAction { Continue, Deliver, HelpBuildWonder }
public enum CaravanSound { Freight = 22, Delivery = 42, WonderDonation = 44 }
public sealed record CaravanMenu(int Actor, UnitTarget Unit, CityTarget City, string CargoName, string CityName,
    bool? Demanded, OwnedList<CaravanAction> Actions);
public abstract record CaravanDecision
{
    public sealed record Menu(CaravanMenu Request) : CaravanDecision;
    public sealed record Lifecycle(LifecycleDecision Decision) : CaravanDecision;
}
public sealed record CaravanPayment(CitySlot Home, int Actor, int Victim, int Cargo, bool Freight, int Payment);
public sealed record CaravanDeliveryNotice(int Recipient, UnitTarget Unit, CaravanPayment Delivery,
    string CargoName, string HomeName, string DestinationName);
public sealed record CaravanReciprocalNotice(int Recipient, UnitType Type, int Actor, int Victim, int Cargo, int ReturnCargo,
    string Adjective, string CargoName, string HomeName, string DestinationName, string ReturnName);
public sealed record CaravanDonationNotice(int Recipient, CitySlot City, string CityName, int Shields, int Remaining);
public interface ICaravanTradeEffects
{
    void Sound(CaravanSound sound);
    void Delivery(CaravanDeliveryNotice notice);
    void Reciprocal(CaravanReciprocalNotice notice);
    void Donation(CaravanDonationNotice notice);
    void RefreshStatus();
}

/// <summary>C07 local city entry and complete delivery. Uses actual C03 cached mode, V03 cost,
/// U01/V01 deletion, diplomacy and route/commodity children; U02 owns movement admission.
/// Extra commodity names are explicit backing for otherwise undefined reciprocal lookups.</summary>
public sealed class CaravanTrade(GameState world, RuleTables rules, GameplayRandom random, CityEconomy economy,
    CivilizationElimination lifecycle, EspionageIncidents incidents, ICaravanTradeEffects effects,
    string foodName, IReadOnlyDictionary<int, string>? extraCommodityNames = null)
{
    private GameState World => world;
    private ref Unit Unit(UnitSlot slot) => ref world.Entities.Units.At(slot);
    private ref State.City City(CitySlot slot) => ref world.Entities.Cities.At(slot);
    private UnitRule Rule(UnitSlot slot) => rules.Units[Unit(slot).Type.Value];
    private bool Human(int owner) => world.Globals.HumanPlayers.Contains(owner & 31);
    private byte HomeByte(UnitSlot slot) => unchecked((byte)(Unit(slot).Home?.Value ?? 255));
    private string Name(int cargo, bool allowFood = false)
    {
        if (allowFood && cargo < 0)
        { return foodName ?? throw new InvalidOperationException("Caravan Food name is required"); }
        if (cargo >= 0 && cargo < rules.Commodities.Length)
        { return rules.Commodities[cargo].Name; }
        return extraCommodityNames is not null && extraCommodityNames.TryGetValue(cargo, out var name) ? name
            : throw new InvalidOperationException("Caravan commodity name requires explicit backing: " + cargo);
    }
    public Operation BeginEntry(UnitSlot unit, CitySlot city, string call) => new(this, unit, city, call, true);
    public Operation BeginDelivery(UnitSlot unit, CitySlot city, string call) => new(this, unit, city, call, false);

    public sealed class Operation : IDisposable
    {
        private readonly CaravanTrade parent;
        private readonly IEnumerator<CaravanDecision?> steps;
        private readonly List<Exception> presentationFailures = [];
        private ResearchReply? reply;
        private bool disposed, faulted;
        public int? Result { get; internal set; }
        public bool Completed => Result is not null;
        public CaravanPayment? Payment { get; internal set; }
        public int? ReturnCargo { get; internal set; }
        public CaravanDecision? Pending { get; private set; }
        public IReadOnlyList<Exception> PresentationFailures => presentationFailures.AsReadOnly();
        internal Operation(CaravanTrade parent, UnitSlot unit, CitySlot city, string call, bool entry)
        {
            ArgumentException.ThrowIfNullOrEmpty(call);
            this.parent = parent;
            steps = (entry ? parent.Enter(this, unit, city, call) : parent.Deliver(this, unit, city, call)).GetEnumerator();
        }
        internal ResearchReply? TakeDecision() { var result = reply; reply = null; return result; }
        internal int? TakeReply() => TakeDecision()?.Selected;
        internal void Report(IEnumerable<Exception> errors) => presentationFailures.AddRange(errors);
        internal void Present(Action action) { try { action(); } catch (Exception error) { presentationFailures.Add(error); } }
        public CaravanDecision? Resume(int? answer = null) => ResumeDecision(answer is { } value ? new(value) : null);
        public CaravanDecision? ResumeDecision(ResearchReply? answer = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("A failed caravan operation cannot resume"); }
            if (Completed)
            { return null; }
            try
            {
                MapKnowledge.RequireLocal(parent.World);
                if (Pending is not null && answer is null)
                { return Pending; }
                reply = Pending is null ? null : answer;
                Pending = null;
                if (steps.MoveNext())
                { return Pending = steps.Current; }
                steps.Dispose();
                return null;
            }
            catch { faulted = true; steps.Dispose(); throw; }
        }
        public void Dispose() { disposed = true; steps.Dispose(); }
    }

    private IEnumerable<CaravanDecision?> Delete(Operation operation, UnitSlot unit, string call)
    {
        using var child = lifecycle.BeginDelete(unit.Value, true, call, incidents);
        while (child.Result is null)
        {
            if (child.ResumeDecision(operation.TakeDecision()) is { } pending)
            { yield return new CaravanDecision.Lifecycle(pending); }
            else if (child.Result is null)
            { yield return null; }
        }
        operation.Report(child.PresentationFailures);
    }
    private IEnumerable<CaravanDecision?> Enter(Operation operation, UnitSlot unit, CitySlot destination, string call)
    {
        if (world.Multiplayer.Saved.AutoplayActive)
        { operation.Result = 0; yield break; }
        int owner = unchecked((sbyte)Unit(unit).Owner.Value);
        int action = (int)CaravanAction.Deliver;
        if (unchecked((sbyte)HomeByte(unit)) == destination.Value && !Human(owner))
        { action = 0; }
        if (City(destination).Owner.Value == owner && (!Human(owner) && City(destination).Flags.FerryPort
            || Unit(unit).Orders == UnitOrder.GoTo && Unit(unit).Destination != City(destination).Position))
        { action = 0; }
        if (Human(owner) && owner == world.Multiplayer.LocalHuman && City(destination).Owner.Value == owner && action != 0)
        {
            int cargo = unchecked((sbyte)Unit(unit).Counter);
            string cargoName = Name(cargo, true);
            bool demanded = false;
            for (int i = 0; cargo >= 0 && i < CityLimits.TradeRoutes; ++i)
            { if (City(destination).Demands[i] == cargo) { demanded = true; } }
            List<CaravanAction> choices = [CaravanAction.Continue];
            if (unchecked((sbyte)HomeByte(unit)) != destination.Value)
            { choices.Add(CaravanAction.Deliver); }
            if (City(destination).Production < -BuildingIds.Capitalization)
            { choices.Add(CaravanAction.HelpBuildWonder); }
            action = 0;
            if (choices.Count > 1)
            {
                yield return new CaravanDecision.Menu(new(owner, new(unit, Unit(unit).Token), new(destination, City(destination).Token),
                    cargoName, City(destination).Name, cargo < 0 ? null : demanded, new(choices)));
                action = operation.TakeReply()!.Value;
            }
        }
        if (action == (int)CaravanAction.Deliver)
        {
            foreach (var pending in Deliver(operation, unit, destination, call + "/delivery"))
            { yield return pending; }
            yield break;
        }
        if (action == (int)CaravanAction.HelpBuildWonder)
        {
            operation.Present(() => effects.Sound(Unit(unit).Type.Value == UnitTypeIds.Freight ? CaravanSound.Freight : CaravanSound.WonderDonation));
            City(destination).ShieldBox = unchecked((short)(City(destination).ShieldBox + unchecked((sbyte)Rule(unit).CostRows) * rules.Cosmic.ShieldBoxRows));
            int shields = unchecked(unchecked((sbyte)Rule(unit).CostRows) * rules.Cosmic.ShieldBoxRows);
            int remaining = Math.Max(unchecked(rules.Improvements[Math.Abs((int)City(destination).Production)].CostRows * rules.Cosmic.ShieldBoxRows - City(destination).ShieldBox), 0);
            effects.Donation(new(world.Multiplayer.LocalHuman, destination, City(destination).Name, shields, remaining));
            byte home = HomeByte(unit);
            if (home != 255)
            { City(new(home)).Flags.TradeDirty = true; }
            foreach (var pending in Delete(operation, unit, call + "/donation-delete"))
            { yield return pending; }
            operation.Result = 1;
            yield break;
        }
        operation.Result = 0;
    }

    private CaravanPayment Prepare(UnitSlot unit, CitySlot destination, string call)
    {
        unchecked
        {
            int owner = (sbyte)Unit(unit).Owner.Value, victim = (sbyte)City(destination).Owner.Value;
            byte homeByte = HomeByte(unit);
            var home = homeByte != 255 ? new CitySlot(homeByte) : CityQueries.Nearest(world, Unit(unit).Position, owner).Slot ?? new(0);
            int distance = world.Map.Geometry.WeightedDistance(City(destination).Position, City(home).Position, world.Globals.Rules.FlatWorld);
            if (world.Globals.Status.LargeMap)
            { distance = 4 * distance / 5; }
            if (world.Globals.Status.SmallMap)
            { distance = 5 * distance / 4; }
            int value = (City(home).BaseTrade + City(destination).BaseTrade) * (distance + 10) / 24;
            int destinationRegion = world.Map.TileAt(City(destination).Position).Region, homeRegion = world.Map.TileAt(City(home).Position).Region;
            if (destinationRegion != homeRegion)
            { value *= 2; }
            if (owner == victim)
            { value >>= 1; }
            int cargo = (sbyte)Unit(unit).Counter;
            bool freight = Unit(unit).Type.Value == UnitTypeIds.Freight;
            if (freight)
            { value += value >> 1; }
            int connection = RoadConnections.Query(world, rules, owner, City(destination).Position, City(home).Position);
            if (BuildingQueries.HasBuilding(world, destination, BuildingIds.Airport) && BuildingQueries.HasBuilding(world, home, BuildingIds.Airport))
            { connection += destinationRegion == homeRegion ? 1 : 2; }
            if (BuildingQueries.HasBuilding(world, destination, BuildingIds.Superhighways))
            { ++connection; }
            if (BuildingQueries.HasBuilding(world, home, BuildingIds.Superhighways))
            { ++connection; }
            value += connection * value >> 1;
            int premium = cargo switch
            {
                (int)Commodity.Cloth or (int)Commodity.Coal or (int)Commodity.Wine or (int)Commodity.Silver => value / 2,
                (int)Commodity.Silk or (int)Commodity.Spice or (int)Commodity.Gems or (int)Commodity.Gold => value,
                (int)Commodity.Oil => 3 * value / 2,
                (int)Commodity.Uranium => 2 * value,
                _ => 0
            };
            for (int i = 0; i < CityLimits.TradeRoutes; ++i)
            {
                if (City(destination).Demands[i] == cargo)
                { value = (sbyte)Unit(unit).Owner.Value == owner ? 2 * value + premium : 2 * (value + premium); }
            }
            if (world.Globals.Turn < 200 && !Knows(AdvanceIds.Invention) && !Knows(AdvanceIds.Navigation))
            { value *= 2; }
            if (Knows(AdvanceIds.Railroad))
            { value -= value / 3; }
            if (Knows(AdvanceIds.Flight))
            { value -= value / 3; }
            int floor = 200 + random.PrimaryModulo(new ChoiceKey("C07.delivery-cap", call), 10);
            int cap = Arithmetic.ClampAB(2 * Research.Cost(world, rules, owner) / 3, floor, 30000);
            return new(home, owner, victim, cargo, freight, Arithmetic.ClampAB(value, 0, cap));
            bool Knows(int advance) => CivilizationKnowledge.KnowsAdvance(world.Civilizations, owner, advance);
        }
    }

    private IEnumerable<CaravanDecision?> Deliver(Operation operation, UnitSlot unit, CitySlot destination, string call)
    {
        var payment = operation.Payment = Prepare(unit, destination, call);
        int owner = payment.Actor, victim = payment.Victim, cargo = payment.Cargo;
        var sound = payment.Freight ? CaravanSound.Freight : CaravanSound.Delivery;
        if (Human(owner))
        {
            var notice = new CaravanDeliveryNotice(world.Multiplayer.LocalHuman, new(unit, Unit(unit).Token), payment,
                Name(cargo, true), City(payment.Home).Name, City(destination).Name);
            operation.Present(() => effects.Sound(sound));
            effects.Delivery(notice);
        }
        DiplomaticAttitudes.Adjust(world, victim, owner, -10);
        DiplomaticAttitudes.Adjust(world, owner, victim, -10);
        if (cargo < 0)
        {
            economy.Calculate(destination, false);
            City(destination).FoodBox = unchecked((short)(City(destination).FoodBox + unchecked((City(destination).Size + 1) * world.Calculation.FoodRows) / 2));
        }
        else
        {
            world.Civilizations[owner].Gold = unchecked(world.Civilizations[owner].Gold + payment.Payment);
            world.Civilizations[owner].ResearchProgress = unchecked((short)(world.Civilizations[owner].ResearchProgress + payment.Payment));
            if (owner == world.Multiplayer.LocalHuman)
            { operation.Present(effects.RefreshStatus); }
        }
        var type = Unit(unit).Type;
        foreach (var pending in Delete(operation, unit, call + "/delete"))
        { yield return pending; }
        TradeRoutes.Add(world, rules, payment.Home, destination.Value, cargo);
        if (cargo >= 0)
        {
            int best = 0, bestCargo = -1;
            for (int i = 0; i < CityLimits.TradeRoutes; ++i)
            {
                int score = random.PrimaryModulo(new ChoiceKey("C07.delivery-return", call, i), 8);
                int supplied = City(destination).Supplies[i], candidate = supplied < 0 ? -supplied : supplied;
                if (supplied >= 0)
                { score += 10; }
                if (candidate == cargo)
                { continue; }
                if (score > 9)
                {
                    for (int j = 0; j < CityLimits.TradeRoutes; ++j)
                    { if (City(payment.Home).Demands[j] == candidate) { score += 10; } }
                }
                if (best <= score)
                { best = score; bestCargo = candidate; }
            }
            operation.ReturnCargo = bestCargo;
            TradeRoutes.Add(world, rules, destination, payment.Home.Value, bestCargo);
            if (Human(victim) && owner != victim)
            {
                operation.Present(() => effects.Sound(sound));
                var notice = new CaravanReciprocalNotice(world.Multiplayer.LocalHuman, type, owner, victim, cargo, bestCargo,
                    world.Names[owner].Adjective, Name(cargo), City(payment.Home).Name, City(destination).Name, Name(bestCargo));
                if (world.Multiplayer.LocalHuman == victim)
                { effects.Reciprocal(notice); }
            }
        }
        operation.Result = 1;
    }
}
