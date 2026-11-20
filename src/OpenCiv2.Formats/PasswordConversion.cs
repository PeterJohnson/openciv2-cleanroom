using System.Globalization;
using System.Runtime.CompilerServices;
using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.State;

namespace OpenCiv2.Formats;

/// <summary>F06 password text, byte scrambling, and retained random padding belong to the adapter.</summary>
public static class PasswordConversion
{
    public const int PasswordWidth = 32;
    public const int ImageSize = EntityLimits.Civilizations * PasswordWidth;
    private static readonly ConditionalWeakTable<OwnedList<string>, byte[]> images = new();

    public static OwnedList<string> Decode(OwnedList<byte> image)
    {
        byte[] bytes = image.ToArray();
        DecodeBytes(bytes);
        var names = new OwnedList<string>(Enumerable.Range(0, EntityLimits.Civilizations)
            .Select(i => ClassicText.DecodeTerminated(bytes.AsSpan(i * PasswordWidth, PasswordWidth).ToArray())));
        images.Add(names, image.ToArray());
        return names;
    }

    public static OwnedList<byte> Encode(OwnedList<string> passwords)
    {
        if (passwords.Count != EntityLimits.Civilizations)
        {
            throw new ArgumentException("Expected eight passwords", nameof(passwords));
        }
        if (images.TryGetValue(passwords, out var image))
        {
            return new(image);
        }
        var bytes = new byte[ImageSize];
        for (int i = 0; i < EntityLimits.Civilizations; ++i)
        {
            byte[] text = ClassicText.EncodeField(passwords[i]);
            if (text.Length > PasswordWidth || text.Contains((byte)0))
            {
                throw new ArgumentException("Password cannot be represented in its classic field", nameof(passwords));
            }
            text.CopyTo(bytes, i * PasswordWidth);
        }
        EncodeBytes(bytes);
        return new(bytes);
    }

    // F06 §9 circular bit rotation across rows and the array seam, followed by index XOR.
    public static void EncodeBytes(Span<byte> bytes)
    {
        if (bytes.Length != ImageSize)
        {
            throw new ArgumentException("Passwords require 256 bytes", nameof(bytes));
        }

        Span<byte> prior = stackalloc byte[ImageSize];
        bytes.CopyTo(prior);
        for (int i = 0; i < ImageSize; ++i)
        {
            bytes[i] = unchecked((byte)(((prior[i] >> 3) | ((prior[(i + (ImageSize - 1)) % ImageSize] & 7) << 5)) ^ i));
        }
    }

    public static void DecodeBytes(Span<byte> bytes)
    {
        if (bytes.Length != ImageSize)
        {
            throw new ArgumentException("Passwords require 256 bytes", nameof(bytes));
        }

        Span<byte> prior = stackalloc byte[ImageSize];
        bytes.CopyTo(prior);
        for (int i = 0; i < ImageSize; ++i)
        {
            int next = (i + 1) % ImageSize;
            bytes[i] = unchecked((byte)(((prior[i] ^ i) << 3) | ((prior[next] ^ next) >> 5)));
        }
    }

    public static void ResetPasswords(ref MultiplayerState state, GameplayRandom random, string call)
    {
        ArgumentException.ThrowIfNullOrEmpty(call);
        ArgumentNullException.ThrowIfNull(random);
        byte[] bytes = Encode(state.Saved.Passwords).ToArray();
        // Detach adapter padding before the first draw so failure retains its prefix without changing
        // earlier snapshots. Logical passwords are replaced only after the reset completes.
        state.Saved.Passwords = new(state.Saved.Passwords);
        images.Add(state.Saved.Passwords, bytes);
        for (int i = 0; i < (ImageSize - 1); ++i)
        {
            var key = new ChoiceKey(
                "F06.password-reset.byte",
                call + "/row/" + (i / PasswordWidth).ToString(
                    CultureInfo.InvariantCulture) + "/byte/" + (i % PasswordWidth).ToString(
                        CultureInfo.InvariantCulture),
                0);
            bytes[i] = unchecked((byte)random.PrimaryCode(key));
        }
        for (int civ = 0; civ < EntityLimits.Civilizations; ++civ)
        {
            bytes[civ * PasswordWidth] = 0;
            state.Passwords.Pending[civ] = state.Passwords.Present[civ] = false;
        }
        EncodeBytes(bytes);
        state.Saved.Passwords = Decode(new(bytes));
    }
}
