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
    private sealed class CityDamageFixture : ICityEspionageDamageEffects
    {
        public EliminationFixture Lifecycle { get; }
        public IncidentFixture Incidents { get; }
        public RevelationFixture Data => Lifecycle.Data;
        public GameState World => Data.World;
        public ref OpenCiv2.Core.State.City City => ref World.Entities.Cities.At(new(0));
        public CityEspionageDamage Damage { get; }
        public CityMissionContext Mission => new(0, new(0), 1, 2, Data.Unit(0).Type, Data.Unit(0).Type.Value == 47);
        public List<string> Order { get; } = [];
        public List<CityEspionageNotice> Notices { get; } = [];
        public bool FailPublish, FailNotice;
        public CityDamageFixture(List<Choice>? choices = null, RevelationFixture? data = null)
        {
            Lifecycle = new(choices, data); Incidents = new(Lifecycle.Random, Data);
            Damage = new(World, Data.Rules, Lifecycle.Random, Lifecycle.Elimination, Incidents.Incidents, this);
            Data.AddUnit(0, 1, 47, new(10, 8)); Data.AddCity(0, 2, new(12, 8));
            City.Name = "Birch"; City.Production = 4;
            Data.Rules.Units[4] = Data.Rules.Units[4] with { Identity = new("target", "Long custom production " + new string('x', 120)) };
            Data.Rules.Units[46] = Data.Rules.Units[47];
            for (int i = 1; i <= 38; ++i)
                Data.Rules.Improvements[i] = Data.Rules.Improvements[i] with { Identity = new("building" + i, "Building " + i) };
            World.Civilizations[1].Government = World.Civilizations[2].Government = Government.Despotism;
        }
        public CityEspionageDamage.Operation Sabotage() => Damage.Sabotage(Mission, "damage");
        public CityEspionageDamage.Operation Poison() => Damage.Poison(Mission, "damage");
        public void Install(params int[] ids) { foreach (int id in ids) BuildingQueries.SetBuilding(World, new(0), id, true); }
        public bool Has(int id) => BuildingQueries.HasBuilding(World, new(0), id);
        public void Sound(CityEspionageSound sound) => Order.Add("sound:" + sound);
        public void RefreshCity(CitySlot city) => Order.Add("refresh");
        public void Redraw(MapPoint point) => Order.Add("redraw");
        public void Publish() { Order.Add("publish"); if (FailPublish) throw new InvalidOperationException("Publication failed"); }
        public void Notice(CityEspionageNotice notice)
        { Notices.Add(notice); Order.Add("notice:" + notice.Kind); if (FailNotice) throw new InvalidOperationException("Notice failed"); }
    }
    private static Choice DamageChoice(string site, int bound, int value) => Choice.Create(new("V06." + site, "damage"), new SamplingLaw.PrimaryModulo(bound), ChoiceCode.Primary(value));
    private static Choice SabotageStart(int value) => DamageChoice("sabotage.building-start", 39, value);
    private static Choice CaptureChoice(int index, int value, int odds = 3) => EscapeChoice("damage/capture/" + index, odds, value);
    private static CityEspionageDecision.Question DamageQuestion(CityEspionageDecision? decision, CityEspionageQuestion expected)
    { var question = (CityEspionageDecision.Question)decision!; Equal(expected, question.Kind); return question; }

    // V06 §§2,6: forward treaty confirmation is human-only, independent of autoplay, before draws/mutations.
    private static void CityDamageTreatyAndPoisonSize()
    {
        foreach (bool autoplay in new[] { false, true })
            foreach (int reply in new[] { -1, 0, 2 })
            {
                var f = new CityDamageFixture(); f.World.Multiplayer.Saved.AutoplayActive = autoplay;
                f.World.Civilizations[1].Treaties[2].Peace = true;
                using var op = f.Poison(); var question = DamageQuestion(op.Resume(), CityEspionageQuestion.TreatyBreach);
                Equal(question, op.Resume()); Equal(false, op.Completed); op.Resume(reply);
                Equal(true, op.Completed); Equal((sbyte)4, f.City.Size); Equal(true, f.Data.Unit(0).Token.Occupied);
                Equal(true, f.World.Civilizations[1].Treaties[2].Peace); Equal(0, f.Order.Count); f.Lifecycle.Random.RequireChoicesConsumed();
            }
        foreach (sbyte size in new sbyte[] { -128, 0, 1, 2, 127 })
            foreach (int mode in new[] { 0, 1 })
            {
                var f = new CityDamageFixture([DamageChoice("poison.escape-mode", 2, mode)]);
                f.Data.Unit(0).Type = new(46); f.Data.Unit(0).MovesUsed = 254; f.City.Size = size; f.City.FoodBox = 41;
                f.City.Tax = 7; f.City.SpecialistCount = 2; f.City.ShieldBox = 30;
                using var op = f.Poison(); Equal(null, op.Resume()); Equal(true, op.Completed);
                Equal(size < 2 ? size : (sbyte)(size - 1), f.City.Size); Equal((short)(size < 2 ? 0 : 41), f.City.FoodBox);
                Equal(true, f.City.KnownTo.Contains(1)); Equal(unchecked((byte)f.City.Size), f.City.RememberedSize[1]);
                Equal((short)7, f.City.Tax); Equal((byte)2, f.City.SpecialistCount); Equal((short)30, f.City.ShieldBox);
                Equal(false, f.Data.Unit(0).Token.Occupied); Equal((byte)1, f.Data.Unit(0).MovesUsed);
                Sequence(new[] { "redraw", "sound:Spy", "notice:WaterPoisoned" }, f.Order);
                Equal(20, f.World.Civilizations[2].Attitude[1]); f.Lifecycle.Random.RequireChoicesConsumed();
            }
        foreach (int mode in new[] { 0, 1 })
        {
            var choices = new List<Choice> { DamageChoice("poison.escape-mode", 2, mode) };
            if (mode == 1) choices.Add(EscapeChoice("damage/escape", 2, 0, true));
            choices.Add(EscapeChoice("damage/escape", 2, 1));
            var f = new CityDamageFixture(choices); f.Data.AddCity(1, 1, new(4, 4));
            using var op = f.Poison(); Equal(null, op.Resume()); Equal(true, op.Completed);
            Equal(new MapPoint(4, 4), f.Data.Unit(0).Position); Equal(true, f.Data.Unit(0).Flags.Veteran);
            Equal((byte)6, f.Data.Unit(0).MovesUsed); Equal((sbyte)3, f.City.Size); f.Lifecycle.Random.RequireChoicesConsumed();
        }
        // AI ignores its forward treaty; human victim has no reverse treaty, so the real incident ends without a prompt.
        var ai = new CityDamageFixture([DamageChoice("poison.escape-mode", 2, 0)]);
        ai.World.Globals.HumanPlayers = CivilizationSet.Only(2); ai.World.Civilizations[1].Treaties[2].Alliance = true;
        ai.Data.Unit(0).Type = new(46); using var poison = ai.Poison(); Equal(null, poison.Resume());
        Equal(0, ai.Notices.Count); Equal(true, ai.World.Civilizations[1].Treaties[2].Alliance); ai.Lifecycle.Random.RequireChoicesConsumed();
    }

    // V06 §6: cyclic scan, excluded targets, signed stock and the Spy/veteran production draw.
    private static void CitySabotageScanAndProductionOdds()
    {
        foreach (int start in new[] { 0, 1, 17, 35, 38 })
        {
            var f = new CityDamageFixture([SabotageStart(start)]); f.Data.Unit(0).Type = new(46); f.Install(1, 2, 17);
            using var op = f.Sabotage(); Equal(null, op.Resume()); Equal(false, f.Has(2)); Equal(true, f.Has(1) && f.Has(17));
            Equal(CityEspionageNoticeKind.BuildingSabotaged, f.Notices.Single().Kind); Equal(2, f.Notices.Single().Building);
            f.Lifecycle.Random.RequireChoicesConsumed();
        }
        foreach (bool spy in new[] { false, true })
            foreach (bool veteran in new[] { false, true })
                foreach (int roll in new[] { 0, 1 })
                {
                    int odds = (spy ? 3 : 2) + (veteran ? 1 : 0);
                    List<Choice> choices = [SabotageStart(4), DamageChoice("sabotage.production", odds, roll)];
                    if (spy) choices.Add(EscapeChoice("damage/escape", veteran ? 4 : 2, 0));
                    var f = new CityDamageFixture(choices); f.Data.Unit(0).Type = new((byte)(spy ? 47 : 46)); f.Data.Unit(0).Flags.Veteran = veteran;
                    f.City.ShieldBox = -1; f.Install(4); f.World.Civilizations[1].Advances = default(AdvanceSet).With(AdvanceIds.Gunpowder);
                    using var op = f.Sabotage();
                    if (spy) { DamageQuestion(op.Resume(), CityEspionageQuestion.SpecificSabotage); op.Resume(0); } else op.Resume();
                    Equal(true, op.Completed); Equal(roll == 0, f.Has(4)); Equal((short)(roll == 0 ? 0 : -1), f.City.ShieldBox);
                    Equal("sound:MediumExplosion", f.Order[0]);
                    Equal(roll == 0 ? CityEspionageNoticeKind.ProductionSabotaged : CityEspionageNoticeKind.BuildingSabotaged, f.Notices.Single().Kind);
                    if (roll == 0) Equal(f.Data.Rules.Units[4].Identity.Name, f.Notices.Single().ItemName);
                    f.Lifecycle.Random.RequireChoicesConsumed();
                }
        var none = new CityDamageFixture([SabotageStart(17)]); none.Data.Unit(0).Type = new(46); none.Install(1, 17); none.City.Production = -6;
        using var production = none.Sabotage(); production.Resume(); Equal("Building 6", none.Notices.Single().ItemName);
        Equal(true, none.Has(1) && none.Has(17)); none.Lifecycle.Random.RequireChoicesConsumed();
    }

    private static void CitySabotageTargetedCaptureChain()
    {
        var choices = new List<Choice> { SabotageStart(2), CaptureChoice(0, 1), CaptureChoice(1, 1), CaptureChoice(2, 1),
            EscapeChoice("damage/escape", 2, 0, true), EscapeChoice("damage/escape", 2, 1) };
        var f = new CityDamageFixture(choices); f.Install(1, 2, 8, 17); f.City.ShieldBox = 99;
        f.Data.AddCity(1, 1, new(4, 4));
        using var op = f.Sabotage(); DamageQuestion(op.Resume(), CityEspionageQuestion.SpecificSabotage);
        var menu = DamageQuestion(op.Resume(1), CityEspionageQuestion.SabotageTarget);
        Sequence(new[] { 2, 8, 17, 0 }, menu.Targets!.Select(t => t.Building));
        Equal(f.Data.Rules.Units[4].Identity.Name, menu.Targets![^1].Name);
        DamageQuestion(op.Resume(8), CityEspionageQuestion.HardSabotage); Equal(null, op.Resume(1));
        Equal(true, op.Completed); Equal(false, f.Has(8)); Equal(true, f.Has(1) && f.Has(2) && f.Has(17));
        Equal((short)99, f.City.ShieldBox); Equal(new MapPoint(4, 4), f.Data.Unit(0).Position);
        Equal(true, f.Data.Unit(0).Flags.Veteran); Equal((byte)6, f.Data.Unit(0).MovesUsed);
        Equal(SpyFate.Survived, f.Lifecycle.SpyNotices.Single().Fate); f.Lifecycle.Random.RequireChoicesConsumed();
        foreach (int lostAt in new[] { 0, 1, 2 })
        {
            choices = [SabotageStart(2)]; for (int i = 0; i <= lostAt; ++i) choices.Add(CaptureChoice(i, i == lostAt ? 0 : 1));
            f = new(choices); f.Install(1, 2, 8); f.Data.Unit(0).MovesUsed = 251;
            using var lost = f.Sabotage(); lost.Resume(); lost.Resume(1); lost.Resume(8); Equal(null, lost.Resume(1));
            Equal(true, lost.Completed); Equal(true, f.Has(8)); Equal(false, f.Data.Unit(0).Token.Occupied);
            Equal((byte)251, f.Data.Unit(0).MovesUsed); Equal(SpyFate.Caught, f.Lifecycle.SpyNotices.Single().Fate);
            Equal(0, f.Incidents.Notices.Count); Equal(0, f.Order.Count); f.Lifecycle.Random.RequireChoicesConsumed();
        }
        // Palace is rechecked after the actual hard-target decision. No invented capture attempt remains.
        f = new([SabotageStart(2), CaptureChoice(0, 1), EscapeChoice("damage/escape", 2, 1, true)]); f.Install(1, 2);
        using var fresh = f.Sabotage(); fresh.Resume(); fresh.Resume(1); fresh.Resume(2);
        BuildingQueries.SetBuilding(f.World, new(0), 1, false); fresh.Resume(1);
        Equal(false, f.Has(2)); f.Lifecycle.Random.RequireChoicesConsumed();
    }

    private static void CitySabotageCustomTargetsAndCancellation()
    {
        var cached = new CityDamageFixture([SabotageStart(2), CaptureChoice(0, 1), EscapeChoice("damage/escape", 2, 1, true)]);
        cached.Install(2); cached.City.ShieldBox = 20;
        using (var op = cached.Sabotage())
        {
            op.Resume(); cached.City.Production = -6;
            var menu = DamageQuestion(op.Resume(1), CityEspionageQuestion.SabotageTarget);
            Equal(cached.Data.Rules.Units[4].Identity.Name, menu.Targets![^1].Name); op.Resume(0);
            Equal((short)0, cached.City.ShieldBox); Equal(cached.Data.Rules.Units[4].Identity.Name, cached.Notices.Single().ItemName);
            cached.Lifecycle.Random.RequireChoicesConsumed();
        }
        foreach (int selected in new[] { 17, 35, 36, 37, 38, 39, -1 })
        {
            List<Choice> choices = [SabotageStart(2)];
            if (selected is >= 35 and <= 38) { choices.Add(CaptureChoice(0, 1)); choices.Add(EscapeChoice("damage/escape", 2, 1, true)); }
            var f = new CityDamageFixture(choices); f.Install(2, 17); f.City.ShieldBox = 8;
            using var op = f.Sabotage(); op.Resume(); op.Resume(1);
            if (selected == 39) { Throws<ArgumentOutOfRangeException>(() => op.Resume(selected)); Throws<InvalidOperationException>(() => op.Resume(2)); }
            else { Equal(null, op.Resume(selected)); Equal(true, op.Completed); }
            Equal(true, f.Has(2) && f.Has(17)); Equal((short)8, f.City.ShieldBox);
            if (selected == 17) { Equal(CityEspionageNoticeKind.Immune, f.Notices.Single().Kind); Equal(true, f.Data.Unit(0).Token.Occupied); }
            if (selected is >= 35 and <= 38) { Equal(selected, f.Notices.Single().Building); Equal("Building " + selected, f.Notices.Single().ItemName); }
            f.Lifecycle.Random.RequireChoicesConsumed();
        }
        foreach (bool hard in new[] { false, true })
        {
            var f = new CityDamageFixture([SabotageStart(2)]); f.Install(1, 2);
            using var op = f.Sabotage(); op.Resume(); if (hard) { op.Resume(1); op.Resume(2); }
            op.Resume(-1); Equal(true, op.Completed); Equal(true, f.Has(2)); Equal(0, f.Order.Count); f.Lifecycle.Random.RequireChoicesConsumed();
        }
    }

    private static void CitySabotageHumanDamageGateAndFailures()
    {
        foreach (bool humanVictim in new[] { false, true })
            foreach (bool production in new[] { false, true })
            {
                var choices = new List<Choice> { SabotageStart(2), DamageChoice("sabotage.production", 2, production ? 0 : 1) };
                var f = new CityDamageFixture(choices); f.Install(2); f.City.ShieldBox = 20; f.Data.Unit(0).Type = new(46);
                f.World.Globals.HumanPlayers = humanVictim ? CivilizationSet.Only(2) : default; f.World.Multiplayer.LocalHuman = 2;
                using var op = f.Sabotage(); op.Resume(); Equal(true, op.Completed);
                Equal(!(humanVictim && !production), f.Has(2)); Equal((short)(humanVictim && production ? 0 : 20), f.City.ShieldBox);
                Equal(0, f.Notices.Count); Equal(false, f.Data.Unit(0).Token.Occupied); f.Lifecycle.Random.RequireChoicesConsumed();
            }
        var failed = new CityDamageFixture([SabotageStart(2)]) { FailPublish = true }; failed.Install(2); failed.Data.Unit(0).Type = new(46);
        using var sabotage = failed.Sabotage(); Throws<InvalidOperationException>(() => sabotage.Resume());
        Equal(false, failed.Has(2)); Equal(true, failed.Data.Unit(0).Token.Occupied); Equal((byte)0, failed.Data.Unit(0).MovesUsed);
        Throws<InvalidOperationException>(() => sabotage.Resume()); Equal(1, failed.Order.Count(x => x == "publish")); failed.Lifecycle.Random.RequireChoicesConsumed();
        failed = new() { FailNotice = true }; using var poison = failed.Poison(); Throws<InvalidOperationException>(() => poison.Resume());
        Equal((sbyte)3, failed.City.Size); Equal((byte)3, failed.City.RememberedSize[1]); Equal(true, failed.Data.Unit(0).Token.Occupied);
        failed.Lifecycle.Random.RequireChoicesConsumed();
        var remote = new CityDamageFixture(); remote.World.Globals.Multiplayer = GameMode.Tcp;
        using var denied = remote.Poison(); Throws<InvalidOperationException>(() => denied.Resume()); Equal((sbyte)4, remote.City.Size); Equal(0, remote.Order.Count);
        var held = new CityDamageFixture([SabotageStart(2)]); held.Install(2); var disposed = held.Sabotage(); disposed.Resume(); disposed.Dispose();
        Throws<ObjectDisposedException>(() => disposed.Resume(0)); Equal(true, held.Has(2)); held.Lifecycle.Random.RequireChoicesConsumed();
    }

    private static void CityCaptureNestedEliminationAndNotice()
    {
        var f = new CityDamageFixture([CaptureChoice(0, 0)]);
        f.Data.Rules.Units[47] = f.Data.Rules.Units[47] with { Role = RuleRole.Settle };
        f.World.Civilizations[1].UnitsActive[47] = 1; f.World.Civilizations[1].CityCount = 0;
        f.Data.AddUnit(1, 3, 4, new(14, 8)); f.Data.Unit(1).Flags.SpyMission = true; f.Data.Unit(1).Counter = 1;
        f.World.Civilizations[1].Treaties[3].Peace = true;
        using var op = f.Lifecycle.Elimination.BeginCaught(0, 2, "damage/capture/0", f.Incidents.Incidents);
        var pretext = op.Resume()!; Equal(1, ((EspionagePretext)pretext).Victim.Civilization); Equal(3, ((EspionagePretext)pretext).Actor.Civilization);
        Equal(false, f.Data.Unit(0).Token.Occupied || f.Data.Unit(1).Token.Occupied);
        Equal(0, f.Lifecycle.SpyNotices.Count); Equal(null, op.Result); Equal(0, f.Lifecycle.Checks);
        Equal(null, op.Resume(1)); Equal(1, op.Result); Equal(EndReason.Defeat, f.World.PendingEndReason);
        var caught = f.Lifecycle.SpyNotices.Single(); Equal(SpyFate.Caught, caught.Fate); Equal(new UnitType(47), caught.Type);
        Equal(1, caught.Owner); Equal((byte)0, f.Data.Unit(0).MovesUsed);
        op.Resume(1); Equal(1, f.Lifecycle.SpyNotices.Count); f.Lifecycle.Random.RequireChoicesConsumed();
    }

    private static void CityPoisonSessionVictimRouting()
    {
        foreach (bool accept in new[] { false, true })
        {
            using var runtime = new SessionRuntime(new(), new(193));
            var f = new CityDamageFixture([DamageChoice("poison.escape-mode", 2, 0), IncidentChoice(0, call: "damage/escape/incident")],
                CreationFixture(new(runtime.Process, runtime.World)));
            f.Data.Unit(0).Type = new(46); f.World.Globals.HumanPlayers = CivilizationSet.Only(1).With(2); f.Incidents.Treaty();
            ParticipantId actorSeat = default, victimSeat = default;
            runtime.RegisterCommand(new(new(1, "test.poison", "Authored poison integration", AccessClass.Player, new([]), new([])),
                (_, _, _) => new(), _ => new CityEspionageDamageOperation(f.Poison(), c => c == 1 ? actorSeat : victimSeat)));
            runtime.BeginClientSession(new()); var actor = SessionClient(runtime); var victim = SessionClient(runtime, 2);
            actorSeat = actor.Identity.Participant; victimSeat = victim.Identity.Participant;
            var command = actor.Submit(new("test.poison", new([]))).Correlation.Command;
            runtime.AdvanceOne(); var confirm = RequiredInteraction(actor, command);
            Equal(null, victim.Result(command)); Equal(false, victim.Reply(confirm, 1).Accepted);
            Equal(true, actor.Reply(confirm, 1).Accepted); runtime.AdvanceOne();
            var incident = RequiredInteraction(victim, command); Equal(false, actor.Reply(incident, 1).Accepted);
            Equal((sbyte)3, f.City.Size); Equal(false, f.Data.Unit(0).Token.Occupied);
            if (accept) Equal(true, victim.Reply(incident, 1).Accepted); else Equal(true, runtime.CancelCommand(command).Accepted);
            runtime.AdvanceOne(); Equal(CommandStatus.Completed, RequiredResult(actor, command).Status);
            Equal(accept, f.World.Civilizations[2].Treaties[1].War); Equal((sbyte)3, f.City.Size); f.Lifecycle.Random.RequireChoicesConsumed();
        }
    }
}
