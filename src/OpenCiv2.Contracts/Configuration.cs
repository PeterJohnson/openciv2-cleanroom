namespace OpenCiv2.Contracts;

// F03 §8 accepted checkbox meanings. Presentation scratch is separate.
public sealed record GameOptionChoices(bool Sound, bool Music, bool Wait, bool Autosave,
    bool EnemyMoves, bool SkipEnemyPauses, bool FastSlide, bool Advice, bool Tutorial,
    bool MouseGoTo, bool EnterClosesCity);
public sealed record GraphicsOptionChoices(bool Throne, bool Diplomacy, bool Heralds,
    bool AdvanceCivilopedia, bool Council, bool WonderMovies);
public sealed record MessageOptionChoices(bool GrowthHalted, bool ImprovementBuilt, bool UnitBuilt,
    bool InvalidBuild, bool Disorder, bool OrderRestored, bool Celebration, bool FoodLow,
    bool Pollution, bool ProductionPenalty, bool ZoomToCity);
public sealed record MultiplayerOptionChoices(bool ClearChatOnNewGame, bool ClearChatOnBoot,
    bool DoubleProduction, bool DoubleMovement);
public interface IMusicControl
{
    void Stop();
    void Reinitialize();
    void Synchronize();
}
public sealed record OptionProposal(string Sender, string Description, bool Production, bool Movement, int ExpectedVotes);
public readonly record struct OptionVoteProgress(int Pending, int Status);
/// <summary>Owned vote registration. Poll performs bounded nonblocking work; Cancel must not throw.</summary>
public interface IOptionVoteChannel : IDisposable
{
    void Begin(OptionProposal proposal);
    OptionVoteProgress Poll();
    void Cancel();
}
public interface IOptionBroadcaster
{
    void Rejected();
    void Accepted();
    void Units();
    void Options(bool production, bool movement, bool simultaneous);
}
public enum MultiplayerOptionResult { Cancelled, ClientOnly, Unchanged, Rejected, Applied, Failed }
