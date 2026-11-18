using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using OpenCiv2.Base;

namespace OpenCiv2.App;

public static class ApplicationIdentity
{
    public static string Read(Assembly host)
    {
        var assemblies = new[] { typeof(Arithmetic).Assembly, typeof(OpenCiv2.IO.CoreFile).Assembly,
            typeof(OpenCiv2.Contracts.CommandRequest).Assembly, typeof(OpenCiv2.Core.Geometry).Assembly,
            typeof(OpenCiv2.Formats.StoredSave).Assembly, typeof(OpenCiv2.Query.StoredSnapshot).Assembly,
            typeof(OpenCiv2.Network.Messages).Assembly, typeof(OpenCiv2.Session.SessionRuntime).Assembly,
            typeof(OpenCiv2.ViewModels.SessionPanel).Assembly, typeof(OpenCiv2.Console.CommandText).Assembly,
            typeof(SeamApp).Assembly, host };
        return new JsonObject
        {
            ["format"] = "openciv2-application-seam/1",
            ["build"] = new JsonArray(assemblies.Distinct()
            .Select(a => JsonSerializer.SerializeToNode(BuildIdentity.Read(a), AppJson.Default.CompiledInputs)).ToArray())
        }.ToJsonString();
    }
}
[JsonSerializable(typeof(CompiledInputs))]
internal partial class AppJson : JsonSerializerContext;
