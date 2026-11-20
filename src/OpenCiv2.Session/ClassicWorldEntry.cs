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
        return WorldEntry.Enter(
            runtime,
            ref files.Starts,
            random,
            call,
            choice,
            calendar,
            services with { ResetSource = files.PrepareNewWorld });
    }
}
