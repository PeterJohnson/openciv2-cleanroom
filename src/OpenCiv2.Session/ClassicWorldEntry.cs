using OpenCiv2.Base.Randomness;
using OpenCiv2.Core;

namespace OpenCiv2.Session;

/// <summary>F03/F05 composition adapter. Common world entry receives owned content and semantic services;
/// classic path/file scratch is reset here after runtime isolation and before loading that content.</summary>
public static class ClassicWorldEntry
{
    public static bool Enter(
        SessionRuntime runtime,
        ClassicFileState files,
        GameplayRandom random,
        string call,
        NewWorldChoice choice,
        CalendarValues calendar,
        WorldEntryServices services)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(services);
        try
        {
            return WorldEntry.Enter(
                runtime,
                ref files.Starts,
                random,
                call,
                choice,
                calendar,
                services with
                {
                    ResetSource = () =>
                    {
                        files.PrepareNewWorld();
                        if (services.Premade?.Source is ClassicPremadeWorldSource source)
                        {
                            source.PrepareNewWorld();
                        }
                    }
                });
        }
        finally
        {
            if (services.Premade?.Source is ClassicPremadeWorldSource { MapBytes: { } mapBytes }
                && mapBytes.BelongsTo(runtime.World.Map))
            {
                files.RecordBytes = files.RecordBytes?.For(runtime.World.Lifetime) ??
                    OpenCiv2.Formats.RecordCompatibility.Find(runtime.World.Lifetime) ?? new(runtime.World.Lifetime);
                files.RecordBytes.Attach(runtime.World.Lifetime);
                files.RecordBytes.Map = mapBytes;
            }
        }
    }
}
