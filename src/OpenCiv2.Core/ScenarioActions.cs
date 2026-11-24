using OpenCiv2.Core.State;

namespace OpenCiv2.Core;

public enum ScenarioAction
{
    PlayWaveFile, PlayCdTrack, CreateUnit, MoveUnit, ChangeTerrain, MakeAggression,
    ChangeMoney, DestroyCivilization, GiveTechnology, Text, SuppressWonderMovies
}

/// <summary>S02 §8 synchronous dispatch with required owning children. A caller must not use a
/// synchronous adapter for an action that can suspend for a participant decision.</summary>
public static class ScenarioActions
{
    private static readonly ScenarioAction[] order =
    [
        ScenarioAction.PlayWaveFile, ScenarioAction.PlayCdTrack, ScenarioAction.CreateUnit,
        ScenarioAction.MoveUnit, ScenarioAction.ChangeTerrain, ScenarioAction.MakeAggression,
        ScenarioAction.ChangeMoney, ScenarioAction.DestroyCivilization, ScenarioAction.GiveTechnology,
        ScenarioAction.Text, ScenarioAction.SuppressWonderMovies
    ];

    public static int Execute(Event entry, Func<ScenarioAction, Event, int> execute)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(execute);
        if (entry.Values.Actions.JustOnce && entry.Values.Actions.Consumed)
        { return 0; }
        foreach (var action in order)
        {
            if (Enabled(entry.Values.Actions, action))
            { _ = execute(action, entry); }
        }
        if (entry.Values.Actions.JustOnce)
        { entry.Values.Actions.Consumed = true; }
        return 1;
    }

    private static bool Enabled(EventActions actions, ScenarioAction action) => action switch
    {
        ScenarioAction.PlayWaveFile => actions.PlayWaveFile,
        ScenarioAction.PlayCdTrack => actions.PlayCdTrack,
        ScenarioAction.CreateUnit => actions.CreateUnit,
        ScenarioAction.MoveUnit => actions.MoveUnit,
        ScenarioAction.ChangeTerrain => actions.ChangeTerrain,
        ScenarioAction.MakeAggression => actions.MakeAggression,
        ScenarioAction.ChangeMoney => actions.ChangeMoney,
        ScenarioAction.DestroyCivilization => actions.DestroyCivilization,
        ScenarioAction.GiveTechnology => actions.GiveTechnology,
        ScenarioAction.Text => actions.Text,
        ScenarioAction.SuppressWonderMovies => actions.SuppressWonderMovies,
        _ => throw new ArgumentOutOfRangeException(nameof(action))
    };

    /// <summary>S02 §9: single indirection only; Anybody remains an ordinary invalid recipient.</summary>
    public static int ResolveCivilization(Event entry, int selector) => selector switch
    {
        -3 => entry.Values.WhoCiv,
        -4 => entry.Values.WhomCiv,
        _ => selector
    };

    /// <summary>S02 §9: signed32 sum, with saturation direction chosen from the amount's sign.</summary>
    public static int ChangeMoney(GameState world, Event entry)
    {
        Units.MapKnowledge.RequireLocal(world);
        int recipient = ResolveCivilization(entry, entry.Values.MoneyReceiverCiv);
        if (recipient is < 1 or > 7 || !world.Globals.CivsAlive.Contains(recipient))
        { return 0; }
        int amount = entry.Values.MoneyAmount;
        int sum = unchecked(world.Civilizations[recipient].Gold + amount);
        world.Civilizations[recipient].Gold = sum is < 0 or > 30000 ? amount < 0 ? 0 : 30000 : sum;
        return 1;
    }
}
