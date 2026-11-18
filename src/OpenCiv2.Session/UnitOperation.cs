using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Session;

public enum UnitOperationKind { Detach, Attach, Relocate, Bottom, Erase, RelocateStack }
public sealed record UnitRequest(UnitOperationKind Operation, OwnedList<int> Arguments);
public readonly record struct UnitReply(int? Value = null, bool Drained = true)
{
    public UnitReply() : this(null, true) { }
}
/// <summary>Owned sequential primitive channel. Poll is bounded/nonblocking; waiting distinguishes reply wait
/// from queue drain. Cancel releases registrations without throwing or retaining world references.</summary>
public interface IUnitRequestChannel
{
    void Send(UnitRequest request);
    UnitReply Poll(bool waiting);
    void Cancel();
}
public sealed record UnitOperationServices(List<StackDiagnostic>? Diagnostics = null, Action? Placement = null,
    IUnitDeletionEffects? Deletion = null, Func<uint>? Ticks = null, Action? TimedOut = null);
public readonly record struct UnitOperationResult(int Reply = 0, bool TimedOut = false, bool Cancelled = false);

/// <summary>U01 §§2–4,6,9: local mechanics or a suspended remote primitive, with captured successors for a
/// whole-stack relocation. Standalone operations pin the world; command children borrow their owner's lifetime.
/// Providers outlive this operation. Explicit disposal cancels; there is no finalizer-driven completion.</summary>
public sealed class UnitOperation : IDisposable
{
    private readonly GameState world;
    private readonly RuleState rules;
    private readonly Action publish;
    private UnitRequest request;
    private readonly bool notify, stack;
    private readonly StackBudget budget;
    private readonly UnitOperationServices services;
    private IUnitRequestChannel? channel;
    private SessionActivity? activity;
    private bool started, remote, timedOut, polling, cancelRequested, stackInitialized, anyTimeout;
    private int savedNext = -1;
    private uint startedAt;
    public UnitOperationResult? Result { get; private set; }
    public UnitOperation(SessionRuntime runtime, UnitRequest request, bool notify, StackBudget budget, UnitOperationServices services, IUnitRequestChannel? channel = null)
        : this(runtime.World, runtime.Process.Rules, runtime.Publish, request, notify, budget, services, channel)
    {
        try { activity = runtime.BeginActivity(); } catch { Cancel(); throw; }
    }
    public UnitOperation(OperationContext context, UnitRequest request, bool notify, StackBudget budget, UnitOperationServices services, IUnitRequestChannel? channel = null)
        : this(context.World, context.Process.Rules, context.Publish, request, notify, budget, services, channel) { }
    private UnitOperation(GameState world, RuleState rules, Action publish, UnitRequest request, bool notify, StackBudget budget, UnitOperationServices services, IUnitRequestChannel? channel)
    {
        this.world = world; this.rules = rules; this.publish = publish; this.request = request; this.notify = notify; this.services = services; this.channel = channel;
        try
        {
            ArgumentNullException.ThrowIfNull(world); ArgumentNullException.ThrowIfNull(rules); ArgumentNullException.ThrowIfNull(publish); ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(budget); ArgumentNullException.ThrowIfNull(services); ArgumentNullException.ThrowIfNull(request.Arguments);
            if (request.Operation is < UnitOperationKind.Detach or > UnitOperationKind.RelocateStack || request.Arguments.Count != 6) throw new ArgumentException("Unknown unit operation or invalid payload");
            this.budget = new(budget.Steps, budget.ValidationDepth); stack = request.Operation == UnitOperationKind.RelocateStack;
            var kind = stack ? UnitOperationKind.Relocate : request.Operation; int used = kind is UnitOperationKind.Attach or UnitOperationKind.Relocate ? 3 : 1;
            this.request = new(kind, new(request.Arguments.Select((value, i) => i < used ? value : 0))); remote = IsRemote;
            RequireRemote();
            if (stack && notify && world.Globals.Multiplayer >= GameMode.Tcp && services.Placement is null) throw new ArgumentException("Stack relocation requires synchronization");
            if (!remote)
            {
                if (kind == UnitOperationKind.Erase && services.Deletion is null) throw new ArgumentException("Unit deletion requires its effect provider");
                if (kind != UnitOperationKind.Erase && notify && world.Globals.Multiplayer >= GameMode.Tcp && services.Placement is null) throw new ArgumentException("Placement requires synchronization");
            }
        }
        catch { Cancel(); throw; }
    }
    private bool IsRemote => world.Globals.Multiplayer >= GameMode.Tcp && !world.Multiplayer.NetworkHost;
    private void RequireRemote() { if (remote && (channel is null || services.Ticks is null || services.TimedOut is null)) throw new ArgumentException("Remote unit operation requires channel, clock and timeout service"); }
    private ref bool Active()
    {
        switch (request.Operation)
        {
            case UnitOperationKind.Detach: return ref world.UnitStacks.DetachActive;
            case UnitOperationKind.Attach: return ref world.UnitStacks.AttachActive;
            case UnitOperationKind.Relocate: return ref world.UnitStacks.RelocateActive;
            case UnitOperationKind.Bottom: return ref world.UnitStacks.BottomActive;
            case UnitOperationKind.Erase: return ref world.UnitDeletion.Active;
            default: throw new InvalidOperationException("Composite request was not reduced");
        }
    }
    private ref int Reply()
    {
        switch (request.Operation)
        {
            case UnitOperationKind.Detach: return ref world.UnitStacks.DetachReply;
            case UnitOperationKind.Attach: return ref world.UnitStacks.AttachReply;
            case UnitOperationKind.Relocate: return ref world.UnitStacks.RelocateReply;
            case UnitOperationKind.Bottom: return ref world.UnitStacks.BottomReply;
            case UnitOperationKind.Erase: return ref world.UnitDeletion.Reply;
            default: throw new InvalidOperationException("Composite request was not reduced");
        }
    }
    private ref Unit BackedUnit(int slot)
    {
        if (slot < 0 || slot >= world.Entities.Units.Storage.Length) throw new ArgumentOutOfRangeException(nameof(slot));
        return ref world.Entities.Units.Storage[slot];
    }
    private void Finish(UnitOperationResult result)
    {
        Result = result; var owned = channel; channel = null;
        try { owned?.Cancel(); }
        catch { /* The channel contract is nonthrowing; ownership still ends. */ }
        finally { activity?.Dispose(); activity = null; }
    }
    private UnitOperationResult? CompleteChild(UnitOperationResult result)
    {
        anyTimeout |= result.TimedOut;
        if (stack && savedNext >= 0)
        {
            var arguments = request.Arguments.ToArray(); arguments[0] = savedNext; request = request with { Arguments = new(arguments) }; started = false; timedOut = false; return null;
        }
        if (stack && notify && world.Globals.Multiplayer >= GameMode.Tcp)
        { (services.Placement ?? throw new ArgumentException("Stack relocation requires synchronization"))(); publish(); }
        Finish(result with { TimedOut = anyTimeout }); return Result;
    }
    public void Cancel()
    {
        if (polling) { cancelRequested = true; return; }
        if (!Result.HasValue) Finish(new(-2, timedOut || anyTimeout, true));
    }
    public void Dispose() => Cancel();
    private bool Cancelled()
    {
        if (!cancelRequested) return false; Finish(new(-2, timedOut || anyTimeout, true)); return true;
    }
    public UnitOperationResult? Poll()
    {
        if (polling) throw new InvalidOperationException("Unit operation poll cannot reenter");
        if (Result.HasValue) return Result; polling = true;
        try
        {
            if (!started && stack)
            {
                var traversal = new StackOperations(world, budget, services.Diagnostics, services.Placement);
                if (!stackInitialized)
                {
                    var arguments = request.Arguments.ToArray(); arguments[0] = traversal.First(arguments[0]); request = request with { Arguments = new(arguments) }; stackInitialized = true;
                }
                if (request.Arguments[0] < 0) return CompleteChild(new());
                savedNext = traversal.Next(request.Arguments[0]); remote = IsRemote; RequireRemote();
            }
            int slot = request.Arguments[0];
            if (!started)
            {
                if (request.Operation == UnitOperationKind.Erase && (slot < 0 || !BackedUnit(slot).Token.Occupied) || request.Operation == UnitOperationKind.Bottom && BackedUnit(slot).Next.Value < 0)
                { Finish(new()); return Result; }
                started = true;
                if (!remote)
                {
                    var placement = new StackOperations(world, budget, services.Diagnostics, services.Placement); var position = new Position(request.Arguments[1], request.Arguments[2]);
                    switch (request.Operation)
                    {
                        case UnitOperationKind.Detach: placement.Detach(slot, notify); break;
                        case UnitOperationKind.Attach: placement.AttachAtFront(slot, position, notify); break;
                        case UnitOperationKind.Relocate: placement.Relocate(slot, position, notify && !stack); break;
                        case UnitOperationKind.Bottom: placement.MoveToBottom(slot, notify); break;
                        case UnitOperationKind.Erase: new UnitDeletion(world, rules.Definitions, budget, services.Deletion!, services.Diagnostics).Erase(slot, notify); break;
                        default: throw new InvalidOperationException("Composite request was not reduced");
                    }
                    publish(); return CompleteChild(new());
                }
                Active() = false; Reply() = -2; publish(); channel!.Send(request); if (Cancelled()) return Result;
                startedAt = services.Ticks!(); if (Cancelled()) return Result; return null;
            }
            if (!timedOut && Reply() == -2 && unchecked((int)(services.Ticks!() - startedAt)) >= 3600)
            {
                timedOut = true; services.TimedOut!(); world.TurnLoopActive = false; if (Cancelled()) return Result;
            }
            if (Cancelled()) return Result;
            var incoming = channel!.Poll(!timedOut && Reply() == -2); if (Cancelled()) return Result;
            if (incoming.Value.HasValue) Reply() = incoming.Value.Value; publish();
            if ((Reply() != -2 || timedOut) && incoming.Drained) return CompleteChild(new(Reply(), timedOut));
            return Result;
        }
        catch { try { publish(); } catch { } Cancel(); throw; }
        finally { polling = false; if (cancelRequested) Cancel(); }
    }
}
