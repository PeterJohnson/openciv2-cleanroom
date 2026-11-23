using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

public sealed record EmissarySpeaker(int Civilization, string Leader, string Title, string Plural, string Adjective, bool Female);
public sealed record EmissaryOffer(EmissarySpeaker Actor, EmissarySpeaker Counterpart, int AttitudeCategory, bool AllowIntelligence);
public sealed record EmissaryIntelligence(int Observer, int Counterpart);
public enum EmissaryNoticeKind { ForcedReception, Greeting, YourNuclearWeapons }
public sealed record EmissaryNotice(EmissaryNoticeKind Kind, EmissarySpeaker Actor, EmissarySpeaker Counterpart,
    int AttitudeCategory, int Variant, bool Vendetta, bool NuclearWeapons);
public abstract record EmissaryDecision
{
    public sealed record Receive(EmissaryOffer Offer) : EmissaryDecision;
    public sealed record Intelligence(EmissaryIntelligence Visit) : EmissaryDecision;
}
/// <summary>Semantic presentation; an intelligence report has a separate resumable decision.</summary>
public interface IEmissaryEffects
{
    void Notice(EmissaryNotice notice);
    void OpenLeader(int counterpart);
    void CloseLeader();
    void EnsureVisible(MapPoint point, int observer);
}

/// <summary>V05 §2 local emissary admission, real A08 evaluation/C08 intelligence gates,
/// greeting and shared meeting reset. No full audience controller or technology exchange.</summary>
public sealed class EmissaryReception(GameState world, RuleTables rules, GameplayRandom random,
    DiplomaticStance stance, IEmissaryEffects effects)
{
    private GameState World => world;
    private DiplomacyWorkspace Meeting => world.Diplomacy;
    private bool Nuclear(int owner) => world.Civilizations[owner].UnitsActive[UnitTypeIds.NuclearMissile] != 0;
    private EmissarySpeaker Speaker(int owner) => new(owner, world.Names[owner].Leader,
        world.Names[owner].Titles[(int)world.Civilizations[owner].Government], world.Names[owner].Tribe,
        world.Names[owner].Adjective, world.Civilizations[owner].Flags.FemaleLeader);
    private void Notice(EmissaryNoticeKind kind, int actor, int counterpart, int variant = 0, bool nuclear = false) =>
        effects.Notice(new(kind, Speaker(actor), Speaker(counterpart), DiplomaticAttitudes.Category(Meeting.Attitude),
            variant, world.Civilizations[counterpart].Treaties[actor].Vendetta, nuclear));
    public Operation Begin(int actor, int counterpart, MapPoint? focus, string call) => new(this, actor, counterpart, focus, call);

    /// <summary>Publishes required greeting information; returns a cosmetic leader-window failure, if any.</summary>
    public Exception? Greet(int actor, int counterpart, string call)
    {
        MapKnowledge.RequireLocal(world);
        ArgumentException.ThrowIfNullOrEmpty(call);
        Meeting.MeetingActive = true;
        Exception? presentationFailure = null;
        if (world.Globals.GameOptions.Diplomacy)
        { try { effects.OpenLeader(counterpart); } catch (Exception error) { presentationFailure = error; } }
        int variant = random.SecondaryRange(new ChoiceKey("V05.emissary-greeting", call), 0, 3);
        Notice(EmissaryNoticeKind.Greeting, actor, counterpart, variant, Nuclear(counterpart));
        var relation = world.Civilizations[actor].Treaties[counterpart];
        if (Nuclear(actor) && (relation.Peace || !relation.NuclearThreat))
        {
            Notice(EmissaryNoticeKind.YourNuclearWeapons, actor, counterpart, Nuclear(counterpart) ? 2 : 1);
            world.Civilizations[actor].Treaties[counterpart].NuclearThreat = true;
        }
        return presentationFailure;
    }
    /// <summary>Clears owned meeting state even if closing the cosmetic leader window fails.</summary>
    public Exception? Reset()
    {
        MapKnowledge.RequireLocal(world);
        Meeting.MeetingActive = Meeting.AskedEmissary = Meeting.InitialEvaluationDone = false;
        Meeting.EmissaryResponse = 0;
        try
        { effects.CloseLeader(); return null; }
        catch (Exception error) { return error; }
    }

    public sealed class Operation : IDisposable
    {
        private readonly EmissaryReception parent;
        private readonly IEnumerator<EmissaryDecision> steps;
        private int? reply;
        private bool faulted, disposed;
        private readonly List<Exception> presentationFailures = [];
        public bool? Accepted { get; internal set; }
        public EmissaryDecision? Pending { get; private set; }
        public IReadOnlyList<Exception> PresentationFailures => presentationFailures.AsReadOnly();
        internal Operation(EmissaryReception parent, int actor, int counterpart, MapPoint? focus, string call)
        {
            ArgumentException.ThrowIfNullOrEmpty(call);
            this.parent = parent;
            steps = parent.Ask(this, actor, counterpart, focus, call).GetEnumerator();
        }
        internal int TakeReply() { int value = reply!.Value; reply = null; return value; }
        internal void Report(Exception? error) { if (error is not null) { presentationFailures.Add(error); } }
        internal void Present(Action action) { try { action(); } catch (Exception error) { Report(error); } }
        public EmissaryDecision? Resume(int? answer = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("A failed emissary reception cannot resume"); }
            if (Accepted is not null)
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

    private IEnumerable<EmissaryDecision> Ask(Operation operation, int actor, int counterpart, MapPoint? focus, string call)
    {
        if (world.Multiplayer.Saved.AutoplayActive)
        { Meeting.EmissaryResponse = -1; operation.Accepted = false; yield break; }
        if (Meeting.AskedEmissary)
        { operation.Accepted = Meeting.EmissaryResponse != -1; yield break; }
        Meeting.AskedEmissary = true;
        bool forced = false;
        int attempt = 0;
        while (Meeting.EmissaryResponse == 0)
        {
            if (Nuclear(counterpart) && !world.Civilizations[counterpart].Treaties[actor].NuclearThreat)
            { forced = true; }
            stance.Evaluate(actor, counterpart, call + "/stance/" + attempt++);
            if (focus is { } point && world.Map.Geometry.InBounds(point))
            { operation.Present(() => effects.EnsureVisible(point, actor)); }
            if (forced)
            { Notice(EmissaryNoticeKind.ForcedReception, actor, counterpart); break; }
            int local = world.Multiplayer.LocalHuman;
            bool intelligence = world.Civilizations[local].Treaties[counterpart].Embassy
                || BuildingQueries.HasWonder(world, rules, local, WonderIds.UnitedNations)
                || BuildingQueries.HasWonder(world, rules, local, WonderIds.MarcoPolo);
            yield return new EmissaryDecision.Receive(new(Speaker(actor), Speaker(counterpart), DiplomaticAttitudes.Category(Meeting.Attitude), intelligence));
            Meeting.EmissaryResponse = unchecked(-operation.TakeReply());
            if (Meeting.EmissaryResponse != -2)
            { break; }
            yield return new EmissaryDecision.Intelligence(new(actor, counterpart));
            operation.TakeReply();
            Meeting.EmissaryResponse = 0;
        }
        if (Meeting.EmissaryResponse != -1)
        { operation.Report(Greet(actor, counterpart, call + "/greet")); }
        operation.Accepted = Meeting.EmissaryResponse != -1;
    }
}
