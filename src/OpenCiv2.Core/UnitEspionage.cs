using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

public static class EspionageCosts
{
    /// <summary>V06 shared capital distance: capped W01 step distance to every actual Palace.</summary>
    public static int DistanceToCapital(GameState world, int owner, MapPoint position)
    {
        int result = 16;
        for (int i = 0; i < world.Entities.Cities.End; ++i)
        {
            ref var city = ref world.Entities.Cities.Storage[i];
            if (city.Token.Occupied && city.Owner.Value == owner && BuildingQueries.HasBuilding(world, new((ushort)i), BuildingIds.Palace))
            {
                int distance = world.Map.Geometry.StepDistance(city.Position, position, world.Globals.Rules.FlatWorld);
                if (distance < result)
                { result = distance; }
            }
        }
        return result;
    }
}

public enum UnitEspionageSound { Spy, SmallExplosion }
public sealed record UnitMissionSubject(UnitTarget Unit, UnitType Type, int Owner, string Adjective, string Name);
public sealed record UnitBribeOffer(UnitMissionSubject Target, int Actor, int Price, bool Affordable);
public abstract record UnitMissionDecision
{
    public sealed record Menu(UnitMissionSubject Actor, UnitMissionSubject Target) : UnitMissionDecision;
    public sealed record Bribe(UnitBribeOffer Offer) : UnitMissionDecision;
    public sealed record Lifecycle(LifecycleDecision Decision) : UnitMissionDecision;
}

/// <summary>Semantic V06 presentation/publication. These callbacks do not choose outcomes or implement transfer/damage.</summary>
public interface IUnitEspionageEffects
{
    void Incorruptible(UnitMissionSubject target);
    void Sound(UnitEspionageSound sound);
    void Publish();
    void RefreshStatus(int civilization);
    void EnsureVisible(MapPoint position);
    void Deserted(UnitMissionSubject target, string newOwnerPlural);
    void Redraw(MapPoint position);
    void Delay(int ticks);
    void Sabotaged(UnitMissionSubject actor, UnitMissionSubject target);
    void Explosion(MapPoint position);
}

/// <summary>V06 §9 local bribery and the full unit-mission dispatcher. Owning movement/event callers admit
/// participants and targets; the human Spy menu is not a universal helper eligibility rule.</summary>
public sealed class UnitEspionage(GameState world, RuleTables rules, StackOperations stacks,
    CivilizationElimination lifecycle, EspionageIncidents incidents, IUnitEspionageEffects effects)
{
    public Operation Mission(int actorUnit, int targetUnit, int actor, string call) => new(this, actorUnit, targetUnit, actor, call);
    public Operation Bribe(int targetUnit, int actor, string call) => new(this, null, targetUnit, actor, call);
    private GameState World => world;
    private RuleTables Rules => rules;
    private StackOperations Stacks => stacks;
    private CivilizationElimination Lifecycle => lifecycle;
    private EspionageIncidents Incidents => incidents;
    private IUnitEspionageEffects Effects => effects;
    private bool Human(int c) => world.Globals.HumanPlayers.Contains(c);
    private UnitMissionSubject Subject(int slot, int? owner = null)
    {
        ref var unit = ref world.Entities.Units.Storage[slot];
        int c = owner ?? unchecked((sbyte)unit.Owner.Value);
        return new(new(new((ushort)slot), unit.Token), unit.Type, c, world.Names[c].Adjective, rules.Units[unit.Type.Value].Identity.Name);
    }

    public sealed class Operation : IDisposable
    {
        private enum Phase { Start, Menu, BribeStart, BribeReply, Escape, Done }
        private readonly UnitEspionage parent;
        private readonly int? actorUnit;
        private readonly int target, actor;
        private readonly string call;
        private Phase phase;
        private UnitMissionDecision? pending;
        private CivilizationElimination.Operation? escape;
        private int victim, price;
        private bool faulted, disposed;
        public int? Result { get; private set; }
        internal Operation(UnitEspionage parent, int? actorUnit, int target, int actor, string call)
        { this.parent = parent; this.actorUnit = actorUnit; this.target = target; this.actor = actor; this.call = call; }

        /// <summary>Result is V06's movement stop flag: sabotage returns 1 regardless of spy fate; bribery returns 0.
        /// Null decision with null Result means a nested lifecycle search yielded its session slice.</summary>
        public UnitMissionDecision? Resume(int? reply = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("A failed unit mission cannot resume"); }
            if (Result is not null)
            { return null; }
            try
            { MapKnowledge.RequireLocal(parent.World); return Advance(reply); }
            catch { faulted = true; escape?.Dispose(); throw; }
        }

        private UnitMissionDecision? Advance(int? reply)
        {
            var w = parent.World;
            while (true)
            {
                switch (phase)
                {
                    case Phase.Start:
                        ArgumentException.ThrowIfNullOrEmpty(call);
                        if (actorUnit is { } unit)
                        {
                            if (parent.Stacks.Count(target) >= 2)
                            { return Finish(0); }
                            if (parent.Human(actor) && w.Entities.Units.Storage[unit].Type.Value == UnitTypeIds.Spy && w.Globals.Rules.NormalCombat)
                            {
                                phase = Phase.Menu;
                                return pending = new UnitMissionDecision.Menu(parent.Subject(unit, actor), parent.Subject(target));
                            }
                        }
                        phase = Phase.BribeStart;
                        break;
                    case Phase.Menu:
                        if (reply is null)
                        { return pending; }
                        int choice = reply.Value;
                        reply = null;
                        pending = null;
                        if (choice < 0)
                        { return Finish(0); }
                        if (choice == 0)
                        { phase = Phase.BribeStart; break; }
                        Sabotage();
                        escape = parent.Lifecycle.BeginEscape(actorUnit!.Value, 0, -1, call + "/escape", parent.Incidents);
                        phase = Phase.Escape;
                        break;
                    case Phase.BribeStart:
                        if (parent.Stacks.Count(target) > 1)
                        { return Finish(0); }
                        victim = unchecked((sbyte)w.Entities.Units.Storage[target].Owner.Value);
                        if (w.Civilizations[victim].Government == Government.Democracy)
                        {
                            if (!w.Multiplayer.Saved.AutoplayActive && parent.Human(actor))
                            { parent.Effects.Incorruptible(parent.Subject(target, victim)); }
                            return Finish(0);
                        }
                        if (!parent.Human(actor) && !CivilizationKnowledge.KnowsAdvance(w.Civilizations, actor,
                            (int)parent.Rules.Units[UnitTypeIds.Diplomat].Prerequisite))
                        { return Finish(0); }
                        int distance = EspionageCosts.DistanceToCapital(w, victim, w.Entities.Units.Storage[target].Position);
                        if (w.Civilizations[actor].Government == Government.Communism && distance > 9)
                        { distance = 10; }
                        var rule = parent.Rules.Units[w.Entities.Units.Storage[target].Type.Value];
                        price = unchecked((sbyte)rule.CostRows * ((w.Civilizations[victim].Gold + 750) / (distance + 2)));
                        if (price < 0)
                        { price = 30000; }
                        if (rule.Role != RuleRole.Settle)
                        { price /= 2; }
                        if (!parent.Human(actor) && rule.Role == RuleRole.Trade)
                        { return Finish(0); }
                        if (parent.Human(actor) && !w.Multiplayer.Saved.AutoplayActive)
                        {
                            if (actor == w.Multiplayer.LocalHuman)
                            {
                                phase = Phase.BribeReply;
                                return pending = new UnitMissionDecision.Bribe(new(parent.Subject(target, victim), actor, price, price <= w.Civilizations[actor].Gold));
                            }
                        }
                        else if (w.Civilizations[actor].Gold / 2 < price)
                        { return Finish(0); }
                        Transfer();
                        return Finish(0);
                    case Phase.BribeReply:
                        if (reply is null)
                        { return pending; }
                        if (reply == 1)
                        { Transfer(); }
                        return Finish(0);
                    case Phase.Escape:
                        var pretext = escape!.Resume(pending is UnitMissionDecision.Lifecycle ? reply : null);
                        if (pretext is not null)
                        { return pending = new UnitMissionDecision.Lifecycle(pretext); }
                        pending = null;
                        if (escape.Result is null)
                        { return null; }
                        return Finish(1);
                    default:
                        return null;
                }
            }
        }

        private void Transfer()
        {
            var w = parent.World;
            ref var unit = ref w.Entities.Units.Storage[target];
            w.Civilizations[actor].Gold = unchecked(w.Civilizations[actor].Gold - price);
            int type = unit.Type.Value;
            w.Civilizations[victim].UnitsActive[type] = unchecked((byte)(w.Civilizations[victim].UnitsActive[type] - 1));
            w.Civilizations[actor].UnitsActive[type] = unchecked((byte)(w.Civilizations[actor].UnitsActive[type] + 1));
            w.Civilizations[actor].SubversionCount = unchecked((short)(w.Civilizations[actor].SubversionCount + 1));
            unit.Owner = new((byte)actor);
            unit.Home = null;
            unit.MovesUsed = 0;
            unit.Orders = UnitOrder.None;
            var city = CityQueries.Nearest(w, unit.Position).Slot;
            if (city is { } home && w.Entities.Cities.At(home).Owner.Value == actor)
            { unit.Home = home; }
            unit.Visibility = unit.Visibility.With(victim);
            w.Map.Mutate(new TileChange.Owner(unit.Position, unchecked((byte)actor)));
            parent.Effects.Publish();
            if (w.Multiplayer.LocalHuman == actor || w.Multiplayer.LocalHuman == victim)
            { parent.Effects.Sound(UnitEspionageSound.Spy); }
            if (parent.Human(actor) && w.Multiplayer.LocalHuman == actor)
            { parent.Effects.RefreshStatus(actor); }
            if (!w.Multiplayer.Saved.AutoplayActive && parent.Human(victim) && w.Multiplayer.LocalHuman == victim)
            {
                parent.Effects.EnsureVisible(unit.Position);
                parent.Effects.Deserted(parent.Subject(target), w.Names[actor].Tribe);
            }
            parent.Effects.Redraw(unit.Position);
            if (parent.Human(victim) && w.Multiplayer.LocalHuman == victim)
            { parent.Effects.Delay(30); }
        }

        private void Sabotage()
        {
            var w = parent.World;
            if (w.Multiplayer.LocalHuman == unchecked((sbyte)w.Entities.Units.Storage[target].Owner.Value) || w.Multiplayer.LocalHuman == actor)
            { parent.Effects.Sound(UnitEspionageSound.SmallExplosion); }
            parent.Effects.Sabotaged(parent.Subject(actorUnit!.Value, actor), parent.Subject(target));
            var position = w.Entities.Units.Storage[target].Position;
            parent.Effects.Explosion(position);
            ref var unit = ref w.Entities.Units.Storage[target];
            int remaining = UnitMovement.RemainingHitPoints(w, parent.Rules, ref unit);
            unit.Damage = unchecked((byte)(unit.Damage + remaining / 2));
            parent.Effects.Redraw(position);
        }

        private UnitMissionDecision? Finish(int result) { Result = result; phase = Phase.Done; pending = null; escape?.Dispose(); return null; }
        public void Dispose() { disposed = true; escape?.Dispose(); }
    }
}
