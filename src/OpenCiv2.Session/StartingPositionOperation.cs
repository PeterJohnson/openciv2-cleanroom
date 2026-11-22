using OpenCiv2.Base.Randomness;
using OpenCiv2.Core;
using OpenCiv2.Core.State;

namespace OpenCiv2.Session;

/// <summary>F05 §7 / V01 §2 session-owned search and late hint selection. Polling bounds work without
/// changing the unlimited human turn-zero search. Cancellation keeps consumed choices and releases the world.</summary>
public sealed class StartingPositionOperation : IDisposable
{
    private readonly GameState world;
    private readonly int civilization;
    private readonly StartingPositions hints;
    private readonly StartingSiteSearch search;
    private SessionActivity? pin;

    public StartingSite? Result { get; private set; }
    public bool Cancelled { get; private set; }

    /// <summary>The caller owns setup admission and prevents concurrent game mutations between polls.
    /// Hints and regional city counts are copied; the world stays pinned until completion or disposal.</summary>
    public StartingPositionOperation(
        SessionRuntime runtime,
        int civilization,
        StartingPositions hints,
        ReadOnlySpan<int> capturedCityCounts,
        GameplayRandom random,
        string call)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        world = runtime.World;
        this.civilization = civilization;
        this.hints = hints;
        search = new(world, civilization, capturedCityCounts, random, call);
        pin = runtime.PinWorld();
    }

    /// <summary>Returns null while pending or cancelled. A result includes V01's late turn-zero hint;
    /// this operation does not perform the subsequent admission, rate changes or unit placement.</summary>
    public StartingSite? Poll(int candidateBudget = 64)
    {
        if (Result is not null || Cancelled)
        {
            return Result;
        }

        try
        {
            if (search.Advance(candidateBudget) is not { } selected)
            {
                return null;
            }

            if (world.Globals.Turn == 0)
            {
                int tribe = world.Civilizations[civilization].Tribe;
                if (tribe is < 0 or >= OpenCiv2.Contracts.RuleLimits.Tribes)
                {
                    throw new ArgumentOutOfRangeException(nameof(tribe), "Starting hint has no tribe backing.");
                }

                selected = StartingSites.ApplyHint(world, selected, hints.Positions[tribe]);
            }

            Result = selected;
            Release();
            return Result;
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    private void Release()
    {
        pin?.Dispose();
        pin = null;
    }

    public void Dispose()
    {
        Cancelled |= Result is null;
        Release();
    }
}
