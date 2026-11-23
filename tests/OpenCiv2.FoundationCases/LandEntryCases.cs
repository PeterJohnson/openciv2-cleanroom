using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // U02 Land checks: actual composed children, authored states and explicit unresolved selectors.
    private sealed class LandFixture : ILandEntryEffects, ICaravanTradeEffects, IUnitEspionageEffects
    {
        public CityMissionFixture City { get; }
        public RevelationFixture Data => City.Data;
        public GameState World => Data.World;
        public LandEntry Entry { get; }
        public MovementContinuation Movement { get; }
        public MovementStep Step { get; set; }
        public List<LandEntryAnimation> Animations { get; } = [];
        public List<AlliedRepairNotice> Repairs { get; } = [];
        public List<LandEntrySound> Sounds { get; } = [];
        public List<MapPoint> Lifted { get; } = [];
        public int Tutorials, Donations;
        public bool FailAnimation, FailSound, FailNotice;
        public LandFixture(List<Choice>? choices = null, RevelationFixture? data = null)
        {
            City = new(choices, data);
            var life = City.Capture.Life; var incidents = City.Capture.Theft.Damage.Incidents.Incidents;
            var units = new UnitEspionage(World, Data.Rules, Data.Stacks, life.Elimination, incidents, this);
            var caravans = new CaravanTrade(World, Data.Rules, life.Random, new(World, Data.Process.Rules, Data.Stacks), life.Elimination, incidents, this, "Food");
            Entry = new(World, Data.Rules, Data.Stacks, new(World, Data.Rules, Data.Stacks), new(caravans, City.Missions, units), this);
            Movement = new(new(0), World.Entities.Units.End, Data.Unit(0).Position) { LocalHuman = true, Options = new(true, true) };
            Step = new(1, RuleDomain.Land, Data.Unit(0).Position, City.Capture.City.Position, Terrain.Grassland, 1, null, 2, new(0));
        }
        public LandEntry.Operation Begin(bool ocean = false, LandEntrySelection selection = default) => Entry.Begin(new(0), Movement, Step, ocean, selection, 77, "land");
        public void Complete(LandEntry.Operation op)
        {
            for (int i = 0; i < 100 && !op.Completed; ++i) Equal(null, op.Resume());
            Equal(true, op.Completed); City.Capture.Life.Random.RequireChoicesConsumed();
        }
        public void Sound(LandEntrySound sound) { Sounds.Add(sound); if (FailSound) throw new InvalidOperationException("No sound"); }
        public void ZoneTutorial(int recipient) { Equal(1, recipient); ++Tutorials; if (FailNotice) throw new InvalidOperationException("No tutorial"); }
        public void PrepareEntry(LandEntryAnimation animation) => Equal(Step.From, Data.Unit(0).Position);
        public void AnimateEntry(LandEntryAnimation animation) { Animations.Add(animation); Lifted.Add(Data.Unit(0).Position); if (FailAnimation) throw new InvalidOperationException("No animation"); }
        public void AlliedRepair(AlliedRepairNotice notice) { Repairs.Add(notice); if (FailNotice) throw new InvalidOperationException("No repair notice"); }
        public void Redraw(MapPoint point) { }
        public void Sound(CaravanSound sound) { }
        public void Delivery(CaravanDeliveryNotice notice) { }
        public void Reciprocal(CaravanReciprocalNotice notice) { }
        public void Donation(CaravanDonationNotice notice) => ++Donations;
        public void RefreshStatus() { }
        public void Incorruptible(UnitMissionSubject target) { }
        public void Sound(UnitEspionageSound sound) { }
        public void Publish() { }
        public void RefreshStatus(int civilization) { }
        public void EnsureVisible(MapPoint point) { }
        public void Deserted(UnitMissionSubject target, string name) { }
        public void Delay(int ticks) { }
        public void Sabotaged(UnitMissionSubject actor, UnitMissionSubject target) { }
        public void Explosion(MapPoint point) { }
    }
    private static void LandEntryZoneOrdering()
    {
        foreach (int variant in Enumerable.Range(0, 6))
        {
            var f = new LandFixture(); f.Data.Unit(0).Type = new(4);
            f.Step = f.Step with { To = new(10, 10), City = null, DestinationOwner = null };
            f.Data.AddUnit(1, 2, 4, new(11, 9)); f.Data.Unit(1).Visibility = default;
            f.Data.Unit(0).Flags.Automated = true; f.World.Globals.GameOptions.Tutorial = true;
            f.Movement.Options = new(variant != 1, true);
            if (variant == 2) f.Step = f.Step with { DestinationUnit = new(0) };
            if (variant == 3) f.Step = f.Step with { Domain = RuleDomain.Sea };
            if (variant == 5) f.Data.Rules.Units[4] = f.Data.Rules.Units[4] with { Abilities = f.Data.Rules.Units[4].Abilities with { IgnoreZonesOfControl = true } };
            using var op = f.Begin(ocean: variant == 4); f.Complete(op);
            bool denied = variant < 2;
            Equal(denied, op.Result!.Stopped); Equal(denied, f.Data.Unit(1).Visibility.Contains(1));
            Equal(denied ? 1 : 0, f.Tutorials); Equal(variant == 0 ? 1 : 0, f.Sounds.Count);
            Equal(denied ? (byte)6 : (byte)0, f.Data.Unit(0).MovesUsed); Equal(f.Step.From, f.Data.Unit(0).Position);
        }
    }
    private static void LandEntryAlliedRepair()
    {
        foreach (int variant in Enumerable.Range(0, 6))
        {
            var f = new LandFixture(); f.Data.Unit(0).Type = new(4); f.Data.Unit(0).Damage = 5; f.Data.Unit(0).Flags.Moved = true;
            f.Data.Rules.Units[4] = f.Data.Rules.Units[4] with { HitPoints = variant == 2 ? (byte)255 : (byte)20 };
            f.World.Civilizations[1].Treaties[2].Alliance = true;
            if (variant == 1) BuildingQueries.SetBuilding(f.World, new(0), BuildingIds.Barracks, true);
            if (variant == 3) f.World.Multiplayer.Saved.AutoplayActive = true;
            if (variant == 4) f.World.Multiplayer.LocalHuman = 2;
            if (variant == 5) f.World.Globals.AdvanceKnownBy[AdvanceIds.Communism] = CivilizationSet.Only(0);
            using var op = f.Begin(); f.Complete(op);
            Equal(true, op.Result!.Stopped); Equal(variant == 1 ? (byte)1 : variant == 2 ? (byte)5 : (byte)3, f.Data.Unit(0).Damage);
            Equal(false, f.Data.Unit(0).Flags.Moved); Equal(variant == 2 ? (byte)3 : (byte)6, f.Data.Unit(0).MovesUsed);
            Equal(variant is 3 or 4 ? 0 : 1, f.Repairs.Count);
            if (f.Repairs.Count != 0) Equal(variant == 5, f.Repairs[0].DiplomaticPortrait);
        }
        var failed = new LandFixture { FailNotice = true }; failed.Data.Unit(0).Type = new(4); failed.Data.Unit(0).Damage = 5;
        failed.World.Civilizations[1].Treaties[2].Alliance = true;
        using var failure = failed.Begin(); Throws<InvalidOperationException>(() => failure.Resume());
        Equal((byte)4, failed.Data.Unit(0).Damage); Equal((byte)3, failed.Data.Unit(0).MovesUsed); Throws<InvalidOperationException>(() => failure.Resume());
        var cosmetic = new LandFixture { FailSound = true }; cosmetic.Data.Unit(0).Type = new(4); cosmetic.World.Civilizations[1].Treaties[2].Alliance = true;
        using var sound = cosmetic.Begin(); cosmetic.Complete(sound); Equal(1, sound.PresentationFailures.Count); Equal(1, cosmetic.Repairs.Count);
    }
    private static void LandEntryCityAndSelectedAnimation()
    {
        foreach (bool selected in new[] { false, true })
        {
            var f = new LandFixture { FailAnimation = true }; f.World.Globals.HumanPlayers = CivilizationSet.Only(1).With(2);
            f.Data.AddUnit(1, 1, 4, f.Step.From);
            using var op = f.Begin(selection: new(false, selected));
            var menu = (LandEntryDecision.City)op.Resume()!; Equal(true, menu.Decision is CityMissionDecision.Menu);
            Equal(menu, op.Resume()); Equal(selected ? 1 : 0, f.Animations.Count); Equal(selected ? 1 : 0, op.PresentationFailures.Count);
            Equal(f.Step.From, f.Data.Unit(0).Position);
            Equal(selected ? 0 : 1, f.Data.Stacks.FirstAt(f.Step.From));
            if (selected) { Equal(UnitStaging.Lifted(1), f.Lifted.Single()); Equal(20, f.Animations.Single().Ticks); }
            op.Resume(new(-1)); f.Complete(op); Equal(true, op.Result!.Stopped); Equal(2, op.Result.DestinationOwner);
            Equal(true, f.Data.Unit(0).Token.Occupied); Equal((byte)0, f.Data.Unit(0).MovesUsed);
        }
        var embassy = new LandFixture(); embassy.Data.Unit(0).Type = new(46);
        using var real = embassy.Begin(); real.Resume(); real.Resume(new((int)CityMission.Embassy));
        Equal(true, ((LandEntryDecision.City)real.Resume()!).Decision is CityMissionDecision.Mission { Decision: CityEspionageDecision.Visit });
        real.Resume(new(0)); embassy.Complete(real);
        Equal(true, embassy.World.Civilizations[1].Treaties[2].Embassy); Equal(false, embassy.Data.Unit(0).Token.Occupied); Equal(true, real.Result!.Stopped);
    }
    private static void LandEntryCaravanChildren()
    {
        var f = new LandFixture(); f.Data.Unit(0).Type = new(3); f.Data.Unit(0).Counter = 255; f.Data.Unit(0).Home = null;
        f.City.Capture.City.Owner = new(1); f.World.Map.TileAt(f.Step.To).Owner = 1;
        f.Step = f.Step with { DestinationOwner = 1 }; f.City.Capture.City.Production = -39;
        f.Data.Rules.Units[3] = f.Data.Rules.Units[3] with { CostRows = 5 }; f.Data.Unit(0).Destination = new(-1, -1);
        using var donation = f.Begin(); var menu = (LandEntryDecision.Caravan)donation.Resume()!;
        Equal(true, ((CaravanDecision.Menu)menu.Decision).Request.Actions.Contains(CaravanAction.HelpBuildWonder));
        donation.Resume(new(2)); f.Complete(donation); Equal(true, donation.Result!.Stopped); Equal(1, f.Donations);
        Equal((short)50, f.City.Capture.City.ShieldBox); Equal(false, f.Data.Unit(0).Token.Occupied);
        f = new([DeliveryCap("land/caravan/delivery")]); f.Data.Unit(0).Type = new(3); f.Data.Unit(0).Counter = 255;
        f.World.Globals.HumanPlayers = CivilizationSet.Only(1).With(2); f.World.Globals.Turn = 200;
        using var delivery = f.Begin(selection: new(true, false)); f.Complete(delivery);
        Equal(true, delivery.Result!.Stopped); Equal(false, f.Data.Unit(0).Token.Occupied); Equal(10, f.Animations.Single().Ticks);
        Equal(1, f.City.Capture.City.RouteCount);
    }
    private static void LandEntryUnitMissionOwnerReload()
    {
        var f = new LandFixture(); f.Data.AddUnit(1, 2, 4, new(10, 10));
        f.Step = f.Step with { To = new(10, 10), City = null, DestinationUnit = new(1) };
        f.Data.Rules.Units[4] = f.Data.Rules.Units[4] with { CostRows = 6 };
        using var op = f.Begin(); Equal(true, ((LandEntryDecision.Unit)op.Resume()!).Decision is UnitMissionDecision.Menu);
        var offer = (UnitMissionDecision.Bribe)((LandEntryDecision.Unit)op.Resume(new(0))!).Decision;
        int before = f.World.Civilizations[1].Gold; op.Resume(new(1)); f.Complete(op);
        Equal(false, op.Result!.Stopped); Equal(1, op.Result.DestinationOwner); Equal(new CivilizationSlot(1), f.Data.Unit(1).Owner);
        Equal(before - offer.Offer.Price, f.World.Civilizations[1].Gold); Equal((UnitSlot?)new UnitSlot(1), f.Step.DestinationUnit);
        Equal(new UnitSlot(0), f.Movement.Unit); op.Resume(new(1)); Equal(before - offer.Offer.Price, f.World.Civilizations[1].Gold);
    }
    private static void LandEntryRestorationAndFailures()
    {
        var f = new LandFixture(); f.Data.AddUnit(1, 1, 4, f.Step.From); f.Movement.Unit = new(1);
        f.Data.Unit(0).Flags.Automated = true; f.Data.Unit(1).Damage = 5;
        f.World.Civilizations[1].Treaties[2].Alliance = true;
        using var repair = f.Begin(); f.Complete(repair);
        Equal((byte)4, f.Data.Unit(1).Damage); Equal((byte)3, f.Data.Unit(1).MovesUsed);
        Equal((byte)6, f.Data.Unit(0).MovesUsed); Equal(new UnitSlot(0), f.Movement.Unit);
        Equal(new UnitSlot(1), f.Repairs.Single().Unit.Slot);
        f = new() { FailNotice = true }; f.Data.Unit(0).Type = new(4); f.Data.Unit(0).Flags.Automated = true;
        f.Step = f.Step with { To = new(10, 10), City = null, DestinationOwner = null };
        f.Data.AddUnit(1, 2, 4, new(11, 9)); f.Data.Unit(1).Visibility = default;
        f.World.Globals.GameOptions.Tutorial = true;
        using var denied = f.Begin(); Throws<InvalidOperationException>(() => denied.Resume());
        Equal(false, f.Data.Unit(1).Visibility.Contains(1)); Equal((byte)0, f.Data.Unit(0).MovesUsed);
        Throws<InvalidOperationException>(() => denied.Resume());
        f = new(); f.World.Globals.Multiplayer = GameMode.Tcp;
        using var remote = f.Begin(); Throws<InvalidOperationException>(() => remote.Resume()); Equal(0, f.City.Reads);
        f = new(); var disposed = f.Begin(); disposed.Resume(); disposed.Dispose(); Throws<ObjectDisposedException>(() => disposed.Resume(new(-1)));
        f = new([EscapeChoice("land/unit/escape", 2, 0)]); f.Data.AddUnit(1, 2, 4, new(10, 10));
        f.Step = f.Step with { To = new(10, 10), City = null, DestinationUnit = new(1) };
        using var sabotage = f.Begin(); sabotage.Resume(); sabotage.Resume(new(1)); f.Complete(sabotage);
        Equal(true, sabotage.Result!.Stopped); Equal(2, sabotage.Result.DestinationOwner);
        Equal(false, f.Data.Unit(0).Token.Occupied); Equal(true, f.Data.Unit(1).Damage > 0);
    }
    private static void LandEntryPrivateSession()
    {
        using var runtime = new SessionRuntime(new(), new(225));
        var f = new LandFixture(data: CreationFixture(new(runtime.Process, runtime.World)));
        ParticipantId actorSeat = default, otherSeat = default; int visits = 0;
        f.World.Civilizations[1].Treaties[2].Embassy = true;
        runtime.RegisterCommand(new(new(1, "test.land-entry", "Authored land entry", AccessClass.Player, new([]), new([new("stopped", ValueKind.Boolean), new("destinationOwner")])), (_, _, _) => new(),
            _ => new LandEntryOperation(f.Begin(), c => c == 1 ? actorSeat : otherSeat, request =>
            { ++visits; return new("City visit", otherSeat, new CommandValue.City(request.City), new([0])); },
            _ => throw new InvalidOperationException("No capture"))));
        runtime.BeginClientSession(new()); var actor = SessionClient(runtime); var other = SessionClient(runtime, 2);
        actorSeat = actor.Identity.Participant; otherSeat = other.Identity.Participant;
        var command = actor.Submit(new("test.land-entry", new([]))).Correlation.Command; runtime.AdvanceOne();
        var menu = RequiredInteraction(actor, command); Equal(false, other.Reply(menu, 1).Accepted); Equal(true, actor.Reply(menu, 1).Accepted); runtime.AdvanceOne();
        var window = RequiredInteraction(actor, command); Equal(1, visits); Equal(false, other.Reply(window, 0).Accepted);
        Equal(true, actor.Reply(window, 0).Accepted); runtime.AdvanceOne();
        var final = RequiredResult(actor, command);
        if (final.Status != CommandStatus.Completed) throw new InvalidOperationException(final.ToString());
        Equal(true, f.City.Capture.City.Flags.Investigated);
        Equal((byte)1, f.Data.Unit(0).MovesUsed); Equal(f.Step.From, f.Data.Unit(0).Position);
        foreach (bool caravan in new[] { false, true })
        {
            using var session = new SessionRuntime(new(), new(226));
            var child = new LandFixture(data: CreationFixture(new(session.Process, session.World)));
            if (caravan)
            {
                child.Data.Unit(0).Type = new(3); child.Data.Unit(0).Counter = 255; child.Data.Unit(0).Home = null;
                child.City.Capture.City.Owner = new(1); child.World.Map.TileAt(child.Step.To).Owner = 1;
                child.Step = child.Step with { DestinationOwner = 1 };
            }
            else
            {
                child.Data.AddUnit(1, 2, 4, new(10, 10));
                child.Step = child.Step with { To = new(10, 10), City = null, DestinationUnit = new(1) };
            }
            ParticipantId seat = default;
            session.RegisterCommand(new(new(1, "test.land-child", "Authored land child", AccessClass.Player, new([]),
                new([new("stopped", ValueKind.Boolean), new("destinationOwner")])), (_, _, _) => new(),
                _ => new LandEntryOperation(child.Begin(), _ => seat, _ => throw new InvalidOperationException("No visit"),
                    _ => throw new InvalidOperationException("No capture"))));
            session.BeginClientSession(new()); var owner = SessionClient(session); var observer = SessionClient(session, 2); seat = owner.Identity.Participant;
            var id = owner.Submit(new("test.land-child", new([]))).Correlation.Command; session.AdvanceOne();
            var prompt = RequiredInteraction(owner, id); Equal(false, observer.Reply(prompt, 0).Accepted);
            Equal(true, owner.Cancel(id).Accepted); session.AdvanceOne();
            Equal(CommandStatus.Completed, RequiredResult(owner, id).Status);
            Equal(true, child.Data.Unit(0).Token.Occupied); Equal((byte)0, child.Data.Unit(0).MovesUsed);
            if (!caravan) Equal(new CivilizationSlot(2), child.Data.Unit(1).Owner);
        }

    }
}
