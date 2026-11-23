using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.State;
using OpenCiv2.Formats.Content;

namespace OpenCiv2.Session;

internal static class OutcomeText
{
    public static string Leader(OperationContext c, int owner)
    {
        var value = c.Process.Rules.Leaders[c.World.Civilizations[owner].Tribe].LeaderName;
        return value.Custom ? c.World.Names[owner].Leader : value.Text;
    }
    public static string Plural(OperationContext c, int owner)
    {
        var value = c.Process.Rules.Leaders[c.World.Civilizations[owner].Tribe].Plural;
        return value.Custom ? c.World.Names[owner].Tribe : value.Text;
    }
    public static string Adjective(OperationContext c, int owner)
    {
        var value = c.Process.Rules.Leaders[c.World.Civilizations[owner].Tribe].Adjective;
        return value.Custom ? c.World.Names[owner].Adjective : value.Text;
    }
    public static string Title(OperationContext c, int owner)
    {
        var civ = c.World.Civilizations[owner];
        var value = c.Process.Rules.Leaders[civ.Tribe].Titles[(int)civ.Government][civ.Flags.FemaleLeader ? 1 : 0];
        return value.Custom ? c.World.Names[owner].Titles[(int)civ.Government] : value.Text;
    }
    public static string Date(GameState world, int value) => world.Globals.Status.Scenario && world.Scenario.YearIncrement < 0
        ? $"Month {Math.Abs(value % 12) + 1}, year {Math.Abs(value / 12)}"
        : $"{Math.Abs((long)value)} {(value < 0 ? "BC" : "AD")}";
    public static InteractionSpec Notice(string text) => new(text, null, null, new([0]), Cancellable: false)
    { Options = new([new(0, "Continue")]) };
    public static bool Acknowledged(OperationInput input) => input.Kind is OperationInputKind.Choice or OperationInputKind.Cancel or OperationInputKind.Timeout;
}

/// <summary>V08 §5 single-player equality check. No one-shot or autoplay suppression; completion follows the notice.</summary>
public sealed class ScheduledRetirementOperation : IContinuation
{
    private bool checkedSchedule, ending;
    private InteractionSpec? notice;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        var world = context.World;
        if (!checkedSchedule)
        {
            if (world.Globals.Multiplayer != GameMode.SinglePlayer)
            { return new(CommandStatus.Failed, Code: "single_player_required"); }
            checkedSchedule = true;
            if (world.Globals.Status.Won || world.PendingEndReason != EndReason.None)
            { return new(); }
            bool scenario = world.Globals.Status.Scenario && world.Scenario.MaximumTurns != 0;
            bool warning = scenario ? world.Globals.Turn == world.Scenario.MaximumTurns - 5 : world.Globals.Year == 2000;
            ending = !warning && (scenario ? world.Globals.Turn == world.Scenario.MaximumTurns : world.Globals.Year == 2020);
            if (!warning && !ending)
            { return new(); }
            int owner = world.Multiplayer.LocalHuman;
            string ruler = (OutcomeText.Title(context, owner) + " " + OutcomeText.Leader(context, owner)).Trim();
            string heading = ending ? scenario ? "Scenario ended" : "Scheduled retirement" : scenario ? "Scenario warning" : "Retirement warning";
            notice = OutcomeText.Notice(heading + "\n" + ruler + "\n" +
                (scenario ? ending ? "The scenario turn limit has been reached." : "The scenario ends in five turns."
                    : ending ? "The year is 2020. Your civilization's final results are ready." : "The year is 2000. Retirement is scheduled for 2020."));
            context.Effect(ending ? "game.retirement.notice" : "game.retirement.warning", OwnedList<NamedValue>.Empty);
        }
        else if (notice is not null && OutcomeText.Acknowledged(input))
        {
            if (ending)
            {
                world.Globals.Status.Won = true;
                world.PendingEndReason = EndReason.Retirement;
                context.Effect("game.retirement.posted", OwnedList<NamedValue>.Empty);
            }
            return new();
        }
        return notice is null ? new() : new(CommandStatus.AwaitingInteraction, Interaction: notice);
    }
}

/// <summary>F04 arrival notification before the final Won publication.</summary>
public sealed class SpaceshipArrivalOperation : IContinuation
{
    private bool started;
    private SpaceshipArrivalNotice? arrival;
    private InteractionSpec? notice;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (!started)
        {
            started = true;
            arrival = SpaceshipArrival.Begin(context.World);
            if (arrival is null)
            { return new(); }
            context.Effect("spaceship.arrived", new([new("civilization", new CommandValue.Integer(arrival.Civilization))]));
            if (arrival.ShowLanding)
            {
                notice = OutcomeText.Notice($"Spaceship landing\nThe {OutcomeText.Plural(context, arrival.Civilization)} spaceship has arrived.");
                return new(CommandStatus.AwaitingInteraction, Interaction: notice);
            }
        }
        else if (notice is not null && !OutcomeText.Acknowledged(input))
        { return new(CommandStatus.AwaitingInteraction, Interaction: notice); }
        if (arrival is not null)
        { SpaceshipArrival.Complete(context.World); context.Publish(); }
        return new();
    }
}

/// <summary>P10 semantic ending information. This desktop profile has no authored ending text or movie provider.</summary>
public sealed class OutcomePresentationOperation(EndReason reason, int owner, bool competing = false) : IContinuation
{
    private InteractionSpec? notice;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (notice is null)
        {
            var world = context.World;
            string text;
            if (reason is EndReason.LocalSpace or EndReason.OtherSpace)
            {
                ref var ship = ref world.Civilizations[owner];
                string leader = OutcomeText.Leader(context, competing ? world.Multiplayer.LocalHuman : owner);
                text = (competing ? "Another civilization reaches Alpha Centauri" : "Arrival at Alpha Centauri")
                    + $"\n{OutcomeText.Adjective(context, owner)} spaceship\nLaunched {OutcomeText.Date(world, ship.SpaceshipLaunchYear)}"
                    + $"\nPopulation {(long)ship.SpaceshipPopulation * 10000:N0}\nArrival {OutcomeText.Date(world, ship.SpaceshipArrivalYear)}\n{leader}";
            }
            else if (reason == EndReason.Conquest)
            {
                var history = world.Eliminations;
                var lines = new List<string>();
                for (int i = 0; i < Math.Clamp((int)history.Count, 0, EliminationHistory.Capacity); ++i)
                {
                    if (history.Killers[i] == owner)
                    {
                        int year = Calendar.Year(history.Turns[i], world.Globals, world.Scenario, GameplayContent.Builtin.Values.Calendar);
                        lines.Add($"{history.Names[i]} · {OutcomeText.Date(world, year)}");
                    }
                }
                text = "Conquered civilizations\n" + (lines.Count == 0 ? "No recorded conquests." : string.Join("\n", lines));
            }
            else
            {
                int advance = Enumerable.Range(0, RuleLimits.Advances).FirstOrDefault(i => world.Civilizations[owner].Advances.Contains(i), -1);
                text = $"Defeat\n{OutcomeText.Leader(context, owner)} · {OutcomeText.Plural(context, owner)}"
                    + (advance < 0 ? "" : $"\nKnown advance: {context.Process.Rules.Definitions.Advances[advance].Identity.Name}");
            }
            notice = OutcomeText.Notice(text + "\nEnding artwork and narrative text are unavailable.");
        }
        else if (OutcomeText.Acknowledged(input))
        { return new(); }
        return new(CommandStatus.AwaitingInteraction, Interaction: notice);
    }
}

/// <summary>V08 §6 pending-outcome dispatch. The caller acts on Quit; this operation clears only the pending reason.</summary>
public sealed class PendingOutcomeOperation(HallOfFame hall) : IContinuation
{
    private IContinuation? child;
    private int stage;
    private InteractionSpec? continuation;
    private string failure = "";
    public bool Quit { get; private set; }
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        while (stage < 2 || child is not null)
        {
            if (child is null)
            {
                var world = context.World;
                var reason = world.PendingEndReason;
                int winner = world.SpaceshipWinner, local = world.Multiplayer.LocalHuman;
                child = stage++ == 0 ? reason switch
                {
                    EndReason.LocalSpace or EndReason.OtherSpace => new OutcomePresentationOperation(reason, winner, winner != 0 && winner != local),
                    EndReason.Conquest or EndReason.Defeat => new OutcomePresentationOperation(reason, local),
                    _ => null
                } : new EndGameReports(hall);
                input = new();
                if (child is null)
                { continue; }
            }
            var result = child.Resume(context, input);
            if (!result.Status.Terminal())
            { return result; }
            if (result.Status == CommandStatus.Failed && failure.Length == 0)
            { failure = result.Code; }
            child.Dispose();
            child = null;
        }
        if (continuation is not null)
        {
            if (!OutcomeText.Acknowledged(input))
            { return new(CommandStatus.AwaitingInteraction, Interaction: continuation); }
            Quit = input.Choice is null or 0;
            context.World.Globals.Status.Scored = true;
        }
        else
        {
            var world = context.World;
            var reason = world.PendingEndReason;
            if (world.Globals.Multiplayer != GameMode.SinglePlayer)
            { Quit = true; }
            else if (reason is EndReason.LocalSpace or EndReason.OtherSpace or EndReason.Conquest or EndReason.Defeat
                || reason == EndReason.Retirement && world.Globals.Year == 2020)
            {
                if (reason == EndReason.Defeat)
                { Quit = world.Globals.Status.Scored = true; }
                else
                {
                    continuation = new("Keep playing?", null, null, new([0, 1]), Cancellable: false)
                    { Options = new([new(0, "End game"), new(1, "Keep playing")]) };
                    return new(CommandStatus.AwaitingInteraction, Interaction: continuation);
                }
            }
        }
        context.World.PendingEndReason = EndReason.None;
        var values = new OwnedList<NamedValue>([new("quit", new CommandValue.Boolean(Quit))]);
        context.Effect("game.outcome.handled", values);
        return new(failure.Length == 0 ? CommandStatus.Completed : CommandStatus.Failed, values, failure);
    }
    public void Dispose() { child?.Dispose(); child = null; }
}

/// <summary>Post-global retirement/outcome presentation. F04 global advancement owns
/// arrival processing; scanning again here would admit two ships in the same turn.</summary>
public sealed class TurnOpeningOperation(HallOfFame hall) : IContinuation
{
    private IContinuation? child;
    private int stage;
    private string failure = "";
    public bool Quit { get; private set; }
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        while (true)
        {
            if (child is null)
            {
                child = stage++ switch
                {
                    0 => new ScheduledRetirementOperation(),
                    1 when context.World.PendingEndReason != EndReason.None => new PendingOutcomeOperation(hall),
                    _ => null
                };
                input = new();
                if (child is null)
                { return new(failure.Length == 0 ? CommandStatus.Completed : CommandStatus.Failed, Code: failure); }
            }
            var result = child.Resume(context, input);
            if (!result.Status.Terminal())
            { return result; }
            if (result.Status == CommandStatus.Failed && failure.Length == 0)
            { failure = result.Code; }
            if (child is PendingOutcomeOperation outcome)
            { Quit = outcome.Quit; }
            child.Dispose();
            child = null;
        }
    }
    public void Dispose() { child?.Dispose(); child = null; }
}
