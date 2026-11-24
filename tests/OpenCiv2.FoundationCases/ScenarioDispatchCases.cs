using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;
using OpenCiv2.Formats;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // S02 §§7–10, actual U01/W03/V04/V05/V03/V01 children. No original execution claim.
    private sealed class ScenarioMedia : IScenarioMedia
    {
        public string ScenarioDirectory => "scenario";
        public string HomeDirectory => "home";
        public HashSet<string> Waves { get; } = [];
        public Queue<int> Counts { get; } = new();
        public List<string> Calls { get; } = [];
        public bool FailProbe;
        public bool WaveExists(string path) { Calls.Add("probe:" + path); if (FailProbe) { throw new IOException("Media access failed"); } return Waves.Contains(path); }
        public void PlayWave(string path) => Calls.Add("wave:" + path);
        public int CdTrackCount() { Calls.Add("count"); return Counts.Dequeue(); }
        public void CloseCd() => Calls.Add("close");
        public void PlayCdTrack(int track, bool forced) => Calls.Add("cd:" + track + ":" + forced);
    }
    private sealed class EventFixture : ICivilWarEffects
    {
        public RevelationFixture Data { get; }
        public GameState World => Data.World;
        public ContactFixture Contact { get; }
        public RetainedGrantFixture Grant => Contact.F.T.T.Retained!;
        public EliminationFixture Life => Contact.F.T.T.Damage.Lifecycle;
        public ScenarioMedia Media { get; } = new();
        public ScenarioPresentationActions Presentation { get; }
        public ScenarioPermissionActions Permissions { get; }
        public ScenarioDispatcher Dispatcher { get; private set; } = null!;
        public ScenarioTriggers Triggers { get; private set; } = null!;
        public CivilWar CivilWar { get; }
        public List<OwnedList<string>> Texts { get; } = [];
        public int Suppressed, Created, MapRedraws;
        public Func<int, CivilizationCreation>? Recreate;
        public bool FailMap;
        public EventFixture(RevelationFixture? data = null, List<Choice>? choices = null,
            Func<OwnedList<string>, OwnedList<string>>? prepareText = null)
        {
            Data = data ?? new();
            Permissions = new(World, () => Dispatcher!);
            Contact = new(choices, Data, Permissions);
            Data.Rules.Units[10] = Data.Rules.Units[0] with { Role = RuleRole.Attack, Domain = RuleDomain.Land };
            Presentation = new(World, Media, lines => { var prepared = prepareText?.Invoke(lines) ?? lines; Texts.Add(prepared); return prepared; }, () => ++Suppressed);
            RebindDispatcher();
            CivilWar = new(World, Data.Stacks, Create, Permissions, this);
        }
        private CivilizationCreation Create(int owner) { ++Created; return (Recreate ?? throw new InvalidOperationException("Unexpected civilization reset"))(owner); }
        public void RebindDispatcher(DiplomaticContact? contact = null, ResearchGrants? grants = null, DiplomaticWar? war = null)
        {
            var incidents = Contact.F.T.T.Damage.Incidents.Incidents;
            var removal = new CivilizationRemoval(World, Data.Rules, Data.Stacks, Life.Elimination, incidents, Create, RedrawMaps);
            var terrain = new ScenarioTerrain(World, Data.Stacks, new(World, Data.Rules, Data.Stacks, Life.Loss, _ => { }),
                Life.Elimination, incidents, Create, new ScenarioTerrainEncoding(World.Map, new(World.Map)), _ => { }, (_, _) => RedrawMaps());
            var creation = contact is null ? RetainedCreation(Contact)
                : new UnitCreation(World, Data.Process, Data.Stacks,
                    new UnitRevelation(World, Data.Rules, Data.Stacks, contact, Data), Data);
            Dispatcher = new(World, new(World, Data.Rules, creation), terrain,
                new(World, war ?? War(Data, new()), grants ?? Contact.F.T.T.Research), new(World, removal), Presentation);
            Triggers = new(World, Contact.F.T.T.Random, () => Dispatcher);
        }
        public void Schism(CivilWarNotice _) => throw new InvalidOperationException("Vetoed schism must not partition");
        public void RedrawMaps() { ++MapRedraws; if (FailMap) { throw new IOException("Map view unavailable"); } }
        public Event Text(string text = "Authored event")
        {
            var entry = World.Events.Collection.Append(); entry.Values.Actions.Text = true;
            entry.SetString(Event.TextBegin, new(text)); return entry;
        }
        public Event Library()
        {
            ScenarioLibrary(Grant); var entry = TechnologyEvent(Grant); entry.Values.Actions.GiveTechnology = true;
            return entry;
        }
        public void Consumed() => Contact.F.T.T.Random.RequireChoicesConsumed();
        public OperationContext Context() => new(World, Data.Process, Contact.F.T.T.Random, new PresentationRandom(1, "scenario"), new(default, new(2)), default, (_, _) => { }, () => { });
    }
    private static void ScenarioMediaWaveSearch()
    {
        foreach (int found in new[] { 0, 1, 2 })
        {
            var f = new EventFixture(); var entry = f.Text(); entry.SetString(34, new("AUTHORED.WAV"));
            f.World.Globals.GameOptions.Sound = true; f.World.Globals.GameOptions.Music = false;
            if (found != 0) { f.Media.Waves.Add((found == 1 ? "scenario" : "home") + "\\SOUND\\AUTHORED.WAV"); }
            Equal(1, f.Presentation.Wave(entry));
            Sequence(found == 1 ? ["probe:scenario\\SOUND\\AUTHORED.WAV", "wave:scenario\\SOUND\\AUTHORED.WAV"]
                : found == 2 ? new[] { "probe:scenario\\SOUND\\AUTHORED.WAV", "probe:home\\SOUND\\AUTHORED.WAV", "wave:home\\SOUND\\AUTHORED.WAV" }
                : ["probe:scenario\\SOUND\\AUTHORED.WAV", "probe:home\\SOUND\\AUTHORED.WAV"], f.Media.Calls);
            f.Media.Calls.Clear(); f.World.Globals.GameOptions.Sound = false;
            Equal(0, f.Presentation.Wave(entry)); Equal(0, f.Media.Calls.Count);
            f.World.Globals.GameOptions.Sound = true; entry.SetString(34, default);
            Equal(1, f.Presentation.Wave(entry)); Equal(0, f.Media.Calls.Count); f.Consumed();
        }
    }
    private static void ScenarioMediaCdRetries()
    {
        foreach (int accepted in new[] { 10, 12, 18, 24 })
        {
            var f = new EventFixture(); var e = f.Text(); e.Values.CdTrack = accepted;
            f.World.Globals.GameOptions.Music = false; f.Media.Counts.Enqueue(9); f.Media.Counts.Enqueue(accepted);
            Equal(1, f.Presentation.Cd(e)); Sequence(new[] { "count", "close", "count", "cd:" + accepted + ":True" }, f.Media.Calls);
            f.Media.Calls.Clear(); f.Media.Counts.Enqueue(accepted); e.Values.CdTrack = 1;
            Equal(0, f.Presentation.Cd(e)); Sequence(new[] { "count" }, f.Media.Calls); f.Consumed();
        }
        var failed = new EventFixture(); var entry = failed.Text(); entry.Values.CdTrack = 2;
        for (int i = 0; i < 10; ++i) { failed.Media.Counts.Enqueue(0); }
        Equal(0, failed.Presentation.Cd(entry)); Equal(10, failed.Media.Calls.Count(x => x == "close")); Equal(0, failed.Media.Counts.Count);
    }
    private static void ScenarioTextHolesAndAutoplay()
    {
        var f = new EventFixture(); var e = f.Text("First"); e.SetString(6, new("")); e.SetString(23, new("Last"));
        var text = f.Presentation.Text(e)!; Sequence(new[] { "First", "", "Last" }, text.Lines); Equal(1, text.Recipient);
        e.Strings[4].Value = "Changed later"; Equal("First", text.Lines[0]);
        f.World.Multiplayer.Saved.AutoplayActive = true; Equal(null, f.Presentation.Text(e)); Equal(2, f.Texts.Count);
        f.World.Globals.Multiplayer = GameMode.Tcp; Throws<InvalidOperationException>(() => f.Presentation.Text(e)); Equal(2, f.Texts.Count);
    }
    private static void ScenarioDispatchAllActions()
    {
        var f = new EventFixture(); var w = f.World; var d = f.Data;
        foreach (ref var u in w.Entities.Units.Storage) { u.Token = default; }
        w.Entities.Units.SetEnd(0);
        foreach (ref var c in w.Entities.Cities.Storage) { c.Token = default; }
        w.Entities.Cities.SetEnd(0);
        foreach (ref var tile in w.Map.Tiles) { tile.Features = default; tile.Owner = 0; }
        for (int owner = 0; owner < 8; ++owner) { d.AddCity(owner, owner, new(32, 2 + 2 * owner)); w.Civilizations[owner].CityCount = 1; }
        w.Globals.Turn = 0; w.Globals.CivsAlive = CivilizationSet.Only(1).With(2); w.Globals.HumanPlayers = CivilizationSet.Only(1);
        w.Globals.GameOptions.Sound = true; f.Media.Waves.Add("scenario\\SOUND\\event.wav"); f.Media.Counts.Enqueue(12);
        var e = CreateUnitEvent(d, 2, 10, new(20, 12)); e.SetString(34, new("event.wav")); e.SetString(4, new("After gameplay"));
        e.Values.Actions = new()
        {
            PlayWaveFile = true,
            PlayCdTrack = true,
            CreateUnit = true,
            MoveUnit = true,
            ChangeTerrain = true,
            MakeAggression = true,
            ChangeMoney = true,
            DestroyCivilization = true,
            GiveTechnology = true,
            Text = true,
            SuppressWonderMovies = true,
            JustOnce = true
        };
        e.Values.CdTrack = 2; e.Values.MoveOwnerCiv = 2; e.Values.MoveUnitType = -2; e.Values.MoveCount = -2;
        e.Values.MoveRectangle[0] = new(0, 0); e.Values.MoveRectangle[1] = new(39, 0);
        e.Values.MoveRectangle[2] = new(39, 23); e.Values.MoveRectangle[3] = new(0, 23); e.Values.MoveTo = new(18, 10);
        e.Values.TerrainRectangle[0] = e.Values.TerrainRectangle[1] = e.Values.TerrainRectangle[3] = new(4, 2); e.Values.TerrainType = (int)Terrain.Plains;
        e.Values.AggressionWhoCiv = 1; e.Values.AggressionWhomCiv = 2; e.Values.MoneyReceiverCiv = 2; e.Values.MoneyAmount = 10;
        e.Values.DestroyCiv = 1; e.Values.GiveTechCiv = 2; e.Values.GiveTech = 54;
        int gold = w.Civilizations[2].Gold;
        using var op = f.Dispatcher.Begin(e, "all"); Equal(true, op.Resume() is ScenarioText);
        int unit = d.Stacks.FirstAt(new(20, 12)); Equal(true, unit >= 0); Equal(UnitOrder.GoTo, d.Unit(unit).Orders); Equal(new MapPoint(18, 10), d.Unit(unit).Destination);
        Equal(Terrain.Plains, w.Map.TileAt(new(4, 2)).Terrain); Equal(true, w.Civilizations[1].Treaties[2].War);
        Equal(gold + 10, w.Civilizations[2].Gold); Equal(false, w.Globals.CivsAlive.Contains(1)); Equal(true, w.Civilizations[2].Advances.Contains(54));
        Equal(false, e.Values.Actions.Consumed); Equal(0, f.Suppressed); Equal(null, op.Result);
        Equal(null, op.Resume(new(0))); Equal(1, op.Result); Equal(true, e.Values.Actions.Consumed); Equal(1, f.Suppressed); Equal(0, f.Created);
        Sequence(new[] { "probe:scenario\\SOUND\\event.wav", "wave:scenario\\SOUND\\event.wav", "count", "cd:2:True" }, f.Media.Calls); f.Consumed();
    }
    private static void ScenarioDispatchLiveFlagsAndFailure()
    {
        var f = new EventFixture(); var e = f.Library(); e.Values.Actions.Text = e.Values.Actions.SuppressWonderMovies = e.Values.Actions.JustOnce = true;
        using var op = f.Dispatcher.Begin(e, "live"); Equal(true, op.Resume() is ResearchGrantDecision.Government);
        Equal(false, e.Values.Actions.Consumed); e.Values.Actions.Text = e.Values.Actions.SuppressWonderMovies = e.Values.Actions.JustOnce = false;
        Equal(null, op.Resume(new(0))); Equal(1, op.Result); Equal(0, f.Texts.Count); Equal(0, f.Suppressed); Equal(false, e.Values.Actions.Consumed); f.Consumed();
        f = new(); e = f.Library(); e.Values.Actions.JustOnce = true;
        using var held = f.Dispatcher.Begin(e, "held"); held.Resume(); held.Dispose(); Throws<ObjectDisposedException>(() => held.Resume(new(1))); Equal(false, e.Values.Actions.Consumed);
        f = new(); e = f.Library(); e.Values.Actions.JustOnce = true; e.Values.Actions.ChangeMoney = true; e.Values.MoneyReceiverCiv = 1; e.Values.MoneyAmount = 9;
        f.Grant.FailRevolution = true; int gold = f.World.Civilizations[1].Gold;
        using var failed = f.Dispatcher.Begin(e, "failed"); failed.Resume(); Throws<InvalidOperationException>(() => failed.Resume(new(1)));
        Equal(gold + 9, f.World.Civilizations[1].Gold); Equal(false, e.Values.Actions.Consumed); Throws<InvalidOperationException>(() => failed.Resume());
        f = new(); e = f.Text(); e.Values.Actions = new() { PlayWaveFile = true, ChangeMoney = true, JustOnce = true };
        e.SetString(34, new("unreadable.wav")); e.Values.MoneyReceiverCiv = 1; e.Values.MoneyAmount = 20;
        f.World.Globals.GameOptions.Sound = true; f.Media.FailProbe = true; gold = f.World.Civilizations[1].Gold;
        using var mediaFailure = f.Dispatcher.Begin(e, "media-failure"); Throws<IOException>(() => mediaFailure.Resume());
        Equal(gold, f.World.Civilizations[1].Gold); Equal(false, e.Values.Actions.Consumed); Throws<InvalidOperationException>(() => mediaFailure.Resume());
    }
    private static void ScenarioDispatchRetainedLifecycle()
    {
        foreach (bool terrain in new[] { false, true })
        {
            var f = new EventFixture(); var grants = new RestartGrantFixture(f.Data);
            f.Recreate = owner => { Equal(2, owner); return grants.Create(); };
            var e = f.World.Events.Collection.Append(); e.Values.Actions.JustOnce = true;
            if (terrain)
            {
                foreach (ref var city in f.World.Entities.Cities.Storage[1..]) { city.Token = default; }
                f.World.Entities.Cities.SetEnd(1);
                int slot = 1;
                foreach (int owner in new[] { 0, 3, 4, 5, 6, 7 })
                { f.Data.AddCity(slot++, owner, new(32, 2 * owner)); }
                f.World.Civilizations[2].CityCount = 0;
                e.Values.Actions.ChangeTerrain = true; e.Values.TerrainRectangle[0] = new(2, 0);
                e.Values.TerrainRectangle[1] = e.Values.TerrainRectangle[3] = new(0, 0);
                e.Values.Actions.ChangeMoney = true; e.Values.MoneyReceiverCiv = 1; e.Values.MoneyAmount = 13;
            }
            else { e.Values.Actions.DestroyCivilization = true; e.Values.DestroyCiv = 2; }
            f.FailMap = true; int gold = f.World.Civilizations[1].Gold;
            using var adapter = new ScenarioEventOperation(f.Dispatcher.Begin(e, "lifecycle"), c => new((ulong)c));
            Equal(CommandStatus.AwaitingInteraction, adapter.Resume(f.Context(), new()).Status);
            Equal(false, e.Values.Actions.Consumed); Equal(gold, f.World.Civilizations[1].Gold); Equal(0, f.MapRedraws);
            var result = adapter.Resume(f.Context(), new(OperationInputKind.Cancel));
            Equal(CommandStatus.Failed, result.Status); Equal("scenario_event_presentation_failed", result.Code);
            Equal(true, e.Values.Actions.Consumed); Equal(gold + (terrain ? 13 : 0), f.World.Civilizations[1].Gold);
            Equal(1, f.Created); Equal(1, f.MapRedraws); Equal(result, adapter.Resume(f.Context(), new()));
            grants.Random.RequireChoicesConsumed(); f.Consumed();
        }
    }
    private static void ScenarioDispatchActualReentry()
    {
        var f = new EventFixture(); var e = TechnologyEvent(f.Grant); e.Values.Actions = new() { GiveTechnology = true, ChangeMoney = true, JustOnce = true };
        e.Values.MoneyReceiverCiv = 2; e.Values.MoneyAmount = 10; f.World.Globals.RevealMap = 1;
        int gold = f.World.Civilizations[2].Gold, visits = 0;
        f.Grant.G.OnAcquired = _ =>
        {
            ++visits; Equal(false, e.Values.Actions.Consumed);
            if (visits == 1)
            { using var nested = f.Dispatcher.Begin(e, "nested"); Equal(null, nested.Resume()); Equal(1, nested.Result); }
        };
        using var outer = f.Dispatcher.Begin(e, "outer"); Equal(null, outer.Resume()); Equal(1, outer.Result);
        Equal(2, visits); Equal(gold + 20, f.World.Civilizations[2].Gold); Equal((byte)2, f.World.Civilizations[2].NumAdvances); Equal(true, e.Values.Actions.Consumed);
        using var consumed = f.Dispatcher.Begin(e, "consumed"); Equal(null, consumed.Resume()); Equal(0, consumed.Result); Equal(2, visits); f.Consumed();
    }
    private static void ScenarioPermissionRetainedSelection()
    {
        var f = new EventFixture(); var e = f.Text("First permission");
        e.Values.Trigger = EventTrigger.Negotiation; e.Values.WhoCiv = -2; e.Values.TalkerType = 2; e.Values.WhomCiv = 2; e.Values.ListenerType = 4;
        e.Values.DenyNegotiation = true; e.Values.Actions.JustOnce = true;
        var later = f.Text("Must not execute"); later.Values = e.Values;
        using var op = f.Permissions.BeginNegotiation(1, 2, "permission"); Equal(true, op.Resume() is ScenarioText);
        Equal(null, op.Allowed); e.Values.DenyNegotiation = false; Equal(null, op.Resume(new(0))); Equal(false, op.Allowed); Equal(1, f.Texts.Count);
        e.Values.DenyNegotiation = true;
        using var repeated = f.Permissions.BeginNegotiation(1, 2, "again"); Equal(null, repeated.Resume()); Equal(false, repeated.Allowed); Equal(1, f.Texts.Count);
        e.Values.TalkerType = 1; later.Values.Trigger = EventTrigger.None;
        using var noMatch = f.Permissions.BeginNegotiation(1, 2, "classification"); Equal(null, noMatch.Resume()); Equal(true, noMatch.Allowed);
    }
    private static void ScenarioSchismRetainedEvents()
    {
        var f = new EventFixture(); f.World.Events.Loaded = false;
        Event first = f.Text("First veto"), second = f.Text("Second veto");
        foreach (var e in new[] { first, second }) { e.Values.Trigger = EventTrigger.NoSchism; e.Values.WhomCiv = -2; e.Values.Actions.JustOnce = true; }
        using var op = f.CivilWar.Begin(2); Equal(null, op.AdvanceDecision()); Equal(true, op.Pending is ScenarioText);
        Equal(0, f.Created); Equal(false, first.Values.Actions.Consumed); Equal(null, op.AdvanceDecision(answer: new(0)));
        Equal(true, first.Values.Actions.Consumed); Equal(false, second.Values.Actions.Consumed); Equal("Second veto", ((ScenarioText)op.Pending!).Lines[0]);
        Equal(0, op.AdvanceDecision(answer: new(0))); Equal(true, second.Values.Actions.Consumed); Equal(0, f.Created); Equal(0, f.MapRedraws);
        using var again = f.CivilWar.Begin(2); Equal(0, again.AdvanceDecision()); Equal(2, f.Texts.Count);
        var broken = new EventFixture(); var removed = broken.Text(); removed.Values.Trigger = EventTrigger.NoSchism; removed.Values.WhomCiv = 2;
        using var invalid = broken.Permissions.BeginSchism(2, "removed"); invalid.Resume(); broken.World.Events.Collection.Reset();
        Throws<InvalidOperationException>(() => invalid.Resume(new(0))); Throws<InvalidOperationException>(() => invalid.Resume());
    }
    private static void ScenarioSchismCallIdentity()
    {
        var choices = new List<Choice>();
        foreach (string call in new[] { "first", "second" })
        {
            choices.Add(Choice.Create(new("V03.emissary", call + "/permission/event/1/technology/grant"),
                new SamplingLaw.SecondaryRange(0, 2), ChoiceCode.Secondary(0)));
        }
        var f = new EventFixture(choices: choices); var e = f.World.Events.Collection.Append(); Equal(1UL, e.Identity);
        e.Values.Trigger = EventTrigger.NoSchism; e.Values.WhomCiv = 2;
        e.Values.Actions.GiveTechnology = true; e.Values.GiveTechCiv = 2;
        f.World.Globals.RevealMap = 0;
        f.World.Diplomacy.EmissaryResponse = 0;
        f.World.Civilizations[1].Treaties[2] = f.World.Civilizations[2].Treaties[1] = new() { Contact = true };
        foreach (var (call, advance) in new[] { ("first", 54), ("second", 55) })
        {
            e.Values.GiveTech = advance;
            using var op = f.CivilWar.Begin(2, call); Equal(null, op.AdvanceDecision());
            Equal(true, op.Pending is ResearchGrantDecision.Emissary { Decision: EmissaryDecision.Receive });
            Equal(0, op.AdvanceDecision(answer: new(1))); Equal(false, e.Values.Actions.Consumed);
        }
        Equal(0, f.Created); f.Consumed();
    }
    private static void ScenarioAudienceRetainedEntry()
    {
        var f = new EventFixture(); var e = f.Library(); e.Values.Trigger = EventTrigger.Negotiation;
        e.Values.WhoCiv = 1; e.Values.WhomCiv = 2; e.Values.TalkerType = e.Values.ListenerType = 4;
        e.Values.DenyNegotiation = true; e.Values.Actions.JustOnce = true; f.World.Events.Loaded = true;
        f.World.Diplomacy.SelectedPair = (6, 7); f.World.Diplomacy.GiftsThisMeeting = 9;
        using var op = f.Contact.F.Service.Begin(1, 2, true, new(10, 8), "audience");
        Equal(true, op.Resume() is DiplomaticAudienceDecision.Scenario { Value: ResearchGrantDecision.Government });
        Equal(true, f.World.Diplomacy.AiMeetingActive); Equal((6, 7), f.World.Diplomacy.SelectedPair); Equal(9, f.World.Diplomacy.GiftsThisMeeting);
        Equal(false, op.Completed); Equal(null, op.Resume(new(0))); Equal(true, op.Completed && op.ScenarioDenied);
        Equal(false, f.World.Diplomacy.AiMeetingActive); Equal(0, f.Contact.F.C.Closed); Equal(true, e.Values.Actions.Consumed); f.Consumed();
        var session = new EventFixture(); var decisionEvent = session.Library();
        decisionEvent.Values.Trigger = EventTrigger.Negotiation; decisionEvent.Values.WhoCiv = 1; decisionEvent.Values.WhomCiv = 2;
        decisionEvent.Values.TalkerType = decisionEvent.Values.ListenerType = 4; decisionEvent.Values.DenyNegotiation = true;
        decisionEvent.Values.Actions.JustOnce = true; session.World.Events.Loaded = true;
        using var adapter = new DiplomaticAudienceOperation(session.Contact.F.Service.Begin(1, 2, true, new(10, 8), "audience"),
            c => new((ulong)c), _ => throw new InvalidOperationException("No intelligence in a scenario Library choice"));
        Equal(CommandStatus.AwaitingInteraction, adapter.Resume(session.Context(), new()).Status);
        Equal(CommandStatus.Completed, adapter.Resume(session.Context(), new(OperationInputKind.Timeout)).Status);
        Equal(true, decisionEvent.Values.Actions.Consumed); Equal(false, session.World.Diplomacy.AiMeetingActive);
        Equal(Government.Despotism, session.World.Civilizations[1].Government); session.Consumed();
    }
    private static Event ScenarioNestedCreation(EventFixture f)
    {
        ScenarioLibrary(f.Grant); f.World.Events.Loaded = true;
        var permission = NegotiationEvent(f.World, 1, 2, deny: true);
        permission.Values.Actions = new() { GiveTechnology = true, JustOnce = true };
        permission.Values.GiveTechCiv = 2; permission.Values.GiveTech = 54;
        f.Data.AddUnit(10, 2, 10, new(22, 12)); f.World.Civilizations[1].Treaties[2].Contact = false;
        var e = CreateUnitEvent(f.Data, 1, 10, new(20, 12)); e.Values.Actions = new() { CreateUnit = true, ChangeMoney = true, Text = true, JustOnce = true };
        e.Values.MoneyReceiverCiv = 1; e.Values.MoneyAmount = 7; e.SetString(4, new("Creation completed")); return e;
    }
    private static void ScenarioCreationNestedPermission()
    {
        var f = new EventFixture(); var e = ScenarioNestedCreation(f); int gold = f.World.Civilizations[1].Gold;
        using var op = f.Dispatcher.Begin(e, "scenario");
        Equal(true, op.Resume() is LifecycleContact { Decision: DiplomaticContactDecision.Scenario { Value: ResearchGrantDecision.Government } });
        int unit = f.Data.Stacks.FirstAt(new(20, 12)); Equal(true, unit >= 0); var token = f.Data.Unit(unit).Token;
        Equal(gold, f.World.Civilizations[1].Gold); Equal(false, e.Values.Actions.Consumed); Equal(op.Pending, op.Resume());
        Equal(true, op.Resume(new(0)) is ScenarioText); Equal(token, f.Data.Unit(unit).Token); Equal(gold + 7, f.World.Civilizations[1].Gold);
        Equal(false, f.World.Civilizations[1].Treaties[2].Contact); Equal(false, e.Values.Actions.Consumed);
        Equal(null, op.Resume(new(0))); Equal(1, op.Result); Equal(true, e.Values.Actions.Consumed); f.Consumed();
    }
    private static void ScenarioEventPrivateSession()
    {
        using var runtime = new SessionRuntime(new(), new(255)); var f = new EventFixture(new(runtime.Process, runtime.World));
        var e = ScenarioNestedCreation(f); ParticipantId recipient = default;
        runtime.RegisterCommand(new(new(1, "test.scenario-event", "Scenario event", AccessClass.Player, new([]), new([new("result", ValueKind.Integer)])),
            (_, _, _) => new(), _ => new ScenarioEventOperation(f.Dispatcher.Begin(e, "scenario"), _ => recipient)));
        runtime.BeginClientSession(new()); var actor = SessionClient(runtime, 2); var human = SessionClient(runtime, 1); recipient = human.Identity.Participant;
        var command = actor.Submit(new("test.scenario-event", new([]))).Correlation.Command; runtime.AdvanceOne();
        var grant = RequiredInteraction(human, command); Equal(false, actor.Reply(grant, 0).Accepted); Equal(null, RequiredResult(actor, command).Interaction);
        Equal(true, human.Reply(grant, 0).Accepted); runtime.AdvanceOne(); var text = RequiredInteraction(human, command);
        Equal(false, human.Reply(grant, 0).Accepted); Equal(false, e.Values.Actions.Consumed); Equal(true, human.Reply(text, 0).Accepted); runtime.AdvanceOne();
        Equal(CommandStatus.Completed, RequiredResult(actor, command).Status); Equal(true, e.Values.Actions.Consumed); Equal(null, human.Result(command)); f.Consumed();
        foreach (var input in new[] { OperationInputKind.Cancel, OperationInputKind.Timeout })
        {
            var cancel = new EventFixture(); var entry = cancel.Text(); entry.Values.Actions.JustOnce = true; entry.Values.Actions.SuppressWonderMovies = true;
            using var adapter = new ScenarioEventOperation(cancel.Dispatcher.Begin(entry, "cancel"), c => new((ulong)c));
            Equal(CommandStatus.AwaitingInteraction, adapter.Resume(cancel.Context(), new()).Status);
            Equal(CommandStatus.Completed, adapter.Resume(cancel.Context(), new(input)).Status); Equal(true, entry.Values.Actions.Consumed); Equal(1, cancel.Suppressed);
        }
    }
}
