using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

public abstract record GlobalTurnDecision
{
    public sealed record Lifecycle(LifecycleDecision Choice) : GlobalTurnDecision;
    public sealed record Throne(ThroneUpgrade Upgrade) : GlobalTurnDecision;
    public sealed record History(HistorianReport Report) : GlobalTurnDecision;
    public sealed record Landing(SpaceshipArrivalNotice Arrival) : GlobalTurnDecision;
}
public abstract record GlobalTurnReply
{
    public sealed record Lifecycle(ResearchReply Value) : GlobalTurnReply
    {
        public Lifecycle(int value) : this(new ResearchReply(value)) { }
    }
    public sealed record Throne(ThroneSelection? Selection) : GlobalTurnReply;
    public sealed record Acknowledge : GlobalTurnReply;
}

/// <summary>F04 §4 local global advancement, composed from actual owner operations.
/// Old-turn children finish before calendar/reset/history/arrival work. Presentation
/// acknowledgements and private decisions retain their child without replaying effects.</summary>
public sealed class GlobalTurn(GameState world, RuleTables rules, GameplayRandom random, CalendarValues calendar,
    BarbarianSpawning spawning, GlobalWarming warming, PowerRanking power, ThroneProgress throne, UnitTurnReset reset)
{
    private GameState World => world;
    public Operation Begin(short accelerationSelection, string call) => new(this, accelerationSelection, call);

    public sealed class Operation : IDisposable
    {
        private readonly GlobalTurn parent;
        private readonly IEnumerator<GlobalTurnDecision?> steps;
        private readonly List<Exception> failures = [];
        private bool disposed, faulted;
        private ResearchReply? lifecycleReply;
        public bool Completed { get; private set; }
        public GlobalTurnDecision? Pending { get; private set; }
        public ThroneProgressResult? Progress { get; internal set; }
        public SpaceshipArrivalNotice? Arrival { get; internal set; }
        public IReadOnlyList<Exception> PresentationFailures => failures.AsReadOnly();
        internal Operation(GlobalTurn parent, short accelerationSelection, string call)
        {
            ArgumentException.ThrowIfNullOrEmpty(call);
            this.parent = parent;
            steps = parent.Execute(this, accelerationSelection, call).GetEnumerator();
        }
        internal ResearchReply? TakeLifecycleReply() { var reply = lifecycleReply; lifecycleReply = null; return reply; }
        internal void AddFailures(IEnumerable<Exception> errors) => failures.AddRange(errors);
        public GlobalTurnDecision? Resume(GlobalTurnReply? reply = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            {
                throw new InvalidOperationException("Failed global advancement cannot resume");
            }

            if (Completed)
            {
                return null;
            }

            try
            {
                MapKnowledge.RequireLocal(parent.World);
                if (Pending is not null)
                {
                    switch (Pending, reply)
                    {
                        case (GlobalTurnDecision.Lifecycle, GlobalTurnReply.Lifecycle answer):
                            lifecycleReply = answer.Value;
                            break;
                        case (GlobalTurnDecision.Throne choice, GlobalTurnReply.Throne answer):
                            if (answer.Selection is { } selection && choice.Upgrade.ActorCurrent)
                            {
                                if (!choice.Upgrade.Select(selection))
                                {
                                    return Pending;
                                }
                            }
                            else
                            {
                                choice.Upgrade.Cancel();
                            }

                            break;
                        case (GlobalTurnDecision.History or GlobalTurnDecision.Landing, GlobalTurnReply.Acknowledge):
                            break;
                        default:
                            return Pending;
                    }
                }
                Pending = null;
                if (steps.MoveNext())
                {
                    return Pending = steps.Current;
                }

                Completed = true;
                steps.Dispose();
                return null;
            }
            catch { faulted = true; steps.Dispose(); throw; }
        }
        public void Dispose() { disposed = true; steps.Dispose(); }
    }

    private IEnumerable<GlobalTurnDecision?> Execute(Operation operation, short accelerationSelection, string call)
    {
        if (world.Globals.Turn != 0)
        {
            using (var child = spawning.Begin(call + "/barbarians"))
            {
                try
                {
                    while (!child.Completed)
                    {
                        var pending = child.ResumeDecision(operation.TakeLifecycleReply());
                        if (!child.Completed)
                        {
                            yield return pending is null ? null : new GlobalTurnDecision.Lifecycle(pending);
                        }
                    }
                }
                finally { operation.AddFailures(child.PresentationFailures); }
            }
            operation.AddFailures(warming.Update());
            power.Publish(call + "/power");
            operation.Progress = throne.Check(accelerationSelection);
            using var upgrade = operation.Progress.Upgrade;
            if (upgrade is not null)
            {
                yield return new GlobalTurnDecision.Throne(upgrade);
            }
        }
        world.Globals.Turn = unchecked((short)(world.Globals.Turn + 1));
        world.Globals.Year = unchecked((short)Calendar.Year(world.Globals.Turn, world.Globals, world.Scenario, calendar));
        if (world.Globals.Turn > 199)
        {
            world.Globals.PeaceTurns = unchecked((short)(world.Globals.PeaceTurns + 1));
        }

        world.Globals.TransientFlags.EndOfTurn = false;
        using (var child = reset.Begin(UnitTurnReset.All, call + "/reset"))
        {
            while (!child.Completed)
            {
                var pending = child.ResumeDecision(operation.TakeLifecycleReply());
                if (!child.Completed)
                {
                    yield return pending is null ? null : new GlobalTurnDecision.Lifecycle(pending);
                }
            }
        }
        DiplomaticAttitudes.MaintainReputation(world, rules);
        if (world.Globals.NextHistoryTurn < world.Globals.Turn)
        {
            int draw = random.PrimaryModulo(new ChoiceKey("F04.history", call), 40);
            world.Globals.NextHistoryTurn = unchecked((short)(world.Globals.Turn + draw + 20));
            if (!world.Multiplayer.Saved.AutoplayActive)
            {
                yield return new GlobalTurnDecision.History(HistorianReport.Prepare(world));
            }
        }
        operation.Arrival = SpaceshipArrival.Begin(world);
        if (operation.Arrival is { } arrival)
        {
            if (arrival.ShowLanding)
            {
                yield return new GlobalTurnDecision.Landing(arrival);
            }

            SpaceshipArrival.Complete(world);
        }
    }
}
