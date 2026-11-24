using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

/// <summary>S02 §9 unit actions. Creation retains actual U01/W03 contact before its caller-owned
/// veteran/home assignments. MoveUnit assigns classic slot-selected orders; it does not move units.</summary>
public sealed class ScenarioUnitActions(GameState world, RuleTables rules, UnitCreation creation)
{
    private GameState World => world;
    private RuleTables Rules => rules;
    private UnitCreation Creation => creation;

    // F06's ordered event string fields: creation home is field 30.
    public const int HomeNameField = 30;
    public CreateOperation BeginCreate(Event entry, string call) => new(this, entry, call);
    public sealed class CreateOperation : IDisposable
    {
        private readonly ScenarioUnitActions parent;
        private readonly Event entry;
        private readonly string call;
        private UnitCreation.Operation? child;
        private int owner;
        private bool started, faulted, disposed;
        public int? Result { get; private set; }
        public bool Completed => Result is not null;
        public DiplomaticContactDecision? Pending => child?.Pending;
        public IReadOnlyList<Exception> PresentationFailures => child?.PresentationFailures ?? [];
        internal CreateOperation(ScenarioUnitActions parent, Event entry, string call)
        {
            ArgumentNullException.ThrowIfNull(entry);
            ArgumentException.ThrowIfNullOrEmpty(call);
            this.parent = parent;
            this.entry = entry;
            this.call = call;
        }
        public DiplomaticContactDecision? Resume(ResearchReply? reply = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("Failed scenario creation cannot resume"); }
            if (Completed)
            { return null; }
            try
            {
                MapKnowledge.RequireLocal(parent.World);
                if (!started)
                {
                    started = true;
                    owner = ScenarioActions.ResolveCivilization(entry, entry.Values.CreateOwnerCiv);
                    if (!parent.World.Globals.CivsAlive.Contains(owner & 31))
                    { Result = 0; return null; }
                    MapPoint? selected = null;
                    for (int i = 0; i < entry.Values.CreateLocationCount; ++i)
                    {
                        if (i >= 10)
                        { throw new InvalidOperationException("Scenario creation location exceeds supplied backing"); }
                        var point = entry.Values.CreateLocations[i];
                        if (!parent.World.Map.Geometry.InBounds(point))
                        { continue; }
                        if (parent.Rules.Units[entry.Values.CreateUnitType].Domain == RuleDomain.Sea)
                        {
                            if (!parent.World.Map.TileAt(point).Ocean)
                            {
                                if (CityQueries.At(parent.World, point) is null
                                    || !parent.World.Entities.Cities.At(CityQueries.At(parent.World, point)!.Value).Flags.Coastal
                                    || parent.World.Map.CityOwner(point) != owner)
                                { continue; }
                            }
                        }
                        else
                        {
                            if (parent.World.Map.TileAt(point).Ocean && parent.Rules.Units[entry.Values.CreateUnitType].Domain != RuleDomain.Air
                                || CityQueries.At(parent.World, point) is not null && parent.World.Map.CityOwner(point) != owner
                                || parent.World.Map.UnitOwner(point) is not null && parent.World.Map.UnitOwner(point) != owner)
                            { continue; }
                        }
                        selected = point;
                        break;
                    }
                    if (selected is not { } location)
                    { Result = 0; return null; }
                    // Do not let a wide invalid selector silently alias a byte-backed owner/type.
                    if (owner < 0 || owner >= EntityLimits.Civilizations || entry.Values.CreateUnitType < 0 || entry.Values.CreateUnitType >= RuleLimits.UnitTypes)
                    { throw new InvalidOperationException("Scenario creation requires backed owner and unit definitions"); }
                    child = parent.Creation.Begin(new((byte)entry.Values.CreateUnitType), new((byte)owner), location, call + "/unit");
                }
                var decision = child!.Resume(reply);
                if (!child.Completed)
                { return decision; }
                if (child.Result is not { } created)
                { Result = 0; return null; }
                if (entry.Values.CreateVeteran)
                { parent.World.Entities.Units.At(created).Flags.Veteran = true; }
                parent.World.Entities.Units.At(created).Home = null;
                if (entry.Strings[HomeNameField].Present)
                {
                    for (int i = 0; i < parent.World.Entities.Cities.End; ++i)
                    {
                        ref readonly var city = ref parent.World.Entities.Cities.Storage[i];
                        if (!city.Token.Occupied || !string.Equals(city.Name, entry.Strings[HomeNameField].Value, StringComparison.OrdinalIgnoreCase))
                        { continue; }
                        if (city.Owner.Value == owner)
                        {
                            byte home = unchecked((byte)i);
                            parent.World.Entities.Units.At(created).Home = home == byte.MaxValue ? null : new(home);
                        }
                        break;
                    }
                }
                Result = 1;
                return null;
            }
            catch { faulted = true; child?.Dispose(); throw; }
        }
        public void Dispose() { disposed = true; child?.Dispose(); }
    }

    public int Move(Event entry)
    {
        MapKnowledge.RequireLocal(world);
        int owner = ScenarioActions.ResolveCivilization(entry, entry.Values.MoveOwnerCiv);
        if (!world.Globals.CivsAlive.Contains(owner & 31) || world.Globals.HumanPlayers.Contains(owner & 31))
        { return 0; }
        var rectangle = entry.Values.MoveRectangle;
        int count = world.Entities.Units.End, moved = 0;
        for (int i = count - 1; i >= 0; --i)
        {
            ref var unit = ref world.Entities.Units.Storage[i];
            if (!unit.Token.Occupied || entry.Values.MoveUnitType != i && entry.Values.MoveUnitType != -2
                || unit.Owner.Value != owner || unchecked((sbyte)rules.Units[unit.Type.Value].Attack) > 98
                || unit.Orders == UnitOrder.AiIdle || (sbyte)unit.Orders > 3 || unit.Orders == UnitOrder.Sleeping)
            { continue; }
            var point = unit.Position;
            if (!world.Map.Geometry.InBounds(point) || point.X < rectangle[0].X || point.X > rectangle[1].X
                || point.X > rectangle[2].X || point.X < rectangle[3].X || point.Y < rectangle[0].Y || point.Y < rectangle[1].Y
                || point.Y > rectangle[2].Y || point.Y > rectangle[3].Y)
            { continue; }
            unit.Orders = UnitOrder.GoTo;
            unit.AiTask = 55;
            unit.Destination = new(unchecked((short)entry.Values.MoveTo.X), unchecked((short)entry.Values.MoveTo.Y));
            ++moved;
            if (moved >= entry.Values.MoveCount && entry.Values.MoveCount != -2)
            { break; }
        }
        return moved;
    }
}
