using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private sealed class IncidentFixture
    {
        public RevelationFixture Data { get; }
        public GameState World => Data.World;
        public GameplayRandom Random { get; }
        public EspionageIncidents Incidents { get; }
        public List<EspionageIncidentNotice> Notices { get; } = [];
        public WarNotices WarNotices { get; } = new();
        public bool FailScandal;
        public IncidentFixture(GameplayRandom? random = null, RevelationFixture? data = null)
        {
            Data = data ?? FoodFixture(); Random = random ?? WarRandom();
            var changes = new GovernmentChanges(World, Data.Process.Rules, Data.Stacks,
                _ => throw new InvalidOperationException("Unexpected government choice"),
                _ => throw new InvalidOperationException("Unexpected rate choice"), _ => { });
            var stance = new DiplomaticStance(World, Data.Process.Rules,
                new GovernmentPolicy(World, Data.Process.Rules, changes, Random));
            Incidents = new(World, Random, War(Data, WarNotices, Random), stance, changes, n =>
            {
                Notices.Add(n);
                if (FailScandal && n.Kind == EspionageIncidentKind.SenateScandal)
                    throw new InvalidOperationException("Scandal output unavailable");
            });
            World.Globals.Turn = 100;
            World.Names[1] = new() { Tribe = "Alderfolk", Adjective = "Alder" };
            World.Names[2] = new() { Tribe = "Birchfolk", Adjective = "Birch" };
        }
        public void Treaty(bool allied = false) => DiplomaticRelations.Set(World, 1, 2, new() { Peace = true, Alliance = allied });
        public void Run() { Equal(null, Incidents.Begin(1, 2, "incident").Resume()); Random.RequireChoicesConsumed(); }
    }

    private static Choice IncidentChoice(int value, bool leniency = false, string call = "incident") =>
        Choice.Create(new(leniency ? "V06.incident-leniency" : "V06.incident-senate", call),
            new SamplingLaw.PrimaryModulo(leniency ? 15 : 2), ChoiceCode.Primary(value));

    // V06 §11: reverse admission, independent forward response and early-return draw boundaries.
    private static void IncidentDirectedAdmissionAndGuards()
    {
        var f = new IncidentFixture(); var w = f.World;
        w.Civilizations[2].Attitude[1] = 13; w.Civilizations[1].Treaties[2].Alliance = true;
        f.Run(); Equal((byte)33, w.Civilizations[2].Attitude[1]); Equal(0, f.Notices.Count);
        w.Civilizations[2].Treaties[1] = new() { War = true, Peace = true }; f.Run();
        Equal((byte)33, w.Civilizations[2].Attitude[1]);
        w.Civilizations[2].Treaties[1] = new() { Peace = true }; w.Globals.HumanPlayers = default;
        f.Run(); Equal(false, w.Civilizations[2].Treaties[1].War); Equal(0, f.Notices.Count);
        foreach (bool autoplay in new[] { false, true })
        {
            f = new(); w = f.World; f.Treaty(); w.Globals.HumanPlayers = CivilizationSet.Only(1).With(2);
            w.Civilizations[1].Government = autoplay ? Government.Democracy : Government.Fundamentalism;
            w.Multiplayer.Saved.AutoplayActive = autoplay; f.Run();
            Equal(false, w.Civilizations[2].Treaties[1].War); Equal(0, f.Notices.Count);
        }
        f = new(WarRandom(IncidentChoice(1))); w = f.World;
        w.Civilizations[2].Treaties[1].Alliance = true; // Reverse alliance admits, but forward absence chooses Terror.
        w.Civilizations[1].Government = Government.Fundamentalism; w.Civilizations[2].Attitude[1] = 30;
        f.Run(); Equal(EspionageIncidentKind.Terror, f.Notices.Single().Kind);
        Equal((byte)40, w.Civilizations[2].Attitude[1]); Equal(true, w.Civilizations[2].Treaties[1].Vendetta);
        Equal(false, w.Civilizations[1].Treaties[2].War); Equal(Government.Fundamentalism, w.Civilizations[1].Government);
    }

    private static void IncidentHumanPretextAndNoExpulsion()
    {
        foreach (bool allied in new[] { false, true })
            foreach (int reply in new[] { 0, 1, 2 })
            {
                var f = new IncidentFixture(); var w = f.World; f.Treaty(allied);
                w.Globals.HumanPlayers = CivilizationSet.Only(2);
                f.Data.AddCity(1, 2, new(8, 4)); f.Data.AddUnit(0, 1, 0, new(8, 4));
                w.Civilizations[2].ContinentCities[w.Map.TileAt(new(8, 4)).Region] = 1;
                var op = f.Incidents.Begin(1, 2, "incident"); var prompt = op.Resume()!;
                Equal(allied, prompt.Allied); Equal("Alderfolk", prompt.Actor.Plural); Equal(false, op.Completed);
                Equal(prompt, op.Resume()); Equal((short)0, w.Civilizations[2].LastContactTurn[1]);
                Equal(null, op.Resume(reply)); Equal(true, op.Completed); op.Resume(1);
                Equal(allied && reply != 1, w.Civilizations[1].Treaties[2].Alliance);
                Equal(!allied && reply == 1, w.Civilizations[2].Treaties[1].War);
                Equal((short)(reply == 1 ? 100 : 0), w.Civilizations[2].LastContactTurn[1]);
                Equal(new MapPoint(8, 4), f.Data.Unit(0).Position); Equal(0, f.WarNotices.Notices.Count);
                Equal((byte)0, w.Civilizations[1].Reputation); f.Random.RequireChoicesConsumed();
            }
    }

    private static void IncidentWarAndActualSenateTransition()
    {
        foreach (var (government, roll, scandal) in new[]
        {
            (Government.Democracy, -1, true), (Government.Republic, 0, false),
            (Government.Republic, 1, true), (Government.Despotism, 1, false)
        })
        {
            var f = new IncidentFixture(roll < 0 ? WarRandom() : WarRandom(IncidentChoice(roll)));
            var w = f.World; f.Treaty(); w.Civilizations[1].Government = government;
            w.Entities.Cities.Storage[0].Production = UnitTypeIds.Fanatics;
            f.Run(); Equal(true, w.Civilizations[1].Treaties[2].War);
            Equal(scandal ? Government.Anarchy : government, w.Civilizations[1].Government);
            Equal(!scandal, w.Civilizations[2].Treaties[1].Vendetta);
            Equal(EspionageIncidentKind.War, f.Notices[0].Kind);
            Equal(scandal ? 2 : 1, f.Notices.Count);
            if (scandal)
            {
                Equal(EspionageIncidentKind.SenateScandal, f.Notices[1].Kind);
                Equal((sbyte)UnitTypeIds.Riflemen, w.Entities.Cities.Storage[0].Production);
                Equal(false, w.Globals.TransientFlags.AttitudeDirty);
            }
        }
        // A human victim's declined response still reaches the human actor's senate.
        var both = new IncidentFixture(); both.Treaty(); both.World.Globals.HumanPlayers = CivilizationSet.Only(1).With(2);
        both.World.Civilizations[1].Government = Government.Democracy;
        var op = both.Incidents.Begin(1, 2, "incident"); op.Resume(); op.Resume(0);
        Equal(Government.Anarchy, both.World.Civilizations[1].Government); Equal(false, both.World.Civilizations[1].Treaties[2].War);
        both.Random.RequireChoicesConsumed();
    }

    private static void IncidentScenarioAndAutoplayOrdering()
    {
        foreach (bool scenario in new[] { false, true })
            foreach (bool autoplay in new[] { false, true })
            {
                var f = new IncidentFixture(WarRandom(IncidentChoice(1))); var w = f.World; f.Treaty();
                w.Civilizations[1].Government = Government.Republic;
                w.Globals.Status.Scenario = scenario; w.Globals.Status.ScenarioExport = !scenario;
                w.Scenario.Flags.RevolutionRestriction = true; w.Multiplayer.Saved.AutoplayActive = autoplay;
                f.Run(); Equal(scenario ? Government.Republic : Government.Anarchy, w.Civilizations[1].Government);
                Equal(true, w.Civilizations[1].Treaties[2].War);
                Equal(autoplay ? 0 : scenario ? 1 : 2, f.Notices.Count);
            }
    }

    private static void IncidentLeniencyLiveWorkspaceAndAlliance()
    {
        foreach (bool autoplay in new[] { false, true })
        {
            var f = new IncidentFixture(autoplay ? WarRandom() : WarRandom(IncidentChoice(14, true)));
            var w = f.World; f.Treaty(true); w.Civilizations[1].Government = Government.Democracy;
            w.Civilizations[1].NumAdvances = 2; w.Civilizations[2].NumAdvances = 3;
            w.Civilizations[2].Attitude[1] = 50; w.Civilizations[1].Gold = 10000;
            w.Civilizations[1].MilitaryStrength = w.Civilizations[2].MilitaryStrength = 20;
            w.Globals.PowerRank[1] = 3; w.Globals.PowerRank[2] = 4;
            w.Diplomacy.SelectedPair = (2, 1); w.Diplomacy.Attitude = -123;
            Equal(null, w.Espionage.LastMission);
            var op = f.Incidents.Begin(1, 2, "incident"); w.Espionage.LastMission = CityMission.Steal;
            w.Multiplayer.Saved.AutoplayActive = autoplay; Equal(null, op.Resume()); f.Random.RequireChoicesConsumed();
            Equal(Government.Democracy, w.Civilizations[1].Government); Equal(true, w.Civilizations[1].Treaties[2].Alliance);
            Equal((byte)(autoplay ? 50 : 69), w.Civilizations[2].Attitude[1]);
            if (!autoplay)
            {
                Equal(EspionageIncidentKind.Leniency, f.Notices.Single().Kind);
                Equal(2, f.Notices.Single().AttitudeCategory); // Stance 50 - 25 alliance, formatted before the +19 adjustment.
                Equal(44, w.Diplomacy.Attitude);
            }
            Equal(null, new GameState(f.Data.Process).Espionage.LastMission);
        }
        var other = new IncidentFixture(WarRandom(IncidentChoice(0))); var world = other.World; other.Treaty(true);
        world.Espionage.LastMission = (CityMission)99; world.Civilizations[1].Government = Government.Despotism;
        other.Data.AddCity(1, 2, new(8, 4)); other.Data.AddUnit(0, 1, 0, new(8, 4));
        world.Civilizations[1].CityCount = 1;
        world.Civilizations[2].ContinentCities[world.Map.TileAt(new(8, 4)).Region] = 1;
        other.Run(); Equal(false, world.Civilizations[1].Treaties[2].Alliance);
        Equal(false, world.Civilizations[1].Treaties[2].War);
        Equal(world.Entities.Cities.Storage[0].Position, other.Data.Unit(0).Position);
        Equal(EspionageIncidentKind.Allied, other.Notices.Single().Kind);
    }

    // V06 §§2,11: actual loss and explicit pending reply; cancel/timeout decline without replaying the prefix.
    private static void EscapeIncidentContinuationAndLossPrefix()
    {
        foreach (var input in new[] { new OperationInput(OperationInputKind.Choice, 1), new(OperationInputKind.Cancel), new(OperationInputKind.Timeout) })
            foreach (int type in new[] { 4, 47 })
            {
                var loss = new EliminationFixture(type == 47 ? [EscapeChoice("escape", 2, 0)] : []);
                var f = new IncidentFixture(loss.Random, loss.Data); var w = f.World;
                f.Treaty(); w.Globals.HumanPlayers = CivilizationSet.Only(2); loss.Data.AddUnit(0, 1, type, new(10, 10));
                loss.Data.Unit(0).MovesUsed = 254; w.Espionage.LastMission = CityMission.Incite;
                int publishes = 0; List<string> effects = [];
                var context = new OperationContext(w, f.Data.Process, f.Random, new PresentationRandom(2, "incident"),
                    new(default, new(2)), default, (name, _) => effects.Add(name), () => ++publishes);
                var op = new SpyEscapeOperation(loss.Escape, f.Incidents, 0, 0, 2, "escape");
                var step = op.Resume(context, new()); Equal(CommandStatus.AwaitingInteraction, step.Status);
                Equal(false, loss.Data.Unit(0).Token.Occupied); Equal(0, loss.Incidents.Count); Equal(1, publishes);
                int prefix = loss.Order.Count; byte moves = loss.Data.Unit(0).MovesUsed;
                Equal(CommandStatus.AwaitingInteraction, op.Resume(context, new(OperationInputKind.Proceed)).Status);
                Equal(CommandStatus.Completed, op.Resume(context, input).Status);
                Equal(input.Kind == OperationInputKind.Choice, w.Civilizations[2].Treaties[1].War);
                Equal(prefix, loss.Order.Count); Equal(moves, loss.Data.Unit(0).MovesUsed); Equal(1, publishes);
                Equal("espionage.escape.completed", effects.Single()); Equal(CityMission.Incite, w.Espionage.LastMission);
                f.Random.RequireChoicesConsumed();
            }
        var invalid = new EliminationFixture(); invalid.Data.AddUnit(0, 1, 4, new(10, 10));
        var incident = new IncidentFixture(invalid.Random, invalid.Data);
        var fate = invalid.Escape.Resolve(0, 0, 8, "invalid");
        Equal(false, invalid.Data.Unit(0).Token.Occupied); Equal(true, fate.Lost);
        var bad = incident.Incidents.Begin(fate.Incident!.Actor, fate.Incident.Victim, "invalid");
        Throws<IndexOutOfRangeException>(() => bad.Resume()); Equal(true, bad.Completed);
        Equal(null, bad.Resume()); invalid.Random.RequireChoicesConsumed();
    }

    private static void IncidentFailuresRetainWarPrefix()
    {
        var f = new IncidentFixture(); f.Treaty(); var w = f.World;
        w.Civilizations[1].Government = Government.Democracy; f.FailScandal = true;
        var op = f.Incidents.Begin(1, 2, "incident"); Throws<InvalidOperationException>(() => op.Resume());
        Equal(true, op.Completed); Equal(true, w.Civilizations[2].Treaties[1].War);
        Equal(Government.Democracy, w.Civilizations[1].Government); Equal(true, w.Civilizations[2].Treaties[1].Vendetta);
        Equal(null, op.Resume()); Equal(2, f.Notices.Count); f.Random.RequireChoicesConsumed();
        f = new(); w = f.World; w.Globals.Multiplayer = GameMode.Tcp; w.Civilizations[2].Attitude[1] = 10;
        op = f.Incidents.Begin(1, 2, "network"); Throws<InvalidOperationException>(() => op.Resume());
        Equal((byte)10, w.Civilizations[2].Attitude[1]); Equal(true, op.Completed); f.Random.RequireChoicesConsumed();
    }
}
