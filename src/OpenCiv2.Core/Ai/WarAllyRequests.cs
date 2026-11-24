using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.Ai;

public sealed record WarAllyOffer(int Recipient, DiplomaticParty Sender, DiplomaticParty Enemy,
    bool Paid, bool AgainstAlly, int Payment, int? Advance, string? AdvanceName);
public abstract record WarAllyDecision
{
    public sealed record Emissary(EmissaryDecision Decision) : WarAllyDecision;
    public sealed record Offer(WarAllyOffer Request) : WarAllyDecision;
    public sealed record Grant(ResearchGrantDecision Decision) : WarAllyDecision;
}

/// <summary>A08 §9 local paid/unpaid requests for a war ally, with actual V05
/// reception, V03 grants and V04 declarations. The caller owns participant routing.</summary>
public sealed class WarAllyRequests(GameState world, RuleState rules, EmissaryReception emissaries,
    ResearchGrants grants, DiplomaticWar war, Action refreshStatus)
{
    private GameState World => world;
    private DiplomaticParty Party(int owner) => new(owner, world.Names[owner].Tribe, world.Names[owner].Adjective);
    private int Strength(int owner) => world.Civilizations[owner].MilitaryStrength;
    private bool Knows(int owner, int advance) => CivilizationKnowledge.KnowsAdvance(world.Civilizations, owner, advance);
    public Operation Begin(int sender, int partner, string call) => new(this, sender, partner, call);

    public sealed class Operation : IDisposable
    {
        private readonly WarAllyRequests parent;
        private readonly IEnumerator<WarAllyDecision?> steps;
        private readonly List<Exception> failures = [];
        private int? reply;
        private bool faulted, disposed;
        public bool Completed { get; private set; }
        public bool Requested { get; internal set; }
        public bool Helped { get; internal set; }
        public WarAllyDecision? Pending { get; private set; }
        public IReadOnlyList<Exception> PresentationFailures => failures.AsReadOnly();
        internal Operation(WarAllyRequests parent, int sender, int partner, string call)
        {
            ArgumentException.ThrowIfNullOrEmpty(call);
            this.parent = parent;
            steps = parent.Request(this, sender, partner, call).GetEnumerator();
        }
        internal int? TakeReply() { var answer = reply; reply = null; return answer; }
        internal void Report(IEnumerable<Exception> errors) => failures.AddRange(errors);
        internal void Report(Exception? error) { if (error is not null) { failures.Add(error); } }
        internal void Present(Action action) { try { action(); } catch (Exception error) { Report(error); } }
        public WarAllyDecision? Resume(int? answer = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("A failed war-ally request cannot resume"); }
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

    private IEnumerable<WarAllyDecision?> Request(Operation operation, int sender, int partner, string call)
    {
        if (world.Globals.Turn % 32 != 4 * sender
            || world.Civilizations[partner].Reputation - world.Civilizations[sender].ReputationCredit[partner] >= 6)
        { yield break; }
        for (int enemy = 1; enemy < EntityLimits.Civilizations; ++enemy)
        {
            if (enemy == sender || enemy == partner || !world.Globals.CivsAlive.Contains(enemy))
            { continue; }
            bool admitted = world.Civilizations[sender].Treaties[enemy].Fighting
                && !world.Civilizations[sender].Treaties[partner].Alliance
                && !world.Civilizations[sender].Treaties[partner].War
                && !DiplomaticAttitudes.Hostile(world, partner, enemy)
                && world.Civilizations[partner].Treaties[sender].Contact
                && world.Civilizations[partner].Treaties[enemy].Contact;
            bool paid = admitted && Strength(enemy) >= 2 * Strength(sender);
            bool unpaid = admitted && !paid && Strength(enemy) > Strength(sender)
                && (world.Globals.PowerRank[partner] < 6 || world.Globals.PowerRank[sender] < 6);
            if (!paid && !unpaid)
            {
                world.Civilizations[sender].Treaties[enemy].Fighting = false;
                continue;
            }
            operation.Requested = true;
            using var reception = emissaries.Begin(partner, sender, null, $"{call}/{enemy}/emissary");
            while (reception.Accepted is null)
            {
                if (reception.Resume(operation.TakeReply()) is { } question)
                { yield return new WarAllyDecision.Emissary(question); }
            }
            operation.Report(reception.PresentationFailures);
            if (reception.Accepted == false)
            { DelayContact(partner, sender, 15); }
            else
            {
                int amount = paid ? Arithmetic.ClampAB(world.Civilizations[sender].Gold / 100, 1, 10) * 50 : 0;
                int? advance = paid ? SelectAdvance(sender, partner, enemy) : null;
                yield return new WarAllyDecision.Offer(new(partner, Party(sender), Party(enemy), paid,
                    world.Civilizations[partner].Treaties[enemy].Alliance, amount, advance,
                    advance is { } selected ? rules.Definitions.Advances[selected].Identity.Name : null));
                if (operation.TakeReply() == 0)
                { DelayContact(partner, sender, 14); }
                else
                {
                    if (advance is { } technology)
                    {
                        if (!Knows(sender, technology))
                        {
                            foreach (var decision in Grant(operation, sender, technology, enemy, $"{call}/{enemy}/acquire"))
                            { yield return decision; }
                        }
                        foreach (var decision in Grant(operation, partner, technology, sender, $"{call}/{enemy}/share"))
                        { yield return decision; }
                    }
                    DiplomaticRelations.Set(world, sender, partner, new() { Peace = true, Alliance = true });
                    war.Declare(partner, enemy, sender);
                    if (!paid)
                    { world.Civilizations[partner].LastContactTurn[sender] = unchecked((short)(world.Globals.Turn + 16)); }
                    DiplomaticRelations.Set(world, partner, enemy, new() { War = true });
                    world.Civilizations[partner].Treaties[enemy].Aggressor = true;
                    world.Civilizations[partner].Treaties[enemy].ExtendedAggressor = true;
                    world.Civilizations[partner].LastContactTurn[enemy] = world.Globals.Turn;
                    if (paid)
                    {
                        world.Civilizations[partner].LastContactTurn[sender] = unchecked((short)(world.Globals.Turn + 16));
                        world.Civilizations[sender].Gold = unchecked(world.Civilizations[sender].Gold - amount);
                        world.Civilizations[partner].Gold = unchecked(world.Civilizations[partner].Gold + amount);
                        operation.Present(refreshStatus);
                    }
                    operation.Helped = true;
                }
            }
            operation.Report(emissaries.Reset());
            yield break; // Neither a declined request nor acceptance runs this enemy's cleanup.
        }
    }

    private void DelayContact(int partner, int sender, int age) =>
        world.Civilizations[partner].LastContactTurn[sender] = unchecked((short)Math.Max(
            world.Civilizations[partner].LastContactTurn[sender], world.Globals.Turn - age));

    private int? SelectAdvance(int sender, int partner, int enemy)
    {
        int? selected = null;
        int best = -99;
        for (int advance = 0; advance < RuleLimits.Advances; ++advance)
        {
            var definition = rules.Definitions.Advances[advance];
            if (definition.Prerequisites[0] == AdvanceReference.Never && definition.Prerequisites[1] == AdvanceReference.Never
                || Knows(partner, advance))
            { continue; }
            if (!Knows(sender, advance))
            {
                int local = world.Multiplayer.LocalHuman;
                if (!Knows(enemy, advance) || world.Civilizations[local].Treaties[sender].Embassy
                    || BuildingQueries.HasWonder(world, rules.Definitions, local, WonderIds.UnitedNations)
                    || BuildingQueries.HasWonder(world, rules.Definitions, local, WonderIds.MarcoPolo))
                { continue; }
            }
            int value = AdvanceValuation.Value(world, rules, partner, advance);
            if (value > best)
            { best = value; selected = advance; }
        }
        return selected;
    }

    private IEnumerable<WarAllyDecision?> Grant(Operation operation, int owner, int advance, int source, string call)
    {
        using var child = grants.Begin(owner, advance, source, false, call);
        while (!child.Completed)
        {
            var decision = child.Resume(operation.TakeReply());
            if (!child.Completed)
            { yield return decision is null ? null : new WarAllyDecision.Grant(decision); }
        }
        operation.Report(child.PresentationFailures);
    }
}
