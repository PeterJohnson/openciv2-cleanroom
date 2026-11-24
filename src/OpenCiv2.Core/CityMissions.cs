using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

public sealed record CityMissionText(string Embassy, string Investigate, string Steal, string Sabotage,
    string Poison, string PlantNuclear, string Incite, string SpyAnnotation);
public sealed record CityMissionOption(CityMission Mission, string Text);
public sealed record CityMissionMenu(int Actor, CityTarget City, string ActorAdjective, string UnitName,
    string CityName, OwnedList<CityMissionOption> Options);
public abstract record CityMissionDecision
{
    public sealed record Menu(CityMissionMenu Request) : CityMissionDecision;
    public sealed record Mission(CityEspionageDecision Decision) : CityMissionDecision;
    public sealed record Revolt(RevoltDecision Decision) : CityMissionDecision;
}
public sealed record CityMissionServices(CityEspionageVisits Visits, CityTechnologyTheft Theft,
    CityEspionageDamage Damage, PlantedNuclearDevice Device, CityIncitement Incitement);

/// <summary>V06 §3 local city entry and fallback loop. U02 owns movement admission.
/// All ordinary mission bodies are real services; network chat interception is outside this local entry.</summary>
public sealed class CityMissions(GameState world, RuleTables rules, GameplayRandom random,
    CityMissionServices services, Func<CityMissionText?> readOptions, Action stopBlinking)
{
    private GameState World => world;
    public Operation Begin(int unit, CitySlot city, int numericArgument, string call) => new(this, unit, city, numericArgument, call);

    public CityMission SelectFallback(CityMissionContext m, string call)
    {
        MapKnowledge.RequireLocal(world);
        var relation = world.Civilizations[m.Actor].Treaties[m.Victim];
        CityMission choice = CityMission.Steal;
        if (relation.Vendetta && random.PrimaryModulo(new ChoiceKey("V06.city-mission.vendetta", call), 3) == 0 && m.IsSpy)
        { choice = CityMission.Poison; }
        if (relation.Peace)
        { choice = CityMission.Steal; }
        if ((world.Globals.Difficulty > Difficulty.King || world.Globals.Difficulty == Difficulty.King && world.Civilizations[m.Victim].SubversionCount > 5)
            && choice == CityMission.Steal && (relation.Vendetta || relation.War) && world.Civilizations[m.Victim].Government != Government.Democracy)
        { choice = CityMission.Incite; }
        if (relation.Alliance)
        { choice = CityMission.Steal; }
        if (world.Entities.Cities.At(m.City).Flags.TechnologyStolen && !m.IsSpy && choice == CityMission.Steal)
        { choice = relation.Ceasefire || relation.Peace || relation.Alliance ? CityMission.Embassy : CityMission.Sabotage; }
        return choice;
    }

    private CityMissionMenu Menu(CityMissionContext m, CityMissionText text)
    {
        List<CityMissionOption> options = [];
        ref var city = ref world.Entities.Cities.At(m.City);
        string annotation = m.IsSpy ? text.SpyAnnotation : "";
        if (!world.Civilizations[world.Multiplayer.LocalHuman].Treaties[m.Victim].Embassy
            && !BuildingQueries.HasWonder(world, rules, world.Multiplayer.LocalHuman, WonderIds.UnitedNations))
        { options.Add(new(CityMission.Embassy, text.Embassy + annotation)); }
        options.Add(new(CityMission.Investigate, text.Investigate + annotation));
        if (!city.Flags.TechnologyStolen || m.IsSpy)
        { options.Add(new(CityMission.Steal, text.Steal)); }
        options.Add(new(CityMission.Sabotage, text.Sabotage));
        if (m.IsSpy && city.Size > 1)
        { options.Add(new(CityMission.Poison, text.Poison)); }
        if (m.IsSpy && CivilizationKnowledge.KnowsAdvance(world.Civilizations, m.Actor, AdvanceIds.Rocketry)
            && CivilizationKnowledge.KnowsAdvance(world.Civilizations, m.Actor, AdvanceIds.NuclearFission)
            && BuildingQueries.ActiveCity(world, rules, WonderIds.Manhattan) != BuildingQueries.Unbuilt)
        { options.Add(new(CityMission.PlantNuclear, text.PlantNuclear)); }
        if (!BuildingQueries.HasBuilding(world, m.City, BuildingIds.Palace))
        { options.Add(new(CityMission.Incite, text.Incite)); }
        return new(m.Actor, new(m.City, city.Token), world.Names[m.Actor].Adjective,
            rules.Units[world.Entities.Units.Storage[m.Unit].Type.Value].Identity.Name, city.Name, new(options));
    }

    public sealed class Operation : IDisposable
    {
        private readonly CityMissions parent;
        private readonly IEnumerator<CityMissionDecision?> steps;
        private ResearchReply? reply;
        private bool faulted, disposed;
        public bool Completed { get; private set; }
        public CityMissionDecision? Pending { get; private set; }
        internal Operation(CityMissions parent, int unit, CitySlot city, int number, string call)
        {
            ArgumentException.ThrowIfNullOrEmpty(call);
            this.parent = parent;
            steps = parent.Execute(this, unit, city, number, call).GetEnumerator();
        }
        internal ResearchReply? TakeReply() { var answer = reply; reply = null; return answer; }
        /// <summary>Null without completion is a bounded child or fallback slice.</summary>
        public CityMissionDecision? Resume(ResearchReply? response = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("A failed city mission cannot resume"); }
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

    private IEnumerable<CityMissionDecision?> Execute(Operation op, int unit, CitySlot city, int number, string call)
    {
        stopBlinking();
        int actor = world.Entities.Units.Storage[unit].Owner.Value, victim = world.Entities.Cities.At(city).Owner.Value;
        var type = world.Entities.Units.Storage[unit].Type;
        DiplomaticRelations.Set(world, actor, victim, new() { Contact = true });
        var m = new CityMissionContext(unit, city, actor, victim, type, world.Entities.Units.Storage[unit].Type.Value == UnitTypeIds.Spy);
        CityMission choice;
        if (!world.Multiplayer.Saved.AutoplayActive && world.Globals.HumanPlayers.Contains(actor) && world.Multiplayer.LocalHuman == actor)
        {
            var text = readOptions();
            if (text is null)
            { yield break; }
            yield return new CityMissionDecision.Menu(Menu(m, text));
            int selected = op.TakeReply()!.Value.Selected;
            if (selected < 0)
            { yield break; }
            choice = (CityMission)selected;
        }
        else
        { choice = SelectFallback(m, call); }
        while (true)
        {
            world.Espionage.LastMission = choice;
            CityMission? fallback = null;
            string childCall = call + "/mission/" + (int)choice;
            switch (choice)
            {
                case CityMission.Embassy:
                case CityMission.Investigate:
                    using (var child = choice == CityMission.Embassy ? services.Visits.Embassy(m, childCall) : services.Visits.Investigate(m, childCall))
                    {
                        while (!child.Completed)
                        {
                            if (child.ResumeDecision(op.TakeReply()) is { } pending)
                            { yield return new CityMissionDecision.Mission(pending); }
                            else if (!child.Completed)
                            { yield return null; }
                        }
                        fallback = child.Fallback;
                    }
                    break;
                case CityMission.Steal:
                    using (var child = services.Theft.Begin(m, childCall))
                    {
                        while (!child.Completed)
                        {
                            if (child.Resume(op.TakeReply()) is { } pending)
                            { yield return new CityMissionDecision.Mission(pending); }
                            else if (!child.Completed)
                            { yield return null; }
                        }
                        fallback = child.Fallback;
                    }
                    break;
                case CityMission.Sabotage:
                case CityMission.Poison:
                    using (var child = choice == CityMission.Sabotage ? services.Damage.Sabotage(m, childCall) : services.Damage.Poison(m, childCall))
                    {
                        while (!child.Completed)
                        {
                            if (child.ResumeDecision(op.TakeReply()) is { } pending)
                            { yield return new CityMissionDecision.Mission(pending); }
                            else if (!child.Completed)
                            { yield return null; }
                        }
                    }
                    break;
                case CityMission.PlantNuclear:
                    using (var child = services.Device.Begin(m, childCall))
                    {
                        while (!child.Completed)
                        {
                            if (child.ResumeDecision(op.TakeReply()) is { } pending)
                            { yield return new CityMissionDecision.Mission(pending); }
                            else if (!child.Completed)
                            { yield return null; }
                        }
                    }
                    break;
                case CityMission.Incite:
                    using (var child = services.Incitement.Begin(m, number, childCall))
                    {
                        while (!child.Completed)
                        {
                            if (child.Resume(op.TakeReply()) is { } pending)
                            { yield return new CityMissionDecision.Revolt(pending); }
                            else if (!child.Completed)
                            { yield return null; }
                        }
                        fallback = child.Fallback;
                    }
                    break;
                case CityMission.Chat:
                    throw new NotSupportedException("Chat interception requires the deferred network mission service");
            }
            if (fallback is null)
            { yield break; }
            choice = fallback.Value;
            yield return null;
        }
    }
}
