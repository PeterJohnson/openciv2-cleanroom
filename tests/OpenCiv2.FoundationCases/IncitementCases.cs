using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Formats.Classic;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private sealed class IncitementFixture : ICityIncitementEffects
    {
        public CityCaptureFixture Capture { get; }
        public RevelationFixture Data => Capture.Data;
        public GameState World => Data.World;
        public CityIncitement Mission { get; }
        public ClassicTextInput Text { get; } = new();
        public int Reads, Denials, Sounds;
        public List<int> PaidBalances { get; } = [];
        public bool FailStatus;
        public IncitementFixture(List<Choice>? choices = null, RevelationFixture? data = null, CityCaptureFixture? capture = null)
        {
            Capture = capture ?? new(choices, data); World.Civilizations[1].Gold = 1000;
            Text.Files["scenario/REVOLT.TXT"] = "@DISSIDENTOPTIONS\nCancel authored\nOrdinary authored\nIncident authored\nDouble authored\nUnused fifth line\n";
            Mission = new(World, Capture.Life.Elimination, Capture.Theft.Damage.Incidents.Incidents, Capture.Capture, () =>
            { ++Reads; using var reader = Text.Reader(); return ClassicRevoltOptions.Read(reader, "revolt"); }, this);
        }
        public CityMissionContext Context => Capture.Theft.Damage.Mission;
        public CityIncitement.Operation Begin(CityMissionContext? context = null) => Mission.Begin(context ?? Context, 77, "revolt");
        public void Complete(CityIncitement.Operation op)
        {
            int limit = 50;
            while (!op.Completed && --limit > 0)
            {
                var pending = op.Resume();
                if (pending is RevoltDecision.Capture { Decision: CityCaptureDecision.Window }) { op.Resume(new(0)); }
                else if (pending is not null) { throw new InvalidOperationException("Unexpected revolt decision " + pending); }
            }
            Equal(true, op.Completed); Capture.Life.Random.RequireChoicesConsumed();
        }
        public void NoRevolt(CityTarget city, string name, int actor) => ++Denials;
        public void RefreshStatus(int actor)
        {
            PaidBalances.Add(World.Civilizations[actor].Gold);
            if (FailStatus) throw new InvalidOperationException("Status presentation unavailable");
        }
        public void SpySound() => ++Sounds;
    }
    private static Choice RevoltEscape(int roll, bool veteran = false) => EscapeChoice("revolt/escape", veteran ? 4 : 2, roll);
    private static Choice RevoltSenate(int roll = 0) => Choice.Create(new("V06.incident-senate", "revolt/incident"), new SamplingLaw.PrimaryModulo(2), ChoiceCode.Primary(roll));
    private static RevoltOffer RevoltOffered(CityIncitement.Operation op) => ((RevoltDecision.Offer)op.Resume()!).Request;

    private static void IncitementAdmissionAndCapital()
    {
        foreach (var (human, autoplay) in new[] { (true, false), (true, true), (false, false) })
        {
            var f = new IncitementFixture(); f.World.Civilizations[2].Government = Government.Democracy;
            f.World.Multiplayer.Saved.AutoplayActive = autoplay;
            if (!human) f.World.Globals.HumanPlayers = CivilizationSet.Only(3);
            // Democracy returns before reading the supplied unit record.
            using var op = f.Begin(f.Context with { Unit = 9999 }); f.Complete(op);
            Equal(human && !autoplay ? 1 : 0, f.Denials); Equal(null, op.Fallback); Equal(0, f.Reads);
        }
        foreach (bool human in new[] { false, true })
        {
            var f = new IncitementFixture(); if (!human) f.World.Globals.HumanPlayers = CivilizationSet.Only(3);
            f.World.Entities.Cities.Storage[1].Position = f.Data.Unit(0).Position;
            BuildingQueries.SetBuilding(f.World, new(1), BuildingIds.Palace, true);
            f.Capture.City.Position = new(32, 20); // Distance comes from the unit, not the target city.
            using var op = f.Begin(); f.Complete(op); Equal(human ? null : CityMission.Steal, op.Fallback);
            Equal(0, f.Reads + f.Denials + f.Sounds); Equal(1000, f.World.Civilizations[1].Gold);
        }
        var actorCommunism = new IncitementFixture(); actorCommunism.World.Civilizations[1].Government = Government.Communism;
        using var actor = actorCommunism.Begin(); Equal(119, RevoltOffered(actor).Price); actor.Resume(new(0));
    }

    private static void IncitementPriceAndDiscounts()
    {
        foreach (var (variant, expected) in new[] { (0, 119), (1, 285), (2, 30), (3, 24), (4, 238), (5, 12500), (6, 0), (7, 2787862), (8, 35) })
        {
            var f = new IncitementFixture(); var w = f.World; w.Civilizations[1].Gold = int.MaxValue;
            if (variant == 1)
            { w.Civilizations[2].Government = Government.Communism; f.Capture.Theft.Damage.Install(7); }
            if (variant is 2 or 3 or 8)
            { f.Capture.City.Flags.Disorder = true; f.Capture.City.Founder = new(1); }
            if (variant == 3) f.Data.Unit(0).Flags.Veteran = true;
            if (variant == 4) f.Data.AddUnit(1, 2, 4, f.Capture.City.Position);
            if (variant == 5) w.Civilizations[2].Gold = int.MaxValue;
            if (variant == 6) f.Capture.City.Size = 0;
            if (variant == 7) { f.Capture.City.Size = 127; w.Civilizations[2].Gold = 1000000; }
            if (variant == 8) f.Data.Unit(0).Type = new(46);
            using var op = f.Begin();
            var offer = RevoltOffered(op); Equal(expected, offer.Price); Equal(1, f.Reads);
            Sequence(new[] { 0, 1 }, offer.Options.Select(o => o.Value)); Equal("Ordinary authored", offer.Options[1].Text);
            op.Resume(new(0)); Equal(true, op.Completed); Equal(0, f.PaidBalances.Count); f.Capture.Life.Random.RequireChoicesConsumed();
        }
        // Captured Spy status, not the live unit type, determines the discount; veteran is live.
        var captured = new IncitementFixture(); var initial = captured.Context;
        captured.Data.Unit(0).Type = new(46); captured.Data.Unit(0).Flags.Veteran = true;
        using var spy = captured.Begin(initial); Equal(95, RevoltOffered(spy).Price);
    }

    private static void IncitementOfferResourcesAndGates()
    {
        foreach (var (gold, treaty, expected) in new[] { (118, false, 0), (119, false, 2), (119, true, 2), (237, true, 2), (238, true, 3) })
        {
            var f = new IncitementFixture(); f.World.Civilizations[1].Gold = gold; f.World.Civilizations[1].Treaties[2].Peace = treaty;
            using var op = f.Begin(); var offer = RevoltOffered(op); Equal(119, offer.Price); Equal(expected, offer.Options.Count);
            Equal(expected == 0 ? 0 : 1, f.Reads);
            if (expected > 0)
            {
                Equal(treaty ? "Incident authored" : "Ordinary authored", offer.Options[1].Text);
                Equal(5, f.Text.Traces["scenario/REVOLT.TXT"].Reads); Equal(1, f.Text.Traces["scenario/REVOLT.TXT"].Closes);
            }
            if (expected == 3) Equal("Double authored", offer.Options[2].Text);
            op.Resume(new(-1)); Equal(true, op.Completed); Equal(gold, f.World.Civilizations[1].Gold);
        }
        var absent = new IncitementFixture(); absent.Text.Files.Clear(); using var missing = absent.Begin(); absent.Complete(missing);
        Equal(1, absent.Reads); Equal(0, absent.PaidBalances.Count); Equal(true, absent.Data.Unit(0).Token.Occupied);
        var truncated = new IncitementFixture(); truncated.Text.Files["scenario/REVOLT.TXT"] = "@DISSIDENTOPTIONS\nA\nB\nC\n";
        using var shortText = truncated.Begin(); Throws<InvalidDataException>(() => shortText.Resume()); Throws<InvalidOperationException>(() => shortText.Resume());
        Equal(1, truncated.Text.Traces["scenario/REVOLT.TXT"].Closes); Equal(1000, truncated.World.Civilizations[1].Gold);
        // The human double option is capped independently of affordability and signed doubling.
        var large = new IncitementFixture(); large.Capture.City.Size = 127; large.World.Civilizations[2].Gold = 49999000;
        large.World.Entities.Cities.Storage[1].Position = new(12, 10); BuildingQueries.SetBuilding(large.World, new(1), 1, true);
        large.Data.AddUnit(1, 2, 4, large.Capture.City.Position); large.World.Civilizations[1].Gold = int.MaxValue;
        large.World.Civilizations[1].Treaties[2].Alliance = true; large.Data.Unit(0).Type = new(46);
        using var tooLarge = large.Begin(); var high = RevoltOffered(tooLarge);
        Equal(1270000000, high.Price); Sequence(new[] { 0, 1 }, high.Options.Select(o => o.Value));
        foreach (int price in new[] { 15000, 15001 })
        {
            var f = new IncitementFixture(); f.Capture.City.Size = 1; f.World.Civilizations[2].Gold = price * 19 - 1000;
            f.World.Civilizations[1].Gold = 40000; f.World.Civilizations[1].Treaties[2].Peace = true;
            f.Data.AddUnit(1, 2, 4, f.Capture.City.Position); f.Data.Unit(0).Type = new(46);
            using var boundary = f.Begin(); var offer = RevoltOffered(boundary);
            Equal(price, offer.Price); Equal(price == 15000 ? 3 : 2, offer.Options.Count);
        }
    }

    private static void IncitementPaymentEscapeAndTransfer()
    {
        foreach (int choice in new[] { 1, 2, 3 })
        {
            var f = new IncitementFixture(choice == 1 ? [RevoltEscape(0), RevoltSenate()] : [RevoltEscape(0)]); var w = f.World;
            w.Globals.HumanPlayers = CivilizationSet.Only(1).With(2);
            w.Civilizations[1].Treaties[2].Peace = w.Civilizations[2].Treaties[1].Peace = true;
            w.Civilizations[1].SubversionCount = 32767; f.Capture.Theft.Damage.Install(4);
            using var op = f.Begin(); Equal(119, RevoltOffered(op).Price);
            var pending = op.Resume(new(choice)); Equal(1000 - 119 * choice, f.PaidBalances.Single());
            Equal((short)-32767, w.Civilizations[1].SubversionCount); Equal(false, f.Data.Unit(0).Token.Occupied);
            if (choice == 1)
            {
                var incident = (RevoltDecision.Lifecycle)pending!; Equal(2, ((EspionagePretext)incident.Decision).Victim.Civilization);
                Equal(new CivilizationSlot(2), f.Capture.City.Owner); Equal(0, f.Sounds); Equal(incident, op.Resume());
                pending = op.Resume(new(0));
            }
            Equal(true, pending is RevoltDecision.Capture { Decision: CityCaptureDecision.Window });
            Equal(new CivilizationSlot(1), f.Capture.City.Owner); Equal(1, f.Sounds); Equal(1000 - 119 * choice + 50, w.Civilizations[1].Gold);
            Equal(choice == 2, f.Capture.Theft.Damage.Has(4)); Equal(choice != 1, w.Civilizations[1].Treaties[2].Peace);
            Equal(choice == 2, f.Capture.Notices.Single(n => n.Kind == CaptureNoticeKind.Capture).Restored);
            if (choice == 2) Equal(true, w.Civilizations[2].Treaties[1].Vendetta);
            f.Complete(op); Equal(new CivilizationSlot(2), f.Capture.City.Founder);
            Equal(1, f.Capture.Life.SpyNotices.Count); // Failed escape is not refunded and does not cancel capture.
        }
        var retained = new IncitementFixture([RevoltEscape(1)]); using var changed = retained.Begin(); RevoltOffered(changed);
        retained.World.Civilizations[1].Gold = 17; retained.World.Civilizations[2].Gold = 10000;
        retained.World.Civilizations[2].Government = Government.Democracy; retained.Capture.City.Name = "Changed";
        changed.Resume(new(3)); Equal(-340, retained.PaidBalances.Single()); retained.Complete(changed);
        Equal(true, retained.Data.Unit(0).Token.Occupied && retained.Data.Unit(0).Flags.Veteran);
        Equal(new CivilizationSlot(1), retained.Capture.City.Owner); // No renewed immunity or affordability check.

        var senate = new IncitementFixture([RevoltEscape(1), RevoltSenate(1)]); var sw = senate.World;
        sw.Globals.HumanPlayers = CivilizationSet.Only(1).With(2); sw.Civilizations[1].Government = Government.Republic;
        sw.Civilizations[1].Treaties[2].Peace = sw.Civilizations[2].Treaties[1].Peace = true;
        using var scandal = senate.Begin(); RevoltOffered(scandal); Equal(true, scandal.Resume(new(1)) is RevoltDecision.Lifecycle);
        scandal.Resume(new(0)); Equal(Government.Anarchy, sw.Civilizations[1].Government);
        Equal(false, senate.Capture.City.Flags.Disorder); // C09 sees the government after the real incident child.
        Equal(true, senate.Capture.Theft.Damage.Incidents.Notices.Any(n => n.Kind == EspionageIncidentKind.SenateScandal));
        senate.Complete(scandal);
    }

    private static void IncitementAiAndAutoplay()
    {
        foreach (int gate in new[] { 0, 1, 2 })
        {
            var f = new IncitementFixture(); var w = f.World; w.Globals.HumanPlayers = CivilizationSet.Only(3);
            if (gate == 0) w.Civilizations[1].Gold = 237;
            if (gate == 1) w.Civilizations[1].Treaties[2].Ceasefire = true;
            if (gate == 2)
            {
                f.Capture.City.Size = 127; w.Civilizations[2].Gold = 49999000; w.Civilizations[1].Gold = int.MaxValue;
                w.Entities.Cities.Storage[1].Position = new(12, 10); BuildingQueries.SetBuilding(w, new(1), 1, true);
                f.Data.AddUnit(1, 2, 4, f.Capture.City.Position); f.Data.Unit(0).Type = new(46);
            }
            using var op = f.Begin(); f.Complete(op);
            Equal(CityMission.Steal, op.Fallback); Equal(0, f.Reads + f.Sounds); Equal(true, f.Data.Unit(0).Token.Occupied);
        }
        var ai = new IncitementFixture([RevoltEscape(1)]); ai.World.Globals.HumanPlayers = CivilizationSet.Only(3);
        ai.World.Civilizations[1].Gold = 238; using var accepted = ai.Begin(); ai.Complete(accepted);
        Equal(169, ai.World.Civilizations[1].Gold); Equal((short)2, ai.World.Civilizations[1].SubversionCount);
        Equal(null, accepted.Fallback); Equal(0, ai.Reads); Equal(new CivilizationSlot(1), ai.Capture.City.Owner);
        var auto = new IncitementFixture([RevoltEscape(1)]); auto.World.Multiplayer.Saved.AutoplayActive = true;
        auto.World.Civilizations[1].Gold = 0; using var autoplay = auto.Begin(); auto.Complete(autoplay);
        Equal(-69, auto.World.Civilizations[1].Gold); Equal(0, auto.Reads); Equal((short)2, auto.World.Civilizations[1].SubversionCount);
    }

    private static void IncitementFailureAndNestedEscape()
    {
        var failed = new IncitementFixture { FailStatus = true }; using var fault = failed.Begin(); RevoltOffered(fault);
        Throws<InvalidOperationException>(() => fault.Resume(new(1))); Throws<InvalidOperationException>(() => fault.Resume(new(1)));
        Equal(881, failed.World.Civilizations[1].Gold); Equal((short)2, failed.World.Civilizations[1].SubversionCount);
        Equal(true, failed.Data.Unit(0).Token.Occupied); Equal(new CivilizationSlot(2), failed.Capture.City.Owner);
        var held = new IncitementFixture(); var disposed = held.Begin(); RevoltOffered(disposed); disposed.Dispose();
        Throws<ObjectDisposedException>(() => disposed.Resume(new(1))); Equal(1000, held.World.Civilizations[1].Gold);
        var remote = new IncitementFixture(); remote.World.Globals.Multiplayer = GameMode.Tcp;
        using var denied = remote.Begin(); Throws<InvalidOperationException>(() => denied.Resume()); Equal(0, remote.Reads);

        // Loss of an authored last Settler can suspend in a foreign spy incident before the explicit revolt incident.
        var nested = new IncitementFixture([RevoltSenate()]); var w = nested.World; nested.Data.Unit(0).Type = new(46);
        nested.Data.Rules.Units[46] = nested.Data.Rules.Units[46] with { Role = RuleRole.Settle };
        w.Civilizations[1].UnitsActive[46] = 1; w.Globals.HumanPlayers = CivilizationSet.Only(1).With(2);
        nested.Data.AddUnit(1, 3, 4, new(20, 10)); nested.Data.Unit(1).Flags.SpyMission = true; nested.Data.Unit(1).Counter = 1;
        w.Civilizations[1].Treaties[3].Peace = true;
        w.Civilizations[1].Treaties[2].Peace = w.Civilizations[2].Treaties[1].Peace = true;
        nested.Capture.Life.Recreate = c => Creation(nested.Data, c, nested.Capture.Life.Random);
        using var loss = nested.Begin(); Equal(142, RevoltOffered(loss).Price);
        var first = (RevoltDecision.Lifecycle)loss.Resume(new(1))!; Equal(1, ((EspionagePretext)first.Decision).Victim.Civilization);
        Equal(858, w.Civilizations[1].Gold); Equal(new CivilizationSlot(2), nested.Capture.City.Owner); Equal(0, nested.Sounds);
        var second = (RevoltDecision.Lifecycle)loss.Resume(new(0))!; Equal(2, ((EspionagePretext)second.Decision).Victim.Civilization);
        Equal(new CivilizationSlot(2), nested.Capture.City.Owner);
        loss.Resume(new(0)); nested.Complete(loss); Equal(new CivilizationSlot(1), nested.Capture.City.Owner);
    }

    private static void IncitementSessionPrivacyAndClosure()
    {
        using (var runtime = new SessionRuntime(new(), new(201)))
        {
            var f = new IncitementFixture([RevoltEscape(0), RevoltSenate()], CreationFixture(new(runtime.Process, runtime.World)));
            f.World.Globals.HumanPlayers = CivilizationSet.Only(1).With(2);
            f.World.Civilizations[1].Treaties[2].Peace = f.World.Civilizations[2].Treaties[1].Peace = true;
            ParticipantId actorSeat = default, victimSeat = default; int windows = 0;
            runtime.RegisterCommand(new(new(1, "test.revolt", "Authored revolt mission", AccessClass.Player, new([]), new([new("fallback")])),
                (_, _, _) => new(), _ => new CityIncitementOperation(f.Begin(), c => c == 1 ? actorSeat : victimSeat, request =>
                { ++windows; return new("Authored P06 boundary", victimSeat, new CommandValue.City(request.City), new([0])); })));
            runtime.BeginClientSession(new()); var actor = SessionClient(runtime); var victim = SessionClient(runtime, 2);
            actorSeat = actor.Identity.Participant; victimSeat = victim.Identity.Participant;
            var command = actor.Submit(new("test.revolt", new([]))).Correlation.Command; runtime.AdvanceOne();
            var offer = RequiredInteraction(actor, command); Equal(false, victim.Reply(offer, 1).Accepted);
            Equal("Incident authored", RequiredResult(actor, command).Interaction!.Spec.Options[1].Label);
            Equal(true, actor.Reply(offer, 1).Accepted); runtime.AdvanceOne();
            var incident = RequiredInteraction(victim, command); Equal(null, RequiredResult(actor, command).Interaction);
            Equal(false, actor.Reply(incident, 0).Accepted); Equal(false, f.Data.Unit(0).Token.Occupied);
            Equal(true, victim.Reply(incident, 0).Accepted); runtime.AdvanceOne(); var window = RequiredInteraction(actor, command);
            Equal(false, victim.Reply(window, 0).Accepted); Equal(true, actor.Reply(window, 0).Accepted); runtime.AdvanceOne();
            Equal(CommandStatus.Completed, RequiredResult(actor, command).Status); Equal(1, windows); f.Capture.Life.Random.RequireChoicesConsumed();
        }
        foreach (bool cancel in new[] { false, true })
        {
            var f = new IncitementFixture(); f.World.Civilizations[1].Gold = 0;
            var context = new OperationContext(f.World, f.Data.Process, f.Capture.Life.Random, new PresentationRandom(3, "revolt"),
                new(default, new(1)), default, (_, _) => { }, () => { });
            using var op = new CityIncitementOperation(f.Begin(), _ => new(1), _ => throw new InvalidOperationException("No window on cancelled offer"));
            var offer = op.Resume(context, new()); Equal(CommandStatus.AwaitingInteraction, offer.Status);
            Sequence(new[] { 0 }, offer.Interaction!.Choices); Equal("Close", offer.Interaction.Options.Single().Label);
            Equal(CommandStatus.Completed, op.Resume(context, new(cancel ? OperationInputKind.Cancel : OperationInputKind.Timeout)).Status);
            Equal(0, f.Reads); Equal(0, f.World.Civilizations[1].Gold);
        }
        using var emptyRuntime = new SessionRuntime(new(), new(202));
        var poor = new IncitementFixture(data: CreationFixture(new(emptyRuntime.Process, emptyRuntime.World)));
        poor.World.Civilizations[1].Gold = 0; ParticipantId seat = default;
        emptyRuntime.RegisterCommand(new(new(1, "test.unaffordable-revolt", "Authored unaffordable offer", AccessClass.Player, new([]), new([new("fallback")])),
            (_, _, _) => new(), _ => new CityIncitementOperation(poor.Begin(), _ => seat, _ => throw new InvalidOperationException("No capture window"))));
        emptyRuntime.BeginClientSession(new()); var client = SessionClient(emptyRuntime); seat = client.Identity.Participant;
        var closeCommand = client.Submit(new("test.unaffordable-revolt", new([]))).Correlation.Command; emptyRuntime.AdvanceOne();
        var close = RequiredInteraction(client, closeCommand); Equal(true, client.Reply(close, 0).Accepted); emptyRuntime.AdvanceOne();
        Equal(CommandStatus.Completed, RequiredResult(client, closeCommand).Status); Equal(0, poor.Reads);
    }
}
