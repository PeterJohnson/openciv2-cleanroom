namespace OpenCiv2.Core;

/// <summary>A human choice retained through civilization destruction, recreation or a research grant.
/// The concrete decision determines its recipient and cancellation semantics.</summary>
public abstract record LifecycleDecision;

public sealed record LifecycleContact(DiplomaticContactDecision Decision) : LifecycleDecision;
