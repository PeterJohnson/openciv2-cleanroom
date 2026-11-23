using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Formats;

namespace OpenCiv2.Session;

/// <summary>Opt-in, bounded recording of one local game's operation lane. Exhausting the journal
/// leaves the game playable and invalidates the export, including the already recorded prefix.</summary>
public sealed class LocalReplayRecorder(string scriptSha256)
{
    private readonly List<LocalReplayStep> steps = [];
    private int remaining = LocalCheckpointCodec.MaximumPayload - 4096;
    private bool overflow;
    internal int Count => steps.Count;
    internal LocalReplayStep Last => steps[^1];

    public LocalReplayTape Capture() => overflow
        ? throw new InvalidOperationException("Replay recording exceeded its limits.")
        : new(scriptSha256, new(steps));

    private bool Reserve(int bytes)
    {
        if (overflow || steps.Count == LocalCheckpointCodec.MaximumItems || bytes > remaining)
        {
            overflow = true;
            return false;
        }
        remaining -= bytes;
        return true;
    }

    internal void RecordLoad(ReadOnlyMemory<byte> bytes, LocalLoadResult result)
    {
        if (bytes.Length > LocalCheckpointCodec.MaximumOwnerBytes)
        {
            overflow = true;
        }
        else if (Reserve(512 + 2 * bytes.Length))
        {
            steps.Add(new LocalReplayStep.Load(new(bytes.ToArray()), result.ToString()));
        }
    }

    internal void Record(LocalReplayStep step)
    {
        if (overflow)
        {
            return;
        }
        int bytes = 512;
        bool Account(string? text)
        {
            if (text is null || text.Length > LocalCheckpointCodec.MaximumText)
            {
                overflow = true;
                return false;
            }
            bytes += 32 + 4 * text.Length;
            return bytes <= remaining;
        }

        bool valid = step switch
        {
            LocalReplayStep.Submit submit => Account(submit.Request.Name) && Account(submit.Code)
                && submit.Request.Arguments.Count <= LocalCheckpointCodec.MaximumItems
                && submit.Request.Arguments.All(arg => arg is not null && Account(arg.Name) && arg.Value is not null
                    && (arg.Value is not CommandValue.Text text || Account(text.Value))),
            LocalReplayStep.Advance advance => Account(advance.Code),
            LocalReplayStep.Reply reply => Account(reply.Receipt.Code),
            LocalReplayStep.TextReply reply => Account(reply.Text) && Account(reply.Receipt.Code),
            LocalReplayStep.Cancel cancel => Account(cancel.Receipt.Code),
            _ => true
        };
        if (!valid)
        {
            overflow = true;
        }
        else if (Reserve(bytes + (step is LocalReplayStep.Submit s ? 192 * s.Request.Arguments.Count : 0)))
        {
            steps.Add(step);
        }
    }
}
