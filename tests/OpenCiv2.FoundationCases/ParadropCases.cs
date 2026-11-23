using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
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
    private static readonly MapPoint DropTarget = new(18, 8);
    private static List<Choice> DropRolls(params int[] indices) => (indices.Length == 0 ? Enumerable.Range(0, 8) : indices)
        .Select(i => Choice.Create(new("U05.paradrop-approach", "drop", i), new SamplingLaw.PrimaryModulo(6), ChoiceCode.Primary(0))).ToList();
    private sealed class ParadropFixture : IParadropEffects
    {
        public CityCaptureFixture Capture { get; }
        public RevelationFixture Data => Capture.Data;
        public GameState World => Data.World;
        public Paradrop Drop { get; }
        public List<string> Effects { get; } = [];
        public List<ParadropRejection> Rejections { get; } = [];
        public List<ParadropAnnouncement> Announcements { get; } = [];
        public List<AttackNotice> AttackNotices { get; } = [];
        public List<(MapPoint From, MapPoint To, int Direction)> Animations { get; } = [];
        public Action? OnChanged;
        public string? Failure;
        public ParadropFixture(List<Choice>? choices = null)
        {
            Capture = new(choices ?? []); Data.Unit(0).Type = new(0);
            Data.Rules.Units[0] = Data.Rules.Units[0] with { Role = RuleRole.Attack, Identity = new("jumper", "Jumper") };
            Data.Rules.Cosmic = Data.Rules.Cosmic with { ParadropRange = 6 };
            World.Globals.ActiveUnit = new(0);
            var confirmation = new AttackConfirmation(World, Data.Rules, AttackNotices.Add);
            Drop = new(World, Data.Rules, Data.Stacks, new(World, Data.Rules, Data.Stacks), new(World, Data.Rules, Data.Stacks, Data),
                Capture.Capture, confirmation, Capture.Life.Random, this);
        }
        private void Effect(string name) { Effects.Add(name); if (Failure == name) throw new InvalidOperationException("Unavailable " + name); }
        public Paradrop.Operation Begin(MapPoint? target = null) => Drop.Begin(new(0), target ?? DropTarget, "drop");
        public void Complete(Paradrop.Operation operation)
        {
            for (int i = 0; i < 50 && !operation.Completed; ++i) Equal(null, operation.Resume());
            Equal(true, operation.Completed); Capture.Life.Random.RequireChoicesConsumed();
        }
        public void Rejected(UnitSlot unit, ParadropRejection reason, int range) { Equal(6, range); Rejections.Add(reason); Effect("reject"); }
        public void EnterTargeting(UnitSlot unit) => Effect("targeting");
        public void EnsureVisible(MapPoint target, int actor) => Effect("visible");
        public void Announce(ParadropAnnouncement announcement) { Announcements.Add(announcement); Effect("announce"); }
        public void Redraw(MapPoint origin) => Effect("redraw");
        public void Animate(UnitSlot unit, MapPoint approach, MapPoint target, int direction)
        { Animations.Add((approach, target, direction)); Equal(UnitStaging.Lifted(Data.Unit(unit.Value).Owner.Value), Data.Unit(unit.Value).Position); Effect("animate"); }
        public void MoveCursor(MapPoint target) { Equal(UnitStaging.Lifted(1), Data.Unit(0).Position); Effect("cursor"); }
        public void CityUnitChanged(UnitSlot unit, MapPoint origin) { Effect("city:" + unit.Value); OnChanged?.Invoke(); }
        public void RedrawStatus() => Effect("status");
    }

    // U05 entry is distinct from target execution and P05 menu availability.
    private static void ParadropCommandAndTargetAdmission()
    {
        foreach (int variant in Enumerable.Range(0, 5))
        {
            var f = new ParadropFixture(); f.Data.Unit(0).Type = new(1); f.World.Map.TileAt(new(10, 8)).Features |= MapFeatures.CitySite | MapFeatures.Fortification;
            if (variant == 0) f.Data.Unit(0).MovesUsed = 1;
            if (variant == 1) f.Data.Unit(0).Flags.Paradropped = true;
            if (variant == 2) f.World.Map.TileAt(new(10, 8)).Features &= ~MapFeatures.CitySite;
            if (variant == 4) f.World.Globals.ActiveUnit = null;
            if (variant == 4) { Throws<InvalidOperationException>(() => f.Drop.Enter()); continue; }
            Equal(variant == 3, f.Drop.Enter()); Equal(variant == 3 ? "targeting" : "reject", f.Effects.Single());
            if (variant < 3) Equal(variant < 2 ? ParadropRejection.AlreadyMoved : ParadropRejection.RequiresCityOrAirbase, f.Rejections.Single());
            f.Capture.Life.Random.RequireChoicesConsumed();
        }
        foreach (bool human in new[] { false, true })
            foreach (int variant in Enumerable.Range(0, 3))
            {
                var f = new ParadropFixture(); f.World.Globals.HumanPlayers = human ? CivilizationSet.Only(1) : default;
                f.World.Multiplayer.Saved.AutoplayActive = true;
                if (variant == 0) { f.World.Map.TileAt(DropTarget).Terrain = Terrain.Ocean; f.Data.AddUnit(1, 2, 0, DropTarget); }
                if (variant == 1) f.Data.AddUnit(1, 2, 0, DropTarget);
                using var op = f.Begin(variant == 2 ? new(38, 20) : null); f.Complete(op);
                Equal(false, op.Landed); Equal(new MapPoint(10, 8), f.Data.Unit(0).Position); Equal((byte)0, f.Data.Unit(0).MovesUsed);
                Equal(human ? 1 : 0, f.Rejections.Count);
                if (human) Equal(new[] { ParadropRejection.Ocean, ParadropRejection.ForeignUnit, ParadropRejection.Range }[variant], f.Rejections.Single());
            }
        var direct = new ParadropFixture(DropRolls()); direct.Data.Unit(0).MovesUsed = 4; direct.Data.Unit(0).Flags.Paradropped = true;
        using var target = direct.Begin(); direct.Complete(target); Equal(true, target.Landed); Equal((byte)4, direct.Data.Unit(0).MovesUsed);
    }

    private static void ParadropTreatyAndSenateCancellation()
    {
        foreach (int variant in Enumerable.Range(0, 4))
        {
            var f = new ParadropFixture(); f.World.Civilizations[1].Treaties[2].Peace = true;
            if (variant == 0) f.World.Globals.HumanPlayers = default;
            if (variant == 1) f.World.Civilizations[1].Treaties[2].Alliance = true;
            if (variant == 3) f.World.Civilizations[1].Government = Government.Democracy;
            using var op = f.Begin(new(12, 8)); var pending = op.Resume();
            if (variant >= 2)
            { Equal(true, pending is ParadropDecision.Attack); Equal(pending, op.Resume()); op.Resume(new(variant == 2 ? 0 : 1)); }
            else Equal(null, pending);
            f.Complete(op); Equal(false, op.Landed); Equal((byte)(variant == 0 ? 6 : 0), f.Data.Unit(0).MovesUsed);
            Equal(true, f.World.Civilizations[1].Treaties[2].Peace); Equal(false, f.World.Civilizations[1].Treaties[2].War);
            Equal(variant is 1 or 3 ? 1 : 0, f.AttackNotices.Count);
        }
    }

    private static void ParadropVisibilityApproachAndRepairs()
    {
        foreach (int observer in new[] { -1, 0, 1, 2 })
        {
            var f = new ParadropFixture(DropRolls()); f.World.Multiplayer.LocalHuman = observer; f.World.Globals.HumanPlayers = CivilizationSet.Only(1).With(2);
            using var op = f.Begin(); f.Complete(op); Equal(true, op.Landed); Equal(observer == 1, f.Effects.Contains("visible"));
            Equal(observer == 1 ? 1 : 0, f.Animations.Count); Equal(0, f.Announcements.Count);
            if (observer == 1) Equal((new MapPoint(19, 7), DropTarget, 4), f.Animations.Single());
        }
        var occupied = new ParadropFixture(DropRolls()); occupied.Data.AddUnit(1, 2, 0, new(19, 7));
        using var alternate = occupied.Begin(); occupied.Complete(alternate); Equal((new MapPoint(19, 9), DropTarget, 6), occupied.Animations.Single());
        foreach (bool show in new[] { false, true })
        {
            var ai = new ParadropFixture(DropRolls()); ai.Data.Unit(0).Owner = new(2); ai.World.Globals.RevealMap = 1; ai.World.Globals.GameOptions.EnemyMoves = show;
            ai.World.Entities.Cities.SetEnd(0); using var op = ai.Begin(); ai.Complete(op);
            Equal(1, ai.Announcements.Count); Equal((string?)null, ai.Announcements.Single().CityName); Equal(DropTarget, ai.Announcements.Single().Target);
            Equal(show ? 1 : 0, ai.Animations.Count); Equal((byte)0, ai.Data.Unit(0).MovesUsed);
        }
        var tiny = new ParadropFixture(DropRolls(1)); tiny.World.Map.Allocate(new(4, 1)); tiny.World.Entities.Cities.SetEnd(0);
        foreach (ref var tile in tiny.World.Map.Tiles) tile.Terrain = Terrain.Grassland;
        tiny.Data.AddUnit(0, 1, 0, new(0, 0)); tiny.Data.AddUnit(1, 1, 0, new(2, 0));
        using var noApproach = tiny.Begin(new(0, 0)); tiny.Complete(noApproach); Equal(true, noApproach.Landed); Equal(0, tiny.Animations.Count);
        foreach (bool flat in new[] { false, true })
        {
            var edge = new ParadropFixture(flat ? DropRolls(0, 1, 2, 3, 7) : DropRolls()); edge.World.Globals.Rules.FlatWorld = flat;
            using var op = edge.Begin(new(0, 8)); edge.Complete(op); Equal(new MapPoint(0, 8), edge.Data.Unit(0).Position);
            Equal((new MapPoint(1, 7), new MapPoint(0, 8), 4), edge.Animations.Single());
        }
    }

    private static void ParadropCargoAndRevelation()
    {
        var f = new ParadropFixture(DropRolls()); f.Data.Unit(0).Type = new(1); f.Data.Rules.Units[1] = f.Data.Rules.Units[1] with { Capacity = 1 };
        f.World.Map.TileAt(new(10, 8)).Terrain = Terrain.Ocean;
        f.Data.AddUnit(1, 1, 0, new(10, 8)); f.Data.Unit(1).Orders = UnitOrder.Sleeping; f.Data.Unit(1).Carrier = new(0);
        f.Data.Unit(0).Orders = UnitOrder.GoTo; f.Data.Unit(0).Flags.Garrison = true;
        f.Data.Unit(0).Damage = 2; f.Data.Unit(0).Counter = 7; f.Data.Unit(0).Visibility = CivilizationSet.Only(3);
        f.Data.Unit(1).Visibility = CivilizationSet.Only(4); f.Data.Unit(1).Flags.Garrison = true;
        f.OnChanged = () => { Equal(true, f.Data.Unit(0).Flags.Garrison); Equal(true, f.World.Map.TileAt(DropTarget).RevealedTo.Contains(1)); };
        using var op = f.Begin(); f.Complete(op);
        Equal(DropTarget, f.Data.Unit(0).Position); Equal(DropTarget, f.Data.Unit(1).Position); Equal(2, f.Data.Stacks.Count(0));
        Equal(default(CivilizationSet), f.Data.Unit(0).Visibility); Equal(default(CivilizationSet), f.Data.Unit(1).Visibility);
        Equal(UnitOrder.None, f.Data.Unit(0).Orders); Equal(UnitOrder.Sleeping, f.Data.Unit(1).Orders); Equal(new UnitSlot(0), f.Data.Unit(1).Carrier);
        Equal(true, f.Data.Unit(0).Flags.Paradropped); Equal(false, f.Data.Unit(1).Flags.Paradropped);
        Equal(false, f.Data.Unit(0).Flags.Garrison); Equal(true, f.Data.Unit(1).Flags.Garrison);
        Equal((byte)2, f.Data.Unit(0).Damage); Equal((byte)7, f.Data.Unit(0).Counter); Equal((byte)0, f.Data.Unit(0).MovesUsed); Equal(false, f.Data.Unit(0).Flags.Moved);
        Sequence(new[] { "visible", "redraw", "animate", "cursor", "city:0", "status" }, f.Effects);
    }

    private static Choice DropCaptureParity() => Choice.Create(new("C09.capture.building-parity", "drop/capture"), new SamplingLaw.PrimaryModulo(2), ChoiceCode.Primary(0));
    private static void ParadropCaptureAndSession()
    {
        var f = new ParadropFixture([.. DropRolls(), DropCaptureParity()]); f.World.Events.Loaded = true;
        f.Data.Unit(0).Damage = 4; f.Data.AddUnit(1, 1, 0, new(20, 10)); f.Data.Unit(1).Flags.Garrison = true;
        f.Capture.OnTaken = (_, n, _) =>
        {
            Equal(1, n); Equal(new MapPoint(12, 8), f.Data.Unit(0).Position); Equal((byte)0, f.Data.Unit(0).Damage);
            Equal(true, f.Data.Unit(0).Flags.Paradropped); f.World.Globals.MovingUnit = new(1);
        };
        using var op = f.Begin(new(12, 8)); var choice = op.Resume(); Equal(true, choice is ParadropDecision.Capture { Decision: CityCaptureDecision.Window });
        Equal(true, op.Landed); Equal(false, op.Completed); Equal(false, f.Effects.Contains("city:1"));
        op.Resume(new(0)); f.Complete(op); Equal(new CivilizationSlot(1), f.Capture.City.Owner); Equal((sbyte)4, f.Capture.City.Size);
        Equal(false, f.Data.Unit(1).Flags.Garrison); Equal(true, f.Effects.Contains("city:1")); Equal(true, f.World.Map.TileAt(new(20, 10)).RevealedTo.Contains(1));
        Equal((byte)0, f.Data.Unit(0).MovesUsed);

        var sessionFixture = new ParadropFixture([.. DropRolls(), DropCaptureParity()]); sessionFixture.World.Civilizations[1].Treaties[2].Peace = true;
        var context = new OperationContext(sessionFixture.World, sessionFixture.Data.Process, sessionFixture.Capture.Life.Random, new PresentationRandom(1, "drop"),
            new(default, new(3)), default, (_, _) => { }, () => { });
        int windows = 0;
        using var session = new ParadropOperation(sessionFixture.Begin(new(12, 8)), c => new((ulong)c), _ => { ++windows; return new("Captured city", null, null, new([0])); });
        var warning = session.Resume(context, new()); Equal(CommandStatus.AwaitingInteraction, warning.Status); Equal(new ParticipantId(1), warning.Interaction!.Recipient);
        var window = session.Resume(context, new(OperationInputKind.Choice, 1)); Equal(CommandStatus.AwaitingInteraction, window.Status); Equal("Captured city", window.Interaction!.Prompt);
        Equal(new ParticipantId(1), window.Interaction.Recipient); Equal(1, windows);
        var done = session.Resume(context, new(OperationInputKind.Cancel)); Equal(CommandStatus.Completed, done.Status); Equal(new CommandValue.Boolean(true), done.Result!.Single().Value);
        sessionFixture.Capture.Life.Random.RequireChoicesConsumed();

        var tileOwner = new ParadropFixture(DropRolls()); tileOwner.World.Map.TileAt(new(12, 8)).Owner = 1; tileOwner.Data.Unit(0).Damage = 4;
        using var noCapture = tileOwner.Begin(new(12, 8)); tileOwner.Complete(noCapture);
        Equal(new CivilizationSlot(2), tileOwner.Capture.City.Owner); Equal((byte)4, tileOwner.Data.Unit(0).Damage); Equal((UnitSlot?)null, tileOwner.World.Globals.MovingUnit);

        var ai = new ParadropFixture([.. DropRolls(), DropCaptureParity()]); ai.World.Globals.HumanPlayers = CivilizationSet.Only(2);
        ai.World.Multiplayer.LocalHuman = 2; ai.Capture.City.Size = 1; ai.Data.Unit(0).Damage = 4;
        using var destroyed = ai.Begin(new(12, 8)); ai.Complete(destroyed);
        // Exhaustion reads the damaged allowance before damage is cleared: healing then leaves movement.
        Equal(true, destroyed.Landed); Equal(false, ai.Capture.City.Token.Occupied); Equal((byte)3, ai.Data.Unit(0).MovesUsed);
        Equal(3, UnitMovement.Left(ai.World, ai.Data.Rules, ai.Data.Unit(0)));
        Equal((byte)0, ai.Data.Unit(0).Damage); Equal(true, ai.Data.Unit(0).Flags.Paradropped); Equal(false, ai.Data.Unit(0).Flags.Moved);
    }

    private static void ParadropFailurePrefixes()
    {
        foreach (string stage in new[] { "visible", "animate", "city:0", "status" })
        {
            var f = new ParadropFixture(stage == "visible" ? [] : DropRolls()) { Failure = stage };
            f.Data.Unit(0).Orders = UnitOrder.GoTo; f.Data.Unit(0).Flags.Garrison = true;
            using var op = f.Begin(); Throws<InvalidOperationException>(() => op.Resume()); Throws<InvalidOperationException>(() => op.Resume());
            Equal(stage is "city:0" or "status" ? DropTarget : stage == "animate" ? UnitStaging.Lifted(1) : new MapPoint(10, 8), f.Data.Unit(0).Position);
            Equal(stage is "city:0" or "status", f.Data.Unit(0).Flags.Paradropped); Equal(stage != "status", f.Data.Unit(0).Flags.Garrison);
            Equal(stage == "visible" ? UnitOrder.GoTo : UnitOrder.None, f.Data.Unit(0).Orders); f.Capture.Life.Random.RequireChoicesConsumed();
        }
        var disposed = new ParadropFixture(); disposed.World.Civilizations[1].Treaties[2].Peace = true;
        var pending = disposed.Begin(new(12, 8)); pending.Resume(); pending.Dispose(); Throws<ObjectDisposedException>(() => pending.Resume(new(1)));
        Equal(new MapPoint(10, 8), disposed.Data.Unit(0).Position);
        var missing = new ParadropFixture([.. DropRolls(), DropCaptureParity()]); missing.World.Events.Loaded = true;
        missing.Capture.OnTaken = (_, _, _) => missing.World.Globals.MovingUnit = null;
        using var lostActor = missing.Begin(new(12, 8)); Equal(true, lostActor.Resume() is ParadropDecision.Capture);
        Throws<InvalidOperationException>(() => lostActor.Resume(new(0))); Throws<InvalidOperationException>(() => lostActor.Resume());
        Equal(new CivilizationSlot(1), missing.Capture.City.Owner); Equal(true, lostActor.Landed); Equal(false, missing.Effects.Contains("city:0"));
        missing.Capture.Life.Random.RequireChoicesConsumed();
        var remote = new ParadropFixture(); remote.World.Globals.Multiplayer = GameMode.Tcp;
        using var rejected = remote.Begin(); Throws<InvalidOperationException>(() => rejected.Resume()); Equal(0, remote.Effects.Count);
    }
}
