using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.State;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static IPlayerClient SessionClient(SessionRuntime runtime, byte civ = 1) => runtime.Bind(runtime.AddParticipant(new(civ)), ControllerKind.Scripted);
    private static void CheckRevoked(IPlayerClient client, AccessFailure failure)
    {
        foreach (var action in new Action[] { () => _ = client.Identity, () => client.OwnUnits(), () => client.OwnCities(), () => client.Commands(),
            () => client.Submit(new("test", new([]))), () => client.Updates(), () => client.Result(new(1)), () => client.Reply(new(1), 1), () => client.Cancel(new(1)) })
        {
            try { action(); throw new Exception("Revoked client gained access"); }
            catch (AccessException error) { Equal((int)failure, (int)error.Failure); }
        }
    }
    private static void SessionPrivateBindings()
    {
        using var runtime = new SessionRuntime(new(), new(7)); var session = runtime.BeginClientSession(new(11, 22));
        runtime.World.Entities.Units.SetEnd(2); runtime.World.Entities.Units.Storage[0] = new() { Token = new(100), Owner = new(1) }; runtime.World.Entities.Units.Storage[1] = new() { Token = new(101), Owner = new(2) };
        runtime.World.Globals.HumanPlayers = 2; var participant = runtime.AddParticipant(new(1), new(9));
        var frontend = runtime.Bind(participant, ControllerKind.Frontend); var automation = runtime.Bind(participant, ControllerKind.Automation);
        var developer = runtime.Bind(participant, ControllerKind.EnginePolicy, AccessClass.Developer);
        Equal(frontend.Identity.Participant, automation.Identity.Participant); Equal(false, frontend.Identity.Controller == automation.Identity.Controller);
        Equal(frontend.OwnUnits(), automation.OwnUnits()); Equal(frontend.OwnUnits(), developer.OwnUnits()); Equal(1, frontend.OwnUnits().Records.Count);
        var forged = frontend.Identity with { Observer = new(2), Access = AccessClass.Developer }; Equal(new CivilizationSlot(1), frontend.Identity.Observer); Equal((int)AccessClass.Player, (int)frontend.Identity.Access); Equal(false, forged == frontend.Identity);
        var old = frontend.OwnUnits(); runtime.World.Entities.Units.Storage[0].Damage = 9; runtime.Publish();
        Equal((byte)0, old.Records[0].Damage); Equal(new LiveObservation(new(7), session, 1, new(1)), frontend.OwnUnits().Observation);
        var alias = frontend; runtime.Revoke(frontend.Identity.Controller); CheckRevoked(alias, AccessFailure.Revoked); Equal(1, automation.OwnUnits().Records.Count);
        Equal((byte)2, runtime.World.Globals.HumanPlayers); Equal(new Participant(participant, new(1), new(9)), runtime.Participant(participant));
    }
    private static void SessionReassignmentAndLifetimes()
    {
        using var runtime = new SessionRuntime(new(), new(1)); var first = runtime.BeginClientSession(new()); var seat = runtime.AddParticipant(new(1));
        var old = runtime.Bind(seat, ControllerKind.Frontend); var oldAutomation = runtime.Bind(seat, ControllerKind.Automation); var unrelated = SessionClient(runtime, 2);
        runtime.ReassignParticipant(seat, new(2), new(17)); CheckRevoked(old, AccessFailure.Revoked); CheckRevoked(oldAutomation, AccessFailure.Revoked); Equal(new CivilizationSlot(2), unrelated.Identity.Observer);
        var rebound = runtime.Bind(seat, ControllerKind.Frontend); Equal(new CivilizationSlot(2), rebound.Identity.Observer); runtime.RemoveParticipant(seat); CheckRevoked(rebound, AccessFailure.Revoked);
        Throws<KeyNotFoundException>(() => runtime.Bind(seat, ControllerKind.Frontend));
        var second = runtime.BeginClientSession(new()); Equal(false, first == second); CheckRevoked(unrelated, AccessFailure.Revoked); Throws<KeyNotFoundException>(() => runtime.Participant(seat));
        var current = SessionClient(runtime); Equal(second, current.Identity.Session); runtime.EndClientSession(); CheckRevoked(current, AccessFailure.SessionEnded); Equal(false, runtime.Active);
        Throws<AccessException>(() => runtime.AddParticipant(new(1))); Throws<AccessException>(() => _ = runtime.GameplayRandom);
        runtime.BeginClientSession(new()); CheckRevoked(current, AccessFailure.Revoked);
    }
    private static void SessionDisconnectAndDisposal()
    {
        var runtime = new SessionRuntime(new(), new(1)); runtime.BeginClientSession(new()); var first = runtime.AddParticipant(new(1), new(9)); var second = runtime.AddParticipant(new(2), new(10)); var third = runtime.AddParticipant(new(3), new(9));
        var a = runtime.Bind(first, ControllerKind.Frontend); var b = runtime.Bind(second, ControllerKind.Scripted); var c = runtime.Bind(third, ControllerKind.Scripted);
        runtime.World.Civilizations[1].Gold = 123; runtime.Disconnect(new(9)); CheckRevoked(a, AccessFailure.Revoked); CheckRevoked(c, AccessFailure.Revoked);
        Equal(true, runtime.Participant(first).Connection is null); Equal(true, runtime.Participant(third).Connection is null); Equal(new ConnectionId(10), runtime.Participant(second).Connection!.Value); Equal(123, runtime.World.Civilizations[1].Gold);
        runtime.Disconnect(new(9)); Equal(second, b.Identity.Participant); runtime.Dispose(); CheckRevoked(b, AccessFailure.SessionEnded); runtime.Dispose();
        Throws<ObjectDisposedException>(() => runtime.BeginClientSession(new())); Throws<ObjectDisposedException>(() => _ = runtime.World);
    }
    private static void SessionRetainedOwnership()
    {
        var process = new ProcessContext(); using var runtime = new SessionRuntime(process, new(1)); using var peer = new SessionRuntime(new(), new(2));
        runtime.BeginClientSession(new(91, 92)); peer.BeginClientSession(new(91, 92));
        var world = runtime.World; var rules = process.Rules; process.Tokens.AssignUnit(); process.Tokens.AssignCity(); rules.Text.Initialize(); var label = rules.Text.Store("Retained rule"u8);
        rules.Names.Units[3] = label; rules.Leaders[4].CityNameIndex = 73; rules.Definitions.Units[3] = rules.Definitions.Units[3] with { Attack = 91 }; process.Fallback.Value.Fertility = 99;
        world.Map.Allocate(new(4, 2)); world.Map.Tiles[0].Fertility = 42; world.Entities.Units.SetEnd(5);
        var a = SessionClient(runtime); var b = SessionClient(peer); Equal(false, a.OwnUnits().Observation.Runtime == b.OwnUnits().Observation.Runtime); Equal(0, b.OwnUnits().Records.Count);
        Equal((byte)0, peer.Process.Rules.Leaders[4].CityNameIndex); Equal(0, peer.Process.Rules.Text.Count); Equal((byte)0, peer.Process.Fallback.Value.Fertility);
        runtime.BeginClientSession(new(93, 94)); Equal(true, ReferenceEquals(world, runtime.World)); Equal(true, ReferenceEquals(rules, runtime.Process.Rules));
        Equal(5, world.Entities.Units.End); Equal((byte)42, world.Map.Tiles[0].Fertility); Equal((byte)73, rules.Leaders[4].CityNameIndex); Equal(new OwnedList<byte>("Retained rule"u8.ToArray()), rules.Text.Text(label));
        Equal(new UnitToken(2), process.Tokens.AssignUnit()); Equal(new CityToken(2), process.Tokens.AssignCity());
    }
    private static void SessionRandomDomains()
    {
        using var viewed = new SessionRuntime(new(), new(1)); using var headless = new SessionRuntime(new(), new(2)); viewed.BeginClientSession(new(101, 202)); headless.BeginClientSession(new(101, 202));
        var client = SessionClient(viewed);
        for (int i = 0; i < 100; ++i)
        {
            client.OwnUnits(); client.OwnCities(); viewed.PresentationRandom.PrimaryModulo("test.content", 5);
            Equal(headless.GameplayRandom.SecondaryRange("test.gameplay", -10, 20), viewed.GameplayRandom.SecondaryRange("test.gameplay", -10, 20));
        }
        Equal(headless.GameplayRandom.Capture(), viewed.GameplayRandom.Capture()); Equal(false, headless.PresentationRandom.Capture().Equals(viewed.PresentationRandom.Capture()));
        var before = viewed.GameplayRandom.Capture(); viewed.Revoke(client.Identity.Controller); SessionClient(viewed); Equal(before, viewed.GameplayRandom.Capture());
    }
    private static void SessionGrantAdmission()
    {
        Throws<ArgumentException>(() => new SessionRuntime(new(), new(0))); using var runtime = new SessionRuntime(new(), new(1));
        Throws<AccessException>(() => runtime.Bind(new(1), ControllerKind.Frontend)); runtime.BeginClientSession(new()); Throws<ArgumentException>(() => runtime.AddParticipant(new(8)));
        var seat = runtime.AddParticipant(new(1)); var client = runtime.Bind(seat, ControllerKind.Frontend);
        Throws<ArgumentException>(() => runtime.ReassignParticipant(seat, new(255))); Equal(new CivilizationSlot(1), client.Identity.Observer);
        Throws<ArgumentException>(() => runtime.Bind(seat, (ControllerKind)99)); Throws<ArgumentException>(() => runtime.Bind(seat, ControllerKind.Frontend, (AccessClass)99));
        Throws<KeyNotFoundException>(() => runtime.Bind(new(999), ControllerKind.Frontend)); runtime.Revoke(new(999)); Equal(seat, client.Identity.Participant);
    }
    private static void SessionWorldPins()
    {
        using var runtime = new SessionRuntime(new(), new(1)); using (var pin = runtime.PinWorld())
        {
            Throws<InvalidOperationException>(() => runtime.BeginClientSession(new())); Throws<AccessException>(() => runtime.BeginActivity());
        }
        runtime.BeginClientSession(new()); var client = SessionClient(runtime); var activity = runtime.BeginActivity();
        Throws<InvalidOperationException>(() => runtime.BeginActivity()); Throws<InvalidOperationException>(() => runtime.EndClientSession());
        using (var nested = runtime.PinWorld()) { runtime.Publish(); Equal(1UL, client.OwnUnits().Observation.Revision); }
        activity.Dispose(); activity.Dispose(); runtime.EndClientSession(); Equal(false, runtime.Active);
    }
}
