using OpenCiv2.Base;

namespace OpenCiv2.Contracts;

public enum WorldSource { Generated, PremadeCylindrical, PremadeFlat }
public abstract record WorldSizeChoice
{
    private protected WorldSizeChoice() { }

    public sealed record Preset(int Row) : WorldSizeChoice;

    public sealed record Custom : WorldSizeChoice;
}
public enum CustomSizeStatus { Accepted, Cancelled, Unavailable }
public sealed record CustomSizeResponse(CustomSizeStatus Status, string Width, string Height);
public interface IWorldSizeInteraction
{
    // 0=small, 2=large, other nonnegative=normal; negative cancels.
    WorldSizeChoice ChoosePreset(WorldSize rememberedPreset);

    CustomSizeResponse ChooseCustom(string widthDefault, string heightDefault);
}
public abstract record WorldParameterChoice
{
    private protected WorldParameterChoice() { }

    public sealed record Selected(int Row) : WorldParameterChoice;

    public sealed record Random : WorldParameterChoice;
}
public abstract record SetupNumericChoice
{
    private protected SetupNumericChoice() { }

    public sealed record Selected(int Row) : SetupNumericChoice;

    public sealed record Random : SetupNumericChoice;
}
// Classic byte strings remain bytes; no implicit UTF-8 conversion at this boundary.
public sealed record SetupTribeOption(int Tribe, OwnedList<byte> Plural, short PreferredColor);
public sealed record SetupTribeChoice(int Tribe = -1, bool Custom = false);
public sealed record SetupAdvancedChoices(bool SimplifiedCombat = false, bool FlatWorld = false,
    bool PickOpponents = false, bool AcceleratedStartup = false, bool Bloodlust = false, bool NoRestart = false);
// F05 deliberately ignores the modal result, including cancellation.
public sealed record SetupAdvancedResponse(int ModalResult, SetupAdvancedChoices Checks);
public enum SetupNameAction { Accept, Cancel, Unavailable, Titles, Portrait }
public sealed record SetupNames(OwnedList<byte> Leader, OwnedList<byte> Plural, OwnedList<byte> Adjective);
public sealed record SetupNameResponse(SetupNameAction Action, SetupNames Names);
public sealed record GovernmentTitles
{
    public OwnedList<OwnedList<byte>> Values { get; }

    public GovernmentTitles(IEnumerable<OwnedList<byte>> values)
    {
        Values = new(values);
        if (Values.Count != 7)
        {
            throw new ArgumentException("Seven government titles are required.", nameof(values));
        }
    }
}
public enum SetupTitleStatus { Accept, Cancel, Unavailable }
public sealed record SetupTitleResponse(SetupTitleStatus Status, GovernmentTitles Titles);
public readonly record struct AccelerationYears(uint First, uint Second, uint Third);
public interface IPlayerSetupInteraction
{
    int ChooseDifficulty(Difficulty remembered);

    int ChooseRules(RuleSelection remembered);

    int ChooseGender(Gender remembered);

    SetupNumericChoice ChooseOpponents(short remembered);

    SetupNumericChoice ChooseBarbarians(short remembered);

    SetupAdvancedResponse ChooseAdvanced(SetupAdvancedChoices defaults);

    int ChooseAcceleration(AccelerationYears years, short remembered);

    SetupTribeChoice ChooseTribe(OwnedList<SetupTribeOption> options, short remembered);

    SetupNameResponse ChooseNames(bool custom, SetupNames current);

    SetupTitleResponse ChooseTitles(GovernmentTitles current);

    void ChoosePortrait(CivilizationSlot civilization);

    int ChooseCityStyle(short tribeStyle, short currentOverride);

    SetupNumericChoice ChooseOpponentTribe(CivilizationSlot civilization, OwnedList<SetupTribeOption> options);
}
public sealed record PlayerSetupResult(bool Accepted = false, bool PickOpponents = false, int Acceleration = 0);
