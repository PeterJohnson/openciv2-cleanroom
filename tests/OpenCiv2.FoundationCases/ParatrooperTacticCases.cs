using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static ParatrooperTactics ParaPolicy(ParadropFixture f) => new(f.World, f.Data.Rules, f.Data.Stacks, f.Drop);
    private static UnitTacticsContext ParaContext(ParadropFixture f)
    {
        var c = new UnitTacticsContext(f.World, f.Data.Rules, new(0));
        c.CitySelector = c.NearestCity; // Retained A03 selector supplied for these isolated A07 cases.
        return c;
    }
    private static List<Choice> ParaRolls(bool capture = false)
    {
        var choices = Enumerable.Range(0, 8).Select(i => Choice.Create(new("U05.paradrop-approach", "para/paradrop", i),
            new SamplingLaw.PrimaryModulo(6), ChoiceCode.Primary(0))).ToList();
        if (capture) choices.Add(Choice.Create(new("C09.capture.building-parity", "para/paradrop/capture"), new SamplingLaw.PrimaryModulo(2), ChoiceCode.Primary(0)));
        return choices;
    }
    private static ParadropFixture ParaHome(List<Choice>? choices = null, RevelationFixture? data = null)
    {
        var f = new ParadropFixture(choices, data);
        f.Data.Stacks.Relocate(0, new(4, 8), false); f.Data.AddCity(2, 1, new(4, 8));
        f.World.Civilizations[1].Treaties[2].War = true;
        return f;
    }
    private static void CompletePara(ParadropFixture f, ParatrooperTactics.Operation op)
    {
        for (int i = 0; i < 100 && op.Result is null; ++i) Equal(null, op.Resume());
        Equal(true, op.Result is not null); f.Capture.Life.Random.RequireChoicesConsumed();
    }

    private static void ParatrooperAwayHomeAndFortress()
    {
        var home = new ParadropFixture(); home.Data.Stacks.Relocate(0, new(6, 8), false);
        home.Data.AddCity(2, 1, new(4, 8)); var c = ParaContext(home);
        home.Data.AddCity(3, 1, new(6, 8)); // A later, closer city does not replace captured H/N or the A03 selector.
        using var returning = ParaPolicy(home).Begin(c, "para"); CompletePara(home, returning);
        Equal(SpecialUnitRoute.Finish, returning.Result!.Value.Route); Equal(new MapPoint(4, 8), home.Data.Unit(0).Destination);
        Equal((byte)0x63, home.Data.Unit(0).AiTask); Equal((byte)0, home.Data.Unit(0).MovesUsed);
        var missing = new ParadropFixture(); missing.Data.Stacks.Relocate(0, new(6, 8), false); missing.Data.AddCity(2, 1, new(4, 8));
        var mc = ParaContext(missing); mc.CitySelector = null;
        using var failed = ParaPolicy(missing).Begin(mc, "para"); Throws<InvalidOperationException>(() => failed.Resume());
        Equal(UnitOrder.None, missing.Data.Unit(0).Orders); Throws<InvalidOperationException>(() => failed.Resume());
        foreach (int count in new[] { 1, 2 })
        {
            var f = new ParadropFixture(); f.World.Map.TileAt(new(10, 8)).Features |= MapFeatures.Fortification;
            if (count == 2) f.Data.AddUnit(1, 1, 0, new(10, 8));
            var fc = ParaContext(f); using var op = ParaPolicy(f).Begin(fc, "para"); CompletePara(f, op);
            Equal(count == 1 ? SpecialUnitRoute.Finish : SpecialUnitRoute.Continue, op.Result!.Value.Route);
            Equal(false, fc.Hold); Equal(UnitOrder.None, f.Data.Unit(0).Orders);
            if (count == 1) Equal((byte)0x46, f.Data.Unit(0).AiTask);
        }
        var enemy = new ParadropFixture(); enemy.Data.Stacks.Relocate(0, new(6, 8), false); enemy.Data.AddCity(2, 1, new(4, 8));
        enemy.Data.AddUnit(1, 3, 1, new(7, 7)); enemy.World.Map.TileAt(new(7, 7)).Terrain = Terrain.Ocean;
        using var blocked = ParaPolicy(enemy).Begin(ParaContext(enemy), "para"); CompletePara(enemy, blocked);
        Equal(SpecialUnitRoute.Continue, blocked.Result!.Value.Route); // The adjacent-unit gate crosses media.
    }

    private static void ParatrooperHomeGatesAndCancellation()
    {
        foreach (int variant in Enumerable.Range(0, 5))
        {
            var f = ParaHome();
            if (variant == 0) f.Data.Unit(0).MovesUsed = 1;
            if (variant == 1) f.Data.Unit(0).Orders = UnitOrder.GoTo;
            if (variant == 2) f.World.Civilizations[1].Treaties[2].War = false;
            if (variant == 3) f.Data.Rules.Cosmic = f.Data.Rules.Cosmic with { ParadropRange = 3 };
            if (variant == 4) f.Data.AddUnit(1, 1, 0, new(13, 7)); // Last D0 cancels the already selected center.
            var c = ParaContext(f); using var op = ParaPolicy(f).Begin(c, "para"); CompletePara(f, op);
            Equal(variant == 1 ? SpecialUnitRoute.Continue : SpecialUnitRoute.PlanMove, op.Result!.Value.Route);
            Equal(variant != 1, c.Hold); Equal((MapPoint?)null, op.Destination); Equal(new MapPoint(4, 8), f.Data.Unit(0).Position);
        }
    }

    private static void ParatrooperRetainedTargetScore()
    {
        foreach (bool small in new[] { false, true })
        {
            var f = ParaHome(); f.World.Globals.HumanPlayers = CivilizationSet.Only(2); f.World.Multiplayer.LocalHuman = 2;
            f.Data.Rules.Cosmic = f.Data.Rules.Cosmic with { ParadropRange = 10 };
            f.World.Map.TileAt(new(12, 8)).Terrain = Terrain.Mountains;
            f.Data.Rules.Terrain[(int)Terrain.Mountains] = new() { DefenseFactor = 127 };
            f.Data.AddUnit(1, 1, 0, new(13, 7));
            f.World.Map.TileAt(f.World.Entities.Cities.At(new(1)).Position).Features &= ~MapFeatures.CitySite;
            f.Data.AddCity(1, 2, new(20, 8), small ? -128 : 5); f.World.Map.TileAt(new(20, 8)).Terrain = Terrain.Ocean;
            var c = ParaContext(f); using var op = ParaPolicy(f).Begin(c, "para"); CompletePara(f, op);
            Equal(small ? SpecialUnitRoute.PlanMove : SpecialUnitRoute.ReturnZero, op.Result!.Value.Route);
            Equal(small ? null : new MapPoint(20, 8), op.Destination); Equal(false, op.Landed);
            Equal(small, c.Hold); // A cancelled score of 885 outranks the later signed-size seed 871.
        }
    }

    private static void ParatrooperLandingScoresAndTies()
    {
        foreach (bool fortress in new[] { false, true })
        {
            var f = ParaHome(ParaRolls());
            f.World.Map.TileAt(new(12, 8)).Terrain = Terrain.Ocean;
            f.Data.Rules.Terrain[(int)Terrain.Ocean] = new() { DefenseFactor = 128 }; // signed -128 lowers center below neighbors.
            f.Data.Rules.Terrain[(int)Terrain.Grassland] = new() { DefenseFactor = 50 };
            if (fortress) f.World.Map.TileAt(new(11, 7)).Features |= MapFeatures.Fortification;
            using var op = ParaPolicy(f).Begin(ParaContext(f), "para"); CompletePara(f, op);
            Equal(SpecialUnitRoute.ReturnZero, op.Result!.Value.Route); Equal(true, op.Landed);
            Equal(fortress ? new MapPoint(11, 7) : new(12, 6), op.Destination);
            Equal(op.Destination!.Value, f.Data.Unit(0).Position); Equal(true, f.Data.Unit(0).Flags.Paradropped);
            Equal((byte)0, f.Data.Unit(0).MovesUsed); // No extra A04 common finish after the real drop.
        }
    }

    private static void ParatrooperFirstTargetAndChildRefusal()
    {
        foreach (bool ai in new[] { false, true })
        {
            var f = ParaHome(); f.World.Map.TileAt(new(12, 8)).Terrain = Terrain.Ocean;
            f.World.Map.TileAt(f.World.Entities.Cities.At(new(1)).Position).Features &= ~MapFeatures.CitySite;
            f.Data.AddCity(1, 2, new(14, 10), 100); // A better later city is never reached after a local selection.
            if (ai) { f.World.Globals.HumanPlayers = CivilizationSet.Only(2); f.World.Multiplayer.LocalHuman = 2; }
            using var op = ParaPolicy(f).Begin(ParaContext(f), "para"); CompletePara(f, op);
            Equal(SpecialUnitRoute.ReturnZero, op.Result!.Value.Route); Equal(new MapPoint(12, 8), op.Destination);
            Equal(false, op.Landed); Equal(new MapPoint(4, 8), f.Data.Unit(0).Position); Equal(ai ? 0 : 1, f.Rejections.Count);
        }
    }

    private static void ParatrooperWrappedRange()
    {
        foreach (bool flat in new[] { false, true })
        {
            var f = ParaHome(); f.World.Globals.Rules.FlatWorld = flat;
            f.World.Map.TileAt(new(4, 8)).Features &= ~MapFeatures.CitySite;
            f.Data.Stacks.Relocate(0, new(0, 8), false); f.Data.AddCity(2, 1, new(0, 8));
            f.World.Map.TileAt(new(12, 8)).Features &= ~MapFeatures.CitySite;
            f.Data.AddCity(0, 2, new(36, 8)); f.World.Map.TileAt(new(36, 8)).Terrain = Terrain.Ocean;
            var c = ParaContext(f); using var op = ParaPolicy(f).Begin(c, "para"); CompletePara(f, op);
            Equal(flat ? SpecialUnitRoute.PlanMove : SpecialUnitRoute.ReturnZero, op.Result!.Value.Route);
            Equal(flat ? null : new MapPoint(36, 8), op.Destination); Equal(flat, c.Hold);
        }
    }

    private static void ParatrooperCaptureAndPendingFailure()
    {
        var f = ParaHome(ParaRolls(true)); f.Data.Unit(0).Damage = 4;
        using var op = ParaPolicy(f).Begin(ParaContext(f), "para");
        var pending = op.Resume(); Equal(true, pending is ParadropDecision.Capture { Decision: CityCaptureDecision.Window });
        Equal(pending, op.Resume()); Equal(true, op.Landed); Equal(null, op.Result);
        Equal(new MapPoint(12, 8), f.Data.Unit(0).Position); Equal(new CivilizationSlot(1), f.Capture.City.Owner);
        op.Resume(new(0)); CompletePara(f, op); Equal(SpecialUnitRoute.ReturnZero, op.Result!.Value.Route);
        Equal((byte)0, f.Data.Unit(0).MovesUsed); Equal((byte)0, f.Data.Unit(0).Damage);
        var ai = ParaHome(ParaRolls(true)); ai.World.Globals.HumanPlayers = CivilizationSet.Only(2); ai.World.Multiplayer.LocalHuman = 2;
        ai.Capture.City.Size = 1; ai.Data.Unit(0).Damage = 4;
        using var razed = ParaPolicy(ai).Begin(ParaContext(ai), "para"); CompletePara(ai, razed);
        Equal(SpecialUnitRoute.ReturnZero, razed.Result!.Value.Route); Equal(true, razed.Landed); Equal(false, ai.Capture.City.Token.Occupied);
        Equal((byte)3, ai.Data.Unit(0).MovesUsed); Equal((byte)0, ai.Data.Unit(0).Damage);
        Equal(3, UnitMovement.Left(ai.World, ai.Data.Rules, ai.Data.Unit(0))); // Healing leaves moves; the parent must not finish/exhaust again.
        var missing = ParaHome(); using var failed = ParaPolicy(missing).Begin(ParaContext(missing), "para");
        Throws<ChoiceException>(() => failed.Resume()); Equal(new MapPoint(12, 8), failed.Destination);
        Equal(new MapPoint(4, 8), missing.Data.Unit(0).Position); Throws<InvalidOperationException>(() => failed.Resume());
        var disposed = ParaHome(ParaRolls(true)); var held = ParaPolicy(disposed).Begin(ParaContext(disposed), "para"); held.Resume(); held.Dispose();
        Throws<ObjectDisposedException>(() => held.Resume(new(0))); Equal(new CivilizationSlot(1), disposed.Capture.City.Owner);
        var remote = ParaHome(); var c = ParaContext(remote); remote.World.Globals.Multiplayer = GameMode.Tcp;
        using var denied = ParaPolicy(remote).Begin(c, "para"); Throws<InvalidOperationException>(() => denied.Resume()); Equal(false, c.Hold);
        var treaty = ParaHome(); treaty.World.Civilizations[1].Treaties[2].Peace = true;
        treaty.World.Globals.HumanPlayers = CivilizationSet.Only(2); treaty.World.Multiplayer.LocalHuman = 2;
        using var refused = ParaPolicy(treaty).Begin(ParaContext(treaty), "para");
        CompletePara(treaty, refused); Equal(SpecialUnitRoute.ReturnZero, refused.Result!.Value.Route);
        Equal((byte)6, treaty.Data.Unit(0).MovesUsed);
        Equal(false, refused.Landed); Equal(new MapPoint(4, 8), treaty.Data.Unit(0).Position);
        var late = ParaHome(ParaRolls()); late.World.Map.TileAt(new(12, 8)).Terrain = Terrain.Ocean;
        late.Data.Rules.Terrain[(int)Terrain.Ocean] = new() { DefenseFactor = 128 };
        late.Data.Rules.Terrain[(int)Terrain.Grassland] = new() { DefenseFactor = 50 }; late.Failure = "city:0";
        using var landed = ParaPolicy(late).Begin(ParaContext(late), "para"); Throws<InvalidOperationException>(() => landed.Resume());
        Equal(true, landed.Landed); Equal(new MapPoint(12, 6), late.Data.Unit(0).Position); Equal(null, landed.Result);
        Throws<InvalidOperationException>(() => landed.Resume()); late.Capture.Life.Random.RequireChoicesConsumed();
    }

    private static void ParatrooperPrivateCaptureSession()
    {
        foreach (bool cancel in new[] { false, true })
        {
            using var runtime = new SessionRuntime(new(), new(651));
            var f = ParaHome(ParaRolls(true), CreationFixture(new(runtime.Process, runtime.World)));
            ParticipantId actorSeat = default, otherSeat = default; int windows = 0;
            runtime.RegisterCommand(new(new(1, "test.para", "Proactive paratrooper", AccessClass.Player, new([]), new([new("route"), new("landed", ValueKind.Boolean)])),
                (_, _, _) => new(), _ => new ParatrooperTacticsOperation(ParaPolicy(f).Begin(ParaContext(f), "para"),
                    civ => civ == 1 ? actorSeat : otherSeat, visit =>
                    { ++windows; return new("Captured city", null, new CommandValue.City(visit.City), new([0])); })));
            runtime.BeginClientSession(new()); var actor = SessionClient(runtime); var other = SessionClient(runtime, 2);
            actorSeat = actor.Identity.Participant; otherSeat = other.Identity.Participant;
            var command = actor.Submit(new("test.para", new([]))).Correlation.Command; runtime.AdvanceOne();
            var prompt = RequiredInteraction(actor, command); Equal(null, other.Result(command)); Equal(false, other.Reply(prompt, 0).Accepted);
            Equal(new MapPoint(12, 8), f.Data.Unit(0).Position); Equal(new CivilizationSlot(1), f.Capture.City.Owner);
            if (cancel) Equal(true, actor.Cancel(command).Accepted); else Equal(true, actor.Reply(prompt, 0).Accepted);
            runtime.AdvanceOne(); var result = RequiredResult(actor, command);
            if (result.Status != CommandStatus.Completed) throw new InvalidOperationException($"Paratrooper session ({cancel}): {result.Status}, {result.Code}");
            Equal(1, windows); Equal((byte)0, f.Data.Unit(0).MovesUsed); f.Capture.Life.Random.RequireChoicesConsumed();
        }
    }
}
