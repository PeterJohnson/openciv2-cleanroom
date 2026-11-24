using System.Globalization;
using System.Text;
using OpenCiv2.Base;
using OpenCiv2.Core.State;

namespace OpenCiv2.Formats.Classic;

/// <summary>P04 §2 adopted classic substitution grammar. Owned parameters are captured
/// once per interaction; appending fragments cannot mutate or re-read the caller's state.</summary>
public sealed class ClassicTextExpansion
{
    private readonly OwnedList<int> numbers;
    private readonly OwnedList<string> strings;
    private readonly string? profileHeight;

    public ClassicTextExpansion(OwnedList<int> numbers, OwnedList<string> strings, string? profileHeight = null)
    {
        ArgumentNullException.ThrowIfNull(numbers);
        ArgumentNullException.ThrowIfNull(strings);
        if (numbers.Count != 6 || strings.Count != TextParameters.StringCount)
        { throw new ArgumentException("Classic substitution requires six numbers and eleven ordinary strings"); }
        this.numbers = numbers;
        this.strings = strings;
        this.profileHeight = profileHeight;
    }

    public static ClassicTextExpansion Capture(TextParameters parameters, string? profileHeight = null)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        return new(new(Enumerable.Range(0, 6).Select(i => parameters.Numbers[i])), new(parameters.Strings), profileHeight);
    }

    /// <summary>Source tokens expand once. Unknown percent markers lose the percent;
    /// inserted parameter text is never rescanned. Unsupported selectors/truncated tokens
    /// and the excluded original HEX padding overflow fail with a content diagnostic.</summary>
    public string Expand(string source)
    {
        byte[] input = Bytes(source);
        var output = new StringBuilder();
        int cursor = 0;
        while (cursor < input.Length)
        {
            int relative = input.AsSpan(cursor).IndexOf((byte)'%');
            if (relative < 0)
            { output.Append(ClassicText.DecodeWindows1252(input.AsSpan(cursor))); break; }
            output.Append(ClassicText.DecodeWindows1252(input.AsSpan(cursor, relative)));
            cursor += relative + 1;
            ReadOnlySpan<byte> suffix = input.AsSpan(cursor);
            if (Prefix(suffix, "STRING"u8, insensitive: true))
            {
                int index = TextReader.ParseDecimal(suffix[6..]);
                string value = index is >= 0 and < TextParameters.StringCount ? strings[index]
                    : index == TextParameters.StringCount && profileHeight is not null ? profileHeight
                    : throw Invalid("String selector has no supplied value");
                int advance = index >= 10 ? 8 : 7;
                RequireAdvance(suffix, advance);
                // Parameters are semantic names/text and may contain Unicode from native content.
                // Only the authored classic source is constrained to its file encoding.
                RequireText(value);
                output.Append(value);
                cursor += advance;
            }
            else if (Prefix(suffix, "NUMBER"u8, insensitive: false))
            {
                int value = Number(suffix[6..]);
                RequireAdvance(suffix, 7);
                output.Append(value.ToString(CultureInfo.InvariantCulture));
                cursor += 7;
            }
            else if (Prefix(suffix, "HEX"u8, insensitive: false))
            {
                uint value = unchecked((uint)Number(suffix[3..]));
                RequireAdvance(suffix, 4);
                if (value > ushort.MaxValue)
                { throw Invalid("HEX value exceeds the admitted four-digit width"); }
                output.Append(value.ToString("x4", CultureInfo.InvariantCulture));
                cursor += 4;
            }
            else if (!suffix.IsEmpty && suffix[0] == '%')
            { output.Append('%'); ++cursor; }
            // Unknown token: retain the text after the removed marker, including a final empty suffix.
        }
        return output.ToString();
    }

    public OwnedList<string> Expand(OwnedList<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        return new(lines.Select(Expand));
    }

    private int Number(ReadOnlySpan<byte> suffix)
    {
        int index = TextReader.ParseDecimal(suffix);
        return index is >= 0 and < 6 ? numbers[index] : throw Invalid("Number selector has no supplied value");
    }
    private static void RequireAdvance(ReadOnlySpan<byte> suffix, int advance)
    {
        if (suffix.Length < advance)
        { throw Invalid("Truncated substitution token has no backed continuation"); }
    }
    private static bool Prefix(ReadOnlySpan<byte> text, ReadOnlySpan<byte> prefix, bool insensitive)
    {
        if (text.Length < prefix.Length)
        { return false; }
        for (int i = 0; i < prefix.Length; ++i)
        {
            byte value = text[i];
            if (insensitive && value is >= (byte)'a' and <= (byte)'z')
            { value -= (byte)('a' - 'A'); }
            if (value != prefix[i])
            { return false; }
        }
        return true;
    }
    private static byte[] Bytes(string value)
    {
        RequireText(value);
        return ClassicText.EncodeWindows1252(value, "classic-substitution");
    }
    private static void RequireText(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Contains('\0'))
        { throw Invalid("Embedded NUL is outside the admitted text domain"); }
    }
    private static InvalidDataException Invalid(string message) => new("Classic text substitution: " + message);
}

/// <summary>S02 Text's P04 preparation port. Resolves the current process parameter object
/// at the action boundary, including after a fresh-game/checkpoint replacement.</summary>
public sealed class ClassicScenarioText(ProcessContext process, Func<string?>? profileHeight = null)
{
    public OwnedList<string> Prepare(OwnedList<string> lines) =>
        ClassicTextExpansion.Capture(process.TextParameters, profileHeight?.Invoke()).Expand(lines);
}
