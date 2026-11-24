using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

public sealed record RevoltOptionText(string Cancel, string Ordinary, string Incident, string Double);
public sealed record RevoltOption(int Value, string Text);
public sealed record RevoltOffer(int Actor, int Victim, CityTarget City, string CityName, int Price, OwnedList<RevoltOption> Options);
public abstract record RevoltDecision
{
    public sealed record Offer(RevoltOffer Request) : RevoltDecision;
    public sealed record Lifecycle(LifecycleDecision Decision) : RevoltDecision;
    public sealed record Capture(CityCaptureDecision Decision) : RevoltDecision;
}
public interface ICityIncitementEffects
{
    void NoRevolt(CityTarget city, string name, int actor);
    void RefreshStatus(int actor);
    void SpySound();
}

/// <summary>V06 §8 local revolt body. The city controller owns admission/fallback dispatch.
/// The required options source reads all four lines, closes its reader and returns null on open failure.</summary>
public sealed class CityIncitement(GameState world, CivilizationElimination lifecycle, EspionageIncidents incidents,
    CityCapture capture, Func<RevoltOptionText?> readOptions, ICityIncitementEffects effects)
{
    private GameState World => world;
    private bool Human(int c) => world.Globals.HumanPlayers.Contains(c);
    private CityTarget Target(CitySlot slot) => new(slot, world.Entities.Cities.At(slot).Token);
    public Operation Begin(CityMissionContext mission, int numericArgument, string call) => new(this, mission, numericArgument, call);

    private int Price(CityMissionContext m, int distance)
    {
        if (world.Civilizations[m.Victim].Government == Government.Communism && distance > 9)
        { distance = 10; }
        if (BuildingQueries.HasBuilding(world, m.City, BuildingIds.Courthouse))
        { distance /= 2; }
        ref var city = ref world.Entities.Cities.At(m.City);
        int cost = unchecked(city.Size * (unchecked(world.Civilizations[m.Victim].Gold + 1000) / unchecked(distance + 3)));
        if (cost < 0)
        { cost = 30000; }
        if (city.Flags.Disorder)
        { cost /= 2; }
        if (world.Map.UnitOwner(city.Position) is null)
        { cost /= 2; }
        if (unchecked((sbyte)city.Founder.Value) == m.Actor)
        { cost /= 2; }
        if (m.IsSpy)
        { cost -= cost / (world.Entities.Units.Storage[m.Unit].Flags.Veteran ? 3 : 6); }
        return cost;
    }

    public sealed class Operation : IDisposable
    {
        private readonly CityIncitement parent;
        private readonly IEnumerator<RevoltDecision?> steps;
        private ResearchReply? reply;
        private bool faulted, disposed;
        public bool Completed { get; private set; }
        public CityMission? Fallback { get; internal set; }
        public RevoltDecision? Pending { get; private set; }
        internal Operation(CityIncitement parent, CityMissionContext mission, int numericArgument, string call)
        {
            ArgumentException.ThrowIfNullOrEmpty(call);
            this.parent = parent;
            steps = parent.Execute(this, mission, numericArgument, call).GetEnumerator();
        }
        internal ResearchReply? TakeReply() { var answer = reply; reply = null; return answer; }
        /// <summary>Null without completion means bounded nested lifecycle/capture work remains.</summary>
        public RevoltDecision? Resume(ResearchReply? response = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("A failed revolt mission cannot resume"); }
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

    private IEnumerable<RevoltDecision?> Execute(Operation op, CityMissionContext m, int number, string call)
    {
        if (world.Civilizations[m.Victim].Government == Government.Democracy)
        {
            if (Human(m.Actor) && !world.Multiplayer.Saved.AutoplayActive)
            { effects.NoRevolt(Target(m.City), world.Entities.Cities.At(m.City).Name, m.Actor); }
            yield break;
        }
        int distance = EspionageCosts.DistanceToCapital(world, m.Victim, world.Entities.Units.Storage[m.Unit].Position);
        if (distance < 2)
        { if (!Human(m.Actor)) { op.Fallback = CityMission.Steal; } yield break; }
        int cost = Price(m, distance);
        var relation = world.Civilizations[m.Actor].Treaties[m.Victim];
        bool treaty = relation.Ceasefire || relation.Peace || relation.Alliance;
        int doubleCost = unchecked(cost * 2);
        if (!Human(m.Actor) && (doubleCost < 0 || doubleCost > world.Civilizations[m.Actor].Gold || treaty))
        { op.Fallback = CityMission.Steal; yield break; }
        int choice = 1;
        if (Human(m.Actor) && !world.Multiplayer.Saved.AutoplayActive)
        {
            string name = world.Entities.Cities.At(m.City).Name;
            number = cost;
            List<RevoltOption> options = [];
            if (cost <= world.Civilizations[m.Actor].Gold)
            {
                var text = readOptions();
                if (text is null)
                { yield break; }
                options.Add(new(0, text.Cancel));
                options.Add(new(1, treaty ? text.Incident : text.Ordinary));
                if (treaty && unchecked(cost * 2) <= world.Civilizations[m.Actor].Gold && cost < 15001)
                { options.Add(new(2, text.Double)); }
            }
            yield return new RevoltDecision.Offer(new(m.Actor, m.Victim, Target(m.City), name, cost, new(options)));
            choice = op.TakeReply()!.Value.Selected;
        }
        if (choice <= 0)
        { yield break; }
        world.Civilizations[m.Actor].Gold = unchecked(world.Civilizations[m.Actor].Gold - unchecked(cost * choice));
        world.Civilizations[m.Actor].SubversionCount = unchecked((short)(world.Civilizations[m.Actor].SubversionCount + 2));
        if (world.Multiplayer.LocalHuman == m.Actor)
        { effects.RefreshStatus(m.Actor); }
        using (var escape = lifecycle.BeginEscape(m.Unit, 0, 0, call + "/escape", incidents))
        {
            while (escape.Result is null)
            {
                if (escape.ResumeDecision(op.TakeReply()) is { } incident)
                { yield return new RevoltDecision.Lifecycle(incident); }
                else if (escape.Result is null)
                { yield return null; }
            }
        }
        if (choice == 1)
        {
            var incident = incidents.Begin(m.Actor, m.Victim, call + "/incident");
            while (!incident.Completed)
            {
                if (incident.Resume(op.TakeReply()?.Selected) is { } pretext)
                { yield return new RevoltDecision.Lifecycle(pretext); }
            }
        }
        else if (choice == 2)
        {
            world.Civilizations[m.Victim].Treaties[m.Actor].Vendetta = true;
            world.Entities.Cities.At(m.City).Founder = new((byte)m.Actor);
        }
        if (world.Multiplayer.LocalHuman == m.Actor || world.Multiplayer.LocalHuman == m.Victim)
        { effects.SpySound(); }
        using var transfer = capture.BeginIncited(m.City, m.Actor, choice, number, call + "/transfer");
        while (!transfer.Completed)
        {
            if (transfer.Resume(op.TakeReply()) is { } pending)
            { yield return new RevoltDecision.Capture(pending); }
            else if (!transfer.Completed)
            { yield return null; }
        }
    }
}
