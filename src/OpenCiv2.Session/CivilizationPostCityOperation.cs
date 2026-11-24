using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Ai;

namespace OpenCiv2.Session;

/// <summary>F04 §6 steps 5–7: actual government/diplomacy, assessment and population/funds.
/// The admitted upkeep parent supplies the captured civilization and normalized start treasury.</summary>
public sealed class CivilizationPostCityOperation(int owner, int startGold, DiplomacyTurn diplomacy,
    CivilizationAssessment assessment, EspionageIncidents incidents, IPreferenceWriter preferences,
    RateDecisionPreferences choices, Func<int, ParticipantId> recipient,
    Func<EmissaryIntelligence, InteractionSpec> intelligence, string diplomacyCall, string assessmentCall,
    int candidateBudget = 64) : IContinuation
{
    private IContinuation? child;
    private int stage;
    private string failure = "";
    private OperationStep? completed;
    private bool disposed, faulted;

    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (faulted)
        { throw new InvalidOperationException("A failed post-city turn cannot resume"); }
        if (completed is not null)
        { return completed; }
        try
        {
            if (context.World.Globals.Multiplayer != GameMode.SinglePlayer)
            { throw new InvalidOperationException("Local post-city upkeep requires single-player mode"); }
            if (owner < 0 || owner >= EntityLimits.Civilizations)
            { throw new ArgumentOutOfRangeException(nameof(owner)); }
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(candidateBudget);
            ArgumentException.ThrowIfNullOrEmpty(diplomacyCall);
            ArgumentException.ThrowIfNullOrEmpty(assessmentCall);
            while (stage < 3)
            {
                if (child is null)
                {
                    child = stage switch
                    {
                        0 => new CivilizationPoliticsOperation(owner, diplomacy, preferences, choices, recipient, intelligence, diplomacyCall),
                        1 => new CivilizationAssessmentOperation(assessment.Begin(owner, assessmentCall, incidents, candidateBudget), recipient, intelligence),
                        _ => new CivilizationFinanceOperation(preferences, choices, startGold, owner, recipient)
                    };
                    input = new();
                }
                var result = child.Resume(context, input);
                if (!result.Status.Terminal())
                { return result; }
                if (result.Status == CommandStatus.Failed && failure.Length == 0)
                { failure = result.Code; }
                child.Dispose();
                child = null;
                ++stage;
            }
            return completed = new(failure.Length == 0 ? CommandStatus.Completed : CommandStatus.Failed, Code: failure);
        }
        catch { faulted = true; child?.Dispose(); context.Publish(); throw; }
    }
    public void Dispose() { disposed = true; child?.Dispose(); }
}
