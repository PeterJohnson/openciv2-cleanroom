using System.Globalization;
using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Formats.Content;

namespace OpenCiv2.Session;

/// <summary>V07 §7 text status and retained launch confirmation. The modal's accepted extra
/// result uses live shared success and does not revalidate launch eligibility.</summary>
public sealed class SpaceshipStatusOperation(int owner, bool allowLaunch,
    Func<OperationContext, SpaceshipConstruction> construction, ParticipantId? recipient = null,
    SpaceshipValues? values = null, CalendarValues? calendar = null) : IContinuation
{
    private InteractionSpec? prompt;
    private bool confirming, disposed, faulted;
    private OperationStep? completed;
    public SpaceshipReport? Report { get; private set; }

    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (faulted)
        { throw new InvalidOperationException("A failed spaceship report cannot resume"); }
        if (completed is not null)
        { return completed; }
        try
        {
            if (Report is null)
            {
                Report = SpaceshipReport.Prepare(context.World, context.Process.Rules.Definitions,
                    values ?? GameplayContent.Builtin.Values.Spaceship, calendar ?? GameplayContent.Builtin.Values.Calendar,
                    owner, allowLaunch);
                var options = new List<InteractionOption> { new(0, "Close report") };
                if (Report.CanLaunch)
                { options.Add(new(1, "Launch spaceship")); }
                prompt = new(Text(context, Report), recipient, null, new(options.Select(o => o.Value)))
                { Options = new(options) };
                context.Publish();
            }
            else if (input.Kind is OperationInputKind.Cancel or OperationInputKind.Timeout
                || input.Kind == OperationInputKind.Choice && input.Choice == 0)
            { return completed = new(); }
            else if (input.Kind == OperationInputKind.Choice && input.Choice is not null)
            {
                if (confirming)
                {
                    construction(context).Launch(owner);
                    context.Effect("spaceship.launched", new([new("civilization", new CommandValue.Integer(owner))]));
                    return completed = new();
                }
                // Session validates normal offered choices. A trusted custom modal's extra result
                // may be any nonzero value, even when callbacks changed the report's eligibility.
                int success = context.World.Spaceship.Success;
                prompt = new($"Launch spaceship?\nSuccess probability: {success.ToString(CultureInfo.InvariantCulture)}%",
                    recipient, null, new([0, 1]))
                { Options = new([new(0, "Keep building"), new(1, "Launch")]) };
                confirming = true;
            }
            return new(CommandStatus.AwaitingInteraction, Interaction: prompt);
        }
        catch { faulted = true; context.Publish(); throw; }
    }

    private static string Text(OperationContext context, SpaceshipReport report)
    {
        string[] names = ["Structural", "Propulsion", "Fuel", "Habitation", "Life support", "Solar energy"];
        var lines = new List<string> { $"{OutcomeText.Adjective(context, report.Civilization)} spaceship",
            $"{OutcomeText.Leader(context, report.Civilization)} · {OutcomeText.Plural(context, report.Civilization)}" };
        for (int i = 0; i < 6; ++i)
        { lines.Add($"{names[i]}: {report.Parts[i]}{(i == 0 && report.OverCapacity ? "*" : "")}"); }
        lines.AddRange(["", $"Population: {(long)report.Population * 10000}", $"Support: {report.Support}%",
            $"Energy: {report.Energy}%", $"Mass: {(long)report.Mass * 100} tons",
            $"Fuel: {report.Fuel}%{(report.Fusion ? " (Fusion power)" : "")}",
            $"Flight time: {(report.FlightTime / 10m).ToString("0.0", CultureInfo.InvariantCulture)} years", $"Success: {report.Success}%"]);
        if (report.Launched)
        { lines.Add(report.Arrived ? "Arrived" : $"In flight · Arrival {OutcomeText.Date(context.World, report.ArrivalYear)}"); }
        return string.Join("\n", lines);
    }

    public void Dispose() { disposed = true; }
}
