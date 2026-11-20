using OpenCiv2.Contracts;

namespace OpenCiv2.Formats;

public static partial class StateConversion
{
    /// <summary>Classic signed references use every negative value for absence.</summary>
    public static UnitSlot? DecodeUnitReference(short value) => value < 0 ? null : new((ushort)value);

    public static CitySlot? DecodeCityReference(short value) => value < 0 ? null : new((ushort)value);

    public static short EncodeUnitReference(UnitSlot? value) => value is { } slot ? checked((short)slot.Value) : (short)-1;

    public static short EncodeCityReference(CitySlot? value) => value is { } slot ? checked((short)slot.Value) : (short)-1;

    private static short PreserveAbsentReference(short output, short imported) =>
        output < 0 && imported < 0 ? imported : output;
}
