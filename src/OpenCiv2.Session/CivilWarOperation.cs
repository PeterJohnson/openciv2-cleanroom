using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;

namespace OpenCiv2.Session;

/// <summary>Trusted V01 child. Once admitted, bounded creation/partition work completes despite cancellation.</summary>
public sealed class CivilWarOperation(CivilWar.Operation operation) : IContinuation
{
    private OperationStep? completed;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (completed is not null)
        {
            return completed;
        }

        int? result;
        try
        { result = operation.Advance(); }
        catch { context.Publish(); throw; }
        if (result is null)
        { context.Publish(); return new(CommandStatus.Pending); }
        var values = new OwnedList<NamedValue>([new("result", new CommandValue.Integer(result.Value))]);
        context.Effect("civilization.civil-war.completed", values);
        return completed = new(Result: values);
    }
    public void Dispose() => operation.Dispose();
}
