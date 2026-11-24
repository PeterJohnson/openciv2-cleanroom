using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.City;

/// <summary>C01 authoritative local city creation with actual C06, C07, C08, W03 and U01 children.
/// Founding policy, worker assignment and city-output calculation belong to callers.</summary>
public sealed class CityCreation(
    GameState world, ProcessContext process, NameCatalog names, UnitCreation units, Action tooManyCities)
{
    private GameState World => world;
    /// <summary>Compatibility entry for explicitly synchronous unit creation.</summary>
    public CitySlot? Create(CivilizationSlot owner, MapPoint position)
    {
        using var operation = new Operation(this, owner, position, "city-creation", false);
        while (!operation.Completed)
        {
            if (operation.Resume() is not null)
            { throw new InvalidOperationException("Synchronous city creation cannot answer contact decisions"); }
        }
        return operation.Result;
    }

    public Operation Begin(CivilizationSlot owner, MapPoint position, string call) => new(this, owner, position, call, true);
    public sealed class Operation : IDisposable
    {
        private readonly CityCreation parent;
        private readonly IEnumerator<DiplomaticContactDecision?> steps;
        private readonly List<Exception> failures = [];
        private ResearchReply? reply;
        private bool disposed, faulted;
        public bool Completed { get; private set; }
        public CitySlot? Result { get; internal set; }
        public DiplomaticContactDecision? Pending { get; private set; }
        public IReadOnlyList<Exception> PresentationFailures => failures.AsReadOnly();
        internal Operation(CityCreation parent, CivilizationSlot owner, MapPoint position, string call, bool retained)
        {
            ArgumentException.ThrowIfNullOrEmpty(call);
            this.parent = parent;
            steps = parent.Execute(this, owner, position, call, retained).GetEnumerator();
        }
        internal ResearchReply? TakeReply() { var answer = reply; reply = null; return answer; }
        internal void Report(IEnumerable<Exception> errors) => failures.AddRange(errors);
        public DiplomaticContactDecision? Resume(ResearchReply? answer = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("Failed city creation cannot resume"); }
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
                Completed = true;
                steps.Dispose();
                return null;
            }
            catch { faulted = true; steps.Dispose(); throw; }
        }
        public void Dispose() { disposed = true; steps.Dispose(); }
    }
    private IEnumerable<DiplomaticContactDecision?> Execute(Operation operation, CivilizationSlot owner,
        MapPoint position, string call, bool retained)
    {
        if (Initialize(owner, position, out bool firstAi) is not { } slot)
        { yield break; }
        if (firstAi && FirstAiCity(slot, owner, position) is { } defender)
        {
            if (retained)
            {
                using var child = units.Begin(defender, owner, position, call + "/defender");
                while (!child.Completed)
                {
                    var pending = child.Resume(operation.TakeReply());
                    if (!child.Completed)
                    { yield return pending; }
                }
                operation.Report(child.PresentationFailures);
            }
            else
            { units.Create(defender, owner, position); }
        }
        Finish(slot, position);
        operation.Result = slot;
    }
    private CitySlot? Initialize(CivilizationSlot owner, MapPoint position, out bool firstAi)
    {
        firstAi = false;
        MapKnowledge.RequireLocal(world);
        var pool = world.Entities.Cities;
        int index = 0;
        while (index < pool.End && pool.Storage[index].Token.Occupied)
        {
            ++index;
        }

        if (index == pool.End)
        {
            if (pool.End >= EntityLimits.CreatableCities)
            {
                if (world.Globals.HumanPlayers.Contains(owner.Value))
                {
                    tooManyCities();
                }

                return null;
            }
            pool.SetEnd(pool.End + 1);
        }
        world.Globals.CityEnd = (short)pool.End;
        var slot = new CitySlot((ushort)index);
        ref var civ = ref world.Civilizations[owner.Value];
        civ.CityCount = unchecked((short)(civ.CityCount + 1));
        civ.LastCityTurn = world.Globals.Turn;
        ref var tile = ref world.Map.TileAt(position);
        tile.Features = (tile.Features | MapFeatures.CitySite) & ~MapFeatures.Fortification;
        ref var city = ref pool.At(slot);
        city.Position = position;
        city.Owner = owner;
        city.Token = process.Tokens.AssignCity();
        city.Founder = owner;
        city.TurnsCaptured = 0;
        city.Flags = new() { TradeDirty = true };
        city.Size = 1;
        city.KnownTo = default;
        city.RememberedSize = default;
        if (world.Globals.WonderCity[WonderIds.Apollo] >= 0)
        {
            for (int c = 0; c < EntityLimits.Civilizations; ++c)
            {
                city.KnownTo = city.KnownTo.With(c);
                city.RememberedSize[c] = 1;
                MapKnowledge.Explore(world, c, position);
            }
            for (int c = 1; c < EntityLimits.Civilizations; ++c)
            {
                MapKnowledge.Remember(world, c, position);
            }
        }
        city.Specialists = default;
        city.Production = UnitTypeIds.Settlers;
        int best = 0;
        var rules = process.Rules.Definitions;
        for (int type = 2; type < RuleLimits.UnitTypes; ++type)
        {
            var rule = rules.Units[type];
            if (rule.Role != RuleRole.Defend || !UnitAvailability.CanBuild(world, rules, owner.Value, slot, new((byte)type)))
            {
                continue;
            }

            int score = unchecked((sbyte)rule.Defense) * 8 / unchecked((sbyte)rule.CostRows);
            if (rule.Abilities.AntiHorseDefense)
            {
                ++score;
            }

            if (score >= best)
            {
                best = score;
                city.Production = (sbyte)type;
            }
        }
        civ.UnitsInProduction[city.Production] = unchecked((byte)(civ.UnitsInProduction[city.Production] + 1));
        city.FoodBox = city.ShieldBox = city.BaseTrade = 0;
        city.Name = string.Empty;
        CityNames.DefaultName(ref city, world.Civilizations, process.Rules, names);
        city.WorkedTiles = default;
        city.SpecialistCount = 0;
        city.Improvements = default;
        if (civ.CityCount == 1)
        {
            BuildingQueries.SetBuilding(world, slot, BuildingIds.Palace, true);
            civ.CapitalX = unchecked((short)position.X);
            if (!world.Globals.HumanPlayers.Contains(owner.Value))
            {
                firstAi = true;
            }
        }
        return slot;
    }
    private void Finish(CitySlot slot, MapPoint position)
    {
        ref var city = ref world.Entities.Cities.At(slot);
        var rules = process.Rules.Definitions;
        city.RouteCount = 0;
        city.RoutePartners[0] = new(0);
        // C01 Appendix A's partial clear: positive residual references retain their upper byte.
        // All negative encodings remain semantic absence; F06 owns their imported opaque encoding.
        if (city.RoutePartners[1] is { } retained)
        {
            short partner = unchecked((short)(retained.Value & 0xff00));
            city.RoutePartners[1] = partner < 0 ? null : new((ushort)partner);
        }
        city.RouteGoods = default;
        for (int i = 0; i <= 8; ++i)
        {
            var point = MapNeighborhoods.Offset(world.Map.Geometry, position,
                i == 8 ? new(0, 0) : MapNeighborhoods.Directions[i], world.Globals.Rules.FlatWorld);
            if (!world.Map.Geometry.InBounds(point))
            {
                continue;
            }

            ref var neighbor = ref world.Map.TileAt(point);
            if (neighbor.Ocean)
            {
                if (world.Map.Regions[neighbor.Region].Water > 1 && (neighbor.Region < 63 || city.Flags.Coastal))
                {
                    city.Flags.OceanAccess = true;
                }
                city.Flags.Coastal = true;
            }
            if (neighbor.River || neighbor.Terrain == Terrain.Mountains)
            {
                city.Flags.HydroPossible = true;
            }
        }
        CityArea.Refresh(world, slot);
        Commodities.Refresh(world, rules, slot);
        city.Science = city.Tax = city.Trade = 0;
        city.TotalFood = city.TotalShields = city.Happy = city.Unhappy = 0;
    }

    private UnitType? FirstAiCity(CitySlot slot, CivilizationSlot owner, MapPoint position)
    {
        for (int i = 0; i < 20; ++i)
        {
            var point = MapNeighborhoods.Offset(world.Map.Geometry, position, MapNeighborhoods.City[i], world.Globals.Rules.FlatWorld);
            if (!world.Map.Geometry.InBounds(point))
            {
                continue;
            }

            MapKnowledge.Explore(world, owner.Value, point);
            ref var tile = ref world.Map.TileAt(point);
            if (world.Globals.Turn >= 40 && i < 8 && !tile.RevealedTo.Intersects(world.Globals.HumanPlayers)
                && process.Rules.Definitions.Terrain[(int)tile.Terrain].Work?.Irrigation.Result == TerrainResult.ImproveInPlace)
            {
                tile.Features |= MapFeatures.Irrigation;
            }
        }
        ref var city = ref world.Entities.Cities.At(slot);
        if (world.Globals.Turn > 40 && !world.Map.TileAt(position).RevealedTo.Intersects(world.Globals.HumanPlayers))
        {
            city.Size = (sbyte)Math.Clamp((world.Globals.Turn - 20) / 20, 2, 10);
            BuildingQueries.SetBuilding(world, slot, BuildingIds.Temple, true);
            BuildingQueries.SetBuilding(world, slot, BuildingIds.Marketplace, true);
            BuildingQueries.SetBuilding(world, slot, BuildingIds.Library, true);
        }
        if (world.Globals.Turn > 40 && city.Production != UnitTypeIds.Settlers)
        {
            return new((byte)city.Production);
        }
        return null;
    }
}
