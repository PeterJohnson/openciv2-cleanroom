using OpenCiv2.AutomationCases;

var report = AutomationReport.Run(typeof(Program).Assembly);
Console.WriteLine(report.Json());
return report.Passed ? 0 : 1;
