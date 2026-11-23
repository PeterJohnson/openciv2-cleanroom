using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Formats.Content;

namespace OpenCiv2.Session;

/// <summary>P07 §14 read-only history report. Presentation never repopulates A01 history.</summary>
public sealed class PowerHistoryOperation : IContinuation
{
    private InteractionSpec? decision;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (decision is null)
        {
            var report = PowerHistory.Prepare(context.World, context.Process, GameplayContent.Builtin.Values.Calendar);
            decision = new("Power history\n" + (report.Dates.Count == 0 ? "No completed history samples." :
                $"{report.Dates.Count} samples, every {report.Interval} turns. Includes extinct civilizations."), null, null, new([0]))
            { Options = new([new(0, "Close history")]), PowerHistory = report };
        }
        else if (input.Kind is OperationInputKind.Choice or OperationInputKind.Cancel or OperationInputKind.Timeout)
        {
            return new();
        }
        return new(CommandStatus.AwaitingInteraction, Interaction: decision);
    }
}

/// <summary>V08 §3 direct rating consumes shared score without recalculating cities or score.</summary>
public sealed class CivilizationRatingOperation(int civilization) : IContinuation
{
    private InteractionSpec? decision;
    private OwnedList<NamedValue> result = OwnedList<NamedValue>.Empty;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (decision is null)
        {
            CivilizationScoreOperation.CreateScore(context).PrepareRating();
            var score = context.World.Score;
            var name = context.Process.Rules.Leaders[context.World.Civilizations[civilization].Tribe].LeaderName;
            string ruler = name.Custom ? context.World.Names[civilization].Leader : name.Text;
            result = new([new("rating", new CommandValue.Integer(score.Rating)), new("title", new CommandValue.Integer(score.TitleIndex))]);
            context.Effect("civilization.rating.prepared", result);
            decision = new($"Civilization rating\n{ruler}\n{score.Rating}%", null, null, new([0]))
            { Options = new([new(0, "Close rating")]) };
        }
        else if (input.Kind is OperationInputKind.Choice or OperationInputKind.Cancel or OperationInputKind.Timeout)
        {
            return new(Result: result);
        }
        return new(CommandStatus.AwaitingInteraction, Interaction: decision);
    }
}

/// <summary>V08 §§6–7 common report tail. Read Scored after history and local human before each child.</summary>
public sealed class EndGameReports(HallOfFame hall) : IContinuation
{
    private IContinuation? child;
    private int stage;
    private string failure = "";
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        while (true)
        {
            if (child is null)
            {
                if (stage == 1 && context.World.Globals.Status.Scored)
                { stage = 4; }
                child = stage++ switch
                {
                    0 => new PowerHistoryOperation(),
                    1 => new CivilizationScoreOperation(context.World.Multiplayer.LocalHuman, includeRating: false),
                    2 => new CivilizationRatingOperation(context.World.Multiplayer.LocalHuman),
                    3 => new HallOfFameOperation(hall, context.World.Multiplayer.LocalHuman),
                    _ => null
                };
                input = new();
                if (child is null)
                { return new(failure.Length == 0 ? CommandStatus.Completed : CommandStatus.Failed, Code: failure); }
            }
            var next = child.Resume(context, input);
            if (!next.Status.Terminal())
            { return next; }
            if (next.Status == CommandStatus.Failed && failure.Length == 0)
            { failure = next.Code; }
            child.Dispose();
            child = null;
        }
    }
    public void Dispose() { child?.Dispose(); child = null; }
}
