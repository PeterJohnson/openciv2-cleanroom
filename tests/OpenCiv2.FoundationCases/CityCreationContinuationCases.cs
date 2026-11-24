using OpenCiv2.Base;
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
    // C01 first-AI-city defender precedes route/environment/commodity completion and founding deletion.
    private sealed class RetainedCityCreationFixture
    {
        public SinglePlayerDiplomacyFixture Diplomacy { get; }
        public RevelationFixture Data => Diplomacy.Data;
        public GameState World => Data.World;
        public FoundingFixture Founding { get; }
        public CityCreation Creator { get; }
        public Event Permission { get; }
        public MapPoint Position { get; } = new(30, 10);
        public CitySlot Slot { get; }
        public ref OpenCiv2.Core.State.City City => ref World.Entities.Cities.At(Slot);
        public RetainedCityCreationFixture(RevelationFixture? data = null)
        {
            Diplomacy = new(data: data);
            var units = new UnitCreation(World, Data.Process, Data.Stacks,
                new UnitRevelation(World, Data.Rules, Data.Stacks, Diplomacy.Services.Contact, Data), Data);
            Founding = new(data: Data, creation: units);
            Creator = CityCreator(Data, units: units);
            for (int i = 0; i < World.Entities.Cities.End; ++i)
            {
                ref var city = ref World.Entities.Cities.Storage[i];
                if (city.Token.Occupied && city.Owner.Value == 2)
                { city.Owner = new(1); World.Map.TileAt(city.Position).Owner = 1; }
            }
            Data.AddCity(2, 1, new(31, 9));
            World.Civilizations[2].CityCount = 0; World.Civilizations[2].Tribe = 0;
            World.Civilizations[2].Treaties[1].Contact = false;
            World.Globals.Turn = 100; World.Globals.HumanPlayers = CivilizationSet.Only(1);
            World.Multiplayer.LocalHuman = 1;
            for (int i = 0; i < RuleLimits.UnitTypes; ++i)
            { Data.Rules.Units[i] = Data.Rules.Units[0] with { Domain = RuleDomain.Land, Role = RuleRole.Attack, CostRows = 2 }; }
            Data.Rules.Units[0] = Data.Rules.Units[0] with { Role = RuleRole.Settle };
            Data.Rules.Units[3] = Data.Rules.Units[0] with { Role = RuleRole.Defend, Defense = 2, Prerequisite = AdvanceReference.None };
            Data.AddUnit(0, 2, 0, Position); World.Civilizations[2].UnitsActive[0] = 1; World.UnitTotals[2] = 1;
            int index = 0;
            while (index < World.Entities.Cities.End && World.Entities.Cities.Storage[index].Token.Occupied) ++index;
            Slot = new((ushort)index); City.RouteCount = 3; City.Science = 9;
            World.Events.Loaded = true;
            Permission = NegotiationEvent(World, 2, 1, deny: true);
            Permission.Values.Actions = new() { Text = true, JustOnce = true };
            Permission.SetString(Event.TextBegin, new("Defender contact"));
        }
        public CityCreation.Operation Begin() => Creator.Begin(new(2), Position, "city");
        public void Consumed() { Founding.Life.Random.RequireChoicesConsumed(); Diplomacy.Events.Consumed(); }
    }

    private static void CityCreationRetainedDefender()
    {
        var f = new RetainedCityCreationFixture();
        using var operation = f.Begin(); var pending = operation.Resume();
        Equal(true, pending is DiplomaticContactDecision.Scenario { Value: ScenarioText });
        Equal(false, operation.Completed); Equal(null, operation.Result);
        Equal((byte)3, f.City.RouteCount); Equal((short)9, f.City.Science);
        Equal((sbyte)4, f.City.Size); Equal(true, f.City.Improvements.Contains(BuildingIds.Palace));
        Equal(true, f.City.Improvements.Contains(BuildingIds.Library));
        int defender = f.Data.Stacks.FirstAt(f.Position); var token = f.Data.Unit(defender).Token;
        Equal(new UnitType(3), f.Data.Unit(defender).Type); Equal(f.Slot, f.Data.Unit(defender).Home);
        Equal(pending, operation.Resume()); Equal(token, f.Data.Unit(defender).Token);
        // A genuine decision boundary admits changed terrain; the pending city's identity is retained.
        f.World.Map.TileAt(new(29, 9)).Terrain = Terrain.Mountains;
        f.City.RoutePartners[1] = new(0x1234);
        Equal(null, operation.Resume(new(0))); Equal(true, operation.Completed); Equal(f.Slot, operation.Result);
        Equal((byte)0, f.City.RouteCount); Equal((short)0, f.City.Science);
        Equal(new CitySlot(0x1200), f.City.RoutePartners[1]); Equal(true, f.City.Flags.HydroPossible);
        Equal(false, f.City.Flags.TradeDirty); Equal(token, f.Data.Unit(defender).Token);
        Equal((short)1, f.World.Civilizations[2].CityCount); operation.Resume(new(0)); f.Consumed();
    }

    private static void CityCreationRetainedFailureAndDisposal()
    {
        var f = new RetainedCityCreationFixture();
        using (var operation = f.Begin())
        {
            operation.Resume(); var token = f.City.Token;
            operation.Dispose(); Throws<ObjectDisposedException>(() => operation.Resume(new(0)));
            Equal(token, f.City.Token); Equal((byte)3, f.City.RouteCount); Equal((short)9, f.City.Science);
            Equal(false, operation.Completed); Equal(null, operation.Result); f.Consumed();
        }
        f = new(); f.Permission.Values.Actions.PlayWaveFile = true;
        f.Permission.SetString(34, new("defender.wav")); f.World.Globals.GameOptions.Sound = true;
        f.Diplomacy.Events.Media.FailProbe = true;
        using (var operation = f.Begin())
        {
            Throws<IOException>(() => operation.Resume()); Throws<InvalidOperationException>(() => operation.Resume());
            Equal((byte)3, f.City.RouteCount); Equal((short)1, f.World.Civilizations[2].CityCount);
            Equal(new UnitType(3), f.Data.Unit(f.Data.Stacks.FirstAt(f.Position)).Type);
            Equal(false, operation.Completed); f.Consumed();
        }
        f = new(); f.World.Civilizations[2].MilitaryCount = 1949;
        using (var operation = f.Begin())
        {
            Equal(null, operation.Resume()); Equal(true, operation.Completed); Equal(f.Slot, operation.Result);
            Equal((byte)0, f.City.RouteCount); Equal(new UnitType(0), f.Data.Unit(f.Data.Stacks.FirstAt(f.Position)).Type);
            f.Consumed();
        }
    }

    private static void FoundingRetainedDefenderPrivateContact()
    {
        using var runtime = new SessionRuntime(new(), new(260));
        var f = new RetainedCityCreationFixture(CreationFixture(new(runtime.Process, runtime.World)));
        ParticipantId owner = default, neighbor = default;
        runtime.RegisterCommand(new(new(1, "test.found-defender", "Found a city with a retained defender", AccessClass.Player, new([]), new([new("outcome", ValueKind.Text)])),
            (_, _, _) => new(), _ => new CityFoundingOperation(f.Founding.Founding.Begin(new(0), "found"),
                c => c == 1 ? neighbor : owner, _ => throw new InvalidOperationException("Direct founding has no city window"))));
        runtime.BeginClientSession(new()); var actor = SessionClient(runtime, 2); var witness = SessionClient(runtime, 1);
        owner = actor.Identity.Participant; neighbor = witness.Identity.Participant;
        var command = actor.Submit(new("test.found-defender", new([]))).Correlation.Command; runtime.AdvanceOne();
        for (int i = 0; i < 50 && witness.Result(command)?.Interaction is null && !RequiredResult(actor, command).Status.Terminal(); ++i)
        { runtime.AdvanceOne(); }
        var prompt = RequiredInteraction(witness, command);
        Equal(null, RequiredResult(actor, command).Interaction); Equal(false, actor.Reply(prompt, 0).Accepted);
        Equal(true, f.Data.Unit(0).Token.Occupied); Equal((byte)3, f.City.RouteCount);
        Equal(true, witness.Reply(prompt, 0).Accepted);
        for (int i = 0; i < 50 && !RequiredResult(actor, command).Status.Terminal(); ++i) runtime.AdvanceOne();
        var result = RequiredResult(actor, command);
        if (result.Status != CommandStatus.Completed)
        { throw new InvalidOperationException($"Founding did not complete: {result.Status} {result.Code}"); }
        Equal(false, f.Data.Unit(0).Token.Occupied); Equal((byte)0, f.City.RouteCount);
        Equal(new UnitType(3), f.Data.Unit(f.Data.Stacks.FirstAt(f.Position)).Type);
        Equal(false, witness.Reply(prompt, 0).Accepted); f.Consumed();
    }
}
