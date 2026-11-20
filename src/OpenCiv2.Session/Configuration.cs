using OpenCiv2.Contracts;
using OpenCiv2.Core.State;
using OpenCiv2.IO;

namespace OpenCiv2.Session;

public sealed record GameOptionServices(IFileStorage Preferences, IMusicControl Music);
public sealed record GraphicsOptionServices(
    IFileStorage Preferences,
    Func<bool> HeraldsAvailable,
    Action LowMemoryNotice);

/// <summary>F03 §8 accepted option boundaries. Cancellation has no effects. Required providers
/// are checked before mutation; child failures retain completed effects and release the world pin.</summary>
public static class Configuration
{
    private static void Publish(SessionRuntime runtime)
    {
        if (runtime.Active)
        {
            runtime.Publish();
        }
    }

    public static bool Game(SessionRuntime runtime, GameOptionChoices? choices, GameOptionServices services)
    {
        if (choices is null)
        {
            return false;
        }

        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(services.Preferences);
        ArgumentNullException.ThrowIfNull(services.Music);
        using var pin = runtime.PinWorld();
        try
        {
            ref var options = ref runtime.World.Globals.GameOptions;
            bool oldMusic = options.Music;
            options.Sound = options.Music = options.Wait = options.Autosave = options.EnemyMoves =
                options.SkipEnemyPauses = options.FastSlide = options.Advice = options.Tutorial =
                options.MouseGoTo = options.EnterClosesCity = false;
            if (choices.Sound)
            {
                options.Sound = true;
            }

            if (choices.Music)
            {
                options.Music = true;
                if (!oldMusic)
                {
                    Publish(runtime);
                    services.Music.Reinitialize();
                }
                Publish(runtime);
                services.Music.Synchronize();
            }
            else
            {
                Publish(runtime);
                services.Music.Stop();
            }
            if (choices.Wait)
            {
                options.Wait = true;
            }

            if (choices.Autosave)
            {
                options.Autosave = true;
            }

            if (choices.EnemyMoves)
            {
                options.EnemyMoves = true;
            }

            if (choices.SkipEnemyPauses)
            {
                options.SkipEnemyPauses = true;
            }

            if (choices.FastSlide)
            {
                options.FastSlide = true;
            }

            if (choices.Advice)
            {
                options.Advice = true;
            }

            if (choices.Tutorial)
            {
                options.Tutorial = true;
            }

            if (choices.MouseGoTo)
            {
                options.MouseGoTo = true;
            }

            if (choices.EnterClosesCity)
            {
                options.EnterClosesCity = true;
            }

            ClassicConfigurationMetadata.CopyOptionsToPreferences(runtime.Process, runtime.World);
            runtime.Process.OrdinaryPreferences.GameOptions = options;
            _ = PreferenceFiles.Save(runtime, services.Preferences);
            return true;
        }
        finally
        {
            Publish(runtime);
        }
    }

    public static bool Graphics(SessionRuntime runtime, GraphicsOptionChoices? choices, GraphicsOptionServices services)
    {
        if (choices is null)
        {
            return false;
        }

        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(services.Preferences);
        ArgumentNullException.ThrowIfNull(services.HeraldsAvailable);
        ArgumentNullException.ThrowIfNull(services.LowMemoryNotice);
        using var pin = runtime.PinWorld();
        try
        {
            ref var options = ref runtime.World.Globals.GameOptions;
            bool oldHeralds = options.Heralds;
            options.Throne = options.Diplomacy = options.Heralds = options.AdvanceCivilopedia =
                options.Council = options.WonderMovies = false;
            if (choices.Throne)
            {
                options.Throne = true;
            }

            if (choices.Diplomacy)
            {
                options.Diplomacy = true;
            }

            if (choices.Heralds)
            {
                if (!oldHeralds)
                {
                    Publish(runtime);
                    if (!services.HeraldsAvailable())
                    {
                        Publish(runtime);
                        services.LowMemoryNotice();
                    }
                }
                options.Heralds = true;
            }
            if (choices.AdvanceCivilopedia)
            {
                options.AdvanceCivilopedia = true;
            }

            if (choices.Council)
            {
                options.Council = true;
            }

            if (choices.WonderMovies)
            {
                options.WonderMovies = true;
            }

            ClassicConfigurationMetadata.CopyOptionsToPreferences(runtime.Process, runtime.World);
            runtime.Process.OrdinaryPreferences.GameOptions = options;
            _ = PreferenceFiles.Save(runtime, services.Preferences);
            return true;
        }
        finally
        {
            Publish(runtime);
        }
    }

    public static bool Messages(SessionRuntime runtime, MessageOptionChoices? choices, IFileStorage preferences)
    {
        if (choices is null)
        {
            return false;
        }

        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(preferences);
        using var pin = runtime.PinWorld();
        try
        {
            var messages = new MessageSuppression
            {
                GrowthHalted = !choices.GrowthHalted,
                ImprovementBuilt = !choices.ImprovementBuilt,
                UnitBuilt = !choices.UnitBuilt,
                InvalidBuild = !choices.InvalidBuild,
                Disorder = !choices.Disorder,
                OrderRestored = !choices.OrderRestored,
                Celebration = !choices.Celebration,
                FoodLow = !choices.FoodLow,
                Pollution = !choices.Pollution,
                ProductionPenalty = !choices.ProductionPenalty,
                ZoomToCity = !choices.ZoomToCity,
            };

            ClassicConfigurationMetadata.ResetMessages(runtime.Process, runtime.World);
            runtime.World.Globals.MessageOptions = messages;
            runtime.Process.OrdinaryPreferences.DisabledMessages = messages;
            _ = PreferenceFiles.Save(runtime, preferences);
            return true;
        }
        finally
        {
            Publish(runtime);
        }
    }
}
