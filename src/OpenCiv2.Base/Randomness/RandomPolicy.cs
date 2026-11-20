namespace OpenCiv2.Base.Randomness;

public enum RandomDomain { Gameplay, PresentationContent, Cosmetic }

/// <summary>Implementation replay policy, distinct from the F01 sampling laws.</summary>
public static class RandomPolicy
{
    public const string Id = "oc2-pcg32";
    public const int Version = 1;

    public static string DomainLabel(RandomDomain domain) => domain switch
    {
        RandomDomain.Gameplay => "gameplay",
        RandomDomain.PresentationContent => "presentation-content",
        RandomDomain.Cosmetic => "cosmetic",
        _ => throw new ArgumentOutOfRangeException(nameof(domain))
    };

    /// <summary>Stable ASCII identity; never an address, locale or thread identity.</summary>
    public static void ValidateName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (name.Length == 0
            || name.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '.' and not '-' and not '_' and not '/'))
        {
            throw new ArgumentException(
                "Random context/site names require nonempty ASCII letters, digits or .-_/.",
                nameof(name));
        }
    }

    public static Pcg32Position InitialPosition(ulong seed, RandomDomain domain, string context)
    {
        ValidateName(context);
        string identity = DomainLabel(domain) + "\0" + context;
        ulong stateSeed = Hash("oc2-pcg32/state\0" + identity) ^ seed;
        ulong stream = Hash("oc2-pcg32/stream\0" + identity) & 0x7FFFFFFFFFFFFFFFUL;
        return new(stateSeed, stream, 0);
    }

    // FNV-1a-64 over defined ASCII bytes. This is reproducibility, not cryptography.
    private static ulong Hash(string text)
    {
        ulong hash = 14695981039346656037UL;
        foreach (char c in text)
        {
            hash = unchecked((hash ^ (byte)c) * 1099511628211UL);
        }

        return hash;
    }
}
