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
    Action<bool>? GenerateWorld,
    NewGameServices? Initialize,
    Action<int>? Accelerate,
    Action<CivilizationSlot, string> Introduce,
    WorldGenerationOptions? Generation = null,
    AccelerationOptions? Acceleration = null)
{
    internal void Validate(NewWorldChoice choice)
    {
        ArgumentNullException.ThrowIfNull(ResetSource);
        ArgumentNullException.ThrowIfNull(LoadContent);
        ArgumentNullException.ThrowIfNull(StopMenuSound);
        if (GenerateWorld is null && Generation is null)
        {
            throw new ArgumentException("World entry requires generation options or a supplied generation child");
        }
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
        Initialize?.Validate();
    }
}
public static class WorldEntry
{
    /// <summary>F05 §2/V03 §2: highest twelve eligible known advances, displayed ascending with trailing
    /// comma/space.</summary>
    public static string StartingAdvanceList(GameState world, RuleState rules, int civilization)
    {
        var selected = new List<int>();
        for (int index = RuleLimits.Advances - 1; index > 0 && selected.Count < 12; --index)
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
        var text = new System.Text.StringBuilder();
        for (int i = selected.Count - 1; i >= 0; --i)
        {
            text.Append(RuleText.Display(rules.Definitions.Advances[selected[i]].Identity.Name));
            text.Append(", ");
        }
        return text.ToString();
    }

    /// <summary>F05 §2. Validate selected services, cross the fresh-game boundary, then compose setup.
    /// Generation uses W02 with explicit options or a supplied child. A null Initialize selects actual local
    /// V01/F05 starting operations. A null Accelerate selects actual acceleration with its naming content.
    /// No client session is admitted here. Cooperative hosts can use the generated
    /// initialization/acceleration operations directly for cancellable polling.</summary>
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
            content.Labels?.Install(rules.Labels);
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
                PreferenceFiles.Capture(runtime.Process));
            installer.Advances(true);
            installer.Units(true);
            installer.Improvements(true);
            Publish();
            services.StopMenuSound();
            Publish();
            if (services.GenerateWorld is { } generate)
            {
                generate(source != WorldSource.Generated);
            }
            else
            {
                var compatibility = (services.Premade?.Source as ClassicPremadeWorldSource)?.MapBytes;
                WorldGeneration.Generate(runtime, ref starts, random, call + "/generation", services.Generation!,
                    source != WorldSource.Generated, compatibility);
            }
            if (services.Initialize is { } initialization)
            {
                NewGameInitialization.Initialize(runtime, random, call + "/initialize", initialization);
            }
            else
            {
                using var initializationOperation = new GeneratedGameInitialization(runtime, starts, random, call + "/initialize");
                while (!initializationOperation.Poll(256))
                { }
            }
            if (players.Acceleration != 0)
            {
                Publish();
                if (services.Accelerate is { } accelerate)
                {
                    accelerate(players.Acceleration);
                }
                else
                {
                    using var acceleration = new GeneratedGameAcceleration(runtime, random, call + "/accelerate",
                        players.Acceleration - 1, services.Acceleration
                            ?? throw new InvalidOperationException("Generated acceleration requires its naming content"));
                    while (!acceleration.Poll())
                    { }
                }
            }
            int player = runtime.World.Globals.PlayerCiv;
            if (player is < 1 or >= EntityLimits.Civilizations)
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
