using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.City;

public enum CityProductionOutcome { Unit, NotReady, AlreadyBuilt, LostWonder, BadSpace, HumanWonderPriority, Completed }
public sealed record CityProductionResult(CityProductionOutcome Outcome, int? Improvement = null, UnitProductionResult? Unit = null);
public sealed record ImprovementCompletionNotice(CityTarget City, int Owner, int Improvement, int? Wonder,
    string CityName, string Adjective, string ItemName, int WordingLabel, bool ForcedCityNotice);

/// <summary>C06 semantic production presentation. Window state is live at each documented draw
/// boundary; player decisions for research/spaceship assembly belong to their owning services.</summary>
public interface ICityProductionEffects
{
    bool WindowShown { get; }
    void DrawProduction(CitySlot city);
    void DrawImprovements(CitySlot city);
    void InvalidBuild(CitySlot city, int improvement, string name);
    void BadSpace(CitySlot city, int improvement, string name);
    void Manhattan(CitySlot city, int recipient);
    void CompletionSound(CitySlot city, int improvement);
    void Built(ImprovementCompletionNotice notice);
    void WonderMovie(int wonder);
    void CapitalMoved(CitySlot city, int owner, string name, string adjective);
    void RedrawMaps();
    void Astronauts();
    void AlmostWonder(AlmostWonderNotice notice);
}

/// <summary>C06 local full production dispatch. The unit child owns its accumulation and common
/// tail; the nonunit operation retains its item/cost/net across actual child effects and Darwin replies.</summary>
public sealed class CityProduction
{
    private readonly GameState world;
    private readonly RuleTables rules;
    private readonly CityUnitProduction units;
    private readonly ProductionOrders orders;
    private readonly ResearchFlow research;
    private readonly WonderEffects wonders;
    private readonly SpaceshipPerformance performance;
    private readonly SpaceshipConstruction spaceship;
    private readonly SpaceshipValues spaceValues;
    private readonly CityEconomy economy;
    private readonly ICityProductionEffects effects;
    private readonly AiProductionPurchase purchase;

    public CityProduction(GameState world, RuleTables rules, CityUnitProduction units, ProductionOrders orders,
        ResearchFlow research, WonderEffects wonders, SpaceshipPerformance performance, SpaceshipConstruction spaceship,
        SpaceshipValues spaceValues, CityEconomy economy, ICityProductionEffects effects)
    {
        this.world = world;
        this.rules = rules;
        this.units = units;
        this.orders = orders;
        this.research = research;
        this.wonders = wonders;
        this.performance = performance;
        this.spaceship = spaceship;
        this.spaceValues = spaceValues;
        this.economy = economy;
        this.effects = effects;
        purchase = new(world, rules, slot => orders.Set(slot, 99), effects.AlmostWonder);
    }
    private bool Human(int c) => world.Globals.HumanPlayers.Contains(c & 31);
    public Operation Begin(CitySlot city, string call) => new(this, city, call);
    public Operation Begin(CitySlot city, string call, CivilizationElimination lifecycle, EspionageIncidents incidents)
    {
        ArgumentNullException.ThrowIfNull(lifecycle);
        ArgumentNullException.ThrowIfNull(incidents);
        return new(this, city, call, lifecycle, incidents);
    }

    public sealed class Operation(CityProduction production, CitySlot slot, string call,
        CivilizationElimination? lifecycle = null, EspionageIncidents? incidents = null) : IDisposable
    {
        private enum Phase { Begin, Unit, FirstResearch, SecondResearch, AfterEffects, Finish, Done }
        private Phase phase;
        private bool faulted, disposed;
        private int owner, net, cost;
        private int? improvement, wonder;
        private ResearchOperation? child;
        private CityUnitProduction.Operation? unit;
        public UnitProductionDecision? UnitDecision { get; private set; }
        private readonly List<Exception> failures = [];
        public IReadOnlyList<Exception> PresentationFailures => failures.AsReadOnly();
        private CityProductionOutcome outcome = CityProductionOutcome.NotReady;
        private GameState World => production.world;
        private RuleTables Rules => production.rules;
        private ref State.City City => ref World.Entities.Cities.At(slot);
        private ICityProductionEffects Effects => production.effects;
        public bool Finished { get; private set; }
        public CityProductionResult? Result { get; private set; }

        public ResearchDecision? Resume(ResearchReply? reply = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            {
                throw new InvalidOperationException("Failed city production cannot resume.");
            }

            try
            { return ResumeCore(reply); }
            catch { faulted = true; child?.Dispose(); unit?.Dispose(); throw; }
        }
        public void Dispose() { disposed = true; child?.Dispose(); unit?.Dispose(); }

        private ResearchDecision? ResumeCore(ResearchReply? reply)
        {
            MapKnowledge.RequireLocal(World);
            if (Finished)
            {
                return null;
            }

            ArgumentException.ThrowIfNullOrEmpty(call);
            while (true)
            {
                switch (phase)
                {
                    case Phase.Begin:
                        World.Production.CompletedImprovement = null;
                        if (City.Production >= 0)
                        {
                            if (lifecycle is null)
                            {
                                Result = new(CityProductionOutcome.Unit, Unit: production.units.Run(slot, call));
                                Finished = true;
                                return null;
                            }
                            unit = production.units.Begin(slot, call, lifecycle, incidents
                                ?? throw new InvalidOperationException("Retained unit production requires incident services"));
                            phase = Phase.Unit;
                            reply = null;
                            break;
                        }
                        StartNonunit();
                        break;
                    case Phase.Unit:
                        UnitDecision = unit!.ResumeDecision(reply);
                        reply = null;
                        if (unit.Result is null)
                        {
                            return null;
                        }

                        failures.AddRange(unit.PresentationFailures);
                        Result = new(CityProductionOutcome.Unit, Unit: unit.Result);
                        unit.Dispose();
                        unit = null;
                        Finished = true;
                        return null;
                    case Phase.FirstResearch:
                    case Phase.SecondResearch:
                        if (child!.Resume(reply) is { } request)
                        {
                            return request;
                        }

                        reply = null;
                        if (!child.Finished)
                        { return null; }
                        failures.AddRange(child.PresentationFailures);
                        child.Dispose();
                        child = null;
                        if (phase == Phase.FirstResearch)
                        {
                            child = production.research.Complete(owner, 0, call + "/darwin/1");
                            phase = Phase.SecondResearch;
                        }
                        else
                        {
                            phase = Phase.AfterEffects;
                        }

                        break;
                    case Phase.AfterEffects:
                        AfterEffects();
                        phase = Phase.Finish;
                        break;
                    case Phase.Finish:
                        Finish();
                        phase = Phase.Done;
                        break;
                    default:
                        Result = new(outcome, improvement);
                        Finished = true;
                        return null;
                }
            }
        }

        private void StartNonunit()
        {
            owner = unchecked((sbyte)City.Owner.Value);
            net = City.Flags.Disorder ? 0 : Math.Max(0, unchecked(World.Calculation.Shields - World.Calculation.ShieldSupport));
            if (City.Production != -BuildingIds.Capitalization)
            {
                City.ShieldBox = unchecked((short)(City.ShieldBox + (short)net));
            }

            if (owner == 0 && World.Civilizations[0].ContinentMilitary[World.Map.TileAt(City.Position).Region] > 16
                && World.Map.UnitOwner(City.Position) is not null)
            {
                City.ShieldBox = 0;
            }

            improvement = -City.Production;
            int item = improvement.Value;
            wonder = item > BuildingIds.Capitalization ? item - 39 : null;
            cost = unchecked(Rules.Improvements[item].CostRows * World.Calculation.ShieldRows);
            phase = Phase.Finish;
            if (!World.Multiplayer.Saved.AutoplayActive
                && (wonder is { } w ? World.Globals.WonderCity[w] != BuildingQueries.Unbuilt : BuildingQueries.HasBuilding(World, slot, item))
                && !World.Globals.MessageOptions.InvalidBuild)
            {
                Effects.InvalidBuild(slot, item, Rules.Improvements[item].Identity.Name);
            }

            if (item is >= BuildingIds.Structural and <= BuildingIds.Module && production.Human(owner)
                && (World.Civilizations[owner].SpaceshipFlags.Launched || SpaceshipQueries.PartMaxed(World, production.spaceValues, owner, item)))
            {
                BadSpace(item);
                outcome = CityProductionOutcome.BadSpace;
                return;
            }
            if (cost > City.ShieldBox || item == BuildingIds.Capitalization)
            {
                return;
            }

            City.Flags.WonderBuilt = false;
            if (wonder is not { } identity)
            {
                if (BuildingQueries.HasBuilding(World, slot, item))
                { improvement = null; outcome = CityProductionOutcome.AlreadyBuilt; return; }
            }
            else
            {
                if (World.Globals.WonderCity[identity] == BuildingQueries.Unbuilt && !production.Human(owner))
                {
                    for (int i = 0; i < World.Entities.Cities.End; ++i)
                    {
                        ref var other = ref World.Entities.Cities.Storage[i];
                        if (other.Token.Occupied && production.Human(unchecked((sbyte)other.Owner.Value)) && other.Production == City.Production
                            && other.ShieldBox >= unchecked(Rules.Improvements[item].CostRows * Rules.Cosmic.ShieldBoxRows))
                        { outcome = CityProductionOutcome.HumanWonderPriority; phase = Phase.Done; return; }
                    }
                }
                if (World.Globals.WonderCity[identity] != BuildingQueries.Unbuilt)
                {
                    improvement = wonder = null;
                    outcome = CityProductionOutcome.LostWonder;
                    if (!production.Human(owner))
                    {
                        production.orders.Set(slot, 99);
                    }

                    return;
                }
                // Local production acquisition differs from the standalone host acquisition:
                // storage/cost and the later announcement still run in this parent.
                World.Globals.WonderCity[identity] = unchecked((short)slot.Value);
                City.Flags.WonderBuilt = true;
            }
            if (item is >= BuildingIds.Structural and <= BuildingIds.Module && !production.Human(owner))
            {
                if (World.Civilizations[owner].SpaceshipFlags.Launched)
                { production.orders.Set(slot, 99); return; }
                int group = production.performance.ChooseGroup(owner, item - BuildingIds.Structural);
                if (group < 0)
                { production.orders.Set(slot, 99); return; }
                item = group + BuildingIds.Structural;
                improvement = item;
                City.Production = unchecked((sbyte)-item);
                cost = unchecked(Rules.Improvements[item].CostRows * World.Calculation.ShieldRows);
                if (cost > City.ShieldBox)
                {
                    return;
                }
            }
            Complete(item);
        }

        private void Complete(int item)
        {
            if (Effects.WindowShown)
            {
                Effects.DrawProduction(slot);
            }

            if (wonder == WonderIds.Manhattan)
            {
                for (int c = 1; c < EntityLimits.Civilizations; ++c)
                {
                    World.Civilizations[c].Reputation = (byte)Arithmetic.ClampAB((World.Civilizations[c].Reputation + 1) / 2, 0, 6);
                }

                if (!World.Multiplayer.Saved.AutoplayActive)
                {
                    for (int c = 1; c < EntityLimits.Civilizations; ++c)
                    {
                        if (World.Globals.CivsAlive.Contains(c) && production.Human(c) && (City.KnownTo.Contains(c) || City.Owner.Value == c)
                            && c == World.Multiplayer.LocalHuman)
                        {
                            Effects.Manhattan(slot, c);
                        }
                    }
                }
            }
            BuildingQueries.SetBuilding(World, slot, item, true);
            City.ShieldBox = unchecked((short)(City.ShieldBox - (short)cost));
            if (!production.Human(owner) && item < BuildingIds.Structural)
            {
                if (item == BuildingIds.Palace)
                {
                    --City.Production;
                }

                production.orders.Set(slot, 99);
            }
            else
            {
                Announce(item);
            }

            if (item is >= BuildingIds.Structural and <= BuildingIds.Module)
            {
                int part = production.spaceship.AddPart(owner, item - BuildingIds.Structural);
                if (production.Human(owner) && !World.Civilizations[owner].SpaceshipFlags.Launched && part < 0)
                {
                    BadSpace(item);
                }
            }
            if (item == BuildingIds.Palace)
            {
                for (int i = 0; i < World.Entities.Cities.End; ++i)
                {
                    if (World.Entities.Cities.Storage[i].Token.Occupied && unchecked((sbyte)World.Entities.Cities.Storage[i].Owner.Value) == owner)
                    {
                        BuildingQueries.SetBuilding(World, new((ushort)i), BuildingIds.Palace, false);
                    }
                }

                BuildingQueries.SetBuilding(World, slot, BuildingIds.Palace, true);
                World.Civilizations[owner].CapitalX = unchecked((short)City.Position.X);
                if (!World.Multiplayer.Saved.AutoplayActive)
                {
                    Effects.CapitalMoved(slot, owner, City.Name, World.Names[owner].Adjective);
                }
            }
            outcome = CityProductionOutcome.Completed;
            if (wonder == WonderIds.Darwin)
            {
                child = production.research.Complete(owner, 0, call + "/darwin/0");
                phase = Phase.FirstResearch;
                return;
            }
            if (wonder == WonderIds.Leonardo)
            {
                production.wonders.UpgradeForLeonardo(owner);
            }
            else if (wonder == WonderIds.EiffelTower)
            {
                production.wonders.ApplyEiffel(owner);
            }
            else if (wonder == WonderIds.Apollo)
            {
                ApolloRevelation.Run(World, Effects.RedrawMaps, Effects.Astronauts);
            }

            phase = Phase.AfterEffects;
        }

        private void Announce(int item)
        {
            bool announce = (World.Multiplayer.LocalHuman == owner || item > BuildingIds.Last) && !World.Multiplayer.Saved.AutoplayActive;
            if (!production.Human(owner))
            {
                for (int c = 0; c < EntityLimits.Civilizations; ++c)
                {
                    City.KnownTo = City.KnownTo.With(c);
                }
            }

            int wording = wonder switch
            {
                WonderIds.CureForCancer => 184,
                WonderIds.WomensSuffrage => 185,
                WonderIds.MarcoPolo or WonderIds.SunTzu or WonderIds.KingRichard or WonderIds.Leonardo or WonderIds.Darwin or WonderIds.Magellan => 186,
                _ => 187
            };
            string name = Rules.Improvements[item].Identity.Name, adjective = World.Names[owner].Adjective;
            if (announce)
            {
                Effects.CompletionSound(slot, item);
            }

            bool local = owner == World.Multiplayer.LocalHuman && !World.Multiplayer.Saved.AutoplayActive;
            if (local ? !World.Globals.MessageOptions.ImprovementBuilt : announce)
            {
                Effects.Built(new(new(slot, City.Token), owner, item, wonder, City.Name, adjective, name, wording, local));
            }

            if (announce && wonder is { } movie && owner == World.Multiplayer.LocalHuman && !World.Multiplayer.Saved.AutoplayActive
                && World.Globals.GameOptions.WonderMovies)
            {
                Effects.WonderMovie(movie);
            }

            if (production.Human(owner))
            {
                World.Production.CompletedImprovement = item;
            }

            City.ShieldBox = 0;
            if (City.Flags.Autobuild || !production.Human(owner))
            {
                production.orders.Set(slot, 99);
            }
        }

        private void AfterEffects()
        {
            if (Effects.WindowShown)
            { Effects.DrawProduction(slot); Effects.DrawImprovements(slot); }
            if (wonder is not null)
            {
                for (int i = 0; i < World.Entities.Cities.End; ++i)
                {
                    ref var other = ref World.Entities.Cities.Storage[i];
                    if (i != slot.Value && other.Token.Occupied && !production.Human(unchecked((sbyte)other.Owner.Value))
                        && other.Production == -improvement)
                    {
                        production.orders.Set(new((ushort)i), 99);
                    }
                }
                production.economy.Calculate(slot, false);
            }
        }

        private void Finish()
        {
            if (production.Human(owner) && City.Flags.Autobuild && (City.ShieldBox == 0 || City.Production < 0 && improvement is null))
            {
                production.orders.Set(slot, 99);
                if (City.Production == 99)
                {
                    City.Flags.Autobuild = City.Flags.UnitsOnlyAutomation = City.Flags.ImprovementsOnlyAutomation = false;
                }
            }
            if (!production.Human(owner))
            {
                production.purchase.Run(slot, owner, net);
            }

            City.Flags.DefenderLost = false;
        }
        private void BadSpace(int item) => Effects.BadSpace(slot, item, Rules.Improvements[item].Identity.Name);
    }
}
