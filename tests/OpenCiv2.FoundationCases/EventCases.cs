using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.State;
using OpenCiv2.Formats;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // S02 §2: authored alias-sensitive inputs, not native-pointer/reference traces.
    private static Event AddEvent(EventCollection collection, string text)
    {
        var result = collection.Append(); result.Values.Actions = StateConversion.DecodeEventActions(0x80000021); result.Values.WhoCiv = -2; result.Values.MoneyAmount = int.MinValue;
        result.SetString(0, new(text)); return result;
    }
    private static void EventPresence()
    {
        EventString absent = default; Equal(false, absent.Present); Throws<ArgumentOutOfRangeException>(() => _ = absent.Value);
        var empty = new EventString(""); Equal(true, empty.Present); Equal(0, empty.Value.Length); Equal(false, empty == absent);
        var text = new EventString("é\0\u0001"); Equal("é\0\u0001", text.Value); var alias = text; alias.Value = "\u20ac\u0000\u0081";
        Equal(text, alias); Equal("\u20ac\u0000\u0081", text.Value); var duplicate = new EventString(text.Value); Equal(text.Value, duplicate.Value); Equal(false, duplicate == text);
        var state = new EventState { Loaded = true }; Equal(true, state.Collection.Pristine); var first = state.Collection.Append(); Equal(1UL, first.Identity); Equal(new EventValues(), first.Values);
        Equal(true, first.Strings.ToArray().All(s => !s.Present)); Throws<IndexOutOfRangeException>(() => first.SetString(35, text));
        state.Collection.Reset(); Equal(true, state.Loaded); Equal(false, state.Collection.Pristine); Equal(false, state.Collection.Contains(first)); Equal(true, state.Collection.Find(1) is null); Equal(2UL, state.Collection.Append().Identity);
        var other = new EventCollection(); Equal(1UL, other.Append().Identity); Equal(false, other.Contains(first)); Equal(false, other.Contains(null));
    }
    private static void EventCopyAliases()
    {
        var source = new EventCollection(); var destination = new EventCollection(); var a = AddEvent(source, "Shared");
        a.SetString(1, a.Strings[0]); a.SetString(4, a.Strings[0]); a.SetString(6, a.Strings[0]); a.SetString(34, new(new string('W', 300)));
        destination.CopyFrom(source); var b = destination.At(0); Equal(a.Values, b.Values); Equal(a.Strings[0].Value, b.Strings[0].Value);
        Equal(false, b.Strings[0] == a.Strings[0]); Equal(false, b.Strings[1] == b.Strings[0]); Equal(false, b.Strings[4] == b.Strings[0]); Equal(true, b.Strings[6] == a.Strings[0]); Equal(300, b.Strings[34].Value.Length);
        a.Strings[0].Value = "!hared"; Equal("Shared", b.Strings[0].Value); Equal("!hared", b.Strings[6].Value);
        a.Strings[0].Value = "A\u0000B"; var terminated = new EventCollection(); terminated.CopyFrom(source); Equal("A\0B", terminated.At(0).Strings[0].Value);
        Throws<ArgumentException>(() => source.CopyFrom(source)); Equal(1, source.Count);
        b.Values.MoveRectangle[3] = new(0, 34); Equal(default(MapPoint), a.Values.MoveRectangle[3]);
    }
    private static void EventCopyNonempty()
    {
        var source = new EventCollection(); var destination = new EventCollection(); var original = AddEvent(source, "Source"); var old = AddEvent(destination, "Destination"); old.Values.Turn = 99;
        old.SetString(1, new("Retained")); destination.CopyFrom(source); Equal(2, destination.Count); Equal(true, ReferenceEquals(old, destination.At(0))); Equal(99, old.Values.Turn);
        Equal("Source", old.Strings[0].Value); Equal(false, old.Strings[0] == original.Strings[0]); Equal("Retained", old.Strings[1].Value);
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
        var strings = new string?[35]; string source = "é\0ÿ"; strings[0] = source; strings[1] = "";
        var values = new EventValues { Turn = 27 }; values.CreateLocations[9] = new(0, 123);
        var input = new OwnedList<EventInput>([new(values, new(strings))]); source = "changed"; strings[0] = null;
        collection.Install(input); var installed = collection.At(0); Equal(2UL, installed.Identity); Equal(false, collection.Contains(original)); Equal(true, collection.Find(1) is null); Equal(values, installed.Values);
        Equal("é\0ÿ", installed.Strings[0].Value); Equal(true, installed.Strings[1].Present); Equal(false, installed.Strings[2].Present);
        Throws<ArgumentException>(() => collection.Install(new([input[0], new(new(), new(new string?[34]))])));
        Equal(1, collection.Count); Equal(true, ReferenceEquals(installed, collection.At(0))); Equal(3UL, collection.Append().Identity);
        collection.Install(OwnedList<EventInput>.Empty); Equal(0, collection.Count); Equal(false, collection.Pristine); Equal(4UL, collection.Append().Identity);
    }
}
