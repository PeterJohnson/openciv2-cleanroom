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
    private sealed class CityMissionFixture : ICityEspionageVisitEffects, INuclearEffects
    {
        public IncitementFixture Revolt { get; }
        public CityCaptureFixture Capture => Revolt.Capture;
        public RevelationFixture Data => Capture.Data;
        public GameState World => Data.World;
        public CityMissions Missions { get; }
        public ClassicTextInput Text { get; } = new();
        public List<CityEspionageVisitNotice> Visits { get; } = [];
        public int Reads, Stops, Animations;
        public bool FailRead;
        public CityMissionFixture(List<Choice>? choices = null, RevelationFixture? data = null, CityCaptureFixture? capture = null)
        {
            Revolt = new(choices, data, capture);
            var life = Capture.Life; var incidents = Capture.Theft.Damage.Incidents.Incidents;
            var nuclear = new NuclearEffects(World, life.Random, Data.Stacks, life.Loss, life.Elimination, incidents, this);
            var device = new PlantedNuclearDevice(World, life.Random, life.Elimination, incidents, nuclear, _ => { });
            var visits = new CityEspionageVisits(World, life.Loss, life.Elimination, incidents, this);
            Text.Files["scenario/MISSIONS.TXT"] = "@SPYOPTIONS\nEmbassy authored\nInvestigate authored\nSteal authored\nSabotage authored\nPoison authored\nDevice authored\nIncite authored\nUnused\n";
            Missions = new(World, Data.Rules, life.Random, new(visits, Capture.Theft.Theft, Capture.Theft.Damage.Damage, device, Revolt.Mission), () =>
            {
                ++Reads; if (FailRead) throw new InvalidOperationException("Mission content unavailable");
                using var reader = Text.Reader(); return ClassicCityMissionOptions.Read(reader, "missions", " (Spy authored)");
            }, () => ++Stops);
        }
        public CityMissions.Operation Begin() => Missions.Begin(0, new(0), 77, "city");
        public CityMissionMenu Menu(CityMissions.Operation operation) => ((CityMissionDecision.Menu)operation.Resume()!).Request;
        public CityMissionDecision? Choose(CityMissions.Operation operation, CityMission mission)
        { Menu(operation); return operation.Resume(new((int)mission)); }
        public void Complete(CityMissions.Operation operation)
        {
            int limit = 50;
            while (!operation.Completed && --limit > 0)
            { if (operation.Resume() is { } pending) throw new InvalidOperationException("Unexpected city decision " + pending); }
            Equal(true, operation.Completed); Capture.Life.Random.RequireChoicesConsumed();
        }
        public void Sound() { }
        public void Notice(CityEspionageVisitNotice notice) => Visits.Add(notice);
        public void Notice(NuclearNotice notice) { }
        public void Animate(MapPoint target) => ++Animations;
        public void Redraw(MapPoint point) { }
    }
    private static Choice MissionDraw(string site, CityMission mission, int bound, int roll) => Choice.Create(new(site, "city/mission/" + (int)mission), new SamplingLaw.PrimaryModulo(bound), ChoiceCode.Primary(roll));
    private static Choice MissionEscape(CityMission mission, int roll, int odds = 2, string suffix = "escape") => EscapeChoice("city/mission/" + (int)mission + "/" + suffix, odds, roll);

    private static void CityMissionMenuResourcesAndGates()
    {
        var f = new CityMissionFixture(); using var op = f.Begin(); var menu = f.Menu(op);
        Sequence(new[] { CityMission.Embassy, CityMission.Investigate, CityMission.Steal, CityMission.Sabotage, CityMission.Poison, CityMission.Incite }, menu.Options.Select(o => o.Mission));
        Equal("Embassy authored (Spy authored)", menu.Options[0].Text); Equal("Renamed agent", menu.UnitName); Equal("Birch", menu.CityName);
        Equal(true, f.World.Civilizations[1].Treaties[2].Contact && f.World.Civilizations[2].Treaties[1].Contact);
        Equal(null, f.World.Espionage.LastMission); Equal(menu, f.Menu(op)); Equal(1, f.Reads); Equal(1, f.Stops);
        var trace = f.Text.Traces["scenario/MISSIONS.TXT"]; Equal(8, trace.Reads); Equal(1, trace.Closes);
        op.Resume(new(-1)); f.Complete(op); Equal(null, f.World.Espionage.LastMission);
        for (int variant = 0; variant < 8; ++variant)
        {
            f = new(); var w = f.World; f.Capture.City.Flags.TechnologyStolen = true; f.Capture.City.Size = 1;
            f.Data.Unit(0).Type = new(46); BuildingQueries.SetBuilding(w, new(0), BuildingIds.Palace, true);
            if (variant == 0) { f.Data.AddCity(2, 1, new(2, 2)); w.Globals.WonderCity[WonderIds.MarcoPolo] = 2; }
            if (variant == 1) w.Civilizations[1].Treaties[2].Embassy = true;
            if (variant == 2) { f.Data.AddCity(2, 1, new(2, 2)); w.Globals.WonderCity[WonderIds.UnitedNations] = 2; }
            if (variant >= 3)
            {
                f.Data.Unit(0).Type = new(47); w.Civilizations[1].Advances = w.Civilizations[1].Advances.With(AdvanceIds.Rocketry).With(AdvanceIds.NuclearFission);
                w.Globals.WonderCity[WonderIds.Manhattan] = BuildingQueries.Destroyed;
            }
            if (variant == 4) w.Civilizations[1].Advances = w.Civilizations[1].Advances.With(AdvanceIds.Rocketry, false);
            if (variant == 5) w.Civilizations[1].Advances = w.Civilizations[1].Advances.With(AdvanceIds.NuclearFission, false);
            if (variant == 6) w.Globals.WonderCity[WonderIds.Manhattan] = BuildingQueries.Unbuilt;
            if (variant == 7) { f.Data.Rules.WonderObsolescence[WonderIds.Manhattan] = (AdvanceReference)AdvanceIds.Rocketry; }
            using var gated = f.Begin(); var choices = f.Menu(gated).Options.Select(o => o.Mission).ToArray();
            Equal(variant is not (1 or 2), choices.Contains(CityMission.Embassy)); Equal(variant >= 3, choices.Contains(CityMission.Steal));
            Equal(false, choices.Contains(CityMission.Poison)); Equal(false, choices.Contains(CityMission.Incite));
            Equal(variant == 3, choices.Contains(CityMission.PlantNuclear)); Equal(8, f.Text.Traces["scenario/MISSIONS.TXT"].Reads);
        }
        f = new(); f.World.Civilizations[2].Government = Government.Democracy;
        using var democracy = f.Begin(); Equal(true, f.Menu(democracy).Options.Any(o => o.Mission == CityMission.Incite));
        f = new(); f.Text.Files.Clear(); using var missing = f.Begin(); f.Complete(missing); Equal(true, f.World.Civilizations[1].Treaties[2].Contact);
        f = new(); f.Text.Files["scenario/MISSIONS.TXT"] = "@SPYOPTIONS\nOnly one\n";
        using var shortFile = f.Begin(); Throws<InvalidDataException>(() => shortFile.Resume()); Equal(1, f.Text.Traces["scenario/MISSIONS.TXT"].Closes);
        Throws<InvalidOperationException>(() => shortFile.Resume()); Equal(null, f.World.Espionage.LastMission);
    }

    private static void CityMissionFallbackSelection()
    {
        // Vendetta consumes its choice even for a non-Spy, before Peace/Alliance overrides.
        foreach (bool spy in new[] { false, true })
            foreach (int variant in Enumerable.Range(0, 9))
            {
                var f = new CityMissionFixture([Choice.Create(new("V06.city-mission.vendetta", "select"), new SamplingLaw.PrimaryModulo(3), ChoiceCode.Primary(0))]);
                var w = f.World; w.Civilizations[1].Treaties[2].Vendetta = true;
                if (variant is 1 or 2) w.Civilizations[1].Treaties[2].Peace = true;
                if (variant is 2 or 3) w.Globals.Difficulty = Difficulty.Emperor;
                if (variant is 4 or 5) { w.Globals.Difficulty = Difficulty.King; w.Civilizations[2].SubversionCount = (short)(variant == 4 ? 5 : 6); }
                if (variant == 6) { w.Globals.Difficulty = Difficulty.Deity; w.Civilizations[2].Government = Government.Democracy; }
                if (variant == 7) { w.Civilizations[1].Treaties[2].Alliance = true; f.Capture.City.Flags.TechnologyStolen = true; }
                if (variant == 8) { f.Capture.City.Flags.TechnologyStolen = true; w.Civilizations[1].Treaties[2].Ceasefire = true; }
                var expected = variant switch
                {
                    1 => CityMission.Steal,
                    2 => CityMission.Incite,
                    7 => spy ? CityMission.Steal : CityMission.Embassy,
                    8 => spy ? CityMission.Poison : CityMission.Embassy,
                    _ => spy ? CityMission.Poison : variant is 3 or 5 ? CityMission.Incite : CityMission.Steal
                };
                Equal(expected, f.Missions.SelectFallback(f.Revolt.Context with { IsSpy = spy }, "select"));
                Equal(null, w.Espionage.LastMission); f.Capture.Life.Random.RequireChoicesConsumed();
            }
        var noVendetta = new CityMissionFixture(); noVendetta.Capture.City.Flags.TechnologyStolen = true;
        Equal(CityMission.Sabotage, noVendetta.Missions.SelectFallback(noVendetta.Revolt.Context with { IsSpy = false }, "select"));
        noVendetta.Capture.Life.Random.RequireChoicesConsumed();
    }

    private static void CityMissionVisitsAndRetainedContext()
    {
        var f = new CityMissionFixture(); f.World.Civilizations[1].Treaties[2].Embassy = true;
        using var op = f.Begin(); f.Menu(op);
        // A hidden custom reply is admitted, then the real Embassy child requests Investigation.
        Equal(null, op.Resume(new((int)CityMission.Embassy))); Equal(CityMission.Embassy, f.World.Espionage.LastMission); Equal(false, op.Completed);
        f.Data.Unit(0).Type = new(46); // Captured Spy classification survives the fallback boundary.
        var pending = (CityMissionDecision.Mission)op.Resume()!; var visit = ((CityEspionageDecision.Visit)pending.Decision).Request;
        Equal(CityEspionageVisitKind.City, visit.Kind); Equal(1, visit.Actor); Equal(CityMission.Investigate, f.World.Espionage.LastMission);
        Equal(1, f.Reads); Equal(pending, op.Resume()); op.Resume(new(0)); f.Complete(op);
        Equal(true, f.Capture.City.Flags.Investigated); Equal(true, f.Data.Unit(0).Token.Occupied); Equal((byte)1, f.Data.Unit(0).MovesUsed);
        Equal(0, f.Visits.Count); // Victim is AI.
        f = new(); using var unknown = f.Begin(); f.Menu(unknown); unknown.Resume(new(99)); f.Complete(unknown); Equal((CityMission)99, f.World.Espionage.LastMission);
        f = new(); using var chat = f.Begin(); f.Menu(chat); Throws<NotSupportedException>(() => chat.Resume(new(7))); Equal(CityMission.Chat, f.World.Espionage.LastMission);
    }

    private static void CityMissionRealBodies()
    {
        var f = new CityMissionFixture([MissionDraw("V06.poison.escape-mode", CityMission.Poison, 2, 0)]);
        f.Data.Unit(0).Type = new(46); using var poison = f.Begin(); f.Choose(poison, CityMission.Poison); f.Complete(poison);
        Equal((sbyte)4, f.Capture.City.Size); Equal(false, f.Data.Unit(0).Token.Occupied); Equal(CityMission.Poison, f.World.Espionage.LastMission);
        f = new([MissionDraw("V06.sabotage.building-start", CityMission.Sabotage, 39, 0)]); f.Capture.Theft.Damage.Install(BuildingIds.Temple); using var sabotage = f.Begin(); var question = (CityMissionDecision.Mission)f.Choose(sabotage, CityMission.Sabotage)!;
        Equal(CityEspionageQuestion.SpecificSabotage, ((CityEspionageDecision.Question)question.Decision).Kind); sabotage.Resume(new(-1)); f.Complete(sabotage);
        f = new([MissionDraw("V06.theft.advance-start", CityMission.Steal, 100, 0)]);
        using var theft = f.Begin(); f.Choose(theft, CityMission.Steal); f.Complete(theft); Equal(TechnologyTheftNoticeKind.Unavailable, f.Capture.Theft.Notices.Single().Kind);
        Equal((byte)3, f.Data.Unit(0).MovesUsed);
        f = new(); using var device = f.Begin(); question = (CityMissionDecision.Mission)f.Choose(device, CityMission.PlantNuclear)!;
        Equal(CityEspionageQuestion.MajorIncident, ((CityEspionageDecision.Question)question.Decision).Kind); device.Resume(new(0)); f.Complete(device); Equal(0, f.Animations);
        f = new([MissionEscape(CityMission.Incite, 0)]); using var revolt = f.Begin(); var offer = (CityMissionDecision.Revolt)f.Choose(revolt, CityMission.Incite)!;
        Equal(119, ((RevoltDecision.Offer)offer.Decision).Request.Price);
        var window = (CityMissionDecision.Revolt)revolt.Resume(new(2))!;
        Equal(true, window.Decision is RevoltDecision.Capture { Decision: CityCaptureDecision.Window });
        Equal(new CivilizationSlot(1), f.Capture.City.Owner); Equal(false, f.Data.Unit(0).Token.Occupied);
        revolt.Resume(new(0)); f.Complete(revolt); Equal(CityMission.Incite, f.World.Espionage.LastMission);
    }

    private static void CityMissionAiFallbackChainAndFailures()
    {
        var f = new CityMissionFixture([MissionDraw("V06.theft.advance-start", CityMission.Steal, 100, 0), MissionDraw("V06.sabotage.building-start", CityMission.Sabotage, 39, 0)]);
        f.Data.Unit(0).Type = new(46);
        var w = f.World; w.Globals.HumanPlayers = CivilizationSet.Only(3); w.Multiplayer.LocalHuman = 3;
        w.Globals.Difficulty = Difficulty.Emperor; w.Civilizations[1].Treaties[2].War = true; w.Civilizations[1].Gold = 0;
        using var ai = f.Begin(); Equal(null, ai.Resume()); Equal(CityMission.Incite, w.Espionage.LastMission);
        Equal(null, ai.Resume()); Equal(CityMission.Steal, w.Espionage.LastMission);
        // The next dispatch uses actual sabotage and non-Spy loss after its target scan.
        BuildingQueries.SetBuilding(w, new(0), BuildingIds.Palace, true); f.Capture.City.ShieldBox = 0;
        f.Complete(ai); Equal(CityMission.Sabotage, w.Espionage.LastMission); Equal(0, f.Reads); Equal(1, f.Stops);
        foreach (bool autoplay in new[] { false, true })
        {
            f = new([MissionDraw("V06.theft.advance-start", CityMission.Steal, 100, 0)]);
            f.World.Multiplayer.Saved.AutoplayActive = autoplay; if (!autoplay) f.World.Multiplayer.LocalHuman = 2;
            using var skipped = f.Begin(); f.Complete(skipped); Equal(0, f.Reads); Equal(CityMission.Steal, f.World.Espionage.LastMission);
        }
        f = new() { FailRead = true }; using var failed = f.Begin(); Throws<InvalidOperationException>(() => failed.Resume());
        Equal(true, f.World.Civilizations[2].Treaties[1].Contact); Equal(null, f.World.Espionage.LastMission); Throws<InvalidOperationException>(() => failed.Resume());
        f = new(); f.World.Globals.Multiplayer = GameMode.Tcp; using var remote = f.Begin(); Throws<InvalidOperationException>(() => remote.Resume());
        Equal(0, f.Stops); Equal(false, f.World.Civilizations[1].Treaties[2].Contact);
        f = new(); var disposed = f.Begin(); f.Menu(disposed); disposed.Dispose(); Throws<ObjectDisposedException>(() => disposed.Resume(new(1)));
    }

    private static void CityMissionSessionRouting()
    {
        using var runtime = new SessionRuntime(new(), new(220));
        var f = new CityMissionFixture([MissionEscape(CityMission.Incite, 0), Choice.Create(new("V06.incident-senate", "city/mission/6/incident"), new SamplingLaw.PrimaryModulo(2), ChoiceCode.Primary(0))], CreationFixture(new(runtime.Process, runtime.World)));
        f.World.Globals.HumanPlayers = CivilizationSet.Only(1).With(2);
        f.World.Civilizations[1].Treaties[2].Peace = f.World.Civilizations[2].Treaties[1].Peace = true;
        ParticipantId actorSeat = default, victimSeat = default; int windows = 0;
        runtime.RegisterCommand(new(new(1, "test.city-mission", "Authored city mission", AccessClass.Player, new([]), new([])), (_, _, _) => new(),
            _ => new CityMissionOperation(f.Begin(), c => c == 1 ? actorSeat : victimSeat,
                _ => throw new InvalidOperationException("No visit in revolt"), request =>
                { ++windows; return new("Authored P06", victimSeat, new CommandValue.City(request.City), new([0])); })));
        runtime.BeginClientSession(new()); var actor = SessionClient(runtime); var victim = SessionClient(runtime, 2);
        actorSeat = actor.Identity.Participant; victimSeat = victim.Identity.Participant;
        var command = actor.Submit(new("test.city-mission", new([]))).Correlation.Command; runtime.AdvanceOne();
        var menu = RequiredInteraction(actor, command); Equal(false, victim.Reply(menu, 6).Accepted); Equal(true, actor.Reply(menu, 6).Accepted); runtime.AdvanceOne();
        var offer = RequiredInteraction(actor, command); Equal(true, actor.Reply(offer, 1).Accepted); runtime.AdvanceOne();
        var incident = RequiredInteraction(victim, command); Equal(null, RequiredResult(actor, command).Interaction);
        Equal(CityMission.Incite, f.World.Espionage.LastMission); Equal(false, actor.Reply(incident, 0).Accepted);
        Equal(true, victim.Reply(incident, 0).Accepted); runtime.AdvanceOne();
        var window = RequiredInteraction(actor, command); Equal(false, victim.Reply(window, 0).Accepted); Equal(1, windows);
        Equal(true, actor.Reply(window, 0).Accepted); runtime.AdvanceOne(); Equal(CommandStatus.Completed, RequiredResult(actor, command).Status);
        f.Capture.Life.Random.RequireChoicesConsumed();
        foreach (bool cancel in new[] { false, true })
        {
            var closed = new CityMissionFixture(); closed.World.Espionage.LastMission = CityMission.Poison;
            var context = new OperationContext(closed.World, closed.Data.Process, closed.Capture.Life.Random, new PresentationRandom(3, "city"),
                new(default, new(1)), default, (_, _) => { }, () => { });
            using var op = new CityMissionOperation(closed.Begin(), _ => new(1), _ => throw new InvalidOperationException("No visit on cancellation"),
                _ => throw new InvalidOperationException("No capture on cancellation"));
            Equal(CommandStatus.AwaitingInteraction, op.Resume(context, new()).Status);
            Equal(CommandStatus.Completed, op.Resume(context, new(cancel ? OperationInputKind.Cancel : OperationInputKind.Timeout)).Status);
            Equal(CityMission.Poison, closed.World.Espionage.LastMission); Equal(true, closed.Data.Unit(0).Token.Occupied);
        }
        var visiting = new CityMissionFixture(); visiting.World.Civilizations[1].Treaties[2].Embassy = true;
        int opened = 0;
        var visitContext = new OperationContext(visiting.World, visiting.Data.Process, visiting.Capture.Life.Random, new PresentationRandom(4, "city"),
            new(default, new(1)), default, (_, _) => { }, () => { });
        using var visitOp = new CityMissionOperation(visiting.Begin(), _ => new(1), request =>
            { ++opened; Equal(CityEspionageVisitKind.City, request.Kind); return new("Authored investigation", new(2), new CommandValue.City(request.City), new([0])); },
            _ => throw new InvalidOperationException("No capture in investigation"));
        visitOp.Resume(visitContext, new());
        Equal(CommandStatus.Pending, visitOp.Resume(visitContext, new(OperationInputKind.Choice, (int)CityMission.Embassy)).Status);
        var visitPrompt = visitOp.Resume(visitContext, new()).Interaction!; Equal(new ParticipantId(1), visitPrompt.Recipient);
        Equal(visitPrompt, visitOp.Resume(visitContext, new()).Interaction); Equal(1, opened);
        Equal(CommandStatus.Completed, visitOp.Resume(visitContext, new(OperationInputKind.Cancel)).Status);
        Equal(true, visiting.Capture.City.Flags.Investigated); Equal((byte)1, visiting.Data.Unit(0).MovesUsed);
    }
}
