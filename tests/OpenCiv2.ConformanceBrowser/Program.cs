using System.Runtime.InteropServices.JavaScript;
using System.Text;
using System.Text.Json.Nodes;
using OpenCiv2.Conformance;
using OpenCiv2.Formats.Content;

namespace OpenCiv2.ConformanceBrowser;

public static partial class BrowserAdapter
{
    [JSExport]
    public static string Identity() => Adapter.Identity(typeof(BrowserAdapter).Assembly);
    [JSExport]
    public static string Run(string arguments, string input)
    {
        try
        {
            var array = ContentJson.Parse(Encoding.UTF8.GetBytes(arguments), "adapter-arguments") as JsonArray ?? throw new ArgumentException("Expected argument array");
            if (array.Count is < 1 or > 6) throw new ArgumentException("Wrong argument count");
            string[] args = array.Select(n => n?.GetValue<string>() ?? throw new ArgumentException("Null argument")).ToArray();
            return new JsonObject { ["exitCode"] = 0, ["stdout"] = Adapter.Run(args, input), ["stderr"] = "" }.ToJsonString();
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        { return new JsonObject { ["exitCode"] = 2, ["stdout"] = "", ["stderr"] = error.Message }.ToJsonString(); }
    }
}
internal static class Program { public static void Main() { } }
