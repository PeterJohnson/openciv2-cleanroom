using System.Text.Json;
using OpenCiv2.Core.State;
using OpenCiv2.Formats;
using OpenCiv2.IO;
using OpenCiv2.IO.Native;
using OpenCiv2.Session;

try
{
    if (args.Length is not (1 or 2)) throw new ArgumentException("Usage: oc2_preference_roundtrip INPUT.DAT [NEW_OUTPUT.DAT]");
    byte[] bytes = NativeFiles.ReadFile(args[0]);
    const int size = ClassicPreferences.OrdinarySize + ClassicPreferences.MultiplayerSize;
    if (bytes.Length < size) throw new ArgumentException("Comparison requires two complete preference items");
    string path = Path.GetFullPath(args[0]);
    var storage = new ObservedStorage(new ClassicStorage(Path.GetDirectoryName(path)!), ClassicStorage.ClassicPath(path));
    var process = new ProcessContext();
    if (PreferenceFiles.Load(process, new(), storage) != PreferenceLoadStatus.Success || !storage.Complete || storage.Items != 2)
        throw new InvalidOperationException("Comparison requires two successful item reads and finalization");
    var ordinary = ClassicPreferences.DecodeOrdinary(bytes.AsSpan(0, ClassicPreferences.OrdinarySize));
    var multiplayer = ClassicPreferences.DecodeMultiplayer(bytes.AsSpan(ClassicPreferences.OrdinarySize, ClassicPreferences.MultiplayerSize));
    if (process.OrdinaryPreferences != StateConversion.ToLive(ordinary) || process.MultiplayerPreferences != StateConversion.ToLive(multiplayer))
        throw new InvalidOperationException("Preference admission differs from decoded values");
    byte[] encoded = [.. ClassicPreferences.EncodeOrdinary(ordinary), .. ClassicPreferences.EncodeMultiplayer(multiplayer)];
    if (!encoded.AsSpan().SequenceEqual(bytes.AsSpan(0, size))) throw new InvalidOperationException("Loaded and re-encoded preference items differ");
    if (args.Length == 2) NativeFiles.WriteNewFile(args[1], encoded);
    using var stream = StandardOutput.Open(); using var writer = new Utf8JsonWriter(stream);
    writer.WriteStartObject(); writer.WriteNumber("ordinaryBytes", ClassicPreferences.OrdinarySize); writer.WriteNumber("multiplayerBytes", ClassicPreferences.MultiplayerSize);
    writer.WriteNumber("trailingBytes", bytes.Length - size); writer.WriteNumber("options", ordinary.GameOptions); writer.WriteEndObject(); writer.Flush();
    return 0;
}
catch (Exception error) { Console.Error.WriteLine(error.Message); return 1; }

sealed class ObservedStorage(IFileStorage storage, string path) : IFileStorage
{
    internal bool Complete = true;
    internal int Items;
    public CoreInput? Read(string ignored) => new ObservedInput(storage.Read(path) ?? throw new IOException("Comparison input could not be reopened"), this);
    public CoreOutput? Replace(string target) => throw new NotSupportedException();
    public bool Remove(string target) => throw new NotSupportedException();
    private sealed class ObservedInput(CoreInput input, ObservedStorage owner) : CoreInput
    {
        protected override ReadResult ReadItem(int size)
        { var result = input.Item(size); owner.Complete &= result.Complete; ++owner.Items; return result; }
        protected override byte? ReadByte() => input.Byte();
        protected override bool Finish() { try { return input.Close(); } finally { input.Dispose(); } }
    }
}
