using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.State;
namespace OpenCiv2.Formats;

public static partial class StateConversion
{
    public static AdvanceSet DecodeAdvances(OwnedList<byte> bytes)
    {
        if (bytes.Count != 13)
        {
            throw new ArgumentException("Incorrect advances extent", nameof(bytes));
        }

        var result = new AdvanceSet();
        for (int i = 0; i < RuleLimits.Advances; ++i)
        {
            result = result.With(i, (bytes[i / 8] & (1 << (i % 8))) != 0);
        }

        return result;
    }
    public static OwnedList<byte> EncodeAdvances(AdvanceSet value)
    {
        byte[] bytes = new byte[13];
        for (int i = 0; i < RuleLimits.Advances; ++i)
        {
            if (value.Contains(i))
            {
                bytes[i / 8] |= (byte)(1 << (i % 8));
            }
        }

        return new(bytes);
    }
    public static ImprovementSet DecodeImprovements(OwnedList<byte> bytes)
    {
        if (bytes.Count != 5)
        {
            throw new ArgumentException("Incorrect improvements extent", nameof(bytes));
        }

        var result = new ImprovementSet();
        for (int i = 0; i < CityLimits.ImprovementSlots; ++i)
        {
            result = result.With(i, (bytes[i / 8] & (1 << (i % 8))) != 0);
        }

        return result;
    }
    public static OwnedList<byte> EncodeImprovements(ImprovementSet value)
    {
        byte[] bytes = new byte[5];
        for (int i = 0; i < CityLimits.ImprovementSlots; ++i)
        {
            if (value.Contains(i))
            {
                bytes[i / 8] |= (byte)(1 << (i % 8));
            }
        }

        return new(bytes);
    }
    public static SpecialistArray<SpecialistKind> DecodeSpecialists(uint bits)
    {
        SpecialistArray<SpecialistKind> result = default;
        for (int i = 0; i < CityLimits.Specialists; ++i)
        {
            result[i] = ((bits >> (2 * i)) & 3) switch
            { 0 => SpecialistKind.Unused, 1 => SpecialistKind.Entertainer, 2 => SpecialistKind.TaxCollector, _ => SpecialistKind.Scientist };
        }

        return result;
    }
    public static uint EncodeSpecialists(SpecialistArray<SpecialistKind> value)
    {
        uint bits = 0;
        for (int i = 0; i < CityLimits.Specialists; ++i)
        {
            bits |= (value[i] switch
            {
                SpecialistKind.Unused => 0U,
                SpecialistKind.Entertainer => 1U,
                SpecialistKind.TaxCollector => 2U,
                SpecialistKind.Scientist => 3U,
                _ => throw new ArgumentException("Unrecognized specialist", nameof(value))
            }) << (2 * i);
        }

        return bits;
    }
    public static CityWorkSet DecodeCityWork(uint bits)
    {
        var value = new CityWorkSet();
        for (int i = 0; i < CityLimits.WorkableTiles; ++i)
        {
            value = value.With(i, (bits & (1U << i)) != 0);
        }

        return value;
    }
    public static uint EncodeCityWork(CityWorkSet value, byte specialists = 0)
    {
        if (specialists > 63)
        {
            throw new ArgumentOutOfRangeException(nameof(specialists));
        }

        uint bits = (uint)specialists << 26;
        for (int i = 0; i < CityLimits.WorkableTiles; ++i)
        {
            if (value.Contains(i))
            {
                bits |= 1U << i;
            }
        }

        return bits;
    }
    public static EventTrigger DecodeEventTrigger(uint value) => value switch
    {
        0 => EventTrigger.None,
        1 => EventTrigger.UnitKilled,
        2 => EventTrigger.CityTaken,
        4 => EventTrigger.Turn,
        8 => EventTrigger.TurnInterval,
        16 => EventTrigger.Negotiation,
        32 => EventTrigger.ScenarioLoaded,
        64 => EventTrigger.RandomTurn,
        128 => EventTrigger.NoSchism,
        256 => EventTrigger.ReceivedTechnology,
        4096 => EventTrigger.DenyNegotiation,
        _ => EventTrigger.Unrecognized
    };
    public static uint EncodeEventTrigger(EventTrigger value) => value switch
    {
        EventTrigger.None => 0,
        EventTrigger.UnitKilled => 1,
        EventTrigger.CityTaken => 2,
        EventTrigger.Turn => 4,
        EventTrigger.TurnInterval => 8,
        EventTrigger.Negotiation => 16,
        EventTrigger.ScenarioLoaded => 32,
        EventTrigger.RandomTurn => 64,
        EventTrigger.NoSchism => 128,
        EventTrigger.ReceivedTechnology => 256,
        EventTrigger.DenyNegotiation => 4096,
        EventTrigger.Unrecognized => 0,
        _ => throw new ArgumentException("Unrecognized event trigger", nameof(value))
    };
}
