using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.IO;

namespace OpenCiv2.Session;

public sealed record MultiplayerOptionServices(IFileStorage Preferences, Func<uint> Clock,
    Func<string> LocalPlayerName, Func<string> Separator, Func<bool, string> ProductionLabel,
    Func<bool, string> MovementLabel, IOptionBroadcaster Broadcaster, Action<string> Notice, Action<int> RedrawStatus)
{
    internal void Validate()
    {
        ArgumentNullException.ThrowIfNull(Preferences);
        ArgumentNullException.ThrowIfNull(Clock);
        ArgumentNullException.ThrowIfNull(LocalPlayerName);
        ArgumentNullException.ThrowIfNull(Separator);
        ArgumentNullException.ThrowIfNull(ProductionLabel);
        ArgumentNullException.ThrowIfNull(MovementLabel);
        ArgumentNullException.ThrowIfNull(Broadcaster);
        ArgumentNullException.ThrowIfNull(Notice);
        ArgumentNullException.ThrowIfNull(RedrawStatus);
    }
}

/// <summary>F03 §8 / N03 §11: accepted chat preferences precede host/change gates. Votes yield
/// between Begin and Poll, hold an explicit world pin, and preserve completed effects on failure.
/// The channel is transferred to this operation, including failed admission; providers outlive it.
/// Proposal text is owned logical text, independent of presentation parameter scratch.</summary>
public sealed class MultiplayerOptions : IDisposable
{
    private readonly SessionRuntime runtime;
    private readonly MultiplayerOptionChoices choices;
    private readonly MultiplayerOptionServices services;
    private IOptionVoteChannel? channel;
    private SessionActivity? activity;
    private bool started, polling, cancelRequested, chosen;
    public OptionProposal? Proposal { get; private set; }
    public MultiplayerOptionResult? Result { get; private set; }

    public MultiplayerOptions(SessionRuntime runtime, int dialogResult, MultiplayerOptionChoices choices,
        MultiplayerOptionServices services, IOptionVoteChannel? channel = null)
    {
        this.runtime = runtime;
        this.choices = choices;
        this.services = services;
        this.channel = channel;
        try
        {
            if (dialogResult != 0)
            {
                Finish(MultiplayerOptionResult.Cancelled);
                return;
            }
            ArgumentNullException.ThrowIfNull(runtime);
            ArgumentNullException.ThrowIfNull(choices);
            ArgumentNullException.ThrowIfNull(services);
            services.Validate();
            ref var live = ref runtime.World.Multiplayer;
            bool changed = live.Saved.DoubleProduction != choices.DoubleProduction
                || live.Saved.DoubleMovement != choices.DoubleMovement;
            if (changed && runtime.World.Globals.Multiplayer >= GameMode.Tcp && live.NetworkHost && live.PlayerCount > 1
                && channel is null)
            {
                throw new ArgumentException("Multiplayer proposal requires a vote channel", nameof(channel));
            }

            activity = runtime.BeginActivity();
        }
        catch
        {
            Finish(MultiplayerOptionResult.Failed);
            throw;
        }
    }

    private void Publish()
    {
        if (runtime.Active)
        {
            runtime.Publish();
        }
    }

    private void Finish(MultiplayerOptionResult result)
    {
        Result = result;
        var owned = channel;
        channel = null;
        // Cleanup cannot replace a preceding child error or retain world ownership.
        try
        {
            owned?.Cancel();
        }
        catch { /* Nonthrowing channel contract. */ }
        try
        {
            owned?.Dispose();
        }
        catch { /* Nonthrowing channel contract. */ }
        activity?.Dispose();
        activity = null;
    }

    private void RestorePreferences()
    {
        ref var remembered = ref runtime.Process.MultiplayerPreferences;
        ref var live = ref runtime.World.Multiplayer.Saved;
        remembered.DoubleProduction = live.DoubleProduction;
        remembered.DoubleMovement = live.DoubleMovement;
    }

    private void Reject()
    {
        RestorePreferences();
        Publish();
        services.Broadcaster.Rejected();
        Publish();
        services.Notice("PMCHANGENO");
        Finish(MultiplayerOptionResult.Rejected);
    }

    private void CancelNow()
    {
        if (Proposal is not null)
        {
            Reject();
        }
        else
        {
            if (chosen)
            {
                RestorePreferences();
            }

            Finish(MultiplayerOptionResult.Cancelled);
        }
    }

    private bool Cancelled()
    {
        if (!cancelRequested)
        {
            return false;
        }

        CancelNow();
        return true;
    }

    public void Cancel()
    {
        if (Result.HasValue)
        {
            return;
        }

        if (polling)
        {
            cancelRequested = true;
            return;
        }
        polling = true;
        try
        {
            CancelNow();
        }
        catch
        {
            Finish(MultiplayerOptionResult.Failed);
            throw;
        }
        finally
        {
            polling = false;
        }
    }

    public void Dispose() => Cancel();

    private void Apply()
    {
        var world = runtime.World;
        ref var live = ref world.Multiplayer.Saved;
        ref var remembered = ref runtime.Process.MultiplayerPreferences;
        live.DoubleProduction = remembered.DoubleProduction;
        live.DoubleMovement = remembered.DoubleMovement;
        DoubleMovement.Apply(world.Globals.Multiplayer, live.DoubleMovement, runtime.Process.Rules, value =>
        {
            Publish();
            services.RedrawStatus(value);
        });
        if (world.Globals.Multiplayer >= GameMode.Tcp && world.Multiplayer.PlayerCount > 1)
        {
            Publish();
            services.Broadcaster.Units();
            Publish();
            services.Broadcaster.Options(live.DoubleProduction, live.DoubleMovement, live.Simultaneous);
            Publish();
            services.Broadcaster.Accepted();
            Publish();
            services.Notice("PMCHANGEYES");
        }
        Publish();
        Finish(MultiplayerOptionResult.Applied);
    }

    private static string Text(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        int end = value.IndexOf('\0');
        return end < 0 ? value : value[..end];
    }

    public MultiplayerOptionResult? Poll()
    {
        if (polling)
        {
            throw new InvalidOperationException("Cannot reenter an option vote");
        }

        if (Result.HasValue)
        {
            return Result;
        }

        polling = true;
        try
        {
            ref var remembered = ref runtime.Process.MultiplayerPreferences;
            var world = runtime.World;
            ref var multiplayer = ref world.Multiplayer;
            ref var live = ref multiplayer.Saved;
            ref var vote = ref multiplayer.Vote;
            if (!started)
            {
                started = true;
                remembered.ClearChatNewGame = choices.ClearChatOnNewGame;
                remembered.ClearChatBoot = choices.ClearChatOnBoot;
                _ = PreferenceFiles.Save(runtime, services.Preferences);
                if (Cancelled())
                {
                    return Result;
                }

                if (world.Globals.Multiplayer >= GameMode.Tcp && !multiplayer.NetworkHost)
                {
                    Finish(MultiplayerOptionResult.ClientOnly);
                    return Result;
                }
                remembered.DoubleProduction = choices.DoubleProduction;
                remembered.DoubleMovement = choices.DoubleMovement;
                chosen = true;
                if (remembered.DoubleProduction == live.DoubleProduction
                    && remembered.DoubleMovement == live.DoubleMovement)
                {
                    Finish(MultiplayerOptionResult.Unchanged);
                    return Result;
                }
                if (world.Globals.Multiplayer >= GameMode.Tcp && multiplayer.PlayerCount > 1)
                {
                    if (channel is null)
                    {
                        throw new InvalidOperationException("Multiplayer proposal requires a vote channel");
                    }

                    Publish();
                    string sender = Text(services.LocalPlayerName());
                    Publish();
                    string description = Text(services.ProductionLabel(remembered.DoubleProduction));
                    Publish();
                    description += Text(services.Separator());
                    Publish();
                    description += Text(services.MovementLabel(remembered.DoubleMovement));
                    if (Cancelled())
                    {
                        return Result;
                    }

                    vote.Pending = unchecked(multiplayer.PlayerCount - 1);
                    vote.Status = 0;
                    Proposal = new(
                        sender,
                        description,
                        remembered.DoubleProduction,
                        remembered.DoubleMovement,
                        vote.Pending);
                    Publish();
                    channel.Begin(Proposal);
                    if (Cancelled())
                    {
                        return Result;
                    }

                    Publish();
                    vote.PopupTick = services.Clock();
                    if (Cancelled())
                    {
                        return Result;
                    }

                    return null;
                }
                Apply();
                return Result;
            }
            Publish();
            var progress = channel!.Poll();
            if (Cancelled())
            {
                return Result;
            }

            vote.Pending = progress.Pending;
            vote.Status = progress.Status;
            if (!multiplayer.Handoff)
            {
                Publish();
                uint now = services.Clock();
                if (Cancelled())
                {
                    return Result;
                }

                if (unchecked((int)(now - vote.PopupTick)) < 2401 && vote.Pending != 0 && vote.Status != -1)
                {
                    return null;
                }
            }
            Publish();
            vote.PopupTick = services.Clock();
            if (Cancelled())
            {
                return Result;
            }

            if (vote.Status == -1 || vote.Pending != 0)
            {
                Reject();
            }
            else
            {
                Apply();
            }

            return Result;
        }
        catch
        {
            try
            {
                Publish();
            }
            catch { }

            Finish(MultiplayerOptionResult.Failed);
            throw;
        }
        finally
        {
            polling = false;
        }
    }
}
