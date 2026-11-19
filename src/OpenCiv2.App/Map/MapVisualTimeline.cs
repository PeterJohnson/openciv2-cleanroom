using OpenCiv2.Base;
using OpenCiv2.Contracts;

namespace OpenCiv2.App.Map;

internal enum MapEffectKind { Move, CombatParticipant, Explosion, Nuclear }
internal enum PresentationEnd { Finished, Cancelled, Interrupted, Skipped }

internal sealed record MapEffect(ulong Id, MapEffectKind Kind, MapPoint From, MapPoint To, int Milliseconds,
    ObservedMapUnit? Unit = null);
internal sealed record MapNotice(ulong Id, string Message, int Milliseconds);
internal readonly record struct EffectFrame(MapEffect Effect, double Progress, int WrappedDestinationX);
internal sealed record VisualFrame(OwnedList<EffectFrame> Effects, OwnedList<UnitTarget> Moving, OwnedList<string> Notices, bool Pulse)
{
    public static VisualFrame Empty { get; } = new(OwnedList<EffectFrame>.Empty, OwnedList<UnitTarget>.Empty, OwnedList<string>.Empty, false);
}

/// <summary>MV22/MV24 disposable, binding-local presentation timelines. Decision-bearing
/// messages use the normal command/interaction channel, rather than this cosmetic one.</summary>
internal sealed class MapVisualTimeline(Action<ulong, PresentationEnd>? completed = null)
{
    private readonly Dictionary<ulong, (MapEffect Effect, long Start)> effects = [];
    private readonly Dictionary<ulong, (MapNotice Notice, long Start)> notices = [];
    private ClientIdentity? binding;
    private ulong sequence;

    public void Bind(ClientIdentity? identity)
    {
        if (identity == binding) return;
        binding = identity;
        sequence = 0;
        EndAll(PresentationEnd.Interrupted);
    }

    public bool Queue(ClientIdentity identity, MapEffect effect, long now)
    {
        if (effect.Milliseconds <= 0 || effect is { Kind: MapEffectKind.Move, Unit: null } ||
            effects.Count >= 32 || !Admit(identity, effect.Id)) return false;
        effects.Add(effect.Id, (effect, now));
        return true;
    }

    public bool Queue(ClientIdentity identity, MapNotice notice, long now)
    {
        if (notice.Milliseconds <= 0 || notice.Message.Length is < 1 or > 400 || notices.Count >= 8 ||
            !Admit(identity, notice.Id)) return false;
        notices.Add(notice.Id, (notice, now));
        return true;
    }

    public bool Cancel(ulong id) => End(id, PresentationEnd.Cancelled);

    public void CancelAll() => EndAll(PresentationEnd.Cancelled);

    private void EndAll(PresentationEnd reason)
    {
        var pending = effects.Keys.Concat(notices.Keys).ToArray();
        effects.Clear();
        notices.Clear();
        foreach (ulong id in pending) completed?.Invoke(id, reason);
    }

    private bool Admit(ClientIdentity identity, ulong id)
    {
        if (binding != identity || id <= sequence) return false;
        sequence = id;
        return true;
    }

    private bool End(ulong id, PresentationEnd reason)
    {
        if (!(effects.Remove(id) || notices.Remove(id))) return false;
        completed?.Invoke(id, reason);
        return true;
    }

    public VisualFrame Sample(MapScene? scene, long now, bool reducedMotion)
    {
        if (scene is null || binding != scene.Map.Identity) return VisualFrame.Empty;
        var visible = new List<EffectFrame>(effects.Count);
        var moving = new List<UnitTarget>();
        foreach (var (effect, start) in effects.Values.ToArray())
        {
            if (effect.Unit?.Target is UnitTarget target && !scene.HasTarget(target) ||
                !scene.PermitsSegment(effect.From, effect.To, out int endX))
            {
                End(effect.Id, PresentationEnd.Interrupted);
                continue;
            }
            if (reducedMotion || now >= start && (double)now - start >= effect.Milliseconds)
            {
                End(effect.Id, reducedMotion ? PresentationEnd.Skipped : PresentationEnd.Finished);
                continue;
            }
            double progress = Math.Max(0, ((double)now - start) / effect.Milliseconds);
            visible.Add(new(effect, progress, endX));
            if (effect.Kind == MapEffectKind.Move && effect.Unit?.Target is UnitTarget hidden) moving.Add(hidden);
        }
        foreach (var (notice, start) in notices.Values.ToArray())
            if (now >= start && (double)now - start >= notice.Milliseconds)
                End(notice.Id, PresentationEnd.Finished);
        return new(new(visible), new(moving.Distinct().OrderBy(t => t.Slot.Value)),
            new(notices.Values.Select(n => n.Notice.Message)), reducedMotion || (now / 400 & 1) == 0);
    }

    public static int FrameIndex(MapEffectKind kind, double progress)
    {
        int count = kind == MapEffectKind.Explosion ? 8 : 11;
        return Math.Clamp((int)(progress * count), 0, count - 1);
    }
}
