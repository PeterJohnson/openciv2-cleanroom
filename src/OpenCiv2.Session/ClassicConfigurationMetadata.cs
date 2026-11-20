using OpenCiv2.Contracts;
using OpenCiv2.Core.State;
using OpenCiv2.Formats;
namespace OpenCiv2.Session;
/// <summary>Classic opaque fields follow the same copy/reset boundaries as their logical settings.</summary>
internal static class ClassicConfigurationMetadata
{
    public static void ResetMessages(ProcessContext process, GameState world)
    {
        if (PreferenceFiles.Capture(process).ImportedOrdinary is { } preferences)
        {
            PreferenceFiles.ReplaceImported(process, preferences with { DisabledMessages = 0 });
        }

        if (RecordCompatibility.Find(world.Lifetime) is { } records)
        {
            records.Globals = records.Globals with { MessageOptions = 0 };
        }
    }
    public static void ResetRules(ProcessContext process, GameState world, bool preferences)
    {
        if (preferences && PreferenceFiles.Capture(process).ImportedOrdinary is { } prior)
        {
            PreferenceFiles.ReplaceImported(process, prior with { Rules = 0x2f });
        }

        if (RecordCompatibility.Find(world.Lifetime) is { } records)
        {
            records.Globals = records.Globals with { Rules = 0x2f };
        }
    }
    public static void CopyOptionsToPreferences(ProcessContext process, GameState world)
    {
        var prior = PreferenceFiles.Capture(process).ImportedOrdinary ??
            new StoredOrdinaryPreferences { Rules = StateConversion.EncodeGameRules(process.OrdinaryPreferences.Rules) };
        PreferenceFiles.ReplaceImported(process, prior with
        { GameOptions = RecordCompatibility.Find(world.Lifetime)?.Globals.GameOptions ?? 0 });
    }
    public static void CopyOptions(ProcessContext process, GameState world, bool initialize)
    {
        var preferences = PreferenceFiles.Capture(process).ImportedOrdinary;
        var records = RecordCompatibility.Find(world.Lifetime);
        if (records is null && preferences is null)
        {
            return;
        }

        if (records is null)
        {
            records = new(world.Lifetime);
            records.Attach(world.Lifetime);
        }
        records.Globals = records.Globals with { GameOptions = preferences?.GameOptions ?? 0 };
        if (!initialize)
        {
            return;
        }

        records.Globals = records.Globals with
        { MessageOptions = preferences?.DisabledMessages ?? 0, TransientFlags = 0, ShownEventFlags = 0 };
        records.Scenario = null;
        for (int i = 0; i < records.Civilizations.Length; ++i)
        {
            records.Civilizations[i] = records.Civilizations[i] with
            { SpaceshipFlags = 0, ContinentFlags = new(new byte[MapLimits.Regions]) };
        }
    }
}
