using OpenCiv2.Base;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Formats;

public static partial class StateConversion
{
    public static SavedLayout ToStored(in Layout layout)
    {
        var rectangles = new SavedRectangle[13];
        for (int i = 0; i < rectangles.Length; ++i)
        {
            var from = layout.Rectangles[i];
            rectangles[i] = new()
            {
                Left = unchecked((short)from.Left),
                Top = unchecked((short)from.Top),
                Right = unchecked((short)from.Right),
                Bottom = unchecked((short)from.Bottom)
            };
        }
        return new() { Valid = layout.Valid ? (short)1 : (short)0, Rectangles = new(rectangles) };
    }

    public static Layout ToLive(SavedLayout packed)
    {
        ArgumentNullException.ThrowIfNull(packed);
        if (packed.Rectangles.Count != 13)
        {
            throw new ArgumentException("Layout requires thirteen rectangles", nameof(packed));
        }

        var layout = new Layout { Valid = packed.Valid != 0 };
        for (int i = 0; i < 13; ++i)
        {
            var from = packed.Rectangles[i];
            layout.Rectangles[i] = new(from.Left, from.Top, from.Right, from.Bottom);
        }
        return layout;
    }

    private static short NameMarker(LeaderText name) => name.Custom ? (short)-1 : (short)1;

    public static StoredLeader ToStored(Leader live) => new()
    {
        Attack = live.Attack,
        Expand = live.Expand,
        Civilize = live.Civilize,
        TimesUsed = live.TimesUsed,
        Female = live.Female ? (byte)1 : (byte)0,
        CityNameIndex = live.CityNameIndex,
        Color = live.Color,
        CityStyle = live.CityStyle,
        LeaderName = NameMarker(live.LeaderName),
        Plural = NameMarker(live.Plural),
        Adjective = NameMarker(live.Adjective),
        Names = new(live.Names.Select(NameMarker)),
        Titles = new(live.Titles.Select(row => new OwnedList<short>(row.Select(NameMarker)))),
    };

    public static EventRecord ToStored(Event entry)
    {
        uint Present(int field) => entry.Strings[field].Present ? 1U : 0U;
        return ToStored(entry.Values) with
        {
            UnitName = Present(0),
            CityName = Present(1),
            WhoName = Present(2),
            WhomName = Present(3),
            Text = new(Enumerable.Range(Event.TextBegin, 20).Select(Present)),
            MoveOwnerName = Present(24),
            MoveUnitName = Present(25),
            AggressionWhomName = Present(26),
            AggressionWhoName = Present(27),
            CreateOwnerName = Present(28),
            CreateUnitName = Present(29),
            CreateHomecity = Present(30),
            AuxiliaryCivName = Present(31),
            AuxiliaryUnitName = Present(32),
            MoneyReceiverName = Present(33),
            WaveFile = Present(34),
        };
    }
}
