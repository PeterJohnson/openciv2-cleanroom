using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private sealed class RoundEffects : ICombatRoundEffects
    {
        public List<string> Calls { get; } = [];
        public string? Fail { get; set; }
        private void Emit(string value) { Calls.Add(value); if (value == Fail) throw new InvalidOperationException("presentation failed"); }
        public void Publish() => Emit("publish");
        public void Explosion(MapPoint point, int kind) => Emit($"explode:{point.X},{point.Y}:{kind}");
        public void Redraw(MapPoint origin, MapPoint target) => Emit("redraw");
    }
    private static Choice CombatChoice(string site, int strength, int code, int round = 0) =>
        Choice.Create(new(site, "fight", round), new SamplingLaw.PrimaryModulo(strength), ChoiceCode.Primary(code));
    private static GameplayRandom CombatRandom(params Choice[] choices) => new(new ControlledChoices(new(choices)), "combat/test");
    private static CombatRounds RoundService(CombatFixture f, GameplayRandom random, RoundEffects effects) => new(f.World, f.Data.Rules, random, f.State, effects);

    // U06 rollBelow: signed decrement gate, primary law and no-draw cases.
    private static void CombatSamplingBoundaries()
    {
        var random = CombatRandom(CombatChoice("small", 2, 32767), CombatChoice("large", int.MaxValue, 32767),
            Choice.Create(new("minimum", "fight"), new SamplingLaw.PrimaryCode(), ChoiceCode.Primary(32767)));
        foreach (int strength in new[] { -100, -1, 0, 1, int.MinValue + 1 })
            Equal(0, CombatSampling.RollBelow(random, new("absent", "fight"), strength));
        Equal(0, random.Capture().Trace!.Events.Count);
        Equal(1, CombatSampling.RollBelow(random, new("small", "fight"), 2));
        Equal(32767, CombatSampling.RollBelow(random, new("large", "fight"), int.MaxValue));
        Equal(32767, CombatSampling.RollBelow(random, new("minimum", "fight"), int.MinValue));
        random.RequireChoicesConsumed();
    }

    // U06 Damage rounds: equality differs between the first pair and protected-defender reroll.
    private static void CombatRoundTiesAndRerolls()
    {
        foreach (int variant in Enumerable.Range(0, 4))
        {
            var f = new CombatFixture(); var prepared = f.Prepare() with { Attack = 8, Defense = 8, DefenderReroll = variant != 3 };
            f.World.Globals.Rules.NormalCombat = false; f.Attacker.Damage = 9; f.Data.Unit(2).Damage = 9;
            List<Choice> choices = [CombatChoice("U06.round.attack", 8, variant == 0 ? 2 : 7), CombatChoice("U06.round.defense", 8, 2)];
            if (variant is 1 or 2)
            {
                choices.Add(CombatChoice("U06.reroll.attack", 8, variant == 1 ? 3 : 2));
                choices.Add(CombatChoice("U06.reroll.defense", 8, 3));
            }
            var random = CombatRandom([.. choices]); var effects = new RoundEffects();
            using var op = RoundService(f, random, effects).Begin(prepared, "fight");
            bool won = variant is 1 or 3;
            Equal(won ? 1 : 0, op.Advance()); Equal(1, op.Rounds); Equal(true, op.Completed);
            Equal((byte)(won ? 0 : 1), f.Attacker.Damage); Equal((byte)(won ? 1 : 0), f.Data.Unit(2).Damage);
            Equal(won ? 1 : 0, op.Advance()); Equal(0, effects.Calls.Count);
            Equal(string.Join('|', choices.Select(x => x.Key.Site)), string.Join('|', random.Capture().Trace!.Events.Select(x => x.Key.Site)));
            random.RequireChoicesConsumed();
        }
    }

    private static void CombatRoundContinuation()
    {
        var f = new CombatFixture(); var prepared = f.Prepare() with { AttackFirepower = 5 };
        f.Attacker.Damage = 2; var defenderToken = f.Data.Unit(2).Token;
        // Live attack/defense/firepower changes must not replace the captured combat values.
        f.Data.Rules.Units[10] = f.Data.Rules.Units[10] with { Attack = 0, Firepower = 0 };
        f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Defense = 127 };
        var random = CombatRandom(CombatChoice("U06.round.attack", 80, 79), CombatChoice("U06.round.defense", 24, 0),
            CombatChoice("U06.round.attack", 80, 79, 1), CombatChoice("U06.round.defense", 24, 0, 1));
        using var op = RoundService(f, random, new()).Begin(prepared, "fight", 1);
        Equal((int?)null, op.Advance()); Equal(1, op.Rounds); Equal((byte)5, f.Data.Unit(2).Damage);
        Equal((int?)null, op.Advance()); Equal(2, op.Rounds); Equal((byte)10, f.Data.Unit(2).Damage);
        Equal(8, op.Advance()); Equal(8, op.Advance()); Equal(2, op.Rounds);
        Equal(defenderToken, f.Data.Unit(2).Token); Equal(CargoPoint, f.Data.Unit(2).Position);
        random.RequireChoicesConsumed();

        var stalled = new CombatFixture(); var zero = stalled.Prepare() with { Attack = 0, Defense = 1, AttackFirepower = 0, DefenseFirepower = 0 };
        var empty = CombatRandom(); var operation = RoundService(stalled, empty, new()).Begin(zero, "fight", 3);
        Equal((int?)null, operation.Advance()); Equal(3, operation.Rounds);
        Equal((int?)null, operation.Advance()); Equal(6, operation.Rounds); Equal(false, operation.Completed);
        Equal((byte)0, stalled.Attacker.Damage); empty.RequireChoicesConsumed();
        operation.Dispose(); Throws<ObjectDisposedException>(() => operation.Advance());
        Throws<ArgumentOutOfRangeException>(() => RoundService(stalled, empty, new()).Begin(zero, "fight", 0));
        var negative = new CombatFixture(); var backwards = negative.Prepare() with { Attack = 0, Defense = 1, DefenseFirepower = -1 };
        using var backwardsOp = RoundService(negative, CombatRandom(), new()).Begin(backwards, "fight", 1);
        Equal((int?)null, backwardsOp.Advance()); Equal((byte)255, negative.Attacker.Damage);
        Equal(0, backwardsOp.Advance()); Equal(1, backwardsOp.Rounds);

    }

    private static void CombatHitPresentation()
    {
        var f = new CombatFixture(); var effects = new RoundEffects(); var service = RoundService(f, CombatRandom(), effects);
        f.State.AnyViewer = true; f.State.Seen[1] = true; f.State.AnimationInterval = 5; f.State.ExplosionKind = 6;
        f.Attacker.Damage = 250; service.Hit(new(3), 10, new(8, 10), new(8, 10), CargoPoint);
        Equal((byte)4, f.Attacker.Damage); Equal("publish|explode:8,10:6|redraw", string.Join('|', effects.Calls));
        Equal(true, f.State.ExplosionShown); effects.Calls.Clear();
        service.Hit(new(3), -1, new(8, 10), new(8, 10), CargoPoint); Equal((byte)3, f.Attacker.Damage); Equal(0, effects.Calls.Count);
        f.Attacker.Damage = 4; f.World.Globals.Rules.NormalCombat = false;
        service.Hit(new(3), 1, new(8, 10), new(8, 10), CargoPoint);
        Equal((byte)0, f.Attacker.Damage); Equal("publish|explode:8,10:6|redraw", string.Join('|', effects.Calls));
        effects.Calls.Clear(); f.State.Seen[1] = false; f.State.Seen[0] = true; f.State.Seen[2] = true; f.Attacker.Damage = 4;
        service.Hit(new(3), 1, new(8, 10), new(8, 10), CargoPoint);
        Equal((byte)5, f.Attacker.Damage); Equal("publish", string.Join('|', effects.Calls));
        effects.Calls.Clear(); f.State.Seen[1] = true; f.State.AnyViewer = false; f.Attacker.Damage = 4;
        service.Hit(new(3), 1, new(8, 10), new(8, 10), CargoPoint); Equal((byte)5, f.Attacker.Damage); Equal(0, effects.Calls.Count);
        f.State.AnyViewer = true; f.State.AnimationInterval = 0;
        service.Hit(new(3), 5, new(8, 10), new(8, 10), CargoPoint); Equal((byte)10, f.Attacker.Damage); Equal(0, effects.Calls.Count);
        f.World.Globals.Rules.NormalCombat = true; f.State.AnimationInterval = 5; f.Attacker.Damage = 9;
        service.Hit(new(3), 1, new(8, 10), new(8, 10), CargoPoint); Equal("publish|explode:8,10:6", string.Join('|', effects.Calls));
    }

    private static void CombatRoundInitializationAndFailures()
    {
        foreach (byte hp in new byte[] { 29, 30, 128 })
        {
            var f = new CombatFixture(); var prepared = f.Prepare() with { Attack = 0, Defense = 0 };
            f.Data.Rules.Units[10] = f.Data.Rules.Units[10] with { HitPoints = hp };
            f.State.ExplosionShown = true;
            using var op = RoundService(f, CombatRandom(), new()).Begin(prepared, "fight", 1);
            Equal(hp == 128 ? 0 : (int?)null, op.Advance()); Equal(hp == 30 ? 10 : 5, f.State.AnimationInterval); Equal(true, f.State.ExplosionShown);
        }
        var shortCircuit = new CombatFixture(); var capturedZero = shortCircuit.Prepare();
        shortCircuit.Data.Rules.Units[10] = shortCircuit.Data.Rules.Units[10] with { HitPoints = 0 };
        shortCircuit.World.Globals.Rules.NormalCombat = false; shortCircuit.Attacker.Damage = 8; shortCircuit.Data.Unit(2).Damage = 9;
        using var zeroOp = RoundService(shortCircuit, CombatRandom(), new()).Begin(capturedZero, "fight");
        Equal(0, zeroOp.Advance()); Equal((byte)0, shortCircuit.Attacker.Damage); Equal((byte)9, shortCircuit.Data.Unit(2).Damage);
        var missile = new CombatFixture(); var p = missile.Prepare();
        missile.Data.Rules.Units[10] = missile.Data.Rules.Units[10] with { Abilities = new() { DestroyedAfterAttack = true }, HitPoints = 0 };
        missile.State.Seen[1] = true; missile.State.AnyViewer = false; missile.State.ExplosionShown = true; missile.State.ExplosionKind = 46;
        var fx = new RoundEffects(); using var missileOp = RoundService(missile, CombatRandom(), fx).Begin(p, "fight");
        Equal(0, missileOp.Advance()); Equal("explode:10,10:46", string.Join('|', fx.Calls));
        Equal(0, missile.State.AnimationInterval); Equal(false, missile.State.ExplosionShown);
        foreach (string fail in new[] { "publish", "explode:8,10:0", "redraw" })
        {
            var f = new CombatFixture(); var prepared = f.Prepare() with { Attack = 0, Defense = 0 };
            f.Attacker.Damage = 4; f.State.AnyViewer = f.State.Seen[1] = true;
            var effects = new RoundEffects { Fail = fail }; using var op = RoundService(f, CombatRandom(), effects).Begin(prepared, "fight");
            Throws<InvalidOperationException>(() => op.Advance()); Equal((byte)5, f.Attacker.Damage); Equal(true, f.State.ExplosionShown);
            int count = effects.Calls.Count; Throws<InvalidOperationException>(() => op.Advance()); Equal(count, effects.Calls.Count);
        }
        var remote = new CombatFixture(); var captured = remote.Prepare(); remote.World.Globals.Multiplayer = GameMode.Tcp;
        using var blocked = RoundService(remote, CombatRandom(), new()).Begin(captured, "fight");
        Throws<InvalidOperationException>(() => blocked.Advance()); Equal(0, remote.State.AnimationInterval); Equal((byte)0, remote.Attacker.Damage);
    }

    // U06 Promotion bookkeeping: captured strengths/owners, inclusive test, live winner and C08 wonder.
    private static void CombatPromotionSelection()
    {
        foreach (bool won in new[] { false, true })
            foreach (int offset in new[] { 0, 1 })
            {
                var f = new CombatFixture(); var p = f.Prepare() with { Attack = 8, Defense = 4 };
                f.World.Civilizations[1].AggressionCounter[2] = 255;
                var random = CombatRandom(CombatChoice("U06.promotion", 12, (won ? 4 : 8) + offset));
                List<CombatPromotionNotice> notices = [];
                new CombatPromotion(f.World, f.Data.Rules, random, notices.Add).Apply(p, won, "fight");
                Equal((byte)0, f.World.Civilizations[1].AggressionCounter[2]);
                Equal(offset == 0, f.Data.Unit(won ? 3 : 2).Flags.Veteran); Equal(won && offset == 0 ? 1 : 0, notices.Count);
                random.RequireChoicesConsumed();
            }
        foreach (bool expired in new[] { false, true })
        {
            var f = new CombatFixture(); var p = f.Prepare() with { Attack = 8, Defense = 4 };
            var city = f.City(); f.World.Globals.WonderCity[WonderIds.SunTzu] = (short)city.Value;
            f.Attacker.Owner = new(2); f.Data.Rules.Units[10] = f.Data.Rules.Units[10] with { Domain = RuleDomain.Air };
            f.Data.Rules.WonderObsolescence[WonderIds.SunTzu] = (AdvanceReference)5;
            if (expired) f.World.Civilizations[4].Advances = default(AdvanceSet).With(5);
            var random = CombatRandom(CombatChoice("U06.promotion", 12, 11));
            new CombatPromotion(f.World, f.Data.Rules, random, _ => throw new InvalidOperationException()).Apply(p, true, "fight");
            Equal(!expired, f.Attacker.Flags.Veteran); random.RequireChoicesConsumed();
        }
    }

    private static void CombatPromotionEligibilityAndFailures()
    {
        foreach (int variant in Enumerable.Range(0, 4))
        {
            var f = new CombatFixture(); var p = f.Prepare();
            f.Attacker.Flags.Veteran = variant == 0;
            if (variant == 1) f.Data.Rules.Units[10] = f.Data.Rules.Units[10] with { Abilities = new() { DestroyedAfterAttack = true } };
            f.World.Multiplayer.Saved.AutoplayActive = variant == 2;
            var random = CombatRandom(CombatChoice("U06.promotion", 104, 0)); List<CombatPromotionNotice> notices = [];
            new CombatPromotion(f.World, f.Data.Rules, random, notices.Add).Apply(p, true, "fight");
            Equal(variant != 1, f.Attacker.Flags.Veteran); Equal(variant == 3 ? 1 : 0, notices.Count); random.RequireChoicesConsumed();
            if (notices.Count != 0) { Equal(new UnitTarget(new(3), f.Attacker.Token), notices[0].Unit); Equal(f.Attacker.Type, notices[0].Type); Equal(1, notices[0].Owner); }
        }
        var failed = new CombatFixture(); var prepared = failed.Prepare(); failed.World.Civilizations[1].AggressionCounter[2] = 9;
        var empty = CombatRandom(); var missing = new CombatPromotion(failed.World, failed.Data.Rules, empty, _ => { });
        Throws<ChoiceException>(() => missing.Apply(prepared, false, "fight")); Equal((byte)10, failed.World.Civilizations[1].AggressionCounter[2]);
        Equal(false, failed.Data.Unit(2).Flags.Veteran);
        var random2 = CombatRandom(CombatChoice("U06.promotion", 104, 0));
        var noticeFailure = new CombatPromotion(failed.World, failed.Data.Rules, random2, _ => throw new InvalidOperationException("notice failed"));
        Throws<InvalidOperationException>(() => noticeFailure.Apply(prepared, true, "fight"));
        Equal(true, failed.Attacker.Flags.Veteran); Equal((byte)0, failed.World.Civilizations[1].AggressionCounter[2]); random2.RequireChoicesConsumed();
    }
}
