using System.Runtime.InteropServices.JavaScript;
using OpenCiv2.PerformanceCases;

namespace OpenCiv2.PerformanceBrowser;

public static partial class BrowserWorkloads
{
    [JSExport]
    public static string Run(string inputs, int repetitions) => Workloads.Run(inputs, repetitions, typeof(BrowserWorkloads).Assembly);
}
internal static class Program
{
    public static void Main() { }
}
