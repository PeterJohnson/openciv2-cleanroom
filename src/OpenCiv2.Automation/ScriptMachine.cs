using System.Text.Json.Nodes;

namespace OpenCiv2.Automation;

public enum ScriptMachineState { Waiting, Request, Completed, Failed }

public readonly record struct ScriptMachineResult(ScriptMachineState State, string Operation = "",
    string Argument = "", string Error = "");

/// <summary>A Lua execution boundary. Requests always return to the owner before game dispatch.</summary>
public interface IScriptMachine : IDisposable
{
    /// <summary>Starts or polls one resume. A waiting result has not consumed another reply.</summary>
    ScriptMachineResult Resume(JsonNode? reply);
}
