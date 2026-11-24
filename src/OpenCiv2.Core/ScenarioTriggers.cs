using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

/// <summary>S02 §7 ordinary local trigger scans. Callers own Loaded, Music and phase gates;
/// actual event actions retain decisions before the next live record is inspected.</summary>
public sealed class ScenarioTriggers(GameState world, GameplayRandom random, Func<ScenarioDispatcher> dispatcher)
{
    public const int CityNameField = 1;
    private GameState World => world;
    public Operation BeginTurn(int turn, string call) => new(this, EventTrigger.Turn, turn, 0, 0, null, call);
    public Operation BeginInterval(int turn, string call) => new(this, EventTrigger.TurnInterval, turn, 0, 0, null, call);
    public Operation BeginRandom(string call) => new(this, EventTrigger.RandomTurn, 0, 0, 0, null, call);
    public Operation BeginLoaded(string call) => new(this, EventTrigger.ScenarioLoaded, 0, 0, 0, null, call);
    public Operation BeginTechnology(string call) => new(this, EventTrigger.ReceivedTechnology, 0, 0, 0, null, call);
    public Operation BeginKilled(CombatDeath death, string call) => new(this, EventTrigger.UnitKilled,
        death.Type.Value, death.Winner, death.Loser, null, call);
    public Operation BeginTaken(CitySlot city, int newOwner, int oldOwner, string call) =>
        BeginTaken(() => world.Entities.Cities.At(city).Name, newOwner, oldOwner, call);
    public Operation BeginTaken(Func<string> liveName, int newOwner, int oldOwner, string call)
    {
        ArgumentNullException.ThrowIfNull(liveName);
        return new(this, EventTrigger.CityTaken, 0, newOwner, oldOwner, liveName, call);
    }
    public sealed class Operation : IDisposable
    {
        private readonly ScenarioTriggers parent;
        private readonly IEnumerator<LifecycleDecision?> steps;
        private readonly List<Exception> failures = [];
        private ResearchReply? reply;
        private bool disposed, faulted;
        public int? Result { get; internal set; }
        public LifecycleDecision? Pending { get; private set; }
        public IReadOnlyList<Exception> PresentationFailures => failures.AsReadOnly();
        internal Operation(ScenarioTriggers parent, EventTrigger trigger, int value, int who, int whom, Func<string>? name, string call)
        {
            ArgumentException.ThrowIfNullOrEmpty(call);
            this.parent = parent;
            steps = parent.Execute(this, trigger, value, who, whom, name, call).GetEnumerator();
        }
        internal ResearchReply? TakeReply() { var value = reply; reply = null; return value; }
        internal void Report(IEnumerable<Exception> errors) => failures.AddRange(errors);
        public LifecycleDecision? Resume(ResearchReply? answer = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("A failed scenario trigger cannot resume"); }
            if (Result is not null)
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
                steps.Dispose();
                return null;
            }
            catch { faulted = true; steps.Dispose(); throw; }
        }
        public void Dispose() { disposed = true; steps.Dispose(); }
    }
    private IEnumerable<LifecycleDecision?> Execute(Operation op, EventTrigger trigger, int value, int who, int whom, Func<string>? name, string call)
    {
        bool matched = false;
        var events = world.Events.Collection;
        for (int i = 0; i < events.Count; ++i)
        {
            var entry = events.At(i);
            if (entry.Values.Trigger != trigger)
            { continue; }
            // Capture the wildcard branch once; action mutations do not end the first polling scan.
            bool wildcard = trigger == EventTrigger.ReceivedTechnology && entry.Values.WhomCiv == -2;
            for (int civilization = 0; civilization < (wildcard ? EntityLimits.Civilizations : 1); ++civilization)
            {
                bool hit;
                if (trigger == EventTrigger.ReceivedTechnology)
                {
                    int receiver = wildcard ? civilization : entry.Values.WhomCiv;
                    hit = world.Globals.CivsAlive.Contains(receiver & 31)
                        && CivilizationKnowledge.KnowsAdvance(world.Civilizations, receiver, entry.Values.Technology);
                    if (hit && wildcard)
                    { entry.Values.WhomCiv = receiver; }
                }
                else
                { hit = Matches(entry, trigger, value, who, whom, name, call); }
                if (!hit)
                { continue; }
                matched = true;
                string childCall = call + "/event/" + entry.Identity + (wildcard ? "/receiver/" + civilization : "");
                using var child = dispatcher().Begin(entry, childCall);
                while (child.Result is null)
                {
                    var pending = child.Resume(op.TakeReply());
                    if (child.Result is null)
                    { yield return pending; }
                }
                op.Report(child.PresentationFailures);
                if (!events.Contains(entry))
                { throw new InvalidOperationException("Scenario action invalidated the current event"); }
            }
        }
        op.Result = matched ? 1 : 0;
    }
    private bool Matches(Event entry, EventTrigger trigger, int value, int who, int whom, Func<string>? name, string call)
    {
        switch (trigger)
        {
            case EventTrigger.Turn:
                return entry.Values.Turn == -1 || entry.Values.Turn == value;
            case EventTrigger.TurnInterval:
                return entry.Values.Turn == 0 || Arithmetic.Remainder32(value, entry.Values.Turn) == 0;
            case EventTrigger.RandomTurn:
                int roll = 1;
                if (entry.Values.Denominator > 1)
                {
                    int code = random.PrimaryCode(new ChoiceKey("S02.random-turn", call + "/event/" + entry.Identity));
                    roll = unchecked(Arithmetic.Remainder32(code, entry.Values.Denominator) + 1);
                }
                return entry.Values.Denominator == roll;
            case EventTrigger.ScenarioLoaded:
                return true;
            case EventTrigger.UnitKilled:
                if (entry.Values.UnitType != value || !Selector(entry.Values.WhomCiv, whom) || !Selector(entry.Values.WhoCiv, who))
                { return false; }
                break;
            case EventTrigger.CityTaken:
                if (!string.Equals(entry.Strings[CityNameField].Value, name!(), StringComparison.OrdinalIgnoreCase)
                    || !Selector(entry.Values.WhoCiv, who) || !Selector(entry.Values.WhomCiv, whom))
                { return false; }
                break;
            default:
                throw new InvalidOperationException("Unknown ordinary scenario trigger");
        }
        if (entry.Values.WhoCiv == -2)
        { entry.Values.WhoCiv = who; }
        if (entry.Values.WhomCiv == -2)
        { entry.Values.WhomCiv = whom; }
        return true;
    }
    private static bool Selector(int stored, int actual) => stored == actual || stored == -2;
}
