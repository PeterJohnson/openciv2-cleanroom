using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.State;
using OpenCiv2.Formats;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static GameState ScenarioWorld() => new(new ProcessContext())
    {
        Globals = new() { HumanPlayers = CivilizationSet.Only(1), CivsAlive = CivilizationSet.Only(1).With(2) }
    };

    private static Event NegotiationEvent(GameState world, int talker = 1, int listener = 2,
        int talkerType = 4, int listenerType = 4, bool deny = true)
    {
        var entry = world.Events.Collection.Append();
        entry.Values = new()
        {
            Trigger = EventTrigger.Negotiation,
            WhoCiv = talker,
            WhomCiv = listener,
            TalkerType = talkerType,
            ListenerType = listenerType,
            DenyNegotiation = deny
        };
        return entry;
    }

    // S02 §7 / Appendix A and F06: only the exact stored Deny value changes permission.
    private static void NegotiationPermissionEncoding()
    {
        foreach (int value in new[] { int.MinValue, -4096, -1, 0, 1, 4095, 4096, 4097, 8192, int.MaxValue })
        {
            var stored = new EventRecord
            {
                Triggers = 16,
                WhoCiv = 1,
                WhomCiv = 2,
                TalkerType = 4,
                ListenerType = 4,
                NegotiationFlag = value
            };
            Equal(value, ClassicRecords.DecodeEventRecord(ClassicRecords.Encode(stored)).NegotiationFlag);
            var world = ScenarioWorld(); var entry = world.Events.Collection.Append();
            entry.Values = StateConversion.ToLive(stored);
            Equal(value == 4096, entry.Values.DenyNegotiation);
            Equal(value != 4096, ScenarioPermissions.AllowNegotiation(world, 1, 2, _ => { }));
            var output = new ItemCapture();
            Equal(true, ClassicLive.WriteEventItems(output.Write, _ => true, _ => true, world.Events.Collection));
            var exported = ClassicRecords.DecodeEventRecord(output.Items[2]);
            Equal(value == 4096 ? 4096 : 0, exported.NegotiationFlag);
            Equal(entry.Values.DenyNegotiation, StateConversion.ToLive(exported).DenyNegotiation);
            entry.Values.DenyNegotiation = !entry.Values.DenyNegotiation;
            Equal(value == 4096 ? 0 : 4096, StateConversion.ToStored(entry).NegotiationFlag);
        }
    }

    private static void NegotiationFirstMatch()
    {
        var world = ScenarioWorld(); var visits = new List<ulong>();
        Equal(true, ScenarioPermissions.AllowNegotiation(world, 1, 2, _ => throw new Exception("No match")));
        var wrong = NegotiationEvent(world); wrong.Values.Trigger = EventTrigger.DenyNegotiation;
        NegotiationEvent(world, talker: 3); NegotiationEvent(world, listener: 3);
        var allowed = NegotiationEvent(world, deny: false); var denied = NegotiationEvent(world);
        Equal(true, ScenarioPermissions.AllowNegotiation(world, 1, 2, e => visits.Add(e.Identity)));
        Sequence(new[] { allowed.Identity }, visits);
        allowed.Values.DenyNegotiation = true;
        Equal(false, ScenarioPermissions.AllowNegotiation(world, 1, 2, e =>
        {
            Equal(allowed.Identity, e.Identity);
            e.Values.DenyNegotiation = false; // Permission was captured before actions.
            world.Events.Collection.Reset(); // First match returns without reading a successor.
        }));
        Equal(false, world.Events.Collection.Contains(denied));
        // The checker itself does not impose caller Loaded, turn, alive or barbarian gates.
        var zero = NegotiationEvent(world, 0, 7);
        world.Events.Loaded = false;
        Equal(false, ScenarioPermissions.AllowNegotiation(world, 0, 7, e => Equal(zero.Identity, e.Identity)));
    }

    private static void NegotiationStoredClassification()
    {
        foreach (int talker in new[] { -2, 1 }) foreach (int listener in new[] { -2, 2 })
            foreach (int talkerType in new[] { 0, 1, 2, 3, 4, 5, -1 })
                foreach (int listenerType in new[] { 0, 1, 2, 3, 4, 5, -1 })
                {
                    var world = ScenarioWorld(); var entry = NegotiationEvent(world, talker, listener, talkerType, listenerType);
                    int visits = 0;
                    bool matches = (talkerType == 4 || talkerType == (talker == 1 ? 1 : 2))
                        && listenerType is 2 or 4;
                    Equal(!matches, ScenarioPermissions.AllowNegotiation(world, 1, 2, _ => ++visits));
                    Equal(matches ? 1 : 0, visits);
                    Equal(talker, entry.Values.WhoCiv); Equal(listener, entry.Values.WhomCiv);
                }
        // An exact non-wildcard stored selector supplies its low-five-bit membership query.
        var alias = ScenarioWorld(); NegotiationEvent(alias, 33, 2, 1, 2);
        Equal(false, ScenarioPermissions.AllowNegotiation(alias, 33, 2, _ => { }));
        var local = ScenarioWorld(); local.Globals.HumanPlayers = local.Globals.HumanPlayers.With(2);
        local.Globals.Multiplayer = GameMode.Hotseat; NegotiationEvent(local, 2, 1, 1, 1);
        Equal(true, ScenarioPermissions.AllowNegotiation(local, 1, 2, _ => throw new Exception("Reversed local meeting")));
        Equal(false, ScenarioPermissions.AllowNegotiation(local, 2, 1, _ => { }));
        local.Globals.Multiplayer = GameMode.Email;
        Equal(true, ScenarioPermissions.AllowNegotiation(local, 1, 2, _ => throw new Exception("Reversed local meeting")));
        local.Globals.Multiplayer = GameMode.Tcp;
        Throws<InvalidOperationException>(() => ScenarioPermissions.AllowNegotiation(local, 1, 2, _ => throw new Exception("Network action")));
    }

    private static void ScenarioActionOrderAndLiveFlags()
    {
        var entry = new EventCollection().Append(); var visits = new List<ScenarioAction>();
        entry.Values.Actions = new()
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
        Equal(1, ScenarioActions.Execute(entry, (kind, e) =>
        {
            Equal(false, e.Values.Actions.Consumed); visits.Add(kind); return 0;
        }));
        Sequence(new[] { ScenarioAction.PlayWaveFile, ScenarioAction.PlayCdTrack, ScenarioAction.CreateUnit,
            ScenarioAction.MoveUnit, ScenarioAction.ChangeTerrain, ScenarioAction.MakeAggression,
            ScenarioAction.ChangeMoney, ScenarioAction.DestroyCivilization, ScenarioAction.GiveTechnology,
            ScenarioAction.Text, ScenarioAction.SuppressWonderMovies }, visits);
        Equal(true, entry.Values.Actions.Consumed);
        Equal(0, ScenarioActions.Execute(entry, (_, _) => throw new Exception("Consumed event")));
        entry.Values.Actions = new() { PlayWaveFile = true, PlayCdTrack = true, JustOnce = true };
        visits.Clear();
        Equal(1, ScenarioActions.Execute(entry, (kind, e) =>
        {
            visits.Add(kind);
            if (kind == ScenarioAction.PlayWaveFile)
            {
                e.Values.Actions.PlayCdTrack = false; e.Values.Actions.Text = true;
                e.Values.Actions.JustOnce = false; e.SetString(Event.TextBegin, new("Live text"));
            }
            else { Equal("Live text", e.Strings[Event.TextBegin].Value); }
            return -1;
        }));
        Sequence(new[] { ScenarioAction.PlayWaveFile, ScenarioAction.Text }, visits);
        Equal(false, entry.Values.Actions.Consumed);
        entry.Values.Actions = new() { Text = true, Consumed = true };
        Equal(1, ScenarioActions.Execute(entry, (_, e) => { e.Values.Actions.JustOnce = true; return 0; }));
        Equal(true, entry.Values.Actions.Consumed);
        entry.Values.Actions = default;
        Equal(1, ScenarioActions.Execute(entry, (_, _) => throw new Exception("Empty action set")));
    }

    private static void ScenarioActionReentryAndFailures()
    {
        var entry = new EventCollection().Append(); entry.Values.Actions = new() { Text = true, JustOnce = true };
        int visits = 0;
        int Run(ScenarioAction action, Event e)
        {
            Equal(ScenarioAction.Text, action); Equal(false, e.Values.Actions.Consumed);
            if (++visits == 1) { Equal(1, ScenarioActions.Execute(e, Run)); }
            return 1;
        }
        Equal(1, ScenarioActions.Execute(entry, Run)); Equal(2, visits); Equal(true, entry.Values.Actions.Consumed);
        var world = ScenarioWorld(); entry = NegotiationEvent(world);
        entry.Values.Actions = new() { ChangeMoney = true, Text = true, JustOnce = true };
        entry.Values.MoneyReceiverCiv = 1; entry.Values.MoneyAmount = 10;
        void Execute(Event e) => ScenarioActions.Execute(e, (kind, current) => kind == ScenarioAction.ChangeMoney
            ? ScenarioActions.ChangeMoney(world, current) : throw new InvalidOperationException("Required authored text failed"));
        Throws<InvalidOperationException>(() => ScenarioPermissions.AllowNegotiation(world, 1, 2, Execute));
        Equal(10, world.Civilizations[1].Gold); Equal(false, entry.Values.Actions.Consumed);
        Equal(true, entry.Values.DenyNegotiation);
    }

    private static void ScenarioMoneyAndConsumedDenial()
    {
        var world = ScenarioWorld(); var entry = NegotiationEvent(world, -2, 2, 2, 2);
        entry.Values.Actions = new() { ChangeMoney = true, JustOnce = true };
        entry.Values.MoneyReceiverCiv = -4; entry.Values.MoneyAmount = 25;
        int calls = 0;
        void Execute(Event e) => ScenarioActions.Execute(e, (kind, current) =>
        { ++calls; Equal(ScenarioAction.ChangeMoney, kind); return ScenarioActions.ChangeMoney(world, current); });
        Equal(false, ScenarioPermissions.AllowNegotiation(world, 1, 2, Execute));
        Equal(false, ScenarioPermissions.AllowNegotiation(world, 1, 2, Execute));
        Equal(1, calls); Equal(25, world.Civilizations[2].Gold); Equal(-2, entry.Values.WhoCiv);
        foreach (var (gold, amount, expected) in new (int, int, int)[]
        {
            (100, -100, 0), (100, -101, 0), (29999, 1, 30000), (29999, 2, 30000),
            (int.MaxValue, 1, 30000), (int.MinValue, -1, 0), (-1, 0, 30000), (50000, -1, 0), (50, 25, 75)
        })
        {
            world.Civilizations[2].Gold = gold; entry.Values.MoneyAmount = amount;
            Equal(1, ScenarioActions.ChangeMoney(world, entry)); Equal(expected, world.Civilizations[2].Gold);
        }
        entry.Values.WhoCiv = -4; entry.Values.MoneyReceiverCiv = -3;
        Equal(-4, ScenarioActions.ResolveCivilization(entry, -3));
        Equal(0, ScenarioActions.ChangeMoney(world, entry)); // No recursive indirection to listener2.
        foreach (int recipient in new[] { -2, -1, 0, 3, 8, 33, int.MaxValue })
        { entry.Values.MoneyReceiverCiv = recipient; Equal(0, ScenarioActions.ChangeMoney(world, entry)); }
        entry.Values.MoneyReceiverCiv = 1; world.Globals.Multiplayer = GameMode.Tcp;
        Throws<InvalidOperationException>(() => ScenarioActions.ChangeMoney(world, entry));
    }
}
