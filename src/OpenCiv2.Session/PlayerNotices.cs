using OpenCiv2.Base;
using OpenCiv2.Contracts;

namespace OpenCiv2.Session;

/// <summary>Session presentation history, not saved gameplay. A bounded private snapshot survives
/// controller rebinding but is cleared when its client session ends.</summary>
internal sealed class PlayerNoticeHistory
{
    internal const int Capacity = 64;
    private readonly Queue<PlayerNotice> items = new();
    private ulong next = 1, omitted;
    internal void Add(int turn, string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var notice = new PlayerNotice(next, turn, text);
        next = checked(next + 1);
        if (items.Count == Capacity)
        { items.Dequeue(); ++omitted; }
        items.Enqueue(notice);
    }
    internal PlayerNotices Observe(LiveObservation observation) => new(observation, new(items), omitted);
}

public sealed partial class SessionRuntime
{
    private sealed partial class PlayerClient
    {
        public PlayerNotices ObserveNotices()
        {
            var (current, grant) = Checked();
            var observation = new LiveObservation(current.Identity, current.Session, current.Revision, grant.Identity.Observer);
            return current.Notices.TryGetValue(grant.Identity.Observer, out var history)
                ? history.Observe(observation) : new(observation, OwnedList<PlayerNotice>.Empty, 0);
        }
    }
}
