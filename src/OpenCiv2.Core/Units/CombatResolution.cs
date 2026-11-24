using OpenCiv2.Contracts;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Units;

public interface ICombatResolutionEffects
{
    void Sound(CombatSound sound, int argument);
    void Synchronize();
    void Redraw(MapPoint origin, MapPoint target);
    void Explosion(MapPoint point, int kind);
    void RedrawNeighbors(MapPoint point);
}
public sealed record CombatResolutionServices(CombatPromotion Promotion, CombatCasualties Casualties,
    UnitCargo Cargo, CityRemoval Removal, CivilizationElimination Lifecycle, EspionageIncidents Incidents,
    ProductionOrders Production, ExposedCityTransports Transports);
public sealed record ResolvedCombat(int Winner, int Loser, bool Ransom, CitySlot? DefendingCity);

/// <summary>U06 post-round result presentation, promotion, actual loser removal and defender-city
/// consequences. The combat parent owns pre-round diplomacy and final effects/surviving-attacker handling.</summary>
public sealed class CombatResolution(GameState world, RuleTables rules, CombatWorkspace state,
    CombatResolutionServices services, ICombatResolutionEffects effects)
{
    private GameState World => world;
    private ref Unit Unit(UnitSlot slot) => ref world.Entities.Units.At(slot);
    private ref State.City City(CitySlot slot) => ref world.Entities.Cities.At(slot);
    private bool Human(int owner) => world.Globals.HumanPlayers.Contains(owner);
    public Operation Begin(PreparedCombat combat, bool attackerWon, string call) => new(this, combat, attackerWon, call);

    public sealed class Operation : IDisposable
    {
        private readonly CombatResolution parent;
        private readonly IEnumerator<LifecycleDecision?> steps;
        private ResearchReply? reply;
        private readonly List<Exception> failures = [];
        private bool disposed, faulted;
        public LifecycleDecision? Pending { get; private set; }
        public ResolvedCombat? Result { get; internal set; }
        public bool Completed => Result is not null;
        public IReadOnlyList<Exception> PresentationFailures => failures.AsReadOnly();
        internal void Report(IEnumerable<Exception> errors) => failures.AddRange(errors);
        internal Operation(CombatResolution parent, PreparedCombat combat, bool won, string call)
        {
            ArgumentException.ThrowIfNullOrEmpty(call);
            this.parent = parent;
            steps = parent.Execute(this, combat, won, call).GetEnumerator();
        }
        internal ResearchReply? TakeReply() { var answer = reply; reply = null; return answer; }
        public LifecycleDecision? Resume(int? response = null) => ResumeDecision(response is { } value ? new(value) : null);
        public LifecycleDecision? ResumeDecision(ResearchReply? response = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("A failed combat resolution cannot resume"); }
            if (Completed)
            { return null; }
            try
            {
                MapKnowledge.RequireLocal(parent.World);
                if (Pending is not null && response is null)
                { return Pending; }
                reply = Pending is null ? null : response;
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
    private void Present(PreparedCombat combat, bool won)
    {
        if (!won)
        { state.ActiveUnitLost = true; }
        if (state.AnyViewer)
        {
            state.LastSound = null;
            UnitSlot? crash = won && rules.Units[Unit(combat.Defender).Type.Value].Domain == RuleDomain.Air && state.City is null
                ? combat.Defender : !won && rules.Units[Unit(combat.Attacker).Type.Value].Domain == RuleDomain.Air
                && !rules.Units[Unit(combat.Attacker).Type.Value].Abilities.DestroyedAfterAttack ? combat.Attacker : null;
            CombatSound? sound = crash is { } unit ? Unit(unit).Type.Value < 30 ? CombatSound.DiveCrash : CombatSound.JetCrash
                : state.ExplosionKind != 0 ? CombatSound.LargeExplosion : null;
            if (sound is { } selected)
            {
                state.LastSound = selected;
                state.SoundArgument = 1;
                effects.Sound(selected, 1);
            }
        }
        effects.Synchronize();
        if (!state.AnyViewer)
        { return; }
        for (int viewer = 1; viewer < EntityLimits.Civilizations; ++viewer)
        {
            if (!state.Seen[viewer] || viewer != world.Multiplayer.LocalHuman)
            { continue; }
            effects.Redraw(combat.Origin, combat.Target);
            if (!state.ExplosionShown)
            {
                state.ExplosionKind = 0;
                effects.Explosion(won ? combat.Target : combat.Origin, 0);
            }
        }
    }
    private IEnumerable<LifecycleDecision?> FinishChild(Operation operation, CivilizationElimination.Operation child)
    {
        while (child.Result is null)
        {
            if (child.ResumeDecision(operation.TakeReply()) is { } pending)
            { yield return pending; }
            else if (child.Result is null)
            { yield return null; }
        }
        operation.Report(child.PresentationFailures);
    }
    private IEnumerable<LifecycleDecision?> Execute(Operation operation, PreparedCombat combat, bool won, string call)
    {
        Present(combat, won);
        state.AnimationDefender = null;
        state.KillCount = 0;
        services.Promotion.Apply(combat, won, call + "/promotion");
        bool ransom = won && combat.DefenderOwner == 0 && unchecked((sbyte)rules.Units[Unit(combat.Defender).Type.Value].Role) > 4;
        CombatCasualties.Operation casualties;
        if (!won)
        {
            services.Cargo.Lift(combat.Attacker.Value, true);
            casualties = services.Casualties.BeginStack(combat.Attacker, combat.Defender, combat.Target, call + "/loss");
        }
        else
        {
            var tile = world.Map.TileAt(combat.Target);
            bool fortress = tile.Features.HasFlag(MapFeatures.Fortification) && !tile.Features.HasFlag(MapFeatures.CitySite);
            bool wholeStack = state.City is null && !fortress && world.Map.AirbaseOwner(combat.Target) is null;
            casualties = wholeStack ? services.Casualties.BeginStack(combat.Defender, combat.Attacker, combat.Target, call + "/loss")
                : services.Casualties.Begin(combat.Defender, combat.Attacker, combat.Target, call + "/loss");
        }
        using (casualties)
        {
            while (!casualties.Completed)
            {
                if (casualties.ResumeDecision(operation.TakeReply()) is { } pending)
                { yield return pending; }
                else if (!casualties.Completed)
                { yield return null; }
            }
            operation.Report(casualties.PresentationFailures);
        }
        CitySlot? city = won ? state.City : null;
        if (city is { } targetCity)
        {
            City(targetCity).Flags.DefenderLost = true;
            if (!world.Map.TileAt(combat.Origin).Ocean && !BuildingQueries.HasBuilding(world, targetCity, BuildingIds.CityWalls)
                && !BuildingQueries.HasWonder(world, rules, combat.DefenderOwner, WonderIds.GreatWall)
                && !(world.Globals.Difficulty == Difficulty.Chieftain && Human(combat.DefenderOwner)))
            {
                City(targetCity).Size = unchecked((sbyte)(City(targetCity).Size - 1));
                if (City(targetCity).Size == 0)
                {
                    using (var removal = services.Lifecycle.BeginRemoveCity(services.Removal, targetCity.Value, call + "/city", services.Incidents))
                    { foreach (var pending in FinishChild(operation, removal)) { yield return pending; } }
                    for (int viewer = 1; viewer < EntityLimits.Civilizations; ++viewer)
                    { MapKnowledge.Remember(world, viewer, combat.Target); }
                    effects.RedrawNeighbors(combat.Target);
                    using var check = services.Lifecycle.Begin(combat.DefenderOwner, combat.AttackerOwner, call + "/check", services.Incidents);
                    foreach (var pending in FinishChild(operation, check))
                    { yield return pending; }
                    if (check.Result != 0)
                    {
                        state.AggregateTreatyNotice = false;
                        for (int viewer = 1; viewer < EntityLimits.Civilizations; ++viewer)
                        { state.TreatyNotices[viewer] = false; }
                    }
                }
                else
                {
                    MapKnowledge.RevealCity(world, combat.AttackerOwner, targetCity);
                    if (world.Map.UnitOwner(combat.Target) is null && !Human(combat.DefenderOwner))
                    { services.Production.ResetCivilization(combat.DefenderOwner, world.Map.TileAt(combat.Target).Region); }
                }
            }
            if (world.Map.UnitOwner(combat.Target) is null && Human(combat.DefenderOwner))
            { services.Transports.Retask(targetCity); }
        }
        operation.Result = new(won ? combat.AttackerOwner : combat.DefenderOwner, won ? combat.DefenderOwner : combat.AttackerOwner, ransom, city);
    }
}
