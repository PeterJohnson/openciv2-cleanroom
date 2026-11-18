using OpenCiv2.Contracts;
using OpenCiv2.IO;

namespace OpenCiv2.Session;

public sealed record GameOptionServices(IFileStorage Preferences, IMusicControl Music);
public sealed record GraphicsOptionServices(IFileStorage Preferences, Func<bool> HeraldsAvailable, Action LowMemoryNotice);

/// <summary>F03 §8 accepted option boundaries. Cancellation has no effects. Required providers
/// are checked before mutation; child failures retain completed effects and release the world pin.</summary>
public static class Configuration
{
    private static void Publish(SessionRuntime runtime) { if (runtime.Active) runtime.Publish(); }

    public static bool Game(SessionRuntime runtime, GameOptionChoices? choices, GameOptionServices services)
    {
        if (choices is null) return false;
        ArgumentNullException.ThrowIfNull(runtime); ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(services.Preferences); ArgumentNullException.ThrowIfNull(services.Music);
        using var pin = runtime.PinWorld();
        try
        {
            ref uint options = ref runtime.World.Globals.GameOptions;
            bool oldMusic = (options & 8) != 0;
            options &= 0xffff8027;
            if (choices.Sound) options |= 0x10;
            if (choices.Music)
            {
                options |= 8;
                if (!oldMusic) { Publish(runtime); services.Music.Reinitialize(); }
                Publish(runtime); services.Music.Synchronize();
            }
            else { Publish(runtime); services.Music.Stop(); }
            if (choices.Wait) options |= 0x4000;
            if (choices.Autosave) options |= 0x2000;
            if (choices.EnemyMoves) options |= 0x1000;
            if (choices.SkipEnemyPauses) options |= 0x800;
            if (choices.FastSlide) options |= 0x400;
            if (choices.Advice) options |= 0x200;
            if (choices.Tutorial) options |= 0x100;
            if (choices.MouseGoTo) options |= 0x80;
            if (choices.EnterClosesCity) options |= 0x40;
            runtime.Process.OrdinaryPreferences.GameOptions = options;
            _ = PreferenceFiles.Save(runtime, services.Preferences);
            return true;
        }
        finally { Publish(runtime); }
    }

    public static bool Graphics(SessionRuntime runtime, GraphicsOptionChoices? choices, GraphicsOptionServices services)
    {
        if (choices is null) return false;
        ArgumentNullException.ThrowIfNull(runtime); ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(services.Preferences); ArgumentNullException.ThrowIfNull(services.HeraldsAvailable); ArgumentNullException.ThrowIfNull(services.LowMemoryNotice);
        using var pin = runtime.PinWorld();
        try
        {
            ref uint options = ref runtime.World.Globals.GameOptions;
            bool oldHeralds = (options & 0x200000) != 0;
            options &= ~0x003f0000U;
            if (choices.Throne) options |= 0x20000;
            if (choices.Diplomacy) options |= 0x40000;
            if (choices.Heralds)
            {
                if (!oldHeralds)
                {
                    Publish(runtime);
                    if (!services.HeraldsAvailable()) { Publish(runtime); services.LowMemoryNotice(); }
                }
                options |= 0x200000;
            }
            if (choices.AdvanceCivilopedia) options |= 0x80000;
            if (choices.Council) options |= 0x100000;
            if (choices.WonderMovies) options |= 0x10000;
            runtime.Process.OrdinaryPreferences.GameOptions = options;
            _ = PreferenceFiles.Save(runtime, services.Preferences);
            return true;
        }
        finally { Publish(runtime); }
    }

    public static bool Messages(SessionRuntime runtime, MessageOptionChoices? choices, IFileStorage preferences)
    {
        if (choices is null) return false;
        ArgumentNullException.ThrowIfNull(runtime); ArgumentNullException.ThrowIfNull(preferences);
        using var pin = runtime.PinWorld();
        try
        {
            ReadOnlySpan<bool> enabled = [choices.GrowthHalted, choices.ImprovementBuilt, choices.UnitBuilt, choices.InvalidBuild,
                choices.Disorder, choices.OrderRestored, choices.Celebration, choices.FoodLow, choices.Pollution, choices.ProductionPenalty, choices.ZoomToCity];
            ushort messages = 0;
            for (int i = 0; i < enabled.Length; ++i) if (!enabled[i]) messages |= (ushort)(1 << i);
            runtime.World.Globals.MessageOptions = messages;
            runtime.Process.OrdinaryPreferences.DisabledMessages = messages;
            _ = PreferenceFiles.Save(runtime, preferences);
            return true;
        }
        finally { Publish(runtime); }
    }
}
