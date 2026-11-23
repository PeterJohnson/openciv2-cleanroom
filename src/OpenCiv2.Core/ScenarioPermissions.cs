using OpenCiv2.Core.State;

namespace OpenCiv2.Core;

/// <summary>S02 permission predicates. The required action dispatcher owns authored effects and one-shot state.</summary>
public static class ScenarioPermissions
{
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
