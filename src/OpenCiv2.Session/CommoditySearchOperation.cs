using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.City;

namespace OpenCiv2.Session;

/// <summary>C07 private commodity search, using actual report filtering and retained decisions.</summary>
public sealed class CommoditySearchOperation(int? viewer = null, ParticipantId? recipient = null) : IContinuation
{
    private CommoditySearch? operation;
    private CommoditySearchDecision? pending;
    private InteractionSpec? prompt;
    private OperationStep? completed;
    private bool disposed, faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (faulted)
        {
            throw new InvalidOperationException("A failed commodity search presentation cannot resume");
        }

        if (completed is not null)
        {
            return completed;
        }

        try
        {
            operation ??= new(context.World, context.Process.Rules.Definitions, viewer ?? context.Actor.Civilization.Value);
            int? reply = input.Kind is OperationInputKind.Cancel or OperationInputKind.Timeout ? -1
                : input.Kind == OperationInputKind.Choice ? input.Choice : null;
            var previous = pending;
            pending = operation.Resume(reply);
            if (operation.Completed)
            {
                return completed = new();
            }

            if (!ReferenceEquals(pending, previous))
            {
                var who = recipient ?? context.Actor.Participant;
                List<InteractionOption> options = [];
                string text;
                if (pending is CommoditySearchMenu menu)
                {
                    text = "Search commodities";
                    options.AddRange(menu.Options.Select(o => new InteractionOption(o.Commodity, o.Name)));
                }
                else if (pending is CommoditySearchReport report)
                {
                    text = report.Name + " supply and demand\n";
                    if (report.Cities.Count == 0)
                    {
                        text += "No known cities supply or demand this commodity.";
                        options.Add(new(0, "Search again"));
                    }
                    else
                    {
                        text += string.Join("\n", report.Cities.Select(row => $"{row.Name} ({(row.Adjective is null ? "" : row.Adjective + ", ")}{row.Size}): "
                            + (row.Demand ? "Demands " : "Supplies ") + report.Name));
                        text += "\nSelect a listed city to return to commodity search.";
                        options.AddRange(report.Cities.DistinctBy(row => row.City.Slot).Select(row => new InteractionOption(row.City.Slot.Value, row.Name)));
                    }
                }
                else
                {
                    throw new InvalidOperationException("Unknown commodity search decision");
                }

                options.Add(new(-1, "Close search"));
                prompt = new(text, who, null, new(options.Select(o => o.Value))) { Options = new(options) };
                context.Publish();
            }
            return new(CommandStatus.AwaitingInteraction, Interaction: prompt);
        }
        catch { faulted = true; operation?.Dispose(); context.Publish(); throw; }
    }
    public void Dispose() { disposed = true; operation?.Dispose(); }
}
