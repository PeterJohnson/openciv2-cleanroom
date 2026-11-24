using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Rules;

namespace OpenCiv2.Session;

/// <summary>V03 informational presentation through recipient-scoped Session notices.
/// The owner supplies the viewer for local announcements/advice lacking an explicit recipient.
/// Decisions remain in the retained research/government/emissary operations.</summary>
public sealed class SinglePlayerResearchPresentation(Func<OperationContext> current, Func<int> viewer,
    Func<CitySlot?>? selectedCity = null, int noticeMode = 0) :
    IResearchGrantEffects, IResearchGovernmentEffects, IResearchChoiceEffects, IResearchCompletionEffects
{
    private OperationContext Current => current();
    public bool DiplomacyActive => Current.World.Diplomacy.MeetingActive;
    public int NoticeMode => noticeMode;
    public CitySlot? SelectedCity => selectedCity?.Invoke();
    private void Tell(int recipient, string text)
    {
        if (recipient is < 0 or >= EntityLimits.Civilizations)
        { throw new ArgumentOutOfRangeException(nameof(recipient)); }
        (Current.Notify ?? throw new InvalidOperationException("Research presentation requires a scoped notice publisher"))(new((byte)recipient), text);
    }
    private void Tell(string text) => Tell(viewer(), text);
    private void Record(int recipient, string name, params NamedValue[] values)
    {
        // Command effects are visible to the initiator; foreign decisions are not.
        if (Current.Actor.Civilization.Value == recipient)
        {
            Current.Effect(name, new(values));
        }
        else
        {
            Current.Publish();
        }
    }
    private string Advance(int identity) => RuleText.Display(Current.Process.Rules.Definitions.Advances[identity].Identity.Name);
    private static string Name(string name, int? number) => number is { } n ? $"{name} {n}" : name;
    public void Completed(ResearchCompletionNotice notice)
    {
        Record(notice.Civilization, "research.completed", new NamedValue("advance", new CommandValue.Integer(notice.Advance)));
        Tell(notice.Civilization, $"Research completed: {Name(notice.Name, notice.FutureNumber)}.");
    }
    public void Selected(int civilization, int advance) => Record(civilization, "research.selected",
        new NamedValue("advance", new CommandValue.Integer(advance)));
    public void Ability(ResearchAbilityNotice notice)
    {
        Record(notice.Civilization, "research.ability", new NamedValue("ability", new CommandValue.Text(notice.Ability.ToString())));
        string ability = notice.Ability switch
        {
            ResearchAbility.Transform => "terrain transformation",
            ResearchAbility.Fortress => "fortresses",
            ResearchAbility.Airlift => "airlifts",
            ResearchAbility.Railroad => "railroads",
            ResearchAbility.Farmland => "farmland",
            ResearchAbility.Paradrop => "paradrops",
            _ => throw new ArgumentOutOfRangeException(nameof(notice))
        };
        Tell(notice.Civilization, $"{Advance(notice.Advance)} enables {ability}"
            + (notice.Range is { } range ? $" with range {range}." : "."));
    }
    public void FirstCivilization(int civilization)
    {
        Record(civilization, "research.first_civilization");
        Tell(civilization, "Choose an advance to direct your civilization's research.");
    }
    public void Advice(ResearchAdvice advice)
    {
        Record(viewer(), "research.advice", new NamedValue("advance", new CommandValue.Integer(advice.Recommendation)));
        string extra = advice.Kind switch
        {
            ResearchAdviceKind.Unlocks => " Unlocks: ",
            ResearchAdviceKind.SubsequentAdvances => " Leads to: ",
            _ => ""
        };
        Tell($"Recommended research: {Name(advice.Name, advice.FutureNumber)}."
            + (extra.Length == 0 ? "" : extra + string.Join(", ", advice.Items.Select(ResearchGoalInteraction.Label)) + "."));
    }
    // Advice is retained in the recent-notices view; closing the choice has no separate window to close.
    public void CloseAdvice() { }
    public void Encyclopedia(ResearchTopic topic, int identity) => Tell(ResearchReference.Text(Current.Process.Rules.Definitions, topic, identity));
    public void Encyclopedia(int advance) => Encyclopedia(ResearchTopic.Advance, advance);
    public void Acquired(AdvanceAcquisitionNotice notice) => Tell($"{notice.CivilizationName} acquired {notice.AdvanceName}"
        + (notice.SourceName is { } source ? $" from {source}." : ".")
        + (notice.Unlocks.Count == 0 ? "" : $" Unlocks: {string.Join(", ", notice.Unlocks.Select(u => u.Name))}."));
    public void EmissaryKnowledge(int civilization, int advance) => Tell($"The {Current.World.Names[civilization].Tribe} emissary reports knowledge of {Advance(advance)}.");
    public void BarracksSold(BarracksSaleNotice notice) => Tell(notice.Civilization,
        $"{Advance(notice.Advance)} made {notice.Count} barracks obsolete. Their sale returns {notice.Value} gold.");
    public void GoldenAge(GoldenAgeNotice notice) => Tell($"{notice.CityName} celebrates a golden age of {notice.Adjective} philosophy.");
    public void WonderExpired(WonderExpiryNotice notice) => Tell(notice.Owner,
        $"{Current.Process.Rules.Definitions.Improvements[39 + notice.Wonder].Identity.Name} in {Current.World.Entities.Cities.At(notice.City).Name} is obsolete after the discovery of {Advance(notice.Advance)}.");
    public void RefreshStatus() => Current.Publish();
    public void RevolutionStarting(int civilization)
    {
        Record(civilization, "government.revolution_started", new NamedValue("civilization", new CommandValue.Integer(civilization)));
        Tell(civilization, "A revolution is beginning. Your civilization enters Anarchy.");
    }
    public void Tutorial(int civilization, ResearchTutorial tutorial)
    {
        int advance = tutorial switch
        {
            ResearchTutorial.Republic => AdvanceIds.Republic,
            ResearchTutorial.Railroad => AdvanceIds.Railroad,
            ResearchTutorial.Refrigeration => AdvanceIds.Refrigeration,
            ResearchTutorial.Trade => AdvanceIds.Trade,
            ResearchTutorial.Seafaring => AdvanceIds.Seafaring,
            ResearchTutorial.Writing => AdvanceIds.Writing,
            _ => throw new ArgumentOutOfRangeException(nameof(tutorial))
        };
        Tell(civilization, "Research guide: " + ResearchReference.Text(Current.Process.Rules.Definitions, ResearchTopic.Advance, advance));
    }
    private static InvalidOperationException Retained() => new("Player research decisions require their retained operation");
    public int ConfirmGovernment(ResearchGovernmentChoice choice) => throw Retained();
    public bool ReceiveEmissary(int civilization) => throw Retained();
    public void ResetDiplomacy() => throw Retained();
    public ResearchReply GoalChoice(ResearchGoalNotice notice) => throw Retained();
    public int GoalHelp(ResearchTopic topic, int identity, int prerequisite) => throw Retained();
    public void GoalPath(ResearchPathNotice notice) => throw Retained();
}
