using System.Runtime.CompilerServices;
using OpenCiv2.Core.State;
using OpenCiv2.Formats;
using OpenCiv2.IO;

namespace OpenCiv2.Session;

public readonly record struct PreferenceSnapshot(OrdinaryPreferences Ordinary, MultiplayerPreferences Multiplayer)
{
    public StoredOrdinaryPreferences? ImportedOrdinary { get; init; }
}
public readonly record struct PreferenceDefaults(int ScreenWidth, bool AnimationsAvailable);
public enum PreferenceLoadStatus { Success, Defaults, Input }
public enum PreferenceSaveStatus { Success, Output }
public interface IPreferenceWriter
{
    bool Write(PreferenceSnapshot values, Action? beforeIo = null);
}
/// <summary>F03 production preference persistence. Both blocks are encoded before external I/O.
/// Failed writes/close preserve accepted configuration and remove partial output when possible.</summary>
public sealed class ClassicPreferenceWriter : IPreferenceWriter
{
    private readonly IFileStorage storage;

    public ClassicPreferenceWriter(IFileStorage storage)
    {
        ArgumentNullException.ThrowIfNull(storage);
        this.storage = storage;
    }

    private sealed class NotifiedOutput(CoreOutput output, Action? beforeIo) : CoreOutput
    {
        protected override bool WriteItem(ReadOnlySpan<byte> bytes)
        {
            beforeIo?.Invoke();
            return output.Item(bytes);
        }

        protected override bool Finish()
        {
            beforeIo?.Invoke();
            return output.Close();
        }
    }

    public bool Write(PreferenceSnapshot values, Action? beforeIo = null)
    {
        var stored = StateConversion.ToStored(values.Ordinary);
        if (values.ImportedOrdinary is { } imported)
        {
            stored = StateConversion.PreserveOpaque(stored, imported);
        }

        var ordinary = ClassicPreferences.EncodeOrdinary(stored);
        var multiplayer = ClassicPreferences.EncodeMultiplayer(StateConversion.ToStored(values.Multiplayer));
        beforeIo?.Invoke();
        using var file = storage.Replace("CIV2.DAT");
        if (file is null)
        {
            return false;
        }
        // The outer owner also disposes the underlying file if a notification throws during close.
        using var output = new NotifiedOutput(file, beforeIo);
        bool written = output.Item(ordinary) && output.Item(multiplayer);
        bool finalized = output.Close();
        if (written && finalized)
        {
            return true;
        }

        beforeIo?.Invoke();
        _ = storage.Remove("CIV2.DAT");
        return false;
    }
}
public static class PreferenceFiles
{
    private static readonly ConditionalWeakTable<object, StoredOrdinaryPreferences> imported = new();
    public static PreferenceSnapshot Capture(ProcessContext process) => new(process.OrdinaryPreferences, process.MultiplayerPreferences)
    { ImportedOrdinary = imported.TryGetValue(process.PreferencesIdentity, out var value) ? value : null };
    internal static void ReplaceImported(ProcessContext process, StoredOrdinaryPreferences? value)
    {
        imported.Remove(process.PreferencesIdentity);
        if (value is not null)
        {
            imported.Add(process.PreferencesIdentity, value);
        }
    }

    /// <summary>Stage both independent items and close before installation. Missing/short items default;
    /// close failure or exceptions leave installed preferences unchanged.</summary>
    public static PreferenceLoadStatus Load(ProcessContext process, PreferenceDefaults defaults, IFileStorage storage)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(storage);
        StoredOrdinaryPreferences? stored = null;
        var ordinary = process.OrdinaryPreferences;
        var multiplayer = process.MultiplayerPreferences;
        var status = PreferenceLoadStatus.Defaults;
        using var input = storage.Read("CIV2.DAT");
        if (input is null)
        {
            ordinary.SetDefaults(defaults.ScreenWidth, defaults.AnimationsAvailable);
            multiplayer.SetDefaults();
        }
        else
        {
            var first = input.Item(ClassicPreferences.OrdinarySize);
            var second = input.Item(ClassicPreferences.MultiplayerSize);
            if (!input.Close())
            {
                return PreferenceLoadStatus.Input;
            }

            if (first.Prefix.Count > ClassicPreferences.OrdinarySize
                || second.Prefix.Count > ClassicPreferences.MultiplayerSize)
            {
                throw new ArgumentException("Preference item exceeds its stored field size");
            }

            if (first.Complete)
            {
                stored = ClassicPreferences.DecodeOrdinary(first.Prefix.ToArray());
                ordinary = StateConversion.ToLive(stored);
            }
            else
            {
                ordinary.SetDefaults(defaults.ScreenWidth, defaults.AnimationsAvailable);
            }

            if (second.Complete)
            {
                multiplayer = StateConversion.ToLive(ClassicPreferences.DecodeMultiplayer(second.Prefix.ToArray()));
            }
            else
            {
                multiplayer.SetDefaults();
            }

            if (first.Complete && second.Complete)
            {
                status = PreferenceLoadStatus.Success;
            }
        }
        ReplaceImported(process, stored);
        process.OrdinaryPreferences = ordinary;
        process.MultiplayerPreferences = multiplayer;
        return status;
    }

    public static PreferenceSaveStatus Save(ProcessContext process, IFileStorage storage)
    {
        ArgumentNullException.ThrowIfNull(process);
        return new ClassicPreferenceWriter(
            storage).Write(
                Capture(process))
            ? PreferenceSaveStatus.Success
            : PreferenceSaveStatus.Output;
    }

    /// <summary>Publish before each external boundary; these services do not admit configuration mutations.</summary>
    public static PreferenceSaveStatus Save(SessionRuntime runtime, IFileStorage storage)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        var process = runtime.Process;
        return new ClassicPreferenceWriter(
            storage).Write(
                Capture(process),
                () =>
            {
                if (runtime.Active)
                {
                    runtime.Publish();
                }
            }) ? PreferenceSaveStatus.Success : PreferenceSaveStatus.Output;
    }
}
