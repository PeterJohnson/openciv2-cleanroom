using System.Text.Json;
using OpenCiv2.FoundationCases;

if (args is ["--local-play-reference", string saved, string continued])
{
    var reference = FoundationSuite.LocalPlayReference();
    using (var output = new FileStream(saved, FileMode.CreateNew))
    {
        output.Write(reference.Saved);
    }

    using (var output = new FileStream(continued, FileMode.CreateNew))
    {
        output.Write(reference.Continued);
    }

    return 0;
}

if (args is ["--local-work-reference", string initial, string working, string completed])
{
    var reference = FoundationSuite.LocalTerrainWorkReference();
    foreach (var (path, bytes) in new[] { (initial, reference.Initial), (working, reference.Working), (completed, reference.Completed) })
    {
        using var output = new FileStream(path, FileMode.CreateNew);
        output.Write(bytes);
    }
    return 0;
}

string report = FoundationSuite.RunJson(typeof(Program).Assembly);
Console.WriteLine(report);
using var parsed = JsonDocument.Parse(report);
return parsed.RootElement.GetProperty("Passed").GetBoolean() ? 0 : 1;
