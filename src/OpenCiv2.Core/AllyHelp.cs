using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

public sealed record AllyHelpRequest(int Recipient, DiplomaticParty Ally, DiplomaticParty Enemy, int Payment = 0);
public abstract record AllyHelpDecision
{
    public sealed record Emissary(EmissaryDecision Decision) : AllyHelpDecision;
    public sealed record Demand(AllyHelpRequest Request) : AllyHelpDecision;
    public sealed record Bonus(AllyHelpRequest Request) : AllyHelpDecision;
}
public interface IAllyHelpEffects
{
    void DidNotHelp(AllyHelpRequest request);
    void RefreshStatus();
}

/// <summary>V05 §5 ally-demand-help, composed with actual emissary, attitude and V04 war/expulsion.
/// Captures the local recipient once; paid offers retain their price across the decision.</summary>
public sealed class AllyHelp(GameState world, EmissaryReception emissaries, DiplomaticWar war, IAllyHelpEffects effects)
{
    private GameState World => world;
    private DiplomaticParty Party(int owner) => new(owner, world.Names[owner].Tribe, world.Names[owner].Adjective);
    public Operation Begin(int ally, int enemy, string call) => new(this, ally, enemy, call);

    public sealed class Operation : IDisposable
    {
        private readonly AllyHelp parent;
        private readonly IEnumerator<AllyHelpDecision> steps;
        private int? reply;
        private bool faulted, disposed;
        private readonly List<Exception> presentationFailures = [];
        public bool Completed { get; private set; }
        public bool Admitted { get; internal set; }
        public bool Helped { get; internal set; }
        public AllyHelpDecision? Pending { get; private set; }
        public IReadOnlyList<Exception> PresentationFailures => presentationFailures.AsReadOnly();
        internal Operation(AllyHelp parent, int ally, int enemy, string call)
        {
            ArgumentException.ThrowIfNullOrEmpty(call);
            this.parent = parent;
            steps = parent.Request(this, ally, enemy, call).GetEnumerator();
        }
        internal int? TakeReply() { var value = reply; reply = null; return value; }
        internal void Report(Exception? error) { if (error is not null) { presentationFailures.Add(error); } }
        internal void Present(Action action) { try { action(); } catch (Exception error) { Report(error); } }
        public AllyHelpDecision? Resume(int? answer = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("A failed ally-help request cannot resume"); }
            if (Completed)
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
                Completed = true;
                steps.Dispose();
                return null;
            }
            catch { faulted = true; steps.Dispose(); throw; }
        }
        public void Dispose() { disposed = true; steps.Dispose(); }
    }

    private IEnumerable<AllyHelpDecision> Request(Operation operation, int ally, int enemy, string call)
    {
        int local = world.Multiplayer.LocalHuman;
        if (!world.Globals.HumanPlayers.Contains(local) || !world.Civilizations[ally].Treaties[local].Alliance
            || world.Civilizations[local].Treaties[enemy].Alliance || world.Civilizations[local].Treaties[enemy].War)
        { yield break; }
        operation.Admitted = true;
        using var reception = emissaries.Begin(local, ally, null, call + "/emissary");
        while (reception.Accepted is null)
        {
            if (reception.Resume(operation.TakeReply()) is { } pending)
            { yield return new AllyHelpDecision.Emissary(pending); }
        }
        foreach (var error in reception.PresentationFailures)
        { operation.Report(error); }
        if (reception.Accepted == false)
        { DiplomaticAttitudes.Adjust(world, ally, local, 100); }
        else
        {
            yield return new AllyHelpDecision.Demand(new(local, Party(ally), Party(enemy)));
            int reply = operation.TakeReply()!.Value;
            if (reply != 1)
            {
                int threat = 0;
                // V05's help offer excludes both region zero and region 63, unlike A08 evaluation.
                for (int region = 1; region < MapLimits.Regions - 1; ++region)
                {
                    int military = world.Civilizations[ally].ContinentMilitary[region];
                    int attack = world.Civilizations[enemy].ContinentAttack[region];
                    if ((world.Civilizations[ally].CityCount > 1 ? 1 : 0) < world.Civilizations[ally].ContinentCities[region] && military < attack)
                    { threat = unchecked(threat + attack * (world.Globals.Difficulty == Difficulty.Chieftain ? 4 : 2) / (military + 1)); }
                }
                if (threat != 0)
                {
                    int units = Arithmetic.ClampAB(threat, 0, world.Civilizations[ally].Gold / 50);
                    int payment = unchecked(units * 50);
                    if (payment != 0)
                    {
                        yield return new AllyHelpDecision.Bonus(new(local, Party(ally), Party(enemy), payment));
                        reply = operation.TakeReply()!.Value;
                        if (reply == 1)
                        {
                            world.Civilizations[ally].Gold = unchecked(world.Civilizations[ally].Gold - units * 50);
                            world.Civilizations[local].Gold = unchecked(world.Civilizations[local].Gold + payment);
                            operation.Present(effects.RefreshStatus);
                        }
                    }
                }
            }
            if (reply == 1)
            { war.Declare(local, enemy, ally); operation.Helped = true; }
            else
            {
                effects.DidNotHelp(new(local, Party(ally), Party(enemy)));
                if (world.Diplomacy.Attitude < 51)
                { DiplomaticAttitudes.Adjust(world, ally, local, 100); }
                else
                { war.CancelAlliance(local, ally); }
            }
        }
        operation.Report(emissaries.Reset());
    }
}
