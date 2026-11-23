using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private sealed class UnitMissionFixture : IUnitEspionageEffects
    {
        public EliminationFixture Lifecycle { get; }
        public IncidentFixture Incidents { get; }
        public RevelationFixture Data => Lifecycle.Data;
        public GameState World => Data.World;
        public UnitEspionage Missions { get; }
        public List<string> Order { get; } = [];
        public List<UnitMissionSubject> Desertions { get; } = [];
        public bool FailPublish, FailExplosion, DisableCombatOnExplosion;
        public UnitMissionFixture(List<Choice>? choices = null, RevelationFixture? data = null)
        {
            Lifecycle = new(choices, data); Incidents = new(Lifecycle.Random, Data);
            Missions = new(World, Data.Rules, Data.Stacks, Lifecycle.Elimination, Incidents.Incidents, this);
            Data.Rules.Units[4] = Data.Rules.Units[4] with { CostRows = 6, Identity = new("target", "Target unit") };
            Data.Rules.Units[46] = Data.Rules.Units[4] with { Role = RuleRole.Diplomacy, Prerequisite = AdvanceReference.None };
            World.Civilizations[1].Gold = 1000; World.Civilizations[2].Gold = 150;
            World.Civilizations[1].Government = World.Civilizations[2].Government = Government.Despotism;
            Data.AddUnit(0, 1, 47, new(8, 8)); Data.AddUnit(1, 2, 4, new(12, 8));
        }
        public UnitEspionage.Operation Bribe() => Missions.Bribe(1, 1, "bribe");
        public UnitEspionage.Operation Mission() => Missions.Mission(0, 1, 1, "mission");
        public void Incorruptible(UnitMissionSubject target) => Order.Add("incorruptible");
        public void Sound(UnitEspionageSound sound) => Order.Add("sound:" + sound);
        public void Publish() { Order.Add("publish"); if (FailPublish) { throw new InvalidOperationException("Publication failed"); } }
        public void RefreshStatus(int civilization) => Order.Add("status:" + civilization);
        public void EnsureVisible(MapPoint position) => Order.Add("visible");
        public void Deserted(UnitMissionSubject target, string newOwnerPlural) { Desertions.Add(target); Order.Add("deserted:" + newOwnerPlural); }
        public void Redraw(MapPoint position) => Order.Add("redraw");
        public void Delay(int ticks) => Order.Add("delay:" + ticks);
        public void Sabotaged(UnitMissionSubject actor, UnitMissionSubject target) => Order.Add("sabotaged:" + actor.Owner + ":" + target.Owner);
        public void Explosion(MapPoint position)
        {
            Order.Add("explosion");
            if (FailExplosion) { throw new InvalidOperationException("Explosion failed"); }
            if (DisableCombatOnExplosion) { World.Globals.Rules.NormalCombat = false; }
        }
    }

    // V06 §9 and shared capital distance: step-distance Palaces, actor Communism, signed costs and ordered truncation.
    private static void UnitBribePriceAndCapitalDistance()
    {
        var f = new UnitMissionFixture(); var w = f.World;
        Equal(16, EspionageCosts.DistanceToCapital(w, 2, new(12, 8)));
        f.Data.AddCity(0, 2, new(16, 8)); BuildingQueries.SetBuilding(w, new(0), BuildingIds.Palace, true);
        Equal(2, EspionageCosts.DistanceToCapital(w, 2, new(12, 8)));
        f.Data.AddCity(1, 2, new(12, 8)); f.Data.AddCity(2, 3, new(12, 10));
        BuildingQueries.SetBuilding(w, new(2), BuildingIds.Palace, true);
        using (var op = f.Bribe()) { Equal(675, ((UnitMissionDecision.Bribe)op.Resume()!).Offer.Price); }
        BuildingQueries.SetBuilding(w, new(1), BuildingIds.Palace, true);
        Equal(0, EspionageCosts.DistanceToCapital(w, 2, new(12, 8)));
        using (var op = f.Bribe()) { Equal(1350, ((UnitMissionDecision.Bribe)op.Resume()!).Offer.Price); }
        foreach (var (cost, role, expected) in new[] { (6, RuleRole.Attack, 150), (6, RuleRole.Settle, 300),
            (255, RuleRole.Attack, 15000), (255, RuleRole.Settle, 30000), (0, RuleRole.Trade, 0) })
        {
            f = new(); f.Data.Rules.Units[4] = f.Data.Rules.Units[4] with { CostRows = (byte)cost, Role = role };
            using var op = f.Bribe(); Equal(expected, ((UnitMissionDecision.Bribe)op.Resume()!).Offer.Price); f.Lifecycle.Random.RequireChoicesConsumed();
        }
        f = new(); w = f.World; w.Civilizations[2].Government = Government.Communism;
        using (var op = f.Bribe()) { Equal(150, ((UnitMissionDecision.Bribe)op.Resume()!).Offer.Price); }
        w.Civilizations[1].Government = Government.Communism;
        using (var op = f.Bribe()) { Equal(225, ((UnitMissionDecision.Bribe)op.Resume()!).Offer.Price); }
        w.Civilizations[2].Gold = int.MaxValue;
        using (var op = f.Bribe()) { Equal(15000, ((UnitMissionDecision.Bribe)op.Resume()!).Offer.Price); }
    }

    private static void UnitBribeCachedPriceAndTransfer()
    {
        var f = new UnitMissionFixture(); var w = f.World;
        f.Data.AddCity(0, 1, new(20, 8)); f.Data.AddCity(1, 3, new(14, 8));
        f.Data.Unit(0).MovesUsed = 5;
        f.Data.Unit(1).Home = new(0); f.Data.Unit(1).Orders = UnitOrder.Fortified; f.Data.Unit(1).MovesUsed = 9;
        f.Data.Unit(1).Damage = 7; f.Data.Unit(1).Flags.Veteran = true; f.Data.Unit(1).Visibility = CivilizationSet.Only(3);
        w.UnitTotals[1] = 7; w.UnitTotals[2] = 9; w.Civilizations[1].MilitaryCount = 8; w.Civilizations[2].MilitaryCount = 12;
        w.Civilizations[1].SubversionCount = short.MaxValue;
        using var op = f.Bribe(); var prompt = (UnitMissionDecision.Bribe)op.Resume()!;
        Equal(150, prompt.Offer.Price); Equal(true, prompt.Offer.Affordable); Equal(new UnitType(4), prompt.Offer.Target.Type);
        Equal(prompt, op.Resume());
        // A supplied reply retains the quoted price and captured owner, but transfer uses the live unit type.
        w.Civilizations[2].Gold = 100000; w.Civilizations[2].Government = Government.Democracy;
        w.Civilizations[1].Gold = 0; f.Data.Unit(1).Type = new(5);
        w.Civilizations[2].UnitsActive[5] = 0; w.Civilizations[1].UnitsActive[5] = 255;
        Equal(null, op.Resume(1)); Equal(0, op.Result);
        Equal(-150, w.Civilizations[1].Gold); Equal(100000, w.Civilizations[2].Gold);
        Equal((byte)255, w.Civilizations[2].UnitsActive[5]); Equal((byte)0, w.Civilizations[1].UnitsActive[5]);
        Equal(short.MinValue, w.Civilizations[1].SubversionCount); Equal(new CivilizationSlot(1), f.Data.Unit(1).Owner);
        Equal(null, f.Data.Unit(1).Home); Equal((byte)0, f.Data.Unit(1).MovesUsed); Equal(UnitOrder.None, f.Data.Unit(1).Orders);
        Equal((byte)7, f.Data.Unit(1).Damage); Equal(true, f.Data.Unit(1).Flags.Veteran);
        Equal(CivilizationSet.Only(2).With(3), f.Data.Unit(1).Visibility); Equal((byte?)1, w.Map.Owner(new(12, 8)));
        Equal(7, w.UnitTotals[1]); Equal(9, w.UnitTotals[2]); Equal((short)8, w.Civilizations[1].MilitaryCount);
        Equal((short)12, w.Civilizations[2].MilitaryCount); Equal((byte)5, f.Data.Unit(0).MovesUsed);
        Sequence(new[] { "publish", "sound:Spy", "status:1", "redraw" }, f.Order);
        Equal(0, f.Incidents.Notices.Count); f.Lifecycle.Random.RequireChoicesConsumed();
        op.Resume(1); Equal(4, f.Order.Count);

        f = new(); f.Data.AddCity(0, 1, new(12, 8));
        using var home = f.Bribe(); home.Resume(); home.Resume(1); Equal((CitySlot?)new CitySlot(0), f.Data.Unit(1).Home);
    }

    private static void UnitBribeAdmissionAndNonlocalPresentation()
    {
        var f = new UnitMissionFixture(); f.Data.AddUnit(2, 2, 4, new(12, 8));
        using (var op = f.Mission()) { Equal(null, op.Resume()); Equal(0, op.Result); }
        using (var op = f.Bribe()) { Equal(null, op.Resume()); Equal(0, op.Result); }
        Equal(0, f.Order.Count);
        foreach (bool autoplay in new[] { false, true })
        {
            f = new(); f.World.Civilizations[2].Government = Government.Democracy; f.World.Multiplayer.Saved.AutoplayActive = autoplay;
            using var op = f.Bribe(); Equal(null, op.Resume()); Equal(autoplay ? 0 : 1, f.Order.Count);
            Equal(new CivilizationSlot(2), f.Data.Unit(1).Owner);
        }
        foreach (int gold in new[] { 299, 300 })
        {
            f = new(); var w = f.World; w.Globals.HumanPlayers = CivilizationSet.Only(2); w.Civilizations[1].Gold = gold;
            using var op = f.Missions.Mission(9999, 1, 1, "ai"); // AI branch does not read the actor unit.
            Equal(null, op.Resume()); Equal(new CivilizationSlot((byte)(gold == 300 ? 1 : 2)), f.Data.Unit(1).Owner);
            f.Lifecycle.Random.RequireChoicesConsumed();
        }
        f = new(); f.World.Globals.HumanPlayers = default;
        f.Data.Rules.Units[46] = f.Data.Rules.Units[46] with { Prerequisite = (AdvanceReference)7 };
        using (var op = f.Bribe()) { op.Resume(); Equal(new CivilizationSlot(2), f.Data.Unit(1).Owner); }
        f.World.Civilizations[1].Advances = default(AdvanceSet).With(7);
        f.Data.Rules.Units[4] = f.Data.Rules.Units[4] with { Role = RuleRole.Trade };
        using (var op = f.Bribe()) { op.Resume(); Equal(new CivilizationSlot(2), f.Data.Unit(1).Owner); }
        foreach (bool autoplay in new[] { false, true })
        {
            f = new(); var w = f.World; w.Globals.HumanPlayers = CivilizationSet.Only(1).With(2); w.Multiplayer.LocalHuman = 2;
            w.Multiplayer.Saved.AutoplayActive = autoplay; w.Civilizations[1].Gold = autoplay ? 300 : 0;
            using var op = f.Bribe(); Equal(null, op.Resume()); Equal(0, op.Result); Equal(new CivilizationSlot(1), f.Data.Unit(1).Owner);
            Equal(autoplay ? 150 : -150, w.Civilizations[1].Gold); Equal(autoplay ? 0 : 1, f.Desertions.Count);
            Equal("delay:30", f.Order[^1]); Equal(false, f.Order.Contains("status:1"));
            if (!autoplay) { Equal(1, f.Desertions.Single().Owner); }
        }
    }

    private static void UnitSabotageDispatchAndActualEscape()
    {
        foreach (bool survives in new[] { false, true })
        {
            var f = new UnitMissionFixture([EscapeChoice("mission/escape", 2, survives ? 1 : 0)]); var w = f.World;
            w.Multiplayer.Saved.AutoplayActive = true; w.Espionage.LastMission = CityMission.Steal;
            f.Data.AddCity(0, 1, new(4, 4)); f.Data.Unit(1).Damage = 2;
            using var op = f.Mission(); Equal(true, op.Resume() is UnitMissionDecision.Menu); Equal(null, op.Resume(9));
            Equal(1, op.Result); Equal((byte)6, f.Data.Unit(1).Damage); Equal(true, f.Data.Unit(1).Token.Occupied);
            Equal(survives, f.Data.Unit(0).Token.Occupied); Equal(CityMission.Steal, w.Espionage.LastMission);
            if (survives) { Equal(new MapPoint(4, 4), f.Data.Unit(0).Position); Equal(true, f.Data.Unit(0).Flags.Veteran); Equal((byte)6, f.Data.Unit(0).MovesUsed); }
            Sequence(new[] { "sound:SmallExplosion", "sabotaged:1:2", "explosion", "redraw" }, f.Order);
            Equal(0, f.Incidents.Notices.Count); Equal(0, f.Lifecycle.Incidents.Count); f.Lifecycle.Random.RequireChoicesConsumed();
        }
        var cancelled = new UnitMissionFixture(); using (var op = cancelled.Mission())
        { op.Resume(); op.Resume(-1); Equal(0, op.Result); Equal((byte)0, cancelled.Data.Unit(1).Damage); }
        cancelled.Lifecycle.Random.RequireChoicesConsumed();
        foreach (bool ordinary in new[] { false, true })
        {
            var f = new UnitMissionFixture(); f.World.Globals.Rules.NormalCombat = ordinary;
            if (ordinary) { f.Data.Unit(0).Type = new(46); }
            using var op = f.Mission(); Equal(true, op.Resume() is UnitMissionDecision.Bribe); op.Resume(0);
            Equal(0, op.Result); Equal(true, f.Data.Unit(0).Token.Occupied); f.Lifecycle.Random.RequireChoicesConsumed();
        }
    }

    private static void UnitSabotageHitPointsAndNestedElimination()
    {
        foreach (bool disableCombat in new[] { false, true })
        {
            var f = new UnitMissionFixture([EscapeChoice("mission/escape", 2, 0)]) { DisableCombatOnExplosion = disableCombat };
            f.Data.Unit(1).Damage = 9; using var op = f.Mission(); op.Resume(); op.Resume(1);
            Equal((byte)(disableCombat ? 5 : 9), f.Data.Unit(1).Damage); Equal(true, f.Data.Unit(1).Token.Occupied);
            Equal(1, op.Result); f.Lifecycle.Random.RequireChoicesConsumed();
        }
        var nested = new UnitMissionFixture([EscapeChoice("mission/escape", 2, 0)]); var w = nested.World;
        nested.Data.Rules.Units[47] = nested.Data.Rules.Units[47] with { Role = RuleRole.Settle };
        w.Civilizations[1].UnitsActive[47] = 1; w.Civilizations[1].CityCount = 0;
        nested.Data.Unit(1).Flags.SpyMission = true; nested.Data.Unit(1).Counter = 1;
        DiplomaticRelations.Set(w, 1, 2, new() { Peace = true });
        using var mission = nested.Mission(); mission.Resume();
        var decision = (UnitMissionDecision.Incident)mission.Resume(1)!;
        Equal(1, decision.Pretext.Victim.Civilization); Equal(2, decision.Pretext.Actor.Civilization);
        Equal(null, mission.Result); Equal(false, nested.Data.Unit(0).Token.Occupied || nested.Data.Unit(1).Token.Occupied);
        Equal((byte)5, nested.Data.Unit(1).Damage); Equal(0, nested.Lifecycle.Checks);
        Equal(null, mission.Resume(1)); Equal(1, mission.Result); Equal(EndReason.Defeat, w.PendingEndReason);
        Equal(true, w.Civilizations[1].Treaties[2].War); nested.Lifecycle.Random.RequireChoicesConsumed();
    }

    private static void UnitMissionFailurePrefixesAndAuthority()
    {
        var f = new UnitMissionFixture { FailPublish = true }; using var bribe = f.Bribe(); bribe.Resume();
        Throws<InvalidOperationException>(() => bribe.Resume(1)); Throws<InvalidOperationException>(() => bribe.Resume(1));
        Equal(new CivilizationSlot(1), f.Data.Unit(1).Owner); Equal(850, f.World.Civilizations[1].Gold);
        Equal("publish", f.Order.Single()); Equal(null, bribe.Result); f.Lifecycle.Random.RequireChoicesConsumed();
        f = new() { FailExplosion = true }; using var sabotage = f.Mission(); sabotage.Resume();
        Throws<InvalidOperationException>(() => sabotage.Resume(1)); Equal((byte)0, f.Data.Unit(1).Damage);
        Equal(true, f.Data.Unit(0).Token.Occupied); Equal((byte)0, f.Data.Unit(0).MovesUsed); f.Lifecycle.Random.RequireChoicesConsumed();
        f = new(); f.World.Globals.Multiplayer = GameMode.Tcp; using var remote = f.Bribe();
        Throws<InvalidOperationException>(() => remote.Resume()); Equal(1000, f.World.Civilizations[1].Gold); Equal(0, f.Order.Count);
        f = new(); var disposed = f.Bribe(); disposed.Resume(); disposed.Dispose();
        Throws<ObjectDisposedException>(() => disposed.Resume(1)); Equal(new CivilizationSlot(2), f.Data.Unit(1).Owner);
    }

    private static void UnitMissionSessionMenuPriceAndPrivacy()
    {
        foreach (bool affordable in new[] { false, true })
        {
            using var runtime = new SessionRuntime(new(), new(192));
            var f = new UnitMissionFixture(data: CreationFixture(new(runtime.Process, runtime.World)));
            f.World.Civilizations[1].Gold = affordable ? 1000 : 149; ParticipantId actorSeat = default;
            runtime.RegisterCommand(new(new(1, "test.mission", "Authored unit-mission integration", AccessClass.Player, new([]), new([new("stop", ValueKind.Boolean)])),
                (_, _, _) => new(), _ => new UnitEspionageOperation(f.Mission(), c => c == 1 ? actorSeat : throw new InvalidOperationException("Unexpected recipient"))));
            runtime.BeginClientSession(new()); var actor = SessionClient(runtime); actorSeat = actor.Identity.Participant;
            var victim = SessionClient(runtime, 2); var command = actor.Submit(new("test.mission", new([]))).Correlation.Command;
            runtime.AdvanceOne(); var menu = RequiredInteraction(actor, command);
            Equal(null, victim.Result(command)); Equal(false, victim.Reply(menu, 1).Accepted);
            Equal(true, actor.Reply(menu, 0).Accepted); runtime.AdvanceOne(); var quote = RequiredResult(actor, command).Interaction!;
            Equal(affordable ? 2 : 1, quote.Spec.Options.Count); Equal(affordable, actor.Reply(quote.Id, 1).Accepted);
            if (!affordable) { Equal(true, actor.Reply(quote.Id, 0).Accepted); }
            runtime.AdvanceOne(); var result = RequiredResult(actor, command); Equal(CommandStatus.Completed, result.Status);
            Equal(new CommandValue.Boolean(false), result.Result.Single().Value);
            Equal(new CivilizationSlot((byte)(affordable ? 1 : 2)), f.Data.Unit(1).Owner);
            Equal(affordable ? 850 : 149, f.World.Civilizations[1].Gold); f.Lifecycle.Random.RequireChoicesConsumed();
        }
    }
}
