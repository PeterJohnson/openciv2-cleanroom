using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.IO;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static void ExplicitByteImages()
    {
        var writer = new ByteWriter();
        writer.I8(-1); writer.I16(short.MinValue); writer.I32(int.MinValue);
        writer.U64(0xfedcba9876543210); writer.Append([0, 0xff]);
        byte[] expected = [0xff, 0, 0x80, 0, 0, 0, 0x80, 0x10, 0x32, 0x54, 0x76, 0x98, 0xba, 0xdc, 0xfe, 0, 0xff];
        Equal(true, expected.SequenceEqual(writer.ToArray()));
        byte[] output = writer.ToArray(); output[0] = 0;
        Equal((byte)0xff, writer.ToArray()[0]);
        var reader = new ByteReader(expected);
        Equal((sbyte)-1, reader.I8()); Equal(short.MinValue, reader.I16()); Equal(int.MinValue, reader.I32());
        Equal(0xfedcba9876543210UL, reader.U64());
        Equal(true, reader.Take(2).SequenceEqual(new byte[] { 0, 0xff }));
        Equal(expected.Length, reader.Position); Equal(0, reader.Remaining.Length);
    }

    private static void TruncationOffsets()
    {
        for (int size = 0; size < 8; ++size)
        {
            var reader = new ByteReader(new byte[size + 1]);
            reader.U8();
            try { reader.U64(); throw new InvalidOperationException("Truncated u64 was accepted."); }
            catch (DecodeException error) { Equal(1, error.Offset); Equal(1, reader.Position); }
        }
        var shortReader = new ByteReader(new byte[3]);
        shortReader.U16();
        try { shortReader.U16(); throw new InvalidOperationException("Truncated u16 was accepted."); }
        catch (DecodeException error) { Equal(2, error.Offset); Equal(2, shortReader.Position); }
        Equal((byte)0, shortReader.U8());
    }

    private sealed class TestInput(bool throwsOnClose = false) : CoreInput
    {
        public int Closes { get; private set; }
        public int Reads { get; private set; }
        protected override ReadResult ReadItem(int size) { ++Reads; return new(new(new byte[] { 0x80, 0xff }.Take(size)), size <= 2); }
        protected override byte? ReadByte() { ++Reads; return null; }
        protected override bool Finish()
        {
            ++Closes;
            if (throwsOnClose) throw new IOException("close failed");
            return false;
        }
    }

    private static void CloseOnceAndShortPrefix()
    {
        var file = new TestInput();
        var partial = file.Item(3);
        Equal(false, partial.Complete); Equal(2, partial.Prefix.Count); Equal((byte)0xff, partial.Prefix[1]);
        Equal(true, file.Byte() is null);
        Equal(false, file.Close()); Equal(false, file.Close()); file.Dispose();
        Equal(1, file.Closes);
        Throws<ObjectDisposedException>(() => file.Item(1));
        Throws<ObjectDisposedException>(() => file.Byte()); Equal(2, file.Reads);
        var failing = new TestInput(true);
        Throws<IOException>(() => failing.Close());
        Equal(false, failing.Close()); failing.Dispose(); Equal(1, failing.Closes);
        var cleanup = new TestInput(true);
        try { using (cleanup) throw new ArgumentException("original"); }
        catch (ArgumentException error) { Equal("original", error.Message); }
        Equal(1, cleanup.Closes);
    }

    private static void DescriptorValidation()
    {
        var descriptor = new CommandDescriptor(1, "unit.move", "Move a unit", AccessClass.Player,
            new([new("unit", ValueKind.Unit), new("steps", ValueKind.Integer), new("optional", ValueKind.Text, false)]), new([]));
        descriptor.Validate();
        NamedValue unit = new("unit", new CommandValue.Unit(new(new(2047), new(uint.MaxValue))));
        NamedValue steps = new("steps", new CommandValue.Integer(-1));
        Equal(true, CommandDescriptor.ValidateValues(descriptor.Arguments, [unit, steps]) is null);
        Equal("duplicate_value:unit", CommandDescriptor.ValidateValues(descriptor.Arguments, [unit, unit])!);
        Equal("unknown_value:other", CommandDescriptor.ValidateValues(descriptor.Arguments, [steps with { Name = "other" }])!);
        Equal("wrong_type:steps", CommandDescriptor.ValidateValues(descriptor.Arguments, [new("steps", new CommandValue.Boolean(true))])!);
        Equal("missing_value:unit", CommandDescriptor.ValidateValues(descriptor.Arguments, [steps])!);
        Equal("invalid_unit_slot:unit", CommandDescriptor.ValidateValues(descriptor.Arguments,
            [new("unit", new CommandValue.Unit(new(new(2048), new(0))))])!);
        Equal("invalid_city_slot:city", CommandDescriptor.ValidateValues([new("city", ValueKind.City)],
            [new("city", new CommandValue.City(new(new(256), new(0))))])!);
        Equal("invalid_civilization:civ", CommandDescriptor.ValidateValues([new("civ", ValueKind.Civilization)],
            [new("civ", new CommandValue.Civilization(new(8)))])!);
        foreach (var bad in new[] { descriptor with { Version = 2 }, descriptor with { Name = "bad/name" },
            descriptor with { Help = "" }, descriptor with { Access = (AccessClass)9 },
            descriptor with { Arguments = new([new("x"), new("x")]) },
            descriptor with { Result = new([new("x", (ValueKind)99)]) } })
            Throws<ArgumentException>(bad.Validate);
        Throws<KeyNotFoundException>(() => CommandDescriptor.Value([unit], "missing"));
        Equal(true, CommandStatus.Completed.Terminal()); Equal(false, CommandStatus.AwaitingInteraction.Terminal());
    }

    private static void OwnedObservations()
    {
        NamedValue[] input = [new("name", new CommandValue.Text("before"))];
        var request = new CommandRequest("test", new(input));
        input[0] = input[0] with { Value = new CommandValue.Text("after") };
        Equal("before", ((CommandValue.Text)request.Arguments[0].Value).Value);
        int[] choices = [1, 2];
        var interaction = new InteractionSpec("select", null, null, new(choices));
        choices[0] = 3;
        Equal(1, interaction.Choices[0]);
        byte[] classic = [0xff, 0x80];
        var names = new SetupNames(new(classic), new([]), new([]));
        classic[0] = 0;
        Equal((byte)0xff, names.Leader[0]);
        Throws<ArgumentException>(() => new GovernmentTitles([]));
        Equal(false, new UnitToken(0).Occupied); Equal(true, new CityToken(uint.MaxValue).Occupied);
    }
}
