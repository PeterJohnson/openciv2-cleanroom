using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

public sealed record GiftCityOption(CitySlot City, string Name);
public sealed record GiftUnitOption(UnitSlot Unit, string Name);
public abstract record DiplomaticGiftDecision
{
    public sealed record Menu(int Recipient, DiplomaticParty Counterpart) : DiplomaticGiftDecision;
    public sealed record Technology(int Recipient, ResearchOption Best, ResearchOption? Second) : DiplomaticGiftDecision;
    public sealed record Gold(int Recipient, OwnedList<int> Amounts) : DiplomaticGiftDecision;
    public sealed record City(int Recipient, OwnedList<GiftCityOption> Options) : DiplomaticGiftDecision;
    public sealed record Unit(int Recipient, OwnedList<GiftUnitOption> Options) : DiplomaticGiftDecision;
    public sealed record Grant(ResearchGrantDecision Decision) : DiplomaticGiftDecision;
}
public enum DiplomaticGiftNoticeKind { KnowNo, Wasting, Accept, MilitaryNone, MilitaryNo, Breakthrough }
public sealed record DiplomaticGiftNotice(DiplomaticGiftNoticeKind Kind, int Recipient,
    DiplomaticParty Counterpart, int Reaction, ResearchOption? Advance = null);
public interface IDiplomaticGiftEffects
{
    void Notice(DiplomaticGiftNotice notice);
    void RefreshStatus();
    void RefreshCity(CitySlot city);
}

/// <summary>V05 §4 local gift menu with actual technology survey, grants and U01 stack transfer.</summary>
public sealed class DiplomaticGifts(GameState world, RuleState rules, GameplayRandom random,
    DiplomaticTechnology technology, ResearchGrants grants, StackOperations stacks, IDiplomaticGiftEffects effects)
{
    private GameState World => world;
    private DiplomacyWorkspace E => world.Diplomacy;
    private bool Knows(int c, int a) => CivilizationKnowledge.KnowsAdvance(world.Civilizations, c, a);
    private ResearchOption Advance(int a) => new(a, rules.Definitions.Advances[a].Identity.Name);
    private DiplomaticParty Party(int c) => new(c, world.Names[c].Tribe, world.Names[c].Adjective);
    private static int Required(int? a) => a ?? throw new InvalidOperationException("The gift technology is absent");
    private void Request(int c, int amount) => world.Civilizations[c].DiploRequestCount =
        unchecked((sbyte)(world.Civilizations[c].DiploRequestCount + amount));
    private void Notice(DiplomaticGiftNoticeKind kind, int a, int b, int reaction = 0, int? advance = null) =>
        effects.Notice(new(kind, a, Party(b), reaction, advance is { } tech ? Advance(tech) : null));

    public static int GoldValue(int gold)
    {
        int value = 0, divisor = 10, step = 50;
        while (gold > 0)
        {
            value = unchecked(value + Arithmetic.ClampAB(gold, 0, step) / divisor);
            divisor += 5;
            gold -= step;
            step = 100;
        }
        return value;
    }

    public Operation Begin(int actor, int other, string call) => new(this, actor, other, call);
    public sealed class Operation : IDisposable
    {
        private readonly DiplomaticGifts parent;
        private readonly IEnumerator<DiplomaticGiftDecision?> steps;
        private readonly List<Exception> failures = [];
        private ResearchReply? answer;
        private bool disposed, faulted;
        public bool Completed { get; private set; }
        public DiplomaticGiftDecision? Pending { get; private set; }
        public IReadOnlyList<Exception> PresentationFailures => failures.AsReadOnly();
        internal Operation(DiplomaticGifts parent, int actor, int other, string call)
        {
            ArgumentException.ThrowIfNullOrEmpty(call);
            this.parent = parent;
            steps = parent.Run(this, actor, other, call).GetEnumerator();
        }
        internal ResearchReply? TakeAnswer() { var value = answer; answer = null; return value; }
        internal void Present(Action action) { try { action(); } catch (Exception error) { failures.Add(error); } }
        internal void Report(IEnumerable<Exception> errors) => failures.AddRange(errors);
        public DiplomaticGiftDecision? Resume(ResearchReply? reply = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("A failed gift cannot resume"); }
            if (Completed)
            { return null; }
            try
            {
                MapKnowledge.RequireLocal(parent.World);
                if (Pending is not null && reply is null)
                { return Pending; }
                answer = Pending is null ? null : reply;
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

    private IEnumerable<DiplomaticGiftDecision?> Run(Operation op, int a, int b, string call)
    {
        yield return new DiplomaticGiftDecision.Menu(a, Party(b));
        int menu = op.TakeAnswer()!.Value.Selected;
        if (menu == 0)
        { Request(b, -1); yield break; }
        if (menu == 1)
        {
            using var survey = technology.Begin(a, b, 0, default, call + "/survey", selectionOnly: true);
            survey.Resume();
            if (E.BestTechnology is not { } best)
            { Notice(DiplomaticGiftNoticeKind.KnowNo, a, b, 4); yield break; }
            yield return new DiplomaticGiftDecision.Technology(a, Advance(best), E.SecondTechnology is { } second ? Advance(second) : null);
            int reply = op.TakeAnswer()!.Value.Selected;
            if (reply == 0)
            { Notice(DiplomaticGiftNoticeKind.Wasting, a, b, 4); yield break; }
            int benefit = unchecked(AdvanceValuation.Value(world, rules, b, Required(E.BestTechnology)) * (reply == 1 ? 4 : 2));
            int gift = Required(reply == 1 ? E.BestTechnology : E.SecondTechnology);
            foreach (var decision in Grant(op, b, gift, a, call + "/technology"))
            { yield return decision; }
            DiplomaticAttitudes.Adjust(world, b, a, unchecked(-benefit));
            Accept(a, b);
        }
        else if (menu == 2)
        {
            int v = world.Civilizations[a].Gold / 50;
            List<int> amounts = [];
            for (int i = 1; i <= 4; ++i)
            {
                int amount = unchecked((i * v / 4) * 50);
                if (amount > 0 && (amounts.Count == 0 || amounts[^1] != amount))
                { amounts.Add(amount); }
            }
            yield return new DiplomaticGiftDecision.Gold(a, new(amounts));
            int gold = op.TakeAnswer()!.Value.Selected;
            if (gold == 0)
            { Notice(DiplomaticGiftNoticeKind.Wasting, a, b, 4); yield break; }
            world.Civilizations[a].Gold = unchecked(world.Civilizations[a].Gold - gold);
            world.Civilizations[b].Gold = unchecked(world.Civilizations[b].Gold + gold);
            op.Present(effects.RefreshStatus);
            DiplomaticAttitudes.Adjust(world, b, a, unchecked(-(GoldValue(gold) * 3 / 2)));
            Accept(a, b);
        }
        else if (menu == 3)
        {
            foreach (var decision in Military(op, a, b, call))
            { yield return decision; }
        }
    }

    private void Accept(int a, int b)
    {
        Notice(DiplomaticGiftNoticeKind.Accept, a, b, 2);
        Request(b, unchecked(-((sbyte)E.GiftsThisMeeting + 2)));
        E.GiftsThisMeeting = unchecked(E.GiftsThisMeeting + 1);
    }

    private IEnumerable<DiplomaticGiftDecision?> Military(Operation op, int a, int b, string call)
    {
        List<GiftCityOption> cities = [];
        for (int c = 0; c < world.Entities.Cities.End; ++c)
        {
            var city = world.Entities.Cities.Storage[c];
            if (city.Token.Occupied && city.Owner.Value == a)
            { cities.Add(new(new((ushort)c), city.Name)); }
        }
        if (cities.Count == 0)
        { Request(b, -1); yield break; }
        yield return new DiplomaticGiftDecision.City(a, new(cities));
        int selected = op.TakeAnswer()!.Value.Selected;
        if (selected < 0)
        { Request(b, -1); yield break; }
        var source = new CitySlot(checked((ushort)selected));
        var origin = world.Entities.Cities.At(source).Position;
        int first = stacks.FirstAt(origin);
        if (first < 0)
        { Notice(DiplomaticGiftNoticeKind.MilitaryNone, a, b); Request(b, -1); yield break; }
        var destination = CityQueries.Nearest(world, origin, b).Slot;
        if (destination is not { } target || DiplomaticAttitudes.Category(E.Attitude) > 4
            || world.Civilizations[a].NumAdvances < world.Civilizations[b].NumAdvances
            || world.Globals.PowerRank[a] < world.Globals.PowerRank[b]
            || world.Civilizations[a].MilitaryStrength < world.Civilizations[b].MilitaryStrength)
        { Notice(DiplomaticGiftNoticeKind.MilitaryNo, a, b, 4); yield break; }
        List<GiftUnitOption> units = [];
        for (int u = stacks.First(first); u >= 0; u = stacks.Next(u))
        { units.Add(new(new((ushort)u), rules.Definitions.Units[world.Entities.Units.Storage[u].Type.Value].Identity.Name)); }
        yield return new DiplomaticGiftDecision.Unit(a, new(units));
        int unit = op.TakeAnswer()!.Value.Selected;
        if (unit < 0)
        { Request(b, -1); yield break; }
        var rule = rules.Definitions.Units[world.Entities.Units.Storage[unit].Type.Value];
        if ((int)rule.ObsoleteBy >= 0 && Knows(b, (int)rule.ObsoleteBy))
        { Notice(DiplomaticGiftNoticeKind.MilitaryNo, a, b, 4); yield break; }
        stacks.Detach(unit, true);
        int type = world.Entities.Units.Storage[unit].Type.Value;
        world.Civilizations[a].UnitsActive[type] = unchecked((byte)(world.Civilizations[a].UnitsActive[type] - 1));
        world.Entities.Units.Storage[unit].Owner = new((byte)b);
        world.Entities.Units.Storage[unit].Visibility = default;
        world.Civilizations[b].UnitsActive[type] = unchecked((byte)(world.Civilizations[b].UnitsActive[type] + 1));
        stacks.AttachAtFront(unit, world.Entities.Cities.At(target).Position, true);
        world.Entities.Units.Storage[unit].Home = null;
        rule = rules.Definitions.Units[world.Entities.Units.Storage[unit].Type.Value];
        int benefit = unchecked((sbyte)rule.CostRows * 3);
        if (Knows(b, (int)rule.Prerequisite))
        { benefit /= 2; }
        op.Present(() => effects.RefreshCity(target));
        op.Present(() => effects.RefreshCity(source));
        DiplomaticAttitudes.Adjust(world, b, a, unchecked(-benefit));
        Notice(DiplomaticGiftNoticeKind.Accept, a, b, 2);
        int prerequisite = (int)rules.Definitions.Units[world.Entities.Units.Storage[unit].Type.Value].Prerequisite;
        if (Knows(b, prerequisite))
        { yield break; }
        for (int advance = 0; advance < RuleLimits.Advances; ++advance)
        {
            if (Knows(b, advance) || !Research.DescendsFrom(rules.Definitions, prerequisite, advance))
            { continue; }
            if (advance > 0 && random.PrimaryModulo(new ChoiceKey("V05.gift-breakthrough", call), 2) == 0)
            {
                Notice(DiplomaticGiftNoticeKind.Breakthrough, a, b, advance: advance);
                foreach (var decision in Grant(op, b, advance, a, call + "/breakthrough"))
                { yield return decision; }
            }
            yield break;
        }
    }

    private IEnumerable<DiplomaticGiftDecision?> Grant(Operation op, int owner, int advance, int source, string call)
    {
        using var grant = grants.Begin(owner, advance, source, false, call);
        while (!grant.Completed)
        {
            var decision = grant.Resume(op.TakeAnswer()?.Selected);
            if (!grant.Completed)
            { yield return decision is null ? null : new DiplomaticGiftDecision.Grant(decision); }
        }
        op.Report(grant.PresentationFailures);
    }
}
