using OpenCiv2.Contracts;

namespace OpenCiv2.Session;

/// <summary>Feeds a recorded input sequence through the ordinary local runtime without a script VM.</summary>
public static class LocalReplay
{
    public static byte[] Run(LocalReplayTape tape)
    {
        var observed = new LocalReplayRecorder(tape.ScriptSha256);
        using var game = new LocalGame(observed);
        Dictionary<ControllerKind, IPlayerClient> clients = [];
        IPlayerClient Client(ControllerKind kind)
        {
            if (kind == ControllerKind.Frontend)
            {
                return game.Player;
            }
            if (!clients.TryGetValue(kind, out var client))
            {
                client = game.Bind(kind);
                clients.Add(kind, client);
            }
            return client;
        }

        for (int i = 0; i < tape.Steps.Count; ++i)
        {
            var step = tape.Steps[i];
            switch (step)
            {
                case LocalReplayStep.Submit v:
                    Client(v.Controller).Submit(v.Request);
                    break;
                case LocalReplayStep.Reply v:
                    Client(v.Controller).Reply(v.Interaction, v.Choice);
                    break;
                case LocalReplayStep.TextReply v:
                    Client(v.Controller).Reply(v.Interaction, v.Choice, v.Text);
                    break;
                case LocalReplayStep.Cancel v:
                    Client(v.Controller).Cancel(v.Command);
                    break;
                case LocalReplayStep.Advance:
                    game.AdvanceOne();
                    break;
                case LocalReplayStep.Retire v:
                    game.RetireCommand(v.Command);
                    break;
                case LocalReplayStep.Save:
                    game.SaveClassic();
                    break;
                case LocalReplayStep.Load v:
                    if (game.LoadClassic(v.Bytes.ToArray()) == LocalLoadResult.Loaded)
                    {
                        clients.Clear();
                    }
                    break;
                default:
                    throw new ArgumentException("Unknown replay step", nameof(tape));
            }
            if (observed.Count != i + 1 || observed.Last != step)
            {
                throw new InvalidDataException($"Replay diverged at step {i + 1} ({step.GetType().Name}).");
            }
            game.Player.Updates();
            foreach (var client in clients.Values)
            {
                client.Updates();
            }
        }
        if (game.Busy)
        {
            throw new InvalidDataException("Replay ends with a pending command.");
        }
        return game.SaveClassic();
    }
}
