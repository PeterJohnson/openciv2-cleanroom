using System.Runtime.InteropServices.JavaScript;
using OpenCiv2.FoundationCases;

namespace OpenCiv2.Browser.Tests;

public static partial class BrowserTests
{
    [JSExport]
    public static string Run() => FoundationSuite.RunJson(typeof(BrowserTests).Assembly);
}

internal static class Program
{
    public static void Main() { }
}
