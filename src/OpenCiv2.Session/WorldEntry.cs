using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Session;

public enum NewWorldChoice { Random, Premade, Customize }
public sealed record WorldEntryServices(
    Action ResetSource,
    Func<LoadedContent> LoadContent,
    IWorldSizeInteraction? Size,
    Func<string?>? ChooseMap,
    PremadeWorldServices? Premade,
    WorldParameterServices Parameters,
    IPlayerSetupInteraction Players,
    Action StopMenuSound,
    Action<bool> GenerateWorld,
    NewGameServices Initialize,
    Action<int> Accelerate,
    Action<CivilizationSlot, OwnedList<byte>> Introduce)
{
    internal void Validate(NewWorldChoice choice)
    {
        ArgumentNullException.ThrowIfNull(ResetSource);
        ArgumentNullException.ThrowIfNull(LoadContent);
        ArgumentNullException.ThrowIfNull(StopMenuSound);
        ArgumentNullException.ThrowIfNull(GenerateWorld);
        ArgumentNullException.ThrowIfNull(Accelerate);
        ArgumentNullException.ThrowIfNull(Introduce);
        if (choice == NewWorldChoice.Premade)
        {
            ArgumentNullException.ThrowIfNull(ChooseMap);
            ArgumentNullException.ThrowIfNull(Premade);
            Premade.Validate();
        }
        else
        {
            ArgumentNullException.ThrowIfNull(Size);
        }

        ArgumentNullException.ThrowIfNull(Parameters);
        if (choice == NewWorldChoice.Customize)
        {
            Parameters.Validate();
        }
        else
        {
            ArgumentNullException.ThrowIfNull(Parameters.Preferences);
        }

        ArgumentNullException.ThrowIfNull(Players);
        ArgumentNullException.ThrowIfNull(Initialize);
        Initialize.Validate();
    }
}
public static class WorldEntry
{
    /// <summary>F05 §2/V03 §2: highest twelve eligible known advances, displayed ascending with trailing
    /// comma/space.</summary>
    public static OwnedList<byte> StartingAdvanceList(GameState world, RuleState rules, int civilization)
    {
        var selected = new List<int>();
        for (int index = 99; index > 0 && selected.Count < 12; --index)
        {
            var prerequisites = rules.Definitions.Advances[index].Prerequisites;
            if (prerequisites[0] == AdvanceReference.Never && prerequisites[1] == AdvanceReference.Never)
            {
                continue;
            }

            if (CivilizationKnowledge.KnowsAdvance(world.Civilizations, civilization, index))
            {
                selected.Add(index);
            }
        }
        var bytes = new List<byte>();
        for (int i = selected.Count - 1; i >= 0; --i)
        {
            bytes.AddRange(rules.Text.Text(rules.Names.Advances[selected[i]]));
            bytes.Add((byte)',');
            bytes.Add((byte)' ');
        }
        return new(bytes);
    }

    /// <summary>F05 §2. Validate selected services, cross the fresh-game boundary, then compose setup.
    /// Required W02/V01/balancing/acceleration/presentation children remain explicit; no session is admitted
    /// here.</summary>
    public static bool Enter(
        SessionRuntime runtime,
        ref StartingPositions starts,
        GameplayRandom random,
        string call,
        NewWorldChoice choice,
        CalendarValues calendar,
        WorldEntryServices services)
    {
        ArgumentException.ThrowIfNullOrEmpty(call);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(calendar);
        ArgumentNullException.ThrowIfNull(services);
        if (choice is not (NewWorldChoice.Random or NewWorldChoice.Premade or NewWorldChoice.Customize))
        {
            throw new ArgumentOutOfRangeException(nameof(choice));
        }

        services.Validate(choice);
        runtime.PrepareNewGame();
        using var pin = runtime.PinWorld();
        starts = new();
        services.ResetSource();
        void Publish()
        {
            if (runtime.Active)
            {
                runtime.Publish();
            }
        }
        try
        {
            Publish();
            var content = services.LoadContent();
            ArgumentNullException.ThrowIfNull(content);
            ArgumentNullException.ThrowIfNull(content.Rules);
            var rules = runtime.Process.Rules;
            content.Labels?.Install(rules.Text, rules.Labels);
            var installer = new RuleInstaller(content.Rules, rules);
            installer.Load(true);
            var source = WorldSource.Generated;
            bool selected;
            if (choice == NewWorldChoice.Premade)
            {
                Publish();
                var loaded = PremadeWorld.Load(runtime, ref starts, services.ChooseMap!(), services.Premade!);
                selected = loaded.HasValue;
                if (loaded is { } loadedSource)
                {
                    source = loadedSource;
                }
            }
            else
            {
                selected = WorldSetupOperations.SelectSize(runtime, services.Size!);
            }

            if (!WorldSetupOperations.SelectParameters(
                runtime,
                random,
                call + "/parameters",
                choice == NewWorldChoice.Customize,
                selected,
                services.Parameters))
            {
                Publish();
                return false;
            }
            var players = PlayerSetup.Select(runtime, random, call + "/players", source, calendar, services.Players);
            if (!players.Accepted)
            {
                Publish();
                return false;
            }
            Publish();
            _ = services.Parameters.Preferences.Write(
                new(runtime.Process.OrdinaryPreferences, runtime.Process.MultiplayerPreferences));
            installer.Advances(true);
            installer.Units(true);
            installer.Improvements(true);
            Publish();
            services.StopMenuSound();
            Publish();
            services.GenerateWorld(source != WorldSource.Generated);
            NewGameInitialization.Initialize(runtime, random, call + "/initialize", services.Initialize);
            if (players.Acceleration != 0)
            {
                Publish();
                services.Accelerate(players.Acceleration);
            }
            int player = runtime.World.Globals.PlayerCiv;
            if (player is < 1 or > 7)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(runtime),
                    "World introduction requires a human civilization");
            }

            var advances = StartingAdvanceList(runtime.World, rules, player);
            Publish();
            services.Introduce(new((byte)player), advances);
            Publish();
            return true;
        }
        catch
        {
            try
            {
                Publish();
            }
            catch { }

            throw;
        }
    }
}
