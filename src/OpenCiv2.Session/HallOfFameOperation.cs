using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Formats;

namespace OpenCiv2.Session;

/// <summary>V08 §4 show/insert/clear continuation. An outcome caller supplies a civilization to record;
/// the ordinary viewer cannot create an entry or clear the ranking.</summary>
public sealed class HallOfFameOperation(HallOfFame hall, int? civilizationToRecord = null) : IContinuation
{
    private StoredHallRecord? current;
    private bool started, outputFailed;
    private int rank = -1;
    private InteractionSpec? decision;

    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (context.World.Globals.Multiplayer != GameMode.SinglePlayer)
        {
            return new(CommandStatus.Failed, Code: "single_player_required");
        }

        if (!started)
        {
            if (input.Kind is OperationInputKind.Cancel or OperationInputKind.Timeout)
            {
                return new(CommandStatus.Cancelled, Code: "cancelled");
            }

            if (civilizationToRecord is int owner)
            {
                current = HallOfFame.Prepare(context.World, context.Process, owner);
                hall.BeginInsertion();
                Insert(context);
            }
            started = true;
            Show();
        }
        else if (input.Kind is OperationInputKind.Cancel or OperationInputKind.Timeout
            || input.Kind == OperationInputKind.Choice && input.Choice == 0)
        {
            return new(outputFailed ? CommandStatus.Failed : CommandStatus.Completed, RankValue(), outputFailed ? "hall_output_failed" : "");
        }
        else if (current is not null && input.Kind == OperationInputKind.Choice && input.Choice is int choice && choice != 0)
        {
            hall.Clear();
            Insert(context); // Same prepared record and previous rank. No load/prepare at the loop head.
            Show();
        }
        return new(CommandStatus.AwaitingInteraction, Interaction: decision);
    }

    private OwnedList<NamedValue> RankValue() => new([new("rank", new CommandValue.Integer(rank))]);
    private void Insert(OperationContext context)
    {
        rank = hall.Insert(current!, rank);
        bool saved = hall.Save();
        outputFailed |= !saved;
        context.Effect(saved ? "hall.saved" : "hall.output_failed", RankValue());
    }

    private void Show()
    {
        var rows = hall.Show(rank);
        string body = rows.Count == 0 ? "No recorded games." : string.Join("\n\n", rows.Select(row =>
        {
            var r = row.Record;
            string difficulty = row.ValidDifficulty ? ((Difficulty)row.Difficulty).ToString() : "Difficulty unavailable";
            string date = row.Month is int month ? $"Month {month + 1}, year {row.DisplayYear}"
                : $"{row.DisplayYear} {(row.BeforeChrist ? "BC" : "AD")}";
            return $"{row.Rank + 1}. {row.Leader} · {row.Civilization}{(row.Rank == rank ? " · Current game" : "")}\n"
                + $"Score {r.Score} · Rating {r.Rating}% · {difficulty}\n{date} · "
                + (row.Objectives ? $"Objectives {r.Objectives}" : $"Population {(int)r.Population * 10000:N0}")
                + (r.Scenario != 0 ? " · Scenario" : "") + (row.Cheated ? " · Cheated" : "");
        }));
        InteractionOption[] options = rank >= 0 ? [new(0, "Close"), new(1, "Clear and retain current game")] : [new(0, "Close")];
        decision = new("Hall of Fame\n" + body, null, null, new(options.Select(o => o.Value))) { Options = new(options) };
    }
}
