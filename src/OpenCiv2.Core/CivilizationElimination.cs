using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

public readonly record struct CivilizationDestroyedNotice(int Civilization, int Killer,
    string Adjective, string KillerName, byte Portrait);

public interface ICivilizationEliminationEffects
{
    void Destroyed(CivilizationDestroyedNotice notice);
    void SpaceshipLost(SpaceshipLossNotice notice);
    void RedrawMaps();
}

/// <summary>V01 §6 local last-city check, using real U01 deletion, V06 escape, V07 loss and V01 recreation.
/// Forced retirement and civil war are separate operations. Network request orchestration belongs to the session.</summary>
public sealed class CivilizationElimination(GameState world, UnitDeletion deletion, SpyEscape escape,
    Func<int, string, CivilizationCreation> recreate, ICivilizationEliminationEffects effects)
{
    public CivilizationElimination(GameState world, UnitDeletion deletion, SpyEscape escape,
        Func<int, CivilizationCreation> recreate, ICivilizationEliminationEffects effects)
        : this(world, deletion, escape, (owner, _) => recreate(owner), effects) { }

    public int Check(int civilization, int killer, string call)
    {
        int result = 0;
        foreach (var work in Steps(civilization, killer, call, value => result = value))
        {
            if (work.Check is { } check)
            {
                if (work.EscapeLoss)
                { escape.CompleteCheck(check); }
                else
                { deletion.CompleteCheck(check); }
            }
            else if (work.Incident is { } incident)
            { escape.CompleteIncident(incident); }
            else if (work.Creation is { } creation)
            {
                while (creation.Advance(256) is null)
                {
                    if (creation.Pending is not null)
                    { throw new InvalidOperationException("A retained catch-up decision requires the lifecycle continuation"); }
                }
            }
        }
        return result;
    }

    /// <summary>Resumable local lifecycle with actual nested destruction checks and espionage incidents.
    /// The session serializes mutations, routes pretext decisions and retains this operation until complete.</summary>
    public Operation Begin(int civilization, int killer, string call, EspionageIncidents incidents, int candidateBudget = 64)
        => Operation.ForCheck(this, civilization, killer, call, incidents, candidateBudget);

    /// <summary>V06 escape with actual nested V01 checks and incidents. Result is 1 on loss, 0 on survival.</summary>
    public Operation BeginEscape(int unit, int mode, int victim, string call, EspionageIncidents incidents, int candidateBudget = 64)
        => Operation.ForEscape(this, unit, mode, victim, call, incidents, candidateBudget);

    /// <summary>V06 capture check, including its captured-type notice after all nested loss checks.</summary>
    public Operation BeginCaught(int unit, int victim, string call, EspionageIncidents incidents, int candidateBudget = 64)
        => Operation.ForEscape(this, unit, -1, victim, call, incidents, candidateBudget, true);

    /// <summary>U01 basic deletion with actual nested civilization checks. Result is 1 when finished.</summary>
    public Operation BeginDelete(int unit, bool notify, string call, EspionageIncidents incidents, int candidateBudget = 64)
        => Operation.ForDeletion(this, null, unit, notify, call, incidents, candidateBudget);

    /// <summary>U01 cargo-aware destruction/redraw with actual nested civilization checks.</summary>
    public Operation BeginDestroy(UnitLoss loss, int unit, string call, EspionageIncidents incidents, int candidateBudget = 64)
    {
        ArgumentNullException.ThrowIfNull(loss);
        return Operation.ForDeletion(this, loss, unit, true, call, incidents, candidateBudget);
    }

    /// <summary>U01 whole-stack deletion with actual nested civilization checks and final synchronization.</summary>
    public Operation BeginEraseStack(UnitLoss loss, int unit, string call, EspionageIncidents incidents, int candidateBudget = 64)
    {
        ArgumentNullException.ThrowIfNull(loss);
        return Operation.ForStack(this, loss, unit, call, incidents, candidateBudget);
    }

    /// <summary>U01 cargo-aware deletion without the destroy/redraw wrapper, including real nested checks.</summary>
    public Operation BeginEraseCargo(UnitLoss loss, int unit, string call, EspionageIncidents incidents, int candidateBudget = 64)
    {
        ArgumentNullException.ThrowIfNull(loss);
        return Operation.ForStack(this, loss, unit, call, incidents, candidateBudget, true);
    }

    /// <summary>C01 city deletion, including supported-unit loss and its actual nested civilization checks.</summary>
    public Operation BeginRemoveCity(CityRemoval removal, int city, string call, EspionageIncidents incidents, int candidateBudget = 64)
        => Operation.ForCityLoss(this, removal, city, null, call, incidents, candidateBudget);

    /// <summary>C09 old-owner supported-unit loss only. Production and city-window continuation belong to capture.</summary>
    public Operation BeginSupportedLoss(CityRemoval removal, int city, int oldOwner, string call, EspionageIncidents incidents, int candidateBudget = 64)
        => Operation.ForCityLoss(this, removal, city, oldOwner, call, incidents, candidateBudget);

    private readonly record struct Work(string Call, CivilizationCheck? Check = null, SpyIncidentRequest? Incident = null, bool EscapeLoss = false, CivilizationCreation? Creation = null);
    private void RequireLocal() => MapKnowledge.RequireLocal(world);

    public sealed class Operation : IDisposable
    {
        private readonly CivilizationElimination parent;
        private readonly EspionageIncidents incidents;
        private readonly int candidateBudget;
        private readonly bool barbarian;
        private readonly Stack<IEnumerator<Work>> frames = new();
        private EspionageIncidents.Operation? incident;
        private CivilizationCreation? creation;
        private bool faulted, disposed;
        private readonly List<Exception> presentationFailures = [];
        public IReadOnlyList<Exception> PresentationFailures => presentationFailures.AsReadOnly();
        public int? Result { get; private set; }

        private Operation(CivilizationElimination parent, EspionageIncidents incidents, int candidateBudget,
            bool barbarian, Func<Action<int>, IEnumerable<Work>> steps)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(candidateBudget);
            ArgumentNullException.ThrowIfNull(incidents);
            this.parent = parent;
            this.incidents = incidents;
            this.candidateBudget = candidateBudget;
            this.barbarian = barbarian;
            frames.Push(steps(value => Result = value).GetEnumerator());
        }

        internal static Operation ForCheck(CivilizationElimination parent, int civilization, int killer, string call,
            EspionageIncidents incidents, int candidateBudget) => new(parent, incidents, candidateBudget, civilization == 0,
                complete => parent.Steps(civilization, killer, call, complete));

        internal static Operation ForEscape(CivilizationElimination parent, int unit, int mode, int victim, string call,
            EspionageIncidents incidents, int candidateBudget, bool caught = false) => new(parent, incidents, candidateBudget, false,
                complete => parent.EscapeSteps(unit, mode, victim, call, complete, caught));

        internal static Operation ForDeletion(CivilizationElimination parent, UnitLoss? loss, int unit, bool notify, string call,
            EspionageIncidents incidents, int candidateBudget) => new(parent, incidents, candidateBudget, false,
                complete => parent.DeleteSteps(loss, unit, notify, call, complete));

        internal static Operation ForStack(CivilizationElimination parent, UnitLoss loss, int unit, string call,
            EspionageIncidents incidents, int candidateBudget, bool cargo = false) => new(parent, incidents, candidateBudget, false,
                complete => parent.StackSteps(loss, unit, call, complete, cargo));

        internal static Operation ForCityLoss(CivilizationElimination parent, CityRemoval removal, int city, int? oldOwner,
            string call, EspionageIncidents incidents, int candidateBudget) => new(parent, incidents, candidateBudget, false,
                complete => CityLossSteps(removal, city, oldOwner, call, complete));

        /// <summary>Returns a pending human decision, or null after completion / a bounded search slice.
        /// Result remains null during search. A reply is consumed only by the currently pending incident or catch-up grant.</summary>
        public LifecycleDecision? Resume(int? reply = null) => ResumeDecision(reply is { } value ? new(value) : null);
        public LifecycleDecision? ResumeDecision(ResearchReply? reply = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("A failed lifecycle operation cannot resume"); }
            if (Result is not null)
            { return null; }
            try
            {
                if (!barbarian)
                { parent.RequireLocal(); }
                while (true)
                {
                    if (creation is not null)
                    {
                        var result = creation.AdvanceDecision(candidateBudget, reply);
                        reply = null;
                        if (result is null)
                        { return creation.Pending; }
                        presentationFailures.AddRange(creation.PresentationFailures);
                        creation = null; // The suspended frame owns disposal.
                    }
                    if (incident is not null)
                    {
                        var decision = incident.Resume(reply?.Selected);
                        reply = null;
                        if (decision is not null)
                        { return decision; }
                        incident = null;
                    }
                    if (frames.Count == 0)
                    { return null; }
                    var frame = frames.Peek();
                    if (!frame.MoveNext())
                    { frames.Pop().Dispose(); continue; }
                    var work = frame.Current;
                    if (work.Check is { } check)
                    {
                        frames.Push(parent.Steps(check.Owner, check.Killer, work.Call, _ => { }).GetEnumerator());
                    }
                    else if (work.Creation is { } child)
                    { creation = child; reply = null; }
                    else if (work.Incident is { } request)
                    {
                        incident = incidents.Begin(request.Actor, request.Victim, work.Call);
                        reply = null; // A reply to an earlier incident never accepts the next prompt.
                    }
                    else
                    { throw new InvalidOperationException("Lifecycle work requires a child operation"); }
                }
            }
            catch { faulted = true; Release(); throw; }
        }

        private void Release()
        {
            while (frames.TryPop(out var frame))
            { frame.Dispose(); }
        }
        public void Dispose() { disposed = true; Release(); }
    }

    private IEnumerable<Work> DeleteSteps(UnitLoss? loss, int unit, bool notify, string call, Action<int> complete)
    {
        ArgumentException.ThrowIfNullOrEmpty(call);
        var checks = loss is null ? deletion.EraseSteps(unit, notify) : loss.DestroyAndRedrawSteps(unit, true);
        foreach (var check in checks)
        { yield return new(call + "/unit/" + check.Unit + "/check", check); }
        complete(1);
    }

    private IEnumerable<Work> StackSteps(UnitLoss loss, int unit, string call, Action<int> complete, bool cargo)
    {
        ArgumentException.ThrowIfNullOrEmpty(call);
        foreach (var check in cargo ? loss.EraseWithCargoSteps(unit, true) : loss.EraseStackSteps(unit, true))
        { yield return new(call + "/unit/" + check.Unit + "/check", check); }
        complete(1);
    }

    private static IEnumerable<Work> CityLossSteps(CityRemoval removal, int city, int? oldOwner, string call, Action<int> complete)
    {
        ArgumentNullException.ThrowIfNull(removal);
        ArgumentException.ThrowIfNullOrEmpty(call);
        var steps = oldOwner is { } owner ? removal.SupportedUnitSteps(city, owner) : removal.DeleteSteps(city);
        foreach (var check in steps)
        { yield return new(call + "/unit/" + check.Unit + "/check", check); }
        complete(1);
    }

    private IEnumerable<Work> EscapeSteps(int unit, int mode, int victim, string call, Action<int> complete, bool caught = false)
    {
        var resolution = caught ? escape.BeginCaught(unit, victim, call) : escape.BeginResolve(unit, mode, victim, call);
        foreach (var check in resolution.Steps())
        { yield return new(call + "/unit/" + check.Unit + "/check", check, EscapeLoss: true); }
        if (resolution.Result!.Incident is { } incident)
        { yield return new(call + "/incident", Incident: incident); }
        complete(resolution.Result.Lost ? 1 : 0);
    }

    private IEnumerable<Work> Steps(int civilization, int killer, string call, Action<int> complete)
    {
        if (civilization == 0)
        {
            complete(0);
            yield break;
        }

        MapKnowledge.RequireLocal(world);
        ArgumentOutOfRangeException.ThrowIfNegative(civilization);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(civilization, EntityLimits.Civilizations);
        ArgumentException.ThrowIfNullOrEmpty(call);
        for (int i = 0; i < world.Entities.Cities.End; ++i)
        {
            ref var city = ref world.Entities.Cities.Storage[i];
            if (city.Token.Occupied && unchecked((sbyte)city.Owner.Value) == civilization)
            {
                complete(0);
                yield break;
            }
        }
        if (killer == -1)
        {
            SpaceshipLifecycle.Reset(world, civilization);
        }
        else
        {
            RecordDestruction(civilization, killer);
        }
        int bound = world.Entities.Units.End;
        for (int i = bound - 1; i >= 0; --i)
        {
            ref var unit = ref world.Entities.Units.Storage[i];
            if (!unit.Token.Occupied)
            {
                continue;
            }

            if (unchecked((sbyte)unit.Owner.Value) == civilization)
            {
                foreach (var check in deletion.EraseSteps(i, true))
                { yield return new(call + "/unit/" + check.Unit + "/check", check); }
            }
            else if (unit.Flags.SpyMission && unchecked((sbyte)unit.Counter) == civilization)
            {
                unit.Flags.SpyMission = false;
                world.Civilizations[civilization].Treaties[unchecked((sbyte)unit.Counter)].SpyChat = false;
                string escapeCall = call + "/spy/" + i;
                foreach (var work in EscapeSteps(i, 0, unchecked((sbyte)unit.Counter), escapeCall, _ => { }))
                { yield return work; }
            }
        }
        if (world.Globals.HumanPlayers.Contains(civilization))
        {
            if (civilization == world.Multiplayer.LocalHuman)
            {
                world.PendingEndReason = EndReason.Defeat;
            }

            var passwords = world.Multiplayer.Saved.Passwords.ToArray();
            passwords[civilization] = string.Empty;
            world.Multiplayer.Saved.Passwords = new(passwords);
            if (world.Globals.HumanPlayers.With(civilization, false).Count == 0)
            {
                complete(0);
                yield break;
            }
        }
        var formerOwner = CivilizationSet.Only(civilization);
        for (int x = 0; x < world.Map.Geometry.Width; ++x)
        {
            for (int y = 0; y < world.Map.Geometry.Height; ++y)
            {
                world.Map.Mutate(new TileChange.Revelation(new(x, y), formerOwner, false));
            }
        }
        if (world.Globals.Multiplayer == GameMode.Hotseat)
        {
            world.Globals.HumanPlayers = world.Globals.HumanPlayers.With(civilization, false);
        }
        using var creation = recreate(civilization, call + "/creation");
        yield return new(call + "/creation", Creation: creation);
        if (world.Globals.Multiplayer != GameMode.Hotseat || world.Globals.CivsAlive.Contains(world.Multiplayer.LocalHuman))
        {
            effects.RedrawMaps();
        }
        complete(1);
    }

    private void RecordDestruction(int civilization, int killer)
    {
        if (!world.Multiplayer.Saved.AutoplayActive)
        {
            effects.Destroyed(new(civilization, killer, world.Names[civilization].Adjective,
                world.Names[killer].Tribe, world.Civilizations[civilization].LeaderPortrait));
        }
        ref var history = ref world.Eliminations;
        if (history.Count < EliminationHistory.Capacity)
        {
            int index = history.Count++;
            history.Turns[index] = world.Globals.Turn;
            history.Killers[index] = unchecked((byte)killer);
            history.Portraits[index] = world.Civilizations[civilization].LeaderPortrait;
            var names = history.Names.ToArray();
            names[index] = world.Names[civilization].Tribe;
            history.Names = new(names);
        }
        SpaceshipLifecycle.Lost(world, civilization, effects.SpaceshipLost);
    }
}
