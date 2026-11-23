using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.Ai;

/// <summary>A03 city-site and fortress phases. Actual C01 founding can suspend for
/// lifecycle decisions; A04 owns direction application, common finish and later work.</summary>
public sealed partial class SettlementTactics(GameState world, RuleTables rules, SharedGoals goals, CityFounding founding)
{
    private GameState World => world;
    private CityFounding Founding => founding;
    private ref Unit Actor(UnitTacticsContext c) => ref world.Entities.Units.At(c.Unit);
    private ref Civilization Civ(UnitTacticsContext c) => ref world.Civilizations[c.Owner];
    private MapPoint Offset(MapPoint p, MapPoint delta) => MapNeighborhoods.Offset(world.Map.Geometry, p, delta, world.Globals.Rules.FlatWorld);

    public SiteOperation Site(UnitTacticsContext context, StartingPositions starts, string call) => new(this, context, starts, call);

    // Null selects the actual founding child; all other results belong to the shared parent.
    private SpecialUnitChoice? ChooseSite(UnitTacticsContext c, StartingPositions starts)
    {
        c.SettlerKind = c.Role == (int)RuleRole.Settle;
        c.SettlerType = rules.Units[Actor(c).Type.Value].Role == RuleRole.Settle;
        int best = world.Map.ClaimantOrFertility(c.Position);
        if (c.NearestDistance < 4 && c.Region == c.NearestRegion && best > 7)
        {
            best = 8;
        }

        if (c.Terrain == Terrain.Ocean)
        {
            best = 0;
        }

        if (c.SettlerKind)
        {
            goals.ClearStandingNear(c.Owner, (int)RuleRole.Settle, c.Position, 0);
            if (Civ(c).CityCount == 0 && world.Globals.Turn == 1
                && c.Position == starts.Positions[Civ(c).Tribe] && best > 7)
            {
                return null;
            }
        }
        c.CitySelector = c.NearestCity;
        if (best != 0 && (unchecked((sbyte)c.Role) < 2 || c.SettlerType) && !c.ForceAttack)
        {
            int selected = -1;
            for (int i = 0; i < (c.SettlerKind ? 20 : 8); ++i)
            {
                var p = Offset(c.Position, MapNeighborhoods.Radius[i]);
                if (!world.Map.Geometry.InBounds(p) || world.Map.TileAt(p).Ocean || world.Map.TileAt(p).Region != c.Region)
                {
                    continue;
                }

                int value = world.Map.ClaimantOrFertility(p);
                if (value > best)
                { best = value; selected = i; }
                if (unchecked((sbyte)c.Role) < 2 && i < 8 && world.Map.Hut(p))
                { Actor(c).AiTask = AiTaskIds.Hut; return new(SpecialUnitRoute.Direction, i); }
            }
            if (best > 7 && !c.EnemyZone)
            {
                bool proceed = unchecked(15 - c.NearestDistance) <= best
                    || c.SettlerKind && selected < 0 && Civ(c).ContinentCities[c.Region] == 0;
                if (!proceed && c.SettlerKind && selected < 0)
                {
                    Actor(c).AuxiliaryTimer = unchecked((byte)(Actor(c).AuxiliaryTimer + 1));
                    proceed = unchecked((sbyte)Actor(c).AuxiliaryTimer) > 4;
                }
                if (proceed)
                {
                    if (c.SettlerKind)
                    {
                        if (selected < 0)
                        {
                            return null;
                        }

                        Actor(c).AiTask = AiTaskIds.FoundSite;
                        if (selected < 8)
                        {
                            return new(SpecialUnitRoute.Direction, selected);
                        }

                        UnitOrders.SetGoto(ref Actor(c), AiTaskIds.FoundSite, Offset(c.Position, MapNeighborhoods.Radius[selected]));
                    }
                    else if (selected is >= 0 and < 8)
                    { goals.AddStanding(c.Owner, Offset(c.Position, MapNeighborhoods.Radius[selected]), (int)RuleRole.Settle, 2); }
                }
            }
        }
        if (c.SettlerKind && world.Globals.Difficulty != Difficulty.Chieftain && c.NearestCity is { } nearest
            && world.Globals.HumanPlayers.Contains(world.Entities.Cities.At(nearest).Owner.Value) && !c.EnemyZone && c.NearestDistance > 1
            && Civ(c).NumAdvances < world.Civilizations[world.Entities.Cities.At(nearest).Owner.Value].NumAdvances)
        {
            int fertility = world.Map.TileAt(c.Position).Fertility;
            if (fertility > 8 && unchecked(14 - c.NearestDistance) <= fertility)
            {
                return null;
            }
        }
        return new(SpecialUnitRoute.Continue);
    }

    public sealed class SiteOperation : IDisposable
    {
        private readonly SettlementTactics parent;
        private readonly UnitTacticsContext context;
        private readonly StartingPositions starts;
        private readonly string call;
        private bool started, faulted, disposed;
        private CityFounding.Operation? child;
        public SpecialUnitChoice? Result { get; private set; }
        public FoundingOutcome? FoundingResult { get; private set; }
        internal SiteOperation(SettlementTactics parent, UnitTacticsContext context, StartingPositions starts, string call)
        { this.parent = parent; this.context = context; this.starts = starts; this.call = call; }
        public FoundingDecision? Resume(FoundingReply? reply = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            {
                throw new InvalidOperationException("Failed settlement choice cannot resume");
            }

            if (Result is not null)
            {
                return null;
            }

            try
            {
                MapKnowledge.RequireLocal(parent.World);
                if (!started)
                {
                    ArgumentException.ThrowIfNullOrEmpty(call);
                    started = true;
                    Result = parent.ChooseSite(context, starts);
                    if (Result is not null)
                    {
                        return null;
                    }

                    child = parent.Founding.Begin(context.Unit, call + "/found");
                    reply = null;
                }
                var pending = child!.Resume(reply);
                if (child.Completed)
                { FoundingResult = child.Result; Result = new(SpecialUnitRoute.ReturnOne); child.Dispose(); child = null; }
                return pending;
            }
            catch { faulted = true; child?.Dispose(); throw; }
        }
        public void Dispose() { disposed = true; child?.Dispose(); }
    }
}
