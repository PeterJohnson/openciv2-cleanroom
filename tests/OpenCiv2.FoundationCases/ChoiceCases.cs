using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static void ChoiceFails(ChoiceFailure failure, Action action, ChoiceKey? key = null)
    {
        try { action(); }
        catch (ChoiceException error)
        {
            Equal(true, failure == error.Failure);
            Equal(true, key == error.Key);
            return;
        }
        throw new InvalidOperationException($"Expected choice failure {failure}.");
    }

    private static void ControlledSemanticOrder()
    {
        ChoiceKey a = new("a\0\u00ff", "first"), b = new("test", "second", 1),
            c = new("test", "third", 2), d = new("test", "fourth", 3);
        Choice[] input = [Choice.Create(a, new SamplingLaw.PrimaryCode(), ChoiceCode.Primary(32767)),
            Choice.Create(b, new SamplingLaw.PrimaryModulo(7), ChoiceCode.Primary(9)),
            Choice.Create(c, new SamplingLaw.SecondaryUnit(), ChoiceCode.Secondary(uint.MaxValue)),
            Choice.Create(d, new SamplingLaw.SecondaryRange(-4, 7), ChoiceCode.Secondary(0))];
        var game = new GameplayRandom(new ControlledChoices(new(input)), "choices/test");
        input[0] = input[1];
        Equal(2, game.PrimaryModulo(b, 7));
        var saved = game.Capture();
        var restored = new GameplayRandom(99, "choices/test");
        restored.Restore(saved, 0);
        ChoiceFails(ChoiceFailure.AlreadyConsumed, () => restored.PrimaryModulo(b, 7), b);
        Equal(saved, restored.Capture());
        Equal(32767, restored.PrimaryCode(a));
        Equal(1.0, restored.SecondaryUnit(c));
        Equal(-4, restored.SecondaryRange(d, -4, 7));
        restored.RequireChoicesConsumed();
        Equal(0UL, restored.Draws);
        Equal(1, saved.Trace!.Events.Count);
        Equal(b, saved.Trace.Events[0].Key);
        Equal(saved, game.Capture());
    }

    private static void ChoiceFailurePrefix()
    {
        ChoiceKey key = new("test", "choice"), missing = new("test", "missing");
        var record = Choice.Create(key, new SamplingLaw.PrimaryModulo(7), ChoiceCode.Primary(9));
        var game = new GameplayRandom(new ControlledChoices(new([record])), "choices/test");
        var initial = game.Capture();
        ChoiceFails(ChoiceFailure.Missing, () => game.PrimaryCode(missing), missing);
        ChoiceFails(ChoiceFailure.LawMismatch, () => game.PrimaryModulo(key, 8), key);
        ChoiceFails(ChoiceFailure.SemanticKeyRequired, () => game.PrimaryCode("test"));
        ChoiceFails(ChoiceFailure.Unused, game.RequireChoicesConsumed, key);
        ChoiceFails(ChoiceFailure.InvalidRecord, () => game.PrimaryCode(default(ChoiceKey)), default(ChoiceKey));
        Equal(0, game.PrimaryModulo(default(ChoiceKey), 1));
        Equal(4, game.SecondaryRange(default(ChoiceKey), 4, 4));
        Equal(initial, game.Capture());
        Equal(2, game.PrimaryModulo(key, 7));
        game.RequireChoicesConsumed();
        var consumed = game.Capture();
        ChoiceFails(ChoiceFailure.AlreadyConsumed, () => game.PrimaryModulo(key, 7), key);
        Equal(consumed, game.Capture());
        var traced = new GameplayRandom(0, "choices/test");
        traced.RecordChoices(0);
        var zero = traced.Capture();
        ChoiceFails(ChoiceFailure.TraceLimit, () => traced.PrimaryCode(key), key);
        Equal(zero, traced.Capture());
        ChoiceFails(ChoiceFailure.InvalidRecord, () => Choice.Create(key, new SamplingLaw.PrimaryCode(), ChoiceCode.Secondary(0)), key);
        ChoiceFails(ChoiceFailure.InvalidRecord, () => Choice.Create(key, new SamplingLaw.PrimaryCode(), ChoiceCode.Primary(-1)), key);
        ChoiceFails(ChoiceFailure.InvalidRecord, () => Choice.Create(key, new SamplingLaw.PrimaryCode(), ChoiceCode.Primary(32768)), key);
        ChoiceFails(ChoiceFailure.InvalidRecord, () => Choice.Create(key, new SamplingLaw.PrimaryModulo(0), ChoiceCode.Primary(0)), key);
        ChoiceFails(ChoiceFailure.InvalidRecord, () => Choice.Create(key with { Attempt = -1 }, new SamplingLaw.PrimaryCode(), ChoiceCode.Primary(0)), key with { Attempt = -1 });
    }

    private static void GeneratedTraceRestore()
    {
        var game = new GameplayRandom(0, "session/main");
        var plain = new GameplayRandom(0, "session/main");
        game.SecondaryCode("prefix"); plain.SecondaryCode("prefix");
        game.RecordChoices(2);
        ChoiceKey first = new("test", "first"), second = new("test", "second"), third = new("test", "third");
        Equal(plain.PrimaryModulo("test", 7), game.PrimaryModulo(first, 7));
        var saved = game.Capture();
        var restored = new GameplayRandom(1, "session/main");
        restored.Restore(saved, 2);
        ChoiceFails(ChoiceFailure.AlreadyConsumed, () => restored.PrimaryModulo(first, 7), first);
        Equal(saved, restored.Capture());
        Equal(game.SecondaryUnit(second), restored.SecondaryUnit(second));
        Equal(game.Capture(), restored.Capture());
        Equal(3UL, game.Draws);
        Equal(1, saved.Trace!.Events.Count);
        var full = restored.Capture();
        ChoiceFails(ChoiceFailure.TraceLimit, () => restored.PrimaryCode(third), third);
        ChoiceFails(ChoiceFailure.SemanticKeyRequired, () => restored.SecondaryCode("test"));
        Throws<InvalidOperationException>(() => restored.RecordChoices(3));
        Equal(full, restored.Capture());
    }

    private static void ChoiceCheckpointAdmission()
    {
        ChoiceKey key = new("test", "one");
        Choice choice = Choice.Create(key, new SamplingLaw.SecondaryUnit(), ChoiceCode.Secondary(0));
        var game = new GameplayRandom(new ControlledChoices(new([choice])), "choices/test");
        game.SecondaryUnit(key);
        var saved = game.Capture();
        RandomCheckpoint[] corrupt =
        [
            saved with { Position = saved.Position with { Draws = 1 } },
            saved with { Trace = null },
            saved with { Trace = saved.Trace! with { Limit = 0 } },
            saved with { Trace = saved.Trace! with { Limit = -1 } },
            saved with { Controlled = new(new([choice, choice])), Trace = new(2, new([])) },
            saved with { Trace = new(1, new([choice with { Result = ChoiceResult.Binary64(-0.0) }])) },
            saved with { Trace = new(1, new([Choice.Create(key with { Call = "unknown" }, choice.Law, choice.Code)])) },
            saved with { Controlled = null }
        ];
        foreach (RandomCheckpoint bad in corrupt)
        {
            Throws<ChoiceException>(() => game.Restore(bad, 1));
            Equal(saved, game.Capture());
        }
        ChoiceFails(ChoiceFailure.InvalidRecord, () => (choice with { Result = ChoiceResult.Binary64(-0.0) }).Validate(), key);
    }
}
