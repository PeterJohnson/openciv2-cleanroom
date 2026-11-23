using System.Globalization;
using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Units;
using OpenCiv2.Formats.Content;

namespace OpenCiv2.Session;

/// <summary>V08 §3 explicit score preparation on the session lane, retained across presentation.</summary>
public sealed class CivilizationScoreOperation : IContinuation
{
    private ScoreReport? report;
    private bool prepared;
    private InteractionSpec? decision;

    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (input.Kind is OperationInputKind.Cancel or OperationInputKind.Timeout)
        {
            return new(CommandStatus.Cancelled, Code: "cancelled");
        }

        if (LocalPlayCommands.ValidatePhase(context.Actor, context.World) is { } error)
        {
            return new(CommandStatus.Failed, Code: error);
        }

        if (!prepared)
        {
            var values = GameplayContent.Builtin.Values;
            var rules = context.Process.Rules;
            var score = new CivilizationScore(context.World, values.Scoring,
                new(context.World, rules.Definitions, values.Spaceship, values.Calendar),
                new(context.World, rules, new StackOperations(context.World, new(5_000_000, 64))));
            report = score.Prepare(context.Actor.Civilization.Value);
            prepared = true;
            if (report is not null)
            {
                context.Effect("civilization.score.prepared", Result(report));
            }

            decision = new(report is null ? "This game has already been scored." : Text(report), null, null, new([0]))
            { Options = new([new(0, "Close report")]) };
        }
        else if (input.Kind == OperationInputKind.Choice && input.Choice == 0)
        {
            return new(CommandStatus.Completed, report is null ? OwnedList<NamedValue>.Empty : Result(report));
        }

        return new(CommandStatus.AwaitingInteraction, Interaction: decision);
    }

    private static OwnedList<NamedValue> Result(ScoreReport report) => new([
        new("score", new CommandValue.Integer(report.Score)), new("rating", new CommandValue.Integer(report.Rating)),
        new("title", new CommandValue.Integer(report.TitleIndex))]);

    private static string Text(ScoreReport r)
    {
        string outcome = r.ObjectiveOutcome switch
        {
            ObjectiveOutcome.DecisiveVictory => "Decisive victory",
            ObjectiveOutcome.MarginalVictory => "Marginal victory",
            ObjectiveOutcome.Draw => "Draw",
            ObjectiveOutcome.MarginalDefeat => "Marginal defeat",
            _ => "Decisive defeat"
        };
        string body = r.Objective
            ? $"Objectives {r.CivObjectives} of {r.TotalObjectives}; objective civilization {r.ObjectiveCivValue}.\n"
                + $"Outcome: {outcome}.\nVictory: decisive {r.Thresholds[0]}, marginal {r.Thresholds[1]}.\n"
                + $"Defeat: marginal {r.Thresholds[2]}, decisive {r.Thresholds[3]}."
            : $"Citizens {r.Citizens} · Wonders {r.Wonders}\n"
                + $"Spaceship {r.SpaceshipArrived} · In flight {r.SpaceshipInFlight} (pending)\n"
                + $"Pollution {r.Pollution}" + (r.PeaceEligible ? $" · Peace {r.Peace}" : "")
                + $"\nFuture technologies {r.FutureTechs} · Barbarians {r.BarbarianBonus}";
        return "Civilization score\n" + body + $"\n{(r.UsesConquest ? "Conquest bonus" : "Total score")}: {r.Score}\n"
            + $"Civilization rating: {r.Rating.ToString(CultureInfo.InvariantCulture)}%"
            + (r.Scenario ? "\nScenario game" : "") + (r.Cheated ? "\nCheated game" : "");
    }
}
