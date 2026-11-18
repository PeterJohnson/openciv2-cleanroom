using OpenCiv2.Contracts;
using OpenCiv2.Session;
using OpenCiv2.ViewModels;

namespace OpenCiv2.App;

/// <summary>Composition root for the explicitly authored P5 integration world. All mechanics,
/// command lifecycles and observations come from the production session.</summary>
internal sealed class SeamSession : IDisposable
{
    private readonly SessionRuntime runtime = new(new(), new(1));
    public SessionPanel Panel { get; } = new();
    public SeamSession()
    {
        runtime.RegisterCommand(LocalUnitCommands.StackBottom()); runtime.BeginClientSession(new(17, 23));
        runtime.World.Map.Allocate(new(8, 6)); runtime.World.Entities.Units.SetEnd(4); runtime.World.Globals.UnitEnd = 4;
        for (int i = 0; i < 4; ++i) runtime.World.Entities.Units.Storage[i] = new()
        {
            Token = new((uint)i + 100),
            Owner = new((byte)(i == 3 ? 2 : 1)),
            X = 2,
            Y = 2,
            Previous = new((short)(i == 3 ? -1 : i - 1)),
            Next = new((short)(i >= 2 ? -1 : i + 1))
        };
        Panel.Bind(runtime.Bind(runtime.AddParticipant(new(1)), ControllerKind.Frontend));
    }
    public void Tick()
    {
        if (runtime.AdvanceOne()) Panel.Refresh();
        if (Panel.View.Command is { } command && command.Status.Terminal()) runtime.RetireCommand(command.Correlation.Command);
    }
    public void Dispose() { Panel.Bind(null); runtime.Dispose(); }
}
