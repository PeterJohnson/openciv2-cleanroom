using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private sealed class GiftFixture : IDiplomaticGiftEffects
    {
        public TechnologyFixture T { get; }
        public GameState World => T.World;
        public RevelationFixture Data => T.T.Data;
        public DiplomaticGifts Gifts { get; }
        public List<DiplomaticGiftNotice> Notices { get; } = [];
        public List<int> Refreshed { get; } = [];
        public DiplomaticGiftNoticeKind? FailNotice;
        public bool FailRefresh;
        public GiftFixture(List<Choice>? choices = null, RevelationFixture? data = null)
        {
            T = new(choices, data);
            Gifts = new(World, Data.Process.Rules, T.T.Random, T.Service, T.T.Research, Data.Stacks, this);
        }
        public DiplomaticGifts.Operation Begin(int menu)
        {
            var op = Gifts.Begin(1, 2, "gift");
            Equal(true, op.Resume() is DiplomaticGiftDecision.Menu);
            op.Resume(new(menu));
            return op;
        }
        public void Military(int prerequisite = -1, byte cost = 10)
        {
            Data.AddCity(0, 1, new(8, 8)); Data.AddCity(1, 2, new(20, 8));
            World.Entities.Cities.Storage[0].Name = "Source"; World.Entities.Cities.Storage[1].Name = "Destination";
            Data.Stacks.Relocate(0, new(8, 8), false); Data.Unit(0).Owner = new(1); Data.Unit(0).Type = new(0);
            Data.Rules.Units[0] = Data.Rules.Units[0] with { CostRows = cost, Prerequisite = (AdvanceReference)prerequisite, Identity = new("unit", "Unit") };
            World.Civilizations[1].UnitsActive[0] = 1; World.Civilizations[2].UnitsActive[0] = 0;
            World.Civilizations[1].MilitaryStrength = World.Civilizations[2].MilitaryStrength = 10;
            Data.Unit(0).Home = new(0); Data.Unit(0).Visibility = CivilizationSet.Only(1).With(3);
            Data.Unit(0).MovesUsed = 3; Data.Unit(0).Orders = UnitOrder.Fortified;
        }
        public void Notice(DiplomaticGiftNotice notice)
        { Notices.Add(notice); if (FailNotice == notice.Kind) { throw new InvalidOperationException("Missing gift notice"); } }
        public void RefreshStatus() { if (FailRefresh) { throw new InvalidOperationException("Missing gift status"); } }
        public void RefreshCity(CitySlot city) { Refreshed.Add(city.Value); if (FailRefresh) { throw new InvalidOperationException("Missing city view"); } }
    }

    private static void DiplomaticGiftGoldAndCounters()
    {
        foreach (var (gold, value) in new[] { (-100, 0), (0, 0), (49, 4), (50, 5), (150, 11), (250, 16), (350, 20) })
        { Equal(value, DiplomaticGifts.GoldValue(gold)); }
        foreach (var (gold, amounts) in new[] { (49, Array.Empty<int>()), (50, new[] { 50 }), (150, new[] { 50, 100, 150 }), (250, new[] { 50, 100, 150, 250 }) })
        {
            var f = new GiftFixture(); f.World.Civilizations[1].Gold = gold;
            using var op = f.Begin(2); Sequence(amounts, ((DiplomaticGiftDecision.Gold)op.Pending!).Amounts);
            Equal(op.Pending, op.Resume()); op.Resume(new(0)); Equal(true, op.Completed);
            Equal(DiplomaticGiftNoticeKind.Wasting, f.Notices.Single().Kind); Equal(0, f.World.Diplomacy.GiftsThisMeeting);
        }
        foreach (int amount in new[] { 150, -150 })
        {
            var f = new GiftFixture(); f.World.Civilizations[1].Gold = 150;
            f.World.Diplomacy.GiftsThisMeeting = 128; f.World.Civilizations[2].DiploRequestCount = 127;
            f.FailRefresh = true;
            using var op = f.Begin(2); op.Resume(new(amount));
            Equal(150 - amount, f.World.Civilizations[1].Gold); Equal(777 + amount, f.World.Civilizations[2].Gold);
            Equal((byte)(amount > 0 ? 34 : 50), f.World.Civilizations[2].Attitude[1]);
            Equal((sbyte)-3, f.World.Civilizations[2].DiploRequestCount); Equal(129, f.World.Diplomacy.GiftsThisMeeting);
            Equal(1, op.PresentationFailures.Count); f.T.T.Random.RequireChoicesConsumed();
        }
    }

    private static void DiplomaticGiftTechnologyAndFailures()
    {
        foreach (int choice in new[] { 0, 1, 2 })
        {
            var f = new GiftFixture(); f.T.Know(1, 94, 4); f.T.Know(1, 95, 9);
            using var op = f.Begin(1); var offer = (DiplomaticGiftDecision.Technology)op.Pending!;
            Equal(95, offer.Best.Identity); Equal(94, offer.Second!.Identity); op.Resume(new(choice)); Equal(true, op.Completed);
            Equal(choice == 1, f.World.Civilizations[2].Advances.Contains(95)); Equal(choice == 2, f.World.Civilizations[2].Advances.Contains(94));
            Equal((byte)(choice == 0 ? 50 : choice == 1 ? 14 : 32), f.World.Civilizations[2].Attitude[1]);
            Equal((sbyte)(choice == 0 ? 0 : -2), f.World.Civilizations[2].DiploRequestCount);
            Equal(choice == 0 ? 0 : 1, f.World.Diplomacy.GiftsThisMeeting); f.T.T.Random.RequireChoicesConsumed();
        }
        var none = new GiftFixture(); using var empty = none.Begin(1);
        Equal(true, empty.Completed); Equal(DiplomaticGiftNoticeKind.KnowNo, none.Notices.Single().Kind);
        var failed = new GiftFixture(); failed.FailNotice = DiplomaticGiftNoticeKind.Accept;
        using var gold = failed.Begin(2); Throws<InvalidOperationException>(() => gold.Resume(new(150)));
        Equal(4850, failed.World.Civilizations[1].Gold); Equal((byte)34, failed.World.Civilizations[2].Attitude[1]);
        Equal(0, failed.World.Diplomacy.GiftsThisMeeting); Equal((sbyte)0, failed.World.Civilizations[2].DiploRequestCount);
        Throws<InvalidOperationException>(() => gold.Resume(new(0)));
        using var stopped = new GiftFixture().Begin(2); stopped.Dispose(); Throws<ObjectDisposedException>(() => stopped.Resume(new(50)));
    }

    private static void DiplomaticGiftMilitaryTransfer()
    {
        foreach (byte cost in new byte[] { 10, 200 })
        {
            var f = new GiftFixture(); f.Military(cost: cost); f.FailRefresh = true;
            f.World.Civilizations[1].MilitaryCount = 17; f.World.Civilizations[2].MilitaryCount = 23;
            using var op = f.Begin(3); Sequence(new[] { 0 }, ((DiplomaticGiftDecision.City)op.Pending!).Options.Select(c => (int)c.City.Value));
            op.Resume(new(0)); Equal(true, op.Pending is DiplomaticGiftDecision.Unit);
            // Destination identity survives the choice; attachment uses that city's live location.
            f.World.Entities.Cities.Storage[1].Position = new(22, 8);
            op.Resume(new(0)); Equal(true, op.Completed);
            Equal((byte)2, f.Data.Unit(0).Owner.Value); Equal(new MapPoint(22, 8), f.Data.Unit(0).Position);
            Equal((CitySlot?)null, f.Data.Unit(0).Home); Equal(default(CivilizationSet), f.Data.Unit(0).Visibility);
            Equal((byte)0, f.World.Civilizations[1].UnitsActive[0]); Equal((byte)1, f.World.Civilizations[2].UnitsActive[0]);
            Equal((byte)3, f.Data.Unit(0).MovesUsed); Equal(UnitOrder.Fortified, f.Data.Unit(0).Orders);
            Equal((short)17, f.World.Civilizations[1].MilitaryCount); Equal((short)23, f.World.Civilizations[2].MilitaryCount);
            Equal((byte)(cost == 10 ? 35 : 100), f.World.Civilizations[2].Attitude[1]); // None is known: halve signed cost benefit.
            Sequence(new[] { 1, 0 }, f.Refreshed); Equal(2, op.PresentationFailures.Count);
            Equal(0, f.World.Diplomacy.GiftsThisMeeting); Equal((sbyte)0, f.World.Civilizations[2].DiploRequestCount);
            Equal(0, f.Data.Stacks.FirstAt(new(22, 8))); f.T.T.Random.RequireChoicesConsumed();
        }
    }

    private static void DiplomaticGiftMilitaryRejections()
    {
        for (int gate = 0; gate < 6; ++gate)
        {
            var f = new GiftFixture(); f.Military();
            switch (gate)
            {
                case 0: f.World.Entities.Cities.Storage[1].Token = default; break;
                case 1: f.World.Diplomacy.Attitude = 62; break;
                case 2: f.World.Civilizations[2].NumAdvances = 1; break;
                case 3: f.World.Globals.PowerRank[2] = 4; break;
                case 4: f.World.Civilizations[2].MilitaryStrength = 11; break;
                case 5:
                    f.T.Know(2, 90); f.World.Civilizations[1].NumAdvances = 1;
                    f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { ObsoleteBy = (AdvanceReference)90 }; break;
            }
            using var op = f.Begin(3); op.Resume(new(0)); if (gate == 5) { op.Resume(new(0)); }
            Equal(true, op.Completed); Equal(DiplomaticGiftNoticeKind.MilitaryNo, f.Notices.Single().Kind);
            Equal((byte)1, f.Data.Unit(0).Owner.Value); Equal((sbyte)0, f.World.Civilizations[2].DiploRequestCount);
            f.T.T.Random.RequireChoicesConsumed();
        }
        for (int cancel = 0; cancel < 4; ++cancel)
        {
            var f = new GiftFixture(); f.Military();
            if (cancel == 0) { f.World.Entities.Cities.Storage[0].Owner = new(2); }
            if (cancel == 2) { f.Data.Stacks.Relocate(0, new(6, 8), false); }
            using var op = f.Begin(3);
            if (cancel != 0) { op.Resume(new(cancel == 1 ? -1 : 0)); }
            if (cancel == 3) { op.Resume(new(-1)); }
            Equal(true, op.Completed); Equal((sbyte)-1, f.World.Civilizations[2].DiploRequestCount);
            Equal(cancel == 2 ? 1 : 0, f.Notices.Count); Equal((byte)1, f.Data.Unit(0).Owner.Value);
        }
    }

    private static void DiplomaticGiftBreakthrough()
    {
        foreach (int ancestor in new[] { 0, 90 })
            foreach (int roll in new[] { 0, 1 })
            {
                List<Choice> choices = [];
                if (ancestor > 0)
                {
                    choices.Add(Choice.Create(new("V05.gift-breakthrough", "gift"), new SamplingLaw.PrimaryModulo(2), ChoiceCode.Primary(roll)));
                    if (roll == 0) { choices.Add(Choice.Create(new("V03.emissary", "gift/breakthrough"), new SamplingLaw.SecondaryRange(0, 2), ChoiceCode.Secondary(uint.MaxValue))); }
                }
                var f = new GiftFixture(choices);
                f.Military(95); f.Data.Rules.Advances[95] = f.Data.Rules.Advances[95] with { Prerequisites = new([(AdvanceReference)ancestor, AdvanceReference.None]) };
                f.Data.Rules.Advances[ancestor] = f.Data.Rules.Advances[ancestor] with { Prerequisites = new([AdvanceReference.None, AdvanceReference.None]) };
                using var op = f.Begin(3); op.Resume(new(0)); op.Resume(new(0)); Equal(true, op.Completed);
                Equal(ancestor > 0 && roll == 0, f.World.Civilizations[2].Advances.Contains(ancestor));
                Equal(false, f.World.Civilizations[1].Advances.Contains(ancestor)); // No donor-knowledge gate.
                Equal(false, f.World.Civilizations[2].Advances.Contains(95));
                Equal((byte)20, f.World.Civilizations[2].Attitude[1]); f.T.T.Random.RequireChoicesConsumed();
            }
        var failed = new GiftFixture(); failed.Military(); failed.FailNotice = DiplomaticGiftNoticeKind.Accept;
        using var transfer = failed.Begin(3); transfer.Resume(new(0)); Throws<InvalidOperationException>(() => transfer.Resume(new(0)));
        Equal((byte)2, failed.Data.Unit(0).Owner.Value); Equal((CitySlot?)null, failed.Data.Unit(0).Home);
        Equal((byte)35, failed.World.Civilizations[2].Attitude[1]); Equal(0, failed.World.Diplomacy.GiftsThisMeeting);
    }

    private static void DiplomaticGiftPrivateSession()
    {
        using var runtime = new SessionRuntime(new(), new(252));
        var f = new GiftFixture(data: CreationFixture(new(runtime.Process, runtime.World)));
        ParticipantId recipient = default;
        runtime.RegisterCommand(new(new(1, "test.gift", "Authored diplomatic gift", AccessClass.Player, new([]), new([])),
            (_, _, _) => new(), _ => new DiplomaticGiftOperation(f.Gifts.Begin(1, 2, "gift"), _ => recipient,
                _ => throw new InvalidOperationException("Unexpected intelligence"))));
        runtime.BeginClientSession(new()); var actor = SessionClient(runtime, 2); var player = SessionClient(runtime, 1); recipient = player.Identity.Participant;
        var command = actor.Submit(new("test.gift", new([]))).Correlation.Command; runtime.AdvanceOne();
        var prompt = RequiredInteraction(player, command); Equal(null, RequiredResult(actor, command).Interaction);
        Equal(false, actor.Reply(prompt, 2).Accepted); Equal(true, player.Reply(prompt, 2).Accepted); runtime.AdvanceOne();
        prompt = RequiredInteraction(player, command); Equal(true, player.Reply(prompt, 1250).Accepted); runtime.AdvanceOne();
        Equal(CommandStatus.Completed, RequiredResult(actor, command).Status);
        Equal(3750, f.World.Civilizations[1].Gold); Equal(2027, f.World.Civilizations[2].Gold);
        Equal(1, f.World.Diplomacy.GiftsThisMeeting); f.T.T.Random.RequireChoicesConsumed();
    }

    private static void DiplomaticGiftGovernmentContinuation()
    {
        foreach (bool failure in new[] { false, true })
        {
            var f = new GiftFixture(); f.T.Know(1, AdvanceIds.Monarchy);
            f.World.Globals.HumanPlayers = CivilizationSet.Only(1).With(2); f.World.Multiplayer.LocalHuman = 2;
            f.World.Civilizations[2].Government = Government.Despotism;
            for (int terrain = 0; terrain < 11; ++terrain)
            { f.Data.Rules.Terrain[terrain] = new() { Food = 2, Shields = 1, Trade = 1, Work = new() }; }
            f.T.T.Retained!.FailRevolution = failure;
            using var op = f.Begin(1);
            Equal(true, op.Resume(new(1)) is DiplomaticGiftDecision.Grant { Decision: ResearchGrantDecision.Government });
            Equal(true, f.World.Civilizations[2].Advances.Contains(AdvanceIds.Monarchy));
            Equal((byte)50, f.World.Civilizations[2].Attitude[1]); Equal(0, f.World.Diplomacy.GiftsThisMeeting);
            if (failure)
            {
                Throws<InvalidOperationException>(() => op.Resume(new(1)));
                Equal(0, f.World.Diplomacy.GiftsThisMeeting); Equal((sbyte)0, f.World.Civilizations[2].DiploRequestCount);
            }
            else
            {
                op.Resume(new(1)); Equal(true, op.Completed); Equal(Government.Anarchy, f.World.Civilizations[2].Government);
                Equal(1, f.World.Diplomacy.GiftsThisMeeting); Equal((sbyte)-2, f.World.Civilizations[2].DiploRequestCount);
            }
            f.T.T.Random.RequireChoicesConsumed();
        }
    }

    private static void DiplomaticGiftSessionCancellation()
    {
        foreach (var input in new[] { OperationInputKind.Cancel, OperationInputKind.Timeout })
            for (int stage = 0; stage < 5; ++stage)
            {
                var f = new GiftFixture(); f.Military(); f.T.Know(1, 94);
                f.World.Civilizations[2].DiploRequestCount = -128;
                var context = new OperationContext(f.World, f.Data.Process, f.T.T.Random, new PresentationRandom(1, "gift"),
                    new(default, new(1)), default, (_, _) => { }, () => { });
                using var op = new DiplomaticGiftOperation(f.Gifts.Begin(1, 2, "gift"), c => new((ulong)c),
                    _ => throw new InvalidOperationException("Unexpected intelligence"));
                Equal(CommandStatus.AwaitingInteraction, op.Resume(context, new()).Status);
                if (stage > 0) { Equal(CommandStatus.AwaitingInteraction, op.Resume(context, new(OperationInputKind.Choice, Math.Min(stage, 3))).Status); }
                if (stage == 4) { Equal(CommandStatus.AwaitingInteraction, op.Resume(context, new(OperationInputKind.Choice, 0)).Status); }
                Equal(CommandStatus.Completed, op.Resume(context, new(input)).Status);
                Equal((sbyte)(stage is 1 or 2 ? -128 : 127), f.World.Civilizations[2].DiploRequestCount);
                Equal(5000, f.World.Civilizations[1].Gold); Equal((byte)1, f.Data.Unit(0).Owner.Value);
                Equal(false, f.World.Civilizations[2].Advances.Contains(94)); f.T.T.Random.RequireChoicesConsumed();
            }
    }
}
