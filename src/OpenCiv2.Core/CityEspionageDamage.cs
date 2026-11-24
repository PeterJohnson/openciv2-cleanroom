using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

/// <summary>V06 city-controller captures, retained by fallback dispatch and its mission children.</summary>
public sealed record CityMissionContext(int Unit, CitySlot City, int Actor, int Victim, UnitType InitialType, bool IsSpy);
public enum CityEspionageQuestion { TreatyBreach, SpecificSabotage, SabotageTarget, HardSabotage, SpecificTheft, HardTheft, MajorIncident }
public sealed record CitySabotageTarget(int Building, string Name);
public abstract record CityEspionageDecision
{
    public sealed record Question(CityEspionageQuestion Kind, int Actor, CityTarget City, string Name,
        OwnedList<CitySabotageTarget>? Targets = null) : CityEspionageDecision;
    public sealed record Lifecycle(LifecycleDecision Decision) : CityEspionageDecision;
    public sealed record Technology(CapturedTechnologyChoice Choice) : CityEspionageDecision;
    public sealed record Grant(ResearchGrantDecision Decision) : CityEspionageDecision;
    public sealed record Visit(CityEspionageVisit Request) : CityEspionageDecision;
}
public enum CityEspionageSound { Spy, MediumExplosion }
public enum CityEspionageNoticeKind { Immune, ProductionSabotaged, BuildingSabotaged, WaterPoisoned }
public sealed record CityEspionageNotice(CityEspionageNoticeKind Kind, CityTarget City, string CityName,
    int Actor, int Victim, string ActorAdjective, int Building = 0, string? ItemName = null);
public interface ICityEspionageDamageEffects
{
    void Sound(CityEspionageSound sound);
    void RefreshCity(CitySlot city);
    void Redraw(MapPoint position);
    void Publish();
    void Notice(CityEspionageNotice notice);
}

/// <summary>V06 §§2,6 local sabotage and poison helpers. Owning city/movement dispatch supplies its
/// captured context; helper admission deliberately differs from the ordinary city mission menu.</summary>
public sealed class CityEspionageDamage(GameState world, RuleTables rules, GameplayRandom random,
    CivilizationElimination lifecycle, EspionageIncidents incidents, ICityEspionageDamageEffects effects)
{
    private GameState World => world;
    private RuleTables Rules => rules;
    private GameplayRandom Random => random;
    private CivilizationElimination Lifecycle => lifecycle;
    private EspionageIncidents Incidents => incidents;
    private ICityEspionageDamageEffects Effects => effects;
    public Operation Sabotage(CityMissionContext mission, string call) => new(this, mission, call, false);
    public Operation Poison(CityMissionContext mission, string call) => new(this, mission, call, true);
    private bool Human(int c) => world.Globals.HumanPlayers.Contains(c);
    private bool HumanNotice(int c) => Human(c) && !world.Multiplayer.Saved.AutoplayActive;
    private CityTarget Target(CitySlot city) => new(city, world.Entities.Cities.At(city).Token);
    private CityEspionageDecision.Question Question(CityMissionContext m, CityEspionageQuestion kind,
        OwnedList<CitySabotageTarget>? targets = null) => new(kind, m.Actor, Target(m.City), world.Entities.Cities.At(m.City).Name, targets);
    private void Notice(CityMissionContext m, CityEspionageNoticeKind kind, int building = 0, string? name = null) =>
        effects.Notice(new(kind, Target(m.City), world.Entities.Cities.At(m.City).Name, m.Actor, m.Victim,
            world.Names[m.Actor].Adjective, building, name));

    public sealed class Operation : IDisposable
    {
        private sealed record Step(CityEspionageDecision? Decision = null, CivilizationElimination.Operation? Child = null);
        private readonly CityEspionageDamage parent;
        private readonly CityMissionContext mission;
        private readonly string call;
        private readonly IEnumerator<Step> steps;
        private CivilizationElimination.Operation? child;
        private CityEspionageDecision? pending;
        private int answer;
        private bool childLost, disposed, faulted;
        public bool Completed { get; private set; }
        internal Operation(CityEspionageDamage parent, CityMissionContext mission, string call, bool poison)
        { this.parent = parent; this.mission = mission; this.call = call; steps = Execute(poison).GetEnumerator(); }

        /// <summary>Null with Completed=false is a bounded nested lifecycle slice. Decisions consume
        /// one reply only; failures/disposal retain committed effects and cannot restart the operation.</summary>
        public CityEspionageDecision? Resume(int? reply = null) => ResumeDecision(reply is { } value ? new(value) : null);
        public CityEspionageDecision? ResumeDecision(ResearchReply? fullReply = null)
        {
            int? reply = fullReply?.Selected;
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("A failed city mission cannot resume"); }
            if (Completed)
            { return null; }
            try
            {
                MapKnowledge.RequireLocal(parent.World);
                if (pending is not null && reply is null)
                { return pending; }
                ResearchReply? childReply = pending is CityEspionageDecision.Lifecycle ? fullReply : null;
                if (pending is CityEspionageDecision.Question)
                { answer = reply!.Value; }
                pending = null;
                while (true)
                {
                    if (child is not null)
                    {
                        var incident = child.ResumeDecision(childReply);
                        childReply = null;
                        if (incident is not null)
                        { return pending = new CityEspionageDecision.Lifecycle(incident); }
                        if (child.Result is null)
                        { return null; }
                        childLost = child.Result == 1;
                        child.Dispose();
                        child = null;
                    }
                    if (!steps.MoveNext())
                    { Completed = true; steps.Dispose(); return null; }
                    var step = steps.Current;
                    if (step.Decision is not null)
                    { return pending = step.Decision; }
                    child = step.Child ?? throw new InvalidOperationException("Empty city mission step");
                }
            }
            catch { faulted = true; Release(); throw; }
        }

        private Step Ask(CityEspionageQuestion kind, OwnedList<CitySabotageTarget>? targets = null) => new(parent.Question(mission, kind, targets));
        private Step Caught(int index) => new(Child: parent.Lifecycle.BeginCaught(mission.Unit, mission.Victim,
            call + "/capture/" + index, parent.Incidents));
        private Step Escape(int mode) => new(Child: parent.Lifecycle.BeginEscape(mission.Unit, mode, mission.Victim,
            call + "/escape", parent.Incidents));
        private bool Building(int id) => BuildingQueries.HasBuilding(parent.World, mission.City, id);

        private IEnumerable<Step> Execute(bool poison)
        {
            ArgumentException.ThrowIfNullOrEmpty(call);
            var w = parent.World;
            var rules = parent.Rules;
            var m = mission;
            var relation = w.Civilizations[m.Actor].Treaties[m.Victim];
            if (parent.Human(m.Actor) && (relation.Ceasefire || relation.Peace || relation.Alliance))
            {
                yield return Ask(CityEspionageQuestion.TreatyBreach);
                if (answer != 1)
                { yield break; }
            }
            if (poison)
            {
                ref var city = ref w.Entities.Cities.At(m.City);
                if (city.Size < 2)
                { city.FoodBox = 0; }
                else
                { city.Size = unchecked((sbyte)(city.Size - 1)); }
                MapKnowledge.RevealCity(w, m.Actor, m.City);
                parent.Effects.Redraw(city.Position);
                if (parent.HumanNotice(m.Actor))
                {
                    parent.Effects.Sound(CityEspionageSound.Spy);
                    parent.Notice(m, CityEspionageNoticeKind.WaterPoisoned);
                }
                int mode = parent.Random.PrimaryModulo(new ChoiceKey("V06.poison.escape-mode", call), 2);
                yield return Escape(mode);
                yield break;
            }

            int production = w.Entities.Cities.At(m.City).Production;
            string productionName = production >= 0 ? rules.Units[production].Identity.Name : rules.Improvements[-production].Identity.Name;
            int start = parent.Random.PrimaryModulo(new ChoiceKey("V06.sabotage.building-start", call), 39), target = 0, escapeMode = 0;
            for (int i = 0; i < 39; ++i)
            {
                int candidate = (start + i) % 39;
                if (candidate != 0 && candidate != BuildingIds.Palace && candidate != BuildingIds.SdiDefense && Building(candidate))
                { target = candidate; break; }
            }
            if (parent.HumanNotice(m.Actor) && m.IsSpy && target > 0)
            {
                yield return Ask(CityEspionageQuestion.SpecificSabotage);
                if (answer < 0)
                { yield break; }
                if (answer == 1)
                {
                    List<CitySabotageTarget> targets = [];
                    for (int id = 1; id <= 38; ++id)
                    {
                        if (id != BuildingIds.Palace && Building(id))
                        { targets.Add(new(id, rules.Improvements[id].Identity.Name)); }
                    }
                    targets.Add(new(0, productionName));
                    yield return Ask(CityEspionageQuestion.SabotageTarget, new(targets));
                    if (answer < 0)
                    { yield break; }
                    // V06 Appendix A: retain custom 35..38, but stop before unsupported rule-name reads.
                    if (answer > 38)
                    { throw new ArgumentOutOfRangeException(nameof(answer), "Sabotage target has no supported rule-name backing"); }
                    target = answer;
                    if (target == BuildingIds.SdiDefense)
                    { parent.Notice(m, CityEspionageNoticeKind.Immune, target); yield break; }
                    if (Building(BuildingIds.Palace) || target == BuildingIds.CityWalls)
                    {
                        yield return Ask(CityEspionageQuestion.HardSabotage);
                        if (answer != 1)
                        { yield break; }
                    }
                    yield return Caught(0);
                    if (childLost)
                    { yield break; }
                    if (Building(BuildingIds.Palace))
                    { yield return Caught(1); if (childLost) { yield break; } }
                    if (target == BuildingIds.CityWalls)
                    { yield return Caught(2); if (childLost) { yield break; } }
                    escapeMode = 1;
                }
            }
            if (escapeMode == 0 && w.Entities.Cities.At(m.City).ShieldBox != 0)
            {
                int odds = (m.IsSpy ? 3 : 2) + (w.Entities.Units.Storage[m.Unit].Flags.Veteran ? 1 : 0);
                if (parent.Random.PrimaryModulo(new ChoiceKey("V06.sabotage.production", call), odds) == 0)
                { target = 0; }
            }
            if (parent.Human(m.Actor) || parent.Human(m.Victim))
            {
                if (w.Multiplayer.LocalHuman == m.Actor || w.Multiplayer.LocalHuman == m.Victim)
                {
                    bool gunpowder = CivilizationKnowledge.KnowsAdvance(w.Civilizations, m.Actor, AdvanceIds.Gunpowder);
                    parent.Effects.Sound(gunpowder && w.Multiplayer.LocalHuman == m.Actor ? CityEspionageSound.MediumExplosion : CityEspionageSound.Spy);
                }
                if (target == 0)
                {
                    w.Entities.Cities.At(m.City).ShieldBox = 0;
                    parent.Effects.RefreshCity(m.City);
                    if (parent.HumanNotice(m.Actor))
                    { parent.Notice(m, CityEspionageNoticeKind.ProductionSabotaged, name: productionName); }
                }
                else
                {
                    BuildingQueries.SetBuilding(w, m.City, target, false);
                    parent.Effects.Redraw(w.Entities.Cities.At(m.City).Position);
                    string name = rules.Improvements[target].Identity.Name;
                    parent.Effects.Publish();
                    if (parent.HumanNotice(m.Actor))
                    {
                        parent.Effects.RefreshCity(m.City);
                        parent.Notice(m, CityEspionageNoticeKind.BuildingSabotaged, target, name);
                    }
                }
            }
            yield return Escape(escapeMode);
        }

        private void Release() { child?.Dispose(); steps.Dispose(); }
        public void Dispose() { disposed = true; Release(); }
    }
}
