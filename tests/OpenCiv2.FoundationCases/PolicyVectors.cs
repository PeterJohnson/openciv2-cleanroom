namespace OpenCiv2.FoundationCases;

// Policy regression vectors derived from the documented FNV and PCG arithmetic.
// Calculation was separately checked against the PCG author's 42/54 vector.
internal static class PolicyVectors
{
    public const ulong StateSeed = 0xCEFE7B236446324FUL;
    public const ulong Stream = 0x773610F86F42849EUL;
    public static readonly uint[] Words = [0x1F35CA66, 0x7064C4EC, 0x652D0716, 0x5DD5C3D6, 0xB9E0258E, 0x5DD19C44];
}
