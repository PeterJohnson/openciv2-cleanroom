using OpenCiv2.Base;

namespace OpenCiv2.Contracts;

/// <summary>Informational presentation for one civilization. Sequence is private to that recipient;
/// no initiating command, foreign participant or unfiltered world record is exposed.</summary>
public sealed record PlayerNotice(ulong Sequence, int Turn, string Text);
public sealed record PlayerNotices(LiveObservation Observation, OwnedList<PlayerNotice> Items, ulong Omitted);
