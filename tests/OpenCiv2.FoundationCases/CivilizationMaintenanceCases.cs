using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.State;
using OpenCiv2.Formats;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static void GovernmentTurnCalendarAndChildren()
    {
        foreach (var (turn, revolution, statue, request) in new[]
        {
            (4, true, false, false), (4, false, false, true), (5, true, false, false),
            (5, true, true, true), (8, true, true, false)
        })
        {
            var f = new GovernmentMoodFixture(); var world = f.World;
            world.Globals.Turn = (short)turn;
            world.Civilizations[1].Government = Government.Anarchy;
            world.Civilizations[1].Flags.Revolution = revolution;
            world.Civilizations[1].Flags.GovernmentChanged = true;
            if (statue) world.Globals.WonderCity[19] = 0;
            var opaque = new RecordCompatibility(world.Lifetime);
            opaque.Civilizations[1] = new() { Flags = 0x8040 };
            int clearCount = 0;
            var operation = new GovernmentTurn(world, f.Data.Rules, 1, owner =>
            {
                ++clearCount;
                Equal(false, world.Civilizations[owner].Flags.GovernmentChanged);
                opaque.Civilizations[owner] = opaque.Civilizations[owner] with
                { Flags = (ushort)(opaque.Civilizations[owner].Flags & ~0x40) };
            });
            Equal(request ? 1 : (int?)null, operation.NextChange());
            Equal((ushort)0x8000, opaque.Civilizations[1].Flags);
            if (request)
            {
                Equal(1, operation.NextChange()); Equal(1, clearCount); // No duplicate preparation while awaiting a child.
                f.Governments.Change(1, 1);
                Equal(1, f.SelectionRequests.Count);
                f.Governments.Set(1, (int)Government.Despotism);
                Equal(1, f.RateRequests.Count);
                operation.ChangeCompleted();
                Equal(null, operation.NextChange());
                Equal(Government.Despotism, world.Civilizations[1].Government);
            }
            Equal(true, operation.Completed); Equal(1, clearCount);
            Equal(turn == 5 && !statue, world.Civilizations[1].Flags.Revolution);
        }
        var ai = new GovernmentMoodFixture();
        ai.World.Globals.Turn = 4;
        ai.World.Civilizations[2].Government = Government.Anarchy;
        ai.World.Civilizations[1].Treaties[2].Embassy = true;
        var aiTurn = new GovernmentTurn(ai.World, ai.Data.Rules, 2, _ => { });
        Equal(1, aiTurn.NextChange());
        ai.Governments.Change(2, 1); // Observer notice sets Revolution.
        Equal(true, ai.World.Civilizations[2].Flags.Revolution);
        aiTurn.ChangeCompleted();
        Equal(1, aiTurn.NextChange()); // Live AI flag triggers the second announcement.
        ai.Governments.Change(2, 1); aiTurn.ChangeCompleted();
        Equal(null, aiTurn.NextChange()); Equal(2, ai.Announcements.Count);
        Equal(false, ai.World.Civilizations[2].Flags.Revolution);
        Throws<InvalidOperationException>(aiTurn.ChangeCompleted);
    }

    private sealed class DiplomacyTurnChildren : IDiplomacyTurnEffects
    {
        public List<string> Calls { get; } = [];
        public Action<string>? OnCall;
        private void Call(string value) { Calls.Add(value); OnCall?.Invoke(value); }
        public void ExchangeAdvances(int from, int to) => Call($"exchange/{from}/{to}");
        public void Meet(int from, int to) => Call($"meet/{from}/{to}");
        public void WarEnded(int from, int to) => Call($"peace/{from}/{to}");
        public void ReviewRelation(int from, int human) => Call($"review/{from}/{human}");
        public void MilitaryAid(int owner) => Call($"aid/{owner}");
        public void SeekWarAlly(int from, int human) => Call($"seek/{from}/{human}");
    }

    private static GameplayRandom DiplomacyRandom(string call, params Choice[] extra) => new(new ControlledChoices(new(new[]
    {
        Choice.Create(new("V04.turn-random", call), new SamplingLaw.PrimaryModulo(100), ChoiceCode.Primary(123)),
        Choice.Create(new("V04.turn-senate", call), new SamplingLaw.PrimaryModulo(3), ChoiceCode.Primary(0))
    }.Concat(extra))), "diplomacy-turn");

    private static void DiplomacyHumanTurnDrawsAndFlags()
    {
        var f = new GovernmentMoodFixture(); var world = f.World;
        world.Globals.Turn = 3; world.Globals.CivsAlive = CivilizationSet.Only(1);
        world.Civilizations[1].DiploRequestCount = -128;
        for (int other = 0; other < 8; ++other)
        {
            world.Civilizations[1].Treaties[other] = new()
            { ChangedThisTurn = true, WarThisTurn = true, MetThisTurn = true, HostileIntent = true };
        }
        var children = new DiplomacyTurnChildren(); var random = DiplomacyRandom("human");
        new DiplomacyTurn(world, f.Data.Rules, random, children).Run(1, "human");
        random.RequireChoicesConsumed(); Equal((byte)23, world.Civilizations[1].DiploRandom);
        Equal((sbyte)127, world.Civilizations[1].DiploRequestCount);
        Equal(true, world.Civilizations[1].Flags.SenateActive); Equal(0, children.Calls.Count);
        for (int other = 1; other < 8; ++other)
        {
            Equal(true, world.Diplomacy.RecentPeace[other]);
            var relation = world.Civilizations[1].Treaties[other];
            Equal(false, relation.ChangedThisTurn || relation.WarThisTurn || relation.MetThisTurn);
            Equal(true, relation.HostileIntent);
        }
        Equal(true, world.Civilizations[1].Treaties[0].ChangedThisTurn);
        var noDraws = new GameplayRandom(new ControlledChoices(new([])), "diplomacy-turn");
        new DiplomacyTurn(world, f.Data.Rules, noDraws, children).Run(0, "barbarian");
        noDraws.RequireChoicesConsumed();
        world.Globals.Multiplayer = GameMode.Tcp;
        Throws<InvalidOperationException>(() => new DiplomacyTurn(world, f.Data.Rules, noDraws, children).Run(1, "network"));
    }

    private static void DiplomacyAiAgingAndPolicyOrder()
    {
        var f = new GovernmentMoodFixture(); var world = f.World;
        world.Globals.Turn = 32; world.Globals.CivsAlive = CivilizationSet.Only(1).With(2).With(3);
        world.Civilizations[1].Reputation = 8;
        world.Civilizations[2].Treaties[1] = new() { HostileIntent = true, Alliance = true, Contact = true };
        world.Civilizations[2].Treaties[3] = new() { Aggressor = true, ExtendedAggressor = true, Tribute = true };
        var children = new DiplomacyTurnChildren(); var random = DiplomacyRandom("aging");
        children.OnCall = value =>
        {
            if (value == "review/2/1") world.Globals.HumanPlayers = world.Globals.HumanPlayers.With(2);
        };
        new DiplomacyTurn(world, f.Data.Rules, random, children).Run(2, "aging");
        random.RequireChoicesConsumed();
        Equal((byte)100, world.Civilizations[2].Attitude[1]); Equal((short)-1, world.Civilizations[1].LastContactTurn[2]);
        Equal(true, world.Civilizations[2].Treaties[1].Alliance); Equal(false, world.Civilizations[2].Treaties[1].HostileIntent);
        Equal(true, world.Civilizations[2].Treaties[3].Aggressor);
        Equal(false, world.Civilizations[2].Treaties[3].ExtendedAggressor || world.Civilizations[2].Treaties[3].Tribute);
        Equal(true, children.Calls.SequenceEqual(["review/2/1", "aid/2", "seek/2/1"])); // Entry to the later block is captured.

        world.Globals.HumanPlayers = CivilizationSet.Only(1); world.Globals.Turn = 64;
        children.OnCall = null; children.Calls.Clear(); random = DiplomacyRandom("next");
        new DiplomacyTurn(world, f.Data.Rules, random, children).Run(2, "next");
        Equal(false, world.Civilizations[2].Treaties[3].Aggressor);

        world.Globals.Turn = 7; world.Civilizations[1].Reputation = 0;
        world.Civilizations[2].Treaties[1] = new() { HostileIntent = true, Peace = true, Ceasefire = true };
        world.Civilizations[1].LastContactTurn[2] = -20;
        random = DiplomacyRandom("hostile", Choice.Create(new("V04.turn-hostile", "hostile/1"),
            new SamplingLaw.PrimaryModulo(3), ChoiceCode.Primary(0)));
        new DiplomacyTurn(world, f.Data.Rules, random, children).Run(2, "hostile"); random.RequireChoicesConsumed();
        Equal((short)-1, world.Civilizations[1].LastContactTurn[2]);
        var changed = world.Civilizations[2].Treaties[1];
        Equal(true, changed.BrokenTreaty && changed.Aggressor && changed.ExtendedAggressor);
        Equal(false, changed.Peace || changed.Ceasefire || changed.HostileIntent);
    }

    private static void DiplomacyStaleContactChildren()
    {
        foreach (string branch in new[] { "alliance", "peace", "war", "recent", "eight" })
        {
            var f = new GovernmentMoodFixture(); var world = f.World;
            world.Globals.Turn = (short)(branch == "eight" ? 8 : 16);
            world.Globals.CivsAlive = CivilizationSet.Only(2).With(3);
            world.Globals.RevealMap = 1;
            world.Civilizations[2].Treaties[3] = new()
            {
                Contact = true,
                Alliance = branch is "alliance" or "eight",
                Peace = branch == "peace",
                War = branch == "war",
                RecentContact = branch == "recent"
            };
            world.Civilizations[3].Treaties[2] = world.Civilizations[2].Treaties[3];
            world.Globals.Status.AiCoordination = true;
            var children = new DiplomacyTurnChildren();
            var random = DiplomacyRandom(branch);
            new DiplomacyTurn(world, f.Data.Rules, random, children).Run(2, branch); random.RequireChoicesConsumed();
            string? first = branch switch { "alliance" or "eight" => "exchange/2/3", "peace" => "meet/2/3", "war" => "peace/2/3", _ => null };
            if (first is not null) Equal(first, children.Calls[0]);
            Equal(branch is "recent" or "eight", world.Civilizations[2].Treaties[3].Contact);
            Equal(branch is "alliance" or "eight", world.Civilizations[2].Treaties[3].Alliance);
            Equal(false, world.Civilizations[2].Treaties[3].RecentContact);
        }
        var failed = new GovernmentMoodFixture(); failed.World.Globals.CivsAlive = CivilizationSet.Only(2).With(3);
        failed.World.Globals.Turn = 16;
        failed.World.Civilizations[2].Treaties[3] = new() { Alliance = true, Contact = true, ChangedThisTurn = true };
        var effects = new DiplomacyTurnChildren { OnCall = _ => throw new InvalidOperationException("AI child") };
        Throws<InvalidOperationException>(() => new DiplomacyTurn(failed.World, failed.Data.Rules, DiplomacyRandom("failure"), effects).Run(2, "failure"));
        Equal(true, failed.World.Civilizations[2].Treaties[3].Contact); // Failure precedes the bilateral clear.
        Equal(false, failed.World.Civilizations[2].Treaties[3].ChangedThisTurn);
        Equal(true, failed.World.Diplomacy.RecentPeace[3]);
    }
}
