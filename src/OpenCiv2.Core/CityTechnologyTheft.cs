using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

public enum TechnologyTheftNoticeKind { Unavailable, Stolen, Detected }
public sealed record TechnologyTheftNotice(TechnologyTheftNoticeKind Kind, CityTarget City, string CityName,
    int Actor, int Victim, string ActorAdjective, int? Advance = null, string? AdvanceName = null,
    AdvanceCategory? Category = null, AdvanceEpoch? Epoch = null, UnitType? Detector = null, string? DetectorName = null);
public interface ITechnologyTheftEffects
{
    void Sound();
    void Redraw(MapPoint point);
    void Notice(TechnologyTheftNotice notice);
}

/// <summary>V06 §5 ordinary/targeted theft. Real V03 grants/captured selection and V01/V06
/// capture/escape execute before completion. The owning controller executes a returned fallback.</summary>
public sealed class CityTechnologyTheft(GameState world, RuleTables rules, GameplayRandom random, StackOperations stacks,
    ResearchGrants grants, CapturedTechnology captured, CivilizationElimination lifecycle,
    EspionageIncidents incidents, ITechnologyTheftEffects effects)
{
    private GameState World => world;
    private RuleTables Rules => rules;
    private GameplayRandom Random => random;
    private StackOperations Stacks => stacks;
    private ResearchGrants Grants => grants;
    private CapturedTechnology Captured => captured;
    private CivilizationElimination Lifecycle => lifecycle;
    private EspionageIncidents Incidents => incidents;
    private ITechnologyTheftEffects Effects => effects;
    private bool Human(int c) => world.Globals.HumanPlayers.Contains(c);
    private bool HumanNotice(int c) => Human(c) && !world.Multiplayer.Saved.AutoplayActive;
    private CityTarget Target(CitySlot city) => new(city, world.Entities.Cities.At(city).Token);
    private void Notice(CityMissionContext m, TechnologyTheftNoticeKind kind, int? advance = null, UnitType? detector = null)
    {
        var rule = advance is { } a ? rules.Advances[a] : null;
        effects.Notice(new(kind, Target(m.City), world.Entities.Cities.At(m.City).Name, m.Actor, m.Victim,
            world.Names[m.Actor].Adjective, advance, rule?.Identity.Name, rule?.Category, rule?.Epoch,
            detector, detector is { } type ? rules.Units[type.Value].Identity.Name : null));
    }
    public Operation Begin(CityMissionContext mission, string call) => new(this, mission, call);

    public sealed class Operation : IDisposable
    {
        private sealed record Step(CityEspionageDecision? Decision = null, CivilizationElimination.Operation? Loss = null,
            CapturedTechnology.Operation? Selection = null, ResearchGrants.Operation? Grant = null);
        private readonly CityTechnologyTheft parent;
        private readonly CityMissionContext mission;
        private readonly string call;
        private readonly IEnumerator<Step> steps;
        private CivilizationElimination.Operation? loss;
        private CapturedTechnology.Operation? selection;
        private ResearchGrants.Operation? grant;
        private readonly List<Exception> presentationFailures = [];
        public IReadOnlyList<Exception> PresentationFailures => presentationFailures.AsReadOnly();
        private CityEspionageDecision? pending;
        private int answer;
        private bool lost, disposed, faulted;
        public bool Completed { get; private set; }
        public CityMission? Fallback { get; private set; }
        internal Operation(CityTechnologyTheft parent, CityMissionContext mission, string call)
        { this.parent = parent; this.mission = mission; this.call = call; steps = Execute().GetEnumerator(); }

        public CityEspionageDecision? Resume(ResearchReply? reply = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("A failed theft cannot resume"); }
            if (Completed)
            { return null; }
            try
            {
                MapKnowledge.RequireLocal(parent.World);
                if (pending is not null && reply is null)
                { return pending; }
                ResearchReply? incidentReply = pending is CityEspionageDecision.Lifecycle ? reply : null;
                ResearchReply? selectionReply = selection is not null && pending is CityEspionageDecision.Technology or CityEspionageDecision.Grant ? reply : null;
                int? grantReply = grant is not null && pending is CityEspionageDecision.Grant ? reply?.Selected : null;
                if (pending is CityEspionageDecision.Question)
                { answer = reply!.Value.Selected; }
                pending = null;
                while (true)
                {
                    if (loss is not null)
                    {
                        var incident = loss.ResumeDecision(incidentReply);
                        incidentReply = null;
                        if (incident is not null)
                        { return pending = new CityEspionageDecision.Lifecycle(incident); }
                        if (loss.Result is null)
                        { return null; }
                        lost = loss.Result == 1;
                        presentationFailures.AddRange(loss.PresentationFailures);
                        loss.Dispose();
                        loss = null;
                    }
                    if (selection is not null)
                    {
                        var choice = selection.Resume(selectionReply);
                        selectionReply = null;
                        if (choice is CapturedTechnologyChoice technology)
                        { return pending = new CityEspionageDecision.Technology(technology); }
                        if (choice is CapturedTechnologyDecision.Grant granted)
                        { return pending = new CityEspionageDecision.Grant(granted.Decision); }
                        if (!selection.Finished)
                        { return null; }
                        presentationFailures.AddRange(selection.PresentationFailures);
                        selection.Dispose();
                        selection = null;
                    }
                    if (grant is not null)
                    {
                        var question = grant.Resume(grantReply);
                        grantReply = null;
                        if (question is not null)
                        { return pending = new CityEspionageDecision.Grant(question); }
                        if (!grant.Completed)
                        { return null; }
                        presentationFailures.AddRange(grant.PresentationFailures);
                        grant.Dispose();
                        grant = null;
                    }
                    if (!steps.MoveNext())
                    { Completed = true; steps.Dispose(); return null; }
                    var step = steps.Current;
                    if (step.Decision is not null)
                    { return pending = step.Decision; }
                    loss = step.Loss;
                    selection = step.Selection;
                    grant = step.Grant;
                }
            }
            catch { faulted = true; Release(); throw; }
        }
        private Step Ask(CityEspionageQuestion question) => new(new CityEspionageDecision.Question(question, mission.Actor,
            parent.Target(mission.City), parent.World.Entities.Cities.At(mission.City).Name));
        private Step Capture() => new(Loss: parent.Lifecycle.BeginCaught(mission.Unit, mission.Victim, call + "/capture", parent.Incidents));
        private Step Escape(int mode) => new(Loss: parent.Lifecycle.BeginEscape(mission.Unit, mode, mission.Victim, call + "/escape", parent.Incidents));
        private IEnumerable<Step> Execute()
        {
            ArgumentException.ThrowIfNullOrEmpty(call);
            var w = parent.World;
            var r = parent.Rules;
            var m = mission;
            var relation = w.Civilizations[m.Actor].Treaties[m.Victim];
            if (parent.Human(m.Actor) && (relation.Ceasefire || relation.Peace || relation.Alliance))
            { yield return Ask(CityEspionageQuestion.TreatyBreach); if (answer != 1) { yield break; } }
            int start = parent.Random.PrimaryModulo(new ChoiceKey("V06.theft.advance-start", call), 100), candidate = -1;
            for (int i = 0; i < 100; ++i)
            {
                int a = (start + i) % 100;
                var rule = r.Advances[a];
                if (rule.Prerequisites[0] == AdvanceReference.Never && rule.Prerequisites[1] == AdvanceReference.Never
                    || CivilizationKnowledge.KnowsAdvance(w.Civilizations, m.Actor, a)
                    || !CivilizationKnowledge.KnowsAdvance(w.Civilizations, m.Victim, a))
                { continue; }
                candidate = a;
                if (!SpaceshipQueries.RaceActive(w) || w.Globals.Difficulty < Difficulty.Prince && !parent.Human(m.Actor)
                    || (int)r.Improvements[BuildingIds.Structural].Prerequisite == a
                    || (int)r.Improvements[BuildingIds.Component].Prerequisite == a
                    || (int)r.Improvements[BuildingIds.Module].Prerequisite == a || a == AdvanceIds.FusionPower)
                { break; }
            }
            if (candidate < 0)
            {
                if (parent.Human(m.Actor))
                {
                    ref var unit = ref w.Entities.Units.Storage[m.Unit];
                    unit.MovesUsed = unchecked((byte)(unit.MovesUsed + r.Cosmic.RoadMovementMultiplier));
                    if (parent.HumanNotice(m.Actor))
                    { parent.Notice(m, TechnologyTheftNoticeKind.Unavailable); }
                }
                else
                {
                    var current = w.Civilizations[m.Actor].Treaties[m.Victim];
                    Fallback = current.Ceasefire || current.Peace || current.Alliance ? CityMission.Embassy : CityMission.Sabotage;
                }
                yield break;
            }
            int escapeMode = 0;
            if (!w.Entities.Cities.At(m.City).Flags.TechnologyStolen)
            {
                if (parent.HumanNotice(m.Actor) && m.IsSpy)
                {
                    yield return Ask(CityEspionageQuestion.SpecificTheft);
                    if (answer < 0)
                    { yield break; }
                    if (answer == 1)
                    {
                        yield return Capture();
                        if (lost)
                        { yield break; }
                        if (w.Multiplayer.LocalHuman == m.Actor)
                        { parent.Effects.Sound(); }
                        yield return new(Selection: parent.Captured.Begin(m.Actor, m.Victim, call + "/take"));
                        yield return Escape(1);
                        w.Entities.Cities.At(m.City).Flags.TechnologyStolen = true;
                        yield break;
                    }
                }
            }
            else
            {
                if (parent.HumanNotice(m.Actor))
                { yield return Ask(CityEspionageQuestion.HardTheft); if (answer != 1) { yield break; } }
                yield return Capture();
                if (lost)
                { yield break; }
                escapeMode = 1;
            }
            UnitType? detector = null;
            for (int u = parent.Stacks.FirstAt(w.Entities.Cities.At(m.City).Position); u >= 0; u = parent.Stacks.Next(u))
            {
                var defender = w.Entities.Units.Storage[u];
                if (r.Units[defender.Type.Value].Role != RuleRole.Diplomacy)
                { continue; }
                int threshold = defender.Type.Value == UnitTypeIds.Spy ? (defender.Flags.Veteran ? 60 : 40) : 20;
                if (parent.Random.PrimaryModulo(new ChoiceKey("V06.theft.detect", call, u), 100) < threshold)
                { detector = defender.Type; break; }
            }
            if (detector is { } type)
            {
                UnitMovement.Exhaust(w, r, ref w.Entities.Units.Storage[m.Unit]);
                parent.Effects.Redraw(w.Entities.Cities.At(m.City).Position);
                if (parent.HumanNotice(m.Actor))
                { parent.Effects.Sound(); parent.Notice(m, TechnologyTheftNoticeKind.Detected, detector: type); }
                yield break;
            }
            if (parent.HumanNotice(m.Actor))
            { parent.Effects.Sound(); parent.Notice(m, TechnologyTheftNoticeKind.Stolen, candidate); }
            yield return new(Grant: parent.Grants.Begin(m.Actor, candidate, m.Victim, false, call + "/grant"));
            w.Entities.Cities.At(m.City).Flags.TechnologyStolen = true;
            yield return Escape(escapeMode);
        }
        private void Release() { loss?.Dispose(); selection?.Dispose(); grant?.Dispose(); steps.Dispose(); }
        public void Dispose() { disposed = true; Release(); }
    }
}
