using System.Text.Json;
using OpenCiv2.FoundationCases;

if (args is ["--local-retirement-reference", string retirementInitial, string retirementHall])
{
    var reference = FoundationSuite.LocalRetirementReference();
    foreach (var (path, bytes) in new[] { (retirementInitial, reference.Initial), (retirementHall, reference.Hall) })
    {
        using var output = new FileStream(path, FileMode.CreateNew);
        output.Write(bytes);
    }
    return 0;
}

if (args is ["--hall-reference", string hallFile])
{
    using var output = new FileStream(hallFile, FileMode.CreateNew);
    output.Write(FoundationSuite.HallReference());
    return 0;
}

if (args is ["--local-score-reference", string scoreSaved, string scoreContinued])
{
    var reference = FoundationSuite.LocalScoreReference();
    foreach (var (path, bytes) in new[] { (scoreSaved, reference.Saved), (scoreContinued, reference.Continued) })
    {
        using var output = new FileStream(path, FileMode.CreateNew);
        output.Write(bytes);
    }
    return 0;
}

if (args is ["--local-population-reference", string populationInitial, string populationSaved, string populationContinued])
{
    var reference = FoundationSuite.LocalPopulationReference();
    foreach (var (path, bytes) in new[] { (populationInitial, reference.Initial), (populationSaved, reference.Saved),
        (populationContinued, reference.Continued) })
    {
        using var output = new FileStream(path, FileMode.CreateNew);
        output.Write(bytes);
    }
    return 0;
}

if (args is ["--local-government-reference", string anarchy, string government, string governmentContinued])
{
    var reference = FoundationSuite.LocalGovernmentReference();
    foreach (var (path, bytes) in new[] { (anarchy, reference.Initial), (government, reference.Selected),
        (governmentContinued, reference.Continued) })
    {
        using var output = new FileStream(path, FileMode.CreateNew);
        output.Write(bytes);
    }
    return 0;
}

if (args is ["--local-rates-reference", string initialRates, string savedRates, string continuedRates, string preferences])
{
    var reference = FoundationSuite.LocalRateReference();
    foreach (var (path, bytes) in new[] { (initialRates, reference.Initial), (savedRates, reference.Saved),
        (continuedRates, reference.Continued), (preferences, reference.Preferences) })
    {
        using var output = new FileStream(path, FileMode.CreateNew);
        output.Write(bytes);
    }
    return 0;
}

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
