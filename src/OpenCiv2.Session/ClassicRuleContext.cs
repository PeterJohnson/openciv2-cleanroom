using OpenCiv2.Core.Rules;
using OpenCiv2.Formats.Classic;
using ClassicReader = OpenCiv2.Formats.Classic.TextReader;

namespace OpenCiv2.Session;

/// <summary>F03/F06 owned classic content source. Keeps resource selection separate from live installation.</summary>
public sealed class ClassicRuleContext : IDisposable
{
    private readonly ClassicFileState files;
    private readonly Func<string, string, bool> exists;
    private readonly TextCase comparison;
    private readonly Action? missingLabels;
    private readonly Action<RuleLoadException>? ruleFailure;
    private readonly ClassicReader reader;
    private bool disposed;

    public ClassicRuleContext(
        ClassicFileState files,
        TextWorkspace workspace,
        TextFiles textFiles,
        Func<string, string, bool> exists,
        TextCase comparison,
        Action? missingLabels = null,
        Action<RuleLoadException>? ruleFailure = null)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(exists);
        ArgumentNullException.ThrowIfNull(comparison);
        this.files = files;
        this.exists = exists;
        this.comparison = comparison;
        this.missingLabels = missingLabels;
        this.ruleFailure = ruleFailure;
        reader = new(workspace, files.Text, textFiles, comparison);
    }

    public LoadedContent Load()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        string search = files.Text.Search;
        try
        {
            var labels = ClassicLabels.Load(reader, files.Text, missingLabels);
            files.Text.Search = exists(files.ScenarioDirectory, "RULES." + files.Text.Extension)
                ? files.ScenarioDirectory
                : files.Text.Home;
            var rules = new RuleLoader(reader, comparison, ruleFailure).Load("RULES");
            reader.Close();
            return new(rules, labels);
        }
        finally
        {
            files.Text.Search = search;
            reader.Dispose();
        }
    }

    public void Dispose()
    {
        if (!disposed)
        {
            disposed = true;
            reader.Dispose();
        }
    }
}
