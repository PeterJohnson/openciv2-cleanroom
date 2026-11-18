using System.Buffers.Binary;
using System.Text.Json;
using OpenCiv2.IO.Native;
using OpenCiv2.Network;

try
{
    bool datagram = args.Length == 2 && args[0] == "--datagram";
    if (args.Length != 1 && !datagram) throw new ArgumentException("Usage: oc2_wire_roundtrip [--datagram] INPUT.bin");
    byte[] bytes = NativeFiles.ReadFile(args[datagram ? 1 : 0]);
    if (bytes.Length == 0) throw new IOException("Empty wire input");
    int frames = 0, messages = 0; uint kind = 0;
    if (datagram)
    {
        var packet = Wire.DecodeDatagram(bytes); var message = Messages.Decode(packet.Payload.ToArray());
        byte[] payload = Messages.Encode(message); kind = message.Header.Kind;
        if (payload.Length > 512 || !Wire.EncodeDatagram(packet.Header, (uint)payload.Length, payload, new byte[bytes.Length]).SequenceEqual(bytes))
            throw new InvalidOperationException("Datagram reconstruction differs or exceeds nominal capacity");
    }
    else
    {
        foreach (int chunk in new[] { 1, bytes.Length })
        {
            var receiver = new FrameReceiver(); using var encoded = new MemoryStream(); frames = messages = 0;
            void VerifyFrame(FrameView frame)
            {
                encoded.Write(Wire.EncodeFrame(frame.Tag, frame.Payload, frame.Padding)); ++frames;
                if (frame.Payload.Length >= 4 && BinaryPrimitives.ReadUInt32LittleEndian(frame.Payload) == Messages.Magic)
                {
                    if (!Messages.Encode(Messages.Decode(frame.Payload)).AsSpan().SequenceEqual(frame.Payload))
                        throw new InvalidOperationException("Application message bytes differ");
                    ++messages;
                }
            }
            for (int offset = 0; offset < bytes.Length; offset += chunk)
                if (receiver.Feed(bytes.AsSpan(offset, Math.Min(chunk, bytes.Length - offset)), VerifyFrame).Stop != ReceiveStop.Exhausted)
                    throw new InvalidOperationException("Wire receiver rejected input");
            if (receiver.Incomplete || !encoded.ToArray().SequenceEqual(bytes)) throw new InvalidOperationException("Incomplete or differing reconstructed stream");
        }
    }
    using var stream = StandardOutput.Open(); using var writer = new Utf8JsonWriter(stream);
    writer.WriteStartObject();
    if (datagram) writer.WriteNumber("kind", kind);
    else { writer.WriteNumber("frames", frames); writer.WriteNumber("applicationMessages", messages); writer.WriteBoolean("oneByteAndCoalesced", true); }
    writer.WriteNumber("bytes", bytes.Length); writer.WriteBoolean("byteEqual", true); writer.WriteEndObject(); writer.Flush(); return 0;
}
catch (Exception error) { Console.Error.WriteLine(error.Message); return 1; }
