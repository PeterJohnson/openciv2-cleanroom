using System.Reflection;

namespace OpenCiv2.Base;

/// <summary>Read one explicitly supplied assembly's build-time manifest; no assembly discovery.</summary>
public static class BuildIdentity
{
    public static CompiledInputs Read(Assembly assembly)
    {
        using Stream resource = assembly.GetManifestResourceStream("OpenCiv2.BuildIdentity")
            ?? throw new InvalidOperationException("Assembly has no compiled source identity.");
        using var reader = new StreamReader(resource);
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        while (reader.ReadLine() is { } line)
        {
            int separator = line.IndexOf('=');
            if (separator < 1 || !fields.TryAdd(line[..separator].Replace('\\', '/'), line[(separator + 1)..]))
            {
                throw new InvalidOperationException("Malformed compiled source identity.");
            }
        }
        if (!fields.Remove("format", out string? format) || format != "openciv2-compiled-inputs/1")
        {
            throw new InvalidOperationException("Unknown compiled source identity.");
        }

        string Take(string name) => fields.Remove(name, out string? value) && value.Length != 0
            ? value : throw new InvalidOperationException($"Missing build identity {name}.");
        string sdk = Take(
            "sdk"), runtime = Take(
                "runtime"), target = Take(
                    "target"), configuration = Take(
                        "configuration");
        return new(assembly.GetName().Name!, sdk, runtime, target, configuration,
            fields.ToDictionary(p => p.Key, p => p.Value.ToLowerInvariant(), StringComparer.Ordinal));
    }
}

// These are reporting values, not mutable game/client observations.
public sealed record CompiledInputs(string Assembly, string Sdk, string Runtime, string Target,
    string Configuration, Dictionary<string, string> Sources);
