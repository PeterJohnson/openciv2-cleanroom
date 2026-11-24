using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Units;

public enum ParadropRejection { AlreadyMoved, RequiresCityOrAirbase, Ocean, ForeignUnit, Range }
public sealed record ParadropAnnouncement(UnitSlot Unit, string Adjective, string UnitName, string? CityName, MapPoint Target);
public interface IParadropEffects
{
    void Rejected(UnitSlot unit, ParadropRejection reason, int range);
    void EnterTargeting(UnitSlot unit);
    void EnsureVisible(MapPoint target, int actor);
    void Announce(ParadropAnnouncement announcement);
    void Redraw(MapPoint origin);
    void Animate(UnitSlot unit, MapPoint approach, MapPoint target, int direction);
    void MoveCursor(MapPoint target);
    void CityUnitChanged(UnitSlot unit, MapPoint origin);
    void RedrawStatus();
}
public abstract record ParadropDecision
{
    public sealed record Attack(AttackWarning Warning) : ParadropDecision;
    public sealed record Capture(CityCaptureDecision Decision) : ParadropDecision;
    public sealed record Contact(DiplomaticContactDecision Decision) : ParadropDecision;
}

/// <summary>U05 local paradrop with actual V04 permission, U01 cargo/placement, C09 capture and W03
/// revelation. P01/P05 own command availability and map-click acceptance. See U05's X01 repairs
/// for unwritten presentation locals, absent nearest cities and absent animation approaches.</summary>
public sealed class Paradrop(GameState world, RuleTables rules, StackOperations stacks, UnitCargo cargo,
    UnitRevelation revelation, CityCapture capture, AttackConfirmation confirmation, GameplayRandom random, IParadropEffects effects)
{
    private GameState World => world;
    private ref Unit Unit(UnitSlot slot) => ref world.Entities.Units.At(slot);
    private bool Human(int owner) => world.Globals.HumanPlayers.Contains(owner);
    public bool Enter()
    {
        MapKnowledge.RequireLocal(world);
        var actor = world.Globals.ActiveUnit ?? throw new InvalidOperationException("Paradrop requires an active actor");
        if (Unit(actor).MovesUsed != 0 || Unit(actor).Flags.Paradropped)
        { effects.Rejected(actor, ParadropRejection.AlreadyMoved, rules.Cosmic.ParadropRange); return false; }
        if ((world.Map.TileAt(Unit(actor).Position).Features & MapFeatures.CitySite) == 0)
        { effects.Rejected(actor, ParadropRejection.RequiresCityOrAirbase, rules.Cosmic.ParadropRange); return false; }
        effects.EnterTargeting(actor);
        return true;
    }
    public Operation Begin(UnitSlot actor, MapPoint target, string call) => new(this, actor, target, call);
    public sealed class Operation : IDisposable
    {
        private readonly Paradrop parent;
        private readonly IEnumerator<ParadropDecision?> steps;
        private ResearchReply? reply;
        private readonly List<Exception> failures = [];
        private bool disposed, faulted;
        public bool Completed { get; private set; }
        public bool Landed { get; internal set; }
        public ParadropDecision? Pending { get; private set; }
        public IReadOnlyList<Exception> PresentationFailures => failures.AsReadOnly();
        internal Operation(Paradrop parent, UnitSlot actor, MapPoint target, string call)
        {
            ArgumentException.ThrowIfNullOrEmpty(call);
            this.parent = parent;
            steps = parent.Execute(this, actor, target, call).GetEnumerator();
        }
        internal ResearchReply? TakeReply() { var result = reply; reply = null; return result; }
        internal void Report(IEnumerable<Exception> errors) => failures.AddRange(errors);
        public ParadropDecision? Resume(ResearchReply? response = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("A failed paradrop cannot resume"); }
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
                Completed = true;
                steps.Dispose();
                return null;
            }
            catch { faulted = true; steps.Dispose(); throw; }
        }
        public void Dispose() { disposed = true; steps.Dispose(); }
    }
    private MapPoint Neighbor(MapPoint target, int index) => index == MapLimits.Directions ? target
        : MapNeighborhoods.Offset(world.Map.Geometry, target, MapNeighborhoods.Directions[index], world.Globals.Rules.FlatWorld);

    private (bool[] Seen, bool[] Animate) Viewers(int owner, int targetOwner, CitySlot? city, MapPoint target)
    {
        // X01 repair: presentation-only unwritten entries start false; no original stack inputs.
        bool[] seen = new bool[EntityLimits.Civilizations], animate = new bool[EntityLimits.Civilizations];
        for (int viewer = 1; viewer < EntityLimits.Civilizations; ++viewer)
        {
            if (world.Multiplayer.LocalHuman != viewer || !world.Globals.CivsAlive.Contains(viewer) || !Human(viewer))
            { continue; }
            seen[viewer] = owner == viewer || world.Globals.RevealMap != 0;
            if (city is not null && targetOwner != owner && world.Map.TileAt(target).RevealedTo.Contains(viewer)
                && (world.Civilizations[viewer].Treaties[owner].Embassy || world.Civilizations[viewer].Treaties[targetOwner].Embassy))
            { seen[viewer] = true; }
            for (int i = 0; i <= MapLimits.Directions; ++i)
            {
                var point = Neighbor(target, i);
                if (world.Map.Geometry.InBounds(point) && world.Map.CityOrUnitOwner(point) == viewer)
                { seen[viewer] = true; }
            }
            animate[viewer] = seen[viewer] && (viewer == owner || world.Globals.GameOptions.EnemyMoves || city is not null);
        }
        return (seen, animate);
    }
    private bool Local(bool[] viewers) => world.Multiplayer.LocalHuman is > 0 and < EntityLimits.Civilizations && viewers[world.Multiplayer.LocalHuman];

    private IEnumerable<ParadropDecision?> Execute(Operation operation, UnitSlot actor, MapPoint target, string call)
    {
        int owner = Unit(actor).Owner.Value;
        var origin = Unit(actor).Position;
        int range = rules.Cosmic.ParadropRange;
        ParadropRejection? rejection = world.Map.TileAt(target).Ocean ? ParadropRejection.Ocean
            : world.Map.UnitOwner(target) is { } unitOwner && unitOwner != owner ? ParadropRejection.ForeignUnit
            : world.Map.Geometry.StepDistance(Unit(actor).Position, target, world.Globals.Rules.FlatWorld) > range ? ParadropRejection.Range : null;
        if (rejection is { } reason)
        { if (Human(owner)) { effects.Rejected(actor, reason, range); } yield break; }
        int targetOwner = world.Map.Owner(target) ?? -1;
        CitySlot? city = CityQueries.At(world, target);
        if (city is not null && targetOwner != owner && DiplomaticRelations.Intersects(world.Civilizations[owner].Treaties[targetOwner],
            new() { Ceasefire = true, Peace = true, Alliance = true }))
        {
            if (!Human(owner))
            { UnitMovement.Exhaust(world, rules, ref Unit(actor)); yield break; }
            using var attack = confirmation.Begin(owner, targetOwner, new() { Ceasefire = true, Peace = true, Alliance = true });
            if (attack.Resume() is { } warning)
            {
                yield return new ParadropDecision.Attack(warning);
                attack.Resume(operation.TakeReply()!.Value.Selected);
            }
            if (attack.Blocked)
            { yield break; }
        }
        var (seen, animate) = Viewers(owner, targetOwner, city, target);
        var nearest = CityQueries.Nearest(world, target).Slot;
        var announcement = new ParadropAnnouncement(actor, world.Names[owner].Adjective,
            rules.Units[Unit(actor).Type.Value].Identity.Name, nearest is { } nearby ? world.Entities.Cities.At(nearby).Name : null, target);
        if (Local(seen))
        {
            effects.EnsureVisible(target, owner);
            if (!Human(owner) && !world.Multiplayer.Saved.AutoplayActive)
            { effects.Announce(announcement); }
        }
        int best = 0;
        int? direction = null;
        MapPoint? approach = null;
        for (int i = 0; i < MapLimits.Directions; ++i)
        {
            var point = Neighbor(target, i);
            if (!world.Map.Geometry.InBounds(point))
            { continue; }
            int score = random.PrimaryModulo(new ChoiceKey("U05.paradrop-approach", call, i), 6);
            var offset = MapNeighborhoods.Directions[i];
            if (offset.X != 0 && offset.Y != 0)
            { score += 3; }
            if (world.Map.CityOrUnitOwner(point) is null)
            { score += 200; }
            if (score > best)
            { best = score; direction = i ^ 4; approach = point; }
        }
        Unit(actor).Orders = UnitOrder.None;
        cargo.Lift(actor.Value, false);
        int observer = world.Multiplayer.LocalHuman;
        if (observer == owner || world.Globals.RevealMap != 0 || Unit(actor).Visibility.Contains(observer)
            || Unit(actor).Owner.Value == unchecked((byte)observer))
        { effects.Redraw(origin); }
        // No winning approach suppresses only animation. Gameplay relocation continues.
        if (Local(animate) && approach is { } from && direction is { } heading)
        { effects.Animate(actor, from, target, heading); }
        stacks.ClearVisibility(actor.Value);
        if (world.Globals.ActiveUnit == actor && world.Multiplayer.LocalHuman == owner)
        { effects.MoveCursor(target); }
        stacks.RelocateStack(actor.Value, target, false);
        operation.Landed = true;
        Unit(actor).Flags.Paradropped = true;
        if (city is { } capturedCity && targetOwner != owner)
        {
            if (!Human(owner))
            { UnitMovement.Exhaust(world, rules, ref Unit(actor)); }
            Unit(actor).Damage = 0;
            world.Globals.MovingUnit = actor;
            using var child = capture.Begin(capturedCity, owner, 0, range, call + "/capture");
            while (!child.Completed)
            {
                if (child.Resume(operation.TakeReply()) is { } pending)
                { yield return new ParadropDecision.Capture(pending); }
                else if (!child.Completed)
                { yield return null; }
            }
            operation.Report(child.PresentationFailures);
            actor = world.Globals.MovingUnit ?? throw new InvalidOperationException("Capture did not retain a continuing paradrop actor");
        }
        using (var child = revelation.Begin(actor.Value, true, call + "/revelation"))
        {
            while (!child.Completed)
            {
                if (child.Resume(operation.TakeReply()) is { } pending)
                { yield return new ParadropDecision.Contact(pending); }
                else if (!child.Completed)
                { yield return null; }
            }
            operation.Report(child.PresentationFailures);
        }
        effects.CityUnitChanged(actor, origin);
        Unit(actor).Flags.Garrison = false;
        effects.RedrawStatus();
    }
}
