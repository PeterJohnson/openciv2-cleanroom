using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

public sealed record PlantedNuclearNotice(CityTarget City, string CityName, int Actor, int Victim,
    string ActorAdjective, string ActorPlural, bool Identified);

/// <summary>V06 §7 local device mission. City admission owns technology/menu gates; this helper owns
/// confirmation, actual capture checks, U07 action and identification. No final escape is added.</summary>
public sealed class PlantedNuclearDevice(GameState world, GameplayRandom random, CivilizationElimination lifecycle,
    EspionageIncidents incidents, NuclearEffects nuclear, Action<PlantedNuclearNotice> notice)
{
    private GameState World => world;
    public Operation Begin(CityMissionContext mission, string call) => new(this, mission, call);
    private CityTarget Target(CitySlot city) => new(city, world.Entities.Cities.At(city).Token);

    public sealed class Operation : IDisposable
    {
        private readonly PlantedNuclearDevice parent;
        private readonly IEnumerator<Step> steps;
        private CivilizationElimination.Operation? capture;
        private NuclearEffects.Operation? explosion;
        private readonly List<Exception> presentationFailures = [];
        public IReadOnlyList<Exception> PresentationFailures => presentationFailures.AsReadOnly();
        private CityEspionageDecision? pending;
        private bool lost, faulted, disposed;
        private int answer;
        public bool Completed { get; private set; }
        private sealed record Step(CityEspionageDecision? Decision = null,
            CivilizationElimination.Operation? Capture = null, NuclearEffects.Operation? Explosion = null);
        internal Operation(PlantedNuclearDevice parent, CityMissionContext mission, string call)
        {
            ArgumentException.ThrowIfNullOrEmpty(call);
            this.parent = parent;
            steps = Execute(mission, call).GetEnumerator();
        }
        /// <summary>Null with Completed=false is a bounded nested lifecycle slice.</summary>
        public CityEspionageDecision? Resume(int? reply = null) => ResumeDecision(reply is { } value ? new(value) : null);
        public CityEspionageDecision? ResumeDecision(ResearchReply? fullReply = null)
        {
            int? reply = fullReply?.Selected;
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            {
                throw new InvalidOperationException("A failed device mission cannot resume");
            }

            if (Completed)
            {
                return null;
            }

            try
            {
                MapKnowledge.RequireLocal(parent.World);
                if (pending is not null && reply is null)
                {
                    return pending;
                }

                ResearchReply? childReply = pending is CityEspionageDecision.Lifecycle ? fullReply : null;
                if (pending is CityEspionageDecision.Question)
                {
                    answer = reply!.Value;
                }

                pending = null;
                while (true)
                {
                    if (capture is not null)
                    {
                        var incident = capture.ResumeDecision(childReply);
                        childReply = null;
                        if (incident is not null)
                        {
                            return pending = new CityEspionageDecision.Lifecycle(incident);
                        }

                        if (capture.Result is null)
                        {
                            return null;
                        }

                        lost = capture.Result == 1;
                        presentationFailures.AddRange(capture.PresentationFailures);
                        capture.Dispose();
                        capture = null;
                    }
                    if (explosion is not null)
                    {
                        var incident = explosion.ResumeDecision(childReply);
                        childReply = null;
                        if (incident is not null)
                        {
                            return pending = new CityEspionageDecision.Lifecycle(incident);
                        }

                        if (explosion.Result is null)
                        {
                            return null;
                        }

                        presentationFailures.AddRange(explosion.PresentationFailures);
                        explosion.Dispose();
                        explosion = null;
                    }
                    if (!steps.MoveNext())
                    { Completed = true; steps.Dispose(); return null; }
                    var step = steps.Current;
                    if (step.Decision is not null)
                    {
                        return pending = step.Decision;
                    }

                    capture = step.Capture;
                    explosion = step.Explosion;
                }
            }
            catch { faulted = true; Release(); throw; }
        }
        private IEnumerable<Step> Execute(CityMissionContext mission, string call)
        {
            var w = parent.World;
            if (w.Multiplayer.Saved.AutoplayActive)
            {
                yield break;
            }

            yield return new(new CityEspionageDecision.Question(CityEspionageQuestion.MajorIncident, mission.Actor,
                parent.Target(mission.City), w.Entities.Cities.At(mission.City).Name));
            if (answer != 1)
            {
                yield break;
            }

            for (int index = 0; index < 4; ++index)
            {
                if (index == 3 && !BuildingQueries.HasBuilding(w, mission.City, BuildingIds.Palace))
                {
                    break;
                }

                yield return new(Capture: parent.Caught(mission, call + "/capture/" + index));
                if (lost)
                {
                    yield break;
                }
            }
            yield return new(Explosion: parent.Explode(mission, call + "/nuclear"));
            parent.Identify(mission, call);
        }
        private void Release() { capture?.Dispose(); explosion?.Dispose(); steps.Dispose(); }
        public void Dispose() { disposed = true; Release(); }
    }
    private CivilizationElimination.Operation Caught(CityMissionContext mission, string call) =>
        lifecycle.BeginCaught(mission.Unit, mission.Victim, call, incidents);
    private NuclearEffects.Operation Explode(CityMissionContext mission, string call) =>
        nuclear.Begin(mission.Actor, world.Entities.Cities.At(mission.City).Position, false, call);

    private void Identify(CityMissionContext mission, string call)
    {
        string cityName = world.Entities.Cities.At(mission.City).Name,
            adjective = world.Names[mission.Actor].Adjective, plural = world.Names[mission.Actor].Tribe;
        bool identified = false;
        if (world.Civilizations[mission.Actor].Government != Government.Fundamentalism)
        {
            int odds = world.Entities.Units.Storage[mission.Unit].Flags.Veteran ? 4 : 2;
            if (random.PrimaryModulo(new ChoiceKey("V06.device.identification", call), odds) == 0)
            {
                identified = true;
                for (int other = 1; other < EntityLimits.Civilizations; ++other)
                {
                    if (other == mission.Actor)
                    {
                        continue;
                    }

                    DiplomaticAttitudes.Adjust(world, other, mission.Actor, 100);
                    DiplomaticRelations.Set(world, mission.Actor, other, new() { War = true });
                }
            }
        }
        notice(new(Target(mission.City), cityName, mission.Actor, mission.Victim, adjective, plural, identified));
    }
}
