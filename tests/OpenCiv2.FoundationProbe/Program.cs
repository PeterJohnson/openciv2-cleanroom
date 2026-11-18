using System.Text.Json;
using OpenCiv2.FoundationCases;

string report = FoundationSuite.RunJson(typeof(Program).Assembly);
Console.WriteLine(report);
using var parsed = JsonDocument.Parse(report);
return parsed.RootElement.GetProperty("Passed").GetBoolean() ? 0 : 1;
