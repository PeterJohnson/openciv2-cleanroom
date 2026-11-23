using System.Text.Json;
using OpenCiv2.FoundationCases;

if (args is ["--local-play-reference" or "--desktop-production-policy-reference", string saved, string continued])
{
    var reference = args[0] == "--local-play-reference" ? FoundationSuite.LocalPlayReference()
        : FoundationSuite.DesktopProductionPolicyReference();
    using (var output = new FileStream(saved, FileMode.CreateNew))
    {
        output.Write(reference.Item1);
    }

    using (var output = new FileStream(continued, FileMode.CreateNew))
    {
        output.Write(reference.Continued);
    }

    return 0;
}

if (args is ["--local-work-reference" or "--local-city-jobs-reference", string initial, string working, string completed])
{
    var reference = args[0] == "--local-work-reference" ? FoundationSuite.LocalTerrainWorkReference()
        : FoundationSuite.LocalCityTerrainGoalReference();
    foreach (var (path, bytes) in new[] { (initial, reference.Item1), (working, reference.Item2), (completed, reference.Item3) })
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
