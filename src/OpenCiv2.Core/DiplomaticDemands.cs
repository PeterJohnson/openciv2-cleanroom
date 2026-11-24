using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

public abstract record DiplomaticDemandDecision
{
    public sealed record Technology(TechnologyNegotiationDecision Decision) : DiplomaticDemandDecision;
    public sealed record Grant(ResearchGrantDecision Decision) : DiplomaticDemandDecision;
}
public enum DiplomaticDemandNoticeKind { Provoke, Apologize, NoViolators, Feeble, Sympathy, FeebleAlly, TauntAlly, GiveGold, GiveTechnology }
public sealed record DiplomaticDemandNotice(DiplomaticDemandNoticeKind Kind, int Recipient, DiplomaticParty Other,
    int Proposal, int Reaction, int Variant = 0, bool Initial = false, int Gold = 0, ResearchOption? Advance = null);
public interface IDiplomaticDemandEffects
{
    void Notice(DiplomaticDemandNotice notice);
    void RefreshStatus();
    void Redraw(MapPoint position);
}

/// <summary>V05 §5 local aid, tribute and withdrawal with actual stacks, grants and allied exchange.</summary>
public sealed class DiplomaticDemands(GameState world, RuleState rules, GameplayRandom random,
    DiplomaticStance stance, DiplomaticTechnology technology, ResearchGrants grants, StackOperations stacks,
    DiplomaticWar war, IDiplomaticDemandEffects effects)
{
    private GameState World => world;
    private DiplomacyWorkspace E => world.Diplomacy;
    private int Rank(int c) => world.Globals.PowerRank[c];
    private bool Wonder(int c, int w) => BuildingQueries.HasWonder(world, rules.Definitions, c, w);
    private DiplomaticParty Party(int c) => new(c, world.Names[c].Tribe, world.Names[c].Adjective);
    private ResearchOption Advance(int a) => new(a, rules.Definitions.Advances[a].Identity.Name);
    private static int Required(int? a) => a ?? throw new InvalidOperationException("The demanded technology is absent");
    private void Notice(DiplomaticDemandNoticeKind kind, int a, int b, int proposal, int reaction = 3,
        int variant = 0, bool initial = false, int gold = 0, int? advance = null) =>
        effects.Notice(new(kind, a, Party(b), proposal, reaction, variant, initial, gold, advance is { } tech ? Advance(tech) : null));
    public Operation Begin(int actor, int other, int proposal, string call)
    {
        if (proposal is < 3 or > 5)
        { throw new ArgumentOutOfRangeException(nameof(proposal)); }
        return new(this, actor, other, proposal, call);
    }

    public sealed class Operation : IDisposable
    {
        private readonly DiplomaticDemands parent;
        private readonly IEnumerator<DiplomaticDemandDecision?> steps;
        private readonly List<Exception> failures = [];
        private ResearchReply? reply;
        private bool faulted, disposed;
        public bool Completed { get; private set; }
        public bool EndsAudience { get; internal set; }
        public DiplomaticDemandDecision? Pending { get; private set; }
        public IReadOnlyList<Exception> PresentationFailures => failures.AsReadOnly();
        internal Operation(DiplomaticDemands parent, int a, int b, int proposal, string call)
        { ArgumentException.ThrowIfNullOrEmpty(call); this.parent = parent; steps = parent.Run(this, a, b, proposal, call).GetEnumerator(); }
        internal ResearchReply? TakeReply() { var answer = reply; reply = null; return answer; }
        internal void Report(IEnumerable<Exception> errors) => failures.AddRange(errors);
        internal void Present(Action action) { try { action(); } catch (Exception error) { failures.Add(error); } }
        public DiplomaticDemandDecision? Resume(ResearchReply? answer = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("A failed diplomatic demand cannot resume"); }
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

    private IEnumerable<DiplomaticDemandDecision?> Run(Operation op, int a, int b, int proposal, string call)
    {
        int capturedRequest = world.Civilizations[b].DiploRequestCount;
        world.Civilizations[b].DiploRequestCount = unchecked((sbyte)(proposal == 5 ? capturedRequest + 1 : stance.Patience(a, b)));
        if (E.DeclareWar || world.Civilizations[b].Treaties[a].HostileIntent || world.Civilizations[b].Treaties[a].BrokenTreaty)
        { Provoke(op, a, b, proposal, call, true); yield break; }
        if (proposal == 3 && Rank(a) < Rank(b))
        { E.Fear = Rank(b) - Rank(a); }
        int tribute = unchecked(Arithmetic.ClampAB(E.Fear, 0, world.Civilizations[b].Gold / 50) * 50);
        int age = unchecked(world.Globals.Turn - E.LastContact);
        if (age < 8)
        { tribute = 0; E.WantedTechnology = null; }
        if (age < 16)
        { tribute /= 2; }
        if (!world.Civilizations[a].Treaties[b].Peace)
        { tribute /= 2; }
        if (Wonder(b, WonderIds.GreatWall) || Wonder(b, WonderIds.UnitedNations))
        { tribute = 0; }
        if (world.Civilizations[b].Treaties[a].Alliance && E.WantedTechnology is not null
            && world.Civilizations[a].NumAdvances + unchecked((byte)world.Globals.Difficulty) < world.Civilizations[b].NumAdvances
            && (world.Civilizations[b].MilitaryStrength < world.Civilizations[a].MilitaryStrength || Rank(a) < Rank(b)))
        { tribute = 0; E.Fear = 1; }
        bool protection = Wonder(a, WonderIds.GreatWall) || Wonder(a, WonderIds.UnitedNations);
        if (world.Civilizations[a].Reputation - world.Civilizations[b].ReputationCredit[a] > 6)
        { protection = false; }
        int moved = 0;
        if (world.Civilizations[b].Treaties[a].Peace && !world.Civilizations[b].Treaties[a].Alliance)
        {
            bool refuse = (E.InitiallyAggressive || E.Attitude > 74) && E.Fear == 0 && !protection
                && unchecked((byte)world.Civilizations[b].Government) < 5 + random.PrimaryModulo(new ChoiceKey("V05.withdraw-government", call), 2)
                && !world.Civilizations[b].Treaties[a].ChangedThisTurn && E.Attitude > 25;
            if (!refuse)
            { moved = Withdraw(op, a, b); }
        }
        int adjustment = proposal == 5 ? 2 : 10;
        if (proposal == 3)
        {
            if (Rank(a) < Rank(b))
            { adjustment = 5; }
            if (world.Civilizations[a].NumAdvances < world.Civilizations[b].NumAdvances && Rank(a) < Rank(b) || Rank(a) < 4)
            { adjustment = 0; }
        }
        DiplomaticAttitudes.Adjust(world, b, a, adjustment);
        if (moved != 0)
        {
            Notice(DiplomaticDemandNoticeKind.Apologize, a, b, proposal);
            if (proposal == 5)
            { yield break; }
        }
        if (tribute == 0 && (E.WantedTechnology is null || E.Fear == 0) || proposal == 5)
        {
            if (!world.Civilizations[b].Treaties[a].Alliance)
            {
                bool avoid = !E.InitiallyAggressive && E.Attitude < 76 || moved != 0
                    || (E.Fear != 0 || protection || world.Civilizations[b].Treaties[a].ChangedThisTurn || E.Attitude < 26)
                        && (random.PrimaryModulo(new ChoiceKey("V05.demand-avoid-war", call), 3) != 0 || proposal == 5);
                if (avoid)
                { if (moved == 0) { Notice(proposal == 5 ? DiplomaticDemandNoticeKind.NoViolators : DiplomaticDemandNoticeKind.Feeble, a, b, proposal); } }
                else
                { Provoke(op, a, b, proposal, call, false); }
                yield break;
            }
            using var exchange = technology.Begin(a, b, 2 + (capturedRequest < 2 ? 1 : 0), default, call + "/exchange");
            while (!exchange.Completed)
            {
                var decision = exchange.Resume(op.TakeReply());
                if (!exchange.Completed)
                { yield return decision is null ? null : new DiplomaticDemandDecision.Technology(decision); }
            }
            op.Report(exchange.PresentationFailures);
            if (exchange.Result != 0)
            { yield break; }
            if (world.Civilizations[a].NumAdvances < world.Civilizations[b].NumAdvances && Rank(a) < Rank(b) || Rank(a) < 4)
            { Notice(DiplomaticDemandNoticeKind.Sympathy, a, b, proposal); }
            else if (!E.InitiallyAggressive && E.Attitude < 51 && !E.TargetIsLeader)
            { if (moved == 0) { Notice(DiplomaticDemandNoticeKind.FeebleAlly, a, b, proposal); } }
            else
            {
                Notice(DiplomaticDemandNoticeKind.TauntAlly, a, b, proposal, 4);
                war.CancelAlliance(a, b);
                op.EndsAudience = true;
            }
            yield break;
        }
        if (E.WantedTechnology is null || tribute != 0 && world.Civilizations[b].NumAdvances <= world.Civilizations[a].NumAdvances)
        {
            Notice(DiplomaticDemandNoticeKind.GiveGold, a, b, proposal, 2, gold: tribute);
            world.Civilizations[b].Gold = unchecked(world.Civilizations[b].Gold - tribute);
            world.Civilizations[a].Gold = unchecked(world.Civilizations[a].Gold + tribute);
            op.Present(effects.RefreshStatus);
        }
        else
        {
            Notice(DiplomaticDemandNoticeKind.GiveTechnology, a, b, proposal, 2, advance: E.WantedTechnology);
            using var grant = grants.Begin(a, Required(E.WantedTechnology), b, false, call + "/grant");
            while (!grant.Completed)
            {
                var decision = grant.Resume(op.TakeReply()?.Selected);
                if (!grant.Completed)
                { yield return decision is null ? null : new DiplomaticDemandDecision.Grant(decision); }
            }
            op.Report(grant.PresentationFailures);
            E.WantedTechnology = null;
        }
        if (!world.Civilizations[b].Treaties[a].Peace)
        {
            if (!E.HadCeasefire)
            { DiplomaticRelations.Set(world, a, b, new() { Tribute = true }); }
            DiplomaticRelations.Set(world, a, b, new() { Ceasefire = true });
        }
    }
    private void Provoke(Operation op, int a, int b, int proposal, string call, bool initial)
    {
        int variant = random.SecondaryRange(new ChoiceKey(initial ? "V05.demand-provoke-initial" : "V05.demand-provoke", call), 0, 2);
        E.DeclareWar = false;
        DiplomaticRelations.Set(world, b, a, new() { War = true });
        if (initial)
        { world.Civilizations[a].LastContactTurn[b] = world.Globals.Turn; }
        Notice(DiplomaticDemandNoticeKind.Provoke, a, b, proposal, 4, variant, initial);
        if (!initial)
        { world.Civilizations[a].LastContactTurn[b] = world.Globals.Turn; }
        op.EndsAudience = true;
    }
    private int Withdraw(Operation op, int a, int b)
    {
        int moved = 0;
        for (int u = 0; u < world.Entities.Units.End; ++u)
        {
            var unit = world.Entities.Units.Storage[u];
            if (!unit.Token.Occupied || unit.Owner.Value != b || unchecked((sbyte)rules.Definitions.Units[unit.Type.Value].Role) >= 6
                || world.Map.TileAt(unit.Position).Ocean)
            { continue; }
            var nearest = CityQueries.Nearest(world, unit.Position, a);
            if (nearest.Slot is null || nearest.Distance >= 3 || CityQueries.Nearest(world, unit.Position, b).Slot is not { } home)
            { continue; }
            stacks.Relocate(u, world.Entities.Cities.At(home).Position, true);
            world.Entities.Units.Storage[u].Visibility = default;
            world.Entities.Units.Storage[u].Orders = UnitOrder.None;
            op.Present(() => effects.Redraw(unit.Position));
            ++moved;
        }
        return moved;
    }
}
