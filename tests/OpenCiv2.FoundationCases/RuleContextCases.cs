using OpenCiv2.Core.Rules;
using OpenCiv2.Formats;
using OpenCiv2.Formats.Classic;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static void ClassicRuleContextSelectionAndOwnership()
    {
        var files = new ClassicFileState { ScenarioDirectory = "scenario" }; files.Text.Working = "caller"; files.Text.Search = files.Text.Home = "home";
        var sources = new Dictionary<string, string> { ["home/LABELS.TXT"] = LabelTable(1, "Home"), ["home/RULES.TXT"] = string.Concat(ClassicRuleSections()), ["scenario/RULES.TXT"] = string.Concat(ClassicRuleSections()).Replace("@COSMIC\n3\n", "@COSMIC\n4\n", StringComparison.Ordinal) };
        var traces = new List<TextTrace>(); var opens = new List<string>(); bool scenario = true;
        var services = new TextFiles((directory, name) =>
        {
            Equal(directory, files.Text.Working); string key = directory + "/" + name; opens.Add(key); if (!sources.TryGetValue(key, out string? bytes)) return new();
            var trace = new TextTrace(); traces.Add(trace); return new(key, new TracedText(ByteText(bytes), trace));
        });
        using var context = new ClassicRuleContext(files, new(), services, (directory, name) => { Equal("scenario", directory); Equal("RULES.TXT", name); return scenario; }, TextCase.Ascii);
        var first = context.Load(); Equal((byte)4, first.Rules.Definitions.Cosmic.RoadMovementMultiplier); Equal("Home0", first.Labels!.Entries[0]); Equal("home", files.Text.Search); Equal("caller", files.Text.Working); Equal(true, traces.All(t => t.Closes == 1)); Equal(true, opens.Contains("scenario/RULES.TXT"));
        scenario = false; opens.Clear(); var second = context.Load(); Equal((byte)3, second.Rules.Definitions.Cosmic.RoadMovementMultiplier); Equal(false, opens.Contains("scenario/RULES.TXT")); Equal(true, traces.All(t => t.Closes == 1));
        sources.Clear(); Equal((byte)4, first.Rules.Definitions.Cosmic.RoadMovementMultiplier); Equal("Home0", second.Labels!.Entries[0]); context.Dispose(); Throws<ObjectDisposedException>(() => context.Load());
    }
    private static void ClassicRuleContextFailureCleanup()
    {
        var files = new ClassicFileState { ScenarioDirectory = "scenario" }; files.Text.Working = "caller"; files.Text.Search = "selected"; files.Text.Home = "home";
        var traces = new List<TextTrace>(); bool missing = false, shortRules = false; int failures = 0;
        var services = new TextFiles((directory, name) =>
        {
            if (missing) return new(); var trace = new TextTrace(); traces.Add(trace);
            return new(directory + "/" + name, new TracedText(ByteText(name == "LABELS.TXT" ? LabelTable(888) : shortRules ? "@COSMIC\n3\n" : ClassicRuleSections()[0]), trace));
        });
        using var context = new ClassicRuleContext(files, new(), services, (_, _) => true, TextCase.Ascii, () => throw new IOException("labels"), _ => ++failures);
        Throws<RuleLoadException>(() => context.Load()); Equal(1, failures); Equal(true, traces.All(t => t.Closes == 1)); Equal("selected", files.Text.Search); Equal("caller", files.Text.Working);
        shortRules = true; Throws<TextException>(() => context.Load()); Equal(1, failures); Equal(true, traces.All(t => t.Closes == 1)); Equal("selected", files.Text.Search); Equal("caller", files.Text.Working);
        missing = true; Throws<IOException>(() => context.Load()); Equal("selected", files.Text.Search); Equal("caller", files.Text.Working); Equal(1, failures);
    }
}
