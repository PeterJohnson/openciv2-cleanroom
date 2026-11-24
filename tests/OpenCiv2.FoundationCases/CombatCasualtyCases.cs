using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private sealed class CasualtyFixture : ICombatHistoryEffects
    {
        public EliminationFixture Life { get; }
        public RevelationFixture Data => Life.Data;
        public GameState World => Data.World;
        public CombatWorkspace State { get; } = new();
        public CombatHistory History { get; }
        public CombatCasualties Casualties { get; }
        public List<CombatDeath> Deaths { get; } = [];
        public Action<CombatDeath>? OnDeath;
        public Action? OnColor, OnName, OnRefresh;
        public List<string> Calls { get; } = [];
        public CasualtyFixture(ScenarioTriggers? events = null, RevelationFixture? data = null)
        {
            Life = new(null, data ?? CargoFixture(4, 4, 4));
            Data.AddUnit(3, 2, 4, new(20, 10));
            var incidents = new IncidentFixture(Life.Random, Data);
            World.Globals.Year = -400; World.Civilizations[1].UnitsActive[4] = 3; World.UnitTotals[1] = 3;
            History = new(World, this);
            Casualties = events is null
                ? new(World, Data.Stacks, State, History, Life.Elimination, incidents.Incidents, death =>
                { Calls.Add("event"); Deaths.Add(death); OnDeath?.Invoke(death); })
                : new(World, Data.Stacks, State, History, Life.Elimination, incidents.Incidents, events);
        }
        public int SecondaryColor(int civilization) { Calls.Add("color:" + civilization); OnColor?.Invoke(); return 300 + civilization; }
        public string PluralName(int civilization) { Calls.Add("name:" + civilization); OnName?.Invoke(); return World.Names[civilization].Tribe; }
        public void RefreshHistory(int civilization) { Calls.Add("refresh:" + civilization); OnRefresh?.Invoke(); }
        public CombatCasualties.Operation Single(int slot = 0) => Casualties.Begin(new((ushort)slot), new(3), CargoPoint, "combat");
        public CombatCasualties.Operation Stack() => Casualties.BeginStack(new(1), new(3), CargoPoint, "combat");
        public void Complete(CombatCasualties.Operation operation)
        {
            int budget = 50;
            while (!operation.Completed && --budget > 0) Equal(null, operation.Resume());
            Equal(true, operation.Completed); Life.Random.RequireChoicesConsumed();
        }
    }

    // U06 casualty ring: signed field retention, 299 counted entries and fresh-world isolation.
    private static void CombatHistoryRing()
    {
        var f = new CasualtyFixture(); var log = f.World.Casualties[1];
        f.World.Names[2].Tribe = "A long logical name with Unicode 日本語 retained";
        f.History.Record(new(0), new(3), new(65535, 65534), 65533);
        Equal((short)4, log.At(0).Type); Equal(new MapPoint(-1, -2), log.At(0).Position); Equal((short)-3, log.At(0).Year);
        Equal((short)302, log.At(0).OpponentColor); Equal(f.World.Names[2].Tribe, log.At(0).OpponentName);
        Equal(1, log.Count); Equal(1, log.Next); Equal(0, log.First);
        for (int i = 1; i <= 300; ++i) f.History.Record(new(0), new(3), CargoPoint, i);
        Equal(299, log.Count); Equal(1, log.Next); Equal(2, log.First); Equal((short)300, log.At(0).Year);
        Equal((short)299, log.At(299).Year); Equal(0, f.World.Casualties[2].Count);
        var fresh = new GameState(f.Data.Process); Equal(0, fresh.Casualties[1].Count); Equal(string.Empty, fresh.Casualties[1].At(0).OpponentName);
        Equal(299, log.Count);
        for (int i = 301; i < 599; ++i) f.History.Record(new(0), new(3), CargoPoint, i);
        Equal(299, log.Next); Equal(0, log.First); Equal(299, log.Count);
        f.History.Record(new(0), new(3), CargoPoint, 599); Equal(0, log.Next); Equal(1, log.First); Equal(299, log.Count);
        log.Count = int.MaxValue; log.First = 20; f.History.Record(new(0), new(3), CargoPoint, 0); Equal(int.MinValue, log.Count);
    }

    // Required history queries can change the live owner/next index. Each later field reloads them.
    private static void CombatHistoryLiveQueries()
    {
        var f = new CasualtyFixture();
        f.OnColor = () => { f.Data.Unit(0).Owner = new(2); f.World.Casualties[2].Next = 5; f.Data.Unit(3).Owner = new(3); };
        f.OnName = () => { f.Data.Unit(0).Owner = new(4); f.World.Casualties[4].Next = 8; };
        f.World.Names[3].Tribe = "Third party";
        f.History.Record(new(0), new(3), CargoPoint, -100);
        Equal((short)4, f.World.Casualties[1].At(0).Type); Equal(CargoPoint, f.World.Casualties[1].At(0).Position);
        Equal((short)-100, f.World.Casualties[1].At(0).Year); Equal((short)0, f.World.Casualties[1].At(0).OpponentColor);
        Equal((short)302, f.World.Casualties[2].At(5).OpponentColor); Equal(string.Empty, f.World.Casualties[2].At(5).OpponentName);
        Equal("Third party", f.World.Casualties[4].At(8).OpponentName); Equal(9, f.World.Casualties[4].Next); Equal(1, f.World.Casualties[4].Count);
        Equal(0, f.World.Casualties[1].Next); Equal(5, f.World.Casualties[2].Next);
        Sequence(new[] { "color:2", "name:3", "refresh:4" }, f.Calls);
        f = new(); f.World.Casualties[1].Next = 300;
        Throws<IndexOutOfRangeException>(() => f.History.Record(new(0), new(3), CargoPoint, 0)); Equal(0, f.Calls.Count);
    }

    private static void CombatCasualtyOrdering()
    {
        foreach (bool loaded in new[] { false, true })
        {
            var f = new CasualtyFixture(); f.World.Events.Loaded = loaded; f.State.KillCount = int.MaxValue;
            f.World.Civilizations[1].UnitsLost[4] = 254;
            f.OnDeath = death =>
            {
                Equal(true, f.Data.Unit(0).Token.Occupied); Equal((byte)255, f.World.Civilizations[1].UnitsLost[4]);
                Equal(1, f.World.Casualties[1].Count); Equal(int.MinValue, f.State.KillCount);
                Equal(new CombatDeath(new(4), 2, 1), death);
                // S02 may replace the retained losing record. Basic deletion must not reject its new identity.
                f.Life.Deletion.Erase(0, false); f.Data.AddUnit(0, 2, 4, CargoPoint); f.Data.Unit(0).Token = new(999);
            };
            using var op = f.Single(); f.Complete(op); Equal(false, f.Data.Unit(0).Token.Occupied);
            Equal(true, f.Data.Unit(1).Token.Occupied && f.Data.Unit(2).Token.Occupied);
            Equal(loaded ? 1 : 0, f.Deaths.Count); Equal((byte)255, f.World.Civilizations[1].UnitsLost[4]);
            Equal(1, f.World.Casualties[1].Count); Equal((short)-400, f.World.Casualties[1].At(0).Year);
            f.Complete(op); Equal(1, f.World.Casualties[1].Count);
        }
        var live = new CasualtyFixture(); live.World.Events.Loaded = true;
        live.OnRefresh = () => { live.Data.Unit(0).Type = new(11); live.Data.Unit(0).Owner = new(4); live.Data.Unit(3).Owner = new(5); };
        using var changed = live.Single(); live.Complete(changed);
        Equal(new CombatDeath(new(11), 5, 4), live.Deaths.Single());
        Equal((byte)1, live.World.Civilizations[1].UnitsLost[4]); Equal((byte)0, live.World.Civilizations[4].UnitsLost[11]);
        Equal(1, live.World.Casualties[1].Count); Equal(0, live.World.Casualties[4].Count);
        var saturate = new CasualtyFixture(); saturate.World.Civilizations[1].UnitsLost[4] = 255;
        using var loss = saturate.Single(); saturate.Complete(loss); Equal((byte)255, saturate.World.Civilizations[1].UnitsLost[4]);
    }

    private static void CombatStackCapturedNext()
    {
        var f = new CasualtyFixture(); f.State.KillCount = 77; f.World.Events.Loaded = true;
        f.OnDeath = _ =>
        {
            if (f.Deaths.Count != 1) return;
            // Remove the next unit during the first event and install a replacement elsewhere.
            // The casualty walk must still visit the captured raw slot, not follow repaired links.
            f.Life.Deletion.Erase(1, false); f.Data.AddUnit(1, 2, 4, new(22, 10));
        };
        using var op = f.Stack(); Equal(null, op.Resume()); Equal(false, op.Completed); Equal(1, f.State.KillCount);
        Equal(false, f.Data.Unit(0).Token.Occupied); Equal(true, f.Data.Unit(1).Token.Occupied);
        f.Complete(op); Equal(2, f.State.KillCount); Equal(2, f.Deaths.Count);
        Equal(false, f.Data.Unit(1).Token.Occupied); Equal(true, f.Data.Unit(2).Token.Occupied);
        Equal(1, f.World.Casualties[1].Count); Equal(1, f.World.Casualties[2].Count);
        Equal(new CombatDeath(new(4), 2, 2), f.Deaths[1]);
        var ordinary = new CasualtyFixture(); using var all = ordinary.Stack(); ordinary.Complete(all);
        Equal(3, ordinary.State.KillCount); Equal((byte)3, ordinary.World.Civilizations[1].UnitsLost[4]);
        Equal(3, ordinary.World.Casualties[1].Count); for (int i = 0; i < 3; ++i) Equal(false, ordinary.Data.Unit(i).Token.Occupied);
    }

    private static CasualtyFixture NestedCasualty()
    {
        var f = new CasualtyFixture(); CreationFixture(f.Data);
        f.Data.Unit(0).Type = new(0); f.World.Civilizations[1].UnitsActive[0] = 1; f.World.Civilizations[1].CityCount = 0;
        f.Data.AddUnit(4, 3, 4, new(24, 10)); f.Data.Unit(4).Flags.SpyMission = true; f.Data.Unit(4).Counter = 1;
        f.World.Civilizations[1].Treaties[3].Peace = true; f.Life.Recreate = c => Creation(f.Data, c, f.Life.Random);
        return f;
    }
    private static void CombatCasualtyNestedAndSession()
    {
        var f = NestedCasualty(); using var op = f.Single(); var pending = op.Resume()!;
        Equal(1, ((EspionagePretext)pending).Victim.Civilization); Equal(3, ((EspionagePretext)pending).Actor.Civilization); Equal(pending, op.Resume());
        Equal(false, op.Completed); Equal(false, f.Data.Unit(0).Token.Occupied); Equal(1, f.State.KillCount);
        Equal(1, f.World.Casualties[1].Count); op.Resume(0); f.Complete(op); Equal(false, f.World.UnitDeletion.Active);
        f = NestedCasualty(); var context = new OperationContext(f.World, f.Data.Process, f.Life.Random, new PresentationRandom(1, "casualty"),
            new(default, new(2)), default, (_, _) => { }, () => { });
        using var session = new CombatCasualtyOperation(f.Single(), c => new((ulong)c));
        var question = session.Resume(context, new()); Equal(CommandStatus.AwaitingInteraction, question.Status);
        Equal(new ParticipantId(1), question.Interaction!.Recipient); Equal(1, f.World.Casualties[1].Count);
        var result = session.Resume(context, new(OperationInputKind.Cancel)); Equal(CommandStatus.Completed, result.Status);
        Equal(result, session.Resume(context, new())); f.Life.Random.RequireChoicesConsumed();
    }

    private static void CombatCasualtyFailuresAndDisposal()
    {
        foreach (int phase in Enumerable.Range(0, 4))
        {
            var f = new CasualtyFixture(); f.World.Events.Loaded = true;
            Action fail = () => throw new InvalidOperationException("owning child failed");
            if (phase == 0) f.OnColor = fail;
            else if (phase == 1) f.OnName = fail;
            else if (phase == 2) f.OnRefresh = fail; else f.OnDeath = _ => fail();
            using var op = f.Single(); Throws<InvalidOperationException>(() => op.Resume());
            Equal((byte)1, f.World.Civilizations[1].UnitsLost[4]); Equal(true, f.Data.Unit(0).Token.Occupied);
            Equal(phase >= 2 ? 1 : 0, f.World.Casualties[1].Count); Equal(phase == 3 ? 1 : 0, f.State.KillCount);
            Equal(phase > 0 ? (short)302 : (short)0, f.World.Casualties[1].At(0).OpponentColor);
            int calls = f.Calls.Count; Throws<InvalidOperationException>(() => op.Resume()); Equal(calls, f.Calls.Count);
        }
        var partial = new CasualtyFixture(); var stack = partial.Stack(); stack.Resume(); stack.Dispose();
        Throws<ObjectDisposedException>(() => stack.Resume()); Equal(1, partial.State.KillCount); Equal(true, partial.Data.Unit(1).Token.Occupied);
        var pending = NestedCasualty(); var nested = pending.Single(); Equal(true, nested.Resume() is not null); nested.Dispose();
        Throws<ObjectDisposedException>(() => nested.Resume(0)); Equal(1, pending.World.Casualties[1].Count); Equal(false, pending.Data.Unit(0).Token.Occupied);
        var invalid = new CasualtyFixture(); invalid.World.Casualties[1].Next = 300;
        using var invalidOp = invalid.Single(); Throws<IndexOutOfRangeException>(() => invalidOp.Resume());
        Equal((byte)1, invalid.World.Civilizations[1].UnitsLost[4]); Equal(0, invalid.State.KillCount); Equal(true, invalid.Data.Unit(0).Token.Occupied);
        var remote = new CasualtyFixture(); remote.World.Globals.Multiplayer = GameMode.Tcp; remote.State.KillCount = 9;
        using var blocked = remote.Stack(); Throws<InvalidOperationException>(() => blocked.Resume());
        Equal(9, remote.State.KillCount); Equal((byte)0, remote.World.Civilizations[1].UnitsLost[4]);
    }
}
