using OpenCiv2.Core.State;

namespace OpenCiv2.Core;

/// <summary>S02 permission predicates. The required action dispatcher owns authored effects and one-shot state.</summary>
public static class ScenarioPermissions
{
    /// <summary>S02 §7 local negotiation: first match wins, including consumed records.
    /// Actions must finish before permission is returned; their ordinary result cannot override it.
    /// Loaded-events and meeting-active gates belong to V04/V05 callers.</summary>
    public static bool AllowNegotiation(GameState world, int talker, int listener, Action<Event> execute)
    {
        ArgumentNullException.ThrowIfNull(execute);
        Units.MapKnowledge.RequireLocal(world);
        var events = world.Events.Collection;
        for (int i = 0; i < events.Count; ++i)
        {
            var entry = events.At(i);
            if (entry.Values.Trigger != EventTrigger.Negotiation
                || !Matches(world, entry.Values.WhoCiv, entry.Values.TalkerType, talker)
                || !Matches(world, entry.Values.WhomCiv, entry.Values.ListenerType, listener))
            {
                continue;
            }

            bool allowed = !entry.Values.DenyNegotiation;
            execute(entry);
            return allowed;
        }
        return true;
    }

    private static bool Matches(GameState world, int selector, int control, int participant)
    {
        if (selector != participant && selector != -2)
        { return false; }
        // The stored selector supplies the classification, even when it is Anybody.
        bool human = world.Globals.HumanPlayers.Contains(selector & 31);
        return control == 4 || control == (human ? 1 : 2);
    }

    public static bool AllowSchism(GameState world, int civilization, Action<Event> execute)
    {
        ArgumentNullException.ThrowIfNull(execute);
        bool allowed = true;
        var events = world.Events.Collection;
        for (int i = 0; i < events.Count; ++i)
        {
            var entry = events.At(i);
            if (entry.Values.Trigger != EventTrigger.NoSchism
                || entry.Values.WhomCiv != civilization && entry.Values.WhomCiv != -2)
            {
                continue;
            }

            allowed = false;
            execute(entry);
            if (!events.Contains(entry))
            {
                throw new InvalidOperationException("Scenario action invalidated the current event");
            }
        }
        return allowed;
    }
}
