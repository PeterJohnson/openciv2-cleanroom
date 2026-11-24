using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Rules;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private sealed class MissionPresentationSession : IDisposable
    {
        public SessionRuntime Runtime { get; } = new(new(), new(317));
        public SinglePlayerMissionFixture F { get; }
        public SinglePlayerMissionPresentation Presentation { get; }
        public IPlayerClient Actor { get; private set; } = null!;
        public IPlayerClient Victim { get; private set; } = null!;
        public IPlayerClient Initiator { get; private set; } = null!;
        public CommandId Command { get; private set; }
        private OperationContext? current;
        public bool FailNotice;
        public MissionPresentationSession(List<Choice>? choices = null, int viewer = 1)
        {
            Presentation = new(() => current ?? throw new InvalidOperationException("No active step"), () => viewer);
            F = new(choices, new(Runtime.Process, Runtime.World), Presentation.Effects);
            F.World.Multiplayer.LocalHuman = (sbyte)viewer;
            F.World.Names[1].Tribe = "River Folk"; F.World.Names[2].Adjective = "Highland";
            F.World.Entities.Cities.Storage[0].Name = "Stone Haven";
            F.Data.Rules.Units[47] = F.Data.Rules.Units[47] with { Identity = new("courier", "Cipher Courier") };
        }
        public void Start(Func<IContinuation> create, bool unit = false)
        {
            IContinuation? child = null;
            Runtime.RegisterCommand(new(new(1, "test.mission-presentation", "Mission presentation", AccessClass.Player,
                new([]), unit ? new([new("stop", ValueKind.Boolean)]) : new([])), (_, _, _) => new(), _ => new TestContinuation((context, input) =>
                {
                    current = context with
                    {
                        Notify = (recipient, text) =>
                    {
                        context.Notify!(recipient, text);
                        if (FailNotice) throw new InvalidOperationException("Notice publication failed");
                    }
                    };
                    try { child ??= create(); return child.Resume(context, input); }
                    finally { current = null; }
                }, () => child?.Dispose())));
            Runtime.BeginClientSession(new()); Actor = SessionClient(Runtime); Victim = SessionClient(Runtime, 2); Initiator = SessionClient(Runtime, 3);
            Command = Initiator.Submit(new("test.mission-presentation", new([]))).Correlation.Command;
            Advance();
        }
        public void StartCity() => Start(() => new CityMissionOperation(F.Begin(), c => new((ulong)c),
            visit => new("Visit " + visit.CityName, new((ulong)visit.Actor), new CommandValue.City(visit.City), new([0])),
            _ => throw new InvalidOperationException("No capture in these missions")));
        private void Advance()
        {
            Equal(true, Runtime.AdvanceOne());
            for (int i = 0; i < 100 && RequiredResult(Initiator, Command).Status == CommandStatus.Pending; ++i)
                Equal(true, Runtime.AdvanceOne());
            Equal(false, RequiredResult(Initiator, Command).Status == CommandStatus.Pending);
        }
        public void Answer(IPlayerClient recipient, int choice)
        {
            var prompt = RequiredInteraction(recipient, Command);
            Equal(false, Initiator.Reply(prompt, choice).Accepted);
            var beforeActor = Actor.ObserveNotices(); var beforeVictim = Victim.ObserveNotices();
            Equal(false, Runtime.AdvanceOne()); Equal(beforeActor, Actor.ObserveNotices()); Equal(beforeVictim, Victim.ObserveNotices());
            Equal(true, recipient.Reply(prompt, choice).Accepted); Advance();
        }
        public void Done()
        {
            Equal(FailNotice ? CommandStatus.Failed : CommandStatus.Completed, RequiredResult(Initiator, Command).Status);
            Equal(0, Initiator.ObserveNotices()!.Items.Count); Equal(false, Runtime.AdvanceOne());
            Throws<InvalidOperationException>(() => Presentation.Publish()); F.Combat.Consumed();
        }
        public void Dispose() => Runtime.Dispose();
    }

    // V06 §4: the victim's notice follows the actor's required visit and unit effect.
    private static void MissionPresentationVisitsAndFailure()
    {
        foreach (bool embassy in new[] { false, true })
            foreach (bool fail in new[] { false, true })
            {
                using var s = new MissionPresentationSession { FailNotice = fail };
                s.StartCity(); s.Answer(s.Actor, (int)(embassy ? CityMission.Embassy : CityMission.Investigate));
                Equal(0, s.Victim.ObserveNotices()!.Items.Count);
                Equal(false, s.F.World.Entities.Cities.Storage[0].Flags.Investigated);
                Equal(embassy, s.F.World.Civilizations[1].Treaties[2].Embassy);
                Equal((byte)0, s.F.Data.Unit(0).MovesUsed);
                s.Answer(s.Actor, 0); s.Done();
                Equal(!embassy, s.F.World.Entities.Cities.Storage[0].Flags.Investigated);
                Equal((byte)1, s.F.Data.Unit(0).MovesUsed); Equal(true, s.F.Data.Unit(0).Token.Occupied);
                string text = s.Victim.ObserveNotices()!.Items.Single().Text;
                Equal(true, text.Contains("River Folk", StringComparison.Ordinal)); Equal(true, text.Contains("Cipher Courier", StringComparison.Ordinal));
                if (!embassy) Equal(true, text.Contains("Stone Haven", StringComparison.Ordinal));
                Equal(0, s.Actor.ObserveNotices()!.Items.Count);
            }
    }

    // V06 §5/V03: stolen knowledge is announced before the real retained grant and final escape.
    private static void MissionPresentationTheftAndFailure()
    {
        foreach (bool fail in new[] { false, true })
        {
            using var s = new MissionPresentationSession([MissionDraw("V06.theft.advance-start", CityMission.Steal, 100, AdvanceIds.Monarchy)])
            { FailNotice = fail };
            s.F.Monarchy(); s.F.Data.Rules.Advances[AdvanceIds.Monarchy] = s.F.Data.Rules.Advances[AdvanceIds.Monarchy]
                with
            { Identity = new("charters", "Civic Charters") };
            s.StartCity(); s.Answer(s.Actor, (int)CityMission.Steal);
            Equal(!fail, s.F.World.Civilizations[1].Advances.Contains(AdvanceIds.Monarchy));
            Equal(false, s.F.World.Entities.Cities.Storage[0].Flags.TechnologyStolen); Equal(true, s.F.Data.Unit(0).Token.Occupied);
            string text = s.Actor.ObserveNotices()!.Items.Single().Text;
            Equal(true, text.Contains("Civic Charters", StringComparison.Ordinal)); Equal(true, text.Contains("Stone Haven", StringComparison.Ordinal));
            if (!fail) s.Answer(s.Actor, 0);
            s.Done(); Equal(!fail, s.F.World.Entities.Cities.Storage[0].Flags.TechnologyStolen);
            Equal(fail, s.F.Data.Unit(0).Token.Occupied); Equal(0, s.Victim.ObserveNotices()!.Items.Count);
        }
    }

    // V06 §9: refusal addresses the actor; desertion addresses the local old owner after transfer.
    private static void MissionPresentationBribeRecipients()
    {
        foreach (bool incorruptible in new[] { false, true })
            foreach (bool fail in new[] { false, true })
            {
                using var s = new MissionPresentationSession(viewer: 2) { FailNotice = fail };
                var f = s.F; f.Data.AddUnit(1, 2, 4, new(12, 8));
                f.Data.Rules.Units[4] = f.Data.Rules.Units[4] with { CostRows = 6, Role = RuleRole.Attack, Identity = new("guard", "Banner Guard") };
                f.Data.Rules.Governments[(int)Government.Democracy] = f.Data.Rules.Governments[(int)Government.Democracy]
                    with
                { Identity = new("assembly", "Citizen Assembly") };
                f.World.Civilizations[1].Gold = 1000; f.World.Civilizations[2].Gold = 150;
                if (incorruptible) f.World.Civilizations[2].Government = Government.Democracy;
                var actorView = f.World.Views[1];
                // The admitted nonlocal human actor falls through without a bribe prompt in local mode.
                s.Start(() => new UnitEspionageOperation(f.Services.Units.Bribe(1, 1, "bribe"), c => new((ulong)c)), unit: true);
                s.Done(); Equal(new CivilizationSlot((byte)(incorruptible ? 2 : 1)), f.Data.Unit(1).Owner);
                Equal(incorruptible ? 1000 : 850, f.World.Civilizations[1].Gold);
                var receiver = incorruptible ? s.Actor : s.Victim; var other = incorruptible ? s.Victim : s.Actor;
                string text = receiver.ObserveNotices()!.Items.Single().Text;
                Equal(true, text.Contains("Banner Guard", StringComparison.Ordinal));
                Equal(true, text.Contains(incorruptible ? "Citizen Assembly" : "River Folk", StringComparison.Ordinal));
                Equal(0, other.ObserveNotices()!.Items.Count); Equal(actorView, f.World.Views[1]);
                if (!incorruptible) Equal(new MapPoint(12, 8), f.World.Views[2].Center);
            }
    }
}
