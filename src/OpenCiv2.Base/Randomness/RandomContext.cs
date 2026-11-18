namespace OpenCiv2.Base.Randomness;

/// <summary>Owned immutable data sufficient for bounded replay reconstruction.</summary>
public sealed record RandomCheckpoint(
    string PolicyId, int PolicyVersion, RandomDomain Domain, string Context, Pcg32Position Position,
    ControlledChoices? Controlled = null, ChoiceTrace? Trace = null);

/// <summary>
/// F01 §§2–4,6 generated and controlled contexts with bounded semantic traces.
/// A single session lane owns each context; this class does not synchronize concurrent use.
/// </summary>
public abstract class RandomContext
{
    private Pcg32Source source;
    private ControlledChoices? controlled;
    private Dictionary<ChoiceKey, Choice> choices = [];
    private HashSet<ChoiceKey> consumed = [];
    private List<Choice>? events;
    private long traceLimit;
    public RandomDomain Domain { get; }
    public string Context { get; }
    public ulong Draws => source.Capture().Draws;

    private protected RandomContext(ulong seed, string context, RandomDomain domain)
    {
        Pcg32Position initial = RandomPolicy.InitialPosition(seed, domain, context);
        source = new Pcg32Source(initial.Seed, initial.Stream);
        Domain = domain;
        Context = context;
    }

    private protected RandomContext(ControlledChoices choices, string context, RandomDomain domain)
        : this(0, context, domain)
    {
        ArgumentNullException.ThrowIfNull(choices);
        Restore(Capture() with { Controlled = choices, Trace = new(choices.Records.Count, OwnedList<Choice>.Empty) }, 0);
    }

    public int PrimaryCode(ChoiceKey key) => Sample(key, new SamplingLaw.PrimaryCode()).AsInteger();
    public int PrimaryModulo(ChoiceKey key, int n)
    {
        if (n <= 0) throw new ArgumentOutOfRangeException(nameof(n));
        return n == 1 ? 0 : Sample(key, new SamplingLaw.PrimaryModulo(n)).AsInteger();
    }
    public double SecondaryUnit(ChoiceKey key) => Sample(key, new SamplingLaw.SecondaryUnit()).AsBinary64();
    public int SecondaryRange(ChoiceKey key, int lo, int hi) => lo == hi ? lo : Sample(key, new SamplingLaw.SecondaryRange(lo, hi)).AsInteger();

    public void RecordChoices(long limit)
    {
        if (limit < 0 || limit > uint.MaxValue) throw new ArgumentOutOfRangeException(nameof(limit));
        if (events is not null) throw new InvalidOperationException("Random choices are already being recorded.");
        events = [];
        traceLimit = limit;
    }

    public void RequireChoicesConsumed()
    {
        // Match the stable semantic-key diagnostic order, never dictionary enumeration.
        foreach (ChoiceKey key in choices.Keys.OrderBy(k => k.Site, StringComparer.Ordinal)
            .ThenBy(k => k.Call, StringComparer.Ordinal).ThenBy(k => k.Attempt))
            if (!consumed.Contains(key)) throw new ChoiceException(ChoiceFailure.Unused, key);
    }

    private ChoiceResult Sample(ChoiceKey key, SamplingLaw law)
    {
        bool primary = law is SamplingLaw.PrimaryCode or SamplingLaw.PrimaryModulo;
        Choice choice = Choice.Create(key, law, primary ? ChoiceCode.Primary(0) : ChoiceCode.Secondary(0));
        if (controlled is not null)
        {
            if (!choices.TryGetValue(key, out Choice supplied)) throw new ChoiceException(ChoiceFailure.Missing, key);
            if (supplied.Law != law) throw new ChoiceException(ChoiceFailure.LawMismatch, key);
            choice = supplied;
        }
        else if (Draws == ulong.MaxValue) throw new OverflowException("Random source draw counter exhausted.");
        if (events is not null)
        {
            if (consumed.Contains(key)) throw new ChoiceException(ChoiceFailure.AlreadyConsumed, key);
            if (events.Count >= traceLimit) throw new ChoiceException(ChoiceFailure.TraceLimit, key);
            events.EnsureCapacity(events.Count + 1);
            consumed.Add(key);
        }
        if (controlled is null)
        {
            uint raw;
            try { raw = source.NextUInt32(); }
            catch { consumed.Remove(key); throw; }
            ChoiceCode code = primary ? ChoiceCode.Primary((int)(raw >> 17)) : ChoiceCode.Secondary(raw);
            choice = new(key, law, code, Choice.Map(law, code));
        }
        events?.Add(choice);
        return choice.Result;
    }

    public int PrimaryCode(string site) => (int)(Next(site) >> 17);
    public uint SecondaryCode(string site) => Next(site);

    public int PrimaryModulo(string site, int n)
    {
        if (n <= 0)
            throw new ArgumentOutOfRangeException(nameof(n));
        if (n == 1)
            return 0;
        return Arithmetic.PrimaryModuloFromCode(PrimaryCode(site), n);
    }

    public double SecondaryUnit(string site) => Arithmetic.SecondaryUnitFromCode(Next(site));

    public int SecondaryRange(string site, int lo, int hi) => lo == hi
        ? lo
        : Arithmetic.SecondaryRangeFromCode(lo, hi, Next(site));

    public RandomCheckpoint Capture() => new(RandomPolicy.Id, RandomPolicy.Version, Domain, Context, source.Capture(),
        controlled, events is null ? null : new(traceLimit, new(events)));

    /// <summary>Prepare before replacement; any validation/reconstruction failure leaves this context unchanged.</summary>
    public void Restore(RandomCheckpoint checkpoint, ulong replayLimit)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        if (checkpoint.PolicyId != RandomPolicy.Id || checkpoint.PolicyVersion != RandomPolicy.Version ||
            checkpoint.Domain != Domain || checkpoint.Context != Context)
            throw new ArgumentException("Checkpoint policy or context does not match.", nameof(checkpoint));
        var newChoices = new Dictionary<ChoiceKey, Choice>();
        var newConsumed = new HashSet<ChoiceKey>();
        if (checkpoint.Controlled is { } tape)
        {
            if (checkpoint.Position.Draws != 0 || checkpoint.Trace is null || checkpoint.Trace.Limit != tape.Records.Count)
                throw new ChoiceException(ChoiceFailure.InvalidCheckpoint);
            foreach (Choice choice in tape.Records)
            {
                choice.Validate();
                if (!newChoices.TryAdd(choice.Key, choice)) throw new ChoiceException(ChoiceFailure.DuplicateKey, choice.Key);
            }
        }
        if (checkpoint.Trace is { } trace)
        {
            if (trace.Limit < 0 || trace.Limit > uint.MaxValue || trace.Events.Count > trace.Limit ||
                (checkpoint.Controlled is null && (ulong)trace.Events.Count > checkpoint.Position.Draws))
                throw new ChoiceException(ChoiceFailure.InvalidCheckpoint);
            foreach (Choice choice in trace.Events)
            {
                choice.Validate();
                if (!newConsumed.Add(choice.Key)) throw new ChoiceException(ChoiceFailure.DuplicateKey, choice.Key);
                if (checkpoint.Controlled is not null && (!newChoices.TryGetValue(choice.Key, out Choice supplied) || supplied != choice))
                    throw new ChoiceException(ChoiceFailure.InvalidCheckpoint, choice.Key);
            }
        }
        List<Choice>? newEvents = checkpoint.Trace?.Events.ToList();
        Pcg32Source replacement = Pcg32Source.Restore(checkpoint.Position, replayLimit);
        source = replacement;
        controlled = checkpoint.Controlled;
        choices = newChoices;
        consumed = newConsumed;
        events = newEvents;
        traceLimit = checkpoint.Trace?.Limit ?? 0;
    }

    private uint Next(string site)
    {
        if (events is not null || controlled is not null) throw new ChoiceException(ChoiceFailure.SemanticKeyRequired);
        RandomPolicy.ValidateName(site);
        return source.NextUInt32();
    }
}

public sealed class GameplayRandom : RandomContext
{
    public GameplayRandom(ulong seed, string context) : base(seed, context, RandomDomain.Gameplay) { }
    public GameplayRandom(ControlledChoices choices, string context) : base(choices, context, RandomDomain.Gameplay) { }
}

public sealed class PresentationRandom : RandomContext
{
    public PresentationRandom(ulong seed, string context) : base(seed, context, RandomDomain.PresentationContent) { }
    public PresentationRandom(ControlledChoices choices, string context) : base(choices, context, RandomDomain.PresentationContent) { }
}

public sealed class CosmeticRandom : RandomContext
{
    public CosmeticRandom(ulong seed, string context) : base(seed, context, RandomDomain.Cosmetic) { }
    public CosmeticRandom(ControlledChoices choices, string context) : base(choices, context, RandomDomain.Cosmetic) { }
}
