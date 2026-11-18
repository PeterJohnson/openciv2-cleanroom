using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenCiv2.Base;

namespace OpenCiv2.AutomationCases;

public sealed record ScriptCaseResult(string Name, bool Passed, string? Error);
public sealed record ScriptOutcome(string Stack, int Effects, bool Changed, string Output);
public sealed record AutomationReport(string Format, bool Passed, ScriptCaseResult[] Cases, ScriptOutcome? Outcome, CompiledInputs[] Build)
{
    public static AutomationReport Run(Assembly host)
    {
        var results = AutomationSuite.Names.Select(name =>
        {
            try { AutomationSuite.Run(name); return new ScriptCaseResult(name, true, null); }
            catch (Exception error) { return new ScriptCaseResult(name, false, error.ToString()); }
        }).ToArray();
        var assemblies = new[] { typeof(Arithmetic).Assembly, typeof(OpenCiv2.IO.CoreFile).Assembly,
            typeof(OpenCiv2.Contracts.CommandRequest).Assembly, typeof(OpenCiv2.Core.Geometry).Assembly,
            typeof(OpenCiv2.Formats.StoredSave).Assembly, typeof(OpenCiv2.Query.StoredSnapshot).Assembly,
            typeof(OpenCiv2.Network.Messages).Assembly, typeof(OpenCiv2.Session.SessionRuntime).Assembly,
            typeof(OpenCiv2.Console.CommandText).Assembly, typeof(OpenCiv2.Automation.ScriptTask).Assembly,
            typeof(AutomationSuite).Assembly, host };
        bool passed = results.All(r => r.Passed);
        return new("openciv2-production-lua/1", passed, results, passed ? AutomationSuite.Outcome() : null,
            assemblies.Distinct().Select(BuildIdentity.Read).ToArray());
    }
    public string Json() => JsonSerializer.Serialize(this, AutomationJson.Default.AutomationReport);
}
[JsonSerializable(typeof(AutomationReport))]
internal partial class AutomationJson : JsonSerializerContext;
