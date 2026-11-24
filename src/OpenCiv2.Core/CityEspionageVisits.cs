using OpenCiv2.Contracts;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

public enum CityEspionageVisitKind { Intelligence, City }
/// <summary>Required P07/P06 visit. The owning presentation service supplies the actual report/window,
/// then resumes the mission when it closes; a visit is not a map-revelation operation.</summary>
public sealed record CityEspionageVisit(CityEspionageVisitKind Kind, int Actor, int Victim, CityTarget City, string CityName);
public sealed record CityEspionageVisitNotice(CityEspionageVisitKind Kind, int Actor, int Victim, string ActorPlural,
    CityTarget City, string CityName, UnitType InitialType);
public interface ICityEspionageVisitEffects
{
    void Sound();
    void Notice(CityEspionageVisitNotice notice);
}

/// <summary>V06 §4 local embassy/investigation children. A pending visit must run the required P07/P06
/// presentation before it is acknowledged. The owning controller dispatches returned fallbacks.</summary>
public sealed class CityEspionageVisits(GameState world, UnitLoss loss, CivilizationElimination lifecycle,
    EspionageIncidents incidents, ICityEspionageVisitEffects effects)
{
    private GameState World => world;
    private UnitLoss Loss => loss;
    private CivilizationElimination Lifecycle => lifecycle;
    private EspionageIncidents Incidents => incidents;
    private ICityEspionageVisitEffects Effects => effects;
    private bool Human(int civilization) => world.Globals.HumanPlayers.Contains(civilization);
    private CityTarget Target(CitySlot city) => new(city, world.Entities.Cities.At(city).Token);
    private CityEspionageVisit Visit(CityMissionContext m, CityEspionageVisitKind kind) => new(kind, m.Actor, m.Victim, Target(m.City), world.Entities.Cities.At(m.City).Name);
    private void Notice(CityMissionContext m, CityEspionageVisitKind kind) => effects.Notice(new(kind, m.Actor, m.Victim,
        world.Names[m.Actor].Tribe, Target(m.City), world.Entities.Cities.At(m.City).Name, m.InitialType));
    public Operation Embassy(CityMissionContext mission, string call) => new(this, mission, call, true);
    public Operation Investigate(CityMissionContext mission, string call) => new(this, mission, call, false);

    public sealed class Operation : IDisposable
    {
        private readonly List<Exception> presentationFailures = [];
        public IReadOnlyList<Exception> PresentationFailures => presentationFailures.AsReadOnly();
        private enum Phase { Begin, Visit, Loss, Tail, Done }
        private readonly CityEspionageVisits parent;
        private readonly CityMissionContext mission;
        private readonly string call;
        private readonly bool embassy;
        private Phase phase;
        private bool human, faulted, disposed;
        private CivilizationElimination.Operation? child;
        private CityEspionageDecision? pending;
        public bool Completed => phase == Phase.Done;
        public CityMission? Fallback { get; private set; }
        internal Operation(CityEspionageVisits parent, CityMissionContext mission, string call, bool embassy)
        { this.parent = parent; this.mission = mission; this.call = call; this.embassy = embassy; }
        /// <summary>Any visit reply closes its window. Incident replies retain their separate reply1 semantics.
        /// Null without a decision/completion is a bounded nested lifecycle slice.</summary>
        public CityEspionageDecision? Resume(int? reply = null) => ResumeDecision(reply is { } value ? new(value) : null);
        public CityEspionageDecision? ResumeDecision(ResearchReply? fullReply = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("A failed city visit cannot resume"); }
            if (Completed)
            { return null; }
            try
            { MapKnowledge.RequireLocal(parent.World); return Advance(fullReply); }
            catch { faulted = true; child?.Dispose(); throw; }
        }
        private CityEspionageDecision? Advance(ResearchReply? fullReply)
        {
            int? reply = fullReply?.Selected;
            var w = parent.World;
            var m = mission;
            while (true)
            {
                switch (phase)
                {
                    case Phase.Begin:
                        ArgumentException.ThrowIfNullOrEmpty(call);
                        if (embassy)
                        {
                            if (w.Civilizations[m.Actor].Treaties[m.Victim].Embassy)
                            { Fallback = CityMission.Investigate; phase = Phase.Done; return null; }
                            // An embassy is directed; this is not the bilateral treaty setter.
                            w.Civilizations[m.Actor].Treaties[m.Victim].Embassy = true;
                        }
                        if (w.Multiplayer.LocalHuman == m.Actor || w.Multiplayer.LocalHuman == m.Victim)
                        { parent.Effects.Sound(); }
                        human = parent.Human(m.Actor);
                        if (human)
                        {
                            phase = Phase.Visit;
                            return pending = new CityEspionageDecision.Visit(parent.Visit(m, embassy ? CityEspionageVisitKind.Intelligence : CityEspionageVisitKind.City));
                        }
                        child = parent.Lifecycle.BeginDestroy(parent.Loss, m.Unit, call + "/destroy", parent.Incidents);
                        phase = Phase.Loss;
                        break;
                    case Phase.Visit:
                        if (reply is null)
                        { return pending; }
                        pending = null;
                        reply = null;
                        fullReply = null;
                        if (m.IsSpy)
                        {
                            if (embassy || !w.Entities.Cities.At(m.City).Flags.Investigated)
                            { ref var unit = ref w.Entities.Units.Storage[m.Unit]; unit.MovesUsed = unchecked((byte)(unit.MovesUsed + 1)); }
                            phase = Phase.Tail;
                        }
                        else
                        {
                            child = embassy ? parent.Lifecycle.BeginDelete(m.Unit, true, call + "/delete", parent.Incidents)
                                : parent.Lifecycle.BeginEscape(m.Unit, 0, -1, call + "/escape", parent.Incidents);
                            phase = Phase.Loss;
                        }
                        break;
                    case Phase.Loss:
                        var decision = child!.ResumeDecision(pending is CityEspionageDecision.Lifecycle ? fullReply : null);
                        reply = null;
                        fullReply = null;
                        if (decision is not null)
                        { return pending = new CityEspionageDecision.Lifecycle(decision); }
                        pending = null;
                        if (child.Result is null)
                        { return null; }
                        presentationFailures.AddRange(child.PresentationFailures);
                        child.Dispose();
                        child = null;
                        phase = Phase.Tail;
                        break;
                    case Phase.Tail:
                        if (!embassy && human)
                        { w.Entities.Cities.At(m.City).Flags.Investigated = true; }
                        if (!w.Multiplayer.Saved.AutoplayActive && parent.Human(m.Victim))
                        { parent.Notice(m, embassy ? CityEspionageVisitKind.Intelligence : CityEspionageVisitKind.City); }
                        phase = Phase.Done;
                        return null;
                    default:
                        return null;
                }
            }
        }
        public void Dispose() { disposed = true; child?.Dispose(); }
    }
}
