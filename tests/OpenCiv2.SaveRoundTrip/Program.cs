using System.Text.Json;
using OpenCiv2.Formats;
using OpenCiv2.IO.Native;

try
{
    if (args.Length is not (1 or 2)) throw new ArgumentException("Usage: oc2_save_roundtrip INPUT.SAV [NEW_OUTPUT.SAV]");
    byte[] bytes = NativeFiles.ReadFile(args[0]);
    var save = ClassicFiles.DecodeStoredSave(bytes);
    byte[] encoded = ClassicFiles.EncodeStoredSave(save);
    if (!bytes.SequenceEqual(encoded)) throw new InvalidOperationException("Decoded/encoded bytes differ");
    if (save != ClassicFiles.DecodeStoredSave(encoded)) throw new InvalidOperationException("Decoded/encoded auxiliary values differ");
    if (args.Length == 2) NativeFiles.WriteNewFile(args[1], encoded);
    using var stream = StandardOutput.Open();
    using var w = new Utf8JsonWriter(stream);
    w.WriteStartObject(); w.WriteNumber("version", save.Header.Version); w.WriteNumber("width", save.Map.Header.Width);
    w.WriteNumber("height", save.Map.Header.Height); w.WriteNumber("unitSlots", save.Units.Count); w.WriteNumber("citySlots", save.Cities.Count);
    w.WriteNumber("mode", save.Globals.Multiplayer); w.WriteBoolean("scenario", save.Scenario is not null);
    w.WriteBoolean("eventBlock", save.Events is not null); w.WriteNumber("events", save.Events?.Count ?? 0);
    w.WriteNumber("trailingBytes", save.Trailing.Count); w.WriteBoolean("byteEqual", true); w.WriteBoolean("orderedAuxiliaryEqual", true);
    w.WriteBoolean("outputWritten", args.Length == 2); w.WriteEndObject(); w.Flush();
    return 0;
}
catch (Exception error) { Console.Error.WriteLine(error.Message); return 1; }
