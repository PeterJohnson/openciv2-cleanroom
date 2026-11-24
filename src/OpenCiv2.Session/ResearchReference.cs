using OpenCiv2.Core;
using OpenCiv2.Core.Rules;

namespace OpenCiv2.Session;

/// <summary>Rule-derived reference pages for research Help. Full authored Civilopedia prose
/// remains a content-host concern; these pages use the currently installed custom rule names.</summary>
internal static class ResearchReference
{
    internal static string Text(RuleTables rules, ResearchTopic topic, int identity)
    {
        string Prerequisite(AdvanceReference advance) => advance switch
        {
            AdvanceReference.None => "None",
            AdvanceReference.Never => "Unavailable",
            _ => RuleText.Display(rules.Advances[(int)advance].Identity.Name)
        };
        switch (topic)
        {
            case ResearchTopic.Advance:
                var advance = rules.Advances[identity];
                var prerequisites = advance.Prerequisites.Where(p => p != AdvanceReference.None).Select(Prerequisite).ToArray();
                var unlocks = rules.Units.Where(u => (int)u.Prerequisite == identity).Select(u => RuleText.Display(u.Identity.Name))
                    .Concat(rules.Improvements.Skip(1).Where(i => (int)i.Prerequisite == identity).Select(i => RuleText.Display(i.Identity.Name))).ToArray();
                return $"{RuleText.Display(advance.Identity.Name)}\nRequires: {(prerequisites.Length == 0 ? "None" : string.Join(", ", prerequisites))}."
                    + (unlocks.Length == 0 ? "" : $"\nUnlocks: {string.Join(", ", unlocks)}.");
            case ResearchTopic.Unit:
                var unit = rules.Units[identity];
                return $"{RuleText.Display(unit.Identity.Name)}\nRequires: {Prerequisite(unit.Prerequisite)}."
                    + $"\nAttack: {unit.Attack} · Defense: {unit.Defense} · Hit points: {unit.HitPoints} · Firepower: {unit.Firepower}."
                    + (unit.ObsoleteBy == AdvanceReference.Never ? "" : $"\nObsolete with: {Prerequisite(unit.ObsoleteBy)}.");
            case ResearchTopic.Improvement:
                var building = rules.Improvements[identity];
                return $"{RuleText.Display(building.Identity.Name)}\nRequires: {Prerequisite(building.Prerequisite)}.\nMaintenance: {building.Upkeep} gold per turn.";
            default:
                throw new ArgumentOutOfRangeException(nameof(topic));
        }
    }
}
