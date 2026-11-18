namespace OpenCiv2.Base.Randomness;

/// <summary>F01/X02 semantic identity, independent of generation order.</summary>
public readonly record struct ChoiceKey(string Site, string Call, int Attempt = 0);
public abstract record SamplingLaw
{
    private protected SamplingLaw() { }
    public sealed record PrimaryCode : SamplingLaw;
    public sealed record PrimaryModulo(int N) : SamplingLaw;
    public sealed record SecondaryUnit : SamplingLaw;
    public sealed record SecondaryRange(int Lo, int Hi) : SamplingLaw;
}

public readonly record struct ChoiceCode
{
    public bool IsPrimary { get; }
    public uint Bits { get; }
    private ChoiceCode(bool primary, uint bits) { IsPrimary = primary; Bits = bits; }
    public static ChoiceCode Primary(int value) => new(true, unchecked((uint)value));
    public static ChoiceCode Secondary(uint value) => new(false, value);
}

/// <summary>Exact binary64 equality includes the sign of zero.</summary>
public readonly record struct ChoiceResult
{
    public bool IsInteger { get; }
    public long Bits { get; }
    private ChoiceResult(bool integer, long bits) { IsInteger = integer; Bits = bits; }
    public static ChoiceResult Integer(int value) => new(true, value);
    public static ChoiceResult Binary64(double value) => new(false, BitConverter.DoubleToInt64Bits(value));
    public int AsInteger() => IsInteger ? checked((int)Bits) : throw new InvalidOperationException("Not an integer choice.");
    public double AsBinary64() => !IsInteger ? BitConverter.Int64BitsToDouble(Bits) : throw new InvalidOperationException("Not a binary64 choice.");
}

public enum ChoiceFailure
{
    InvalidRecord, DuplicateKey, SemanticKeyRequired, Missing, AlreadyConsumed,
    LawMismatch, Unused, TraceLimit, InvalidCheckpoint
}

public sealed class ChoiceException(ChoiceFailure failure, ChoiceKey? key = null)
    : Exception($"Random choice: {CodeFor(failure)}")
{
    public ChoiceFailure Failure { get; } = failure;
    public ChoiceKey? Key { get; } = key;
    public string Code => CodeFor(Failure);
    private static string CodeFor(ChoiceFailure value) => value switch
    {
        ChoiceFailure.InvalidRecord => "invalid_record",
        ChoiceFailure.DuplicateKey => "duplicate_key",
        ChoiceFailure.SemanticKeyRequired => "semantic_key_required",
        ChoiceFailure.Missing => "missing",
        ChoiceFailure.AlreadyConsumed => "already_consumed",
        ChoiceFailure.LawMismatch => "law_mismatch",
        ChoiceFailure.Unused => "unused",
        ChoiceFailure.TraceLimit => "trace_limit",
        ChoiceFailure.InvalidCheckpoint => "invalid_checkpoint",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };
}

public readonly record struct Choice(ChoiceKey Key, SamplingLaw Law, ChoiceCode Code, ChoiceResult Result)
{
    public static Choice Create(ChoiceKey key, SamplingLaw law, ChoiceCode code)
    {
        if (string.IsNullOrEmpty(key.Site) || string.IsNullOrEmpty(key.Call) || key.Attempt < 0)
            throw new ChoiceException(ChoiceFailure.InvalidRecord, key);
        try { return new(key, law, code, Map(law, code)); }
        catch (ChoiceException error) { throw new ChoiceException(error.Failure, key); }
    }

    public void Validate()
    {
        if (this != Create(Key, Law, Code)) throw new ChoiceException(ChoiceFailure.InvalidRecord, Key);
    }

    public static ChoiceResult Map(SamplingLaw law, ChoiceCode code)
    {
        bool primary = law is SamplingLaw.PrimaryCode or SamplingLaw.PrimaryModulo;
        if (law is null || primary != code.IsPrimary || (primary && code.Bits > 32767))
            throw new ChoiceException(ChoiceFailure.InvalidRecord);
        return law switch
        {
            SamplingLaw.PrimaryCode => ChoiceResult.Integer((int)code.Bits),
            SamplingLaw.PrimaryModulo { N: > 0 } modulo => ChoiceResult.Integer(Arithmetic.PrimaryModuloFromCode((int)code.Bits, modulo.N)),
            SamplingLaw.SecondaryUnit => ChoiceResult.Binary64(Arithmetic.SecondaryUnitFromCode(code.Bits)),
            SamplingLaw.SecondaryRange range => ChoiceResult.Integer(Arithmetic.SecondaryRangeFromCode(range.Lo, range.Hi, code.Bits)),
            _ => throw new ChoiceException(ChoiceFailure.InvalidRecord)
        };
    }
}

public sealed record ControlledChoices(OwnedList<Choice> Records);
public sealed record ChoiceTrace(long Limit, OwnedList<Choice> Events);
