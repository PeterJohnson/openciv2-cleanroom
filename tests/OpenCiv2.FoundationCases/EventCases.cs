using OpenCiv2.Base;
using OpenCiv2.Core.State;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // S02 §2: authored alias-sensitive inputs, not native-pointer/reference traces.
    private static Event AddEvent(EventCollection collection, string text)
    {
        var result = collection.Append(); result.Values.Actions = 0x80000021; result.Values.WhoCiv = -2; result.Values.MoneyAmount = int.MinValue;
        result.SetString(0, new(System.Text.Encoding.ASCII.GetBytes(text))); return result;
    }
    private static void EventPresence()
    {
        EventString absent = default; Equal(false, absent.Present); Throws<ArgumentOutOfRangeException>(() => _ = absent.Value);
        var empty = new EventString([]); Equal(true, empty.Present); Equal(0, empty.Value.Count); Equal(false, empty == absent);
        var text = new EventString([0xe9, 0, 1]); Equal(new OwnedList<byte>([0xe9]), text.Value); var alias = text; alias.Value = new([0x80, 0, 0x81]);
        Equal(text, alias); Equal(new OwnedList<byte>([0x80, 0, 0x81]), text.Value); var duplicate = new EventString(text.Value); Equal(new OwnedList<byte>([0x80]), duplicate.Value); Equal(false, duplicate == text);
        var state = new EventState { Loaded = true }; Equal(true, state.Collection.Pristine); var first = state.Collection.Append(); Equal(1UL, first.Identity); Equal(new EventValues(), first.Values);
        Equal(true, first.Strings.ToArray().All(s => !s.Present)); Throws<IndexOutOfRangeException>(() => first.SetString(35, text));
        state.Collection.Reset(); Equal(true, state.Loaded); Equal(false, state.Collection.Pristine); Equal(false, state.Collection.Contains(first)); Equal(true, state.Collection.Find(1) is null); Equal(2UL, state.Collection.Append().Identity);
        var other = new EventCollection(); Equal(1UL, other.Append().Identity); Equal(false, other.Contains(first)); Equal(false, other.Contains(null));
    }
    private static void EventCopyAliases()
    {
        var source = new EventCollection(); var destination = new EventCollection(); var a = AddEvent(source, "Shared");
        a.SetString(1, a.Strings[0]); a.SetString(4, a.Strings[0]); a.SetString(6, a.Strings[0]); a.SetString(34, new(Enumerable.Repeat((byte)'W', 300)));
        destination.CopyFrom(source); var b = destination.At(0); Equal(a.Values, b.Values); Equal(a.Strings[0].Value, b.Strings[0].Value);
        Equal(false, b.Strings[0] == a.Strings[0]); Equal(false, b.Strings[1] == b.Strings[0]); Equal(false, b.Strings[4] == b.Strings[0]); Equal(true, b.Strings[6] == a.Strings[0]); Equal(300, b.Strings[34].Value.Count);
        a.Strings[0].Value = new("!hared"u8.ToArray()); Equal(new OwnedList<byte>("Shared"u8.ToArray()), b.Strings[0].Value); Equal(new OwnedList<byte>("!hared"u8.ToArray()), b.Strings[6].Value);
        a.Strings[0].Value = new([65, 0, 66]); var terminated = new EventCollection(); terminated.CopyFrom(source); Equal(new OwnedList<byte>([65]), terminated.At(0).Strings[0].Value);
        Throws<ArgumentException>(() => source.CopyFrom(source)); Equal(1, source.Count);
        b.Values.MoveRectangle[7] = 34; Equal(0, a.Values.MoveRectangle[7]);
    }
    private static void EventCopyNonempty()
    {
        var source = new EventCollection(); var destination = new EventCollection(); var original = AddEvent(source, "Source"); var old = AddEvent(destination, "Destination"); old.Values.Turn = 99;
        old.SetString(1, new("Retained"u8.ToArray())); destination.CopyFrom(source); Equal(2, destination.Count); Equal(true, ReferenceEquals(old, destination.At(0))); Equal(99, old.Values.Turn);
        Equal(new OwnedList<byte>("Source"u8.ToArray()), old.Strings[0].Value); Equal(false, old.Strings[0] == original.Strings[0]); Equal(new OwnedList<byte>("Retained"u8.ToArray()), old.Strings[1].Value);
        Equal(original.Strings[0], destination.At(1).Strings[0]); Equal(original.Values, destination.At(1).Values);
    }
    private static void EventCompactIdentity()
    {
        var collection = new EventCollection(); var original = AddEvent(collection, "Old"); original.SetString(6, original.Strings[0]); var oldString = original.Strings[0]; var values = original.Values;
        collection.Compact(); Equal(true, ReferenceEquals(original, collection.Find(original.Identity))); Equal(values, original.Values); Equal(false, oldString == original.Strings[0]); Equal(oldString, original.Strings[6]);
        var compacted = original.Strings[0]; collection.Compact(); Equal(false, compacted == original.Strings[0]); Equal(oldString, original.Strings[6]); Equal(2UL, collection.Append().Identity);
    }
    private static void EventInstallation()
    {
        var collection = new EventCollection(); var original = collection.Append(); original.Values.Turn = 19;
        var strings = new OwnedList<byte>?[35]; byte[] source = [0xe9, 0, 0xff]; strings[0] = new(source); strings[1] = OwnedList<byte>.Empty;
        var values = new EventValues { Turn = 27 }; values.CreateLocations[19] = 123;
        var input = new OwnedList<EventInput>([new(values, new(strings))]); source[0] = 0; strings[0] = null;
        collection.Install(input); var installed = collection.At(0); Equal(2UL, installed.Identity); Equal(false, collection.Contains(original)); Equal(true, collection.Find(1) is null); Equal(values, installed.Values);
        Equal(new OwnedList<byte>([0xe9]), installed.Strings[0].Value); Equal(true, installed.Strings[1].Present); Equal(false, installed.Strings[2].Present);
        Throws<ArgumentException>(() => collection.Install(new([input[0], new(new(), new(new OwnedList<byte>?[34]))])));
        Equal(1, collection.Count); Equal(true, ReferenceEquals(installed, collection.At(0))); Equal(3UL, collection.Append().Identity);
        collection.Install(OwnedList<EventInput>.Empty); Equal(0, collection.Count); Equal(false, collection.Pristine); Equal(4UL, collection.Append().Identity);
    }
}
