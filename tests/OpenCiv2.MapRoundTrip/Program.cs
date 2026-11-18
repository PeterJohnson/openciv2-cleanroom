using System.Text.Json;
using OpenCiv2.Formats;
using OpenCiv2.IO.Native;

try
{
    if (args.Length != 2) throw new ArgumentException("Usage: oc2_map_roundtrip INPUT.MP NEW_OUTPUT.MP");
    if (File.Exists(args[1]) || Directory.Exists(args[1])) throw new ArgumentException("Output must be a new path");
    byte[] bytes = NativeFiles.ReadFile(args[0]);
    var map = ClassicFiles.DecodeRawMap(bytes); byte[] encoded = ClassicFiles.EncodeRawMap(map);
    if (!bytes.SequenceEqual(encoded)) throw new InvalidOperationException("Decoded/encoded bytes differ");
    NativeFiles.WriteNewFile(args[1], encoded);
    using var stream = StandardOutput.Open(); using var writer = new Utf8JsonWriter(stream);
    writer.WriteStartObject(); writer.WriteNumber("width", map.Header.Width); writer.WriteNumber("height", map.Header.Height);
    writer.WriteNumber("tiles", map.Tiles.Count); writer.WriteNumber("trailingBytes", map.Trailing.Count); writer.WriteBoolean("byteEqual", true);
    writer.WriteEndObject(); writer.Flush(); return 0;
}
catch (Exception error) { Console.Error.WriteLine(error.Message); return 1; }
