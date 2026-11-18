using System.Runtime.InteropServices.JavaScript;
using OpenCiv2.AutomationCases;

namespace OpenCiv2.AutomationBrowser;

public static partial class BrowserTests
{
    [JSExport]
    public static string Run() => AutomationReport.Run(typeof(BrowserTests).Assembly).Json();
}
internal static class Program { public static void Main() { } }
